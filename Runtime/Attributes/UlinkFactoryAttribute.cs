using System;

namespace Ulink.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class UlinkFactoryAttribute : Attribute
    {
        /// <summary>
        /// Marks this Visual Element to generate a <see cref="IUlinkController"/> partial class
        /// </summary>
        public UlinkFactoryAttribute() { }
    }
}