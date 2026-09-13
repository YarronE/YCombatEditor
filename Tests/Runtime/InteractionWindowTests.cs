using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class InteractionWindowTests
    {
        readonly InteractionWindowRuntime runtime = new InteractionWindowRuntime();
        InteractionQuery Query(Global.Attack attack = null) => new InteractionQuery(null, null, InteractionQuery.IncomingHit, attack);
        [SetUp] public void Setup() => runtime.Clear();

        [Test]
        public void WindowUsesInclusiveBoundsAndSignalFilter()
        {
            var list = new[] { new InteractionWindow { keyNumber = 3, endKeyNumber = 5 } };
            Assert.IsFalse(runtime.Resolve(list, 2, Query(), out _, out _));
            Assert.IsTrue(runtime.Resolve(list, 3, Query(), out _, out _));
            Assert.IsTrue(runtime.Resolve(list, 5, Query(), out _, out _));
            Assert.IsFalse(runtime.Resolve(list, 6, Query(), out _, out _));
            Assert.IsFalse(runtime.Resolve(list, 4, new InteractionQuery(null, null, "interact"), out _, out _));
        }

        [Test]
        public void RejectedHighPriorityFallsBackAndOnlyWinnerConsumes()
        {
            var list = new[] {
                new InteractionWindow { response = InteractionResponse.Parry, priority = 30, maxActivations = 1 },
                new InteractionWindow { response = InteractionResponse.Block, priority = 10, maxActivations = 1 }
            };
            Assert.IsTrue(runtime.Resolve(list, 0, Query(new Global.Attack { unparryable = true }), out var block, out _));
            Assert.AreEqual(1, block.Index);
            Assert.IsTrue(runtime.Resolve(list, 0, Query(new Global.Attack()), out var parry, out _));
            Assert.AreEqual(0, parry.Index);
            Assert.IsFalse(runtime.Resolve(list, 0, Query(), out _, out _));
        }

        [Test]
        public void EvaluateDoesNotConsumeAndClearRearmsWindow()
        {
            var list = new[] { new InteractionWindow { maxActivations = 1 } };
            Assert.IsTrue(runtime.Evaluate(list, 0, Query(), out _, out _));
            Assert.IsTrue(runtime.Evaluate(list, 0, Query(), out _, out _));
            Assert.IsTrue(runtime.Resolve(list, 0, Query(), out var first, out _));
            Assert.IsTrue(first.FirstActivation);
            Assert.IsFalse(runtime.Resolve(list, 0, Query(), out _, out _));
            runtime.Clear();
            Assert.IsTrue(runtime.Resolve(list, 0, Query(), out first, out _));
            Assert.IsTrue(first.FirstActivation);
        }

        [Test]
        public void UnlimitedDefenseReportsFirstActivationOnlyOnce()
        {
            var list = new[] { new InteractionWindow() };
            Assert.IsTrue(runtime.Resolve(list, 0, Query(), out var first, out _));
            Assert.IsTrue(first.FirstActivation);
            Assert.IsTrue(runtime.Resolve(list, 1, Query(), out var second, out _));
            Assert.IsFalse(second.FirstActivation);
        }

        [Test]
        public void CustomConditionsAreAndedAndMissingConditionsFailClosed()
        {
            var filter = ScriptableObject.CreateInstance<InteractionAttackCondition>();
            try
            {
                filter.requiredTag = "heavy";
                var list = new[] { new InteractionWindow { signal = "counter", conditions = new List<InteractionCondition> { filter } } };
                Assert.IsFalse(runtime.Resolve(list, 0, new InteractionQuery(null, null, "counter", new Global.Attack()), out _, out _));
                Assert.IsTrue(runtime.Resolve(list, 0, new InteractionQuery(null, null, "counter", new Global.Attack { interactionTag = "heavy" }), out _, out _));
                list[0].conditions.Add(null);
                Assert.IsFalse(runtime.Resolve(list, 0, new InteractionQuery(null, null, "counter", new Global.Attack { interactionTag = "heavy" }), out _, out _));
            }
            finally { Object.DestroyImmediate(filter); }
        }

        [Test]
        public void FrontOnlyGuardRejectsAttackFromBehind()
        {
            var actor = new GameObject("defender"); var other = new GameObject("source");
            try
            {
                var list = new[] { new InteractionWindow { sourceAngle = 120 } };
                other.transform.position = Vector3.back;
                Assert.IsFalse(runtime.Resolve(list, 0, new InteractionQuery(actor, other.transform, InteractionQuery.IncomingHit), out _, out _));
                other.transform.position = Vector3.forward;
                Assert.IsTrue(runtime.Resolve(list, 0, new InteractionQuery(actor, other.transform, InteractionQuery.IncomingHit), out _, out _));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(other); }
        }

        [Test]
        public void ExternalActionStopAndReplacementReleaseInteractionState()
        {
            var actor = new GameObject("actor"); var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            var clip = new AnimationClip(); clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            try
            {
                config.skillClip = clip;
                config.interactionWindows.Add(new InteractionWindow { maxActivations = 1 });
                var player = actor.AddComponent<ActionPlayer>();
                Assert.IsTrue(player.TryBeginExternal(new ActionPlayRequest(config), out _));
                Assert.IsTrue(player.TryResolveInteraction(Query(), out _));
                Assert.IsFalse(player.TryResolveInteraction(Query(), out _));
                Assert.IsTrue(player.TryBeginExternal(new ActionPlayRequest(config), out _));
                Assert.IsTrue(player.TryResolveInteraction(Query(), out _));
                player.CompleteExternal(ActionStopReason.Interrupted);
                Assert.IsFalse(player.TryResolveInteraction(Query(), out _));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(config); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void ValidatorRejectsDuplicateIdsBadRangeAndMissingConditions()
        {
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            try
            {
                config.interactionWindows.Add(new InteractionWindow { id = "guard" });
                config.interactionWindows.Add(new InteractionWindow { id = "guard" });
                config.interactionWindows.Add(new InteractionWindow { id = "bad-range", keyNumber = 9, endKeyNumber = 3 });
                config.interactionWindows.Add(new InteractionWindow { id = "missing", conditions = new List<InteractionCondition> { null } });
                var issues = ActionConfigValidator.Validate(config);
                Assert.AreEqual(3, issues.FindAll(x => x.Code == "ACT140").Count);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void InputBufferPreservesOtherKeysAndRefreshesLatestIntent()
        {
            var buffer = new ActionInputBuffer();
            buffer.Capture(KeyCode.Mouse0, 1); buffer.Capture(KeyCode.LeftShift, 1);
            buffer.Consume(KeyCode.LeftShift);
            Assert.IsTrue(buffer.Contains(KeyCode.Mouse0, 1.1f, .15f));
            buffer.Capture(KeyCode.Mouse0, 1.14f);
            Assert.AreEqual(1, buffer.Count);
            Assert.IsTrue(buffer.Contains(KeyCode.Mouse0, 1.2f, .15f));
            Assert.IsFalse(buffer.Contains(KeyCode.Mouse0, 1.4f, .15f));
        }
    }
}
