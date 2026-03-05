using System;

namespace Ulink.Runtime
{
    /// <summary>
    /// Marks a field or property as a Ulink property.
    /// Will sync the property with the UIBuilder.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UlinkPropertyAttribute : Attribute { }
}