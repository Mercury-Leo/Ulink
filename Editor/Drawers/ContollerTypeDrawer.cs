using System;
using System.Collections.Generic;
using System.Linq;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    [CustomPropertyDrawer(typeof(ControllerType))]
    public class ControllerTypeDrawer : PropertyDrawer
    {
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

            var searchField = new TextField
            {
                name = "controller-search",
                isDelayed = true,
                textEdition =
                {
                    placeholder = "Search Controllers",
                },
                style =
                {
                    marginBottom = new StyleLength(new Length(4, LengthUnit.Pixel))
                }
            };
            container.Add(searchField);

            var typeNameProp = property.FindPropertyRelative(nameof(ControllerType.TypeName));

            var allTypes = TypeCache.GetTypesDerivedFrom<IUIController>().OrderBy(type => type.Name).ToList();

            var filteredTypes = new List<Type> { null };
            filteredTypes.AddRange(allTypes);

            var savedName = typeNameProp.stringValue;
            int defaultIndex = 0;
            if (!string.IsNullOrEmpty(savedName))
            {
                var savedType = allTypes.First(type => type.AssemblyQualifiedName == savedName);
                var idx = allTypes.IndexOf(savedType);
                if (idx >= 0) defaultIndex = idx + 1; // +1 for null placeholder
            }

            var dropdown = new PopupField<Type>(
                "Controller Type",
                filteredTypes,
                defaultIndex,
                type => type?.Name ?? "<None>",
                type => type?.Name ?? "<None>"
            )
            {
                style =
                {
                    flexGrow = 1,
                    height = 20
                }
            };
            container.Add(dropdown);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var chosen = evt.newValue;
                typeNameProp.stringValue = chosen == null ? string.Empty : chosen.AssemblyQualifiedName;
                property.serializedObject.ApplyModifiedProperties();
            });

            searchField.RegisterValueChangedCallback(evt =>
            {
                var filter = evt.newValue;
                filteredTypes.Clear();
                filteredTypes.Add(null);
                var matches = string.IsNullOrEmpty(filter)
                    ? allTypes
                    : allTypes.Where(type => type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                filteredTypes.AddRange(matches);
                dropdown.choices = new List<Type>(filteredTypes);
                if (!filteredTypes.Contains(dropdown.value) && dropdown.value != null)
                {
                    dropdown.value = null;
                }
            });

            return container;
        }
    }
}