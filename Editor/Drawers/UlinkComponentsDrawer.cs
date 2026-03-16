using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    [CustomPropertyDrawer(typeof(UlinkComponentsType))]
    public class UlinkComponentsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new Foldout
            {
                text = "Ulink",
                value = true,
                viewDataKey = $"ulink-components-foldout-{property.propertyPath}"
            };

            var state = new UlinkDrawerState(property);
            var allTypes = GetAllComponentTypes();
            var elementType = GetElementType();
            var exactMatchSet = elementType != null
                ? new HashSet<Type>(allTypes.Where(type => IsExactMatch(type, elementType)))
                : new HashSet<Type>();
            var compatibleSet = elementType != null
                ? new HashSet<Type>(allTypes.Where(type =>
                    !exactMatchSet.Contains(type) && IsCompatibleComponent(type, elementType)))
                : new HashSet<Type>();

            var listContainer = new VisualElement();
            foldout.Add(listContainer);

            RebuildList(listContainer, state, allTypes, exactMatchSet, compatibleSet);

            // ── Search + add button ───────────────────────────────────────────

            var searchField = new TextField
            {
                name = "component-search",
                isDelayed = true,
                textEdition = { placeholder = "Search Components" },
                style = { marginBottom = new StyleLength(new Length(4, LengthUnit.Pixel)) }
            };
            foldout.Add(searchField);

            var addButton = new Button { text = "Add Component", style = { flexGrow = 1, height = 20 } };
            foldout.Add(addButton);

            addButton.clicked += () =>
            {
                string filter = searchField.value;
                var filtered = string.IsNullOrEmpty(filter)
                    ? allTypes
                    : allTypes.Where(type => type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                var currentNames = new HashSet<string>(state.GetCurrentNames());
                var exactAvail = filtered
                    .Where(type => exactMatchSet.Contains(type) && !currentNames.Contains(type.AssemblyQualifiedName))
                    .ToList();
                var compatibleAvail = filtered
                    .Where(type => compatibleSet.Contains(type) && !currentNames.Contains(type.AssemblyQualifiedName))
                    .ToList();
                var incompatibleAll = filtered.Where(t =>
                    !exactMatchSet.Contains(t) && !compatibleSet.Contains(t) &&
                    !currentNames.Contains(t.AssemblyQualifiedName)).ToList();

                var menu = new GenericMenu();

                foreach (var type in exactAvail)
                {
                    var componentType = type;
                    menu.AddItem(new GUIContent($"\u2605\u2605 {componentType.Name}"), false, () =>
                    {
                        string[] cur = state.GetCurrentNames();
                        if (!cur.Contains(componentType.AssemblyQualifiedName))
                            state.SetNames(cur.Append(componentType.AssemblyQualifiedName).ToArray());
                        RebuildList(listContainer, state, allTypes, exactMatchSet, compatibleSet);
                    });
                }

                if (exactAvail.Count > 0 && compatibleAvail.Count > 0)
                    menu.AddSeparator(string.Empty);

                foreach (var type in compatibleAvail)
                {
                    var componentType = type;
                    menu.AddItem(new GUIContent($"\u2605 {componentType.Name}"), false, () =>
                    {
                        string[] cur = state.GetCurrentNames();
                        if (!cur.Contains(componentType.AssemblyQualifiedName))
                            state.SetNames(cur.Append(componentType.AssemblyQualifiedName).ToArray());
                        RebuildList(listContainer, state, allTypes, exactMatchSet, compatibleSet);
                    });
                }

                if (incompatibleAll.Count > 0 && (exactAvail.Count > 0 || compatibleAvail.Count > 0))
                    menu.AddSeparator(string.Empty);

                foreach (var type in incompatibleAll)
                    menu.AddDisabledItem(new GUIContent(type.Name));

                if (exactAvail.Count + compatibleAvail.Count + incompatibleAll.Count == 0)
                    menu.AddDisabledItem(new GUIContent("No components available"));

                menu.DropDown(addButton.worldBound);
            };

            return foldout;
        }

        private static void RebuildList(VisualElement listContainer, UlinkDrawerState state,
            List<Type> allTypes, HashSet<Type> exactMatchSet, HashSet<Type> compatibleSet)
        {
            listContainer.Clear();
            string[] names = state.GetCurrentNames();

            for (var i = 0; i < names.Length; i++)
            {
                int index = i;
                string typeName = names[i];
                var resolved = allTypes.FirstOrDefault(type => type.AssemblyQualifiedName == typeName);

                var section = new VisualElement
                {
                    style = { marginBottom = new StyleLength(new Length(4, LengthUnit.Pixel)) }
                };

                // Header row
                var row = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween }
                };

                string labelText;
                if (resolved != null)
                {
                    if (exactMatchSet.Contains(resolved)) labelText = $"\u2605\u2605 {resolved.Name}";
                    else if (compatibleSet.Contains(resolved)) labelText = $"\u2605 {resolved.Name}";
                    else labelText = resolved.Name;
                }
                else
                {
                    labelText = typeName.Split(',')[0].Split('.').Last();
                }

                row.Add(new Label(labelText)
                {
                    style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft }
                });

                row.Add(new Button(() =>
                {
                    state.SetNames(state.GetCurrentNames().Where((_, j) => j != index).ToArray());
                    state.RemoveComponentData(typeName);
                    RebuildList(listContainer, state, allTypes, exactMatchSet, compatibleSet);
                })
                {
                    text = "\u2715",
                    style = { width = 20, height = 20 }
                });

                section.Add(row);

                // Runtime-only notice
                if (resolved?.GetCustomAttribute<UlinkRuntimeOnlyAttribute>() != null)
                {
                    section.Add(new HelpBox("Runtime only \u2014 not active in editor.", HelpBoxMessageType.Info));
                }

                // [UlinkSerializable] fields
                if (resolved != null)
                {
                    foreach (var field in UlinkFieldDiscovery.GetUlinkSerializableFields(resolved))
                        section.Add(UlinkSerializableControlFactory.Create(typeName, field, state));
                }

                listContainer.Add(section);
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────

        private Type GetElementType()
        {
            var declaringType = fieldInfo?.DeclaringType;
            if (declaringType == null) return null;
            if (typeof(VisualElement).IsAssignableFrom(declaringType)) return declaringType;
            var outer = declaringType.DeclaringType;
            if (outer != null && typeof(VisualElement).IsAssignableFrom(outer)) return outer;
            return null;
        }

        private static List<Type> GetAllComponentTypes()
        {
            var genericBase = typeof(IUlinkComponent<>);
            var result = new HashSet<Type>(TypeCache.GetTypesDerivedFrom<IUlinkComponent>());

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name ?? string.Empty;
                if (name.StartsWith("Unity") || name.StartsWith("System") ||
                    name.StartsWith("mscorlib") || name.StartsWith("Mono") ||
                    name.StartsWith("netstandard"))
                {
                    continue;
                }

                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.IsClass && !type.IsAbstract && type.GetInterfaces()
                            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericBase))
                        {
                            result.Add(type);
                        }
                    }
                }
                catch { }
            }

            return result.OrderBy(type => type.Name).ToList();
        }

        private static bool IsCompatibleComponent(Type componentType, Type elementType)
        {
            var type = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == type &&
                i.GetGenericArguments()[0].IsAssignableFrom(elementType));
        }

        private static bool IsExactMatch(Type componentType, Type elementType)
        {
            var type = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == type &&
                i.GetGenericArguments()[0] == elementType);
        }
    }
}