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
                tooltip = "Toggles Ulink system in the Editor. Disable: Components will not run in the editor."
            };

            runInEditor.RegisterValueChangedCallback(value => UlinkSettings.instance.RunInEditor = value.newValue);

            rootElement.Add(runInEditor);

            var disableAutoGen = new Toggle("Disable Automatic Generation")
            {
                value = settings.DisableAutomaticGeneration,
                tooltip = "When enabled, Ulink will not auto-generate on compilation or asset changes. Use the Generate button to generate manually."
            };
            disableAutoGen.RegisterValueChangedCallback(value => UlinkSettings.instance.DisableAutomaticGeneration = value.newValue);
            rootElement.Add(disableAutoGen);

            var generateButton =
                ElementsUtility.CreateButton("Generate", UlinkGenerator.GenerateControllers, "Generate Components");

            rootElement.Add(generateButton);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new UlinkSettingsProvider("Leo's Tools/Ulink", SettingsScope.Project);
        }
    }
}