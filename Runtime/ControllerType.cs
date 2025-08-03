#nullable enable
using System;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct ControllerType
    {
        [SerializeField] public string TypeName;

        public Type? Type => string.IsNullOrEmpty(TypeName) ? null : Type.GetType(TypeName);

        public static ControllerType Empty => new() { TypeName = string.Empty };
    }
}