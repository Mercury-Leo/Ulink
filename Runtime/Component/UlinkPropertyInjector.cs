#nullable enable
using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();

        public static void Inject(object instance, UlinkComponentsType componentsType, string? assemblyQualifiedName)
        {
            if (assemblyQualifiedName == null) return;

            var data = componentsType.GetDataFor(assemblyQualifiedName);
            if (data.Count == 0) return;

            var type = instance.GetType();

            foreach (var field in GetInjectableFields(type))
            {
                if (!data.TryGetValue(field.Name, out string? rawValue)) continue;

                try
                {
                    object? converted = ConvertValue(rawValue, field.FieldType);
                    if (converted != null) field.SetValue(instance, converted);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[Ulink] Failed to inject field '{field.Name}' on '{type.Name}': {ex.Message}");
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

            if (target == typeof(UnityEngine.Vector2)) return ParseVector2(raw);
            if (target == typeof(UnityEngine.Vector3)) return ParseVector3(raw);
            if (target == typeof(UnityEngine.Vector4)) return ParseVector4(raw);
            if (target == typeof(UnityEngine.Color)) return ParseColor(raw);

            if (typeof(UnityEngine.Object).IsAssignableFrom(target))
            {
                if (string.IsNullOrEmpty(raw)) return null;
#if UNITY_EDITOR
                // Editor: GUID format (or legacy path)
                string path = raw.StartsWith("Assets/") ? raw : UnityEditor.AssetDatabase.GUIDToAssetPath(raw);
                return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath(path, target);
#else
                // Runtime: resolve via registry
                return UlinkAssetRegistry.Instance?.Get(raw);
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

        private static FieldInfo[] GetInjectableFields(Type fieldType)
        {
            if (FieldCache.TryGetValue(fieldType, out var cached)) return cached;
            var result = new List<FieldInfo>();
            var type = fieldType;
            while (type != null && type != typeof(object))
            {
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly))
                    if (f.GetCustomAttribute<UlinkPropertyAttribute>() != null)
                        result.Add(f);
                type = type.BaseType;
            }

            var fields = result.ToArray();
            FieldCache[fieldType] = fields;
            return fields;
        }

        private static UnityEngine.Vector2 ParseVector2(string raw)
        {
            string[]? split = raw.Split(',');
            return new UnityEngine.Vector2(
                float.Parse(split[0], CultureInfo.InvariantCulture),
                float.Parse(split[1], CultureInfo.InvariantCulture));
        }

        private static UnityEngine.Vector3 ParseVector3(string raw)
        {
            string[]? split = raw.Split(',');
            return new UnityEngine.Vector3(
                float.Parse(split[0], CultureInfo.InvariantCulture),
                float.Parse(split[1], CultureInfo.InvariantCulture),
                float.Parse(split[2], CultureInfo.InvariantCulture));
        }

        private static UnityEngine.Vector4 ParseVector4(string raw)
        {
            string[]? split = raw.Split(',');
            return new UnityEngine.Vector4(
                float.Parse(split[0], CultureInfo.InvariantCulture),
                float.Parse(split[1], CultureInfo.InvariantCulture),
                float.Parse(split[2], CultureInfo.InvariantCulture),
                float.Parse(split[3], CultureInfo.InvariantCulture));
        }

        private static UnityEngine.Color ParseColor(string raw)
        {
            string[]? split = raw.Split(',');
            return new UnityEngine.Color(
                float.Parse(split[0], CultureInfo.InvariantCulture),
                float.Parse(split[1], CultureInfo.InvariantCulture),
                float.Parse(split[2], CultureInfo.InvariantCulture),
                float.Parse(split[3], CultureInfo.InvariantCulture));
        }
    }
}