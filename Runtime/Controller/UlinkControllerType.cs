#nullable enable
using System;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct UlinkControllerType
    {
        [SerializeField] public string TypeName;

        public Type? Type => string.IsNullOrEmpty(TypeName) ? null : Type.GetType(TypeName);

        public static UlinkControllerType Empty { get; } = new() { TypeName = string.Empty };
    }
}