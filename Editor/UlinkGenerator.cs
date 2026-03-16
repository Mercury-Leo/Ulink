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
        private const int TemplateVersion = 2;
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
            var collectedGuids = CollectAssetGuidsFromUxml();
            UpdateRegistryAsset(collectedGuids);
        }

        /// <summary>
        /// Scans every UXML file in the project for ulink-components attributes, then collects
        /// the GUIDs of any UnityEngine.Object fields referenced by [UlinkSerializable] so they can
        /// be included in the UlinkAssetRegistry for runtime resolution (where AssetDatabase is unavailable).
        /// </summary>
        private static Dictionary<string, UnityEngine.Object> CollectAssetGuidsFromUxml()
        {
            var collectedGuids = new Dictionary<string, UnityEngine.Object>();
            var fieldCache = new Dictionary<Type, FieldInfo[]>(); // local cache — only Object-typed [UlinkSerializable] fields

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
                            ulinkObjectFields = UlinkFieldDiscovery.GetUlinkSerializableFields(type)
                                .Where(f => typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                                .ToArray();
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

            return collectedGuids;
        }

        private static void UpdateRegistryAsset(Dictionary<string, UnityEngine.Object> collectedGuids)
        {
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
            var writer = new CodeWriter();

            bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
            if (hasNamespace) writer.OpenBlock($"namespace {namespaceName}");

            writer.OpenBlock($"public partial class {className}");

            // Fields
            writer.Line($"private {nameof(UlinkComponentsType)} _componentsType;");
            writer.Line(
                $"private readonly List<{nameof(IUlinkComponent<VisualElement>)}<{className}>> _typedComponents = new();");
            writer.Line(
                $"private readonly List<{nameof(IUlinkComponent<VisualElement>)}<{nameof(VisualElement)}>> _baseComponents = new();");
            writer.Line();

            // Property
            writer.Line($"[UxmlAttribute(\"ulink-components\")]");
            writer.OpenBlock($"private {nameof(UlinkComponentsType)} ComponentsType");

            writer.Line("get => _componentsType;");
            writer.OpenBlock("set");

            // --- Reset block ---
            writer.Line("// Reset components");
            writer.Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            writer.OpenBlock("if (_typedComponents.Count > 0 || _baseComponents.Count > 0)");
            writer.Directive("#if UNITY_EDITOR");
            writer.Line(
                $"if (!(_typedComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) ||");
            writer.Line(
                $"    _baseComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false))))");
            writer.OpenBlock();
            writer.Directive("#endif");
            writer.Line($"UnregisterCallback<{nameof(AttachToPanelEvent)}>(OnComponentsPanelAttach);");
            writer.Line($"UnregisterCallback<{nameof(DetachFromPanelEvent)}>(OnComponentsPanelDetach);");
            writer.Line("foreach (var component in _typedComponents) component.OnDetach();");
            writer.Line("foreach (var component in _baseComponents) component.OnDetach();");
            writer.Directive("#if UNITY_EDITOR");
            writer.CloseBlock(); // anonymous block
            writer.Directive("#endif");
            writer.CloseBlock(); // if
            writer.Directive("#endif");
            writer.Line();

            // --- Clear + early return ---
            writer.Line("_typedComponents.Clear();");
            writer.Line("_baseComponents.Clear();");
            writer.Line();
            writer.OpenBlock($"if (string.IsNullOrEmpty(value.{nameof(UlinkComponentsType.TypeNamesRaw)}))");
            writer.Line($"_componentsType = {nameof(UlinkComponentsType)}.{nameof(UlinkComponentsType.Empty)};");
            writer.Line("return;");
            writer.CloseBlock();
            writer.Line();

            // --- try/catch ---
            writer.OpenBlock("try");
            writer.Line("_componentsType = value;");
            writer.Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            writer.OpenBlock($"foreach (var type in value.{nameof(UlinkComponentsType.Types)})");
            writer.Line("if (type == null) continue;");
            writer.Line();
            writer.Line("object? instance = Activator.CreateInstance(type);");
            writer.Line($"var instanceType = instance as {nameof(IUlinkComponent<VisualElement>)}<{className}>;");
            writer.Line(
                $"var baseComp = instanceType == null ? instance as {nameof(IUlinkComponent<VisualElement>)}<{nameof(VisualElement)}> : null;");
            writer.Line();
            writer.Line("if (instanceType == null && baseComp == null) continue;");
            writer.Line();
            writer.Directive("#if UNITY_EDITOR");
            writer.Line($"if (type.IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) continue;");
            writer.Directive("#endif");
            writer.Line("if (instanceType != null) _typedComponents.Add(instanceType);");
            writer.Line("else _baseComponents.Add(baseComp!);");
            writer.Line();
            writer.Line(
                $"{nameof(UlinkSerializableInjector)}.{nameof(UlinkSerializableInjector.Inject)}(instance!, value, type.AssemblyQualifiedName);");
            writer.Line();
            writer.Line("instanceType?.Setup(this);");
            writer.Line("baseComp?.Setup(this);");
            writer.CloseBlock(); // foreach
            writer.Line();
            writer.OpenBlock("if (panel != null)");
            writer.Line("foreach (var component in _typedComponents) component.OnAttach();");
            writer.Line("foreach (var component in _baseComponents) component.OnAttach();");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock("if (_typedComponents.Count > 0 || _baseComponents.Count > 0)");
            writer.Line($"RegisterCallback<{nameof(AttachToPanelEvent)}>(OnComponentsPanelAttach);");
            writer.Line($"RegisterCallback<{nameof(DetachFromPanelEvent)}>(OnComponentsPanelDetach);");
            writer.CloseBlock();
            writer.Directive("#endif");
            writer.CloseBlock(); // try
            writer.OpenBlock("catch (Exception e)");
            writer.Line("_typedComponents.Clear();");
            writer.Line("_baseComponents.Clear();");
            writer.Line($"_componentsType = {nameof(UlinkComponentsType)}.{nameof(UlinkComponentsType.Empty)};");
            writer.Line($"Debug.LogWarning($\"[Ulink] Failed to initialize Ulink Components: {{e}}\");");
            writer.CloseBlock(); // catch

            writer.CloseBlock(); // set
            writer.CloseBlock(); // property
            writer.Line();

            // --- Callback methods ---
            writer.Directive("#if !UNITY_EDITOR || ULINK_EDITOR");
            writer.OpenBlock($"private void OnComponentsPanelAttach({nameof(AttachToPanelEvent)} panelEvent)");
            writer.Line("if (panelEvent.target != this) return;");
            writer.Line("foreach (var component in _typedComponents) component.OnAttach();");
            writer.Line("foreach (var component in _baseComponents) component.OnAttach();");
            writer.CloseBlock();
            writer.Line();
            writer.OpenBlock($"private void OnComponentsPanelDetach({nameof(DetachFromPanelEvent)} panelEvent)");
            writer.Line("if (panelEvent.target != this) return;");
            writer.Line("foreach (var component in _typedComponents) component.OnDetach();");
            writer.Line("foreach (var component in _baseComponents) component.OnDetach();");
            writer.CloseBlock();
            writer.Directive("#endif");

            writer.Line();
            GenerateComponentLookupMethods(writer);

            writer.CloseBlock(); // class

            if (hasNamespace) writer.CloseBlock(); // namespace

            return writer.ToString();
        }

        private static void GenerateComponentLookupMethods(CodeWriter writer)
        {
            // TryGetComponent<TComponent> — first match, no allocation
            writer.OpenBlock("public bool TryGetComponent<TComponent>([NotNullWhen(true)] out TComponent? component) where TComponent : class");
            writer.OpenBlock("foreach (var c in _typedComponents)");
            writer.Line("if (c is TComponent match) { component = match; return true; }");
            writer.CloseBlock();
            writer.OpenBlock("foreach (var c in _baseComponents)");
            writer.Line("if (c is TComponent match) { component = match; return true; }");
            writer.CloseBlock();
            writer.Line("component = null;");
            writer.Line("return false;");
            writer.CloseBlock();
            writer.Line();

            // GetComponent<TComponent> — returns first match or null
            writer.OpenBlock("public TComponent? GetComponent<TComponent>() where TComponent : class");
            writer.Line("TryGetComponent(out TComponent? component);");
            writer.Line("return component;");
            writer.CloseBlock();
            writer.Line();

            // GetComponents<TComponent>() — returns all matches as array
            writer.OpenBlock("public TComponent[] GetComponents<TComponent>() where TComponent : class");
            writer.Line("var results = new List<TComponent>();");
            writer.Line("GetComponents(results);");
            writer.Line("return results.ToArray();");
            writer.CloseBlock();
            writer.Line();

            // GetComponents<TComponent>(List<TComponent>) — fill-in-place, no allocation
            writer.OpenBlock("public void GetComponents<TComponent>(List<TComponent> results) where TComponent : class");
            writer.OpenBlock("foreach (var c in _typedComponents)");
            writer.Line("if (c is TComponent match) results.Add(match);");
            writer.CloseBlock();
            writer.OpenBlock("foreach (var c in _baseComponents)");
            writer.Line("if (c is TComponent match) results.Add(match);");
            writer.CloseBlock();
            writer.CloseBlock();
        }

        private static string GenerateUsing()
        {
            return @"
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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