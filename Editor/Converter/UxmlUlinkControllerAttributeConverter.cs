using JetBrains.Annotations;
using Ulink.Runtime;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    [UsedImplicitly]
    public class UxmlUlinkControllerAttributeConverter : UxmlAttributeConverter<UlinkControllerType>
    {
        public override UlinkControllerType FromString(string value) => string.IsNullOrEmpty(value)
            ? UlinkControllerType.Empty
            : new UlinkControllerType { TypeName = value };

        public override string ToString(UlinkControllerType value) => value.TypeName;
    }
}