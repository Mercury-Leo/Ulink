#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private const string GenerateFolder = "Generated/Controller";
        private const string AssetsPath = "Assets";
        private const string UlinkFileName = "Ulink.g.cs";
        private const int TemplateVersion = 1;

        private static readonly Dictionary<string, string> AssemblyRootByName = new();

        static UlinkGenerator()
        {
            CompilationPipeline.compilationFinished += _ => GenerateControllers();
        }

        [MenuItem("Tools/Leo's Tools/Ulink/Generate Controllers")]
        public static void GenerateControllers()
        {
            Generate();
        }

        private static void Generate()
        {
            BuildAssemblyRootCache();

            var uxmlElementTypes = new HashSet<Type>(TypeCache.GetTypesWithAttribute<UxmlElementAttribute>());

            var outputByRoot = new Dictionary<string, StringBuilder>();

            AppendGeneratedBlocksFor<UlinkControllerAttribute>(uxmlElementTypes, outputByRoot,
                BuildControllerFileContent);

            AppendGeneratedBlocksFor<UlinkFactoryAttribute>(uxmlElementTypes, outputByRoot, BuildFactoryFileContent);

            var anyChanged = false;

            foreach ((string? root, var builder) in outputByRoot)
            {
                anyChanged = WriteClassToFile(builder, root, anyChanged);
            }

            if (anyChanged)
            {
                AssetDatabase.Refresh();
            }
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
                    // ignore;
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

        private static string BuildControllerFileContent(List<Type> types)
        {
            var builder = new StringBuilder();
            foreach (var type in types)
            {
                string namespaceName = type.Namespace ?? string.Empty;
                string className = type.Name;
                builder.AppendLine(GenerateControllerClass(className, namespaceName));
            }

            return builder.ToString().Replace("\r\n", "\n");
        }

        private static string BuildFactoryFileContent(List<Type> types)
        {
            var builder = new StringBuilder();
            foreach (var type in types)
            {
                string namespaceName = type.Namespace ?? string.Empty;
                string className = type.Name;
                builder.AppendLine(GenerateFactoryClass(className, namespaceName));
            }

            return builder.ToString().Replace("\r\n", "\n");
        }

        private static string GenerateUsing()
        {
            return @"
using System;
using Ulink.Runtime;
using UnityEngine;
using UnityEngine.UIElements;";
        }

        private static string GenerateFactoryClass(string className, string namespaceName)
        {
            return $@"{(string.IsNullOrEmpty(namespaceName) ? string.Empty : $"namespace {namespaceName}\n{{")} 
    public partial class {className} 
    {{
        private UlinkFactory _factory;
        private IUlinkController _factoryController;

        [UxmlAttribute]
        private UlinkFactory Factory
        {{
            get => _factory;
            set
            {{
                if (_factoryController is not null)
                {{
                    UnregisterCallback<AttachToPanelEvent>(OnFactoryPanelAttach);
                    UnregisterCallback<DetachFromPanelEvent>(OnFactoryPanelDetach);
                    _factoryController.Unbind();
                }}

                if (value == null)
                {{
                    _factory = null;
                    _factoryController = null;
                    return;
                }}

                try
                {{
                    _factory = value;
                    _factoryController = _factory.CreateController();
                    _factoryController?.OnSerialize(this);

                    if (panel != null)
                    {{
                        _factoryController?.Bind();
                    }}

                    RegisterCallback<AttachToPanelEvent>(OnFactoryPanelAttach);
                    RegisterCallback<DetachFromPanelEvent>(OnFactoryPanelDetach);
                }}
                catch (Exception e)
                {{
                    _factory = null;
                    _factoryController = null;
                    Debug.LogWarning($""[Ulink] Failed to initialize Ulink Factory: {{e}}"");
                }}
            }}
        }}

        private void OnFactoryPanelAttach(AttachToPanelEvent panelEvent)
        {{
            if(panelEvent.target == this)
            {{
                _factoryController?.Bind();
            }}
        }}

        private void OnFactoryPanelDetach(DetachFromPanelEvent panelEvent)
        {{
            if(panelEvent.target == this)
            {{
                _factoryController?.Unbind();
            }}
        }}
    }}
{(string.IsNullOrEmpty(namespaceName) ? string.Empty : "}")}
";
        }

        private static string GenerateControllerClass(string className, string? namespaceName)
        {
            return $@"{(string.IsNullOrEmpty(namespaceName) ? string.Empty : $"namespace {namespaceName}\n{{")}
    public partial class {className}
    {{
        private IUlinkController? _controller;
        private ControllerType _controllerType;

        [UxmlAttribute]
        private ControllerType ControllerType
        {{
            get => _controllerType;
            set
            {{
                if (_controller is not null)
                {{
                    UnregisterCallback<AttachToPanelEvent>(OnControllerPanelAttach);
                    UnregisterCallback<DetachFromPanelEvent>(OnControllerPanelDetach);
                    _controller.Unbind();
                }}

                if (value.Type == null)
                {{
                    _controller = null;
                    _controllerType = ControllerType.Empty;
                    return;
                }}

                try
                {{
                     _controllerType = value;
                    _controller = Activator.CreateInstance(_controllerType.Type!) as IUlinkController;
                    _controller?.OnSerialize(this);
                    
                    if (panel != null)
                    {{
                        _controller?.Bind();
                    }}

                    RegisterCallback<AttachToPanelEvent>(OnControllerPanelAttach);
                    RegisterCallback<DetachFromPanelEvent>(OnControllerPanelDetach);
                }}
                catch (Exception e)
                {{
                    _controller = null;
                    _controllerType = ControllerType.Empty;
                    Debug.LogWarning($""[Ulink] Failed to initialize Ulink Controller: {{e}}"");
                }}
            }}
        }}

        private void OnControllerPanelAttach(AttachToPanelEvent panelEvent)
        {{
            if(panelEvent.target == this)
            {{
                _controller?.Bind();
            }}
        }}

        private void OnControllerPanelDetach(DetachFromPanelEvent panelEvent)
        {{
            if(panelEvent.target == this)
            {{
                _controller?.Unbind();
            }}
        }}
    }}
{(string.IsNullOrEmpty(namespaceName) ? string.Empty : "}")}
";
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
}