#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ulink.Runtime
{
    [Serializable]
    public struct AssetEntry
    {
        public string guid;
        public UnityEngine.Object asset;
    }

    public class UlinkAssetRegistry : ScriptableObject
    {
        [SerializeField] private List<AssetEntry> entries = new();

        public List<AssetEntry> Entries => entries;

        private static UlinkAssetRegistry? _instance;

        public static UlinkAssetRegistry? Instance =>
            _instance != null ? _instance : _instance = Resources.Load<UlinkAssetRegistry>("UlinkAssetRegistry");

        private Dictionary<string, UnityEngine.Object>? _lookup;

        private void OnValidate() => _lookup = null;

        public UnityEngine.Object? Get(string guid)
        {
            _lookup ??= Entries.ToDictionary(entry => entry.guid, entry => entry.asset);
            return _lookup.GetValueOrDefault(guid);
        }
    }
}