using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    /// <summary>
    /// Interface for the Ulink framework.
    /// </summary>
    public interface IUIController
    {
        /// <summary>
        /// Will run whenever it is assigned via the Builder
        /// </summary>
        /// <param name="element"></param>
        void Initialize(VisualElement element);
    }
}