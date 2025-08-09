#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ulink.Editor
{
    internal static class ElementsUtility
    {
        public static VisualElement CreateTitle(string title)
        {
            return new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 18,
                    marginBottom = 10
                }
            };
        }

        public static VisualElement CreateBrowseField(string title, string initialValue, string titleTooltip,
            string browseTooltip, Action<string> onFolderSelected)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 5
                }
            };

            var textField = new TextField(title)
            {
                value = initialValue,
                isReadOnly = true,
                style =
                {
                    flexGrow = 1,
                    marginRight = 5
                },
                tooltip = titleTooltip
            };

            row.Add(textField);

            var browseButton = new Button(() =>
            {
                var selectedFolder =
                    EditorUtility.OpenFolderPanel("Select folder", Application.dataPath, string.Empty);

                if (string.IsNullOrEmpty(selectedFolder))
                {
                    return;
                }

                if (!selectedFolder.StartsWith(Application.dataPath))
                {
                    EditorUtility.DisplayDialog("Invalid Folder",
                        "Please pick a folder inside the project's Assets directory.", "OK");
                    return;
                }

                var result = "Assets" + selectedFolder[Application.dataPath.Length..];
                onFolderSelected?.Invoke(result);
                textField.value = result;
            })
            {
                text = "Browse",
                style =
                {
                    width = 80,
                },
                tooltip = browseTooltip
            };

            row.Add(browseButton);

            return row;
        }

        public static VisualElement CreateButton(string label, Action? clickAction = null, string? tooltip = null)
        {
            return new Button(clickAction)
            {
                text = label,
                style =
                {
                    marginTop = 20,
                    marginLeft = new Length(30, LengthUnit.Percent),
                    marginRight = new Length(30, LengthUnit.Percent),
                },
                tooltip = tooltip
            };
        }
    }
}