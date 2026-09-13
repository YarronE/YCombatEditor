using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    public static class ActionTimingMigration
    {
        /// <summary>Preserves timeline coordinates and chooses the old first-clip ruler. Mixed clips require explicit acknowledgement.</summary>
        public static bool Migrate(SkillConfigSO config, bool acknowledgeMixedRates, out string backupPath, out string error)
        {
            backupPath = null; error = null;
            if (config == null || EditorApplication.isPlayingOrWillChangePlaymode) { error = "Select an action in Edit Mode."; return false; }
            if (config.UsesExplicitTiming) return true;
            string path = AssetDatabase.GetAssetPath(config);
            if (!path.StartsWith("Assets/",StringComparison.Ordinal)) { error = "Only project Assets can be migrated."; return false; }
            float fps = ActionTiming.FrameRate(config);
            if (!acknowledgeMixedRates && config.GetEffectiveSegments().Any(s => s?.clip != null && !Mathf.Approximately(ActionTiming.SourceRate(s),fps)))
            { error = "Mixed source FPS: confirm the first-clip timeline ruler, then review clip durations, overlap and all event positions."; return false; }
            var draft = UnityEngine.Object.Instantiate(config);
            var recovery = UnityEngine.Object.Instantiate(config);
            draft.name = recovery.name = config.name;
            bool changed = false;
            try
            {
                draft.InitializeExplicitTiming(fps);
                var issues = ActionConfigValidator.Validate(draft);
                if (ActionConfigValidator.HasErrors(issues)) { error = string.Join("\n", issues.Where(i=>i.Severity==ActionValidationSeverity.Error).Select(i=>i.Message)); return false; }
                string directory = "Assets/ActionEditorBackups/Timing";
                ActionConfigMigrationService.EnsureAssetDirectory(directory);
                backupPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + Path.GetFileName(path));
                AssetDatabase.CreateAsset(recovery,backupPath);
                AssetDatabase.SaveAssetIfDirty(recovery);
                Undo.RegisterCompleteObjectUndo(config,"Migrate Action Timing");
                changed=true;
                EditorUtility.CopySerialized(draft,config);
                EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
                return true;
            }
            catch(Exception exception)
            {
                error=exception.Message;
                if(changed) { EditorUtility.CopySerialized(recovery,config); EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config); }
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
                if(!AssetDatabase.Contains(recovery)) UnityEngine.Object.DestroyImmediate(recovery);
            }
        }
    }
}
