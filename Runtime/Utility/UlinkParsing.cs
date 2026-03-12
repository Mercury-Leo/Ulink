#nullable enable
using System;
using System.Globalization;
using UnityEngine;

namespace Ulink.Runtime
{
    /// <summary>
    /// Shared comma-separated float parsing for Vector2/3/4 and Color.
    /// Used by both the runtime injector and the editor drawer.
    /// </summary>
    public static class UlinkParsing
    {
        public static Vector2 ParseVector2(string raw, Vector2 fallback = default)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            Span<float> values = stackalloc float[2];
            return TryParseFloats(raw, values) ? new Vector2(values[0], values[1]) : fallback;
        }

        public static Vector3 ParseVector3(string raw, Vector3 fallback = default)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            Span<float> values = stackalloc float[3];
            return TryParseFloats(raw, values) ? new Vector3(values[0], values[1], values[2]) : fallback;
        }

        public static Vector4 ParseVector4(string raw, Vector4 fallback = default)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            Span<float> values = stackalloc float[4];
            return TryParseFloats(raw, values) ? new Vector4(values[0], values[1], values[2], values[3]) : fallback;
        }

        public static Color ParseColor(string raw, Color fallback = default)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            Span<float> values = stackalloc float[4];
            return TryParseFloats(raw, values) ? new Color(values[0], values[1], values[2], values[3]) : fallback;
        }

        /// <summary>
        /// Splits a comma-separated string and parses each segment into the output span.
        /// For the last component, uses string end instead of searching for another comma.
        /// Returns false (triggering fallback) if there aren't enough components or any parse fails.
        /// </summary>
        private static bool TryParseFloats(string raw, Span<float> output)
        {
            int count = output.Length;
            int start = 0;
            for (var i = 0; i < count; i++)
            {
                // Last element runs to end of string; others delimit at the next comma
                int comma = i < count - 1 ? raw.IndexOf(',', start) : raw.Length;
                if (comma < 0) return false;

                if (!float.TryParse(raw.AsSpan(start, comma - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out output[i]))
                    return false;

                start = comma + 1;
            }

            return true;
        }
    }
}