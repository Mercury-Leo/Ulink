#nullable enable
using UnityEditor;
using UnityEngine;

namespace Ulink.Editor
{
    [FilePath("ProjectSettings/Ulink.asset", FilePathAttribute.Location.ProjectFolder)]
    public class UlinkSettings : ScriptableSingleton<UlinkSettings>
    {
        [SerializeField] private string? targetFolder;

        private const string DefaultTargetFolder = "Assets";

        public string TargetFolder
        {
            get => targetFolder ?? DefaultTargetFolder;
            set
            {
                if (targetFolder == value)
                {
                    return;
                }

                targetFolder = value;
                SaveDirty();
            }
        }

        private void Awake()
        {
            SetDefaultFolder();
        }

        private void SetDefaultFolder()
        {
            if (targetFolder is null)
            {
                TargetFolder = DefaultTargetFolder;
            }
        }

        private void SaveDirty()
        {
            Save(this);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}