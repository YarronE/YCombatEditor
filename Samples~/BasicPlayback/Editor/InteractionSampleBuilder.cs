using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ethan.ActionEditor.Samples
{
    public static class InteractionSampleBuilder
    {
        [MenuItem("Tools/ACT Action Editor/Samples/Create Interaction Playground")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/ACTInteractionPlayground");
            AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var clip=new AnimationClip { name="Pulse",frameRate=60 };
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Transform),"m_LocalPosition.y"),new AnimationCurve(new Keyframe(0,0),new Keyframe(1,1),new Keyframe(2,0)));
            AssetDatabase.CreateAsset(clip,folder+"/Pulse.anim");
            var distance=ScriptableObject.CreateInstance<PlaygroundDistanceCondition>();
            AssetDatabase.CreateAsset(distance,folder+"/Distance.asset");
            var group=ScriptableObject.CreateInstance<InteractionConditionGroup>(); group.children.Add(distance);
            AssetDatabase.CreateAsset(group,folder+"/Conditions.asset");
            var config=ScriptableObject.CreateInstance<SkillConfigSO>(); config.InitializeExplicitTiming(); config.SetActionEditorSchemaVersion(1);
            config.skillName="Interaction Playground"; config.animSegments.Add(new Global.AnimClipSegment { clip=clip });
            config.interactionWindows.Add(new InteractionWindow { id="evade",keyNumber=5,endKeyNumber=14,response=InteractionResponse.Evade });
            config.interactionWindows.Add(new InteractionWindow { id="parry",keyNumber=15,endKeyNumber=29,response=InteractionResponse.Parry,priority=20,maxActivations=1 });
            config.interactionWindows.Add(new InteractionWindow { id="custom-pulse",signal="training-pulse",keyNumber=30,endKeyNumber=50,maxActivations=1,conditions=new List<InteractionCondition>{group} });
            AssetDatabase.CreateAsset(config,folder+"/Action.asset");
            var actor=GameObject.CreatePrimitive(PrimitiveType.Cube); actor.name="ActionActor";
            var source=GameObject.CreatePrimitive(PrimitiveType.Sphere); source.name="QuerySource"; source.transform.position=Vector3.forward;
            var demo=actor.AddComponent<InteractionPlayground>(); demo.action=config; demo.source=source.transform;
            var camera=new GameObject("Camera").AddComponent<Camera>(); camera.transform.position=new Vector3(4,3,-7); camera.transform.LookAt(Vector3.up);
            var light=new GameObject("Light").AddComponent<Light>(); light.type=LightType.Directional; light.transform.rotation=Quaternion.Euler(40,-30,0);
            EditorSceneManager.SaveScene(scene,folder+"/InteractionPlayground.unity"); AssetDatabase.SaveAssets(); Selection.activeObject=config;
        }
    }
}
