using Ulink.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace Ulink.Editor.Localization
{
    [InitializeOnLoad]
    internal static class UlinkLocalizationEditor
    {
        static UlinkLocalizationEditor()
        {
            UlinkSerializableControlFactory.RegisterTypeHandler(
                typeof(LocalizedString),
                (aqn, field, state) =>
                {
                    string current = state.GetStoredPropertyValue(aqn, field.Name);
                    var holder = ScriptableObject.CreateInstance<LocalizedStringHolder>();
                    if (!string.IsNullOrEmpty(current))
                    {
                        int sep = current.IndexOf(UlinkSeparators.FieldValueSeparator);
                        if (sep >= 0)
                            holder.value = new LocalizedString(current[..sep], current[(sep + 1)..]);
                    }

                    var so = new SerializedObject(holder);
                    var prop = so.FindProperty("value");

                    var wrapper = new VisualElement
                    {
                        style =
                        {
                            paddingLeft = new StyleLength(new Length(12, LengthUnit.Pixel)),
                            marginBottom = new StyleLength(new Length(2, LengthUnit.Pixel))
                        }
                    };
                    wrapper.Add(new IMGUIContainer(() =>
                    {
                        so.Update();
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(prop,
                            new GUIContent(ObjectNames.NicifyVariableName(field.Name)), true);
                        if (EditorGUI.EndChangeCheck())
                        {
                            so.ApplyModifiedProperties();
                            var ls = holder.value;
                            state.SetComponentPropertyValue(aqn, field.Name,
                                $"{ls.TableReference.TableCollectionName ?? string.Empty}{UlinkSeparators.FieldValueSeparator}{ls.TableEntryReference.Key ?? string.Empty}");
                        }
                    }));
                    return wrapper;
                });
        }

        private class LocalizedStringHolder : ScriptableObject
        {
            public LocalizedString value;
        }
    }
}
