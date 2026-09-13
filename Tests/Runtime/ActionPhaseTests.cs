using NUnit.Framework;
using UnityEngine;
namespace Ethan.ActionEditor.Tests
{
    public sealed class ActionPhaseTests
    {
        [Test] public void InvalidPhaseRangeIsAnError()
        {
            var config=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {config.phases.Add(new ActionPhaseMarker{id="Active",beginFrame=10,endFrame=3});Assert.That(ActionConfigValidator.Validate(config).Exists(i=>i.Code=="ACT153"),Is.True);}
            finally{Object.DestroyImmediate(config);}
        }
        [Test] public void PhaseMetadataDoesNotExtendRuntimeDuration()
        {
            var config=ScriptableObject.CreateInstance<SkillConfigSO>();
            try {config.InitializeExplicitTiming();config.exitFrame=20;config.phases.Add(new ActionPhaseMarker{id="Recovery",beginFrame=0,endFrame=100});Assert.That(config.RuntimeEndFrame,Is.EqualTo(20));}
            finally{Object.DestroyImmediate(config);}
        }
        [Test] public void EarlyAndLateQueriesRemainSideEffectFree()
        {
            var go=new GameObject("actor");
            try
            {
                var runtime=new InteractionWindowRuntime();var windows=new[]{new InteractionWindow{keyNumber=3,endKeyNumber=5,maxActivations=1}};var query=new InteractionQuery(go,go.transform,InteractionQuery.IncomingHit);
                Assert.That(runtime.Resolve(windows,2,query,out _,out var before),Is.False);Assert.That(before,Does.Contain("Before"));
                Assert.That(runtime.Resolve(windows,6,query,out _,out var after),Is.False);Assert.That(after,Does.Contain("After"));
                Assert.That(runtime.Resolve(windows,3,query,out _,out _),Is.True);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
