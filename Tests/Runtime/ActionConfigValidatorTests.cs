using System.Linq;
using Ethan.ActionEditor;
using NUnit.Framework;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class ActionConfigValidatorTests
    {
        SkillConfigSO _config;
        AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkillConfigSO>();
            _clip = new AnimationClip();
            _config.animSegments.Add(new Global.AnimClipSegment { clip = _clip, startFrame = 0 });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void ValidMinimalConfig_HasNoErrors()
        {
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(ActionConfigValidator.HasErrors(issues), Is.False);
        }

        [TestCase(Global.EffectContentKind.Audio)]
        [TestCase(Global.EffectContentKind.VisualEffect)]
        public void TypedEffectsRequireMatchingReferenceEvenInLegacyCompatibility(Global.EffectContentKind kind)
        {
            var particle = new GameObject("Particle").AddComponent<ParticleSystem>();
            var audio = AudioClip.Create("Audio", 32, 1, 44100, false);
            try
            {
                var effect = new Global.FxAndSound { contentKind = kind };
                _config.fxList.Add(effect);
                var capabilities = new ActionValidationCapabilities { AllowLegacyEmptyFx = true };
                Assert.That(ActionConfigValidator.Validate(_config, capabilities).Any(x => x.Code == "ACT163"), Is.True);
                if (kind == Global.EffectContentKind.Audio) effect.audioClip = audio; else effect.particleSystem = particle;
                Assert.That(ActionConfigValidator.Validate(_config, capabilities).Any(x => x.Code == "ACT163"), Is.False);
                effect.audioClip = audio; effect.particleSystem = particle;
                Assert.That(ActionConfigValidator.Validate(_config, capabilities).Any(x => x.Code == "ACT163"), Is.True);
                effect.contentKind = Global.EffectContentKind.Legacy;
                Assert.That(ActionConfigValidator.Validate(_config, capabilities).Any(x => x.Code == "ACT163" || x.Code == "ACT115"), Is.False);
            }
            finally { Object.DestroyImmediate(particle.gameObject); Object.DestroyImmediate(audio); }
        }

        [TestCase(0, false, true)]
        [TestCase(0, true, false)]
        [TestCase(1, true, true)]
        public void EmptyFxCompatibility_IsExplicitAndLimitedToLegacy(int timing, bool compatible, bool error)
        {
            _config.timingVersion=timing;
            _config.fxList.Add(new Global.FxAndSound());
            var issues=ActionConfigValidator.Validate(_config,new ActionValidationCapabilities{AllowLegacyEmptyFx=compatible});
            Assert.That(issues.Any(x=>x.Code=="ACT115"&&x.Severity==ActionValidationSeverity.Error),Is.EqualTo(error));
            Assert.That(issues.Any(x=>x.Code=="ACT115"),Is.True,"Compatibility must retain a diagnostic.");
        }

        [Test]
        public void ExternalLegacyCompatibility_DoesNotBypassMissingProjectileValidation()
        {
            var actor=new GameObject("Legacy host");
            try
            {
                _config.timingVersion=0;_config.fxList.Add(new Global.FxAndSound());
                var player=actor.AddComponent<ActionPlayer>();
                Assert.That(player.TryBeginExternal(new ActionPlayRequest(_config),out _),Is.False);
                Assert.That(player.TryBeginExternal(new ActionPlayRequest(_config,allowLegacyEmptyFx:true),out _),Is.True);
                _config.projectileList.Add(new Global.Projectile());
                Assert.That(player.TryBeginExternal(new ActionPlayRequest(_config,allowLegacyEmptyFx:true),out _),Is.False);
                Assert.That(player.IsPlaying,Is.True,"Rejected replacement keeps the current action.");
            }
            finally{Object.DestroyImmediate(actor);}
        }

        [Test]
        public void LegacyCollapsedAttackRange_IsAcceptedAsSingleFrame()
        {
            _config.attackList.Add(new Global.Attack { keyNumber = 10, endKeyNumber = 5 });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT113" && x.Track == "Attack" && x.EventIndex == 0), Is.False);
        }

        [Test]
        public void ReversedStrictJumpRange_ReturnsStableError()
        {
            _config.jumpList.Add(new Global.Jump { beginKey = 10, endKey = 5 });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT113" && x.Track == "Jump" && x.EventIndex == 0), Is.True);
        }

        [Test]
        public void MissingProjectilePrefab_IsError()
        {
            _config.projectileList.Add(new Global.Projectile { keyNumber = 1 });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT114"), Is.True);
        }

        [Test]
        public void MissingRuntimeHandler_IsReportedOnlyWhenRequired()
        {
            _config.fxList.Add(new Global.FxAndSound { keyNumber = 0 });
            var capabilities = new ActionValidationCapabilities
            {
                HasAnimator = true,
                RequireEventHandlers = true,
                CanHandleEvent = _ => false
            };
            var issues = ActionConfigValidator.Validate(_config, capabilities);
            Assert.That(issues.Any(x => x.Code == "ACT201" && x.Track == "FxAndSound"), Is.True);
        }

        [Test]
        public void MissingEffectReferences_ReturnStableErrors()
        {
            _config.fxList.Add(new Global.FxAndSound());
            _config.hitFxList.Add(new Global.HitFx());
            _config.warningCueList.Add(new Global.WarningCue());
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT115" && x.Track == "FxAndSound"), Is.True);
            Assert.That(issues.Any(x => x.Code == "ACT117" && x.Track == "HitFx"), Is.True);
            Assert.That(issues.Any(x => x.Code == "ACT118" && x.Track == "WarningCue"), Is.True);
        }

        [Test]
        public void EventAfterExitFrame_IsError()
        {
            _config.exitFrame = 5;
            _config.attackList.Add(new Global.Attack { keyNumber = 6, endKeyNumber = 6 });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT116" && x.Track == "Attack"), Is.True);
        }

        [Test]
        public void InvalidMoveConfiguration_ReturnsStableErrors()
        {
            _config.moveSegmentList.Add(new Global.MoveSegment
            {
                maxDistance = -1f,
                arriveTolerance = -1f,
                curve = null
            });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT123"), Is.True);
            Assert.That(issues.Any(x => x.Code == "ACT124"), Is.True);
        }

        [Test]
        public void ConflictingAdjustMotionRanges_AreRejected()
        {
            _config.adjustMotionList.Add(new Global.AdjustMotionSegment { keyNumber = 1, endKeyNumber = 5, rotationSpeed = 90f });
            _config.adjustMotionList.Add(new Global.AdjustMotionSegment { keyNumber = 4, endKeyNumber = 8, rotationSpeed = 180f });
            var issues = ActionConfigValidator.Validate(_config);
            Assert.That(issues.Any(x => x.Code == "ACT130" && x.Track == "AdjustMotion" && x.EventIndex == 1), Is.True);
        }
    }
}
