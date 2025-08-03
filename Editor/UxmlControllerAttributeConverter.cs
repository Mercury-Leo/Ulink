using Ulink.Runtime;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    public class UxmlControllerAttributeConverter : UxmlAttributeConverter<ControllerType>
    {
        public override ControllerType FromString(string value) => string.IsNullOrEmpty(value)
            ? ControllerType.Empty
            : new ControllerType { TypeName = value };

        public override string ToString(ControllerType value) => value.TypeName;
    }
}