using Ethan.ActionEditor.Editor;
using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor.Tests
{
    public sealed class ActionEditorTransactionTests
    {
        [Test]
        public void Gesture_MultipleUpdatesAreOneUndoAndDoNotMergeNextGesture()
        {
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            config.moveSegmentList.Add(new Global.MoveSegment { keyNumber = 1, endKeyNumber = 4 });
            var gesture = new ActionEditorGesture();
            try
            {
                for (int frame = 2; frame <= 5; frame++)
                {
                    gesture.Record(config, "Move Event");
                    config.moveSegmentList[0].keyNumber = frame;
                    config.moveSegmentList[0].endKeyNumber = frame + 3;
                    Undo.FlushUndoRecordObjects();
                }
                gesture.Complete();
                gesture.Record(config, "Move Event");
                config.moveSegmentList[0].keyNumber = 9;
                gesture.Complete();
                Undo.PerformUndo();
                Assert.That(config.moveSegmentList[0].keyNumber, Is.EqualTo(5));
                Undo.PerformUndo();
                Assert.That(config.moveSegmentList[0].keyNumber, Is.EqualTo(1));
                Assert.That(config.moveSegmentList[0].endKeyNumber, Is.EqualTo(4));
                Undo.PerformRedo();
                Assert.That(config.moveSegmentList[0].keyNumber, Is.EqualTo(5));
            }
            finally { gesture.Complete(); Undo.ClearUndo(config); Object.DestroyImmediate(config); }
        }

        [TestCase("Assets", true)]
        [TestCase("Assets/ActionEditor/Skills", true)]
        [TestCase("Assets2/Skills", false)]
        [TestCase("Assets/../Packages", false)]
        [TestCase("Assets/./Skills", false)]
        [TestCase("Assets//Skills", false)]
        [TestCase("C:/Skills", false)]
        public void AssetDirectory_RejectsPathsOutsideAssets(string path, bool valid)
        {
            Assert.That(ActionEditorSettings.TryNormalizeAssetDirectory(path, out _), Is.EqualTo(valid));
        }

        [Test]
        public void Record_AllowsNativeUndo()
        {
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            config.skillID = 1;
            ActionEditorTransaction.Record(config, "Change Skill Id");
            config.skillID = 2;
            ActionEditorTransaction.MarkChanged(config);
            Undo.PerformUndo();
            Assert.That(config.skillID, Is.EqualTo(1));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Migration_CreatesBackupBeforeChangingSchema()
        {
            const string testFolder = "Assets/__ActionEditorProductizationTests";
            const string assetPath = testFolder + "/LegacyAction.asset";
            ActionConfigMigrationService.EnsureAssetDirectory(testFolder);
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            AssetDatabase.CreateAsset(config, assetPath);

            string backupPath = string.Empty;
            try
            {
                Assert.That(ActionConfigMigrationService.MigrateWithBackup(config, out backupPath, out var error), Is.True, error);
                Assert.That(config.ActionEditorSchemaVersion, Is.EqualTo(ActionConfigMigrationService.CurrentSchemaVersion));
                Assert.That(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(backupPath), Is.Not.Null);
            }
            finally
            {
                if (!string.IsNullOrEmpty(backupPath))
                {
                    AssetDatabase.DeleteAsset(backupPath);
                    string directory = Path.GetDirectoryName(backupPath)?.Replace('\\', '/');
                    if (directory != null && Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                        AssetDatabase.DeleteAsset(directory);
                }
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(testFolder);
            }
        }

        [Test]
        public void MigrationAndRestorePreserveUnsavedDataGuidAndNativeUndo()
        {
            const string path = "Assets/__ActionEditorRestoreTest.asset";
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            config.skillID = 1;
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssetIfDirty(config);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string backupPath = string.Empty;
            string recoveryPath = string.Empty;
            try
            {
                config.skillID = 23; // Intentionally not saved before migration.
                EditorUtility.SetDirty(config);
                Assert.That(ActionConfigMigrationService.MigrateWithBackup(config, out backupPath, out var error), Is.True, error);
                var backup = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(backupPath);
                Assert.That(backup.skillID, Is.EqualTo(23));
                Assert.That(backup.ActionEditorSchemaVersion, Is.Zero);
                config.skillID = 99; // Restoration also backs up unsaved changes.
                EditorUtility.SetDirty(config);
                Undo.IncrementCurrentGroup();
                Assert.That(ActionConfigMigrationService.RestoreWithBackup(config, backup, out recoveryPath, out error), Is.True, error);
                Assert.That(config.skillID, Is.EqualTo(23));
                Assert.That(config.ActionEditorSchemaVersion, Is.Zero);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(recoveryPath).skillID, Is.EqualTo(99));
                Undo.PerformUndo();
                Assert.That(config.skillID, Is.EqualTo(99));
            }
            finally
            {
                Undo.ClearUndo(config);
                if (!string.IsNullOrEmpty(recoveryPath)) AssetDatabase.DeleteAsset(recoveryPath);
                if (!string.IsNullOrEmpty(backupPath)) AssetDatabase.DeleteAsset(backupPath);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
