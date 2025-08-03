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

            rootElement.Add(Utility.CreateTitle("Ulink"));

            var targetRow = Utility.CreateBrowseField("Target Folder", settings.TargetFolder,
                "Where the generated class will be created at",
                "Folder to create the generated controller files",
                result => settings.TargetFolder = result);

            rootElement.Add(targetRow);

            var generateButton =
                Utility.CreateButton("Generate", UlinkGenerator.GenerateControllers, "Generate Controllers");

            rootElement.Add(generateButton);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new UlinkSettingsProvider("Leo's Tools/Ulink", SettingsScope.Project);
        }
    }
}