#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Ulink.Runtime
{
    /// <summary>
    /// Discovers and caches all [UlinkSerializable] fields on a component type, including inherited ones.
    /// Shared by the runtime injector, editor drawer, and generator registry sync.
    /// </summary>
    public static class UlinkFieldDiscovery
    {
        private static readonly Dictionary<Type, FieldInfo[]> Cache = new();

        /// <summary>
        /// Returns all fields marked with [UlinkSerializable] on the given type and its base types.
        /// Walks up the hierarchy with DeclaredOnly to avoid duplicate entries from base classes.
        /// </summary>
        public static FieldInfo[] GetUlinkSerializableFields(Type type)
        {
            if (Cache.TryGetValue(type, out var cached)) return cached;

            var result = new List<FieldInfo>();
            var current = type;
            while (current != null && current != typeof(object))
            {
                foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.GetCustomAttribute<UlinkSerializableAttribute>() != null)
                        result.Add(field);
                }

                current = current.BaseType;
            }

            var fields = result.ToArray();
            Cache[type] = fields;
            return fields;
        }
    }
}