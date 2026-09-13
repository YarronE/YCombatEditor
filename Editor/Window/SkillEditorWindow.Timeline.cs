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
    #region Timeline区域
    struct TrkInfo{ public TrackDef def; public string name; public float height;
        public Dictionary<int,int> lanes; public int laneCount; public bool expanded; public int trackListIdx; }

    // ═══════════════════════════════════════════════════════════
    //  轨道注册表 — 新增一种轨道只需在 BuildTrackDefs() 追加一条
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 轨道描述符：把「颜色/名称/添加菜单/右键菜单/绘制/属性面板/时间轴长度」全部收敛为数据。
    /// 原先这些信息散落在 9 处平行 switch 中，新增轨道需要改 9 个地方。
    /// </summary>
    class TrackDef{
        public Global.TrackType type;
        public SelType selType;
        public Color color;
        public string name;        // 轨道头默认显示名（含图标）
        public string itemName;    // 单个 Event 的中文名，用于菜单文案
        public string menuLabel;   // 「添加轨道」菜单文案
        public bool enemyOnly;     // 仅敌人技能可添加

        public Func<int> count;    // 轨道头括号内计数（null=不显示）
        public Func<int> listCount;// 事件列表长度，用于选中索引边界校验
        public Func<int> maxEnd;   // 对时间轴总长的贡献（null=不贡献）

        public Func<Dictionary<int,int>,Dictionary<int,int>> lanes; // lane 分配（null=非 lane 轨道）
        public Action<Rect,Dictionary<int,int>,int> drawLanes;      // lane 轨道绘制
        public Action<Rect> drawCustom;                             // 非 lane 轨道绘制
        public Func<float> customHeight;                            // 非 lane 轨道高度

        public Action addEvent;        // [+] 按钮 / 菜单「添加」（null=不可添加）
        public Action<int> inspector;  // 属性面板绘制（参数=selIdx）
        public Action<int> removeAt;   // 删除第 idx 个事件

        /// <summary>lane 结果复用缓冲，避免每帧新建 Dictionary</summary>
        public readonly Dictionary<int,int> laneBuf=new Dictionary<int,int>();
    }

    List<TrackDef> _trackDefs;
    List<TrackDef> TrackDefs{ get{ if(_trackDefs==null) _trackDefs=BuildTrackDefs(); return _trackDefs; } }

    TrackDef GetDef(Global.TrackType t){
        var defs=TrackDefs;
        for(int i=0;i<defs.Count;i++) if(defs[i].type==t) return defs[i];
        return null;
    }
    TrackDef GetDefBySel(SelType s){
        var defs=TrackDefs;
        for(int i=0;i<defs.Count;i++) if(defs[i].selType==s) return defs[i];
        return null;
    }

    /// <summary>轨道注册表。所有 lambda 在调用时读取字段，因此 LoadConfig 重建列表后依然有效。</summary>
    List<TrackDef> BuildTrackDefs(){
        // 帧段类事件的结束帧：endKeyNumber <= keyNumber 时视为单帧
        const int TAIL=5; // 事件末尾预留帧，保证时间轴可继续往后拖

        var animDef=new TrackDef{
            type=Global.TrackType.Animation, selType=SelType.AnimSegment, color=C_ANIM,
            name="Animation", itemName="Animation", menuLabel="Animation",
            count=()=>animSegments.Count, listCount=()=>animSegments.Count,
            maxEnd=()=>{ int m=0; foreach(var s in animSegments) if(ActionTiming.EndFrame(configFile,s)>m) m=ActionTiming.EndFrame(configFile,s); return m; },
            addEvent=AddAnimSegHere, inspector=i=>InspAnimSeg(animSegments[i]),
            removeAt=i=>RemoveEvent(animSegments,i) };
        animDef.lanes=buf=>AssignLanes(buf,animSegments,s=>s.startFrame,s=>ActionTiming.EndFrame(configFile,s)>s.startFrame?ActionTiming.EndFrame(configFile,s)-1:s.startFrame);
        animDef.drawLanes=DrawAnimTrackMultiSeg;

        var atkDef=new TrackDef{
            type=Global.TrackType.Attack, selType=SelType.Attack, color=C_ATK,
            name="Attack", itemName="Attack", menuLabel="Attack",
            count=()=>atkList.Count, listCount=()=>atkList.Count,
            maxEnd=()=>MaxEnd(atkList,a=>a.keyNumber,a=>a.endKeyNumber)+TAIL,
            addEvent=AddAtkHere, inspector=i=>InspAtk(atkList[i]),
            removeAt=i=>RemoveEvent(atkList,i) };
        atkDef.lanes=buf=>AssignLanes(buf,atkList,a=>a.keyNumber,a=>SegEnd(a.keyNumber,a.endKeyNumber));
        atkDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,atkList,atkDef,
            a=>a.keyNumber,a=>SegEnd(a.keyNumber,a.endKeyNumber),
            (a,b,e)=>b==e?a.keyNumber.ToString():$"{b}-{e}");

        var fxDef=new TrackDef{
            type=Global.TrackType.Fx, selType=SelType.Fx, color=C_FX,
            name="Effect", itemName="Effect", menuLabel="Effect",
            count=()=>fxAndSoundList.Count, listCount=()=>fxAndSoundList.Count,
            maxEnd=()=>MaxEnd(fxAndSoundList,f=>f.keyNumber,f=>f.endKeyNumber)+TAIL,
            addEvent=ShowEffectAddMenu, inspector=i=>InspFx(fxAndSoundList[i]),
            removeAt=i=>RemoveEvent(fxAndSoundList,i) };
        fxDef.lanes=buf=>AssignLanes(buf,fxAndSoundList,f=>f.keyNumber,f=>SegEnd(f.keyNumber,f.endKeyNumber));
        fxDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,fxAndSoundList,fxDef,
            f=>f.keyNumber,f=>SegEnd(f.keyNumber,f.endKeyNumber),
            (f,b,e)=>EffectAuthoring.Label(f),alwaysLabel:true);

        var sndDef=new TrackDef{
            type=Global.TrackType.Sound, selType=SelType.Sound, color=C_SND,
            name="Effect", itemName="Effect", menuLabel="Effect",
            count=SndCount, listCount=()=>fxAndSoundList.Count,
            maxEnd=null, // 与特效共用列表，长度已由 Fx 轨道贡献
            addEvent=AddSndHere, inspector=i=>InspSnd(fxAndSoundList[i]),
            removeAt=i=>RemoveEvent(fxAndSoundList,i) };
        sndDef.lanes=buf=>AssignLanes(buf,fxAndSoundList,f=>f.keyNumber,f=>SegEnd(f.keyNumber,f.endKeyNumber),BuiltinTrackRegistry.IsVisibleInSoundTrack);
        sndDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,fxAndSoundList,sndDef,
            f=>f.keyNumber,f=>SegEnd(f.keyNumber,f.endKeyNumber),
            (f,b,e)=>b==e?""+f.keyNumber:$"{b}-{e}",
            filter:BuiltinTrackRegistry.IsVisibleInSoundTrack);

        var jumpDef=new TrackDef{
            type=Global.TrackType.Jump, selType=SelType.Jump, color=C_JUMP,
            name="Transition", itemName="Transition", menuLabel="Transition",
            count=()=>jumpList.Count, listCount=()=>jumpList.Count,
            maxEnd=()=>{ int m=0; foreach(var j in jumpList) if(j.endKey>m) m=j.endKey; return m+TAIL; },
            addEvent=AddJumpHere, inspector=i=>InspJump(jumpList[i]),
            removeAt=i=>RemoveEvent(jumpList,i) };
        jumpDef.lanes=buf=>AssignLanes(buf,jumpList,j=>j.beginKey,j=>j.endKey);
        jumpDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,jumpList,jumpDef,
            j=>j.beginKey,j=>j.endKey,
            (j,b,e)=>j.nextSkill!=null?(j.autoTrigger?" ":"")+j.nextSkill.name:$"{b}-{e}",
            endExclusive:true,alwaysLabel:true);

        var warnDef=new TrackDef{
            type=Global.TrackType.Warning, selType=SelType.Warning, color=C_WARN,
            name="Telegraph", itemName="Telegraph", menuLabel="Telegraph", enemyOnly=false,
            count=()=>warningCueList.Count, listCount=()=>warningCueList.Count,
            maxEnd=()=>MaxEnd(warningCueList,w=>w.keyNumber,w=>w.endKeyNumber)+TAIL,
            addEvent=AddWarnHere, inspector=i=>InspWarn(warningCueList[i]),
            removeAt=i=>RemoveEvent(warningCueList,i) };
        warnDef.lanes=buf=>AssignLanes(buf,warningCueList,w=>w.keyNumber,w=>SegEnd(w.keyNumber,w.endKeyNumber));
        warnDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,warningCueList,warnDef,
            w=>w.keyNumber,w=>SegEnd(w.keyNumber,w.endKeyNumber),
            (w,b,e)=>b==e?"":$"{b}-{e}");

        var cancelDef=new TrackDef{
            type=Global.TrackType.Cancel, selType=SelType.Cancel, color=C_CANCEL,
            name="Cancel", itemName="Cancel", menuLabel="Cancel",
            count=()=>cancelList.Count, listCount=()=>cancelList.Count,
            maxEnd=()=>MaxEnd(cancelList,c=>c.keyNumber,c=>c.endKeyNumber)+TAIL,
            addEvent=AddCancelHere, inspector=i=>InspCancel(cancelList[i]),
            removeAt=i=>RemoveEvent(cancelList,i) };
        cancelDef.lanes=buf=>AssignLanes(buf,cancelList,c=>c.keyNumber,c=>SegEnd(c.keyNumber,c.endKeyNumber));
        cancelDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,cancelList,cancelDef,
            cp=>cp.keyNumber,cp=>SegEnd(cp.keyNumber,cp.endKeyNumber),
            (cp,b,e)=>$"≥Lv{(cp.minCancelPriority==Global.CancelPriority.Uncancellable?"∞":((int)cp.minCancelPriority).ToString())}");

        var projDef=new TrackDef{
            type=Global.TrackType.Projectile, selType=SelType.Projectile, color=C_PROJ,
            name="Projectile", itemName="Projectile", menuLabel="Projectile",
            count=()=>projectileList.Count, listCount=()=>projectileList.Count,
            maxEnd=()=>MaxEnd(projectileList,p=>p.keyNumber,p=>p.endKeyNumber)+TAIL,
            addEvent=AddProjHere, inspector=i=>InspProj(projectileList[i]),
            removeAt=i=>RemoveEvent(projectileList,i) };
        projDef.lanes=buf=>AssignLanes(buf,projectileList,p=>p.keyNumber,p=>SegEnd(p.keyNumber,p.endKeyNumber));
        projDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,projectileList,projDef,
            p=>p.keyNumber,p=>SegEnd(p.keyNumber,p.endKeyNumber),
            (p,b,e)=>{ string s=p.prefab!=null?p.prefab.name:$"F{b}"; return b==e?s:$"{s} {b}-{e}"; });

        var saDef=new TrackDef{
            type=Global.TrackType.SuperArmor, selType=SelType.SuperArmor, color=C_SA,
            name="Armor", itemName="Armor", menuLabel="Armor",
            count=()=>superArmorList.Count, listCount=()=>superArmorList.Count,
            maxEnd=()=>MaxEnd(superArmorList,s=>s.keyNumber,s=>s.endKeyNumber)+TAIL,
            addEvent=AddSuperArmorHere, inspector=i=>InspSuperArmor(superArmorList[i]),
            removeAt=i=>RemoveEvent(superArmorList,i) };
        saDef.lanes=buf=>AssignLanes(buf,superArmorList,s=>s.keyNumber,s=>SegEnd(s.keyNumber,s.endKeyNumber));
        saDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,superArmorList,saDef,
            s=>s.keyNumber,s=>SegEnd(s.keyNumber,s.endKeyNumber),
            (s,b,e)=>$"{(s.armorLevel==0?"L1":s.armorLevel==1?"L2":"?")} {b}-{e}");

        var amDef=new TrackDef{
            type=Global.TrackType.AdjustMotion, selType=SelType.AdjustMotion, color=C_AM,
            name="Facing", itemName="Facing", menuLabel="Facing", enemyOnly=false,
            count=()=>adjustMotionList.Count, listCount=()=>adjustMotionList.Count,
            maxEnd=()=>MaxEnd(adjustMotionList,a=>a.keyNumber,a=>a.endKeyNumber)+TAIL,
            addEvent=AddAdjustMotionHere, inspector=i=>InspAdjustMotion(adjustMotionList[i]),
            removeAt=i=>RemoveEvent(adjustMotionList,i) };
        amDef.lanes=buf=>AssignLanes(buf,adjustMotionList,a=>a.keyNumber,a=>SegEnd(a.keyNumber,a.endKeyNumber));
        amDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,adjustMotionList,amDef,
            a=>a.keyNumber,a=>SegEnd(a.keyNumber,a.endKeyNumber),
            (a,b,e)=>a.allowTurning?$"Track target {b}-{e}":$"Lock facing {b}-{e}");

        var trailDef=new TrackDef{
            type=Global.TrackType.Trail, selType=SelType.TrailToggle, color=C_TRAIL,
            name="Trail", itemName="Trail", menuLabel="Trail",
            count=()=>trailToggleList.Count, listCount=()=>trailToggleList.Count,
            maxEnd=()=>MaxEnd(trailToggleList,t=>t.keyNumber,t=>t.endKeyNumber)+TAIL,
            addEvent=AddTrailHere, inspector=i=>InspTrail(trailToggleList[i]),
            removeAt=i=>RemoveEvent(trailToggleList,i) };
        trailDef.lanes=buf=>AssignLanes(buf,trailToggleList,t=>t.keyNumber,t=>SegEnd(t.keyNumber,t.endKeyNumber));
        trailDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,trailToggleList,trailDef,
            t=>t.keyNumber,t=>SegEnd(t.keyNumber,t.endKeyNumber),
            (t,b,e)=>$"{b}-{e} {TrailShortLabel(t)}");

        // ── 非 lane 轨道 ──
        var hitFxDef=new TrackDef{
            type=Global.TrackType.HitFx, selType=SelType.HitFx, color=C_HITFX,
            name="Hit Effect", itemName="Hit Effect", menuLabel="Hit Effect",
            count=()=>hitFxList.Count, listCount=()=>hitFxList.Count,
            drawCustom=DrawHitFxTrack, customHeight=()=>TRACK_HEIGHT*Mathf.Max(hitFxList.Count,1),
            addEvent=AddHitFxHere, inspector=i=>InspHitFx(hitFxList[i]),
            removeAt=i=>RemoveEvent(hitFxList,i) };

        var moveDef=new TrackDef{
            type=Global.TrackType.Move, selType=SelType.Move, color=C_MOVE,
            name="Motion", itemName="Motion", menuLabel="Motion",
            count=()=>moveSegmentList.Count, listCount=()=>moveSegmentList.Count,
            maxEnd=()=>MaxEnd(moveSegmentList,m=>m.keyNumber,m=>SegEnd(m.keyNumber,m.endKeyNumber))+TAIL,
            addEvent=AddMoveSegmentHere, inspector=i=>InspMoveSegment(i),
            removeAt=i=>RemoveEvent(moveSegmentList,i) };
        moveDef.lanes=buf=>AssignLanes(buf,moveSegmentList,m=>m.keyNumber,m=>SegEnd(m.keyNumber,m.endKeyNumber));
        moveDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,moveSegmentList,moveDef,
            m=>m.keyNumber,m=>SegEnd(m.keyNumber,m.endKeyNumber),
            (m,b,e)=>MoveSegLabel(m,b,e));

        var interactionDef=new TrackDef{
            type=Global.TrackType.Interaction, selType=SelType.Interaction, color=C_CANCEL,
            name="Interaction", itemName="Interaction", menuLabel="Interaction",
            count=()=>interactionWindows.Count, listCount=()=>interactionWindows.Count,
            maxEnd=()=>MaxEnd(interactionWindows,w=>w.keyNumber,w=>w.endKeyNumber)+TAIL,
            addEvent=AddInteractionHere, inspector=DrawInteractionInspector,
            removeAt=i=>RemoveEvent(interactionWindows,i) };
        interactionDef.lanes=buf=>AssignLanes(buf,interactionWindows,w=>w.keyNumber,w=>SegEnd(w.keyNumber,w.endKeyNumber));
        interactionDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,interactionWindows,interactionDef,
            w=>w.keyNumber,w=>SegEnd(w.keyNumber,w.endKeyNumber),(w,b,e)=>$"{w.id} [{b}-{e}]",alwaysLabel:true);

        moveDef.count=()=>moveSegmentList.Count+adjustMotionList.Count;
        moveDef.lanes=null;
        moveDef.drawCustom=DrawUnifiedMotionTrack;
        moveDef.customHeight=MotionTrackHeight;
        moveDef.addEvent=ShowMotionAddMenu;
        var cameraDef=new TrackDef {
            type=Global.TrackType.Camera, selType=SelType.Camera, color=new Color(.55f,.65f,.95f,.9f),
            name="Camera", itemName="Camera cue", menuLabel="Camera",
            count=()=>cameraCues.Count, listCount=()=>cameraCues.Count,
            maxEnd=()=>MaxEnd(cameraCues,c=>c.keyNumber,c=>c.endKeyNumber)+TAIL,
            addEvent=AddCameraHere, inspector=i=>DrawSerializedSelection("cameraCues",i),
            removeAt=i=>RemoveEvent(cameraCues,i) };
        cameraDef.lanes=buf=>AssignLanes(buf,cameraCues,c=>c.keyNumber,c=>c.endKeyNumber);
        cameraDef.drawLanes=(r,l,c)=>DrawEventTrack(r,l,c,cameraCues,cameraDef,
            cue=>cue.keyNumber,cue=>cue.endKeyNumber,(cue,b,e)=>cue.label,alwaysLabel:true);
        return new List<TrackDef>{
            cameraDef,animDef,atkDef,fxDef,sndDef,jumpDef,cancelDef,projDef,hitFxDef,
            moveDef,warnDef,saDef,amDef,trailDef,interactionDef };
    }

    /// <summary>帧段结束帧：endKey <= beginKey 时视为单帧</summary>
    static int SegEnd(int begin,int end)=>end>begin?end:begin;
    /// <summary>列表中最大结束帧</summary>
    static int MaxEnd<T>(List<T> list,Func<T,int> getBegin,Func<T,int> getEnd){
        int m=0;
        for(int i=0;i<list.Count;i++){ int e=SegEnd(getBegin(list[i]),getEnd(list[i])); if(e>m) m=e; }
        return m;
    }
    static string TrailShortLabel(Global.TrailToggle t){
        if(t.particle!=null) return $"{t.particle.gameObject.name}";
        if(string.IsNullOrEmpty(t.trailPath)) return "Unassigned";
        return t.trailPath.Length>12?".."+t.trailPath.Substring(t.trailPath.Length-10):t.trailPath;
    }

    // ── 时间轴总帧数：每次 OnGUI 重算一次，帧内复用 ──
    int _totalFramesCache=-1;

    int GetTotalFrames(){
        if(_totalFramesCache>=0) return _totalFramesCache;
        int max=MIN_TOTAL_FRAMES;
        var defs=TrackDefs;
        for(int i=0;i<defs.Count;i++){
            if(defs[i].maxEnd==null) continue;
            int e=defs[i].maxEnd(); if(e>max) max=e;
        }
        return _totalFramesCache=max;
    }
    Color GetTrackColor(Global.TrackType t){ var d=GetDef(t); return d!=null?d.color:Color.gray; }
    string GetTrackDefaultName(Global.TrackType t){ var d=GetDef(t); return d!=null?d.name:"Track"; }

    int FxCount(){ int c=0; foreach(var f in fxAndSoundList) if(BuiltinTrackRegistry.IsVisibleInFxTrack(f)) c++; return c; }
    int SndCount(){ int c=0; foreach(var f in fxAndSoundList) if(BuiltinTrackRegistry.IsVisibleInSoundTrack(f)) c++; return c; }

    readonly List<TrkInfo> _tracks=new List<TrkInfo>();

    List<TrkInfo> BuildTracks(){
        _tracks.Clear();
        var seen=new HashSet<Global.TrackType>();
        foreach(var layer in new[]{"Presentation","Mechanics"})
        for(int ti=0;ti<trackList.Count;ti++){
            var trk=trackList[ti];
            var canonical=BuiltinTrackRegistry.Canonical(trk.type);
            if(BuiltinTrackRegistry.GetCategory(canonical)!=layer || !seen.Add(canonical)) continue;
            var def=GetDef(canonical); if(def==null) continue;
            bool exp=trk.expanded;
            Dictionary<int,int> lanes=null; int laneCount=1; float h;
            if(def.lanes!=null){
                lanes=def.lanes(def.laneBuf);
                foreach(var v in lanes.Values) if(v+1>laneCount) laneCount=v+1;
                h=exp?TRACK_HEIGHT*laneCount:0;
            }else h=exp?def.customHeight():0;
            _tracks.Add(new TrkInfo{ def=def, name=def.name,
                height=h, lanes=lanes, laneCount=laneCount, expanded=exp, trackListIdx=ti });
        }
        return _tracks;
    }

    readonly List<int> _laneEnds=new List<int>();

    /// <summary>分配 lane 索引，使重叠的条目分布在不同行。结果写入复用缓冲，避免每帧分配。</summary>
    Dictionary<int,int> AssignLanes<T>(Dictionary<int,int> buf,List<T> list,
        Func<T,int> getBegin,Func<T,int> getEnd,Func<T,bool> filter=null){
        buf.Clear(); _laneEnds.Clear();
        for(int i=0;i<list.Count;i++){
            if(filter!=null&&!filter(list[i])) continue;
            int b=getBegin(list[i]),e=getEnd(list[i]);
            int assigned=-1;
            for(int l=0;l<_laneEnds.Count;l++){
                if(_laneEnds[l]<b){ assigned=l; _laneEnds[l]=e; break; }
            }
            if(assigned<0){ assigned=_laneEnds.Count; _laneEnds.Add(e); }
            buf[i]=assigned;
        }
        return buf;
    }

    void DrawTimelineArea(Rect area){
        EditorGUI.DrawRect(area,C_BG);
        int totalF=GetTotalFrames();
        float tlW=totalF*pixelsPerFrame+200f;
        var tracks=BuildTracks();
        float trkH=RULER_HEIGHT+ADD_BTN_HEIGHT+52; foreach(var t in tracks) trkH+=Mathf.Max(t.height,30);

        // 左侧轨道头
        Rect hdr=new Rect(area.x,area.y,TRACK_HEADER_WIDTH,area.height);
        DrawTrackHeaders(hdr,tracks);

        // 右侧内容
        Rect cnt=new Rect(area.x+TRACK_HEADER_WIDTH,area.y,area.width-TRACK_HEADER_WIDTH,area.height);
        GUI.BeginClip(cnt);
        float cw=cnt.width,ch=cnt.height;
        timelineScrollPos=GUI.BeginScrollView(new Rect(0,0,cw,ch),timelineScrollPos,
            new Rect(0,0,tlW,Mathf.Max(trkH,ch-16)));

        DrawRuler(new Rect(0,0,tlW,RULER_HEIGHT),totalF);
        float y=RULER_HEIGHT; string previousLayer=null;
        for(int i=0;i<tracks.Count;i++){
            var t=tracks[i]; float h=Mathf.Max(t.height,30);
            string layer=BuiltinTrackRegistry.GetCategory(t.def.type);
            if(layer!=previousLayer){ EditorGUI.DrawRect(new Rect(0,y,tlW,26),new Color(.13f,.14f,.16f)); y+=26; previousLayer=layer; }
            Rect r=new Rect(0,y,tlW,h);
            EditorGUI.DrawRect(r,i%2==0?C_TRK:C_TRK2);
            DrawGrid(r,totalF);
            if(t.expanded&&t.height>0){
                if(t.def.lanes!=null) t.def.drawLanes(r,t.lanes,t.laneCount);
                else t.def.drawCustom?.Invoke(r);
            }
            y+=h;
        }
        float endX=totalF*pixelsPerFrame;
        EditorGUI.DrawRect(new Rect(endX-1,0,2,y),new Color(1f,.65f,.15f,.75f));
        GUI.Label(new Rect(Mathf.Max(0,endX-58),2,56,16),$"END {totalF}",EditorStyles.miniLabel);
        DrawPlayhead(new Rect(0,0,tlW,y),totalF);
        HandleTLInput(new Rect(0,0,tlW,y),totalF);
        GUI.EndScrollView();
        GUI.EndClip();
    }

    void DrawTrackHeaders(Rect area,List<TrkInfo> tracks){
        EditorGUI.DrawRect(area,new Color(.16f,.16f,.16f));
        Rect rh=new Rect(area.x,area.y,area.width,RULER_HEIGHT);
        EditorGUI.DrawRect(rh,new Color(.14f,.14f,.14f));
        GUI.Label(new Rect(rh.x+6,rh.y+3,rh.width-12,16),configFile!=null?configFile.name:"---",EditorStyles.miniLabel);
        float y=area.y+RULER_HEIGHT-timelineScrollPos.y; string previousLayer=null;
        for(int i=0;i<tracks.Count;i++){
            var t=tracks[i]; float h=Mathf.Max(t.height,30);
            string layer=BuiltinTrackRegistry.GetCategory(t.def.type);
            if(layer!=previousLayer){
                EditorGUI.DrawRect(new Rect(area.x,y,area.width,26),new Color(.13f,.14f,.16f));
                GUI.Label(new Rect(area.x+12,y+5,area.width-24,18),layer,EditorStyles.boldLabel);
                y+=26; previousLayer=layer;
            }
            Rect hr=new Rect(area.x,y,area.width,h);
            if(hr.yMax>=area.y&&hr.y<=area.yMax){
                EditorGUI.DrawRect(hr,i%2==0?new Color(.19f,.19f,.19f):new Color(.21f,.21f,.21f));
                // 左侧颜色条
                EditorGUI.DrawRect(new Rect(hr.x,hr.y,4,hr.height),t.def.color);
                // 轨道名称 (为按钮留出空间)
                float btnZoneW=92; // [+] 和 [▼] 两个按钮的总宽度
                GUI.Label(new Rect(hr.x+8,hr.y+3,hr.width-btnZoneW-8,16),t.name,TSty.TrkLbl);
                // ── [+] 添加 Event 按钮（醒目，仅非位移轨道显示）──
                if(t.def.addEvent!=null){
                    Rect addBtnR=new Rect(hr.xMax-92,hr.y+5,40,20);
                    // 用轨道颜色作为按钮高亮背景
                    Color btnBg=new Color(t.def.color.r,t.def.color.g,t.def.color.b,0.35f);
                    EditorGUI.DrawRect(addBtnR,btnBg);
                    if(GUI.Button(addBtnR,"Add",EditorStyles.miniButton)){
                        t.def.addEvent();
                    }
                }
                // ── [▼/▶] 展开/折叠按钮 ──
                int ti=t.trackListIdx;
                if(GUI.Button(new Rect(hr.xMax-48,hr.y+5,44,20),t.expanded?"Hide":"Show",EditorStyles.miniButton)){
                    PushUndo(); trackList[ti].expanded=!trackList[ti].expanded;
                }
                // 右键上下文菜单
                if(Event.current.type==EventType.ContextClick&&hr.Contains(Event.current.mousePosition)){
                    ShowTrackContextMenu(t.trackListIdx); Event.current.Use(); }
            }
            y+=h;
        }
        // ── 底部：添加轨道按钮 ──
        Rect addRect=new Rect(area.x,y,area.width,ADD_BTN_HEIGHT);
        if(addRect.yMax>=area.y&&addRect.y<=area.yMax){
            EditorGUI.DrawRect(addRect,new Color(.17f,.17f,.17f));
            if(GUI.Button(new Rect(addRect.x+4,addRect.y+2,addRect.width-8,addRect.height-4),"Add track",EditorStyles.miniButton))
                ShowAddTrackMenu();
            if(Event.current.type==EventType.ContextClick&&addRect.Contains(Event.current.mousePosition)){
                ShowAddTrackMenu(); Event.current.Use(); }
        }
        EditorGUI.DrawRect(new Rect(area.xMax-1,area.y,1,area.height),new Color(0,0,0,.5f));
    }

    void ShowAddTrackMenu(){
        var menu=new GenericMenu();
        var existing=new HashSet<Global.TrackType>(); foreach(var t in trackList) existing.Add(BuiltinTrackRegistry.Canonical(t.type));
        foreach(var def in TrackDefs){
            if(BuiltinTrackRegistry.Canonical(def.type)!=def.type) continue;
            var type=def.type; var label=GetTrackMenuPath(def);
            if(existing.Contains(type)) menu.AddDisabledItem(new GUIContent($"{label} (already added)"));
            else menu.AddItem(new GUIContent(label),false,()=>{
                PushUndo(); trackList.Add(new Global.SkillTrack{type=type,expanded=true}); });
        }
        menu.ShowAsContext();
    }

    static string GetTrackMenuPath(TrackDef def){
        return $"{BuiltinTrackRegistry.GetCategory(def.type)}/{def.menuLabel}";
    }

    void ShowTrackContextMenu(int trackListIdx){
        if(trackListIdx<0||trackListIdx>=trackList.Count) return;
        var trk=trackList[trackListIdx]; int ti=trackListIdx;
        var def=GetDef(BuiltinTrackRegistry.Canonical(trk.type));
        var menu=new GenericMenu();
        if(def!=null){
            if(def.addEvent!=null)
                menu.AddItem(new GUIContent($"Add {def.itemName} at playhead"),false,()=>def.addEvent());
            bool hasSel=selType==def.selType&&selIdx>=0&&def.listCount!=null&&selIdx<def.listCount();
            if(hasSel){
                menu.AddItem(new GUIContent($"Copy selected {def.itemName}"),false,CopySelected);
                if(def.removeAt!=null){
                    int si=selIdx; var rm=def.removeAt;
                    menu.AddItem(new GUIContent($"Remove selected {def.itemName}"),false,()=>{
                        rm(si); selType=SelType.None; selIdx=-1; });
                }
            }
            if(CanPasteToTrack(trk.type))
                menu.AddItem(new GUIContent($"Paste {GetClipboardLabel()} at frame {frameSelectIndex}"),false,PasteClipboard);
        }
        menu.AddSeparator("");
        if(ti>0) menu.AddItem(new GUIContent("Move track up"),false,()=>{ PushUndo(); var tmp=trackList[ti]; trackList[ti]=trackList[ti-1]; trackList[ti-1]=tmp; });
        else menu.AddDisabledItem(new GUIContent("Move track up"));
        if(ti<trackList.Count-1) menu.AddItem(new GUIContent("Move track down"),false,()=>{ PushUndo(); var tmp=trackList[ti]; trackList[ti]=trackList[ti+1]; trackList[ti+1]=tmp; });
        else menu.AddDisabledItem(new GUIContent("Move track down"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Hide track"),false,()=>{
            if(EditorUtility.DisplayDialog("Hide track",$"Hide the {GetTrackDefaultName(trk.type)} track?\nEvents are preserved and remain active at runtime. Add the track again to edit them.","Remove","Cancel")){
                PushUndo();
                trackList.RemoveAt(ti);
            }
        });
        menu.ShowAsContext();
    }
    #endregion

    #region 标尺与网格
    void DrawRuler(Rect r,int totalF){
        EditorGUI.DrawRect(r,new Color(.15f,.15f,.15f));
        int iv=pixelsPerFrame<6?10:pixelsPerFrame<12?5:pixelsPerFrame<20?2:1;
        for(int f=0;f<=totalF;f++){
            float x=f*pixelsPerFrame; if(x>r.width) break;
            bool maj=f%(iv*5)==0; bool mid=f%iv==0;
            if(maj){ EditorGUI.DrawRect(new Rect(r.x+x,r.yMax-12,1,12),new Color(1,1,1,.5f));
                string lb=f.ToString();
                GUI.Label(new Rect(r.x+x+2,r.y+1,50,r.height),lb,TSty.Ruler); }
            else if(mid) EditorGUI.DrawRect(new Rect(r.x+x,r.yMax-6,1,6),new Color(1,1,1,.25f));
        }
        EditorGUI.DrawRect(new Rect(r.x,r.yMax-1,r.width,1),new Color(0,0,0,.6f));
    }
    void DrawGrid(Rect r,int totalF){
        int iv=pixelsPerFrame<6?10:pixelsPerFrame<12?5:pixelsPerFrame<20?2:1;
        for(int f=0;f<=totalF;f+=iv){
            float x=f*pixelsPerFrame; if(x>r.width) break;
            EditorGUI.DrawRect(new Rect(r.x+x,r.y,1,r.height),f%(iv*5)==0?C_GRIDM:C_GRID);
        }
    }
    void DrawPlayhead(Rect area,int totalF){
        float x=frameSelectIndex*pixelsPerFrame;
        EditorGUI.DrawRect(new Rect(x-1,0,PLAYHEAD_WIDTH,area.height),C_HEAD);
        EditorGUI.DrawRect(new Rect(x-5,0,10,8),C_HEAD);
    }
    #endregion

    #region 轨道绘制
    void DrawAnimTrackMultiSeg(Rect r,Dictionary<int,int> lanes,int laneCount){
        float laneH=laneCount>0?r.height/laneCount:r.height;
        for(int si=0;si<animSegments.Count;si++){
            var seg=animSegments[si]; if(seg.clip==null) continue;
            int lane=lanes.ContainsKey(si)?lanes[si]:0;
            int totalClipF=Mathf.Max(1,(int)(seg.clip.length*seg.clip.frameRate));
            int cs=Mathf.Clamp(seg.clipStartFrame,0,totalClipF);
            int ce=seg.clipEndFrame>0?Mathf.Clamp(seg.clipEndFrame,cs,totalClipF):totalClipF;
            int dur=ActionTiming.Duration(configFile,seg); float xS=seg.startFrame*pixelsPerFrame, xE=(seg.startFrame+dur)*pixelsPerFrame;
            Rect cr=new Rect(r.x+xS,r.y+lane*laneH+2,xE-xS,laneH-4);
            bool sel=selType==SelType.AnimSegment&&selIdx==si;
            EditorGUI.DrawRect(cr,sel?Color.Lerp(C_ANIM,Color.white,.25f):C_ANIM);
            if(sel) DrawOutline(cr,Color.white);

            // ── 绘制融合区域可视化 ──
            if(seg.blendInFrames>0){
                float blendPx=seg.blendInFrames*pixelsPerFrame;
                blendPx=Mathf.Min(blendPx,cr.width); // 不超过片段本身宽度
                // 融合渐变区域（左侧渐变色块）
                Rect blendRect=new Rect(cr.x,cr.y,blendPx,cr.height);
                // 绘制多条渐变条纹模拟渐变效果
                Color blendColor=new Color(0.2f,0.8f,1f,0.35f);
                int stripes=Mathf.Max(3,Mathf.FloorToInt(blendPx/2f));
                for(int s=0;s<stripes;s++){
                    float t=(float)s/stripes;
                    float alpha=Mathf.Lerp(0.45f,0.05f,t);
                    float sx=blendRect.x+t*blendRect.width;
                    float sw=blendRect.width/stripes+1f;
                    EditorGUI.DrawRect(new Rect(sx,blendRect.y,sw,blendRect.height),new Color(0.2f,0.8f,1f,alpha));
                }
                // 融合区域右边界分割线
                EditorGUI.DrawRect(new Rect(cr.x+blendPx-1f,cr.y,2f,cr.height),new Color(0.2f,0.8f,1f,0.7f));
                // 融合帧数标签
                GUI.Label(new Rect(cr.x+2,cr.y+cr.height-13,blendPx-4,12),$"{seg.blendInFrames}f",EditorStyles.miniLabel);
            }

            string lb=$"{seg.clip.name} ({cs}-{ce}, {(ActionTiming.SourceEnd(seg)-ActionTiming.SourceStart(seg)):F2}s)";
            GUI.Label(new Rect(cr.x+4,cr.y+1,cr.width-8,cr.height),lb,TSty.Clip);
            // 边缘手柄
            const float EW=6f;
            Rect le=new Rect(cr.x-EW/2,cr.y,EW,cr.height), re=new Rect(cr.xMax-EW/2,cr.y,EW,cr.height);
            EditorGUI.DrawRect(new Rect(cr.x,cr.y,2,cr.height),new Color(1,1,1,.4f));
            EditorGUI.DrawRect(new Rect(cr.xMax-2,cr.y,2,cr.height),new Color(1,1,1,.4f));
            EditorGUIUtility.AddCursorRect(le,MouseCursor.SplitResizeLeftRight);
            EditorGUIUtility.AddCursorRect(re,MouseCursor.SplitResizeLeftRight);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0){
                if(le.Contains(e.mousePosition)){ isDraggingSegEdge=true; segEdgeIdx=si; segEdgeSide=0;
                    segOrigClipStart=cs; segOrigClipEnd=ce; segOrigTimelineStart=seg.startFrame; e.Use(); }
                else if(re.Contains(e.mousePosition)){ isDraggingSegEdge=true; segEdgeIdx=si; segEdgeSide=1;
                    segOrigClipStart=cs; segOrigClipEnd=ce; segOrigTimelineStart=seg.startFrame; e.Use(); }
                else if(cr.Contains(e.mousePosition)){
                    selType=SelType.AnimSegment; selIdx=si;
                    isDraggingSegment=true; _segmentGrabFrame=(e.mousePosition.x-r.x)/pixelsPerFrame-seg.startFrame; dragSegIdx=si; segOrigTimelineStart=seg.startFrame; e.Use(); }
            }
            if(e.type==EventType.ContextClick&&cr.Contains(e.mousePosition)){
                int idx=si; var menu=new GenericMenu();
                menu.AddItem(new GUIContent("Copy"),false,()=>{ selType=SelType.AnimSegment; selIdx=idx; CopySelected(); });
                if(HasClipboard&&_clipboardType==SelType.AnimSegment) menu.AddItem(new GUIContent($"Paste {GetClipboardLabel()} at frame {frameSelectIndex}"),false,PasteClipboard);
                else menu.AddDisabledItem(new GUIContent("Paste"));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"Remove clip [{seg.clip.name}]"),false,()=>{ RemoveEvent(animSegments,idx); if(selType==SelType.AnimSegment){selType=SelType.None;selIdx=-1;} });
                menu.AddItem(new GUIContent("Reset source trim"),false,()=>{ PushUndo(); animSegments[idx].clipStartFrame=0; animSegments[idx].clipEndFrame=0; });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Blend over 5 frames"),false,()=>{ AutoSetBlendFrames(idx,5); });
                menu.AddItem(new GUIContent("Blend over 8 frames"),false,()=>{ AutoSetBlendFrames(idx,8); });
                menu.AddItem(new GUIContent("Clear blend"),false,()=>{ PushUndo(); animSegments[idx].blendInFrames=0; });
                menu.ShowAsContext(); e.Use(); }
        }
        if(animSegments.Count==0) GUI.Label(new Rect(r.x+10,r.y+4,300,16),"Use Add or drop an AnimationClip here.",TSty.Item);
        HandleAnimClipDrop(r);
    }
    void HandleAnimClipDrop(Rect r){
        var e=Event.current;
        if((e.type==EventType.DragUpdated||e.type==EventType.DragPerform)&&r.Contains(e.mousePosition)){
            bool has=false; foreach(var o in DragAndDrop.objectReferences) if(o is AnimationClip){has=true;break;}
            if(has){ DragAndDrop.visualMode=DragAndDropVisualMode.Copy;
                if(e.type==EventType.DragPerform){ DragAndDrop.AcceptDrag();
                    PushUndo();
                    int df=Mathf.Max(0,Mathf.RoundToInt((e.mousePosition.x-r.x)/pixelsPerFrame));
                    foreach(var o in DragAndDrop.objectReferences){ if(o is AnimationClip c){
                        var seg=new Global.AnimClipSegment{clip=c,startFrame=df}; animSegments.Add(seg); df+=ActionTiming.Duration(configFile,seg); } }
                } e.Use(); }
        }
    }

    /// <summary>为指定索引的 segment 设置融合帧数（仅对排序后非第一段的 segment 有效）</summary>
    void AutoSetBlendFrames(int idx,int blendFrames){
        if(idx<0||idx>=animSegments.Count) return;
        PushUndo();
        var seg=animSegments[idx];
        int sortedIdx=GetSortedSegIndex(seg);
        if(sortedIdx<=0){ seg.blendInFrames=0; return; } // 第一段不融合
        int maxBlend=Mathf.Min(ActionTiming.Duration(configFile,seg),20);
        seg.blendInFrames=Mathf.Clamp(blendFrames,0,maxBlend);
    }

    /// <summary>
    /// 一键自动优化所有动画片段的拼接：
    /// 1. 按 startFrame 排序
    /// 2. 自动消除片段间的间隙（前一段结束帧=后一段起始帧）
    /// 3. 为非第一段自动设置融合帧数
    /// </summary>
    void AutoOptimizeAllSegments(int defaultBlendFrames=5){
        if(animSegments.Count<2) return;
        PushUndo();
        // 按 startFrame 排序
        animSegments.Sort((a,b)=>a.startFrame.CompareTo(b.startFrame));
        // 消除间隙 + 设置融合
        for(int i=1;i<animSegments.Count;i++){
            var prev=animSegments[i-1]; var cur=animSegments[i];
            // 让后一段紧贴前一段（消除间隙或重叠）
            int prevEnd=prev.startFrame+ActionTiming.Duration(configFile,prev);
            if(cur.startFrame!=prevEnd) cur.startFrame=prevEnd;
            // 计算合理的融合帧数：取 defaultBlendFrames 和片段 Duration 的较小值
            int maxBlend=Mathf.Min(ActionTiming.Duration(configFile,cur)/2,ActionTiming.Duration(configFile,prev)/2);
            maxBlend=Mathf.Max(maxBlend,1);
            cur.blendInFrames=Mathf.Clamp(defaultBlendFrames,0,maxBlend);
        }
        // 第一段不融合
        animSegments[0].blendInFrames=0;
    }

    /// <summary>优化拼接弹出菜单</summary>
    void ShowOptimizeMenu(){
        var menu=new GenericMenu();
        menu.AddItem(new GUIContent("Arrange / Short blend (3 frames)"),false,()=>{ AutoOptimizeAllSegments(3); Repaint(); });
        menu.AddItem(new GUIContent("Arrange / Medium blend (5 frames)"),false,()=>{ AutoOptimizeAllSegments(5); Repaint(); });
        menu.AddItem(new GUIContent("Arrange / Long blend (8 frames)"),false,()=>{ AutoOptimizeAllSegments(8); Repaint(); });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Close gaps without blending"),false,()=>{ AutoOptimizeAllSegments(0); Repaint(); });
        menu.AddItem(new GUIContent("Clear all blends"),false,()=>{ PushUndo(); foreach(var s in animSegments) s.blendInFrames=0; Repaint(); });
        menu.ShowAsContext();
    }

    // ═══════════════════════════════════════════════════════════
    //  通用事件轨道绘制 — 替代原先 10 个几乎完全相同的 DrawXxxTrackMultiLane
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 绘制一条 lane 式事件轨道：矩形块 + 标签 + 选中/拖拽/右键菜单。
    /// </summary>
    /// <param name="getLabel">(item, begin, end) → 块内文字</param>
    /// <param name="filter">仅绘制满足条件的条目（特效/音效共用一个列表时用到）</param>
    /// <param name="endExclusive">true=结束帧不占格（连招窗口），false=结束帧占满一格</param>
    /// <param name="alwaysLabel">true=无论缩放都画标签</param>
    void DrawEventTrack<T>(Rect r,Dictionary<int,int> lanes,int laneCount,
        List<T> list,TrackDef def,
        Func<T,int> getBegin,Func<T,int> getEnd,Func<T,int,int,string> getLabel,
        Func<T,bool> filter=null,bool endExclusive=false,bool alwaysLabel=false){
        float laneH=laneCount>0?r.height/laneCount:r.height;
        var e=Event.current;
        for(int i=0;i<list.Count;i++){
            var item=list[i];
            if(filter!=null&&!filter(item)) continue;
            int lane; if(!lanes.TryGetValue(i,out lane)) lane=0;
            int begin=getBegin(item),end=getEnd(item);
            float xs=begin*pixelsPerFrame, xe=(endExclusive?end:end+1)*pixelsPerFrame;
            Rect ir=new Rect(r.x+xs,r.y+lane*laneH+2,Mathf.Max(xe-xs,8),laneH-4);
            bool sel=selType==def.selType&&selIdx==i;
            EditorGUI.DrawRect(ir,sel?Color.Lerp(def.color,Color.white,.3f):def.color);
            if(sel) DrawOutline(ir,Color.white);
            if(alwaysLabel||pixelsPerFrame>6)
                GUI.Label(new Rect(ir.x+2,ir.y,ir.width-4,14),getLabel(item,begin,end),TSty.Item);
            if(e.type==EventType.MouseDown&&e.button==0&&ir.Contains(e.mousePosition)){
                selType=def.selType; selIdx=i; frameSelectIndex=begin;
                StartDrag(def.selType,i,begin,end); e.Use(); }
            if(e.type==EventType.ContextClick&&ir.Contains(e.mousePosition)){
                ShowEventContextMenu(def,i,begin); e.Use(); }
        }
    }

    /// <summary>时间轴上单个 Event 的右键菜单（复制 / 粘贴 / 删除）。</summary>
    void ShowEventContextMenu(TrackDef def,int idx,int frame,string extraLabel=null){
        var menu=new GenericMenu();
        menu.AddItem(new GUIContent("Copy"),false,()=>{ selType=def.selType; selIdx=idx; CopySelected(); });
        if(CanPasteToTrack(def.type))
            menu.AddItem(new GUIContent($"Paste {GetClipboardLabel()} at frame {frameSelectIndex}"),false,PasteClipboard);
        else menu.AddDisabledItem(new GUIContent("Paste"));
        menu.AddSeparator("");
        string tail=extraLabel??$"(frame {frame})";
        menu.AddItem(new GUIContent($"Remove {def.itemName} {tail}"),false,()=>{
            def.removeAt(idx); selType=SelType.None; selIdx=-1; });
        menu.ShowAsContext();
    }

    void DrawHitFxTrack(Rect r){
        float itemH=TRACK_HEIGHT;
        for(int i=0;i<hitFxList.Count;i++){
            var hf=hitFxList[i];
            Rect ir=new Rect(r.x+4,r.y+i*itemH+2,r.width-8,itemH-4);
            bool sel=selType==SelType.HitFx&&selIdx==i;
            EditorGUI.DrawRect(ir,sel?Color.Lerp(C_HITFX,Color.white,.3f):C_HITFX);
            if(sel) DrawOutline(ir,Color.white);
            string atkLabel=hf.attackIndex>=0?$"ATK[{hf.attackIndex}]":"All attacks";
            string vfxLabel=hf.hitVfx!=null?hf.hitVfx.name:"No VFX";
            string sndLabel=hf.hitSound!=null?"":"";
            GUI.Label(new Rect(ir.x+4,ir.y+1,ir.width-8,14),$"{atkLabel} {vfxLabel} {sndLabel}",TSty.Item);
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&ir.Contains(e.mousePosition)){
                selType=SelType.HitFx;selIdx=i; e.Use(); }
            if(e.type==EventType.ContextClick&&ir.Contains(e.mousePosition)){
                ShowEventContextMenu(GetDefBySel(SelType.HitFx),i,0,$"[{atkLabel}]"); e.Use(); }
        }
    }


    // 位移曲线峰值缓存：曲线未变时不重复 100 次 Evaluate
    float _moveCurvePeak=-1f; int _moveCurveHash;

    float GetMoveCurvePeak(AnimationCurve cv){
        int hash=cv.keys.Length;
        for(int i=0;i<cv.keys.Length;i++){
            var k=cv.keys[i];
            hash=hash*31+k.time.GetHashCode();
            hash=hash*31+k.value.GetHashCode();
        }
        if(_moveCurvePeak>0f&&hash==_moveCurveHash) return _moveCurvePeak;
        float mx=.01f; for(float t=0;t<=1;t+=.01f) mx=Mathf.Max(mx,Mathf.Abs(cv.Evaluate(t)));
        _moveCurveHash=hash; _moveCurvePeak=mx;
        return mx;
    }

    void DrawMoveTrack(Rect r){
        if(configFile==null||configFile.moveCurve==null) return;
        int totalF=GetTotalFrames();
        var cv=configFile.moveCurve; if(cv.keys.Length<2) return;
        float mx=GetMoveCurvePeak(cv);
        float zy=r.y+r.height*.5f;
        EditorGUI.DrawRect(new Rect(r.x,zy,totalF*pixelsPerFrame,1),new Color(1,1,1,.1f));
        int seg=Mathf.Min(totalF*3,400); Vector3 prev=Vector3.zero;
        for(int i=0;i<=seg;i++){
            float n=(float)i/seg; float v=cv.Evaluate(n);
            float x=r.x+n*totalF*pixelsPerFrame;
            float y=zy-(v/mx)*(r.height*.4f);
            var pt=new Vector3(x,y);
            if(i>0){ Handles.color=C_MOVE; Handles.DrawLine(prev,pt); }
            prev=pt;
        }
        GUI.Label(new Rect(r.x+4,r.y+2,140,14),$"Distance: {configFile.totalMoveDistance:F1}",TSty.Item);
        if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&r.Contains(Event.current.mousePosition)){
            selType=SelType.Move;selIdx=-1; Event.current.Use(); }
    }
    #endregion

    #region 分割线
    void DrawSplitter(Rect r){
        EditorGUI.DrawRect(r,new Color(.1f,.1f,.1f));
        EditorGUI.DrawRect(new Rect(r.x,r.y+r.height/2-.5f,r.width,1),new Color(.4f,.4f,.4f));
        EditorGUIUtility.AddCursorRect(r,MouseCursor.SplitResizeUpDown);
        if(Event.current.type==EventType.MouseDown&&r.Contains(Event.current.mousePosition)){ isDraggingSplitter=true; Event.current.Use(); }
        if(isDraggingSplitter){
            if(Event.current.type==EventType.MouseDrag){ inspectorHeight-=Event.current.delta.y;
                inspectorHeight=Mathf.Clamp(inspectorHeight,INSPECTOR_MIN_HEIGHT,position.height*.6f); Repaint(); Event.current.Use(); }
            if(Event.current.type==EventType.MouseUp){ isDraggingSplitter=false; Event.current.Use(); }
        }
    }

    void DrawVerticalSplitter(Rect r){
        EditorGUI.DrawRect(r,new Color(.1f,.1f,.1f));
        EditorGUI.DrawRect(new Rect(r.x+r.width/2-.5f,r.y,1,r.height),new Color(.4f,.4f,.4f));
        EditorGUIUtility.AddCursorRect(r,MouseCursor.ResizeHorizontal);
        if(Event.current.type==EventType.MouseDown&&r.Contains(Event.current.mousePosition)){ isDraggingVerticalSplitter=true; Event.current.Use(); }
        if(isDraggingVerticalSplitter){
            if(Event.current.type==EventType.MouseDrag){
                inspectorWidth-=Event.current.delta.x;
                inspectorWidth=Mathf.Clamp(inspectorWidth,INSPECTOR_MIN_WIDTH,INSPECTOR_MAX_WIDTH);
                Repaint(); Event.current.Use();
            }
            if(Event.current.type==EventType.MouseUp){ isDraggingVerticalSplitter=false; Event.current.Use(); }
        }
    }
    #endregion
}
