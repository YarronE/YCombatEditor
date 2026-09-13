using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class ThrowingInteractionTestCondition : InteractionCondition
    { public override bool Evaluate(in InteractionQuery query,out string rejection) { throw new System.InvalidOperationException("test exception"); } }
    public class InteractionConditionGroupTests
    {
        InteractionConditionGroup group, nested;
        InteractionAttackCondition heavy;
        [SetUp] public void SetUp()
        {
            group=ScriptableObject.CreateInstance<InteractionConditionGroup>(); nested=ScriptableObject.CreateInstance<InteractionConditionGroup>();
            heavy=ScriptableObject.CreateInstance<InteractionAttackCondition>(); heavy.requiredTag="heavy";
            group.children.Add(heavy);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(group); Object.DestroyImmediate(nested); Object.DestroyImmediate(heavy); }
        [TestCase(InteractionConditionOperator.All,true)] [TestCase(InteractionConditionOperator.Any,true)] [TestCase(InteractionConditionOperator.Not,false)]
        public void OperatorsEvaluatePureQueries(InteractionConditionOperator op,bool expected)
        {
            group.operation=op;
            Assert.AreEqual(expected,group.Evaluate(new InteractionQuery(null,null,"test",new Global.Attack { interactionTag="heavy" }),out _));
        }
        [Test] public void CyclesAndMissingChildrenFailClosedUnderNot()
        {
            group.operation=InteractionConditionOperator.Not; group.children[0]=nested; nested.children.Add(group);
            Assert.IsNotNull(group.ConfigurationError);
            Assert.IsFalse(group.Evaluate(default,out _));
            nested.children[0]=null;
            Assert.IsFalse(group.Evaluate(default,out _));
        }
        [Test] public void NotDoesNotTurnExceptionsIntoAcceptance()
        {
            var throwing=ScriptableObject.CreateInstance<ThrowingInteractionTestCondition>();
            try {
                group.operation=InteractionConditionOperator.Not; group.children[0]=throwing;
                Assert.IsFalse(group.Evaluate(default,out var reason)); StringAssert.Contains("test exception",reason);
                var runtime=new InteractionWindowRuntime();
                Assert.IsFalse(runtime.Resolve(new[]{new InteractionWindow { conditions=new List<InteractionCondition>{group} }},0,new InteractionQuery(null,null,InteractionQuery.IncomingHit),out _,out _));
            } finally { Object.DestroyImmediate(throwing); }
        }
        [Test] public void AllAndAnyDifferForMixedChildResults()
        {
            nested.operation=InteractionConditionOperator.Not; nested.children.Add(heavy); group.children.Add(nested);
            var query=new InteractionQuery(null,null,"test",new Global.Attack { interactionTag="heavy" });
            Assert.IsFalse(group.Evaluate(query,out _)); group.operation=InteractionConditionOperator.Any; Assert.IsTrue(group.Evaluate(query,out _));
        }
        [Test] public void TraceIsNonConsumingAndSharedAssetsHaveIndependentCounts()
        {
            var windows=new[]{new InteractionWindow { conditions=new List<InteractionCondition>{group},maxActivations=1 }};
            var a=new InteractionWindowRuntime { DiagnosticsEnabled=true }; var b=new InteractionWindowRuntime();
            var query=new InteractionQuery(null,null,InteractionQuery.IncomingHit,new Global.Attack { interactionTag="heavy" });
            Assert.IsTrue(a.Evaluate(windows,2,query,out _,out _));
            Assert.IsTrue(a.LastEvaluation[0].selected); Assert.AreEqual(2,a.LastEvaluation[0].conditions.Count);
            Assert.IsTrue(a.Resolve(windows,2,query,out _,out _)); Assert.IsFalse(a.Resolve(windows,2,query,out _,out _));
            Assert.IsTrue(b.Resolve(windows,2,query,out _,out _));
        }
    }
}
