using UnityEngine;

namespace Ulink.Runtime
{
    public abstract class UlinkFactory : ScriptableObject
    {
        /// <summary>
        /// Creates a <see cref="IUlinkController"/>
        /// This is where setup for the controller will be done
        /// </summary>
        /// <returns></returns>
        public abstract IUlinkController CreateController();
    }
}