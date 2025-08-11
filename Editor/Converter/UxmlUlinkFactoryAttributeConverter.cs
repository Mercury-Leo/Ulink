using Ulink.Runtime;
using UnityEditor;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    public class UxmlUlinkFactoryAttributeConverter : UxmlAttributeConverter<UlinkFactory>
    {
        public override UlinkFactory FromString(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : AssetDatabase.LoadAssetAtPath<UlinkFactory>(value.Trim());
        }

        public override string ToString(UlinkFactory value)
        {
            return value == null ? string.Empty : AssetDatabase.GetAssetPath(value);
        }
    }
}