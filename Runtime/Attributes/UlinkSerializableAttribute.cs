using System;

namespace Ulink.Runtime
{
    /// <summary>
    /// Marks a field or property as a Ulink serializable property.
    /// Will sync the property with the UIBuilder.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UlinkSerializableAttribute : Attribute { }
}
