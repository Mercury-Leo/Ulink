using System;

namespace Ulink.Runtime
{
    /// <summary>
    /// Marks the ulink component to work only in runtime and not in editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UlinkRuntimeOnlyAttribute : Attribute { }
}