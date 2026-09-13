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

/// <summary>
/// ACT 技能动作编辑器 — Action + TimeLine + Event/Command 架构
/// 
/// 布局：顶部工具栏 | 左侧轨道标签(Track Header)+右侧时间轴(TimeLine) | 底部属性检查器(Inspector)
/// 
/// - Action: 一个 SkillConfigSO 对应一个行为
/// - TimeLine: 由多条 Track 组成，每条 Track 承载一类 Event
/// - Event/Command: 轨道上的具体指令（攻击、特效、音效、连招窗口等）
/// </summary>
public partial class SkillEditorWindow : EditorWindow
{
    #region 常量与颜色
    const float TOOLBAR_HEIGHT = 82f;
    const float TRACK_HEADER_WIDTH = 224f;
    const float TRACK_HEIGHT = 30f;
    const float TRACK_HEIGHT_CURVE = 55f;
    const float RULER_HEIGHT = 22f;
    const float INSPECTOR_MIN_HEIGHT = 180f;
    const float SPLITTER_HEIGHT = 5f;
    const float PLAYHEAD_WIDTH = 2f;
    const float MIN_PPF = 4f;
    const float MAX_PPF = 40f;
    const float DEF_PPF = 12f;
    const float ADD_BTN_HEIGHT = 22f;
    const int MIN_TOTAL_FRAMES = 60;
    const float WIDE_LAYOUT_THRESHOLD = 1120f;
    const float INSPECTOR_MIN_WIDTH = 320f;
    const float INSPECTOR_MAX_WIDTH = 520f;

    static readonly Color C_ANIM  = new Color(.30f,.60f,.90f,.85f);
    static readonly Color C_ATK   = new Color(.90f,.30f,.25f,.85f);
    static readonly Color C_FX    = new Color(.30f,.85f,.45f,.85f);
    static readonly Color C_SND   = new Color(.25f,.70f,.85f,.85f);
    static readonly Color C_JUMP  = new Color(.95f,.75f,.20f,.85f);
    static readonly Color C_MOVE  = new Color(.70f,.50f,.90f,.85f);
    static readonly Color C_WARN  = new Color(1f,.50f,0f,.85f);
    static readonly Color C_CANCEL= new Color(.90f,.45f,.70f,.85f);
    static readonly Color C_PROJ  = new Color(.20f,.80f,.95f,.85f);
    static readonly Color C_HITFX = new Color(.95f,.55f,.85f,.85f);
    static readonly Color C_SA    = new Color(1f,.65f,.15f,.85f);  // SuperArmor 霸体轨道 — 橙金色
    static readonly Color C_AM    = new Color(.45f,.95f,.65f,.85f); // AdjustMotion 朝向调整 — 绿色
    static readonly Color C_TRAIL = new Color(.60f,.85f,1f,.85f);   // Trail 刀光轨道 — 冰蓝色
    static readonly Color C_BG    = new Color(.18f,.18f,.18f);
    static readonly Color C_TRK   = new Color(.22f,.22f,.22f);
    static readonly Color C_TRK2  = new Color(.25f,.25f,.25f);
    static readonly Color C_GRID  = new Color(1,1,1,.05f);
    static readonly Color C_GRIDM = new Color(1,1,1,.12f);
    static readonly Color C_HEAD  = new Color(1f,.35f,.2f,1f);
    #endregion

    #region 编辑数据
    public SkillConfigSO configFile;
    int skillID=>configFile!=null?configFile.skillID:0;
    public string skillName=>configFile!=null?configFile.skillName:string.Empty;
    public string skillDescription=>configFile!=null?configFile.skillDescription:string.Empty;
    public int skillTypeSelectIndex=>configFile!=null?configFile.skillType:0;
    string[] skillTypeArray = {"Basic Attack","Active Ability","Passive Ability"};
    public float skillCD=>configFile!=null?configFile.skillCD:0f;
    public float energyCostOnActivate=>configFile!=null?configFile.energyCostOnActivate:0f;
    public float defaultEnergyGainOnHit=>configFile!=null?configFile.defaultEnergyGainOnHit:0f;
    public Global.SkillOwnerType ownerType=>configFile!=null?configFile.ownerType:Global.SkillOwnerType.Player;
    public Global.CancelPriority cancelPriority=>configFile!=null?configFile.cancelPriority:Global.CancelPriority.Lv0;
    public bool isSuperArmorSkill=>configFile!=null&&configFile.isSuperArmorSkill;
    string[] cancelPriorityArray={"Lv0","Lv1","Lv2","Lv3","Lv4","Lv5","Lv6","Lv7","Lv8","Lv9","Uninterruptible"};
    string[] impactLevelArray={"Light","Medium","Heavy"};
    string[] enemyAttackTypeArray={"Normal Attack","Armored Attack"};
    string[] damageModeArray={"Single Hit","Repeated Hits"};
    string[] skillShapeArray={"Sphere","Box"};
    public int skillShapeSelectIndex;
    public float range1,range2,offsetX,offsetY,offsetZ;
    List<Global.Jump> jumpList=new List<Global.Jump>();
    List<Global.Attack> atkList=new List<Global.Attack>();
    List<Global.FxAndSound> fxAndSoundList=new List<Global.FxAndSound>();
    List<Global.WarningCue> warningCueList=new List<Global.WarningCue>();
    List<Global.CancelPoint> cancelList=new List<Global.CancelPoint>();
    List<Global.Projectile> projectileList=new List<Global.Projectile>();
    List<Global.HitFx> hitFxList=new List<Global.HitFx>();
    List<Global.SuperArmorSegment> superArmorList=new List<Global.SuperArmorSegment>();
    List<Global.AdjustMotionSegment> adjustMotionList=new List<Global.AdjustMotionSegment>();
    List<Global.TrailToggle> trailToggleList=new List<Global.TrailToggle>();
    List<Global.MoveSegment> moveSegmentList=new List<Global.MoveSegment>();
    List<ActionCameraCue> cameraCues=new List<ActionCameraCue>();
    List<InteractionWindow> interactionWindows=new List<InteractionWindow>();

    // ── 多段动画 ──
    List<Global.AnimClipSegment> animSegments = new List<Global.AnimClipSegment>();
    // ── 动态轨道 ──
    List<Global.SkillTrack> trackList = new List<Global.SkillTrack>();
    #endregion

    #region 预览
    public GameObject previewModel, animSourceModel;
    Animator previewAnim;
    GameObject _previewInstanceRoot,_previewInstance;
    // ── 多FX预览实例支持 ──
    struct PreviewFxEntry { public GameObject go; public float duration; public float maxLifetime; public float speed; }
    List<PreviewFxEntry> _previewFxInstances=new List<PreviewFxEntry>();
    HashSet<Global.FxAndSound> _spawnedFxIndices=new HashSet<Global.FxAndSound>();
    // ── 投掷物预览实例支持 ──
    struct PreviewProjEntry { public GameObject go; public Vector3 velocity; public float elapsed; public float lifetime; }
    List<PreviewProjEntry> _previewProjInstances=new List<PreviewProjEntry>();
    AnimationClip[] currentClips;
    public AudioSource as1;
    #endregion

    #region Timeline状态
    float pixelsPerFrame=DEF_PPF;
    Vector2 timelineScrollPos;
    int frameSelectIndex,last_frameSelectIndex;
    bool playFrame; float playTimer,frameRate=.033f;
    double m_LastEditorTime;
    float inspectorHeight=250f;
    float inspectorWidth=400f;
    bool isDraggingSplitter,isDraggingVerticalSplitter,isDraggingPlayhead,isDraggingItem;
    SelType dragType=SelType.None; int dragIdx=-1,dragOrigF,dragOrigEnd;
    // Segment 拖拽
    bool isDraggingSegment; int dragSegIdx=-1;
    bool isDraggingSegEdge; int segEdgeIdx=-1, segEdgeSide;
    int segOrigClipStart,segOrigClipEnd,segOrigTimelineStart;
    enum SelType{None,Attack,Fx,Sound,Jump,Warning,Cancel,Basic,Hitbox,Move,Enemy,AnimSegment,Projectile,HitFx,SuperArmor,AdjustMotion,TrailToggle,Phase2Attack,Phase2Fx,Interaction,Camera}
    SelType selType=SelType.None; int selIdx=-1;
    Vector2 inspScrollPos;

    SerializedObject _serializedConfig;
    readonly List<ActionValidationIssue> _validationIssues=new List<ActionValidationIssue>();
    int _validationErrorCount,_validationWarningCount;
    #endregion


    #region SceneGUI
    bool isDrawAtk,isRewriteAtk;
    IJudgeArea _IJudgeArea; Judgment _judgment=new Judgment();
    BoxBoundsHandle boxHandle=new BoxBoundsHandle();
    SphereBoundsHandle sphereHandle=new SphereBoundsHandle();
    BoxItem boxItem=new BoxItem(); SphereItem sphereItem=new SphereItem();
    // ── 自动跟踪骨骼生成判定框 ──
    Transform _trackBoneRef;   // 选中的骨骼参考点
    string _trackBonePath="";  // 骨骼路径缓存
    bool _showBoneOffsetsPreview; // Inspector中偏移数据折叠
    #endregion

    [MenuItem("Tools/ACT Action Editor/Open Editor")]
    static void Open(){ var w=GetWindow<SkillEditorWindow>("ACT Action Editor"); w.minSize=new Vector2(900,600); w.Show(); }

    void OnEnable(){
        if(Camera.main!=null) as1=Camera.main.GetComponent<AudioSource>();
        SceneView.duringSceneGui+=OnSceneGUICallback;
        m_LastEditorTime=EditorApplication.timeSinceStartup;
        EditorApplication.update+=OnEditorUpdate;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        Undo.undoRedoPerformed+=OnUnityUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload+=StopPreviewSession;
        if(!Application.isPlaying&&!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
        EnsureSceneLabelStyles();
        // ★ 域重载后从 SO 重新加载数据，防止空列表覆盖有效配置
        if(configFile!=null) LoadConfig();
    }
    void OnDisable(){
        _editGesture.Complete();
        CommitDeferredChanges();
        SceneView.duringSceneGui-=OnSceneGUICallback;
        EditorApplication.update-=OnEditorUpdate;
        EditorApplication.playModeStateChanged-=OnPlayModeChanged;
        Undo.undoRedoPerformed-=OnUnityUndoRedo;
        AssemblyReloadEvents.beforeAssemblyReload-=StopPreviewSession;
        CleanPreviewFx(); DestroyPreviewInstance();
        if(AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
    }
    void OnPlayModeChanged(PlayModeStateChange state){
        switch(state){
            case PlayModeStateChange.ExitingEditMode:
                // 即将进入 PlayMode：停止预览，避免与运行时动画冲突
                StopPreviewSession();
                CleanOrphanedPreviewFx(); // 兜底清理域重载后丢失引用的残留 FX
                if(AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                break;
            case PlayModeStateChange.EnteredEditMode:
                // 回到编辑模式：清理可能在 Play 模式中残留的预览 FX
                CleanOrphanedPreviewFx();
                if(!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                // ★ Play Mode 域重载后重新加载数据，防止空列表覆盖
                if(configFile!=null) LoadConfig();
                break;
        }
    }
    bool IsPreviewAllowed=>!Application.isPlaying&&_validationErrorCount==0;

    void OnUnityUndoRedo(){
        var selectedType=selType;
        int selectedIndex=selIdx;
        int selectedFrame=frameSelectIndex;
        isDraggingItem=isDraggingSegment=isDraggingSegEdge=false;
        if(configFile!=null) LoadConfig();
        selType=selectedType;
        var def=GetDefBySel(selType);
        selIdx=def?.listCount!=null?Mathf.Clamp(selectedIndex,-1,def.listCount()-1):selectedIndex;
        frameSelectIndex=Mathf.Clamp(selectedFrame,0,GetTotalFrames());
        RefreshValidation();
        Repaint();
        SceneView.RepaintAll();
    }

    #region 主GUI
    void OnGUI(){
        _totalFramesCache=-1; // 每次重绘重算一次总帧数，帧内复用
        DrawToolbar();
        if(configFile==null){ GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox("Create a new action or select an existing asset to begin.",MessageType.None);
            GUILayout.FlexibleSpace(); return; }
        if(ActionConfigMigrationService.NeedsMigration(configFile)){
            GUILayout.Space(28);
            EditorGUILayout.HelpBox($"{configFile.name} uses a legacy schema and is read-only. Choose Tools > Upgrade schema with backup to edit it.",MessageType.None);
            EditorGUILayout.ObjectField("Action asset",configFile,typeof(SkillConfigSO),false);
            DrawReadOnlyConfig();
            return;
        }
        PrepareUndoForCurrentEvent();
        float tb=TOOLBAR_HEIGHT, total=position.height-tb;
        if(position.width>=WIDE_LAYOUT_THRESHOLD){
            float insW=Mathf.Clamp(inspectorWidth,INSPECTOR_MIN_WIDTH,INSPECTOR_MAX_WIDTH);
            float tlW=position.width-insW-SPLITTER_HEIGHT;
            DrawTimelineArea(new Rect(0,tb,tlW,total));
            DrawVerticalSplitter(new Rect(tlW,tb,SPLITTER_HEIGHT,total));
            DrawInspectorPanel(new Rect(tlW+SPLITTER_HEIGHT,tb,insW,total));
        }else{
            float insH=Mathf.Clamp(inspectorHeight,INSPECTOR_MIN_HEIGHT,total*.6f);
            float tlH=total-insH-SPLITTER_HEIGHT;
            DrawTimelineArea(new Rect(0,tb,position.width,tlH));
            DrawSplitter(new Rect(0,tb+tlH,position.width,SPLITTER_HEIGHT));
            DrawInspectorPanel(new Rect(0,tb+tlH+SPLITTER_HEIGHT,position.width,insH));
        }
        HandleGlobalInput();
        // 动画采样只在 Repaint 执行一次，避免 Layout/Repaint 双事件重复采样
        if(Event.current.type==EventType.Repaint) SampleAnim();
    }
    #endregion














    #region 辅助
    /// <summary>
    /// 计算拖入的 Transform 相对于角色根节点的路径（用于 firePointPath 自动填充）。
    /// 优先查找预览模型的层级；如果拖入的物体不是预览模型的子物体，则尝试向上遍历
    /// 到 Animator 根节点来构建路径。
    /// </summary>
    string ComputeRelativePath(Transform target){
        if(target==null) return null;
        // 优先检查是否属于预览模型的层级
        var eff=GetEffectivePreviewModel();
        Transform root=eff!=null?eff.transform:null;
        if(root!=null&&target.IsChildOf(root)){
            return BuildPathFromRoot(root,target);
        }
        // 不属于预览模型 → 向上找 Animator 根节点（适用于直接从场景拖入的情况）
        Transform walk=target;
        while(walk.parent!=null){
            if(walk.parent.GetComponent<Animator>()!=null){
                return BuildPathFromRoot(walk.parent,target);
            }
            walk=walk.parent;
        }
        // 最后尝试从场景根到 target 的完整父级链（root 是最顶层非 target 的父级）
        if(target.parent!=null){
            // 找到最顶层有 Animator 的节点，或者退回到 target 的直接父级
            Transform animRoot=target;
            while(animRoot.parent!=null) animRoot=animRoot.parent;
            if(animRoot!=target) return BuildPathFromRoot(animRoot,target);
        }
        return null;
    }
    string BuildPathFromRoot(Transform root,Transform target){
        if(target==root) return string.Empty;
        var parts=new List<string>();
        Transform cur=target;
        while(cur!=null&&cur!=root){ parts.Add(cur.name); cur=cur.parent; }
        if(cur!=root) return null; // target 不是 root 的子物体
        parts.Reverse();
        return string.Join("/",parts);
    }
    void CleanPreviewFx(){ foreach(var e in _previewFxInstances) if(e.go!=null) DestroyImmediate(e.go); _previewFxInstances.Clear(); _spawnedFxIndices.Clear(); CleanPreviewProj(); }
    /// <summary>
    /// 兜底清理：查找场景中所有带 EditorPreviewFxTag 的残留预览 FX 并销毁。
    /// 用于域重载后 _previewFxInstances 列表被清空但 DontSave 对象仍残留的情况。
    /// </summary>
    void CleanOrphanedPreviewFx(){
        var orphans=Resources.FindObjectsOfTypeAll<EditorPreviewFxTag>();
        foreach(var tag in orphans){
            if(tag!=null&&tag.gameObject!=null) DestroyImmediate(tag.gameObject);
        }
    }
    void LoadClipsFromAsset(){
        if(animSourceModel==null) return; var ap=AssetDatabase.GetAssetPath(animSourceModel);
        var objs=AssetDatabase.LoadAllAssetsAtPath(ap); var cl=new List<AnimationClip>();
        foreach(var o in objs) if(o is AnimationClip c) cl.Add(c); currentClips=cl.ToArray();
    }
    void PlayFxAndSound(int prevFrame,int curFrame){
        // 遍历所有 FX，(prevFrame, curFrame] 范围内的全部触发（支持同帧多个 FX）
        for(int i=0;i<fxAndSoundList.Count;i++){
            var f=fxAndSoundList[i];
            if(f.particleSystem==null&&f.audioClip==null) continue;
            int begin=f.keyNumber;
            int end=f.endKeyNumber>f.keyNumber?f.endKeyNumber:f.keyNumber;
            // 检查 (prevFrame, curFrame] 是否与 [begin, end] 有交集
            if(curFrame<begin||prevFrame>=end) continue;
            // 已触发过的不重复创建
            if(_spawnedFxIndices.Contains(f)) continue;
            _spawnedFxIndices.Add(f);
            if(f.particleSystem!=null) SpawnPreviewFx(f);
            if(f.audioClip!=null&&as1!=null) as1.PlayOneShot(f.audioClip);
        }
    }

    /// <summary>在编辑器预览中生成特效：Instantiate 完整副本，由 FxUpdate 增量 Simulate 驱动</summary>
    void SpawnPreviewFx(Global.FxAndSound t){
        var srcGo=t.particleSystem.gameObject;
        var fxGo=Instantiate(srcGo);
        if(fxGo==null) return;
        fxGo.hideFlags=HideFlags.DontSave|HideFlags.HideInHierarchy;
        // 添加标识组件，用于在域重载后查找并清理残留的预览 FX
        fxGo.AddComponent<EditorPreviewFxTag>().hideFlags=HideFlags.DontSave|HideFlags.HideInInspector;

        // ── 设置位置和旋转 ──
        if(previewModel!=null){
            EnsurePreviewInstance(); var eff=GetEffectivePreviewModel();
            if(eff!=null){
                if(t.useWorldSpace){
                    fxGo.transform.position=t.offset;
                    fxGo.transform.rotation=Quaternion.Euler(t.rotation);
                }else{
                    fxGo.transform.position=eff.transform.TransformPoint(t.offset);
                    fxGo.transform.rotation=eff.transform.rotation*Quaternion.Euler(t.rotation);
                }
                if(t.followCharacter&&!t.useWorldSpace) fxGo.transform.parent=eff.transform;
            }
        }

        // ── 设置缩放 ──
        fxGo.transform.localScale=t.scale;

        // ── 播放速率 & 计算最大存活时间 ──
        float spd=Mathf.Max(t.playbackSpeed,0.01f);
        float maxLife=0f;
        foreach(var p in fxGo.GetComponentsInChildren<ParticleSystem>()){
            float dur=p.main.duration+p.main.startLifetime.constantMax;
            if(dur>maxLife) maxLife=dur;
            p.Simulate(0,false,true,false);
        }
        if(maxLife<=0f) maxLife=5f;
        if(t.customLifetime>0f) maxLife=t.customLifetime;
        maxLife/=spd;
        _previewFxInstances.Add(new PreviewFxEntry{go=fxGo,duration=0f,maxLifetime=maxLife,speed=spd});
    }

    /// <summary>在编辑器预览中生成派生提示特效：复用 PreviewFxEntry 系统驱动 Simulate</summary>
    void SpawnPreviewDerivePrompt(Global.Jump j){
        if(j.derivePromptVfx==null) return;
        var srcGo=j.derivePromptVfx.gameObject;
        var fxGo=Instantiate(srcGo);
        if(fxGo==null) return;
        fxGo.hideFlags=HideFlags.DontSave|HideFlags.HideInHierarchy;
        fxGo.AddComponent<EditorPreviewFxTag>().hideFlags=HideFlags.DontSave|HideFlags.HideInInspector;

        // ── 设置位置 ──
        if(previewModel!=null){
            EnsurePreviewInstance(); var eff=GetEffectivePreviewModel();
            if(eff!=null){
                fxGo.transform.position=eff.transform.TransformPoint(j.derivePromptOffset);
                fxGo.transform.rotation=eff.transform.rotation;
                if(j.derivePromptFollow) fxGo.transform.parent=eff.transform;
            }
        }

        // ── 设置缩放 ──
        if(j.derivePromptScale!=Vector3.one&&j.derivePromptScale!=Vector3.zero)
            fxGo.transform.localScale=j.derivePromptScale;

        // ── 计算最大存活时间 ──
        float maxLife=0f;
        foreach(var p in fxGo.GetComponentsInChildren<ParticleSystem>()){
            float dur=p.main.duration+p.main.startLifetime.constantMax;
            if(dur>maxLife) maxLife=dur;
            p.Simulate(0,false,true,false);
        }
        if(maxLife<=0f) maxLife=5f;
        _previewFxInstances.Add(new PreviewFxEntry{go=fxGo,duration=0f,maxLifetime=maxLife,speed=1f});
    }

    int FindKeyIndex(int f){ for(int i=0;i<atkList.Count;i++){ var a=atkList[i]; int end=a.endKeyNumber>a.keyNumber?a.endKeyNumber:a.keyNumber; if(f>=a.keyNumber&&f<=end) return i; } return -1; }
    /// <summary>收集当前帧所有活跃（在开始帧~结束帧范围内）的攻击索引</summary>
    List<int> FindActiveAttacks(int f){
        var result=new List<int>();
        for(int i=0;i<atkList.Count;i++){
            var a=atkList[i]; int end=a.endKeyNumber>a.keyNumber?a.endKeyNumber:a.keyNumber;
            if(f>=a.keyNumber&&f<=end) result.Add(i);
        }
        return result;
    }
    bool CheckKey(int i)=>i>=0&&i<atkList.Count;
    bool CheckKeyRewrite(int i)=>CheckKey(i)&&atkList[i].isReWrite;
    void DrawOutline(Rect r,Color c,float t=1){
        EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,t),c);
        EditorGUI.DrawRect(new Rect(r.x,r.yMax-t,r.width,t),c);
        EditorGUI.DrawRect(new Rect(r.x,r.y,t,r.height),c);
        EditorGUI.DrawRect(new Rect(r.xMax-t,r.y,t,r.height),c);
    }
    #endregion
}

#region 样式
static class TSty{
    static GUIStyle _tl,_rl,_cl,_il,_ba,_ab;
    public static GUIStyle TrkLbl{ get{ if(_tl==null){ _tl=new GUIStyle(EditorStyles.label); _tl.fontSize=11; _tl.normal.textColor=new Color(.85f,.85f,.85f); _tl.fontStyle=FontStyle.Bold; } return _tl; } }
    public static GUIStyle Ruler{ get{ if(_rl==null){ _rl=new GUIStyle(EditorStyles.miniLabel); _rl.fontSize=9; _rl.normal.textColor=new Color(.7f,.7f,.7f); } return _rl; } }
    public static GUIStyle Clip{ get{ if(_cl==null){ _cl=new GUIStyle(EditorStyles.miniLabel); _cl.fontSize=10; _cl.normal.textColor=Color.white; _cl.fontStyle=FontStyle.Bold; _cl.alignment=TextAnchor.MiddleLeft; _cl.clipping=TextClipping.Clip; } return _cl; } }
    public static GUIStyle Item{ get{ if(_il==null){ _il=new GUIStyle(EditorStyles.miniLabel); _il.fontSize=9; _il.normal.textColor=new Color(1,1,1,.9f); _il.clipping=TextClipping.Clip; } return _il; } }
    public static GUIStyle BtnAct{ get{ if(_ba==null){ _ba=new GUIStyle(EditorStyles.miniButton); _ba.normal.textColor=new Color(1f,.35f,.2f); _ba.fontStyle=FontStyle.Bold; } return _ba; } }
    /// <summary>轨道头部 [+] 添加 Event 按钮样式 — 白色粗体，居中</summary>
    public static GUIStyle AddBtn{ get{ if(_ab==null){ _ab=new GUIStyle(EditorStyles.miniButton); _ab.fontSize=13; _ab.fontStyle=FontStyle.Bold; _ab.normal.textColor=new Color(1f,1f,1f,.95f); _ab.alignment=TextAnchor.MiddleCenter; _ab.padding=new RectOffset(0,0,0,2); } return _ab; } }
}
#endregion

#region 辅助绘图类
namespace AtkJudge{
    public interface IJudgeArea{ void SetValue(float s1,float s2,float s3,float ox,float oy,float oz); }
    public class BoxItem:IJudgeArea{ public Vector3 offset,size;
        public void SetValue(float s1,float s2,float s3,float ox,float oy,float oz){ size=new Vector3(s1,s2,s3); offset=new Vector3(ox,oy,oz); } }
    public class SphereItem:IJudgeArea{ public Vector3 offset; public float radius;
        public void SetValue(float s1,float s2,float s3,float ox,float oy,float oz){ radius=s1; offset=new Vector3(ox,oy,oz); } }
}
public class Judgment{ public AtkJudge.IJudgeArea value; }
public class HandlesDrawTool:DrawTool{
    public static HandlesDrawTool H=new HandlesDrawTool();
    public override Color color{ get=>Handles.color; set=>Handles.color=value; }
    public override void DrawLine(Vector3 s,Vector3 e)=>Handles.DrawLine(s,e);
    protected override void FillPolygon(Vector3[] v)=>Handles.DrawAAConvexPolygon(v);
}
public abstract class DrawTool{
    public static Color colorDefault=Color.white;
    public virtual Color color{get;set;} public bool isFill=false;
    Stack<Color> _stack=new Stack<Color>();
    public abstract void DrawLine(Vector3 s,Vector3 e);
    protected abstract void FillPolygon(Vector3[] v);
    public void PushColor(Color c){_stack.Push(color);color=c;}
    public void PopColor(){color=_stack.Count>0?_stack.Pop():colorDefault;}
    public void DrawBox(Vector3 size,Matrix4x4 m){
        Vector3[] p=MathTool.CalcBoxVertex(size,m); int[] idx=MathTool.GetBoxSurfaceIndex();
        for(int i=0;i<6;i++){ Vector3[] poly={p[idx[i*4]],p[idx[i*4+1]],p[idx[i*4+2]],p[idx[i*4+3]]};
            if(isFill) FillPolygon(poly); for(int k=0;k<4;k++) DrawLine(poly[k],poly[(k+1)%4]); } }
    public void DrawSphere(float r,Matrix4x4 m){ DrawCircle(r,m); DrawCircle(r,m*Matrix4x4.Rotate(Quaternion.Euler(90,0,0)));
        DrawCircle(r,m*Matrix4x4.Rotate(Quaternion.Euler(0,90,0))); }
    void DrawCircle(float r,Matrix4x4 m){ int sides=30; Vector3[] v=new Vector3[sides];
        for(int i=0;i<sides;i++){ float rad=(i*Mathf.PI*2)/sides; v[i]=m.MultiplyPoint(new Vector3(Mathf.Cos(rad)*r,Mathf.Sin(rad)*r,0)); }
        if(isFill) FillPolygon(v); for(int i=0;i<sides;i++) DrawLine(v[i],v[(i+1)%sides]); }
}
public static class MathTool{
    public static int[] GetBoxSurfaceIndex()=>new int[]{0,1,2,3, 4,5,6,7, 2,6,5,3, 0,4,7,1, 1,7,6,2, 0,3,5,4};
    public static Vector3[] CalcBoxVertex(Vector3 s,Matrix4x4 m){
        Vector3 h=s/2; Vector3[] p={new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,h.y,h.z),
            new Vector3(h.x,-h.y,h.z),new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z)};
        for(int i=0;i<8;i++) p[i]=m.MultiplyPoint(p[i]); return p; }
}
public static class GUIStyles{ public static GUIStyle item_selected="MeTransitionSelectHead",item_normal="MeTransitionSelect"; }
#endregion

/// <summary>
/// 空标识组件 — 挂在编辑器预览 FX 实例上，用于在域重载后查找并清理残留对象。
/// 不在 Inspector 中显示，不会保存到场景。
/// </summary>
[AddComponentMenu("")]  // 不出现在 Add Component 菜单
public class EditorPreviewFxTag : MonoBehaviour { }
