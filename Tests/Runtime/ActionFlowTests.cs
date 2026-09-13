using NUnit.Framework;
using UnityEngine;

namespace Ethan.ActionEditor.Tests
{
    public sealed class ActionFlowTests
    {
        SkillConfigSO config;
        [SetUp] public void Setup() { config = ScriptableObject.CreateInstance<SkillConfigSO>(); config.exitFrame = 60; config.cancelPriority = Global.CancelPriority.Lv1; }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(config); }

        [Test]
        public void ExitPermissionIsAWindowNotCompletionAndChecksEachCandidatesCategory()
        {
            config.flowNodes.Add(new ActionFlowNode { keyNumber=10,kind=ActionFlowKind.AllowExit,category="Dodge" });
            Assert.That(ActionFlow.CanExit(config,null,9,2,"Dodge"),Is.False);
            Assert.That(ActionFlow.CanExit(config,null,10,1,"Dodge"),Is.False);
            Assert.That(ActionFlow.CanExit(config,null,10,2,"Attack"),Is.False);
            Assert.That(ActionFlow.CanExit(config,null,45,2,"Dodge"),Is.True);
            Assert.That(ActionFlow.IsComplete(config,45),Is.False);
            config.flowNodes[0].limitWindow=true; config.flowNodes[0].endKeyNumber=20;
            Assert.That(ActionFlow.CanExit(config,null,20,2,"Dodge"),Is.True);
            Assert.That(ActionFlow.CanExit(config,null,21,2,"Dodge"),Is.False);
        }
        [Test]
        public void PriorityRulesAndCompleteBoundaryHaveExplicitSemantics()
        {
            var node=new ActionFlowNode { kind=ActionFlowKind.AllowExit,priorityRule=ActionFlowPriority.AtLeast,minimumPriority=Global.CancelPriority.Lv1 };
            config.flowNodes.Add(node);
            Assert.That(ActionFlow.CanExit(config,null,0,1,"Attack"),Is.True);
            node.priorityRule=ActionFlowPriority.Any;
            Assert.That(ActionFlow.CanExit(config,null,0,0,"Movement"),Is.True);
            config.flowNodes.Add(new ActionFlowNode { kind=ActionFlowKind.Complete,keyNumber=12 });
            Assert.That(ActionFlow.CanExit(config,null,12,5,"Movement"),Is.False);
            Assert.That(ActionFlow.IsComplete(config,15),Is.True);
            Assert.That(config.RuntimeEndFrame,Is.EqualTo(12));
        }
        [Test]
        public void MissingConditionsFailClosedAndInvalidNodesAreReported()
        {
            var node=new ActionFlowNode { kind=ActionFlowKind.AllowExit,priorityRule=ActionFlowPriority.Any };
            node.conditions.Add(null); config.flowNodes.Add(node);
            Assert.That(ActionFlow.CanExit(config,null,5,1,"Attack"),Is.False);
            Assert.That(ActionConfigValidator.Validate(config).Exists(x=>x.Code=="ACT174"),Is.True);
            config.flowNodes.Add(new ActionFlowNode {kind=ActionFlowKind.Branch,automatic=true,nextAction=config});
            Assert.That(ActionConfigValidator.Validate(config).Exists(x=>x.Code=="ACT172"),Is.True);
        }
    }
}
