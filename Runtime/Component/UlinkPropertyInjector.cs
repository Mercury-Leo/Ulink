#nullable enable
using System;

namespace Ulink.Runtime
{
    /// <summary>
    /// Reads [UlinkProperty] fields on a component instance and populates them from
    /// the data stored in a UlinkComponentsType before the component's Setup is called.
    /// </summary>
    public static class UlinkPropertyInjector
    {
        public static void Inject(object instance, UlinkComponentsType componentsType, string? assemblyQualifiedName)
        {
            if (assemblyQualifiedName == null) return;

            var data = componentsType.GetDataFor(assemblyQualifiedName);
            if (data.Count == 0) return;

            var type = instance.GetType();

            foreach (var field in UlinkFieldDiscovery.GetUlinkPropertyFields(type))
            {
                if (!data.TryGetValue(field.Name, out string? rawValue)) continue;

                try
                {
                    object? converted = UlinkValueConverter.Convert(rawValue, field.FieldType);
                    if (converted != null) field.SetValue(instance, converted);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[Ulink] Failed to inject field '{field.Name}' on '{type.Name}': {ex.Message}");
                }
            }
        }
    }
}