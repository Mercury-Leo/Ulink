#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct UlinkComponentsType
    {
        // ── Separators ────────────────────────────────────────────────────────
        // All separators are Unicode Private Use Area characters (U+E000–U+F8FF).
        // They are valid XML 1.0, never appear in C# AQNs, and won't be typed by users.
        //
        // TypeNamesRaw layout:
        //   "{aqn1};{aqn2}\uE000{component-data-section}"
        private const char TypeSeparator = ';'; // between AQNs (plain ASCII, valid XML)
        private const char SectionSeparator = '\uE000'; // between types section and data section
        private const char ComponentSeparator = '\uE001'; // between per-component entries in data section
        private const char DataSeparator = '\uE002'; // between AQN and its field data
        private const char FieldSeparator = '\uE003'; // between individual field=value pairs

        [SerializeField] public string TypeNamesRaw;

        // ── Types cache ──────────────────────────────────────────────────────
        // Avoids re-resolving Type.GetType() on every access when TypeNamesRaw hasn't changed.
        // Keyed on the types section of the raw string (before \uE000).
        [NonSerialized] private Type?[]? _typesCache;
        [NonSerialized] private string? _typesCacheKey;

        // ── Public API ────────────────────────────────────────────────────────

        public string[] TypeNames
        {
            get
            {
                string part = TypesSection(TypeNamesRaw);
                return string.IsNullOrEmpty(part) ? Array.Empty<string>() : part.Split(TypeSeparator);
            }
        }

        public Type?[] Types
        {
            get
            {
                string key = TypesSection(TypeNamesRaw);
                if (_typesCache != null && key == _typesCacheKey)
                    return _typesCache;

                _typesCacheKey = key;
                _typesCache = string.IsNullOrEmpty(key)
                    ? Array.Empty<Type?>()
                    : key.Split(TypeSeparator).Select(Type.GetType).ToArray();
                return _typesCache;
            }
        }

        public static UlinkComponentsType Empty { get; } = new() { TypeNamesRaw = string.Empty };

        /// <summary>Returns field-name → value pairs stored for one component by its AQN.</summary>
        public Dictionary<string, string> GetDataFor(string assemblyQualifiedName)
        {
            var result = new Dictionary<string, string>();
            string data = DataSection(TypeNamesRaw);
            if (string.IsNullOrEmpty(data)) return result;

            foreach (string? entry in data.Split(ComponentSeparator))
            {
                int separator = entry.IndexOf(DataSeparator);
                if (separator < 0) continue;
                if (entry[..separator] != assemblyQualifiedName) continue;

                ParseFieldPairs(entry[(separator + 1)..], result);
                break;
            }

            return result;
        }

        // ── Helpers used by the editor drawer ────────────────────────────────

        /// <summary>Returns the AQN list part of a raw string (before \uE000).</summary>
        public static string TypesSection(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int idx = raw.IndexOf(SectionSeparator);
            return idx < 0 ? raw : raw[..idx];
        }

        /// <summary>Returns the property-data part of a raw string (after \uE000).</summary>
        public static string DataSection(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            int idx = raw.IndexOf(SectionSeparator);
            return idx < 0 ? string.Empty : raw[(idx + 1)..];
        }

        /// <summary>Joins a types section and a data section back into a single raw string.</summary>
        public static string Combine(string typesSection, string dataSection) =>
            string.IsNullOrEmpty(dataSection) ? typesSection : typesSection + SectionSeparator + dataSection;

        /// <summary>Parses the data section of a raw string into a nested dictionary.</summary>
        public static Dictionary<string, Dictionary<string, string>> ParseAllData(string raw)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            string data = DataSection(raw);
            if (string.IsNullOrEmpty(data)) return result;

            foreach (string? entry in data.Split(ComponentSeparator))
            {
                int separator = entry.IndexOf(DataSeparator);
                if (separator < 0) continue;

                string aqn = entry[..separator];
                var fields = new Dictionary<string, string>();
                ParseFieldPairs(entry[(separator + 1)..], fields);
                result[aqn] = fields;
            }

            return result;
        }

        /// <summary>Serializes a nested dictionary back into a data section string.</summary>
        public static string SerializeAllData(Dictionary<string, Dictionary<string, string>> allData)
        {
            var entries = allData
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    string fields = string.Join(
                        FieldSeparator.ToString(),
                        kvp.Value.Select(field => $"{field.Key}={field.Value}"));
                    return $"{kvp.Key}{DataSeparator}{fields}";
                });

            return string.Join(ComponentSeparator.ToString(), entries);
        }

        /// <summary>Parses "key=value" pairs separated by FieldSeparator into the target dictionary.</summary>
        private static void ParseFieldPairs(string fieldData, Dictionary<string, string> target)
        {
            foreach (string? field in fieldData.Split(FieldSeparator))
            {
                if (string.IsNullOrEmpty(field)) continue;
                int eq = field.IndexOf('=');
                if (eq < 0) continue;
                target[field[..eq]] = field[(eq + 1)..];
            }
        }
    }
}