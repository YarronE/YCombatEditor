using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    public static class ActionEditorHelp
    {
        [MenuItem("Tools/ACT Action Editor/Help/User Guide (Chinese)")]
        public static void OpenUserGuide() => Open("Documentation~/UserGuide.zh-CN.md");

        [MenuItem("Tools/ACT Action Editor/Help/Getting Started")]
        public static void OpenTutorial() => Open("Documentation~/index.html");

        [MenuItem("Tools/ACT Action Editor/Help/Agent Guide")]
        public static void OpenAgentGuide() => Open("Documentation~/AgentGuide.md");

        static void Open(string relativePath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ActionEditorHelp).Assembly);
            if (package == null) { Debug.LogError("ACT package location could not be resolved."); return; }
            string path = Path.Combine(package.resolvedPath, relativePath);
            if (!File.Exists(path)) { Debug.LogError("ACT documentation is missing: " + relativePath); return; }
            Application.OpenURL(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        }
    }
}
