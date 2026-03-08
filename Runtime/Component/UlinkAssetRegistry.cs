#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct AssetEntry
    {
        public string Guid;
        public UnityEngine.Object Asset;
    }

    public class UlinkAssetRegistry : ScriptableObject
    {
        [SerializeField] private List<AssetEntry> entries = new();

        public List<AssetEntry> Entries => entries;

        private static UlinkAssetRegistry? _instance;

        public static UlinkAssetRegistry? Instance =>
            _instance != null ? _instance : (_instance = Resources.Load<UlinkAssetRegistry>("UlinkAssetRegistry"));

        public UnityEngine.Object? Get(string guid)
        {
            foreach (var entry in Entries)
            {
                if (entry.Guid == guid) return entry.Asset;
            }

            return null;
        }
    }
}