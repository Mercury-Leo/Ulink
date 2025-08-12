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
            controller?.OnSerialize(element);
            element?.RegisterCallbackOnce<AttachToPanelEvent>(_ => controller?.Bind());
        }
    }
}