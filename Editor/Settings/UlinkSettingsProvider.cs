using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    internal sealed class UlinkSettingsProvider : SettingsProvider
    {
        public UlinkSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(
            path, scopes, keywords) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);

            var settings = UlinkSettings.instance;

            rootElement.Add(ElementsUtility.CreateTitle("Ulink"));

            var runInEditor = new Toggle("Run In Editor")
            {
                value = settings.RunInEditor,
                tooltip = "Toggles Ulink system in the Editor. Disable: Controllers will not run in the editor."
            };

            runInEditor.RegisterValueChangedCallback(value => UlinkSettings.instance.RunInEditor = value.newValue);

            rootElement.Add(runInEditor);

            var generateButton =
                ElementsUtility.CreateButton("Generate", UlinkGenerator.GenerateControllers, "Generate Controllers");

            rootElement.Add(generateButton);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new UlinkSettingsProvider("Leo's Tools/Ulink", SettingsScope.Project);
        }
    }
}