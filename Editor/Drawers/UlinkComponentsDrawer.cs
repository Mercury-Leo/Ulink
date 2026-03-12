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
            var foldout = new Foldout
            {
                text = "Ulink",
                value = true,
                viewDataKey = $"ulink-components-foldout-{property.propertyPath}"
            };

            // Single serialized property — TypeNamesRaw holds both type list and property data
            var rawProperty = property.FindPropertyRelative(nameof(UlinkComponentsType.TypeNamesRaw));

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

            RebuildList();

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

            return foldout;

            // ── Raw string accessors ─────────────────────────────────────────

            void WriteBack(string typesSection, string dataSection)
            {
                rawProperty.stringValue = UlinkComponentsType.Combine(typesSection, dataSection);
                property.serializedObject.ApplyModifiedProperties();
            }

            string RawData() => UlinkComponentsType.DataSection(rawProperty.stringValue);

            string[] GetCurrentNames()
            {
                string part = RawTypes();
                return string.IsNullOrEmpty(part) ? Array.Empty<string>() : part.Split(TypeSeparator);
            }

            string RawTypes() => UlinkComponentsType.TypesSection(rawProperty.stringValue);

            // ── Component-list helpers ────────────────────────────────────────

            void SetNames(string[] names) =>
                WriteBack(string.Join(TypeSeparator.ToString(), names), RawData());

            // ── Per-component property helpers ────────────────────────────────

            void SetComponentPropertyValue(string aqn, string fieldName, string value)
            {
                var all = UlinkComponentsType.ParseAllData(rawProperty.stringValue);
                if (!all.ContainsKey(aqn)) all[aqn] = new Dictionary<string, string>();
                all[aqn][fieldName] = value;
                WriteBack(RawTypes(), UlinkComponentsType.SerializeAllData(all));
            }

            string GetStoredPropertyValue(string aqn, string fieldName)
            {
                var all = UlinkComponentsType.ParseAllData(rawProperty.stringValue);
                return all.TryGetValue(aqn, out var fields) && fields.TryGetValue(fieldName, out string value)
                    ? value
                    : string.Empty;
            }

            void RemoveComponentData(string aqn)
            {
                var all = UlinkComponentsType.ParseAllData(rawProperty.stringValue);
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
                    var intField = new IntegerField { value = v, isDelayed = true, style = { flexGrow = 1 } };
                    intField.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
                    ctrl = intField;
                }
                else if (fieldType == typeof(float))
                {
                    float.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
                    var floatField = new FloatField { value = v, isDelayed = true, style = { flexGrow = 1 } };
                    floatField.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name,
                            evt.newValue.ToString(CultureInfo.InvariantCulture)));
                    ctrl = floatField;
                }
                else if (fieldType == typeof(bool))
                {
                    bool.TryParse(current, out bool value);
                    var toggleField = new Toggle { value = value };
                    toggleField.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
                    ctrl = toggleField;
                }
                else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                {
                    var currentObj = ResolveStoredAsset(current, fieldType);
                    var objField = new UnityEditor.UIElements.ObjectField
                    {
                        value = currentObj,
                        objectType = fieldType,
                        allowSceneObjects = false,
                        style = { flexGrow = 1 }
                    };
                    objField.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.newValue != null)
                        {
                            string path = AssetDatabase.GetAssetPath(evt.newValue);
                            string guid = AssetDatabase.AssetPathToGUID(path);
                            SetComponentPropertyValue(aqn, field.Name, guid);
                        }
                        else
                        {
                            SetComponentPropertyValue(aqn, field.Name, string.Empty);
                        }
                    });
                    ctrl = objField;
                }
                else if (fieldType.IsEnum)
                {
                    var enumNames = Enum.GetNames(fieldType).ToList();
                    string initialName = enumNames.Contains(current) ? current : enumNames[0];
                    var dropdown = new DropdownField(enumNames, initialName) { style = { flexGrow = 1 } };
                    dropdown.RegisterValueChangedCallback(evt =>
                        SetComponentPropertyValue(aqn, field.Name, evt.newValue));
                    ctrl = dropdown;
                }
                else if (fieldType == typeof(Vector2))
                {
                    var v = ParseVector2(current);
                    var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                    var xField = new FloatField("X") { value = v.x, isDelayed = true, style = { flexGrow = 1 } };
                    var yField = new FloatField("Y") { value = v.y, isDelayed = true, style = { flexGrow = 1 } };
                    void SaveVec2() => SetComponentPropertyValue(aqn, field.Name,
                        $"{xField.value.ToString(CultureInfo.InvariantCulture)},{yField.value.ToString(CultureInfo.InvariantCulture)}");
                    xField.RegisterValueChangedCallback(_ => SaveVec2());
                    yField.RegisterValueChangedCallback(_ => SaveVec2());
                    container.Add(xField);
                    container.Add(yField);
                    ctrl = container;
                }
                else if (fieldType == typeof(Vector3))
                {
                    var v = ParseVector3(current);
                    var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                    var xField = new FloatField("X") { value = v.x, isDelayed = true, style = { flexGrow = 1 } };
                    var yField = new FloatField("Y") { value = v.y, isDelayed = true, style = { flexGrow = 1 } };
                    var zField = new FloatField("Z") { value = v.z, isDelayed = true, style = { flexGrow = 1 } };
                    void SaveVec3() => SetComponentPropertyValue(aqn, field.Name,
                        $"{xField.value.ToString(CultureInfo.InvariantCulture)},{yField.value.ToString(CultureInfo.InvariantCulture)},{zField.value.ToString(CultureInfo.InvariantCulture)}");
                    xField.RegisterValueChangedCallback(_ => SaveVec3());
                    yField.RegisterValueChangedCallback(_ => SaveVec3());
                    zField.RegisterValueChangedCallback(_ => SaveVec3());
                    container.Add(xField);
                    container.Add(yField);
                    container.Add(zField);
                    ctrl = container;
                }
                else if (fieldType == typeof(Vector4))
                {
                    var v = ParseVector4(current);
                    var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                    var xField = new FloatField("X") { value = v.x, isDelayed = true, style = { flexGrow = 1 } };
                    var yField = new FloatField("Y") { value = v.y, isDelayed = true, style = { flexGrow = 1 } };
                    var zField = new FloatField("Z") { value = v.z, isDelayed = true, style = { flexGrow = 1 } };
                    var wField = new FloatField("W") { value = v.w, isDelayed = true, style = { flexGrow = 1 } };
                    void SaveVec4() => SetComponentPropertyValue(aqn, field.Name,
                        $"{xField.value.ToString(CultureInfo.InvariantCulture)},{yField.value.ToString(CultureInfo.InvariantCulture)},{zField.value.ToString(CultureInfo.InvariantCulture)},{wField.value.ToString(CultureInfo.InvariantCulture)}");
                    xField.RegisterValueChangedCallback(_ => SaveVec4());
                    yField.RegisterValueChangedCallback(_ => SaveVec4());
                    zField.RegisterValueChangedCallback(_ => SaveVec4());
                    wField.RegisterValueChangedCallback(_ => SaveVec4());
                    container.Add(xField);
                    container.Add(yField);
                    container.Add(zField);
                    container.Add(wField);
                    ctrl = container;
                }
                else if (fieldType == typeof(Color))
                {
                    var c = ParseColor(current);
                    var colorField = new UnityEditor.UIElements.ColorField { value = c, style = { flexGrow = 1 } };
                    colorField.RegisterValueChangedCallback(evt =>
                    {
                        var col = evt.newValue;
                        SetComponentPropertyValue(aqn, field.Name,
                            $"{col.r.ToString(CultureInfo.InvariantCulture)},{col.g.ToString(CultureInfo.InvariantCulture)},{col.b.ToString(CultureInfo.InvariantCulture)},{col.a.ToString(CultureInfo.InvariantCulture)}");
                    });
                    ctrl = colorField;
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

                    // Runtime-only notice
                    if (resolved?.GetCustomAttribute<UlinkRuntimeOnlyAttribute>() != null)
                    {
                        section.Add(new HelpBox("Runtime only — not active in editor.", HelpBoxMessageType.Info));
                    }

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

        private static Vector2 ParseVector2(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Vector2.zero;
            string[] split = raw.Split(',');
            if (split.Length < 2) return Vector2.zero;
            float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            return new Vector2(x, y);
        }

        private static Vector3 ParseVector3(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Vector3.zero;
            string[] split = raw.Split(',');
            if (split.Length < 3) return Vector3.zero;
            float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            return new Vector3(x, y, z);
        }

        private static Vector4 ParseVector4(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Vector4.zero;
            string[] split = raw.Split(',');
            if (split.Length < 4) return Vector4.zero;
            float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            float.TryParse(split[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float w);
            return new Vector4(x, y, z, w);
        }

        private static Color ParseColor(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Color.white;
            string[] split = raw.Split(',');
            if (split.Length < 4) return Color.white;
            float.TryParse(split[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r);
            float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g);
            float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b);
            float.TryParse(split[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float a);
            return new Color(r, g, b, a);
        }

        private static UnityEngine.Object ResolveStoredAsset(string stored, Type type)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            // Legacy: starts with "Assets/" → path format; otherwise treat as GUID
            string path = stored.StartsWith("Assets/") ? stored : AssetDatabase.GUIDToAssetPath(stored);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);
        }

        private static List<FieldInfo> GetUlinkPropertyFields(Type componentType)
        {
            var result = new List<FieldInfo>();
            var type = componentType;
            while (type != null && type != typeof(object))
            {
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                result.AddRange(fields.Where(field => field.GetCustomAttribute<UlinkPropertyAttribute>() != null));
                type = type.BaseType;
            }

            return result;
        }

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
                // Skip known assemblies
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