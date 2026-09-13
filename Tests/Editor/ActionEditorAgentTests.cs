using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Ethan.ActionEditor.Editor;
using Object = UnityEngine.Object;

public class ActionEditorAgentTests
{
    string folder;
    string clipPath;
    string actionPath;

    [SetUp]
    public void SetUp()
    {
        folder = "Assets/AgentTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        clipPath = folder + "/Motion.anim";
        actionPath = folder + "/Action.asset";
        var clip = new AnimationClip { name = "Motion", frameRate = 30 };
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), AnimationCurve.Linear(0, 0, 1, 1));
        AssetDatabase.CreateAsset(clip, clipPath);
    }

    [TearDown]
    public void TearDown() => AssetDatabase.DeleteAsset(folder);

    ActionEditorAgent.Request CreateRequest() => new ActionEditorAgent.Request {
        operation = "create_action", outputAssetPath = actionPath,
        animationAssetPath = clipPath, displayName = "Agent Action"
    };

    [Test]
    public void CreatesRealAsset_AndInventoryAndValidationReadItWithoutChangingBytes()
    {
        var created = ActionEditorAgent.Execute(CreateRequest());
        Assert.IsTrue(created.success, JsonUtility.ToJson(created));
        var action = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath);
        Assert.AreEqual(30, action.exitFrame);
        Assert.AreEqual(1, action.ActionEditorSchemaVersion);
        Assert.AreEqual("Agent Action", action.skillName);
        Assert.AreEqual(clipPath, AssetDatabase.GetAssetPath(action.animSegments[0].clip));
        var before = File.ReadAllBytes(actionPath);
        var inventory = ActionEditorAgent.Execute(new ActionEditorAgent.Request { operation = "inventory" });
        CollectionAssert.Contains(inventory.actionAssets, actionPath);
        CollectionAssert.Contains(inventory.animationAssets, clipPath);
        var validated = ActionEditorAgent.Execute(new ActionEditorAgent.Request { operation = "validate", assetPaths = new[] { actionPath } });
        Assert.IsTrue(validated.success);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(actionPath));
        var roundtrip = JsonUtility.FromJson<ActionEditorAgent.Result>(JsonUtility.ToJson(validated));
        Assert.IsTrue(roundtrip.success);
        Assert.AreEqual(1, roundtrip.schemaVersion);
    }

    [Test]
    public void RefusesOverwriteAndLeavesOriginalBytesAndGuidUnchanged()
    {
        Assert.IsTrue(ActionEditorAgent.Execute(CreateRequest()).success);
        string guid = AssetDatabase.AssetPathToGUID(actionPath);
        var bytes = File.ReadAllBytes(actionPath);
        var request = CreateRequest(); request.displayName = "Replacement";
        Assert.IsFalse(ActionEditorAgent.Execute(request).success);
        Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(actionPath));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(actionPath));
    }

    [TestCase("Packages/Outside.asset")]
    [TestCase("Assets/../Outside.asset")]
    [TestCase("Assets/Test.txt")]
    public void RefusesInvalidDestinations(string destination)
    {
        var request = CreateRequest(); request.outputAssetPath = destination;
        Assert.IsFalse(ActionEditorAgent.Execute(request).success);
        Assert.IsNull(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath));
    }

    [Test]
    public void ValidationRequiresAssetsAndReportsMissingPaths()
    {
        Assert.IsFalse(ActionEditorAgent.Execute(new ActionEditorAgent.Request { operation = "validate" }).success);
        var result = ActionEditorAgent.Execute(new ActionEditorAgent.Request { operation = "validate", assetPaths = new[] { actionPath } });
        Assert.IsFalse(result.success);
        Assert.AreEqual("AGENT_ASSET_MISSING", result.issues[0].code);
    }

    [Test]
    public void InvalidClipDoesNotCreateAction()
    {
        var request = CreateRequest(); request.animationAssetPath = folder + "/Missing.anim";
        Assert.IsFalse(ActionEditorAgent.Execute(request).success);
        Assert.IsFalse(File.Exists(actionPath));
    }

    [Test]
    public void ActorPreflightReportsUnconnectedAttackAndDoesNotStartPlayback()
    {
        Assert.IsTrue(ActionEditorAgent.Execute(CreateRequest()).success);
        var action = AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath);
        var actor = new GameObject("Agent Audit") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            actor.AddComponent<Ethan.ActionEditor.ActionClipAnimator>();
            var player = actor.AddComponent<Ethan.ActionEditor.ActionPlayer>();
            Assert.IsTrue(ActionEditorAgent.InspectActor(actor, action).success);
            action.attackList.Add(new Global.Attack { keyNumber = 15 });
            var result = ActionEditorAgent.InspectActor(actor, action);
            Assert.IsFalse(result.success);
            Assert.That(result.issues.Exists(i => i.track == "Attack" && i.severity == "Error"));
            Assert.IsFalse(player.IsPlaying);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    ActionEditorAgent.Result Read() => ActionEditorAgent.Execute(new ActionEditorAgent.Request { schemaVersion=2,operation="read_action",assetPaths=new[]{actionPath} });
    [Test]
    public void TargetedEditsValidateBeforeCommitAndRejectStaleRequests()
    {
        Assert.IsTrue(ActionEditorAgent.Execute(CreateRequest()).success);
        var read=Read(); Assert.IsTrue(read.success,JsonUtility.ToJson(read));
        string originalGuid=AssetDatabase.AssetPathToGUID(actionPath); var before=File.ReadAllBytes(actionPath);
        var request=new ActionEditorAgent.Request { schemaVersion=2,operation="edit_action",assetPaths=new[]{actionPath},expectedDigest=read.contentDigest,dryRun=true,
            edits=new[]{new ActionEditorAgent.Edit { path="skillName",value=new ActionEditorAgent.Field { kind="String",text="Edited" } }} };
        var dry=ActionEditorAgent.Execute(request); Assert.IsTrue(dry.success,JsonUtility.ToJson(dry)); Assert.AreEqual(1,dry.changes.Count);
        CollectionAssert.AreEqual(before,File.ReadAllBytes(actionPath));
        request.dryRun=false; Assert.IsTrue(ActionEditorAgent.Execute(request).success);
        Assert.AreEqual(originalGuid,AssetDatabase.AssetPathToGUID(actionPath));
        Assert.IsFalse(ActionEditorAgent.Execute(request).success,"Old digest must not edit newer content.");
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.AreEqual("Agent Action",AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath).skillName);
        Undo.PerformRedo(); Assert.AreEqual("Edited",AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath).skillName);
        request.expectedDigest=Read().contentDigest;
        request.edits=new[]{new ActionEditorAgent.Edit { path="animSegments.Array.data[0].clip",value=new ActionEditorAgent.Field { kind="ObjectReference" } }};
        Assert.IsFalse(ActionEditorAgent.Execute(request).success);
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath).animSegments[0].clip);
    }
    [Test]
    public void CloneHasIndependentIdentityStableReferencesAndNoOverwrite()
    {
        Assert.IsTrue(ActionEditorAgent.Execute(CreateRequest()).success);
        var read=Read();
        var request=new ActionEditorAgent.Request { schemaVersion=2,operation="clone_action",assetPaths=new[]{actionPath},expectedDigest=read.contentDigest,outputAssetPath=folder+"/Clone.asset" };
        var result=ActionEditorAgent.Execute(request); Assert.IsTrue(result.success,JsonUtility.ToJson(result));
        Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(actionPath),AssetDatabase.AssetPathToGUID(request.outputAssetPath));
        Assert.AreEqual(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath).animSegments[0].clip,AssetDatabase.LoadAssetAtPath<SkillConfigSO>(request.outputAssetPath).animSegments[0].clip);
        Assert.IsFalse(ActionEditorAgent.Execute(request).success);
        AssetDatabase.ImportAsset(actionPath,ImportAssetOptions.ForceUpdate);
        Assert.AreEqual(read.contentDigest,Read().contentDigest);
    }
    [Test]
    public void StructuralEditsAndInvalidReferenceAreAtomic()
    {
        Assert.IsTrue(ActionEditorAgent.Execute(CreateRequest()).success);
        var request=new ActionEditorAgent.Request { schemaVersion=2,operation="edit_action",assetPaths=new[]{actionPath},expectedDigest=Read().contentDigest,
            edits=new[]{new ActionEditorAgent.Edit { operation="insert",path="interactionWindows",index=0 },
                new ActionEditorAgent.Edit { path="interactionWindows.Array.data[0].endKeyNumber",value=new ActionEditorAgent.Field { kind="Integer",integer=15 } }} };
        var result=ActionEditorAgent.Execute(request); Assert.IsTrue(result.success,JsonUtility.ToJson(result));
        Assert.AreEqual(15,AssetDatabase.LoadAssetAtPath<SkillConfigSO>(actionPath).interactionWindows[0].endKeyNumber);
        var before=File.ReadAllBytes(actionPath); request.expectedDigest=Read().contentDigest;
        request.edits=new[]{new ActionEditorAgent.Edit { operation="remove",path="interactionWindows",index=0 },new ActionEditorAgent.Edit { path="animSegments.Array.data[0].clip",value=new ActionEditorAgent.Field { kind="ObjectReference",referenceGuid="missing",referenceLocalId=123 } }};
        Assert.IsFalse(ActionEditorAgent.Execute(request).success); CollectionAssert.AreEqual(before,File.ReadAllBytes(actionPath));
    }
}