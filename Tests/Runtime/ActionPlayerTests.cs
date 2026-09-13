using Ethan.ActionEditor;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class ActionPlayerTests
    {
        GameObject _actor;
        SkillConfigSO _config;
        AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            _actor = new GameObject("ActionPlayerTestActor");
            _actor.AddComponent<TestAnimator>();
            _actor.AddComponent<TestFxHandler>();
            _actor.AddComponent<ActionPlayer>();
            _config = ScriptableObject.CreateInstance<SkillConfigSO>();
            _clip = new AnimationClip { frameRate = 30f };
            _config.animSegments.Add(new Global.AnimClipSegment { clip = _clip, startFrame = 0 });
        }

        [Test]
        public void ExplicitExitAfterAnimation_DispatchesLateEventBeforeCompleting()
        {
            var audio = AudioClip.Create("LateEvent", 16, 1, 8000, false);
            try
            {
                _config.exitFrame = 90;
                _config.fxList.Add(new Global.FxAndSound { keyNumber = 80, audioClip = audio });
                var player = _actor.GetComponent<ActionPlayer>();
                player.AutoAdvance = false;
                Assert.That(player.TryPlay(new ActionPlayRequest(_config), out var error), Is.True, error.Message);
                player.Advance(2.8f);
                Assert.That(player.IsPlaying, Is.True, "Explicit recovery frames must outlive the animation extent.");
                Assert.That(_actor.GetComponent<TestFxHandler>().Count, Is.EqualTo(1));
                player.Advance(.3f);
                Assert.That(player.IsPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(audio); }
        }
        [TestCase(0)]
        [TestCase(5)]
        public void FlowCompleteStopsBeforeSameFrameEventsAndClosesOpenRanges(int frame)
        {
            _config.exitFrame=60;
            var ranges=_actor.AddComponent<TestRangeHandler>();
            _config.cancelList.Add(new Global.CancelPoint { keyNumber=0,endKeyNumber=50 });
            _config.flowNodes.Add(new ActionFlowNode { kind=ActionFlowKind.Complete,keyNumber=frame });
            var sound=AudioClip.Create("FlowLate",16,1,8000,false);
            _config.fxList.Add(new Global.FxAndSound {keyNumber=frame,audioClip=sound});
            try {
                var player=_actor.GetComponent<ActionPlayer>();
                ActionStopReason? reason=null; player.Stopped+=(c,r)=>reason=r;
                Assert.That(player.TryPlay(new ActionPlayRequest(_config),out var error),Is.True,error.Message);
                player.Advance(1);
                Assert.That(player.IsPlaying,Is.False); Assert.That(reason,Is.EqualTo(ActionStopReason.Completed));
                Assert.That(_actor.GetComponent<TestFxHandler>().Count,Is.Zero);
                Assert.That(ranges.Phases.FindAll(p=>p==ActionEventPhase.Exit).Count,Is.EqualTo(frame==0?0:1));
            } finally { Object.DestroyImmediate(sound); }
        }

        [Test]
        public void FlowRequestBranchesBeforeFreeExitAndRejectsInvalidTargetsWithoutStopping()
        {
            var next=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {
                next.exitFrame=60; _config.exitFrame=60;
                next.animSegments.Add(new Global.AnimClipSegment {clip=_clip});
                _config.flowNodes.Add(new ActionFlowNode {kind=ActionFlowKind.Branch,keyNumber=3,command="Attack",nextAction=next});
                var player=_actor.GetComponent<ActionPlayer>();
                Assert.That(player.TryPlay(new ActionPlayRequest(_config),out _),Is.True);
                Assert.That(player.TryRequestFlow("Attack",null,null,out _),Is.False);
                player.Advance(3.1f/30);
                next.timelineFrameRate=float.NaN; next.timingVersion=1;
                Assert.That(player.TryRequestFlow("Attack",null,null,out _),Is.False);
                Assert.That(player.CurrentConfig,Is.SameAs(_config));
                next.timelineFrameRate=30;
                Assert.That(player.TryRequestFlow("Attack",null,null,out _),Is.True);
                Assert.That(player.CurrentConfig,Is.SameAs(next));
            } finally { Object.DestroyImmediate(next); }
        }

        [Test]
        public void FlowCallbackReplacementWinsWithoutFurtherBranchAttempts()
        {
            var next=ScriptableObject.CreateInstance<SkillConfigSO>();
            var winner=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {
                _config.exitFrame=60; next.exitFrame=60; winner.exitFrame=60;
                next.animSegments.Add(new Global.AnimClipSegment{clip=_clip}); winner.animSegments.Add(new Global.AnimClipSegment{clip=_clip});
                _config.flowNodes.Add(new ActionFlowNode{kind=ActionFlowKind.Branch,command="Attack",nextAction=next});
                _config.flowNodes.Add(new ActionFlowNode{kind=ActionFlowKind.Branch,command="Attack",nextAction=next});
                var player=_actor.GetComponent<ActionPlayer>();
                Assert.That(player.TryPlay(new ActionPlayRequest(_config),out _),Is.True);
                System.Action<SkillConfigSO,ActionStopReason> callback=null;
                callback=(c,r)=>{player.Stopped-=callback;player.TryPlay(new ActionPlayRequest(winner),out _);};
                player.Stopped+=callback;
                Assert.That(player.TryRequestFlow("Attack",null,null,out _),Is.False);
                Assert.That(player.CurrentConfig,Is.SameAs(winner));
            } finally { Object.DestroyImmediate(next); Object.DestroyImmediate(winner); }
        }

        [Test]
        public void AutomaticBranchesAtFrameZeroDoNotRecurseIndefinitely()
        {
            var next=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {
                next.exitFrame=60; _config.exitFrame=60;
                next.animSegments.Add(new Global.AnimClipSegment {clip=_clip});
                _config.flowNodes.Add(new ActionFlowNode {kind=ActionFlowKind.Branch,automatic=true,nextAction=next});
                next.flowNodes.Add(new ActionFlowNode {kind=ActionFlowKind.Branch,automatic=true,nextAction=_config});
                var player=_actor.GetComponent<ActionPlayer>();
                Assert.That(player.TryPlay(new ActionPlayRequest(_config),out _),Is.True);
                Assert.That(player.CurrentConfig,Is.SameAs(next));
                player.Advance(1f/30);
                Assert.That(player.CurrentConfig,Is.SameAs(_config));
            } finally { Object.DestroyImmediate(next); }
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void TryPlay_InvalidConfig_DoesNotStart()
        {
            _config.animSegments.Clear();
            var player = _actor.GetComponent<ActionPlayer>();
            bool result = player.TryPlay(new ActionPlayRequest(_config, requireHandlers: false), out var error);
            Assert.That(result, Is.False);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(error.Code, Is.EqualTo("ACT_PLAY_INVALID"));
        }

        [Test]
        public void TryPlay_DispatchesFrameZeroEventOnce()
        {
            var audio = AudioClip.Create("FrameZero", 16, 1, 8000, false);
            try
            {
                _config.fxList.Add(new Global.FxAndSound { keyNumber = 0, audioClip = audio });
                var player = _actor.GetComponent<ActionPlayer>();
                var receiver = _actor.GetComponent<TestFxHandler>();
                bool result = player.TryPlay(new ActionPlayRequest(_config), out var error);
                Assert.That(result, Is.True, error.Message);
                Assert.That(receiver.Count, Is.EqualTo(1));
                Assert.That(player.CurrentFrame, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(audio);
            }
        }

        [Test]
        public void ConfiguredDisplacement_IsAHandledBuiltInCapabilityAndStopsCleanly()
        {
            _config.moveSegmentList.Add(new Global.MoveSegment
            {
                keyNumber = 0,
                endKeyNumber = 2,
                maxDistance = 1f
            });
            var player = _actor.GetComponent<ActionPlayer>();
            var backend = new TestDisplacementBackend(_actor.transform);
            player.ConfigureDisplacement(backend);

            bool result = player.TryPlay(new ActionPlayRequest(_config), out var error);
            Assert.That(result, Is.True, error.Message);
            Assert.That(backend.Began, Is.True);
            Assert.That(player.CanHandle(typeof(Global.MoveSegment)), Is.True);

            player.Stop(ActionStopReason.Interrupted);
            Assert.That(backend.Ended, Is.True);
        }

        [Test]
        public void StartedCallbackCanStopBeforeAnyFrameIsDispatched()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            player.Started += _ => player.Stop(ActionStopReason.Interrupted);
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            Assert.That(player.IsPlaying, Is.False);
            Assert.That(_actor.GetComponent<TestAnimator>().PlayCount, Is.Zero);
        }

        [Test]
        public void AnimationCallbackCanStopWithoutDispatchingRemainingSegments()
        {
            _config.animSegments.Add(new Global.AnimClipSegment { clip = _clip, startFrame = 0 });
            var player = _actor.GetComponent<ActionPlayer>();
            var animator = _actor.GetComponent<TestAnimator>();
            animator.OnPlay = () => player.Stop(ActionStopReason.Interrupted);
            Assert.DoesNotThrow(() => player.TryPlay(new ActionPlayRequest(_config), out _));
            Assert.That(animator.PlayCount, Is.EqualTo(1));
            Assert.That(player.CurrentConfig, Is.Null);
        }

        [Test]
        public void RangeCallbackStopCleansUpExactlyOnceAndAbortsFrameCatchup()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            var handler = _actor.AddComponent<TestRangeHandler>();
            _config.cancelList.Add(new Global.CancelPoint { keyNumber = 1, endKeyNumber = 8 });
            handler.OnCancel = phase => { if (phase == ActionEventPhase.Enter) player.Stop(ActionStopReason.Interrupted); };
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            Assert.DoesNotThrow(() => player.Advance(1f));
            Assert.That(handler.Phases, Is.EqualTo(new[] { ActionEventPhase.Enter, ActionEventPhase.Exit }));
            Assert.That(player.IsPlaying, Is.False);
            player.Stop(ActionStopReason.Interrupted);
            Assert.That(handler.Phases.Count, Is.EqualTo(2));
        }

        [Test]
        public void RangeNaturalExitIsNotRepeatedByStop()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            var handler = _actor.AddComponent<TestRangeHandler>();
            _config.cancelList.Add(new Global.CancelPoint { keyNumber = 0, endKeyNumber = 1 });
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            player.Advance(2.1f / 30f);
            player.Stop(ActionStopReason.Interrupted);
            Assert.That(handler.Phases, Is.EqualTo(new[] { ActionEventPhase.Enter, ActionEventPhase.Tick, ActionEventPhase.Exit }));
        }

        [Test]
        public void ProjectileRangeDispatchesEveryFrameAndOneExit()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            var handler = _actor.AddComponent<TestRangeHandler>();
            _config.projectileList.Add(new Global.Projectile { keyNumber = 0, endKeyNumber = 2, prefab = _actor });
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            player.Advance(3.1f / 30f);
            Assert.That(handler.Phases, Is.EqualTo(new[] { ActionEventPhase.Enter, ActionEventPhase.Tick, ActionEventPhase.Tick, ActionEventPhase.Exit }));
        }

        [Test]
        public void StopCallbackReplacementWinsOverOuterPlayRequest()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            System.Action<SkillConfigSO, ActionStopReason> callback = null;
            callback = (stoppedConfig, reason) => {
                player.Stopped -= callback;
                Assert.That(player.TryPlay(new ActionPlayRequest(_config, phase: 2), out _), Is.True);
            };
            player.Stopped += callback;
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo("ACT_PLAY_SUPERSEDED"));
            Assert.That(player.CurrentPhase, Is.EqualTo(2));
            Assert.That(player.IsPlaying, Is.True);
        }

        [Test]
        public void InvalidReplacementDoesNotStopCurrentAction()
        {
            var player = _actor.GetComponent<ActionPlayer>();
            Assert.That(player.TryPlay(new ActionPlayRequest(_config), out _), Is.True);
            Assert.That(player.TryPlay(new ActionPlayRequest(null), out _), Is.False);
            Assert.That(player.CurrentConfig, Is.SameAs(_config));
            Assert.That(player.IsPlaying, Is.True);
            Assert.That(_actor.GetComponent<TestAnimator>().StopCount, Is.Zero);
        }

        [Test]
        public void PhaseTwoOnlyEventsRequireHandlers()
        {
            _config.phase2AttackList.Add(new Global.Attack());
            Assert.That(_actor.GetComponent<ActionPlayer>().TryPlay(new ActionPlayRequest(_config, phase: 2), out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo("ACT_PLAY_INVALID"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NonFiniteSpeedIsRejected(float speed)
        {
            Assert.That(_actor.GetComponent<ActionPlayer>().TryPlay(new ActionPlayRequest(_config, speed: speed), out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo("ACT_PLAY_INVALID_REQUEST"));
        }

        sealed class TestRangeHandler : MonoBehaviour, IActionEventHandler<Global.CancelPoint>, IActionEventHandler<Global.Projectile>
        {
            public readonly List<ActionEventPhase> Phases = new List<ActionEventPhase>();
            public System.Action<ActionEventPhase> OnCancel;
            public void Handle(in ActionExecutionContext context, Global.CancelPoint data)
            {
                Phases.Add(context.EventPhase);
                OnCancel?.Invoke(context.EventPhase);
            }
            public void Handle(in ActionExecutionContext context, Global.Projectile data) => Phases.Add(context.EventPhase);
        }

        sealed class TestAnimator : MonoBehaviour, IActionAnimator
        {
            public int PlayCount;
            public int StopCount;
            public System.Action OnPlay;
            public bool CanPlay(Global.AnimClipSegment segment) => segment?.clip != null;
            public void Play(Global.AnimClipSegment segment, float fallbackFrameRate) { PlayCount++; OnPlay?.Invoke(); }
            public void Stop(ActionStopReason reason) { StopCount++; }
        }

        sealed class TestFxHandler : MonoBehaviour, IActionEventHandler<Global.FxAndSound>
        {
            public int Count { get; private set; }
            public void Handle(in ActionExecutionContext context, Global.FxAndSound data) => Count++;
        }

        sealed class TestDisplacementBackend : IDisplacementBackend
        {
            public TestDisplacementBackend(Transform transform) => Transform = transform;

            public Transform Transform { get; }
            public bool IsGrounded => true;
            public bool CanMove => true;
            public bool Began { get; private set; }
            public bool Ended { get; private set; }
            public void BeginDisplacement() => Began = true;
            public void Move(Vector3 delta) { }
            public void SetFacing(Vector3 horizontalDirection) { }
            public void SetActorCollisionIgnored(bool ignored) { }
            public void EndDisplacement() => Ended = true;
        }
    }
}
