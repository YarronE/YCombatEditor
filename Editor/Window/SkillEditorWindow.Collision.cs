using Ethan.ActionEditor;
using UnityEditor;
using UnityEngine;

public partial class SkillEditorWindow
{
    enum CollisionPage { Shape, Response, Feedback }
    enum CollisionHandleMode { Move, Resize, Rotate }
    CollisionPage collisionPage;
    CollisionHandleMode collisionHandleMode = CollisionHandleMode.Resize;
    int collisionFeedbackPage;
    bool editCollisionInScene = true;

    bool TryGetBonePath(Transform bone, out string path)
    {
        path = null;
        if (bone == null || previewModel == null) return false;
        var root = previewModel.transform;
        if (bone != root && !bone.IsChildOf(root))
        {
            var effective = GetEffectivePreviewModel();
            if (effective == null) return false;
            root = effective.transform;
            if (bone != root && !bone.IsChildOf(root)) return false;
        }
        path = AnimationUtility.CalculateTransformPath(bone, root);
        return true;
    }

    void BindCollisionBone(Global.Attack collision, Transform bone)
    {
        if (collision == null || !TryGetBonePath(bone, out var path)) return;
        RunAuthoringOperation("Bind Action Bone", () => {
            _trackBoneRef = bone;
            collision.trackBonePath = path;
        });
    }

    [MenuItem("GameObject/ACT Action Editor/Copy Bone Path", false, 49)]
    static void CopySelectedBonePath(MenuCommand command)
    {
        var selected = command.context as GameObject;
        var bone = selected != null ? selected.transform : Selection.activeTransform;
        string result = null;
        foreach (var editor in Resources.FindObjectsOfTypeAll<SkillEditorWindow>())
        {
            if (!editor.TryGetBonePath(bone, out var path)) continue;
            if (result != null && result != path)
            {
                EditorUtility.DisplayDialog("Copy Bone Path", "Multiple action editors use different Preview actor roots. Copy the path from the intended editor's Tracking bone controls.", "OK");
                return;
            }
            result = path;
        }
        if (result == null)
        {
            EditorUtility.DisplayDialog("Copy Bone Path", "Open ACT Action Editor, assign Preview actor, then select a bone inside that actor's hierarchy.", "OK");
            return;
        }
        EditorGUIUtility.systemCopyBuffer = result;
    }

    void ShowCollisionAddMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Box"), false, () => AddCollision(1));
        menu.AddItem(new GUIContent("Sphere"), false, () => AddCollision(0));
        menu.ShowAsContext();
    }

    void AddCollision(int shape)
    {
        PushUndo();
        atkList.Add(new Global.Attack { keyNumber = frameSelectIndex, endKeyNumber = frameSelectIndex + 5,
            isReWrite = true, shapeType = shape, parameter1 = 1f, parameter2 = 1f,
            boxHeight = 2f, offset = new Vector3(0, 1, 1), autoAim = false,
            collisionName = shape == 1 ? "Box" : "Sphere" });
        selType = SelType.Attack; selIdx = atkList.Count - 1; collisionPage = CollisionPage.Shape;
        SceneView.RepaintAll();
    }

    Global.Attack SelectedCollision => configFile == null || selIdx < 0 ? null :
        selType == SelType.Attack && selIdx < atkList.Count ? atkList[selIdx] :
        selType == SelType.Phase2Attack && selIdx < configFile.phase2AttackList.Count ? configFile.phase2AttackList[selIdx] : null;

    void CollisionFields(SerializedProperty item, params string[] names)
    {
        foreach (var name in names) { var p = item.FindPropertyRelative(name); if (p != null) DrawAuthoringProperty(p); }
    }

    void DrawCollisionInspector(string listName, int index)
    {
        if (_serializedConfig == null) return;
        _serializedConfig.Update();
        var list = _serializedConfig.FindProperty(listName);
        if (list == null || index < 0 || index >= list.arraySize) return;
        var item = list.GetArrayElementAtIndex(index);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
        bool remove = GUILayout.Button("Remove", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        if (remove) { list.DeleteArrayElementAtIndex(index); ApplyInspectorProperties(); selType = SelType.None; selIdx = -1; return; }
        CollisionFields(item, "collisionName", "keyNumber", "endKeyNumber");
        collisionPage = (CollisionPage)GUILayout.Toolbar((int)collisionPage, new[] { "Shape", "Response", "Feedback" });
        EditorGUILayout.Space(6);
        switch (collisionPage)
        {
            case CollisionPage.Shape:
                var shape = item.FindPropertyRelative("shapeType");
                EditorGUI.BeginChangeCheck();
                int nextShape = EditorGUILayout.Popup("Shape", shape.intValue, new[] { "Sphere", "Box" });
                if (EditorGUI.EndChangeCheck()) shape.intValue = nextShape;
                if (shape.intValue == 0) {
                    var radius = item.FindPropertyRelative("parameter1");
                    EditorGUI.BeginChangeCheck(); float value = EditorGUILayout.FloatField("Radius", radius.floatValue);
                    if (EditorGUI.EndChangeCheck()) radius.floatValue = value;
                } else {
                    var x = item.FindPropertyRelative("parameter1"); var y = item.FindPropertyRelative("boxHeight"); var z = item.FindPropertyRelative("parameter2");
                    EditorGUI.BeginChangeCheck(); var size = EditorGUILayout.Vector3Field("Size", new Vector3(x.floatValue, y.floatValue, z.floatValue));
                    if (EditorGUI.EndChangeCheck()) { x.floatValue = size.x; y.floatValue = size.y; z.floatValue = size.z; }
                    CollisionFields(item, "collisionRotation");
                }
                CollisionFields(item, "useBoneTracking");
                if (item.FindPropertyRelative("useBoneTracking").boolValue) CollisionFields(item, "trackBonePath", "boneOffsets");
                else CollisionFields(item, "offset");
                EditorGUILayout.Space(6);
                editCollisionInScene = EditorGUILayout.ToggleLeft("Edit in Scene", editCollisionInScene);
                collisionHandleMode = (CollisionHandleMode)GUILayout.Toolbar((int)collisionHandleMode, new[] { "Move", "Resize", "Rotate" });
                if (GUILayout.Button("Frame collision")) FrameSelectedCollision();
                if (GUILayout.Button("Go to start frame")) { frameSelectIndex = item.FindPropertyRelative("keyNumber").intValue; SceneView.RepaintAll(); }
                if (previewModel == null) EditorGUILayout.HelpBox("Assign a Preview actor to position the volume on your model. Without one, the volume is shown at the world origin.", MessageType.None);
                if (item.FindPropertyRelative("useBoneTracking").boolValue)
                    EditorGUILayout.HelpBox("Move edits the current sampled offset inside the collision window. Resize and Rotate affect the whole volume.", MessageType.None);
                break;
            case CollisionPage.Response:
                CollisionFields(item, "responseMode", "damageMode");
                if (item.FindPropertyRelative("damageMode").enumValueIndex != 0) CollisionFields(item, "tickInterval");
                if (item.FindPropertyRelative("responseMode").enumValueIndex == (int)Global.CollisionResponseMode.Signal) {
                    CollisionFields(item, "contactSignal");
                    EditorGUILayout.HelpBox("The host sends confirmed contacts to IActionCollisionReceiver. Signal responses do not apply damage.", MessageType.None);
                } else {
                    CollisionFields(item, "damageRatio", "knockbackForce", "impactLevel", "toughnessDamage", "unblockable", "unparryable", "interactionTag");
                    showAdvanced = EditorGUILayout.ToggleLeft("Show advanced properties", showAdvanced);
                    if (showAdvanced) CollisionFields(item, "autoAim", "energyDeltaOnHit", "enemyAttackType", "requestSlowPromptOnHit", "slowPromptRequiredEnergy", "requestGlowPromptOnHit");
                }
                break;
            case CollisionPage.Feedback:
                bool signalOnly = item.FindPropertyRelative("responseMode").enumValueIndex == (int)Global.CollisionResponseMode.Signal;
                collisionFeedbackPage = GUILayout.Toolbar(Mathf.Clamp(collisionFeedbackPage, 0, signalOnly ? 1 : 2), signalOnly ? new[] { "Visual", "Audio" } : new[] { "Visual", "Audio", "Impact" });
                if (collisionFeedbackPage == 0) {
                    CollisionFields(item, "hitVfx");
                    if (item.FindPropertyRelative("hitVfx").objectReferenceValue != null)
                        CollisionFields(item, "hitVfxOffset", "hitVfxRotation", "hitVfxScale", "hitVfxPlaybackSpeed", "hitVfxFollowTarget", "hitVfxUseAttackerRotation");
                } else if (collisionFeedbackPage == 1) {
                    CollisionFields(item, "hitSound");
                    if (item.FindPropertyRelative("hitSound").objectReferenceValue != null) {
                        CollisionFields(item, "hitSoundVolume", "hitSoundRandomize");
                        if (item.FindPropertyRelative("hitSoundRandomize").boolValue) CollisionFields(item, "hitSoundPitchVariation", "hitSoundVolumeVariation");
                    }
                } else {
                    CollisionFields(item, "enableHitPause"); if (item.FindPropertyRelative("enableHitPause").boolValue) CollisionFields(item, "hitPauseDuration");
                    CollisionFields(item, "enableCameraShake"); if (item.FindPropertyRelative("enableCameraShake").boolValue) CollisionFields(item, "cameraShakeDuration", "cameraShakeAmplitude", "cameraShakeFrequency");
                    CollisionFields(item, "enableChromaticAberration"); if (item.FindPropertyRelative("enableChromaticAberration").boolValue) CollisionFields(item, "chromaticAberrationDuration", "chromaticAberrationIntensity");
                }
                EditorGUILayout.HelpBox("Feedback runs on a confirmed contact or accepted damage result, through the host receiver.", MessageType.None);
                break;
        }
        ApplyInspectorProperties();
        if (collisionPage == CollisionPage.Shape && SelectedCollision?.useBoneTracking == true) DrawSelectedAuthoringTools(listName, index);
    }

    void FrameSelectedCollision()
    {
        var collision = SelectedCollision; if (collision == null) return;
        var actor = GetEffectivePreviewModel();
        var center = ActionCollisionGeometry.Center(collision, actor != null ? actor.transform : null, frameSelectIndex);
        float size = collision.shapeType == 0 ? collision.parameter1 * 2 : ActionCollisionGeometry.BoxSize(collision).magnitude;
        var scene = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
        scene.Frame(new Bounds(center, Vector3.one * Mathf.Max(size, 1f)), false);
        scene.Repaint();
    }
}
