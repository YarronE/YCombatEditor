using System;
using Ethan.ActionEditor.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ethan.ActionEditor.Editor.Tests
{
    public sealed class ActionSerializedEditingTests
    {
        const string TestFolder = "Assets/__ActionEditorSerializedTests";

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [TestCase("animSegments")]
        [TestCase("moveSegmentList")]
        [TestCase("tracks")]
        [TestCase("jumpList")]
        [TestCase("attackList")]
        [TestCase("fxList")]
        [TestCase("cancelList")]
        [TestCase("projectileList")]
        [TestCase("hitFxList")]
        [TestCase("warningCueList")]
        [TestCase("superArmorList")]
        [TestCase("adjustMotionList")]
        [TestCase("trailToggleList")]
        [TestCase("phase2AttackList")]
        [TestCase("phase2FxList")]
        public void BuiltInList_SupportsEditUndoRedoAndReload(string propertyName)
        {
            ActionConfigMigrationService.EnsureAssetDirectory(TestFolder);
            string path = $"{TestFolder}/{propertyName}-{Guid.NewGuid():N}.asset";
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            AssetDatabase.CreateAsset(config, path);

            try
            {
                var serialized = new SerializedObject(config);
                var list = serialized.FindProperty(propertyName);
                Assert.That(list, Is.Not.Null, propertyName);
                Assert.That(list.isArray, Is.True, propertyName);

                list.InsertArrayElementAtIndex(0);
                serialized.ApplyModifiedProperties();
                Assert.That(list.arraySize, Is.EqualTo(1));

                Undo.PerformUndo();
                serialized.Update();
                Assert.That(list.arraySize, Is.EqualTo(0), "Undo must restore the serialized list.");

                Undo.PerformRedo();
                serialized.Update();
                Assert.That(list.arraySize, Is.EqualTo(1), "Redo must restore the edit.");

                Undo.IncrementCurrentGroup();
                list.InsertArrayElementAtIndex(0); // Unity duplicates an existing element.
                list.MoveArrayElement(0, 1);
                serialized.ApplyModifiedProperties();
                Assert.That(list.arraySize, Is.EqualTo(2));

                Undo.IncrementCurrentGroup();
                list.DeleteArrayElementAtIndex(0);
                serialized.ApplyModifiedProperties();
                Assert.That(list.arraySize, Is.EqualTo(1));

                Undo.PerformUndo();
                serialized.Update();
                Assert.That(list.arraySize, Is.EqualTo(2), "Delete must be independently undoable.");
                Undo.PerformRedo();
                serialized.Update();
                Assert.That(list.arraySize, Is.EqualTo(1));

                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
                var reloadedList = new SerializedObject(reloaded).FindProperty(propertyName);
                Assert.That(reloadedList.arraySize, Is.EqualTo(1), "Saved data must survive a reload.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
