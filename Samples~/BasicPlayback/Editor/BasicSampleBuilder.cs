using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ethan.ActionEditor.Samples
{
    public static class BasicSampleBuilder
    {
        [MenuItem("Tools/ACT Action Editor/Samples/Create Basic Playback")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Switching scenes can unload unreferenced assets. Create assets only after the switch.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/ACTBasicPlayback");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var clip = new AnimationClip { name = "Bounce", frameRate = 30 };
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"),
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 2), new Keyframe(1, 0)));
            AssetDatabase.CreateAsset(clip, folder + "/Bounce.anim");
            var config = ScriptableObject.CreateInstance<SkillConfigSO>();
            config.name = "BasicAction";
            config.skillName = "Basic bounce";
            config.SetActionEditorSchemaVersion(1);
            config.InitializeExplicitTiming();
            config.exitFrame = 30;
            config.cameraCues.Add(new ActionCameraCue { keyNumber=5,endKeyNumber=25,fieldOfViewOffset=-6,shakeAmplitude=.025f });
            config.adjustMotionList.Add(new Global.AdjustMotionSegment { keyNumber=0,endKeyNumber=30,allowTurning=false });
            config.attackList.Add(new Global.Attack { keyNumber = 15, endKeyNumber = 15 });
            config.animSegments.Add(new Global.AnimClipSegment { clip = clip });
            AssetDatabase.CreateAsset(config, folder + "/BasicAction.asset");
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            actor.name = "ActionActor";
            actor.AddComponent<BasicActionReceiver>();
            var launcher = actor.AddComponent<BasicActionLauncher>();
            actor.AddComponent<ActionHitStop>();
            actor.AddComponent<ActionFacingDriver>();
            var serialized = new SerializedObject(launcher);
            serialized.FindProperty("action").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            var pivot = new GameObject("CameraShakePivot");
            var shake = pivot.AddComponent<ActionCameraShake>();
            serialized.Update();
            serialized.FindProperty("cameraShake").objectReferenceValue = shake;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var cuePivot=new GameObject("CameraCuePivot");
            cuePivot.transform.SetParent(pivot.transform,false);
            var cameraDriver=actor.AddComponent<ActionCameraDriver>();
            cameraDriver.cameraPivot=cuePivot.transform;
            cameraDriver.targetCamera=camera;
            camera.transform.SetParent(cuePivot.transform, false);
            camera.transform.position = new Vector3(4, 3, -7);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            EditorSceneManager.SaveScene(scene, folder + "/BasicPlayback.unity");
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("Sample created at " + folder + ". Press Play: the cube rises and returns in one second.");
        }
    }
}
