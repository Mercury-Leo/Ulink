using Ulink.Runtime;
using UnityEditor;

namespace Ulink.Editor
{
    [CustomEditor(typeof(UlinkAssetRegistry))]
    internal sealed class UlinkAssetRegistryInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "This registry is managed by the Ulink generator and cannot be edited manually.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                DrawDefaultInspector();
            }
        }
    }
}
