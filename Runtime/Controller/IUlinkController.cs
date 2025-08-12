using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    /// <summary>
    /// Interface for the Ulink framework.
    /// </summary>
    public interface IUlinkController
    {
        public bool RuntimeOnly { get; } 
        void Bind();
        void Unbind();
        void OnSerialize(VisualElement element);
    }
}