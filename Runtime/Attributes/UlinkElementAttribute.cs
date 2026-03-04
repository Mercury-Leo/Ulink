using System;

namespace Ulink.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UlinkElementAttribute : Attribute
    {
        /// <summary>
        /// Marks this Visual Element to generate a <see cref="IUlinkComponent"/> partial class
        /// </summary>
        public UlinkElementAttribute() { }
    }
}