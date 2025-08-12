#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ulink.Editor
{
    [FilePath("ProjectSettings/Ulink.asset", FilePathAttribute.Location.ProjectFolder)]
    public class UlinkSettings : ScriptableSingleton<UlinkSettings>
    {
        private const string AssetPath = "ProjectSettings/Ulink.asset";
        private const string UlinkSymbol = "ULINK_EDITOR";

        [SerializeField] private bool runInEditor = true;

        public bool RunInEditor
        {
            get => runInEditor;
            set
            {
                if (runInEditor == value)
                {
                    return;
                }

                runInEditor = value;
                SaveDirty();
                DefineSymbolUtility.SetDefineSymbol(UlinkSymbol, value);
            }
        }

        private void OnEnable()
        {
            if (!File.Exists(AssetPath))
            {
                runInEditor = true;
                Save(true);
                AssetDatabase.SaveAssets();
            }

            DefineSymbolUtility.SetDefineSymbol(UlinkSymbol, runInEditor);
        }

        private void Awake()
        {
            DefineSymbolUtility.SetDefineSymbol(UlinkSymbol, runInEditor);
        }

        private void SaveDirty()
        {
            Save(this);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public static class DefineSymbolUtility
    {
        public static void SetDefineSymbol(string symbol, bool enable)
        {
            var target = GetCurrentTarget();
            SetDefineForCurrentTarget(target, symbol, enable);
        }

        public static void SetDefineForCurrentTarget(NamedBuildTarget target, string symbol, bool enable)
        {
            PlayerSettings.GetScriptingDefineSymbols(target, out string[]? symbols);

            var set = new HashSet<string>(symbols, StringComparer.Ordinal);
            bool changed = enable ? set.Add(symbol) : set.Remove(symbol);

            if (!changed)
            {
                return;
            }

            PlayerSettings.SetScriptingDefineSymbols(target, set.ToArray());
        }

        public static NamedBuildTarget GetCurrentTarget()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);

            if (group == BuildTargetGroup.Standalone &&
                EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server)
            {
                return NamedBuildTarget.Server;
            }

            return NamedBuildTarget.FromBuildTargetGroup(group);
        }
    }
}