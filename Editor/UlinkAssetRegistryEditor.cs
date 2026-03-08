using System.IO;
using Ulink.Runtime;
using UnityEditor;
using UnityEngine;

namespace Ulink.Editor
{
    public static class UlinkAssetRegistryEditor
    {
        static string GetResourcesPath()
        {
            string saved = UlinkSettings.instance.AssetRegistryPath;
            if (!string.IsNullOrEmpty(saved) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(saved)))
                return saved;

            string[] guids = AssetDatabase.FindAssets($"t:Script {nameof(UlinkAssetRegistryEditor)}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith($"{nameof(UlinkAssetRegistryEditor)}.cs")) continue;

                string packageRoot = Path.GetDirectoryName(Path.GetDirectoryName(path));
                string resolved = packageRoot + "/Resources/UlinkAssetRegistry.asset";
                UlinkSettings.instance.AssetRegistryPath = resolved;
                return resolved;
            }

            return "Packages/Ulink/Resources/UlinkAssetRegistry.asset";
        }

        static UlinkAssetRegistry GetOrCreate()
        {
            string path = GetResourcesPath();
            var registry = AssetDatabase.LoadAssetAtPath<UlinkAssetRegistry>(path);
            if (registry != null) return registry;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            registry = ScriptableObject.CreateInstance<UlinkAssetRegistry>();
            AssetDatabase.CreateAsset(registry, path);
            AssetDatabase.SaveAssets();
            return registry;
        }

        public static void Register(string guid, Object asset)
        {
            var registry = GetOrCreate();
            var entries = registry.Entries;
            int idx = entries.FindIndex(entry => entry.Guid == guid);
            if (idx >= 0) entries[idx] = new AssetEntry { Guid = guid, Asset = asset };
            else entries.Add(new AssetEntry { Guid = guid, Asset = asset });
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }

        public static void Unregister(string guid)
        {
            var registry = GetOrCreate();
            registry.Entries.RemoveAll(entry => entry.Guid == guid);
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
        }
    }
}