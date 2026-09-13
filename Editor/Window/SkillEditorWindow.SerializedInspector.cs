using System;
using Ethan.ActionEditor.Editor;
using UnityEditor;
using UnityEngine;

public partial class SkillEditorWindow
{
    readonly System.Collections.Generic.Dictionary<string, Global.EffectContentKind> legacyEffectViews =
        new System.Collections.Generic.Dictionary<string, Global.EffectContentKind>();

    Global.EffectContentKind EffectView(string listName, int index)
    {
        var effect = (listName == "fxList" ? configFile.fxList : configFile.phase2FxList)[index];
        string key = configFile.GetInstanceID() + ":" + listName + ":" + index;
        if (effect.contentKind == Global.EffectContentKind.Legacy && legacyEffectViews.TryGetValue(key, out var view)) return view;
        return EffectAuthoring.Resolve(effect);
    }

    void DrawReadOnlyConfig()
    {
        if (_serializedConfig == null) return;
        inspScrollPos = EditorGUILayout.BeginScrollView(inspScrollPos);
        _serializedConfig.Update();
        int previousIndent = EditorGUI.indentLevel;
        var property = _serializedConfig.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            EditorGUI.indentLevel = property.depth;
            bool container = property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren;
            if (container)
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);
            else
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property, false);
            enterChildren = container && property.isExpanded;
        }
        EditorGUI.indentLevel = previousIndent;
        // Discard any property-drawer side effects; legacy assets are never applied here.
        _serializedConfig.Update();
        EditorGUILayout.EndScrollView();
    }

    void DrawSerializedFields(params string[] fields)
    {
        if (_serializedConfig == null) return;
        _serializedConfig.Update();
        foreach (var name in fields)
        {
            var property = _serializedConfig.FindProperty(name);
            if (property != null) DrawAuthoringProperty(property);
        }
        ApplyInspectorProperties();
    }

    void DrawSerializedSelection(string listName, int index)
    {
        if (listName == "warningCueList") { DrawTelegraphInspector(index); return; }
        if (listName == "flowNodes") { DrawFlowInspector(index); return; }
        if (listName == "jumpList" || listName == "cancelList") { DrawLegacyFlowInspector(listName,index); return; }
        if (listName == "attackList" || listName == "phase2AttackList") { DrawCollisionInspector(listName, index); return; }
        if (_serializedConfig == null) return;
        _serializedConfig.Update();
        var list = _serializedConfig.FindProperty(listName);
        if (list == null || index < 0 || index >= list.arraySize)
        {
            if (listName == "animSegments") DrawSerializedFields("skillClip");
            else EditorGUILayout.HelpBox("Select an event first.", MessageType.None);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{SelectionTitle(listName)} {index+1}", EditorStyles.boldLabel);
        bool remove = GUILayout.Button(new GUIContent("Remove", "Remove selected event (Undo supported)"), GUILayout.Width(52));
        EditorGUILayout.EndHorizontal();
        if (remove)
        {
            list.DeleteArrayElementAtIndex(index);
            ApplyInspectorProperties();
            selIdx = -1;
            selType = SelType.None;
            return;
        }

        showAdvanced=EditorGUILayout.ToggleLeft("Show advanced properties",showAdvanced);
        if(listName=="adjustMotionList") EditorGUILayout.HelpBox("Allow tracking while anticipating a strike. Lock facing while feet are planted. Locks win overlaps; gaps hold facing when windows are authored.",MessageType.None);
        if(listName=="cameraCues") EditorGUILayout.HelpBox("Played by an ActionCameraCue receiver. The bundled ActionCameraDriver needs a dedicated camera pivot or lens. Camera playback is not simulated in Scene preview.",MessageType.None);
        bool isEffect = listName == "fxList" || listName == "phase2FxList";
        var effectView = isEffect ? EffectView(listName, index) : Global.EffectContentKind.Legacy;
        var item = list.GetArrayElementAtIndex(index);
        if (isEffect)
        {
            bool legacy = item.FindPropertyRelative("contentKind").enumValueIndex == 0;
            if (legacy)
            {
                int tab = GUILayout.Toolbar(effectView == Global.EffectContentKind.Audio ? 1 : 0, new[] { "Visual Effect", "Audio" });
                effectView = tab == 1 ? Global.EffectContentKind.Audio : Global.EffectContentKind.VisualEffect;
                legacyEffectViews[configFile.GetInstanceID() + ":" + listName + ":" + index] = effectView;
                EditorGUILayout.HelpBox("Legacy event: tabs only change the property view. Existing visual and audio references are preserved and both can play.", MessageType.None);
            }
            else EditorGUILayout.LabelField(effectView == Global.EffectContentKind.Audio ? "Audio" : "Visual Effect", EditorStyles.boldLabel);
        }
        var end = item.GetEndProperty();
        var child = item.Copy();
        if (child.NextVisible(true))
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                if (isEffect ? EffectAuthoring.IsFieldVisible(effectView, child.name, showAdvanced) :
                    showAdvanced || IsEssentialField(listName, child.name)) DrawAuthoringProperty(child);
            } while (child.NextVisible(false));
        ApplyInspectorProperties();
        if(showAdvanced || listName=="fxList" || listName=="phase2FxList") DrawSelectedAuthoringTools(listName, index);
    }

    bool ApplyInspectorProperties()
    {
        if (_serializedConfig == null || configFile == null) return false;
        if (ActionConfigMigrationService.NeedsMigration(configFile))
        {
            _serializedConfig.Update();
            return false;
        }
        // ApplyModifiedProperties owns the native Undo record. Navigation and
        // foldout changes do not commit cached fields or mark the asset dirty.
        if (!_serializedConfig.ApplyModifiedProperties()) return false;
        ActionEditorTransaction.MarkChanged(configFile);
        RefreshConfigBindings();
        _totalFramesCache = -1;
        RefreshValidation();
        Repaint();
        SceneView.RepaintAll();
        return true;
    }

    void DrawAuthoringProperty(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.Generic && property.type == nameof(Global.Attack))
        {
            var match = System.Text.RegularExpressions.Regex.Match(property.propertyPath, @"^(attackList|phase2AttackList)\.Array\.data\[(\d+)\]$");
            if (match.Success) {
                if (GUILayout.Button("Edit collision " + (int.Parse(match.Groups[2].Value) + 1))) {
                    selType = match.Groups[1].Value == "attackList" ? SelType.Attack : SelType.Phase2Attack;
                    selIdx = int.Parse(match.Groups[2].Value); collisionPage = CollisionPage.Shape;
                }
                return;
            }
        }
        var label = new GUIContent(FieldLabel(property.name,property.displayName), property.tooltip);
        // Range/Min property drawers can normalize stored values during drawing.
        // Authoring keeps numeric data verbatim; validation reports invalid values.
        if (property.propertyType == SerializedPropertyType.Float)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(label, property.floatValue);
            if (EditorGUI.EndChangeCheck()) property.floatValue = value;
            return;
        }
        if (property.propertyType == SerializedPropertyType.Integer)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.IntField(label, property.intValue);
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            return;
        }
        if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, label, true);
            if (!property.isExpanded) return;
            if (property.isArray)
            {
                EditorGUI.BeginChangeCheck();
                int size = EditorGUILayout.IntField("Count", property.arraySize);
                if (EditorGUI.EndChangeCheck())
                {
                    property.arraySize = Mathf.Max(0, size);
                    return; // Do not traverse old element handles after resizing.
                }
                EditorGUI.indentLevel++;
                for (int i = 0; i < property.arraySize; i++)
                    DrawAuthoringProperty(property.GetArrayElementAtIndex(i));
                EditorGUI.indentLevel--;
                return;
            }
            bool isEffect = property.type == nameof(Global.FxAndSound);
            var kind = Global.EffectContentKind.Legacy;
            if (isEffect)
            {
                kind = (Global.EffectContentKind)property.FindPropertyRelative("contentKind").intValue;
                if (kind == Global.EffectContentKind.Legacy)
                {
                    string viewKey = configFile.GetInstanceID() + ":" + property.propertyPath;
                    if (!legacyEffectViews.TryGetValue(viewKey, out kind))
                        kind = property.FindPropertyRelative("audioClip").objectReferenceValue != null && property.FindPropertyRelative("particleSystem").objectReferenceValue == null
                            ? Global.EffectContentKind.Audio : Global.EffectContentKind.VisualEffect;
                    kind = GUILayout.Toolbar(kind == Global.EffectContentKind.Audio ? 1 : 0, new[] { "Visual Effect", "Audio" }) == 1
                        ? Global.EffectContentKind.Audio : Global.EffectContentKind.VisualEffect;
                    legacyEffectViews[viewKey] = kind;
                    EditorGUILayout.HelpBox("Legacy event: tabs preserve both references. Both can play.", MessageType.None);
                }
                else EditorGUILayout.LabelField(kind == Global.EffectContentKind.Audio ? "Audio" : "Visual Effect");
            }
            var end = property.GetEndProperty();
            var child = property.Copy();
            EditorGUI.indentLevel++;
            if (child.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    if (!isEffect || EffectAuthoring.IsFieldVisible(kind, child.name, showAdvanced)) DrawAuthoringProperty(child);
                } while (child.NextVisible(false));
            EditorGUI.indentLevel--;
            return;
        }
        EditorGUILayout.PropertyField(property, label, false);
    }

    void RunAuthoringOperation(string name, Action operation)
    {
        if (configFile == null || ActionConfigMigrationService.NeedsMigration(configFile)) return;
        Undo.RecordObject(configFile, name);
        operation();
        ActionEditorTransaction.MarkChanged(configFile);
        _serializedConfig?.Update();
        RefreshConfigBindings();
        RefreshValidation();
        SceneView.RepaintAll();
    }

    void DrawSelectedAuthoringTools(string listName, int index)
    {
        EditorGUILayout.Space();
        if (listName == "attackList" || listName == "phase2AttackList")
        {
            var attack = (listName == "attackList" ? configFile.attackList : configFile.phase2AttackList)[index];
            _trackBoneRef = EditorGUILayout.ObjectField("Tracking bone", _trackBoneRef, typeof(Transform), true) as Transform;
            using (new EditorGUI.DisabledScope(!TryGetBonePath(Selection.activeTransform, out _)))
            {
                if (GUILayout.Button("Use selected bone")) BindCollisionBone(attack, Selection.activeTransform);
            }
            using (new EditorGUI.DisabledScope(!TryGetBonePath(_trackBoneRef, out _)))
            {
                if (GUILayout.Button("Bind bone path")) BindCollisionBone(attack, _trackBoneRef);
                if (GUILayout.Button("Copy bone path") && TryGetBonePath(_trackBoneRef, out var path)) EditorGUIUtility.systemCopyBuffer = path;
                if (GUILayout.Button("Sample bone path")) RunAuthoringOperation("Sample Action Bone", () => SampleBoneTrackingOffsets(attack));
                if (GUILayout.Button("Update current offset")) RunAuthoringOperation("Update Action Bone Offset", () => UpdateSingleFrameBoneOffset(attack, frameSelectIndex));
            }
            EditorGUILayout.HelpBox("Drag a bone from the Preview actor hierarchy into Tracking bone, or select it in Hierarchy and use Use selected bone. Paths are relative to Preview actor.", MessageType.None);
            if (GUILayout.Button("Clear bone offsets")) RunAuthoringOperation("Clear Action Bone Offsets", () => {
                attack.boneOffsets?.Clear(); attack.useBoneTracking = false;
            });
            if (GUILayout.Button("Focus hit shape")) {
                var root = GetEffectivePreviewModel();
                if (root != null) FocusSceneViewOn(root.transform.TransformPoint(attack.GetOffsetAtFrame(frameSelectIndex)), 2f);
            }
        }
        using (new EditorGUI.DisabledScope(!IsPreviewAllowed))
        {
            if (listName == "fxList" || listName == "phase2FxList")
            {
                var fx = (listName == "fxList" ? configFile.fxList : configFile.phase2FxList)[index];
                var view = EffectView(listName, index);
                if (GUILayout.Button(view == Global.EffectContentKind.Audio ? "Preview audio" : "Preview visual effect")) {
                    CleanPreviewFx();
                    if (as1 != null) as1.Stop();
                    if (view == Global.EffectContentKind.VisualEffect && fx.particleSystem != null) SpawnPreviewFx(fx);
                    if (view == Global.EffectContentKind.Audio && fx.audioClip != null && as1 != null) as1.PlayOneShot(fx.audioClip, fx.baseVolume);
                }
            }
            if (listName == "projectileList" && GUILayout.Button("Preview projectile"))
                SpawnPreviewProjectile(configFile.projectileList[index]);
            if (listName == "jumpList" && GUILayout.Button("Preview transition cue"))
                SpawnPreviewDerivePrompt(configFile.jumpList[index]);
        }
        if (GUILayout.Button("Stop effect preview")) { CleanPreviewFx(); if (as1 != null) as1.Stop(); }
    }
}
