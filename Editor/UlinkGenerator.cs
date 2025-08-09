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
            GenerateControllers(UlinkSettings.instance.TargetFolder);
        }

        public static void GenerateControllers(string _)
        {
            BuildAssemblyRootCache();

            var uxmlElementTypes = new HashSet<Type>(TypeCache.GetTypesWithAttribute<UxmlElementAttribute>());

            var controllerTypes = TypeCache.GetTypesWithAttribute<UlinkAttribute>()
                .Where(type => type.IsClass
                               && !type.IsAbstract
                               && typeof(VisualElement).IsAssignableFrom(type)
                               && uxmlElementTypes.Contains(type))
                .GroupBy(type => type.FullName)
                .Select(group => group.First())
                .ToList();

            var options = new HashSet<Type>(controllerTypes);
            controllerTypes = controllerTypes.Where(type => !HasBaseClass(type, options)).ToList();

            var byRoot = new Dictionary<string, List<Type>>();
            foreach (var type in controllerTypes)
            {
                string asmName = type.Assembly.GetName().Name!;
                string? root = AssemblyRootByName.GetValueOrDefault(asmName, AssetsPath);
                (byRoot.TryGetValue(root, out var list) ? list : byRoot[root] = new List<Type>()).Add(type);
            }

            var anyChanged = false;

            foreach ((string? root, var types) in byRoot)
            {
                if (types.Count == 0)
                {
                    continue;
                }

                string generatedPath = Path.Combine(root, GenerateFolder).Replace('\\', '/');
                if (!Directory.Exists(generatedPath))
                {
                    Directory.CreateDirectory(generatedPath);
                }

                string filePath = Path.Combine(generatedPath, UlinkFileName).Replace('\\', '/');

                var sorted = types.OrderBy(type => type.Namespace).ThenBy(type => type.Name).ToList();
                string newContent = BuildFileContent(sorted);

                if (File.Exists(filePath))
                {
                    string previousContent = File.ReadAllText(filePath);
                    if (string.Equals(previousContent, newContent, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                try
                {
                    string temp = filePath + ".tmp";
                    File.WriteAllText(temp, newContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    if (File.Exists(filePath))
                    {
                        File.Replace(temp, filePath, null);
                    }
                    else
                    {
                        File.Move(temp, filePath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Ulink] Failed to create file for {root}: {e}.");
                    continue;
                }

                anyChanged = true;
            }

            if (anyChanged)
            {
                AssetDatabase.Refresh();
            }
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

        private static string BuildFileContent(List<Type> types)
        {
            string manifest = string.Join("|", types.Select(type => type.FullName));
            string manifestHash = HashingUtility.HashString(manifest) ?? "0";

            var builder = new StringBuilder();
            builder.AppendLine("// Auto-generated by Ulink. Do not modify this file.");
            builder.AppendLine($"// TemplateVersion: {TemplateVersion}");
            builder.AppendLine($"// ManifestHash: {manifestHash}");
            builder.AppendLine("#nullable enable");
            builder.AppendLine(GenerateUsing());
            builder.AppendLine();

            foreach (var type in types)
            {
                string ns = type.Namespace ?? string.Empty;
                string className = type.Name;
                builder.AppendLine(GenerateClass(className, ns));
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

        private static string GenerateClass(string className, string? namespaceName)
        {
            return $@"{(string.IsNullOrEmpty(namespaceName) ? string.Empty : $"namespace {namespaceName}\n{{")}
    public partial class {className} 
    {{
        private IUIController? _controller;
        private ControllerType _controllerType;

        [UxmlAttribute]
        private ControllerType ControllerType
        {{
            get => _controllerType;
            set
            {{
                if (value.Type == null)
                {{
                    _controller = null;
                    _controllerType = ControllerType.Empty;
                    return;
                }}

                try
                {{
                    _controllerType = value;
                    _controller = Activator.CreateInstance(_controllerType.Type!) as IUIController;
                    _controller?.Initialize(this);
                }}
                catch (Exception e)
                {{
                    _controller = null;
                    _controllerType = ControllerType.Empty;
                    Debug.LogWarning($""Failed to initialize Ulink Controller: {{e}}"");
                }}
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