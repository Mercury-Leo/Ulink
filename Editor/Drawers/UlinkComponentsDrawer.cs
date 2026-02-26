using System;
using System.Collections.Generic;
using System.Linq;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    [CustomPropertyDrawer(typeof(UlinkComponentsType))]
    public class UlinkComponentsDrawer : PropertyDrawer
    {
        private const char Separator = ';';

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    paddingTop = new StyleLength(new Length(4, LengthUnit.Pixel)),
                    paddingBottom = new StyleLength(new Length(4, LengthUnit.Pixel))
                }
            };

            var rawProp = property.FindPropertyRelative(nameof(UlinkComponentsType.TypeNamesRaw));

            var allTypes = GetAllComponentTypes();
            var elementType = GetElementType();
            var exactMatchSet = elementType != null
                ? new HashSet<Type>(allTypes.Where(t => IsExactMatch(t, elementType)))
                : new HashSet<Type>();
            var compatibleSet = elementType != null
                ? new HashSet<Type>(allTypes.Where(t => !exactMatchSet.Contains(t) && IsCompatibleComponent(t, elementType)))
                : new HashSet<Type>();

            string[] GetCurrentNames() => string.IsNullOrEmpty(rawProp.stringValue)
                ? Array.Empty<string>()
                : rawProp.stringValue.Split(Separator);

            void SetNames(string[] names)
            {
                rawProp.stringValue = string.Join(Separator, names);
                property.serializedObject.ApplyModifiedProperties();
            }

            var listContainer = new VisualElement();
            container.Add(listContainer);

            var searchField = new TextField
            {
                name = "component-search",
                isDelayed = true,
                textEdition = { placeholder = "Search Components" },
                style = { marginBottom = new StyleLength(new Length(4, LengthUnit.Pixel)) }
            };
            container.Add(searchField);

            var addButton = new Button { text = "Add Component", style = { flexGrow = 1, height = 20 } };
            container.Add(addButton);

            void RebuildList()
            {
                listContainer.Clear();
                var names = GetCurrentNames();
                for (int i = 0; i < names.Length; i++)
                {
                    var index = i;
                    var typeName = names[i];
                    var resolvedType = allTypes.FirstOrDefault(t => t.AssemblyQualifiedName == typeName);

                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            justifyContent = Justify.SpaceBetween,
                            marginBottom = new StyleLength(new Length(2, LengthUnit.Pixel))
                        }
                    };

                    string labelText;
                    if (resolvedType != null)
                    {
                        if (exactMatchSet.Contains(resolvedType))
                            labelText = $"★★ {resolvedType.Name}";
                        else if (compatibleSet.Contains(resolvedType))
                            labelText = $"★ {resolvedType.Name}";
                        else
                            labelText = resolvedType.Name;
                    }
                    else
                    {
                        labelText = typeName;
                    }

                    var label = new Label(labelText)
                    {
                        style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft }
                    };

                    var removeButton = new Button(() =>
                    {
                        SetNames(GetCurrentNames().Where((_, j) => j != index).ToArray());
                        RebuildList();
                    })
                    {
                        text = "✕",
                        style = { width = 20, height = 20 }
                    };

                    row.Add(label);
                    row.Add(removeButton);
                    listContainer.Add(row);
                }
            }

            RebuildList();

            addButton.clicked += () =>
            {
                var filter = searchField.value;
                var filtered = string.IsNullOrEmpty(filter)
                    ? allTypes
                    : allTypes.Where(t => t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                var currentNames = new HashSet<string>(GetCurrentNames());

                var exactAvailable     = filtered.Where(t => exactMatchSet.Contains(t)  && !currentNames.Contains(t.AssemblyQualifiedName)).ToList();
                var compatibleAvailable = filtered.Where(t => compatibleSet.Contains(t)  && !currentNames.Contains(t.AssemblyQualifiedName)).ToList();
                var incompatibleAll    = filtered.Where(t => !exactMatchSet.Contains(t) && !compatibleSet.Contains(t) && !currentNames.Contains(t.AssemblyQualifiedName)).ToList();

                var menu = new GenericMenu();

                foreach (var type in exactAvailable)
                {
                    var captured = type;
                    menu.AddItem(new GUIContent($"★★ {captured.Name}"), false, () =>
                    {
                        var current = GetCurrentNames();
                        if (!current.Contains(captured.AssemblyQualifiedName))
                            SetNames(current.Append(captured.AssemblyQualifiedName).ToArray());
                        RebuildList();
                    });
                }

                if (exactAvailable.Count > 0 && compatibleAvailable.Count > 0)
                    menu.AddSeparator("");

                foreach (var type in compatibleAvailable)
                {
                    var captured = type;
                    menu.AddItem(new GUIContent($"★ {captured.Name}"), false, () =>
                    {
                        var current = GetCurrentNames();
                        if (!current.Contains(captured.AssemblyQualifiedName))
                            SetNames(current.Append(captured.AssemblyQualifiedName).ToArray());
                        RebuildList();
                    });
                }

                if (incompatibleAll.Count > 0 && (exactAvailable.Count > 0 || compatibleAvailable.Count > 0))
                    menu.AddSeparator("");

                foreach (var type in incompatibleAll)
                    menu.AddDisabledItem(new GUIContent(type.Name));

                if (exactAvailable.Count + compatibleAvailable.Count + incompatibleAll.Count == 0)
                    menu.AddDisabledItem(new GUIContent("No components available"));

                menu.DropDown(addButton.worldBound);
            };

            return container;
        }

        private Type GetElementType()
        {
            var declaringType = fieldInfo?.DeclaringType;
            if (declaringType == null) return null;

            // Direct: field declared inside a VisualElement subclass
            if (typeof(VisualElement).IsAssignableFrom(declaringType))
                return declaringType;

            // Nested: field is inside a generated nested class (e.g. UxmlSerializedData)
            // whose outer type is the actual VisualElement subclass
            var outerType = declaringType.DeclaringType;
            if (outerType != null && typeof(VisualElement).IsAssignableFrom(outerType))
                return outerType;

            return null;
        }

        private static List<Type> GetAllComponentTypes()
        {
            var genericBase = typeof(IUlinkComponent<>);
            var result = new HashSet<Type>(TypeCache.GetTypesDerivedFrom<IUlinkComponent>());

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name ?? string.Empty;
                if (assemblyName.StartsWith("Unity") || assemblyName.StartsWith("System") ||
                    assemblyName.StartsWith("mscorlib") || assemblyName.StartsWith("Mono") ||
                    assemblyName.StartsWith("netstandard"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract &&
                            type.GetInterfaces().Any(i => i.IsGenericType &&
                                i.GetGenericTypeDefinition() == genericBase))
                            result.Add(type);
                    }
                }
                catch { /* skip inaccessible assemblies */ }
            }

            return result.OrderBy(t => t.Name).ToList();
        }

        private static bool IsCompatibleComponent(Type componentType, Type elementType)
        {
            var genericInterface = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          i.GetGenericTypeDefinition() == genericInterface &&
                          i.GetGenericArguments()[0].IsAssignableFrom(elementType));
        }

        private static bool IsExactMatch(Type componentType, Type elementType)
        {
            var genericInterface = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          i.GetGenericTypeDefinition() == genericInterface &&
                          i.GetGenericArguments()[0] == elementType);
        }
    }
}