using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    /// <summary>
    /// Typed interface for the Ulink framework.
    /// Provides a strongly-typed <see cref="Setup"/> method for a specific VisualElement type.
    /// Implement <see cref="IUlinkController.Setup"/> explicitly to delegate to <see cref="Setup(T)"/>.
    /// </summary>
    public interface IUlinkController<in T> : IUlinkLifecycle<T> where T : VisualElement { }

    public interface IUlinkController : IUlinkController<VisualElement> { }
}