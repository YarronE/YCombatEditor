using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public class BasicSampleTests
{
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ImportedSample_GeneratesUniqueAssets_AndPlays()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("Ethan.ActionEditor.Samples.BasicSampleBuilder"))
            .FirstOrDefault(t => t != null);
        if (type == null) Assert.Ignore("Import Basic Playback before running sample acceptance.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                Assert.Ignore("Save modified scenes before running sample acceptance.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        type.GetMethod("Create").Invoke(null, null);
        string first = System.IO.Path.GetDirectoryName(SceneManager.GetActiveScene().path).Replace('\\', '/') + "/BasicAction.asset";
        type.GetMethod("Create").Invoke(null, null);
        string second = System.IO.Path.GetDirectoryName(SceneManager.GetActiveScene().path).Replace('\\', '/') + "/BasicAction.asset";
        Assert.AreNotEqual(first, second);
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(first));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SkillConfigSO>(second));
        var launcher = GameObject.Find("ActionActor").GetComponents<MonoBehaviour>()
            .First(component => component.GetType().Name == "BasicActionLauncher");
        Assert.IsNotNull(new SerializedObject(launcher).FindProperty("action").objectReferenceValue,
            "Generated scene must retain its action reference across scene creation.");
        Assert.DoesNotThrow(() => launcher.GetType().GetMethod("Play").Invoke(launcher, null));
        Assert.DoesNotThrow(() => launcher.GetType().GetMethod("Stop").Invoke(launcher, null));
        Assert.IsTrue(new SerializedObject(launcher).FindProperty("requireHandlers").boolValue);
        // Generated files remain for inspection; do not delete arbitrary consumer assets.
        yield return new EnterPlayMode();
        var actor = GameObject.Find("ActionActor");
        Assert.IsNotNull(actor);
        float maximum = 0;
        float until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until)
        {
            maximum = Mathf.Max(maximum, actor.transform.position.y);
            yield return null;
        }
        Assert.Greater(maximum, 0.25f, "Animation must visibly move the generated cube.");
        Assert.IsFalse(actor.GetComponent<Ethan.ActionEditor.ActionPlayer>().IsPlaying);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ImportedInteractionSample_ResolvesRealWindowsAndCustomConditions()
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Ethan.ActionEditor.Samples.InteractionSampleBuilder")).FirstOrDefault(t=>t!=null);
        Assert.IsNotNull(type,"Import the sample before release acceptance.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); type.GetMethod("Create").Invoke(null,null);
        yield return new EnterPlayMode();
        var actor=GameObject.Find("ActionActor"); var core=actor.GetComponent<Ethan.ActionEditor.ActionPlayer>(); core.AutoAdvance=false;
        var demo=actor.GetComponents<MonoBehaviour>().First(c=>c.GetType().Name=="InteractionPlayground");
        Assert.IsTrue((bool)demo.GetType().GetMethod("StartAction").Invoke(demo,null));
        core.Advance(.2f);
        Assert.IsTrue((bool)demo.GetType().GetMethod("SendSignal").Invoke(demo,new object[]{"incoming-hit"}));
        core.Advance(.8f);
        Assert.IsTrue((bool)demo.GetType().GetMethod("SendSignal").Invoke(demo,new object[]{"training-pulse"}));
        Assert.IsFalse((bool)demo.GetType().GetMethod("SendSignal").Invoke(demo,new object[]{"training-pulse"}),"Committed custom response consumes its activation.");
        Assert.IsTrue(core.LastInteractionEvaluation.Count>0);
        yield return new ExitPlayMode();
    }
}
