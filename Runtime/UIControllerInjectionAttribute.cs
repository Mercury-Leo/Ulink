using System;

namespace Ulink.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UlinkAttribute : Attribute
    {
        /// <summary>
        /// Marks this Visual Element to generate a <see cref="IUIController"/> partial class
        /// </summary>
        public UlinkAttribute() { }
    }
}