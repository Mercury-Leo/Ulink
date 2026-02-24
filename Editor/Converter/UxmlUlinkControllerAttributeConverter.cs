using JetBrains.Annotations;
using Ulink.Runtime;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    [UsedImplicitly]
    public class UxmlUlinkControllerAttributeConverter : UxmlAttributeConverter<ControllerType>
    {
        public override ControllerType FromString(string value) => string.IsNullOrEmpty(value)
            ? ControllerType.Empty
            : new ControllerType { TypeName = value };

        public override string ToString(ControllerType value) => value.TypeName;
    }
}