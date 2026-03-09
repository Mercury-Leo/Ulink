#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Ulink.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    [InitializeOnLoad]
    public static class UlinkGenerator
    {
        private const string GenerateFolder = "Generated/Ulink";
        private const string AssetsPath = "Assets";
        private const string UlinkFileName = "Ulink.g.cs";
        private const int TemplateVersion = 1;
        private const string RegistryResourcesFolder = "Assets/Generated/Ulink/Resources";
        internal const string RegistryAssetPath = "Assets/Generated/Ulink/Resources/UlinkAssetRegistry.asset";

        private static readonly Dictionary<string, string> AssemblyRootByName = new();

        static UlinkGenerator()
        {
            CompilationPipeline.compilationFinished += _ =>
            {
                if (!UlinkSettings.instance.DisableAutomaticGeneration)
                    GenerateControllers();
            };
        }

        [MenuItem("Tools/Leo's Tools/Ulink/Generate")]
        public static void GenerateControllers()
        {
            Generate();
        }

        private static void Generate()
        {
            BuildAssemblyRootCache();

            var uxmlElementTypes = new HashSet<Type>(TypeCache.GetTypesWithAttribute<UxmlElementAttribute>());

            var outputByRoot = new Dictionary<string, StringBuilder>();

            AppendGeneratedBlocksFor<UlinkElementAttribute>(uxmlElementTypes, outputByRoot, BuildComponentsFileContent);

            var anyChanged = false;

            foreach ((string? root, var builder) in outputByRoot)
            {
                anyChanged = WriteClassToFile(builder, root, anyChanged);
            }

            SyncRegistry();

            if (anyChanged)
            {
                AssetDatabase.Refresh();
            }
        }

        internal static void SyncRegistry()
        {
            var collectedGuids = new Dictionary<string, UnityEngine.Object>();
            var fieldCache = new Dictionary<Type, List<FieldInfo>>();

            string[] uxmlGuids = AssetDatabase.FindAssets("t:VisualTreeAsset");
            foreach (string uxmlGuid in uxmlGuids)
            {
                string uxmlPath = AssetDatabase.GUIDToAssetPath(uxmlGuid);
                if (string.IsNullOrEmpty(uxmlPath)) continue;

                string fullPath = Path.GetFullPath(uxmlPath).Replace('\\', '/');
                if (!File.Exists(fullPath)) continue;

                string text;
                try
                {
                    text = File.ReadAllText(fullPath);
                }
                catch
                {
                    continue;
                }

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(text);
                }
                catch
                {
                    continue;
                }

                foreach (var element in doc.Descendants())
                {
                    var attribute = element.Attributes()
                        .FirstOrDefault(a => a.Name.LocalName == "ulink-components");
                    if (attribute == null) continue;

                    string rawValue = attribute.Value;
                    if (string.IsNullOrEmpty(rawValue)) continue;

                    var allData = UlinkComponentsType.ParseAllData(rawValue);

                    foreach ((string? aqn, var fields) in allData)
                    {
                        var type = Type.GetType(aqn);
                        if (type == null) continue;

                        if (!fieldCache.TryGetValue(type, out var ulinkObjectFields))
                        {
                            ulinkObjectFields = type
                                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(field =>
                                    field.GetCustomAttribute<UlinkPropertyAttribute>() != null &&
                                    typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)).ToList();
                            fieldCache[type] = ulinkObjectFields;
                        }

                        foreach (var field in ulinkObjectFields)
                        {
                            if (!fields.TryGetValue(field.Name, out string? guidValue)) continue;
                            if (string.IsNullOrEmpty(guidValue)) continue;
                            if (collectedGuids.ContainsKey(guidValue)) continue;

                            string assetPath = AssetDatabase.GUIDToAssetPath(guidValue);
                            if (string.IsNullOrEmpty(assetPath)) continue;

                            var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
                            if (asset == null) continue;

                            collectedGuids[guidValue] = asset;
                        }
                    }
                }
            }

            var registry = AssetDatabase.LoadAssetAtPath<UlinkAssetRegistry>(RegistryAssetPath);

            bool changed = registry == null
                || registry.Entries.Count != collectedGuids.Count
                || registry.Entries.Any(e => !collectedGuids.ContainsKey(e.guid));

            if (!changed) return;

            if (registry == null)
            {
                if (!Directory.Exists(RegistryResourcesFolder))
                    Directory.CreateDirectory(RegistryResourcesFolder);

                registry = ScriptableObject.CreateInstance<UlinkAssetRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            registry.Entries.Clear();
            foreach ((string? guid, var asset) in collectedGuids)
                registry.Entries.Add(new AssetEntry { guid = guid, asset = asset });

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }

        private static bool WriteClassToFile(StringBuilder builder, string root, bool anyChanged)
        {
            string body = builder.ToString().Replace("\r\n", "\n");
            string content = BuildFileHeader() + body;

            string generatedPath = Path.Combine(root, GenerateFolder).Replace('\\', '/');
            if (!Directory.Exists(generatedPath))
            {
                Directory.CreateDirectory(generatedPath);
            }

            string filePath = Path.Combine(generatedPath, UlinkFileName).Replace('\\', '/');

            if (File.Exists(filePath))
            {
                string previous = File.ReadAllText(filePath);
                if (string.Equals(previous, content, StringComparison.Ordinal))
                {
                    return anyChanged;
                }
            }

            try
            {
                string temp = filePath + ".tmp";
                File.WriteAllText(temp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (File.Exists(filePath))
                {
                    File.Replace(temp, filePath, null);
                }
                else
                {
                    File.Move(temp, filePath);
                }

                anyChanged = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Ulink] Failed to create file for {root}: {e}.");
            }

            return anyChanged;
        }

        private static void AppendGeneratedBlocksFor<TAttribute>(HashSet<Type> uxmlElementTypes,
            Dictionary<string, StringBuilder> outputByRoot, Func<List<Type>, string> buildBlockForTypes)
            where TAttribute : Attribute
        {
            var eligible = FindEligibleTypes<TAttribute>(uxmlElementTypes);
            var byRoot = GroupByAssemblyRoot(eligible);

            foreach ((string? root, var types) in byRoot)
            {
                if (types.Count == 0)
                {
                    continue;
                }

                var sorted = types.OrderBy(type => type.Namespace).ThenBy(type => type.Name).ToList();

                string? block = buildBlockForTypes(sorted);

                if (string.IsNullOrEmpty(block))
                {
                    continue;
                }

                if (!outputByRoot.TryGetValue(root, out var builder))
                {
                    builder = new StringBuilder();
                    outputByRoot[root] = builder;
                }

                builder.Append(block);
            }
        }

        private static Dictionary<string, List<Type>> GroupByAssemblyRoot(List<Type> types)
        {
            var dict = new Dictionary<string, List<Type>>();
            foreach (var type in types)
            {
                string assemblyName = type.Assembly.GetName().Name!;
                string? root = AssemblyRootByName.GetValueOrDefault(assemblyName, AssetsPath);
                if (!dict.TryGetValue(root, out var list))
                {
                    list = new List<Type>();
                    dict[root] = list;
                }

                list.Add(type);
            }

            return dict;
        }

        private static List<Type> FindEligibleTypes<TAttribute>(HashSet<Type> uxmlElementTypes)
            where TAttribute : Attribute
        {
            var types = TypeCache.GetTypesWithAttribute<TAttribute>()
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    typeof(VisualElement).IsAssignableFrom(type) &&
                    uxmlElementTypes.Contains(type))
                .GroupBy(type => type.FullName)
                .Select(group => group.First())
                .ToList();

            var options = new HashSet<Type>(types);
            return types.Where(t => !HasBaseClass(t, options)).ToList();
        }

        private static void BuildAssemblyRootCache()
        {
            if (AssemblyRootByName.Count > 0)
            {
                return;
            }

            foreach (var assembly in CompilationPipeline.GetAssemblies())
            {
                string? name = assembly.name;
                string root = AssetsPath;

                try
                {
                    string? asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                    if (!string.IsNullOrEmpty(asmdefPath))
                    {
                        root = Path.GetDirectoryName(asmdefPath)!.Replace('\\', '/');
                    }
                }
                catch
                {
                    // ignore
                }

                AssemblyRootByName[name] = root;
            }

            AssemblyRootByName.TryAdd("Assembly-CSharp", AssetsPath);
            AssemblyRootByName.TryAdd("Assembly-CSharp-Editor", AssetsPath);
        }

        private static string BuildFileHeader()
        {
            var builder = new StringBuilder();
            builder.AppendLine("// Auto-generated by Ulink. Do not modify this file.");
            builder.AppendLine($"// TemplateVersion: {TemplateVersion}");
            builder.AppendLine("#nullable enable");
            builder.AppendLine(GenerateUsing());
            builder.AppendLine();

            return builder.ToString().Replace("\r\n", "\n");
        }

        private static string BuildComponentsFileContent(List<Type> types)
        {
            var builder = new StringBuilder();
            foreach (var type in types)
            {
                string namespaceName = type.Namespace ?? string.Empty;
                string className = type.Name;
                builder.AppendLine(GenerateComponentsClass(className, namespaceName));
            }

            return builder.ToString().Replace("\r\n", "\n");
        }

        private static string GenerateComponentsClass(string className, string? namespaceName)
        {
            var builder = new StringBuilder();
            var indent = 0;

            bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
            if (hasNamespace) { Line($"namespace {namespaceName}"); Line("{"); indent++; }

            Line($"public partial class {className}");
            Line("{");
            indent++;

            // Fields
            Line($"private {nameof(UlinkComponentsType)} _componentsType;");
            Line($"private readonly List<{nameof(IUlinkComponent<VisualElement>)}<{className}>> _typedComponents = new();");
            Line($"private readonly List<{nameof(IUlinkComponent<VisualElement>)}<{nameof(VisualElement)}>> _baseComponents = new();");
            Line();

            // Property
            Line($"[UxmlAttribute(\"ulink-components\")]");
            Line($"private {nameof(UlinkComponentsType)} ComponentsType");
            Line("{");
            indent++;

            Line("get => _componentsType;");
            Line("set");
            Line("{");
            indent++;

            // --- Reset block ---
            Line("// Reset components");
            Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            Line("if (_typedComponents.Count > 0 || _baseComponents.Count > 0)");
            Line("{");
            indent++;
            Directive("#if UNITY_EDITOR");
            Line($"if (!(_typedComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) ||");
            Line($"    _baseComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false))))");
            Line("{");
            indent++;
            Directive("#endif");
            Line($"UnregisterCallback<{nameof(AttachToPanelEvent)}>(OnComponentsPanelAttach);");
            Line($"UnregisterCallback<{nameof(DetachFromPanelEvent)}>(OnComponentsPanelDetach);");
            Line("foreach (var component in _typedComponents) component.OnDetach();");
            Line("foreach (var component in _baseComponents) component.OnDetach();");
            Directive("#if UNITY_EDITOR");
            indent--;
            Line("}");
            Directive("#endif");
            indent--;
            Line("}");
            Directive("#endif");
            Line();

            // --- Clear + early return ---
            Line("_typedComponents.Clear();");
            Line("_baseComponents.Clear();");
            Line();
            Line($"if (string.IsNullOrEmpty(value.{nameof(UlinkComponentsType.TypeNamesRaw)}))");
            Line("{");
            indent++;
            Line($"_componentsType = {nameof(UlinkComponentsType)}.{nameof(UlinkComponentsType.Empty)};");
            Line("return;");
            indent--;
            Line("}");
            Line();

            // --- try/catch ---
            Line("try");
            Line("{");
            indent++;
            Line("_componentsType = value;");
            Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            Line($"foreach (var type in value.{nameof(UlinkComponentsType.Types)})");
            Line("{");
            indent++;
            Line("if (type == null) continue;");
            Line();
            Line("object? instance = Activator.CreateInstance(type);");
            Line($"var instanceType = instance as {nameof(IUlinkComponent<VisualElement>)}<{className}>;");
            Line($"var baseComp = instanceType == null ? instance as {nameof(IUlinkComponent<VisualElement>)}<{nameof(VisualElement)}> : null;");
            Line();
            Line("if (instanceType == null && baseComp == null) continue;");
            Line();
            Directive("#if UNITY_EDITOR");
            Line($"if (type.IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) continue;");
            Directive("#endif");
            Line("if (instanceType != null) _typedComponents.Add(instanceType);");
            Line("else _baseComponents.Add(baseComp!);");
            Line();
            Line($"{nameof(UlinkPropertyInjector)}.{nameof(UlinkPropertyInjector.Inject)}(instance!, value, type.AssemblyQualifiedName);");
            Line();
            Line("instanceType?.Setup(this);");
            Line("baseComp?.Setup(this);");
            indent--;
            Line("}");
            Line();
            Line("if (panel != null)");
            Line("{");
            indent++;
            Line("foreach (var component in _typedComponents) component.OnAttach();");
            Line("foreach (var component in _baseComponents) component.OnAttach();");
            indent--;
            Line("}");
            Line();
            Line("if (_typedComponents.Count > 0 || _baseComponents.Count > 0)");
            Line("{");
            indent++;
            Line($"RegisterCallback<{nameof(AttachToPanelEvent)}>(OnComponentsPanelAttach);");
            Line($"RegisterCallback<{nameof(DetachFromPanelEvent)}>(OnComponentsPanelDetach);");
            indent--;
            Line("}");
            Directive("#endif");
            indent--;
            Line("}");
            Line("catch (Exception e)");
            Line("{");
            indent++;
            Line("_typedComponents.Clear();");
            Line("_baseComponents.Clear();");
            Line($"_componentsType = {nameof(UlinkComponentsType)}.{nameof(UlinkComponentsType.Empty)};");
            Line($"Debug.LogWarning($\"[Ulink] Failed to initialize Ulink Components: {{e}}\");");
            indent--;
            Line("}");

            indent--;
            Line("}"); // set
            indent--;
            Line("}"); // property
            Line();

            // --- Callback methods ---
            Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            Line($"private void OnComponentsPanelAttach({nameof(AttachToPanelEvent)} panelEvent)");
            Line("{");
            indent++;
            Line("if (panelEvent.target != this) return;");
            Line("foreach (var component in _typedComponents) component.OnAttach();");
            Line("foreach (var component in _baseComponents) component.OnAttach();");
            indent--;
            Line("}");
            Line();
            Line($"private void OnComponentsPanelDetach({nameof(DetachFromPanelEvent)} panelEvent)");
            Line("{");
            indent++;
            Line("if (panelEvent.target != this) return;");
            Line("foreach (var component in _typedComponents) component.OnDetach();");
            Line("foreach (var component in _baseComponents) component.OnDetach();");
            indent--;
            Line("}");
            Directive("#endif");

            indent--;
            Line("}"); // class

            if (!hasNamespace) return builder.ToString().Replace("\r\n", "\n");
            
            indent--; Line("}"); // namespace

            return builder.ToString().Replace("\r\n", "\n");

            void Directive(string text) => builder.AppendLine(text);

            void Line(string text = "") =>
                builder.AppendLine(text.Length == 0 ? string.Empty : new string(' ', indent * 4) + text);
        }

        private static string GenerateUsing()
        {
            return @"
using System;
using System.Collections.Generic;
using Ulink.Runtime;
using UnityEngine;
using UnityEngine.UIElements;";
        }

        private static bool HasBaseClass(Type type, HashSet<Type> options)
        {
            var baseClass = type.BaseType;
            while (baseClass != null && baseClass != typeof(object) && baseClass != typeof(VisualElement))
            {
                if (options.Contains(baseClass))
                {
                    return true;
                }

                baseClass = baseClass.BaseType;
            }

            return false;
        }
    }

    internal class UlinkRegistryWatcher : AssetPostprocessor
    {
        private const string UxmlFileEnding = ".uxml";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (UlinkSettings.instance.DisableAutomaticGeneration) return;

            bool shouldSync = deletedAssets.Contains(UlinkGenerator.RegistryAssetPath)
                || importedAssets.Any(path => path.EndsWith(UxmlFileEnding, StringComparison.OrdinalIgnoreCase))
                || deletedAssets.Any(path => path.EndsWith(UxmlFileEnding, StringComparison.OrdinalIgnoreCase))
                || movedAssets.Any(path => path.EndsWith(UxmlFileEnding, StringComparison.OrdinalIgnoreCase))
                || movedFromAssetPaths.Any(path => path.EndsWith(UxmlFileEnding, StringComparison.OrdinalIgnoreCase));

            if (!shouldSync) return;
            UlinkGenerator.SyncRegistry();
            AssetDatabase.SaveAssets();
        }
    }
}