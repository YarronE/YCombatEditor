using System;
using Ethan.ActionEditor;
using Ethan.ActionEditor.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ActionTimingTests
{
    SkillConfigSO config;
    AnimationClip a, b;
    [SetUp] public void SetUp()
    {
        config=ScriptableObject.CreateInstance<SkillConfigSO>();
        a=new AnimationClip { frameRate=30 }; b=new AnimationClip { frameRate=60 };
        foreach(var clip in new[]{a,b}) AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.x"),AnimationCurve.Linear(0,0,1,1));
        config.animSegments.Add(new Global.AnimClipSegment { clip=a });
        config.animSegments.Add(new Global.AnimClipSegment { clip=b, startFrame=30 });
    }
    [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(config); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
    [Test] public void ExplicitMixedFpsUsesSecondsAndSourceCrop()
    {
        config.InitializeExplicitTiming();
        Assert.AreEqual(60,config.RuntimeEndFrame);
        var segment=config.animSegments[1]; segment.clipStartFrame=15; segment.clipEndFrame=45;
        Assert.AreEqual(15,ActionTiming.Duration(config,segment));
        Assert.AreEqual(.5f,ActionTiming.SourceTime(segment,37.5f,30),.0001f);
        Assert.AreEqual(45,config.RuntimeEndFrame);
        Assert.AreEqual(60,config.TotalTimelineFrames,"Editor viewport minimum must not extend runtime.");
    }
    [Test] public void LegacyIsNotSilentlyNormalized()
    {
        Assert.IsFalse(config.UsesExplicitTiming);
        Assert.AreEqual(90,config.RuntimeEndFrame);
        Assert.IsTrue(ActionConfigValidator.Validate(config).Exists(i=>i.Code=="ACT152"));
    }
    [TestCase(0)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(241)]
    public void InvalidExplicitRatesReject(float fps)
    {
        config.InitializeExplicitTiming(fps);
        Assert.IsTrue(ActionConfigValidator.Validate(config).Exists(i=>i.Code=="ACT151"));
    }
    [Test] public void TimingMigrationBacksUpAndPreservesIdentityAndUndo()
    {
        string folder="Assets/Timing_"+Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets",folder.Substring(7));
        string backup=null;
        try
        {
            AssetDatabase.CreateAsset(a,folder+"/A.anim"); AssetDatabase.CreateAsset(b,folder+"/B.anim");
            AssetDatabase.CreateAsset(config,folder+"/Action.asset");
            string guid=AssetDatabase.AssetPathToGUID(folder+"/Action.asset");
            Assert.IsFalse(ActionTimingMigration.Migrate(config,false,out _,out _));
            Assert.IsTrue(ActionTimingMigration.Migrate(config,true,out backup,out var error),error);
            Assert.IsFalse(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(backup).UsesExplicitTiming);
            Assert.AreEqual(guid,AssetDatabase.AssetPathToGUID(folder+"/Action.asset"));
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Assert.IsFalse(config.UsesExplicitTiming);
            Undo.PerformRedo(); Assert.IsTrue(config.UsesExplicitTiming);
            AssetDatabase.SaveAssetIfDirty(config);
        }
        finally
        {
            if(backup!=null) AssetDatabase.DeleteAsset(backup);
            AssetDatabase.DeleteAsset(folder); config=null; a=null; b=null;
        }
    }
}
