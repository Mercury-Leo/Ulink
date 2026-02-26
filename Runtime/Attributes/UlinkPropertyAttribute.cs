using System;

namespace Ulink.Runtime
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public class UlinkPropertyAttribute : Attribute { }
}