using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    public static class UlinkExtensions
    {
        /// <summary>
        /// Manually Initialize a Ulink component outside the Ulink life cycle.
        /// This can be used when adding a component not via the Builder.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="element"></param>
        public static void Initialize<T>(this IUlinkComponent<T> component, T element) where T : VisualElement
        {
            component?.Setup(element);
            element?.RegisterCallbackOnce<AttachToPanelEvent>(_ => component?.OnAttach());
            element?.RegisterCallbackOnce<DetachFromPanelEvent>(_ => component?.OnDetach());
        }
    }
}