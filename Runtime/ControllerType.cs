#nullable enable
using System;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct ControllerType
    {
        [SerializeField] private string typeName;

        public Type? Type
        {
            get => string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
            set => typeName = value?.FullName ?? string.Empty;
        }

        public static ControllerType Empty => new() { typeName = string.Empty };
    }
}