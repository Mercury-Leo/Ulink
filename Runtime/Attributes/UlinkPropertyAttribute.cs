using System;

namespace Ulink.Runtime
{
    /// <summary>
    /// Marks a field or property as a Ulink property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public class UlinkPropertyAttribute : Attribute { }
}