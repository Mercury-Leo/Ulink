#nullable enable
using System;
using System.Collections.Generic;

namespace Ulink.Runtime
{
    /// <summary>
    /// Maps component types to generated, strongly-typed field injectors that assign
    /// [UlinkSerializable] fields directly — no reflection FieldInfo lookup or SetValue, and no
    /// boxing for primitive/struct fields. Emitted by the Ulink generator alongside the component
    /// factories.
    ///
    /// Only components whose serializable fields are all accessible from generated code
    /// (public or internal in the same assembly) get a typed injector; anything else — including
    /// types with private fields or when generation hasn't run — falls back to the reflection-based
    /// <see cref="UlinkSerializableInjector"/>, so behaviour is always correct.
    /// </summary>
    public static class UlinkInjectorRegistry
    {
        private static readonly Dictionary<Type, Action<object, UlinkComponentsType>> Injectors = new();

        public static void Register(Type type, Action<object, UlinkComponentsType> injector)
            => Injectors[type] = injector;

        public static void Inject(object instance, Type type, UlinkComponentsType data)
        {
            if (Injectors.TryGetValue(type, out var injector))
                injector(instance, data);
            else
                UlinkSerializableInjector.Inject(instance, data, type.AssemblyQualifiedName);
        }
    }
}