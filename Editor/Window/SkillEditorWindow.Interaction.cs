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
    #region 剪贴板（Clip 复制粘贴，static 支持跨 Config 文件）
    /// <summary>剪贴板数据：保存复制的 Event/Clip 的 JSON + 类型信息</summary>
    static string _clipboardJson;
    static SelType _clipboardType = SelType.None;
    /// <summary>剪贴板中保存的引用对象（UnityEngine.Object 无法 JSON 序列化，需单独保存）</summary>
    static Dictionary<string, UnityEngine.Object> _clipboardRefs = new Dictionary<string, UnityEngine.Object>();

    bool HasClipboard => !string.IsNullOrEmpty(_clipboardJson) && _clipboardType != SelType.None;

    /// <summary>复制当前选中的 Event/Clip 到剪贴板</summary>
    void CopySelected(){
        if(selType==SelType.None||selIdx<0) return;
        _clipboardRefs.Clear();
        _clipboardType=selType;
        switch(selType){
            case SelType.Flow: if(selIdx<flowNodes.Count) { var node=flowNodes[selIdx]; _clipboardJson=JsonUtility.ToJson(node); if(node.nextAction!=null) _clipboardRefs["nextAction"]=node.nextAction; if(node.conditions!=null) for(int i=0;i<node.conditions.Count;i++) _clipboardRefs["flowCondition"+i]=node.conditions[i]; } break;
            case SelType.Camera: if(selIdx<cameraCues.Count) _clipboardJson=JsonUtility.ToJson(cameraCues[selIdx]); break;
            case SelType.Interaction:
                if(selIdx<interactionWindows.Count){
                    var w=interactionWindows[selIdx]; _clipboardJson=JsonUtility.ToJson(w);
                    for(int i=0;i<w.conditions.Count;i++) _clipboardRefs["condition"+i]=w.conditions[i];
                } break;
            case SelType.Attack:
                if(selIdx<atkList.Count){ var a=atkList[selIdx]; SaveAtkRefs(a); _clipboardJson=JsonUtility.ToJson(a); } break;
            case SelType.Fx:
                if(selIdx<fxAndSoundList.Count){ var f=fxAndSoundList[selIdx]; SaveFxRefs(f); _clipboardJson=JsonUtility.ToJson(f); } break;
            case SelType.Sound:
                if(selIdx<fxAndSoundList.Count){ var f=fxAndSoundList[selIdx]; SaveFxRefs(f); _clipboardJson=JsonUtility.ToJson(f); } break;
            case SelType.Jump:
                if(selIdx<jumpList.Count){ var j=jumpList[selIdx]; if(j.nextSkill!=null) _clipboardRefs["nextSkill"]=j.nextSkill; _clipboardJson=JsonUtility.ToJson(j); } break;
            case SelType.Warning:
                if(selIdx<warningCueList.Count){ var w=warningCueList[selIdx]; SaveWarnRefs(w); _clipboardJson=JsonUtility.ToJson(w); } break;
            case SelType.Cancel:
                if(selIdx<cancelList.Count) _clipboardJson=JsonUtility.ToJson(cancelList[selIdx]); break;
            case SelType.Projectile:
                if(selIdx<projectileList.Count){ var p=projectileList[selIdx]; SaveProjRefs(p); _clipboardJson=JsonUtility.ToJson(p); } break;
            case SelType.HitFx:
                if(selIdx<hitFxList.Count){ var h=hitFxList[selIdx]; SaveHitFxRefs(h); _clipboardJson=JsonUtility.ToJson(h); } break;
            case SelType.SuperArmor:
                if(selIdx<superArmorList.Count) _clipboardJson=JsonUtility.ToJson(superArmorList[selIdx]); break;
            case SelType.AdjustMotion:
                if(selIdx<adjustMotionList.Count) _clipboardJson=JsonUtility.ToJson(adjustMotionList[selIdx]); break;
            case SelType.TrailToggle:
                if(selIdx<trailToggleList.Count){ var t=trailToggleList[selIdx]; if(t.particle!=null) _clipboardRefs["particle"]=t.particle; _clipboardJson=JsonUtility.ToJson(t); } break;
            case SelType.AnimSegment:
                if(selIdx<animSegments.Count){ var s=animSegments[selIdx]; if(s.clip!=null) _clipboardRefs["clip"]=s.clip; _clipboardJson=JsonUtility.ToJson(s); } break;
            case SelType.Move:
                if(selIdx<moveSegmentList.Count) _clipboardJson=JsonUtility.ToJson(moveSegmentList[selIdx]); break;
            default: return;
        }
    }
    void SaveAtkRefs(Global.Attack a){
        if(a.hitVfx!=null) _clipboardRefs["hitVfx"]=a.hitVfx;
        if(a.hitSound!=null) _clipboardRefs["hitSound"]=a.hitSound;
    }
    void SaveFxRefs(Global.FxAndSound f){
        if(f.particleSystem!=null) _clipboardRefs["particleSystem"]=f.particleSystem;
        if(f.audioClip!=null) _clipboardRefs["audioClip"]=f.audioClip;
    }
    void SaveWarnRefs(Global.WarningCue w){
        if(w.warningSound!=null) _clipboardRefs["warningSound"]=w.warningSound;
        if(w.warningVfx!=null) _clipboardRefs["warningVfx"]=w.warningVfx;
    }
    void SaveProjRefs(Global.Projectile p){
        if(p.prefab!=null) _clipboardRefs["prefab"]=p.prefab;
        if(p.muzzleVfx!=null) _clipboardRefs["muzzleVfx"]=p.muzzleVfx;
        if(p.fireSound!=null) _clipboardRefs["fireSound"]=p.fireSound;
        if(p.impactVfx!=null) _clipboardRefs["impactVfx"]=p.impactVfx;
        if(p.impactSound!=null) _clipboardRefs["impactSound"]=p.impactSound;
    }
    void SaveHitFxRefs(Global.HitFx h){
        if(h.hitVfx!=null) _clipboardRefs["hitVfx"]=h.hitVfx;
        if(h.hitSound!=null) _clipboardRefs["hitSound"]=h.hitSound;
    }

    /// <summary>粘贴剪贴板中的 Event/Clip 到当前帧位置</summary>
    void PasteClipboard(){
        if(!HasClipboard) return;
        PushUndo();
        int f=frameSelectIndex;
        switch(_clipboardType){
            case SelType.Flow:{ var node=JsonUtility.FromJson<ActionFlowNode>(_clipboardJson); int span=node.endKeyNumber-node.keyNumber; node.keyNumber=f; node.endKeyNumber=f+Mathf.Max(0,span); if(_clipboardRefs.TryGetValue("nextAction",out var target)) node.nextAction=target as SkillConfigSO; if(node.conditions!=null) for(int i=0;i<node.conditions.Count;i++) if(_clipboardRefs.TryGetValue("flowCondition"+i,out var condition)) node.conditions[i]=condition as ActionFlowCondition; flowNodes.Add(node); selType=SelType.Flow; selIdx=flowNodes.Count-1; break; }
            case SelType.Camera:{ var cue=JsonUtility.FromJson<ActionCameraCue>(_clipboardJson); int span=cue.endKeyNumber-cue.keyNumber; cue.keyNumber=f; cue.endKeyNumber=f+Mathf.Max(0,span); cameraCues.Add(cue); selType=SelType.Camera; selIdx=cameraCues.Count-1; break; }
            case SelType.Interaction:{
                var w=JsonUtility.FromJson<InteractionWindow>(_clipboardJson);
                int span=w.endKeyNumber-w.keyNumber; w.keyNumber=f; w.endKeyNumber=f+Mathf.Max(0,span);
                w.id=UniqueWindowId(w.id);
                for(int i=0;i<w.conditions.Count;i++)
                    if(_clipboardRefs.TryGetValue("condition"+i,out var condition)) w.conditions[i]=condition as InteractionCondition;
                interactionWindows.Add(w); selType=SelType.Interaction; selIdx=interactionWindows.Count-1;
                break;}
            case SelType.Attack:{
                var a=JsonUtility.FromJson<Global.Attack>(_clipboardJson);
                int span=a.endKeyNumber-a.keyNumber; a.keyNumber=f; a.endKeyNumber=f+Mathf.Max(span,0);
                RestoreAtkRefs(a); atkList.Add(a); selType=SelType.Attack; selIdx=atkList.Count-1;
                break;}
            case SelType.Fx:{
                var fx=JsonUtility.FromJson<Global.FxAndSound>(_clipboardJson);
                int span=fx.endKeyNumber-fx.keyNumber; fx.keyNumber=f; fx.endKeyNumber=f+Mathf.Max(span,0);
                RestoreFxRefs(fx); fxAndSoundList.Add(fx); selType=SelType.Fx; selIdx=fxAndSoundList.Count-1;
                break;}
            case SelType.Sound:{
                var fx=JsonUtility.FromJson<Global.FxAndSound>(_clipboardJson);
                int span=fx.endKeyNumber-fx.keyNumber; fx.keyNumber=f; fx.endKeyNumber=f+Mathf.Max(span,0);
                RestoreFxRefs(fx); fxAndSoundList.Add(fx); selType=SelType.Sound; selIdx=fxAndSoundList.Count-1;
                break;}
            case SelType.Jump:{
                var j=JsonUtility.FromJson<Global.Jump>(_clipboardJson);
                int span=j.endKey-j.beginKey; j.beginKey=f; j.endKey=f+Mathf.Max(span,0);
                if(_clipboardRefs.ContainsKey("nextSkill")) j.nextSkill=_clipboardRefs["nextSkill"] as SkillConfigSO;
                jumpList.Add(j); selType=SelType.Jump; selIdx=jumpList.Count-1;
                break;}
            case SelType.Warning:{
                var w=JsonUtility.FromJson<Global.WarningCue>(_clipboardJson);
                int span=w.endKeyNumber-w.keyNumber; w.keyNumber=f; w.endKeyNumber=f+Mathf.Max(span,0);
                RestoreWarnRefs(w); warningCueList.Add(w); selType=SelType.Warning; selIdx=warningCueList.Count-1;
                break;}
            case SelType.Cancel:{
                var cp=JsonUtility.FromJson<Global.CancelPoint>(_clipboardJson);
                int span=cp.endKeyNumber-cp.keyNumber; cp.keyNumber=f; cp.endKeyNumber=f+Mathf.Max(span,0);
                cancelList.Add(cp); selType=SelType.Cancel; selIdx=cancelList.Count-1;
                break;}
            case SelType.Projectile:{
                var p=JsonUtility.FromJson<Global.Projectile>(_clipboardJson);
                int span=p.endKeyNumber-p.keyNumber; p.keyNumber=f; p.endKeyNumber=f+Mathf.Max(span,0);
                RestoreProjRefs(p); projectileList.Add(p); selType=SelType.Projectile; selIdx=projectileList.Count-1;
                break;}
            case SelType.HitFx:{
                var h=JsonUtility.FromJson<Global.HitFx>(_clipboardJson);
                RestoreHitFxRefs(h); hitFxList.Add(h); selType=SelType.HitFx; selIdx=hitFxList.Count-1;
                break;}
            case SelType.SuperArmor:{
                var sa=JsonUtility.FromJson<Global.SuperArmorSegment>(_clipboardJson);
                int span=sa.endKeyNumber-sa.keyNumber; sa.keyNumber=f; sa.endKeyNumber=f+Mathf.Max(span,0);
                superArmorList.Add(sa); selType=SelType.SuperArmor; selIdx=superArmorList.Count-1;
                break;}
            case SelType.AdjustMotion:{
                var am=JsonUtility.FromJson<Global.AdjustMotionSegment>(_clipboardJson);
                int span=am.endKeyNumber-am.keyNumber; am.keyNumber=f; am.endKeyNumber=f+Mathf.Max(span,0);
                adjustMotionList.Add(am); selType=SelType.AdjustMotion; selIdx=adjustMotionList.Count-1;
                break;}
            case SelType.TrailToggle:{
                var tt=JsonUtility.FromJson<Global.TrailToggle>(_clipboardJson);
                int span=tt.endKeyNumber-tt.keyNumber; tt.keyNumber=f; tt.endKeyNumber=f+Mathf.Max(span,0);
                if(_clipboardRefs.ContainsKey("particle")) tt.particle=_clipboardRefs["particle"] as ParticleSystem;
                trailToggleList.Add(tt); selType=SelType.TrailToggle; selIdx=trailToggleList.Count-1;
                break;}
            case SelType.AnimSegment:{
                var s=JsonUtility.FromJson<Global.AnimClipSegment>(_clipboardJson);
                s.startFrame=f;
                if(_clipboardRefs.ContainsKey("clip")) s.clip=_clipboardRefs["clip"] as AnimationClip;
                animSegments.Add(s); selType=SelType.AnimSegment; selIdx=animSegments.Count-1;
                break;}
            case SelType.Move:{
                var move=JsonUtility.FromJson<Global.MoveSegment>(_clipboardJson);
                int span=move.endKeyNumber-move.keyNumber;
                move.keyNumber=f;
                move.endKeyNumber=f+Mathf.Max(span,0);
                moveSegmentList.Add(move); selType=SelType.Move; selIdx=moveSegmentList.Count-1;
                break;}
        }
        Repaint();
    }
    void RestoreAtkRefs(Global.Attack a){
        if(_clipboardRefs.ContainsKey("hitVfx")) a.hitVfx=_clipboardRefs["hitVfx"] as ParticleSystem;
        if(_clipboardRefs.ContainsKey("hitSound")) a.hitSound=_clipboardRefs["hitSound"] as AudioClip;
    }
    void RestoreFxRefs(Global.FxAndSound f){
        if(_clipboardRefs.ContainsKey("particleSystem")) f.particleSystem=_clipboardRefs["particleSystem"] as ParticleSystem;
        if(_clipboardRefs.ContainsKey("audioClip")) f.audioClip=_clipboardRefs["audioClip"] as AudioClip;
    }
    void RestoreWarnRefs(Global.WarningCue w){
        if(_clipboardRefs.ContainsKey("warningSound")) w.warningSound=_clipboardRefs["warningSound"] as AudioClip;
        if(_clipboardRefs.ContainsKey("warningVfx")) w.warningVfx=_clipboardRefs["warningVfx"] as ParticleSystem;
    }
    void RestoreProjRefs(Global.Projectile p){
        if(_clipboardRefs.ContainsKey("prefab")) p.prefab=_clipboardRefs["prefab"] as GameObject;
        if(_clipboardRefs.ContainsKey("muzzleVfx")) p.muzzleVfx=_clipboardRefs["muzzleVfx"] as ParticleSystem;
        if(_clipboardRefs.ContainsKey("fireSound")) p.fireSound=_clipboardRefs["fireSound"] as AudioClip;
        if(_clipboardRefs.ContainsKey("impactVfx")) p.impactVfx=_clipboardRefs["impactVfx"] as ParticleSystem;
        if(_clipboardRefs.ContainsKey("impactSound")) p.impactSound=_clipboardRefs["impactSound"] as AudioClip;
    }
    void RestoreHitFxRefs(Global.HitFx h){
        if(_clipboardRefs.ContainsKey("hitVfx")) h.hitVfx=_clipboardRefs["hitVfx"] as ParticleSystem;
        if(_clipboardRefs.ContainsKey("hitSound")) h.hitSound=_clipboardRefs["hitSound"] as AudioClip;
    }

    /// <summary>获取剪贴板中数据的类型描述（用于菜单显示）</summary>
    string GetClipboardLabel(){
        var def=GetDefBySel(_clipboardType);
        return def!=null?def.itemName:"Event";
    }

    /// <summary>检查剪贴板类型是否可以粘贴到指定轨道</summary>
    bool CanPasteToTrack(Global.TrackType trackType){
        if(!HasClipboard) return false;
        if(BuiltinTrackRegistry.Canonical(trackType)==Global.TrackType.Flow) return _clipboardType==SelType.Flow || _clipboardType==SelType.Jump || _clipboardType==SelType.Cancel;
        var def=GetDef(trackType);
        // 位移曲线轨道没有可粘贴的 Event
        return def!=null&&def.removeAt!=null&&(def.selType==_clipboardType ||
            (BuiltinTrackRegistry.Canonical(trackType)==Global.TrackType.Fx &&
             (_clipboardType==SelType.Fx || _clipboardType==SelType.Sound || _clipboardType==SelType.Warning)));
    }
    #endregion

    #region 交互
    float _segmentGrabFrame;
    void HandleTLInput(Rect area,int totalF,Event input=null){
        var e=input??Event.current;
        if(e==null) return;
        Rect ruler=new Rect(area.x,area.y,area.width,RULER_HEIGHT);
        if(e.type==EventType.MouseDown&&e.button==0&&ruler.Contains(e.mousePosition)){ isDraggingPlayhead=true; SetFrame(e.mousePosition.x,totalF); e.Use(); }
        if(isDraggingPlayhead){
            if(e.type==EventType.MouseDrag){ SetFrame(e.mousePosition.x,totalF); e.Use(); }
            if(e.type==EventType.MouseUp){ isDraggingPlayhead=false; e.Use(); }
        }
        // Segment 整体拖拽
        if(isDraggingSegment&&e.type==EventType.MouseDrag){
            _editGesture.Record(configFile,"Move Animation Segment");
            if(dragSegIdx>=0&&dragSegIdx<animSegments.Count){
                int nf=Mathf.Max(0,Mathf.RoundToInt(e.mousePosition.x/pixelsPerFrame-_segmentGrabFrame));
                animSegments[dragSegIdx].startFrame=nf; Repaint(); e.Use(); }
        }
        if(isDraggingSegment&&e.type==EventType.MouseUp){ CommitGuiChanges(); _editGesture.Complete(); isDraggingSegment=false; e.Use(); }
        // Segment 边缘拖拽（裁剪）
        if(isDraggingSegEdge&&e.type==EventType.MouseDrag){
            _editGesture.Record(configFile,"Trim Animation Segment");
            if(segEdgeIdx>=0&&segEdgeIdx<animSegments.Count){
                var seg=animSegments[segEdgeIdx]; if(seg.clip!=null){
                    int totalClipF=Mathf.Max(1,(int)(seg.clip.length*seg.clip.frameRate));
                    int mf=Mathf.Max(0,Mathf.RoundToInt(e.mousePosition.x/pixelsPerFrame));
                    float ratio=configFile!=null && configFile.UsesExplicitTiming ? ActionTiming.SourceRate(seg)/ActionTiming.FrameRate(configFile) : 1f;
                    if(segEdgeSide==0){ // 左：裁剪起始
                        int delta=Mathf.RoundToInt((mf-segOrigTimelineStart)*ratio);
                        int newCS=Mathf.Clamp(segOrigClipStart+delta,0,segOrigClipEnd-1);
                        seg.clipStartFrame=newCS; seg.startFrame=Mathf.Max(0,segOrigTimelineStart+Mathf.RoundToInt((newCS-segOrigClipStart)/ratio));
                    }else{ // 右：裁剪结束
                        int frameInTL=Mathf.RoundToInt((mf-seg.startFrame)*ratio);
                        seg.clipEndFrame=Mathf.Clamp(seg.clipStartFrame+frameInTL,seg.clipStartFrame+1,totalClipF);
                    } Repaint(); e.Use(); }
            }
        }
        if(isDraggingSegEdge&&e.type==EventType.MouseUp){ CommitGuiChanges(); _editGesture.Complete(); isDraggingSegEdge=false; e.Use(); }
        if(isDraggingItem&&e.type==EventType.MouseDrag){
            _editGesture.Record(configFile,"Move Action Event");
            int nf=Mathf.Clamp(Mathf.RoundToInt(e.mousePosition.x/pixelsPerFrame),0,totalF);
            int span=dragOrigEnd-dragOrigF;
            int nb=Mathf.Clamp(nf,0,totalF-Mathf.Max(span,0));
            switch(dragType){
                case SelType.FlowEnd: if(configFile!=null) configFile.exitFrame=Mathf.Max(1,nb); break;
                case SelType.Flow: if(dragIdx>=0&&dragIdx<flowNodes.Count){ flowNodes[dragIdx].keyNumber=nb; if(flowNodes[dragIdx].limitWindow) flowNodes[dragIdx].endKeyNumber=nb+Mathf.Max(0,span); } break;
                case SelType.Camera: if(dragIdx>=0&&dragIdx<cameraCues.Count){cameraCues[dragIdx].keyNumber=nb;cameraCues[dragIdx].endKeyNumber=nb+Mathf.Max(0,span);} break;
                case SelType.Interaction: if(dragIdx>=0&&dragIdx<interactionWindows.Count){
                    interactionWindows[dragIdx].keyNumber=nb; interactionWindows[dragIdx].endKeyNumber=nb+Mathf.Max(0,span); } break;
                case SelType.Attack: if(dragIdx>=0&&dragIdx<atkList.Count){ atkList[dragIdx].keyNumber=nb;
                    if(span>0) atkList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Fx: if(dragIdx>=0&&dragIdx<fxAndSoundList.Count){ fxAndSoundList[dragIdx].keyNumber=nb;
                    if(span>0) fxAndSoundList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Jump: if(dragIdx>=0&&dragIdx<jumpList.Count){
                    jumpList[dragIdx].beginKey=nb; jumpList[dragIdx].endKey=nb+span; } break;
                case SelType.Warning: if(dragIdx>=0&&dragIdx<warningCueList.Count){ warningCueList[dragIdx].keyNumber=nb;
                    if(span>0) warningCueList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Sound: if(dragIdx>=0&&dragIdx<fxAndSoundList.Count){ fxAndSoundList[dragIdx].keyNumber=nb;
                    if(span>0) fxAndSoundList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Cancel: if(dragIdx>=0&&dragIdx<cancelList.Count){ cancelList[dragIdx].keyNumber=nb;
                    if(span>0) cancelList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Projectile: if(dragIdx>=0&&dragIdx<projectileList.Count){ projectileList[dragIdx].keyNumber=nb;
                    if(span>0) projectileList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.SuperArmor: if(dragIdx>=0&&dragIdx<superArmorList.Count){ superArmorList[dragIdx].keyNumber=nb;
                    if(span>0) superArmorList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.AdjustMotion: if(dragIdx>=0&&dragIdx<adjustMotionList.Count){ adjustMotionList[dragIdx].keyNumber=nb;
                    if(span>0) adjustMotionList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.TrailToggle: if(dragIdx>=0&&dragIdx<trailToggleList.Count){ trailToggleList[dragIdx].keyNumber=nb;
                    if(span>0) trailToggleList[dragIdx].endKeyNumber=nb+span; } break;
                case SelType.Move: if(dragIdx>=0&&dragIdx<moveSegmentList.Count){ moveSegmentList[dragIdx].keyNumber=nb;
                    if(span>0) moveSegmentList[dragIdx].endKeyNumber=nb+span; } break;
            }
            Repaint(); e.Use();
        }
        if(isDraggingItem&&e.type==EventType.MouseUp){ CommitGuiChanges(); _editGesture.Complete(); isDraggingItem=false; e.Use(); }
        if(e.type==EventType.ScrollWheel&&area.Contains(e.mousePosition)&&e.control){
            pixelsPerFrame=Mathf.Clamp(pixelsPerFrame-e.delta.y*.5f,MIN_PPF,MAX_PPF); e.Use(); }
        if(e.type==EventType.MouseDown&&e.button==0&&!isDraggingPlayhead&&!isDraggingItem&&!isDraggingSegment&&!isDraggingSegEdge&&e.mousePosition.y>RULER_HEIGHT)
            SetFrame(e.mousePosition.x,totalF);
    }
    void HandleGlobalInput(Event input=null){
        var e=input??Event.current;
        if(e==null || EditorGUIUtility.editingTextField) return;
        // Ctrl+Z：撤销   Ctrl+Shift+Z / Ctrl+Y：重做
        if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Z && (e.control||e.command)){
            if(e.shift) RedoLatest(); else UndoLatest(); e.Use(); return;
        }
        if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Y && (e.control||e.command)){
            RedoLatest(); e.Use(); return;
        }
        // Ctrl+C：复制选中的 clip
        if(e.type==EventType.KeyDown && e.keyCode==KeyCode.C && (e.control||e.command)){
            CopySelected(); e.Use(); return;
        }
        // Ctrl+V：粘贴剪贴板内容到当前播放头位置
        if(e.type==EventType.KeyDown && e.keyCode==KeyCode.V && (e.control||e.command)){
            PasteClipboard(); e.Use(); return;
        }
        // Ctrl/Cmd+S is intentionally left entirely to Unity.
        if(e.type!=EventType.KeyDown || e.shift || e.alt || e.control || e.command) return;
        int tf=GetTotalFrames();
        switch(e.keyCode){
            case KeyCode.Space:
                if(_validationErrorCount==0){ if(playFrame) StopPreviewSession(); else { playTimer=0; playFrame=true; } }
                else ShowNotification(new GUIContent("Fix validation errors before previewing."));
                e.Use(); break;
            case KeyCode.LeftArrow: frameSelectIndex=Mathf.Max(0,frameSelectIndex-1); e.Use(); break;
            case KeyCode.RightArrow: frameSelectIndex=Mathf.Min(tf,frameSelectIndex+1); e.Use(); break;
            case KeyCode.Home: frameSelectIndex=0; e.Use(); break;
            case KeyCode.End: frameSelectIndex=tf; e.Use(); break;
            case KeyCode.Delete: DelSel(); e.Use(); break;
        }
    }
    void SetFrame(float mx,int tf){ frameSelectIndex=Mathf.Clamp(Mathf.RoundToInt(mx/pixelsPerFrame),0,tf); }
    void StartDrag(SelType t,int i,int f,int end=0){ isDraggingItem=true; dragType=t; dragIdx=i; dragOrigF=f; dragOrigEnd=end; }
    void RemoveEvent<T>(List<T> list,int idx){ PushUndo(); list.RemoveAt(idx); }
    void DelSel(){
        if(selType==SelType.FlowEnd && configFile!=null) { PushUndo(); configFile.exitFrame=0; }
        // 原实现漏了「取消点 / 受击特效」两类，改为走注册表后全部覆盖
        var def=GetDefBySel(selType);
        if(def!=null&&def.removeAt!=null&&def.listCount!=null&&selIdx>=0&&selIdx<def.listCount())
            def.removeAt(selIdx);
        selType=SelType.None; selIdx=-1;
    }
    #endregion

    #region 添加项
    void AddAnimSegHere(){
        if(currentClips!=null&&currentClips.Length>0){
            var menu=new GenericMenu();
            foreach(var c in currentClips){ if(c==null)continue; var cc=c;
                menu.AddItem(new GUIContent(cc.name),false,()=>{
                    PushUndo();
                    animSegments.Add(new Global.AnimClipSegment{clip=cc,startFrame=frameSelectIndex});
                    selType=SelType.AnimSegment; selIdx=animSegments.Count-1; }); }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Empty clip (assign animation later)"),false,()=>{
                PushUndo();
                animSegments.Add(new Global.AnimClipSegment{startFrame=frameSelectIndex});
                selType=SelType.AnimSegment; selIdx=animSegments.Count-1; });
            menu.ShowAsContext();
        }else{
            PushUndo();
            animSegments.Add(new Global.AnimClipSegment{startFrame=frameSelectIndex});
            selType=SelType.AnimSegment; selIdx=animSegments.Count-1;
        }
    }
    void AddAtkHere(){ PushUndo(); var a=new Global.Attack{keyNumber=frameSelectIndex,shapeType=skillShapeSelectIndex,parameter1=range1,parameter2=range2,
        offset=new Vector3(offsetX,offsetY,offsetZ),isReWrite=true}; atkList.Add(a); selType=SelType.Attack; selIdx=atkList.Count-1; }
    void ShowEffectAddMenu(){
        var menu=new GenericMenu();
        menu.AddItem(new GUIContent("Visual Effect"),false,AddFxHere);
        menu.AddItem(new GUIContent("Audio"),false,AddSndHere);
        menu.AddItem(new GUIContent("Telegraph"),false,AddWarnHere);
        menu.ShowAsContext();
    }
    void AddFxHere(){ PushUndo(); fxAndSoundList.Add(new Global.FxAndSound{keyNumber=frameSelectIndex,contentKind=Global.EffectContentKind.VisualEffect}); selType=SelType.Fx; selIdx=fxAndSoundList.Count-1; }
    void AddSndHere(){ PushUndo(); fxAndSoundList.Add(new Global.FxAndSound{keyNumber=frameSelectIndex,contentKind=Global.EffectContentKind.Audio}); selType=SelType.Fx; selIdx=fxAndSoundList.Count-1; }
    void AddJumpHere(){ PushUndo(); int tf=GetTotalFrames(); int end=Mathf.Min(frameSelectIndex+10,tf);
        jumpList.Add(new Global.Jump{beginKey=frameSelectIndex,endKey=end}); selType=SelType.Jump; selIdx=jumpList.Count-1; }
    void AddWarnHere(){ PushUndo(); warningCueList.Add(new Global.WarningCue{keyNumber=frameSelectIndex}); selType=SelType.Warning; selIdx=warningCueList.Count-1; }
    void AddCancelHere(){ PushUndo(); int tf=GetTotalFrames(); cancelList.Add(new Global.CancelPoint{keyNumber=frameSelectIndex,endKeyNumber=tf}); selType=SelType.Cancel; selIdx=cancelList.Count-1; }
    void AddProjHere(){ PushUndo(); projectileList.Add(new Global.Projectile{keyNumber=frameSelectIndex}); selType=SelType.Projectile; selIdx=projectileList.Count-1; }
    void AddHitFxHere(){ PushUndo(); hitFxList.Add(new Global.HitFx()); selType=SelType.HitFx; selIdx=hitFxList.Count-1; }
    void AddSuperArmorHere(){ PushUndo(); int tf=GetTotalFrames(); int end=Mathf.Min(frameSelectIndex+15,tf);
        superArmorList.Add(new Global.SuperArmorSegment{keyNumber=frameSelectIndex,endKeyNumber=end}); selType=SelType.SuperArmor; selIdx=superArmorList.Count-1; }
    void AddAdjustMotionHere(){ PushUndo(); int tf=GetTotalFrames(); int end=Mathf.Min(frameSelectIndex+20,tf);
        adjustMotionList.Add(new Global.AdjustMotionSegment{keyNumber=frameSelectIndex,endKeyNumber=end}); selType=SelType.AdjustMotion; selIdx=adjustMotionList.Count-1; }
    void AddTrailHere(){ PushUndo(); int tf=GetTotalFrames(); int end=Mathf.Min(frameSelectIndex+15,tf);
        trailToggleList.Add(new Global.TrailToggle{keyNumber=frameSelectIndex,endKeyNumber=end}); selType=SelType.TrailToggle; selIdx=trailToggleList.Count-1; }

    void AddMoveSegmentHere(){ PushUndo(); int tf=GetTotalFrames(); int end=Mathf.Min(frameSelectIndex+15,tf);
        moveSegmentList.Add(new Global.MoveSegment{keyNumber=frameSelectIndex,endKeyNumber=end}); selType=SelType.Move; selIdx=moveSegmentList.Count-1; }
    #endregion
}
