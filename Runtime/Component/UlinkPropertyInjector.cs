#nullable enable
using System;
using System.Globalization;
using System.Reflection;

namespace Ulink.Runtime
{
    /// <summary>
    /// Reads [UlinkProperty] fields on a component instance and populates them from
    /// the data stored in a UlinkComponentsType before the component's Setup is called.
    /// Sets the backing field directly.
    /// </summary>
    public static class UlinkPropertyInjector
    {
        public static void Inject(object instance, UlinkComponentsType componentsType, string? assemblyQualifiedName)
        {
            if (assemblyQualifiedName == null) return;

            var data = componentsType.GetDataFor(assemblyQualifiedName);
            if (data.Count == 0) return;

            var type = instance.GetType();

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<UlinkPropertyAttribute>() == null) continue;

                if (!data.TryGetValue(field.Name, out string? rawValue)) continue;

                try
                {
                    object? converted = ConvertValue(rawValue, field.FieldType);
                    if (converted != null) field.SetValue(instance, converted);
                }
                catch
                {
                    /* skip on conversion or set failure */
                }
            }
        }

        private static object? ConvertValue(string raw, Type target)
        {
            if (target == typeof(string)) return raw;
            if (target == typeof(int) && int.TryParse(raw, out int i)) return i;
            if (target == typeof(float) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            if (target == typeof(double) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            if (target == typeof(bool) && bool.TryParse(raw, out bool b)) return b;
            if (target == typeof(long) && long.TryParse(raw, out long l)) return l;
            if (target.IsEnum)
            {
                try
                {
                    return Enum.Parse(target, raw);
                }
                catch
                {
                    return null;
                }
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(target))
            {
#if UNITY_EDITOR
                return !string.IsNullOrEmpty(raw) ? UnityEditor.AssetDatabase.LoadAssetAtPath(raw, target) : null;
#endif
            }

            try
            {
                return Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
}