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

                        var ulinkObjectFields = type
                            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(field =>
                                field.GetCustomAttribute<UlinkPropertyAttribute>() != null &&
                                typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)).ToList();

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
                .Select(g => g.First())
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
            return $@"{(string.IsNullOrEmpty(namespaceName) ? string.Empty : $"namespace {namespaceName}\n{{")}
    public partial class {className}
    {{
        private UlinkComponentsType _componentsType;
        private readonly List<IUlinkComponent<{className}>> _typedComponents = new();
        private readonly List<IUlinkComponent<VisualElement>> _baseComponents = new();

        [UxmlAttribute(""ulink-components"")]
        private UlinkComponentsType ComponentsType
        {{
            get => _componentsType;
            set
            {{
                // Reset components
#if !UNITY_EDITOR || ULINK_EDITOR
                if (_typedComponents.Count > 0 || _baseComponents.Count > 0)
                {{
#if UNITY_EDITOR
                    if (!(_typedComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) ||
                        _baseComponents.Exists(component => !component.GetType().IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false))))
                    {{
#endif
                        UnregisterCallback<AttachToPanelEvent>(OnComponentsPanelAttach);
                        UnregisterCallback<DetachFromPanelEvent>(OnComponentsPanelDetach);
                        foreach (var component in _typedComponents) component.OnDetach();
                        foreach (var component in _baseComponents) component.OnDetach();
#if UNITY_EDITOR
                    }}
#endif
                }}
#endif

                _typedComponents.Clear();
                _baseComponents.Clear();

                if (string.IsNullOrEmpty(value.TypeNamesRaw))
                {{
                    _componentsType = UlinkComponentsType.Empty;
                    return;
                }}

                try
                {{
                    _componentsType = value;
#if !UNITY_EDITOR || ULINK_EDITOR
                    foreach (var type in value.Types)
                    {{
                        if (type == null) continue;

                        object? instance = Activator.CreateInstance(type);
                        var instanceType = instance as IUlinkComponent<{className}>;
                        var baseComp = instanceType == null ? instance as IUlinkComponent<VisualElement> : null;

                        if (instanceType == null && baseComp == null) continue;

#if UNITY_EDITOR
                        if (type.IsDefined(typeof({nameof(UlinkRuntimeOnlyAttribute)}), false)) continue;
#endif
                        if (instanceType != null) _typedComponents.Add(instanceType);
                        else _baseComponents.Add(baseComp!);

                        UlinkPropertyInjector.Inject(instance!, value, type.AssemblyQualifiedName);

                        instanceType?.Setup(this);
                        baseComp?.Setup(this);
                    }}

                    if (panel != null)
                    {{
                        foreach (var component in _typedComponents) component.OnAttach();
                        foreach (var component in _baseComponents) component.OnAttach();
                    }}

                    if (_typedComponents.Count > 0 || _baseComponents.Count > 0)
                    {{
                        RegisterCallback<AttachToPanelEvent>(OnComponentsPanelAttach);
                        RegisterCallback<DetachFromPanelEvent>(OnComponentsPanelDetach);
                    }}
#endif
                }}
                catch (Exception e)
                {{
                    _typedComponents.Clear();
                    _baseComponents.Clear();
                    _componentsType = UlinkComponentsType.Empty;
                    Debug.LogWarning($""[Ulink] Failed to initialize Ulink Components: {{e}}"");
                }}
            }}
        }}

#if !UNITY_EDITOR || ULINK_EDITOR
        private void OnComponentsPanelAttach(AttachToPanelEvent panelEvent)
        {{
            if (panelEvent.target != this) return;
            foreach (var component in _typedComponents) component.OnAttach();
            foreach (var component in _baseComponents) component.OnAttach();
        }}

        private void OnComponentsPanelDetach(DetachFromPanelEvent panelEvent)
        {{
            if (panelEvent.target != this) return;
            foreach (var component in _typedComponents) component.OnDetach();
            foreach (var component in _baseComponents) component.OnDetach();
        }}
#endif
    }}
{(string.IsNullOrEmpty(namespaceName) ? string.Empty : "}")}
";
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