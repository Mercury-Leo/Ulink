#nullable enable
using System;
using System.Collections.Generic;

namespace Ulink.Runtime
{
    /// <summary>
    /// Maps component types to allocation-free factory delegates emitted by the Ulink generator,
    /// replacing reflection-based Activator.CreateInstance on the element construction hot path.
    ///
    /// The factories are plain <c>() =&gt; new Concrete()</c> lambdas, so this is AOT/IL2CPP-safe
    /// (no Reflection.Emit or Expression.Compile). Any type without a generated factory — e.g. one
    /// with a non-public constructor, or when generation hasn't run yet — falls back to
    /// Activator.CreateInstance, so behaviour is always correct even if unregistered.
    /// </summary>
    public static class UlinkComponentRegistry
    {
        private static readonly Dictionary<Type, Func<object>> Factories = new();

        /// <summary>Registers a factory for <paramref name="type"/>. Idempotent; last registration wins.</summary>
        public static void Register(Type type, Func<object> factory) => Factories[type] = factory;

        /// <summary>
        /// Creates an instance of <paramref name="type"/> via its registered factory, falling back
        /// to Activator.CreateInstance when no factory is registered.
        /// </summary>
        public static object? Create(Type type)
            => Factories.TryGetValue(type, out var factory) ? factory() : Activator.CreateInstance(type);
    }
}