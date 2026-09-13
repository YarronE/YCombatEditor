using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor
{
    internal static class ActionConfigMigrationService
    {
        internal const int CurrentSchemaVersion = 1;

        internal static bool NeedsMigration(SkillConfigSO config)
        {
            return config != null && config.ActionEditorSchemaVersion < CurrentSchemaVersion;
        }

        internal static bool MigrateWithBackup(SkillConfigSO config, out string backupPath, out string error)
        {
            backupPath = string.Empty;
            error = string.Empty;
            if (config == null)
            {
                error = "No action config selected.";
                return false;
            }
            if (!NeedsMigration(config)) return true;

            string sourcePath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.StartsWith("Assets/"))
            {
                error = "Only project Assets can be migrated.";
                return false;
            }

            bool changedOriginal = false;
            try
            {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupDirectory = $"Assets/ActionEditorBackups/{stamp}";
            EnsureAssetDirectory(backupDirectory);
            backupPath = AssetDatabase.GenerateUniqueAssetPath($"{backupDirectory}/{Path.GetFileName(sourcePath)}");
            if (!AssetDatabase.CopyAsset(sourcePath, backupPath))
            {
                error = $"Failed to back up {sourcePath}.";
                return false;
            }

            // CopyAsset copies disk state, which can lag behind unsaved authoring.
            // Persist the complete in-memory state before touching the original.
            var savedBackup = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(backupPath);
            if (savedBackup == null) { error = "Backup could not be loaded."; return false; }
            EditorUtility.CopySerialized(config, savedBackup);
            EditorUtility.SetDirty(savedBackup);
            AssetDatabase.SaveAssetIfDirty(savedBackup);

                Undo.RecordObject(config, "Migrate Action Config");
                changedOriginal = true;
                config.SetActionEditorSchemaVersion(CurrentSchemaVersion);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                var backup = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(backupPath);
                if (changedOriginal && backup != null)
                {
                    EditorUtility.CopySerialized(backup, config);
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssetIfDirty(config);
                }
                if (changedOriginal) AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceUpdate);
                return false;
            }
        }

        internal static bool RestoreWithBackup(SkillConfigSO config, SkillConfigSO backup,
            out string recoveryPath, out string error)
        {
            recoveryPath = string.Empty;
            error = string.Empty;
            string sourcePath = AssetDatabase.GetAssetPath(config);
            string backupPath = AssetDatabase.GetAssetPath(backup);
            if (config == null || backup == null || config == backup ||
                !sourcePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !backupPath.StartsWith("Assets/ActionEditorBackups/", StringComparison.Ordinal))
            {
                error = "Select a project action and a different asset from Assets/ActionEditorBackups.";
                return false;
            }
            if (backup.ActionEditorSchemaVersion > CurrentSchemaVersion)
            {
                error = "The backup requires a newer action editor.";
                return false;
            }
            SkillConfigSO recovery = null;
            try
            {
                string directory = $"Assets/ActionEditorBackups/{DateTime.Now:yyyyMMdd-HHmmss}";
                EnsureAssetDirectory(directory);
                recoveryPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{Path.GetFileNameWithoutExtension(sourcePath)}-before-restore.asset");
                recovery = ScriptableObject.CreateInstance<SkillConfigSO>();
                EditorUtility.CopySerialized(config, recovery);
                AssetDatabase.CreateAsset(recovery, recoveryPath);
                AssetDatabase.SaveAssetIfDirty(recovery);

                Undo.RegisterCompleteObjectUndo(config, "Restore Action Config");
                string name = config.name;
                EditorUtility.CopySerialized(backup, config);
                config.name = name;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (recovery != null && AssetDatabase.Contains(recovery))
                {
                    EditorUtility.CopySerialized(recovery, config);
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssetIfDirty(config);
                }
                return false;
            }
        }

        internal static void EnsureAssetDirectory(string directory)
        {
            if (!ActionEditorSettings.TryNormalizeAssetDirectory(directory, out string normalized))
                throw new ArgumentException("Directory must be inside Assets.", nameof(directory));
            if (AssetDatabase.IsValidFolder(normalized)) return;
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
