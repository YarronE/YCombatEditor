using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    [FilePath("ProjectSettings/ActionEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ActionEditorSettings : ScriptableSingleton<ActionEditorSettings>
    {
        [SerializeField] string defaultAssetDirectory = "Assets/ActionEditor/Skills";
        [SerializeField, Min(1f)] float fallbackFrameRate = 30f;

        internal string DefaultAssetDirectory
        {
            get
            {
                return TryNormalizeAssetDirectory(defaultAssetDirectory, out var path)
                    ? path : "Assets/ActionEditor/Skills";
            }
            set
            {
                if (!TryNormalizeAssetDirectory(value, out var path))
                    throw new System.ArgumentException("The directory must be inside Assets without relative traversal.", nameof(value));
                defaultAssetDirectory = path;
                Save(true);
            }
        }

        internal static bool TryNormalizeAssetDirectory(string value, out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string normalized = value.Replace('\\', '/').TrimEnd('/');
            if (normalized != "Assets" && !normalized.StartsWith("Assets/", System.StringComparison.Ordinal)) return false;
            foreach (string part in normalized.Split('/'))
                if (part.Length == 0 || part == "." || part == ".." ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            path = normalized;
            return true;
        }

        internal float FallbackFrameRate
        {
            get => Mathf.Max(1f, fallbackFrameRate);
            set
            {
                fallbackFrameRate = Mathf.Max(1f, value);
                Save(true);
            }
        }

        [SettingsProvider]
        static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/ACT Action Editor", SettingsScope.Project)
            {
                label = "ACT Action Editor",
                guiHandler = _ =>
                {
                    var settings = instance;
                    EditorGUI.BeginChangeCheck();
                    string path = EditorGUILayout.TextField("Default Asset Directory", settings.DefaultAssetDirectory);
                    float fps = EditorGUILayout.FloatField("Fallback Frame Rate", settings.FallbackFrameRate);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!TryNormalizeAssetDirectory(path, out var normalized))
                            EditorGUILayout.HelpBox("The directory must be inside Assets.", MessageType.None);
                        else
                            settings.defaultAssetDirectory = normalized;
                        settings.fallbackFrameRate = Mathf.Max(1f, fps);
                        settings.Save(true);
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Existing assets are never moved when this setting changes.", MessageType.None);
                    if (GUILayout.Button("Open Directory") && Directory.Exists(settings.DefaultAssetDirectory))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(settings.DefaultAssetDirectory);
                        if (asset != null) EditorGUIUtility.PingObject(asset);
                    }
                },
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "action", "skill", "timeline", "asset", "frame" })
            };
        }
    }
}
