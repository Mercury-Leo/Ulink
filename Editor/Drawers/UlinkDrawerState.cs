using System;
using System.Collections.Generic;
using Ulink.Runtime;
using UnityEditor;

namespace Ulink.Editor
{
    /// <summary>
    /// Encapsulates read/write access to the serialized UlinkComponentsType raw string.
    /// The raw string has two sections separated by \uE000: type AQN list and per-component property data.
    /// All mutations go through WriteBack which updates the SerializedProperty and applies changes.
    /// </summary>
    internal sealed class UlinkDrawerState
    {
        private const char TypeSeparator = ';';

        private readonly SerializedProperty _rawProperty; // TypeNamesRaw — the single encoded string
        private readonly SerializedProperty _property; // parent property, needed for ApplyModifiedProperties

        public UlinkDrawerState(SerializedProperty property)
        {
            _property = property;
            _rawProperty = property.FindPropertyRelative(nameof(UlinkComponentsType.TypeNamesRaw));
        }

        public string[] GetCurrentNames()
        {
            string part = RawTypes();
            return string.IsNullOrEmpty(part) ? Array.Empty<string>() : part.Split(TypeSeparator);
        }

        public void SetNames(string[] names) =>
            WriteBack(string.Join(TypeSeparator.ToString(), names), RawData());

        public void SetComponentPropertyValue(string aqn, string fieldName, string value)
        {
            var all = UlinkComponentsType.ParseAllData(_rawProperty.stringValue);
            if (!all.ContainsKey(aqn)) all[aqn] = new Dictionary<string, string>();
            all[aqn][fieldName] = value;
            WriteBack(RawTypes(), UlinkComponentsType.SerializeAllData(all));
        }

        public string GetStoredPropertyValue(string aqn, string fieldName)
        {
            var all = UlinkComponentsType.ParseAllData(_rawProperty.stringValue);
            return all.TryGetValue(aqn, out var fields) && fields.TryGetValue(fieldName, out string value)
                ? value
                : string.Empty;
        }

        public void RemoveComponentData(string aqn)
        {
            var all = UlinkComponentsType.ParseAllData(_rawProperty.stringValue);
            if (all.Remove(aqn))
                WriteBack(RawTypes(), UlinkComponentsType.SerializeAllData(all));
        }

        private string RawTypes() => UlinkComponentsType.TypesSection(_rawProperty.stringValue);

        private string RawData() => UlinkComponentsType.DataSection(_rawProperty.stringValue);

        private void WriteBack(string typesSection, string dataSection)
        {
            _rawProperty.stringValue = UlinkComponentsType.Combine(typesSection, dataSection);
            _property.serializedObject.ApplyModifiedProperties();
        }
    }
}