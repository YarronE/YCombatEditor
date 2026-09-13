using AtkJudge;
using System;
using Ethan.ActionEditor;
using Ethan.ActionEditor.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// The original script GUID stays on SkillEditorWindow.cs.
public partial class SkillEditorWindow
{
    #region 属性检查器
    void DrawInspectorPanel(Rect r){
        EditorGUI.DrawRect(r,new Color(.21f,.21f,.21f));
        GUILayout.BeginArea(new Rect(r.x+12,r.y+10,r.width-24,Mathf.Max(1,r.height-20)));
        inspScrollPos=EditorGUILayout.BeginScrollView(inspScrollPos);
        EditorGUIUtility.labelWidth=160;
        DrawValidationSummary();
        switch(selType){
            case SelType.Basic: InspBasic(); break;
            case SelType.Hitbox: InspHitbox(); break;
            case SelType.Enemy: InspEnemy(); break;
            case SelType.Phase2Attack: DrawSerializedSelection("phase2AttackList",selIdx); break;
            case SelType.Phase2Fx: DrawSerializedSelection("phase2FxList",selIdx); break;
            default:{
                // 事件类属性面板统一由轨道注册表分派
                var def=GetDefBySel(selType);
                if(def!=null&&def.inspector!=null){
                    if(def.listCount==null) def.inspector(-1);                  // Move 等无列表轨道
                    else if(selIdx>=0&&selIdx<def.listCount()) def.inspector(selIdx);
                }else{
                    EditorGUILayout.HelpBox("Select a clip to edit its properties. Use Add on a track to insert at the current frame.\n\nPresentation controls what you see and hear. Mechanics controls what the action does. Both share one timeline.\n\nSpace: preview   Left / Right: step   Delete: remove\nCtrl+Z / Y: undo / redo   Ctrl+C / V: copy / paste",MessageType.None);
                }
                break;}
        }
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawValidationSummary(){
        if(!showValidation && _validationErrorCount==0) return;
        if(_validationIssues.Count==0){
            EditorGUILayout.HelpBox("No validation issues.",MessageType.None);
            return;
        }
        EditorGUILayout.LabelField($"Validation: {_validationErrorCount} errors / {_validationWarningCount} warnings",EditorStyles.boldLabel);
        int visible=_validationIssues.Count;
        for(int i=0;i<visible;i++){
            var issue=_validationIssues[i];
            EditorGUILayout.BeginHorizontal();
            var type=issue.Severity==ActionValidationSeverity.Error?MessageType.None:
                issue.Severity==ActionValidationSeverity.Warning?MessageType.None:MessageType.None;
            EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message}",type);
            if(issue.EventIndex>=0&&GUILayout.Button("Locate",GUILayout.Width(54),GUILayout.Height(34))) LocateValidationIssue(issue);
            EditorGUILayout.EndHorizontal();
        }
        if(_validationIssues.Count>visible) EditorGUILayout.LabelField($"{_validationIssues.Count-visible} additional issues",EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
    }

    void LocateValidationIssue(ActionValidationIssue issue){
        selIdx=issue.EventIndex;
        switch(issue.Track){
            case "Camera": selType=SelType.Camera; break;
            case "Animation": selType=SelType.AnimSegment; break;
            case "Attack": selType=SelType.Attack; break;
            case "Phase2Attack": selType=SelType.Phase2Attack; break;
            case "FxAndSound": selType=SelType.Fx; break;
            case "Phase2Fx": selType=SelType.Phase2Fx; break;
            case "HitFx": selType=SelType.HitFx; break;
            case "Jump": selType=SelType.Jump; break;
            case "CancelPoint": selType=SelType.Cancel; break;
            case "Projectile": selType=SelType.Projectile; break;
            case "WarningCue": selType=SelType.Warning; break;
            case "SuperArmor": selType=SelType.SuperArmor; break;
            case "AdjustMotion": selType=SelType.AdjustMotion; break;
            case "Trail": selType=SelType.TrailToggle; break;
            case "Move": selType=SelType.Move; break;
            case "Interaction": selType=SelType.Interaction; break;
            default: selType=SelType.Basic; selIdx=-1; break;
        }
        string listName=BuiltinTrackRegistry.GetValidationListProperty(issue.Track);
        if(listName!=null&&_serializedConfig!=null){
            _serializedConfig.Update();
            var list=_serializedConfig.FindProperty(listName);
            if(list!=null&&selIdx>=0&&selIdx<list.arraySize){
                var item=list.GetArrayElementAtIndex(selIdx);
                var start=item.FindPropertyRelative("keyNumber")??item.FindPropertyRelative("beginKey")??item.FindPropertyRelative("startFrame");
                if(start!=null){
                    frameSelectIndex=Mathf.Max(0,start.intValue);
                    timelineScrollPos.x=Mathf.Max(0,frameSelectIndex*pixelsPerFrame-120f);
                }
            }
        }
        Repaint();
    }

    void InspBasic(){
        if(configFile.UsesExplicitTiming) DrawSerializedFields("timelineFrameRate");
        else {
            EditorGUILayout.HelpBox("Legacy timing uses the first clip FPS. Migration preserves event frame numbers; recheck mixed-rate clips afterward.",MessageType.None);
            if(GUILayout.Button("Migrate to explicit timing") && EditorUtility.DisplayDialog("Migrate timeline","A backup will be created. Mixed-rate clip lengths and overlaps may change; review the timeline afterward.","Back up and migrate","Cancel")) {
                if(ActionTimingMigration.Migrate(configFile,true,out var backup,out var error)) LoadConfig();
                else Debug.LogError(error,configFile);
            }
        }
        DrawSerializedFields("phases");
        EditorGUILayout.LabelField("Action",EditorStyles.boldLabel);
        DrawSerializedFields("skillName","skillDescription","ownerType","exitFrame");
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Mechanics",EditorStyles.boldLabel);
        DrawSerializedFields("skillCD","cancelPriority","energyCostOnActivate");
        showAdvanced=EditorGUILayout.ToggleLeft("Show advanced properties",showAdvanced);
        if(showAdvanced) DrawSerializedFields("skillID","skillType","defaultEnergyGainOnHit","moveCancelFrame","phase2RangeMultiplier","phase2AttackList","phase2FxList","moveCurve","totalMoveDistance");
    }
    void InspHitbox(){
        EditorGUILayout.LabelField("Default hit shape",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Defaults for newly created attack events.",MessageType.None);
        skillShapeSelectIndex=EditorGUILayout.Popup("Shape",skillShapeSelectIndex,skillShapeArray);
        range1=EditorGUILayout.FloatField("Radius / length",range1);
        range2=EditorGUILayout.FloatField("Width",range2);
        var o=EditorGUILayout.Vector3Field("Offset",new Vector3(offsetX,offsetY,offsetZ));
        offsetX=o.x; offsetY=o.y; offsetZ=o.z;
    }
    void InspAnimSeg(Global.AnimClipSegment seg){
        DrawSerializedSelection("animSegments",selIdx);
    }
    /// <summary>获取当前 seg 按 startFrame 升序排列后的索引</summary>
    int GetSortedSegIndex(Global.AnimClipSegment seg){
        int idx=0;
        for(int i=0;i<animSegments.Count;i++){
            if(animSegments[i]==seg) continue;
            if(animSegments[i].startFrame<seg.startFrame) idx++;
        }
        return idx;
    }
    void InspAtk(Global.Attack a){
        DrawSerializedSelection("attackList",selIdx);
    }
    void InspFx(Global.FxAndSound f){
        DrawSerializedSelection("fxList",selIdx);
    }
    void InspSnd(Global.FxAndSound f){
        DrawSerializedSelection("fxList",selIdx);
    }
    void InspHitFx(Global.HitFx hf){
        DrawSerializedSelection("hitFxList",selIdx);
    }
    void InspJump(Global.Jump j){
        EditorGUILayout.HelpBox(string.IsNullOrEmpty(j.triggerCommandId) ? "Legacy key mode. The host can map inputs to semantic commands." : "Command: " + j.triggerCommandId + ". The legacy trigger key is ignored.", MessageType.None);
        string[] options = { "Legacy key / Custom", "Attack", "Shoot", "Special", "Dodge", "Jump" };
        int selected = System.Array.IndexOf(options, j.triggerCommandId);
        int next = EditorGUILayout.Popup("Input command", Mathf.Max(0, selected), options);
        if (next != Mathf.Max(0, selected)) RunAuthoringOperation("Select transition input", () => j.triggerCommandId = next == 0 ? "" : options[next]);
        DrawSerializedSelection("jumpList",selIdx);
    }

    // ── 常用触发按键快捷下拉框 ──────────────────────────────────
    // 根据设计文档 §1.2 输入映射，将项目中常用的按键置顶，其余 KeyCode 折叠在 "Other keys" 子菜单中。
    static readonly (string label, KeyCode key)[] _commonTriggerKeys = {
        ("Left mouse (LMB)",   KeyCode.Mouse0),
        ("Right mouse (RMB)",   KeyCode.Mouse1),
        ("Space",            KeyCode.Space),
        ("E",                KeyCode.E),
        ("LeftShift",        KeyCode.LeftShift),
        ("Middle mouse",          KeyCode.Mouse2),
    };
    KeyCode TriggerKeyPopup(string label, KeyCode current){
        // 构建显示名称列表：常用 + 分隔 + 当前值（若不在常用列表中）
        var names = new List<string>();
        var keys  = new List<KeyCode>();
        int sel = -1;
        for(int i=0;i<_commonTriggerKeys.Length;i++){
            names.Add(_commonTriggerKeys[i].label);
            keys.Add(_commonTriggerKeys[i].key);
            if(_commonTriggerKeys[i].key==current) sel=i;
        }
        // 如果当前值不在常用列表中，追加一个条目显示它
        if(sel<0){
            names.Add($"Current: {current}");
            keys.Add(current);
            sel=names.Count-1;
        }
        // 追加分隔线和 "Other..." 选项
        names.Add(""); // 分隔线占位（EditorGUI.Popup 不支持真分隔线，用空串）
        keys.Add(current); // 占位，选中时保持不变
        int otherIdx=names.Count;
        names.Add("Other keys...");
        keys.Add(current);

        int newSel=EditorGUILayout.Popup(label,sel,names.ToArray());
        if(newSel==otherIdx){
            // 弹出完整 KeyCode 枚举选择
            return (KeyCode)EditorGUILayout.EnumPopup(" ",current);
        }
        if(newSel>=0&&newSel<keys.Count) return keys[newSel];
        return current;
    }

    void InspWarn(Global.WarningCue c){
        DrawSerializedSelection("warningCueList",selIdx);
    }
    void InspCancel(Global.CancelPoint cp){
        DrawSerializedSelection("cancelList",selIdx);
    }
    void InspProj(Global.Projectile p){
        DrawSerializedSelection("projectileList",selIdx);
    }
    static string MoveSegLabel(Global.MoveSegment m,int b,int e){
        string mode=m.driveMode==Global.MoveDriveMode.Animation?"Animation":"Program";
        string dir="";
        if(m.driveMode==Global.MoveDriveMode.Program){
            dir=m.space switch{
                Global.MoveSpace.Forward=>"Forward",Global.MoveSpace.TowardTarget=>"Toward target",
                Global.MoveSpace.AwayFromTarget=>"Away from target",Global.MoveSpace.WorldDirection=>"World direction",
                Global.MoveSpace.LocalDirection=>"Local direction",Global.MoveSpace.InputDirection=>"Input direction",_=>""};
            if(m.origin==Global.MoveOrigin.Target) dir+=" / converge";
            dir=$" {dir} {m.maxDistance:F1}m";
        }else{
            dir=$" ×{m.rootMotionScale:F1}";
        }
        return $"{b}-{e} [{mode}]{dir}";
    }

    void InspMove(){
        DrawSerializedFields("moveCurve","totalMoveDistance");
    }

    void InspMoveSegment(int idx){
        DrawSerializedSelection("moveSegmentList",selIdx);
    }

    bool AnyAnimationOverlap(Global.MoveSegment seg){
        if(seg.driveMode==Global.MoveDriveMode.Animation) return false;
        int b=seg.keyNumber,e=SegEnd(seg.keyNumber,seg.endKeyNumber);
        foreach(var o in moveSegmentList){
            if(ReferenceEquals(o,seg)) continue;
            if(o.driveMode!=Global.MoveDriveMode.Animation) continue;
            int ob=o.keyNumber,oe=SegEnd(o.keyNumber,o.endKeyNumber);
            if(b<=oe&&ob<=e) return true;
        }
        return false;
    }
    bool DependsOnTarget(Global.MoveSegment seg){
        return seg.space==Global.MoveSpace.TowardTarget||seg.space==Global.MoveSpace.AwayFromTarget;
    }

    void InspEnemy(){
        DrawSerializedFields("ownerType","isSuperArmorSkill","phase2RangeMultiplier");
    }
    void InspSuperArmor(Global.SuperArmorSegment sa){
        DrawSerializedSelection("superArmorList",selIdx);
    }
    void InspAdjustMotion(Global.AdjustMotionSegment am){
        DrawSerializedSelection("adjustMotionList",selIdx);
    }
    void InspTrail(Global.TrailToggle tt){
        DrawSerializedSelection("trailToggleList",selIdx);
    }
    static string GetRelativePath(Transform root,Transform child){
        if(child==root) return "";
        var parts=new System.Collections.Generic.List<string>();
        Transform cur=child;
        while(cur!=null&&cur!=root){ parts.Add(cur.name); cur=cur.parent; }
        parts.Reverse();
        return string.Join("/",parts);
    }

    #endregion
}
