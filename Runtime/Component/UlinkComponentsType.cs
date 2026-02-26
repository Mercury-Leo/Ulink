#nullable enable
using System;
using System.Linq;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct UlinkComponentsType
    {
        private const char Separator = ';';

        [SerializeField] public string TypeNamesRaw;

        public string[] TypeNames => string.IsNullOrEmpty(TypeNamesRaw)
            ? Array.Empty<string>()
            : TypeNamesRaw.Split(Separator);

        public Type?[] Types => TypeNames.Select(Type.GetType).ToArray();

        public static UlinkComponentsType Empty { get; } = new() { TypeNamesRaw = string.Empty };
    }
}
