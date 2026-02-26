using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    public interface IUlinkComponent<in T> where T : VisualElement
    {
        /// <summary>
        /// When true, this controller is excluded from editor preview and only runs at runtime.
        /// </summary>
        public bool IsRuntimeOnly { get; }

        /// <summary>
        /// Called once when the controller is first assigned to its element.
        /// Use this to store a reference to the element and perform initial configuration.
        /// </summary>
        void Setup(T element);

        /// <summary>
        /// Called when the element is attached to a panel (AttachToPanelEvent).
        /// Use this to register event listeners and start any active logic.
        /// </summary>
        void OnAttach();

        /// <summary>
        /// Called when the element is detached from a panel (DetachFromPanelEvent).
        /// Use this to unregister event listeners and clean up any active logic.
        /// </summary>
        void OnDetach();
    }

    public interface IUlinkComponent : IUlinkComponent<VisualElement> { }
}