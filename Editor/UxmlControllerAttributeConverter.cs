using System;
using Ulink.Runtime;
using UnityEditor.UIElements;

namespace Ulink.Editor
{
    public class UxmlControllerAttributeConverter : UxmlAttributeConverter<ControllerType>
    {
        public override ControllerType FromString(string value)
            => string.IsNullOrEmpty(value) ? ControllerType.Empty : new ControllerType { Type = Type.GetType(value) };

        public override string ToString(ControllerType value)
            => value.Type?.FullName ?? string.Empty;
    }
}