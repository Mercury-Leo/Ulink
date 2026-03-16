#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ulink.Runtime
{
    /// <summary>
    /// Converts a raw string value into a typed object for [UlinkSerializable] injection.
    /// Primitives use a dictionary lookup; enums, UnityEngine.Object, and unknown types
    /// have special-case handling. Returns null on conversion failure (caller skips the field).
    /// </summary>
    public static class UlinkValueConverter
    {
        private static readonly Dictionary<Type, Func<string, object?>> Converters = new()
        {
            [typeof(string)] = raw => raw,
            [typeof(int)] = raw => int.TryParse(raw, out int value) ? value : null,
            [typeof(float)] = raw =>
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : null,
            [typeof(double)] = raw =>
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null,
            [typeof(bool)] = raw => bool.TryParse(raw, out bool value) ? value : null,
            [typeof(long)] = raw => long.TryParse(raw, out long value) ? value : null,
            [typeof(Vector2)] = raw => UlinkParsing.ParseVector2(raw),
            [typeof(Vector3)] = raw => UlinkParsing.ParseVector3(raw),
            [typeof(Vector4)] = raw => UlinkParsing.ParseVector4(raw),
            [typeof(Color)] = raw => UlinkParsing.ParseColor(raw, Color.white),
        };

        public static object? Convert(string raw, Type target)
        {
            if (Converters.TryGetValue(target, out var converter))
                return converter(raw);

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

            // UnityEngine.Object fields store a GUID string; resolved via AssetDatabase in editor
            // or UlinkAssetRegistry at runtime (registry is populated by the generator)
            if (typeof(UnityEngine.Object).IsAssignableFrom(target))
            {
                if (string.IsNullOrEmpty(raw)) return null;
#if UNITY_EDITOR
                // Legacy support: if value starts with "Assets/" treat as path, otherwise as GUID
                string path = raw.StartsWith("Assets/") ? raw : UnityEditor.AssetDatabase.GUIDToAssetPath(raw);
                return string.IsNullOrEmpty(path) ? null : UnityEditor.AssetDatabase.LoadAssetAtPath(path, target);
#else
                return UlinkAssetRegistry.Instance?.Get(raw);
#endif
            }

            try
            {
                return System.Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
}