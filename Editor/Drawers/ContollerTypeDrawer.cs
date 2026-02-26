using System;
using System.Collections.Generic;
using System.Linq;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    [CustomPropertyDrawer(typeof(UlinkControllerType))]
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

            var typeNameProp = property.FindPropertyRelative(nameof(UlinkControllerType.TypeName));

            var allTypes = GetAllControllerTypes();

            var elementType = GetElementType();
            var compatibleSet = elementType != null
                ? new HashSet<Type>(allTypes.Where(t => IsCompatibleController(t, elementType)))
                : new HashSet<Type>();

            var filteredTypes = BuildOrderedList(allTypes, compatibleSet);

            var savedName = typeNameProp.stringValue;
            int defaultIndex = 0;
            if (!string.IsNullOrEmpty(savedName))
            {
                var savedType = allTypes.FirstOrDefault(type => type.AssemblyQualifiedName == savedName);
                if (savedType != null)
                {
                    var idx = filteredTypes.IndexOf(savedType);
                    if (idx >= 0) defaultIndex = idx;
                }
            }

            var dropdown = new PopupField<Type>(
                "Controller Type",
                filteredTypes,
                defaultIndex,
                type => type?.Name ?? "<None>",
                type => type == null ? "<None>" : (compatibleSet.Contains(type) ? $"★ {type.Name}" : type.Name)
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

                IEnumerable<Type> matches = string.IsNullOrEmpty(filter)
                    ? allTypes
                    : allTypes.Where(type => type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

                var matchList = matches.ToList();
                filteredTypes.Clear();
                filteredTypes.Add(null);
                filteredTypes.AddRange(matchList.Where(t => compatibleSet.Contains(t)));
                filteredTypes.AddRange(matchList.Where(t => !compatibleSet.Contains(t)));

                dropdown.choices = new List<Type>(filteredTypes);
                if (!filteredTypes.Contains(dropdown.value) && dropdown.value != null)
                {
                    dropdown.value = null;
                }
            });

            return container;
        }

        private Type GetElementType()
        {
            var declaringType = fieldInfo?.DeclaringType;
            return declaringType != null && typeof(VisualElement).IsAssignableFrom(declaringType)
                ? declaringType
                : null;
        }

        private static List<Type> GetAllControllerTypes()
        {
            var genericBase = typeof(IUlinkController<>);
            var result = new HashSet<Type>(TypeCache.GetTypesDerivedFrom<IUlinkController>());

            // TypeCache only finds IUlinkController (= IUlinkController<VisualElement>) implementors.
            // We also need to find classes implementing IUlinkController<T> for specific T types.
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
                        {
                            result.Add(type);
                        }
                    }
                }
                catch { }
            }

            return result.OrderBy(t => t.Name).ToList();
        }

        private static bool IsCompatibleController(Type controllerType, Type elementType)
        {
            var genericInterface = typeof(IUlinkController<>);
            return controllerType.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          i.GetGenericTypeDefinition() == genericInterface &&
                          i.GetGenericArguments()[0].IsAssignableFrom(elementType));
        }

        private static List<Type> BuildOrderedList(List<Type> allTypes, HashSet<Type> compatibleSet)
        {
            var result = new List<Type> { null };
            result.AddRange(allTypes.Where(t => compatibleSet.Contains(t)));
            result.AddRange(allTypes.Where(t => !compatibleSet.Contains(t)));
            return result;
        }
    }
}
