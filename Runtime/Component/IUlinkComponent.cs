using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    public interface IUlinkComponent<in T> : IUlinkLifecycle<T> where T : VisualElement { }

    public interface IUlinkComponent : IUlinkComponent<VisualElement> { }
}