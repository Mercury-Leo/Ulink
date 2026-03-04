using JetBrains.Annotations;
using Ulink.Runtime;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    [UsedImplicitly]
    public class UxmlUlinkComponentsAttributeConverter : UxmlAttributeConverter<UlinkComponentsType>
    {
        public override UlinkComponentsType FromString(string value) => new() { TypeNamesRaw = value ?? string.Empty };

        public override string ToString(UlinkComponentsType value) => value.TypeNamesRaw ?? string.Empty;
    }
}