#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Ulink.Runtime
{
    /// <summary>
    /// Marker interface applied to all VisualElements decorated with [UlinkElement].
    /// Exposes component query methods common to all Ulink elements.
    /// </summary>
    public interface IUlinkElement
    {
        bool TryGetComponent<TComponent>([NotNullWhen(true)] out TComponent? component) where TComponent : class;
        TComponent? GetComponent<TComponent>() where TComponent : class;
        TComponent[] GetComponents<TComponent>() where TComponent : class;
        void GetComponents<TComponent>(List<TComponent> results) where TComponent : class;
    }
}