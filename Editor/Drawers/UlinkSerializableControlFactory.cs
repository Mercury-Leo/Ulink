using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    /// <summary>
    /// Creates inspector UI controls for individual [UlinkSerializable] fields.
    /// Each control reads its initial value from the drawer state and writes back on change.
    /// Object references are stored as GUIDs; vectors/colors as comma-separated floats.
    /// </summary>
    internal static class UlinkSerializableControlFactory
    {
        /// <param name="aqn">Assembly-qualified name of the component owning this field.</param>
        /// <param name="field"></param>
        /// <param name="state"></param>
        public static VisualElement Create(string aqn, FieldInfo field, UlinkDrawerState state)
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

            string current = state.GetStoredPropertyValue(aqn, field.Name);
            var fieldType = field.FieldType;
            VisualElement ctrl;

            if (fieldType == typeof(int))
            {
                int.TryParse(current, out int v);
                var intField = new IntegerField { value = v, isDelayed = true, style = { flexGrow = 1 } };
                intField.RegisterValueChangedCallback(evt =>
                    state.SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
                ctrl = intField;
            }
            else if (fieldType == typeof(float))
            {
                float.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
                var floatField = new FloatField { value = value, isDelayed = true, style = { flexGrow = 1 } };
                floatField.RegisterValueChangedCallback(evt =>
                    state.SetComponentPropertyValue(aqn, field.Name,
                        evt.newValue.ToString(CultureInfo.InvariantCulture)));
                ctrl = floatField;
            }
            else if (fieldType == typeof(bool))
            {
                bool.TryParse(current, out bool value);
                var toggleField = new Toggle { value = value };
                toggleField.RegisterValueChangedCallback(evt =>
                    state.SetComponentPropertyValue(aqn, field.Name, evt.newValue.ToString()));
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
                        state.SetComponentPropertyValue(aqn, field.Name, guid);
                    }
                    else
                    {
                        state.SetComponentPropertyValue(aqn, field.Name, string.Empty);
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
                    state.SetComponentPropertyValue(aqn, field.Name, evt.newValue));
                ctrl = dropdown;
            }
            else if (fieldType == typeof(Vector2))
            {
                var v = UlinkParsing.ParseVector2(current);
                var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                float cx = v.x, cy = v.y;

                void SaveVector2() => state.SetComponentPropertyValue(aqn, field.Name,
                    $"{cx.ToString(CultureInfo.InvariantCulture)},{cy.ToString(CultureInfo.InvariantCulture)}");

                container.Add(MakeEntry("X", v.x, val =>
                {
                    cx = val;
                    SaveVector2();
                }));
                container.Add(MakeEntry("Y", v.y, val =>
                {
                    cy = val;
                    SaveVector2();
                }));
                ctrl = container;
            }
            else if (fieldType == typeof(Vector3))
            {
                var v = UlinkParsing.ParseVector3(current);
                var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                float cx = v.x, cy = v.y, cz = v.z;

                void SaveVector3() => state.SetComponentPropertyValue(aqn, field.Name,
                    $"{cx.ToString(CultureInfo.InvariantCulture)},{cy.ToString(CultureInfo.InvariantCulture)},{cz.ToString(CultureInfo.InvariantCulture)}");

                container.Add(MakeEntry("X", v.x, val =>
                {
                    cx = val;
                    SaveVector3();
                }));
                container.Add(MakeEntry("Y", v.y, val =>
                {
                    cy = val;
                    SaveVector3();
                }));
                container.Add(MakeEntry("Z", v.z, val =>
                {
                    cz = val;
                    SaveVector3();
                }));
                ctrl = container;
            }
            else if (fieldType == typeof(Vector4))
            {
                var v = UlinkParsing.ParseVector4(current);
                var container = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
                float cx = v.x, cy = v.y, cz = v.z, cw = v.w;

                void SaveVec4() => state.SetComponentPropertyValue(aqn, field.Name,
                    $"{cx.ToString(CultureInfo.InvariantCulture)},{cy.ToString(CultureInfo.InvariantCulture)},{cz.ToString(CultureInfo.InvariantCulture)},{cw.ToString(CultureInfo.InvariantCulture)}");

                container.Add(MakeEntry("X", v.x, val =>
                {
                    cx = val;
                    SaveVec4();
                }));
                container.Add(MakeEntry("Y", v.y, val =>
                {
                    cy = val;
                    SaveVec4();
                }));
                container.Add(MakeEntry("Z", v.z, val =>
                {
                    cz = val;
                    SaveVec4();
                }));
                container.Add(MakeEntry("W", v.w, val =>
                {
                    cw = val;
                    SaveVec4();
                }));
                ctrl = container;
            }
            else if (fieldType == typeof(Color))
            {
                var c = UlinkParsing.ParseColor(current, Color.white);
                var colorField = new UnityEditor.UIElements.ColorField { value = c, style = { flexGrow = 1 } };
                colorField.RegisterValueChangedCallback(evt =>
                {
                    var col = evt.newValue;
                    state.SetComponentPropertyValue(aqn, field.Name,
                        $"{col.r.ToString(CultureInfo.InvariantCulture)},{col.g.ToString(CultureInfo.InvariantCulture)},{col.b.ToString(CultureInfo.InvariantCulture)},{col.a.ToString(CultureInfo.InvariantCulture)}");
                });
                ctrl = colorField;
            }
            else
            {
                var textField = new TextField { value = current, isDelayed = true, style = { flexGrow = 1 } };
                textField.RegisterValueChangedCallback(evt =>
                    state.SetComponentPropertyValue(aqn, field.Name, evt.newValue));
                ctrl = textField;
            }

            row.Add(ctrl);
            return row;
        }

        private static VisualElement MakeEntry(string lbl, float init, Action<float> onChange)
        {
            var sub = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            sub.Add(new Label(lbl) { style = { width = 14, unityTextAlign = TextAnchor.MiddleLeft } });
            var field = new FloatField { value = init, isDelayed = true, style = { flexGrow = 1, minWidth = 30 } };
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            sub.Add(field);
            return sub;
        }

        /// <summary>Resolves a stored GUID (or legacy asset path) to a UnityEngine.Object for the ObjectField.</summary>
        private static UnityEngine.Object ResolveStoredAsset(string stored, Type type)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            string path = stored.StartsWith("Assets/") ? stored : AssetDatabase.GUIDToAssetPath(stored);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, type);
        }
    }
}
