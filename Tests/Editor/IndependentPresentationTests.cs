using Ethan.ActionEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;

public class IndependentPresentationTests
{
    GameObject actor;
    AnimationClip clip;
    SkillConfigSO config;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        actor = new GameObject("PresentationTest");
        clip = new AnimationClip { frameRate = 30 };
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), AnimationCurve.Linear(0, 0, 2, 2));
        config = ScriptableObject.CreateInstance<SkillConfigSO>();
        config.SetActionEditorSchemaVersion(1);
        config.animSegments.Add(new Global.AnimClipSegment { clip = clip, clipStartFrame = 15, clipEndFrame = 45 });
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(actor);
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(clip);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void AnimationSamplesCropAndHoldsEnd()
    {
        var animator = actor.AddComponent<ActionClipAnimator>();
        animator.Play(config.animSegments[0], 30);
        animator.Sample(0);
        Assert.That(actor.transform.position.y, Is.EqualTo(.5f).Within(.03f));
        animator.Sample(15);
        Assert.That(actor.transform.position.y, Is.EqualTo(1).Within(.03f));
        animator.Sample(200);
        Assert.That(actor.transform.position.y, Is.EqualTo(1.5f).Within(.03f));
        animator.Stop(ActionStopReason.Interrupted);
        animator.Sample(0);
        Assert.That(actor.transform.position.y, Is.EqualTo(1.5f).Within(.03f));
    }

    [Test]
    public void PlayerClockControlsSpeedAndLocalHold()
    {
        actor.AddComponent<ActionClipAnimator>();
        var player = actor.AddComponent<ActionPlayer>();
        var hold = actor.AddComponent<ActionHitStop>();
        player.AutoAdvance = false;
        hold.AutoAdvance = false;
        float globalTime = Time.timeScale;
        Assert.IsTrue(player.TryPlay(new ActionPlayRequest(config, speed: 2), out var error), error.Message);
        player.Advance(.25f);
        Assert.That(player.CurrentFrame, Is.EqualTo(15));
        Assert.That(actor.transform.position.y, Is.EqualTo(1).Within(.03f));
        hold.Request(.2f);
        hold.Request(.1f);
        player.Advance(1);
        Assert.That(player.CurrentFrame, Is.EqualTo(15));
        Assert.That(hold.Remaining, Is.EqualTo(.2f));
        object otherOwner = new object();
        player.SetPresentationPaused(otherOwner, true);
        hold.Advance(.25f);
        Assert.IsTrue(player.IsPaused, "Expiring feedback must not release another owner's pause.");
        player.SetPresentationPaused(otherOwner, false);
        player.Advance(.1f);
        Assert.That(player.CurrentFrame, Is.EqualTo(21));
        Assert.That(Time.timeScale, Is.EqualTo(globalTime));
        hold.Request(1);
        hold.enabled = false;
        Assert.IsFalse(player.IsPaused);
    }

    [Test]
    public void CameraPivotRestoresOnCompletionAndDisable()
    {
        actor.transform.localPosition = new Vector3(2, 3, 4);
        var origin = actor.transform.localPosition;
        var shake = actor.AddComponent<ActionCameraShake>();
        shake.AutoAdvance = false;
        shake.Request(1, .5f);
        shake.Advance(.13f);
        Assert.AreNotEqual(origin, actor.transform.localPosition);
        shake.Advance(1);
        Assert.AreEqual(origin, actor.transform.localPosition);
        shake.Request(1, .5f);
        shake.Advance(.13f);
        shake.enabled = false;
        Assert.AreEqual(origin, actor.transform.localPosition);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => shake.Request(float.NaN, 1));
    }

    [Test]
    public void PublicAssemblyHasNoQuarantinedTypes()
    {
        var assembly = typeof(ActionPlayer).Assembly;
        foreach (var name in new[] { "ClipAnimator", "ClipState", "ClipMixerState", "RootMotionCollector", "FreezeFrame", "CameraShake", "HitFeelDriver" })
            Assert.IsNull(assembly.GetType(name));
        foreach (var reference in assembly.GetReferencedAssemblies())
            Assert.That(reference.Name, Does.Not.Contain("Assembly-CSharp"));
    }

    [TestCase(30)] [TestCase(60)] [TestCase(120)]
    public void ExplicitTimelineCompletesAtContentEndAtAllRenderRates(int renderFps)
    {
        config.InitializeExplicitTiming();
        actor.AddComponent<ActionClipAnimator>(); var player=actor.AddComponent<ActionPlayer>(); player.AutoAdvance=false;
        Assert.IsTrue(player.TryPlay(new ActionPlayRequest(config),out var error),error.Message);
        for(int i=0;i<renderFps-1;i++) player.Advance(1f/renderFps);
        Assert.IsTrue(player.IsPlaying);
        player.Advance(2f/renderFps);
        Assert.IsFalse(player.IsPlaying);
        Assert.That(actor.transform.position.y,Is.EqualTo(1.5f).Within(.03f));
    }
    [Test]
    public void OutgoingClipAdvancesDuringBlendAndResetReleasesGraph()
    {
        var next=new AnimationClip { frameRate=60 };
        AnimationUtility.SetEditorCurve(next,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.y"),AnimationCurve.Constant(0,2,10));
        try {
            var output=actor.AddComponent<ActionClipAnimator>();
            output.Play(new Global.AnimClipSegment { clip=clip },30); output.Sample(15);
            output.Play(new Global.AnimClipSegment { clip=next,startFrame=15,blendInFrames=30 },30); output.Sample(30);
            Assert.That(actor.transform.position.y,Is.EqualTo(5.5f).Within(.04f));
            output.ResetOutput(); output.Sample(0);
            Assert.That(actor.transform.position.y,Is.EqualTo(5.5f).Within(.04f));
        } finally { Object.DestroyImmediate(next); }
    }
}
