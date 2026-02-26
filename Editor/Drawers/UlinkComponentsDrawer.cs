using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const char TypeSeparator = ';';

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

            // Single serialized property — TypeNamesRaw holds both type list and property data
            var rawProp = property.FindPropertyRelative(nameof(UlinkComponentsType.TypeNamesRaw));

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
            container.Add(listContainer);

            RebuildList();

            // ── Search + add button ───────────────────────────────────────────

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

            addButton.clicked += () =>
            {
                string filter = searchField.value;
                var filtered = string.IsNullOrEmpty(filter)
                    ? allTypes
                    : allTypes.Where(type => type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                var currentNames = new HashSet<string>(GetCurrentNames());
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
                    menu.AddItem(new GUIContent($"★★ {componentType.Name}"), false, () =>
                    {
                        string[] cur = GetCurrentNames();
                        if (!cur.Contains(componentType.AssemblyQualifiedName))
                            SetNames(cur.Append(componentType.AssemblyQualifiedName).ToArray());
                        RebuildList();
                    });
                }

                if (exactAvail.Count > 0 && compatibleAvail.Count > 0)
                    menu.AddSeparator(string.Empty);

                foreach (var type in compatibleAvail)
                {
                    var componentType = type;
                    menu.AddItem(new GUIContent($"★ {componentType.Name}"), false, () =>
                    {
                        string[] cur = GetCurrentNames();
                        if (!cur.Contains(componentType.AssemblyQualifiedName))
                            SetNames(cur.Append(componentType.AssemblyQualifiedName).ToArray());
                        RebuildList();
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

            return container;

            // ── Raw string accessors ─────────────────────────────────────────

            void WriteBack(string typesSection, string dataSection)
            {
                rawProp.stringValue = UlinkComponentsType.Combine(typesSection, dataSection);
                property.serializedObject.ApplyModifiedProperties();
            }

            string RawData() => UlinkComponentsType.DataSection(rawProp.stringValue);

            string[] GetCurrentNames()
            {
                string part = RawTypes();
                return string.IsNullOrEmpty(part) ? Array.Empty<string>() : part.Split(TypeSeparator);
            }

            string RawTypes() => UlinkComponentsType.TypesSection(rawProp.stringValue);

            // ── Component-list helpers ────────────────────────────────────────

            void SetNames(string[] names) =>
                WriteBack(string.Join(TypeSeparator.ToString(), names), RawData());

            // ── Per-component property helpers ────────────────────────────────

            void SetComponentPropertyValue(string aqn, string fieldName, string value)
            {
                var all = UlinkComponentsType.ParseAllData(rawProp.stringValue);
                if (!all.ContainsKey(aqn)) all[aqn] = new Dictionary<string, string>();
                all[aqn][fieldName] = value;
                WriteBack(RawTypes(), UlinkComponentsType.SerializeAllData(all));
            }

            string GetStoredPropertyValue(string aqn, string fieldName)
            {
                var all = UlinkComponentsType.ParseAllData(rawProp.stringValue);
                return all.TryGetValue(aqn, out var fields) && fields.TryGetValue(fieldName, out string value)
                    ? value
                    : string.Empty;
            }

            void RemoveComponentData(string aqn)
            {
                var all = UlinkComponentsType.ParseAllData(rawProp.stringValue);
                if (all.Remove(aqn))
                    WriteBack(RawTypes(), UlinkComponentsType.SerializeAllData(all));
            }

            // ── Property control factory ──────────────────────────────────────

            VisualElement CreatePropertyControl(string aqn, FieldInfo field)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel)),
                        marginBottom = new StyleLength(new Length(2, LengthUnit.Pixel))
                    }
                };

                row.Add(new Label(ObjectNames.NicifyVariableName(field.Name))
                {
                    style = { width = 120, unityTextAlign = TextAnchor.MiddleLeft }
                });

                string current = GetStoredPropertyValue(aqn, field.Name);
                var fieldType = field.FieldType;
                VisualElement ctrl;

                if (fieldType == typeof(int))
                {
                    int.TryParse(current, out int v);
                    var f = new IntegerField { value = v, isDelayed = true, style = { flexGrow = 1 } };
                    f.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
                    ctrl = f;
                }
                else if (fieldType == typeof(float))
                {
                    float.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
                    var f = new FloatField { value = v, isDelayed = true, style = { flexGrow = 1 } };
                    f.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name,
                            evt.newValue.ToString(CultureInfo.InvariantCulture)));
                    ctrl = f;
                }
                else if (fieldType == typeof(bool))
                {
                    bool.TryParse(current, out bool value);
                    var f = new Toggle { value = value };
                    f.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
                    ctrl = f;
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                {
                    var currentObj = string.IsNullOrEmpty(current)
                        ? null
                        : AssetDatabase.LoadAssetAtPath(current, fieldType);
                    var f = new UnityEditor.UIElements.ObjectField
                    {
                        value = currentObj,
                        objectType = fieldType,
                        allowSceneObjects = false,
                        style = { flexGrow = 1 }
                    };
                    f.RegisterValueChangedCallback(evt =>
                    {
                        string path = evt.newValue != null ? AssetDatabase.GetAssetPath(evt.newValue) : string.Empty;
                        SetComponentPropertyValue(aqn, field.Name, path);
                    });
                    ctrl = f;
                }
                else
                {
                    var f = new TextField { value = current, isDelayed = true, style = { flexGrow = 1 } };
                    f.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue));
                    ctrl = f;
                }

                row.Add(ctrl);
                return row;
            }

            // ── List ──────────────────────────────────────────────────────────

            void RebuildList()
            {
                listContainer.Clear();
                string[] names = GetCurrentNames();

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
                        if (exactMatchSet.Contains(resolved)) labelText = $"★★ {resolved.Name}";
                        else if (compatibleSet.Contains(resolved)) labelText = $"★ {resolved.Name}";
                        else labelText = resolved.Name;
                    }
                    else
                    {
                        labelText = typeName;
                    }

                    row.Add(new Label(labelText)
                    {
                        style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft }
                    });

                    row.Add(new Button(() =>
                    {
                        SetNames(GetCurrentNames().Where((_, j) => j != index).ToArray());
                        RemoveComponentData(typeName);
                        RebuildList();
                    })
                    {
                        text = "✕",
                        style = { width = 20, height = 20 }
                    });

                    section.Add(row);

                    // [UlinkProperty] fields
                    if (resolved != null)
                    {
                        foreach (var field in GetUlinkPropertyFields(resolved))
                            section.Add(CreatePropertyControl(typeName, field));
                    }

                    listContainer.Add(section);
                }
            }
        }

        // ── Static helpers ────────────────────────────────────────────────────

        private static List<FieldInfo> GetUlinkPropertyFields(Type componentType) =>
            componentType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttribute<UlinkPropertyAttribute>() != null)
                .ToList();

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
                catch { }
            }

            return result.OrderBy(type => type.Name).ToList();
        }

        private static bool IsCompatibleComponent(Type componentType, Type elementType)
        {
            var gi = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == gi &&
                i.GetGenericArguments()[0].IsAssignableFrom(elementType));
        }

        private static bool IsExactMatch(Type componentType, Type elementType)
        {
            var gi = typeof(IUlinkComponent<>);
            return componentType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == gi &&
                i.GetGenericArguments()[0] == elementType);
        }
    }
}