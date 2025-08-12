using UnityEngine.UIElements;

namespace Ulink.Runtime
{
    /// <summary>
    /// Interface for the Ulink framework.
    /// </summary>
    public interface IUlinkController
    {
        public bool RuntimeOnly { get; } 
        void OnSerialize(VisualElement element);
        void Bind();
        void Unbind();
    }
}
