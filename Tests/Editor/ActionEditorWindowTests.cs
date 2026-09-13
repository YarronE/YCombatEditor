using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Ethan.ActionEditor.Editor.Tests
{
    /// <summary>Exercises the actual window interaction methods, not a parallel test-only editing implementation.</summary>
    public sealed class ActionEditorWindowTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        SkillEditorWindow window;
        SkillConfigSO config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SkillConfigSO>();
            config.SetActionEditorSchemaVersion(ActionConfigMigrationService.CurrentSchemaVersion);
            window = ScriptableObject.CreateInstance<SkillEditorWindow>();
            window.configFile = config;
        }

        [TestCase(ActionFlowKind.AllowExit)]
        [TestCase(ActionFlowKind.Branch)]
        [TestCase(ActionFlowKind.Complete)]
        public void FlowCreationCopyAndUndoPreserveTheNodeKind(ActionFlowKind kind)
        {
            Invoke("LoadConfig"); Set("frameSelectIndex",12);
            Invoke("AddFlowNode",kind); Invoke("CommitDeferredChanges");
            Assert.That(config.flowNodes.Count,Is.EqualTo(1));
            Assert.That(config.flowNodes[0].limitWindow,Is.False);
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            Invoke("CopySelected"); Set("frameSelectIndex",24); Invoke("PasteClipboard"); Invoke("CommitDeferredChanges");
            Assert.That(config.flowNodes.Count,Is.EqualTo(2));
            Assert.That(config.flowNodes[1].kind,Is.EqualTo(kind));
            Assert.That(config.flowNodes[1].keyNumber,Is.EqualTo(24));
            Invoke("UndoLatest"); Assert.That(config.flowNodes.Count,Is.EqualTo(1));
        }

        [TearDown]
        public void TearDown()
        {
            Event.current = null;
            Object.DestroyImmediate(window);
            Undo.ClearUndo(config);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void BoneBindingUsesActorRelativePathRejectsOutsideBonesAndSupportsUndo()
        {
            var sceneRoot = new GameObject("Scene Group");
            var actor = new GameObject("Actor");
            var bone = new GameObject("Hand");
            var unrelated = new GameObject("Other Actor Bone");
            try
            {
                actor.transform.SetParent(sceneRoot.transform);
                bone.transform.SetParent(actor.transform);
                config.attackList.Add(new Global.Attack { trackBonePath = "Original" });
                Invoke("LoadConfig");
                window.previewModel = actor;
                Invoke("BindCollisionBone", config.attackList[0], unrelated.transform);
                Assert.That(config.attackList[0].trackBonePath, Is.EqualTo("Original"));
                Invoke("BindCollisionBone", config.attackList[0], bone.transform);
                Assert.That(config.attackList[0].trackBonePath, Is.EqualTo("Hand"));
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That(config.attackList[0].trackBonePath, Is.EqualTo("Original"));
            }
            finally
            {
                window.previewModel = null;
                Object.DestroyImmediate(sceneRoot);
                Object.DestroyImmediate(unrelated);
            }
        }

        [TestCase("Attack", "attackList", "atkList", "keyNumber", "endKeyNumber")]
        [TestCase("Fx", "fxList", "fxAndSoundList", "keyNumber", "endKeyNumber")]
        [TestCase("Sound", "fxList", "fxAndSoundList", "keyNumber", "endKeyNumber")]
        [TestCase("Jump", "jumpList", "jumpList", "beginKey", "endKey")]
        [TestCase("Warning", "warningCueList", "warningCueList", "keyNumber", "endKeyNumber")]
        [TestCase("Cancel", "cancelList", "cancelList", "keyNumber", "endKeyNumber")]
        [TestCase("Projectile", "projectileList", "projectileList", "keyNumber", "endKeyNumber")]
        [TestCase("SuperArmor", "superArmorList", "superArmorList", "keyNumber", "endKeyNumber")]
        [TestCase("AdjustMotion", "adjustMotionList", "adjustMotionList", "keyNumber", "endKeyNumber")]
        [TestCase("TrailToggle", "trailToggleList", "trailToggleList", "keyNumber", "endKeyNumber")]
        [TestCase("Move", "moveSegmentList", "moveSegmentList", "keyNumber", "endKeyNumber")]
        [TestCase("Interaction", "interactionWindows", "interactionWindows", "keyNumber", "endKeyNumber")]
        public void Drag_UndoRedoRestoresEventCacheSelectionAndPlayhead(string selection, string listName, string cacheName, string startName, string endName)
        {
            var field = typeof(SkillConfigSO).GetField(listName);
            var itemType = field.FieldType.GetGenericArguments()[0];
            var item = Activator.CreateInstance(itemType);
            itemType.GetField(startName).SetValue(item, 2);
            itemType.GetField(endName).SetValue(item, 5);
            ((IList)field.GetValue(config)).Add(item);
            Invoke("LoadConfig");
            var selectionType = typeof(SkillEditorWindow).GetNestedType("SelType", BindingFlags.NonPublic);
            var selected = Enum.Parse(selectionType, selection);
            Set("selType", selected);
            Set("selIdx", 0);
            Set("frameSelectIndex", 7);
            Invoke("StartDrag", selected, 0, 2, 5);
            for (int frame = 3; frame <= 6; frame++)
            {
                var input = new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(frame * 12f, 40f) };
                Invoke("HandleTLInput", new Rect(0, 0, 1000, 200), 60, input);
                Undo.FlushUndoRecordObjects();
            }
            Invoke("HandleTLInput", new Rect(0, 0, 1000, 200), 60, new Event { type = EventType.MouseUp, button = 0 });
            Assert.That(itemType.GetField(startName).GetValue(((IList)field.GetValue(config))[0]), Is.EqualTo(6));

            Invoke("UndoLatest");
            var restored = ((IList)field.GetValue(config))[0];
            Assert.That(itemType.GetField(startName).GetValue(restored), Is.EqualTo(2));
            Assert.That(itemType.GetField(endName).GetValue(restored), Is.EqualTo(5));
            Assert.That(((IList)Get(cacheName))[0], Is.SameAs(restored));
            Assert.That(Get("selType"), Is.EqualTo(selected));
            Assert.That(Get("selIdx"), Is.EqualTo(0));
            Assert.That(Get("frameSelectIndex"), Is.EqualTo(7));
            Invoke("RedoLatest");
            Assert.That(itemType.GetField(startName).GetValue(((IList)field.GetValue(config))[0]), Is.EqualTo(6));
        }

        [Test]
        public void MoveClipboardPreservesCurveAndUsesNativeUndo()
        {
            config.moveSegmentList.Add(new Global.MoveSegment { keyNumber = 2, endKeyNumber = 5, maxDistance = 3f });
            Invoke("LoadConfig");
            var selectionType = typeof(SkillEditorWindow).GetNestedType("SelType", BindingFlags.NonPublic);
            Set("selType", Enum.Parse(selectionType, "Move"));
            Set("selIdx", 0);
            Invoke("CopySelected");
            Set("frameSelectIndex", 12);
            Invoke("PasteClipboard");
            Invoke("CommitDeferredChanges");
            Assert.That(config.moveSegmentList.Count, Is.EqualTo(2));
            Assert.That(config.moveSegmentList[1].keyNumber, Is.EqualTo(12));
            Assert.That(config.moveSegmentList[1].endKeyNumber, Is.EqualTo(15));
            Assert.That(config.moveSegmentList[1].maxDistance, Is.EqualTo(3f));
            Assert.That(config.moveSegmentList[1].curve.keys, Is.EqualTo(config.moveSegmentList[0].curve.keys));
            Invoke("UndoLatest");
            Assert.That(config.moveSegmentList.Count, Is.EqualTo(1));
        }

        [Test]
        public void TelegraphAndEffectTracksShareHeaderAndAvoidOverlappingLanes()
        {
            config.tracks.Add(new Global.SkillTrack{type=Global.TrackType.Fx,expanded=true});
            config.tracks.Add(new Global.SkillTrack{type=Global.TrackType.Warning,expanded=true});
            config.tracks.Add(new Global.SkillTrack{type=Global.TrackType.Sound,expanded=true});
            config.fxList.Add(new Global.FxAndSound{keyNumber=0,endKeyNumber=10});
            config.warningCueList.Add(new Global.WarningCue{keyNumber=5,endKeyNumber=8});
            Invoke("LoadConfig");
            var tracks=(IList)typeof(SkillEditorWindow).GetMethod("BuildTracks",Private).Invoke(window,null);
            Assert.That(tracks.Count,Is.EqualTo(1));
            Invoke("UnifiedEffectHeight");
            var visual=(System.Collections.Generic.Dictionary<int,int>)Get("effectVisualLanes");
            var telegraph=(System.Collections.Generic.Dictionary<int,int>)Get("effectTelegraphLanes");
            Assert.That(visual[0],Is.Not.EqualTo(telegraph[0]));
            Assert.That(config.tracks.Count,Is.EqualTo(3),"Grouping must not rewrite serialized legacy track IDs.");
            config.tracks.RemoveAll(t=>t.type!=Global.TrackType.Warning);
            tracks=(IList)typeof(SkillEditorWindow).GetMethod("BuildTracks",Private).Invoke(window,null);
            Assert.That(tracks.Count,Is.EqualTo(1),"A legacy Telegraph-only layout still exposes Effect.");
        }

        [Test]
        public void TelegraphClipboardPastesOnEffectWithReferencesAndUndo()
        {
            var sound=AudioClip.Create("Telegraph test",16,1,8000,false);
            try {
                Invoke("LoadConfig"); Set("frameSelectIndex",4); Invoke("AddWarnHere");
                config.warningCueList[0].warningSound=sound;
                Invoke("CommitDeferredChanges"); Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
                Invoke("CopySelected");
                var canPaste=typeof(SkillEditorWindow).GetMethod("CanPasteToTrack",Private);
                Assert.That(canPaste.Invoke(window,new object[]{Global.TrackType.Fx}),Is.EqualTo(true));
                Assert.That(canPaste.Invoke(window,new object[]{Global.TrackType.Attack}),Is.EqualTo(false));
                Set("frameSelectIndex",12); Invoke("PasteClipboard"); Invoke("CommitDeferredChanges");
                Assert.That(config.warningCueList[1].keyNumber,Is.EqualTo(12));
                Assert.That(config.warningCueList[1].warningSound,Is.SameAs(sound));
                Invoke("UndoLatest"); Assert.That(config.warningCueList.Count,Is.EqualTo(1));
            } finally { Object.DestroyImmediate(sound); }
        }

        [TestCase("AddFxHere", Global.EffectContentKind.VisualEffect)]
        [TestCase("AddSndHere", Global.EffectContentKind.Audio)]
        public void EffectAddRetainsSubtypeThroughPasteUndoAndReload(string method, Global.EffectContentKind kind)
        {
            Invoke("LoadConfig");
            Set("frameSelectIndex", 4);
            Invoke(method);
            Invoke("CommitDeferredChanges");
            Assert.That(config.fxList[0].contentKind, Is.EqualTo(kind));
            Assert.That(config.fxList[0].keyNumber, Is.EqualTo(4));
            // Native UI events advance Undo groups; direct reflection calls run in one event.
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Invoke("CopySelected");
            Set("frameSelectIndex", 12);
            Invoke("PasteClipboard");
            Invoke("CommitDeferredChanges");
            Assert.That(config.fxList[1].contentKind, Is.EqualTo(kind));
            Assert.That(config.fxList[1].keyNumber, Is.EqualTo(12));
            Undo.FlushUndoRecordObjects();
            Invoke("UndoLatest");
            Assert.That(config.fxList.Count, Is.EqualTo(1));
            Invoke("RedoLatest");
            Assert.That(config.fxList[1].contentKind, Is.EqualTo(kind));
            const string path = "Assets/__EffectSubtypeRoundTrip.asset";
            try
            {
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssetIfDirty(config);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
                Assert.That(saved.fxList[1].contentKind, Is.EqualTo(kind));
                Invoke("LoadConfig");
                Assert.That(EffectAuthoring.Resolve(config.fxList[1]), Is.EqualTo(kind), "An empty reference must not reset the selected subtype.");
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EffectInspectorNeverExposesOtherSubtypeFields(bool advanced)
        {
            foreach (string field in new[] { "audioClip", "baseVolume", "audioRandomize", "pitchVariation", "volumeVariation" })
                Assert.That(EffectAuthoring.IsFieldVisible(Global.EffectContentKind.VisualEffect, field, advanced), Is.False, field);
            foreach (string field in new[] { "particleSystem", "offset", "rotation", "scale", "followCharacter", "customLifetime", "playbackSpeed", "useWorldSpace" })
                Assert.That(EffectAuthoring.IsFieldVisible(Global.EffectContentKind.Audio, field, advanced), Is.False, field);
            Assert.That(EffectAuthoring.IsFieldVisible(Global.EffectContentKind.Audio, "audioClip", advanced), Is.True);
            Assert.That(EffectAuthoring.IsFieldVisible(Global.EffectContentKind.VisualEffect, "particleSystem", advanced), Is.True);
        }

        [Test]
        public void LegacyMixedEffectViewsPreserveSerializedReferences()
        {
            var particle = new GameObject("Legacy particle").AddComponent<ParticleSystem>();
            var audio = AudioClip.Create("Legacy audio", 32, 1, 44100, false);
            try
            {
                config.fxList.Add(new Global.FxAndSound { particleSystem = particle, audioClip = audio });
                Invoke("LoadConfig");
                string before = EditorJsonUtility.ToJson(config);
                var views = (System.Collections.Generic.Dictionary<string, Global.EffectContentKind>)Get("legacyEffectViews");
                foreach (var kind in new[] { Global.EffectContentKind.Audio, Global.EffectContentKind.VisualEffect })
                {
                    views[config.GetInstanceID() + ":fxList:0"] = kind;
                    Assert.That(typeof(SkillEditorWindow).GetMethod("EffectView", Private).Invoke(window, new object[] { "fxList", 0 }), Is.EqualTo(kind));
                }
                Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(before));
                Assert.That(config.fxList[0].contentKind, Is.EqualTo(Global.EffectContentKind.Legacy));
            }
            finally { Object.DestroyImmediate(particle.gameObject); Object.DestroyImmediate(audio); }
        }

        [Test]
        public void LegacySoundClipboardCanPasteOnUnifiedEffectTrack()
        {
            config.fxList.Add(new Global.FxAndSound { keyNumber = 3 });
            Invoke("LoadConfig");
            var selectionType = typeof(SkillEditorWindow).GetNestedType("SelType", BindingFlags.NonPublic);
            Set("selType", Enum.Parse(selectionType, "Sound"));
            Set("selIdx", 0);
            Invoke("CopySelected");
            var canPaste = typeof(SkillEditorWindow).GetMethod("CanPasteToTrack", Private);
            Assert.That(canPaste.Invoke(window, new object[] { Global.TrackType.Fx }), Is.EqualTo(true));
            Assert.That(canPaste.Invoke(window, new object[] { Global.TrackType.Attack }), Is.EqualTo(false));
            Set("frameSelectIndex", 10);
            Invoke("PasteClipboard");
            Invoke("CommitDeferredChanges");
            Assert.That(config.fxList[1].contentKind, Is.EqualTo(Global.EffectContentKind.Legacy));
            Assert.That(config.fxList[1].keyNumber, Is.EqualTo(10));
        }

        [Test]
        public void LoadAndSaveUnchangedLegacyAssetDoesNotRewriteIt()
        {
            const string path = "Assets/__ActionEditorWindowUnchanged.asset";
            config.SetActionEditorSchemaVersion(0);
            config.tracks.Clear();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssetIfDirty(config);
            try
            {
                var before = File.ReadAllBytes(path);
                Invoke("LoadConfig");
                Invoke("SaveConfig");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                Assert.That(config.tracks.Count, Is.Zero);
                Assert.That(EditorUtility.IsDirty(config), Is.False);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void InspectorApplyPreservesInvalidValuesAndRebindsCacheWithUndo()
        {
            config.attackList.Add(new Global.Attack { keyNumber = 2 });
            Invoke("LoadConfig");
            var serialized = (SerializedObject)Get("_serializedConfig");
            serialized.FindProperty("skillName").stringValue = "Edited";
            serialized.FindProperty("attackList").GetArrayElementAtIndex(0).FindPropertyRelative("keyNumber").intValue = -4;
            Invoke("ApplyInspectorProperties");
            Assert.That(config.attackList[0].keyNumber, Is.EqualTo(-4), "Validation must not silently repair designer data.");
            Assert.That(window.skillName, Is.EqualTo("Edited"));
            Assert.That(((IList)Get("atkList"))[0], Is.SameAs(config.attackList[0]));
            Invoke("UndoLatest");
            Assert.That(config.attackList[0].keyNumber, Is.EqualTo(2));
            Invoke("RedoLatest");
            Assert.That(config.attackList[0].keyNumber, Is.EqualTo(-4));
        }

        [Test]
        public void LegacyInspectorApplyDiscardsEdits()
        {
            config.SetActionEditorSchemaVersion(0);
            config.skillID = 42;
            Invoke("LoadConfig");
            ((SerializedObject)Get("_serializedConfig")).FindProperty("skillID").intValue = 999;
            Invoke("ApplyInspectorProperties");
            Assert.That(config.skillID, Is.EqualTo(42));
        }

        [Test]
        public void PhaseTwoValidationLocatesItsOwnEventAndFrame()
        {
            config.attackList.Add(new Global.Attack { keyNumber = 1 });
            config.phase2AttackList.Add(new Global.Attack { keyNumber = 90 });
            Invoke("LoadConfig");
            Invoke("LocateValidationIssue", new ActionValidationIssue("ACT_TEST", ActionValidationSeverity.Error, "Phase2Attack", 0, "test"));
            Assert.That(Get("selType").ToString(), Is.EqualTo("Phase2Attack"));
            Assert.That(Get("frameSelectIndex"), Is.EqualTo(90));
            Assert.That(((Vector2)Get("timelineScrollPos")).x, Is.GreaterThan(0));
        }

        [Test]
        public void EmptyEffectIsVisibleBeforeAssigningItsReference()
        {
            var effect = new Global.FxAndSound();
            Assert.That(BuiltinTrackRegistry.IsVisibleInFxTrack(effect), Is.True);
            Assert.That(BuiltinTrackRegistry.IsVisibleInSoundTrack(effect), Is.True);
        }

        [Test]
        public void InteractionClipboardKeepsConditionReferencesAndCreatesUniqueIdentity()
        {
            var condition = ScriptableObject.CreateInstance<InteractionAttackCondition>();
            try
            {
                config.interactionWindows.Add(new InteractionWindow { id = "parry", keyNumber = 2, endKeyNumber = 5 });
                config.interactionWindows[0].conditions.Add(condition);
                Invoke("LoadConfig");
                Set("selType", Enum.Parse(typeof(SkillEditorWindow).GetNestedType("SelType", BindingFlags.NonPublic), "Interaction"));
                Set("selIdx", 0); Set("frameSelectIndex", 10);
                Invoke("CopySelected"); Invoke("PasteClipboard");
                Assert.AreEqual(2, config.interactionWindows.Count);
                Assert.AreNotEqual("parry", config.interactionWindows[1].id);
                Assert.AreEqual(10, config.interactionWindows[1].keyNumber);
                Assert.AreEqual(13, config.interactionWindows[1].endKeyNumber);
                Assert.AreSame(condition, config.interactionWindows[1].conditions[0]);
                Invoke("CommitDeferredChanges"); Undo.FlushUndoRecordObjects();
                Invoke("UndoLatest");
                Assert.AreEqual(1, config.interactionWindows.Count);
                Invoke("RedoLatest");
                Assert.AreEqual(2, config.interactionWindows.Count);
            }
            finally { Object.DestroyImmediate(condition); }
        }

        [Test]
        public void InteractionPresetAndValidationUseTheActualWindow()
        {
            config.interactionWindows.Add(new InteractionWindow { keyNumber = 12, endKeyNumber = 18 });
            Invoke("LoadConfig");
            Invoke("ApplyInteractionPreset", 0, InteractionResponse.Parry);
            Assert.AreEqual(InteractionResponse.Parry, config.interactionWindows[0].response);
            Assert.AreEqual(120, config.interactionWindows[0].sourceAngle);
            Invoke("LocateValidationIssue", new ActionValidationIssue("ACT140", ActionValidationSeverity.Error, "Interaction", 0, "test"));
            Assert.AreEqual("Interaction", Get("selType").ToString());
            Assert.AreEqual(12, Get("frameSelectIndex"));
        }

        [Test]
        public void InteractionSaveReopenPreservesSettingsAndConditionAsset()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/__InteractionSave.asset");
            string conditionPath = AssetDatabase.GenerateUniqueAssetPath("Assets/__InteractionCondition.asset");
            var condition = ScriptableObject.CreateInstance<InteractionAttackCondition>();
            condition.requiredTag = "heavy";
            AssetDatabase.CreateAsset(condition, conditionPath);
            AssetDatabase.CreateAsset(config, path);
            try
            {
                Invoke("LoadConfig");
                Invoke("AddInteractionHere");
                config.interactionWindows[0].conditions.Add(condition);
                Invoke("ApplyInteractionPreset", 0, InteractionResponse.Parry);
                Invoke("SaveConfig");
                Object.DestroyImmediate(window);
                window = ScriptableObject.CreateInstance<SkillEditorWindow>();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                window.configFile = config = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(path);
                Invoke("LoadConfig");
                Assert.AreEqual(1, config.interactionWindows.Count);
                Assert.AreEqual(InteractionResponse.Parry, config.interactionWindows[0].response);
                Assert.AreEqual(120, config.interactionWindows[0].sourceAngle);
                Assert.AreEqual(conditionPath, AssetDatabase.GetAssetPath(config.interactionWindows[0].conditions[0]));
                Assert.AreSame(config.interactionWindows[0], ((IList)Get("interactionWindows"))[0]);
            }
            finally { AssetDatabase.DeleteAsset(path); AssetDatabase.DeleteAsset(conditionPath); }
        }

        [Test]
        public void JumpClipboardPreservesSemanticCommandAndLegacyKey()
        {
            config.jumpList.Add(new Global.Jump { beginKey = 2, endKey = 5, triggerCommandId = "Attack", triggerKey = KeyCode.Mouse0 });
            Invoke("LoadConfig");
            Set("selType", Enum.Parse(typeof(SkillEditorWindow).GetNestedType("SelType", BindingFlags.NonPublic), "Jump"));
            Set("selIdx", 0); Set("frameSelectIndex", 10);
            Invoke("CopySelected"); Invoke("PasteClipboard"); Invoke("CommitDeferredChanges");
            Assert.AreEqual("Attack", config.jumpList[1].triggerCommandId);
            Assert.AreEqual(KeyCode.Mouse0, config.jumpList[1].triggerKey);
            Assert.AreEqual(10, config.jumpList[1].beginKey);
            Undo.FlushUndoRecordObjects(); Invoke("UndoLatest");
            Assert.AreEqual(1, config.jumpList.Count);
            Assert.AreEqual("Attack", config.jumpList[0].triggerCommandId);
        }

        [Test]
        public void MixedFpsTrimConvertsTimelineFramesToSourceFramesAndUndoes()
        {
            var clip=new AnimationClip { frameRate=60 };
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,0,1,1));
            try {
                config.InitializeExplicitTiming(); config.animSegments.Add(new Global.AnimClipSegment { clip=clip }); Invoke("LoadConfig");
                Set("isDraggingSegEdge",true); Set("segEdgeIdx",0); Set("segEdgeSide",1);
                Invoke("HandleTLInput",new Rect(0,0,1000,200),60,new Event { type=EventType.MouseDrag,button=0,mousePosition=new Vector2(15*12,40) });
                Undo.FlushUndoRecordObjects();
                Invoke("HandleTLInput",new Rect(0,0,1000,200),60,new Event { type=EventType.MouseUp,button=0 });
                Assert.AreEqual(30,config.animSegments[0].clipEndFrame);
                Invoke("UndoLatest"); Assert.AreEqual(0,config.animSegments[0].clipEndFrame);
                Invoke("RedoLatest"); Assert.AreEqual(30,config.animSegments[0].clipEndFrame);
            } finally { Object.DestroyImmediate(clip); }
        }
        [Test]
        public void ExplicitPreviewUsesIsolatedCloneAndReleasesItOnStop()
        {
            var clip=new AnimationClip { frameRate=60 }; var model=new GameObject("PreviewSource"); model.AddComponent<Animator>();
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.y"),AnimationCurve.Linear(0,0,1,1));
            try {
                config.InitializeExplicitTiming(); config.animSegments.Add(new Global.AnimClipSegment { clip=clip }); Invoke("LoadConfig");
                window.previewModel=model; Set("frameSelectIndex",15); Invoke("SampleAnim");
                Assert.That(model.transform.position.y,Is.EqualTo(0));
                var preview=(GameObject)Get("_previewInstance"); Assert.IsNotNull(preview);
                Assert.That(preview.transform.position.y,Is.EqualTo(.5f).Within(.03f));
                Set("playFrame",true); Invoke("HandleGlobalInput",new Event { type=EventType.KeyDown,keyCode=KeyCode.Space }); Assert.IsTrue(preview==null);
                Assert.IsNull(model.GetComponent<ActionClipAnimator>());
            } finally { Object.DestroyImmediate(model); Object.DestroyImmediate(clip); }
        }

        [Test] public void TextFocusDoesNotTogglePlayback()
        {
            EditorGUIUtility.editingTextField=true;
            try { Invoke("HandleGlobalInput",new Event { type=EventType.KeyDown,keyCode=KeyCode.Space }); Assert.IsFalse((bool)Get("playFrame")); }
            finally { EditorGUIUtility.editingTextField=false; }
        }

        [TestCase(900,600)]
        [TestCase(1360,680)]
        [TestCase(2100,900)]
        public void ReadOnlyAndEmptyContentStayBelowAllToolbarRows(float width,float height)
        {
            window.position=new Rect(0,0,width,height);
            var rect=(Rect)typeof(SkillEditorWindow).GetMethod("GetContentRect",Private).Invoke(window,null);
            Assert.That(rect.yMin,Is.EqualTo(82));
            Assert.That(rect.yMax,Is.EqualTo(window.position.height));
            Assert.That(rect.width,Is.EqualTo(window.position.width));
        }

        [UnityTest]
        public IEnumerator SwitchingAssetModesRepaintsWithoutLayoutErrors()
        {
            var legacy=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {
                window.position=new Rect(0,0,1360,680);window.Show();
                foreach(var selected in new[]{config,legacy,null,legacy,config}) {
                    Invoke("SelectConfig",new object[]{selected});
                    window.Repaint();yield return null;yield return null;
                    LogAssert.NoUnexpectedReceived();
                }
            } finally {window.Close();Object.DestroyImmediate(legacy);}
        }

        [Test]
        public void SelectionBetweenEditableLegacyAndEmptyResetsViewWithoutMutatingAssets()
        {
            var legacy=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {
                string before=EditorJsonUtility.ToJson(legacy);
                Invoke("LoadConfig");
                Set("inspScrollPos",new Vector2(0,700));Set("isDraggingItem",true);
                Invoke("SelectConfig",legacy);
                Assert.That(window.configFile,Is.SameAs(legacy));
                Assert.That(((SerializedObject)Get("_serializedConfig")).targetObject,Is.SameAs(legacy));
                Assert.That(Get("inspScrollPos"),Is.EqualTo(Vector2.zero));
                Assert.That(Get("isDraggingItem"),Is.False);
                Assert.That(EditorJsonUtility.ToJson(legacy),Is.EqualTo(before));
                Invoke("SelectConfig",new object[]{null});
                Assert.That(window.configFile,Is.Null);Assert.That(Get("_serializedConfig"),Is.Null);
                Invoke("SelectConfig",config);
                Assert.That(((SerializedObject)Get("_serializedConfig")).targetObject,Is.SameAs(config));
                Assert.That(EditorJsonUtility.ToJson(legacy),Is.EqualTo(before));
            } finally {Object.DestroyImmediate(legacy);}
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CollisionCreationAndSceneGeometryEditsRetainResponseThroughUndo(int shape)
        {
            Invoke("LoadConfig"); Set("frameSelectIndex", 8); Invoke("AddCollision", shape); Invoke("CommitDeferredChanges");
            var c = config.attackList[0];
            Assert.That(c.shapeType, Is.EqualTo(shape)); Assert.That(c.endKeyNumber, Is.EqualTo(13));
            c.damageRatio = 3f;
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            Invoke("ApplyCollisionGeometryEdit", c, new Vector3(2,3,4), new Vector3(3,4,5), new Vector3(0,45,0));
            Assert.That(c.offset, Is.EqualTo(new Vector3(2,3,4)));
            Assert.That(c.damageRatio, Is.EqualTo(3f));
            if (shape == 1) { Assert.That(c.boxHeight, Is.EqualTo(4)); Assert.That(c.collisionRotation.y, Is.EqualTo(45)); }
            Invoke("UndoLatest");
            Assert.That(config.attackList[0].offset, Is.EqualTo(new Vector3(0,1,1)));
            Invoke("RedoLatest");
            Assert.That(config.attackList[0].offset, Is.EqualTo(new Vector3(2,3,4)));
            Invoke("CopySelected"); Set("frameSelectIndex", 20); Invoke("PasteClipboard"); Invoke("CommitDeferredChanges");
            Assert.That(config.attackList[1].damageRatio, Is.EqualTo(3f));
            Assert.That(config.attackList[1].boxHeight, Is.EqualTo(config.attackList[0].boxHeight));
            Assert.That(config.attackList[1].collisionRotation, Is.EqualTo(config.attackList[0].collisionRotation));
        }

        [Test]
        public void CollisionBoneHandleEditsOnlyCurrentSample()
        {
            config.attackList.Add(new Global.Attack { keyNumber = 2, endKeyNumber = 4, useBoneTracking = true,
                boneOffsets = new System.Collections.Generic.List<Vector3> { Vector3.zero, Vector3.one, Vector3.right } });
            Invoke("LoadConfig"); Set("frameSelectIndex", 3);
            var c = config.attackList[0];
            Invoke("ApplyCollisionGeometryEdit", c, Vector3.up * 3, Vector3.one, Vector3.zero);
            Assert.That(c.boneOffsets[0], Is.EqualTo(Vector3.zero));
            Assert.That(c.boneOffsets[1], Is.EqualTo(Vector3.up * 3));
            Assert.That(c.boneOffsets[2], Is.EqualTo(Vector3.right));
            Assert.That(c.offset, Is.EqualTo(Vector3.zero));
            Invoke("UndoLatest");
            Assert.That(config.attackList[0].boneOffsets[1], Is.EqualTo(Vector3.one));
        }

        [Test]
        public void SelectedCollisionRemainsAvailableOutsideItsWindowAndWithUnrelatedErrors()
        {
            Invoke("LoadConfig"); Invoke("AddCollision", 1); Invoke("CommitDeferredChanges");
            config.fxList.Add(new Global.FxAndSound()); Invoke("RefreshValidation");
            Set("frameSelectIndex", 100);
            var selected = typeof(SkillEditorWindow).GetProperty("SelectedCollision", Private).GetValue(window);
            Assert.That(selected, Is.SameAs(config.attackList[0]));
            Assert.That((int)Get("_validationErrorCount"), Is.GreaterThan(0));
        }

        void Invoke(string method, params object[] arguments) => typeof(SkillEditorWindow).GetMethod(method, Private).Invoke(window, arguments);
        void Set(string field, object value) => typeof(SkillEditorWindow).GetField(field, Private).SetValue(window, value);
        object Get(string field) => typeof(SkillEditorWindow).GetField(field, Private).GetValue(window);
    }
}
