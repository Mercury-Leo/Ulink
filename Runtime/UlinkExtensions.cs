using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    public static class UlinkExtensions
    {
        /// <summary>
        /// Manually Initialize a Ulink controller outside the Ulink life cycle.
        /// This can be used when adding a controller not via the Builder.
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="element"></param>
        public static void Initialize(this IUlinkController controller, VisualElement element)
        {
            controller?.Setup(element);
            element?.RegisterCallbackOnce<AttachToPanelEvent>(_ => controller?.OnAttach());
            element?.RegisterCallbackOnce<DetachFromPanelEvent>(_ => controller?.OnDetach());
        }

        public static void Initialize<T>(this IUlinkComponent<T> component, T element) where T : VisualElement
        {
            component?.Setup(element);
            element?.RegisterCallbackOnce<AttachToPanelEvent>(_ => component?.OnAttach());
            element?.RegisterCallbackOnce<DetachFromPanelEvent>(_ => component?.OnDetach());
        }
    }
}