using UnityEngine;

namespace Ulink.Runtime
{
    public abstract class UlinkFactory : ScriptableObject
    {
        public abstract IUlinkController CreateController();

        private static NullUlinkFactory _nullFactory;

        public static UlinkFactory NullFactory
        {
            get
            {
                if (_nullFactory != null)
                {
                    return _nullFactory;
                }
                _nullFactory = CreateInstance<NullUlinkFactory>();
                _nullFactory.name = "Null Ulink Factory";

                return _nullFactory;
            }
        }
    }

    public class NullUlinkFactory : UlinkFactory
    {
        public override IUlinkController CreateController() => null;
    }
}