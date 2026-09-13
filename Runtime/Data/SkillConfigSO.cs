using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Action (行为/技能) 配置 — 对应主流 ACT 编辑器中的一个 Action。
/// 包含基本信息、TimeLine 轨道布局、以及各类 Event/Command 数据列表。
/// </summary>
public class SkillConfigSO : ScriptableObject
{
    [SerializeField, HideInInspector] int actionEditorSchemaVersion;
    [Tooltip("0 preserves legacy timing; 1 uses an explicit action timeline rate. Use the backed-up migration for existing actions.")]
    public int timingVersion;
    [Tooltip("Optional inclusive phase labels. These do not change event boundaries or action duration.")]
    public List<Ethan.ActionEditor.ActionPhaseMarker> phases = new List<Ethan.ActionEditor.ActionPhaseMarker>();
    [Min(1)] public float timelineFrameRate = 30f;
    public bool UsesExplicitTiming => timingVersion == 1;
    public int RuntimeEndFrame => exitFrame > 0 ? exitFrame : UsesExplicitTiming ? Ethan.ActionEditor.ActionTiming.ContentEnd(this) : TotalTimelineFrames;

    public void InitializeExplicitTiming(float framesPerSecond = 30f)
    {
        timingVersion = 1;
        timelineFrameRate = framesPerSecond;
    }

    public int ActionEditorSchemaVersion => actionEditorSchemaVersion;

    public void SetActionEditorSchemaVersion(int version)
    {
        actionEditorSchemaVersion = Mathf.Max(0, version);
    }

    [Header("Action identity")]
    public int skillID;             // 技能ID
    public string skillName;        // 技能名称
    [TextArea] public string skillDescription; // 技能描述
    public int skillType;           // 技能类型 (0:普攻, 1:技能, 2:被动 等)
    public string previewModelName; // 仅用于编辑器预览的名字记录

    [Header("Actor profile")]
    [Tooltip("Host metadata identifying the actor profile. All authoring tracks are available to players and enemies.")]
    public Global.SkillOwnerType ownerType = Global.SkillOwnerType.Player;

    [Header("Action parameters")]
    public float skillCD;           // 技能CD

    [Header("Cancel priority")]
    [Tooltip("Higher-priority actions may cancel lower-priority actions according to host rules.")]
    public Global.CancelPriority cancelPriority = Global.CancelPriority.Lv0;

    [Header("Host combat rules")]
    [Tooltip("Activation energy cost checked by the host before playback.")]
    public float energyCostOnActivate = 0f;

    [Tooltip("Default energy gain on a confirmed hit; individual attacks can override it.")]
    public float defaultEnergyGainOnHit = 0f;

    [Header("Animation timeline")]
    [Tooltip("Animation segments evaluated in start-frame order.")]
    public List<Global.AnimClipSegment> animSegments = new List<Global.AnimClipSegment>();

    [Header("Legacy animation fields")]
    public AnimationClip skillClip; // 核心动画片段（如果 animSegments 为空则使用此字段）
    public int exitFrame = 0; // 0表示播放完才退出，>0表示到达该帧就退出

    [Header("Motion")]
    [Tooltip("Displacement segments. When present they replace the legacy whole-action curve. " +
             "Each segment curve uses local normalized time, unlike the legacy whole-action curve.")]
    public List<Global.MoveSegment> moveSegmentList = new List<Global.MoveSegment>();

    // ── 向后兼容：旧的整技能单曲线位移。仅当 moveSegmentList 为空时生效，二者永不叠加。──
    [Tooltip("Legacy cumulative distance curve over the whole action; this is distance, not speed. " +
             "Used only when there are no displacement segments.")]
    public AnimationCurve moveCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));

    [Tooltip("Legacy distance paired with the whole-action curve; used only without displacement segments.")]
    public float totalMoveDistance = 0f;

    [Header("Timeline layout")]
    [Tooltip("Visible track layout, ordering and expansion. Hiding a track does not disable its events.")]
    public List<Global.SkillTrack> tracks = new List<Global.SkillTrack>();

    [Header("Timeline events")]
    // 各类指令数据：连招、攻击判定、特效、音效、取消点、投掷物、受击特效
    public List<Global.Jump> jumpList = new List<Global.Jump>();
    public List<Global.Attack> attackList = new List<Global.Attack>();
    public List<Ethan.ActionEditor.ActionCameraCue> cameraCues = new List<Ethan.ActionEditor.ActionCameraCue>();
    public List<Global.FxAndSound> fxList = new List<Global.FxAndSound>();
    public List<Global.CancelPoint> cancelList = new List<Global.CancelPoint>();
    public List<Global.Projectile> projectileList = new List<Global.Projectile>();
    public List<Global.HitFx> hitFxList = new List<Global.HitFx>();
    [Header("Interaction windows")]
    public List<Ethan.ActionEditor.InteractionWindow> interactionWindows = new List<Ethan.ActionEditor.InteractionWindow>();

    [Header("Interruption")]
    [Tooltip("First frame allowing host movement interruption. Zero means any time; minus one disables it.")]
    public int moveCancelFrame = -1;

    // ═══════════════════════════════════════════════════════════
    //  敌人专用配置
    // ═══════════════════════════════════════════════════════════
    [Header("Actor-specific rules")]
    [Tooltip("Telegraph flashes or sounds before an attack; available to any actor.")]
    public List<Global.WarningCue> warningCueList = new List<Global.WarningCue>();

    [Tooltip("Armor windows evaluated by the host.")]
    public List<Global.SuperArmorSegment> superArmorList = new List<Global.SuperArmorSegment>();

    [Tooltip("Target tracking and facing-lock windows for any actor.")]
    public List<Global.AdjustMotionSegment> adjustMotionList = new List<Global.AdjustMotionSegment>();

    [Tooltip("Trail activation windows; resolved by host presentation.")]
    public List<Global.TrailToggle> trailToggleList = new List<Global.TrailToggle>();


    [Tooltip("Legacy whole-action armor flag. Host rules may derive it from armor windows.")]
    public bool isSuperArmorSkill = false;

    /// <summary>
    /// 查询指定帧是否处于霸体状态
    /// </summary>
    public bool IsFrameInSuperArmor(int frame)
    {
        if (superArmorList == null) return false;
        for (int i = 0; i < superArmorList.Count; i++)
        {
            var sa = superArmorList[i];
            int end = sa.endKeyNumber > sa.keyNumber ? sa.endKeyNumber : sa.keyNumber;
            if (frame >= sa.keyNumber && frame <= end) return true;
        }
        return false;
    }

    /// <summary>
    /// 获取指定帧的霸体等级（-1=无霸体，0=L1，1=L2）
    /// </summary>
    public int GetSuperArmorLevel(int frame)
    {
        if (superArmorList == null) return -1;
        int maxLevel = -1;
        for (int i = 0; i < superArmorList.Count; i++)
        {
            var sa = superArmorList[i];
            int end = sa.endKeyNumber > sa.keyNumber ? sa.endKeyNumber : sa.keyNumber;
            if (frame >= sa.keyNumber && frame <= end)
                maxLevel = Mathf.Max(maxLevel, sa.armorLevel);
        }
        return maxLevel;
    }

    /// <summary>
    /// 查询指定帧是否处于朝向调整状态
    /// </summary>
    public bool IsFrameInAdjustMotion(int frame)
    {
        if (adjustMotionList == null) return false;
        for (int i = 0; i < adjustMotionList.Count; i++)
        {
            var am = adjustMotionList[i];
            int end = am.endKeyNumber > am.keyNumber ? am.endKeyNumber : am.keyNumber;
            if (frame >= am.keyNumber && frame <= end) return true;
        }
        return false;
    }

    /// <summary>
    /// 获取指定帧的朝向调整旋转速度（-1=不在调整范围内）
    /// </summary>
    public float GetAdjustMotionSpeed(int frame)
    {
        if (adjustMotionList == null) return -1f;
        for (int i = 0; i < adjustMotionList.Count; i++)
        {
            var am = adjustMotionList[i];
            int end = am.endKeyNumber > am.keyNumber ? am.endKeyNumber : am.keyNumber;
            if (frame >= am.keyNumber && frame <= end) return am.rotationSpeed;
        }
        return -1f;
    }

    // ═══════════════════════════════════════════════════════════
    //  二阶段技能进化（覆盖/附加配置）
    // ═══════════════════════════════════════════════════════════
    [Header("Phase variants")]
    [Tooltip("Additional phase-two attacks, combined with the base attacks.")]
    public List<Global.Attack> phase2AttackList = new List<Global.Attack>();

    [Tooltip("Additional phase-two effects and audio.")]
    public List<Global.FxAndSound> phase2FxList = new List<Global.FxAndSound>();

    [Tooltip("Host phase-two hit-shape scale; 1.5 expands the base shape by fifty percent.")]
    public float phase2RangeMultiplier = 1.0f;

    // ═══════════════════════════════════════════════════════════
    //  便利属性
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取有效的动画片段列表。如果 animSegments 有数据则直接返回；
    /// 否则从旧字段 skillClip 生成一段。
    /// </summary>
    public List<Global.AnimClipSegment> GetEffectiveSegments()
    {
        if (animSegments != null && animSegments.Count > 0) return animSegments;
        if (skillClip != null)
        {
            return new List<Global.AnimClipSegment>{
                new Global.AnimClipSegment{ clip = skillClip, startFrame = 0 }
            };
        }
        return new List<Global.AnimClipSegment>();
    }

    /// <summary>
    /// 所有动画片段的总帧数（Timeline 总长度）
    /// </summary>
    public int TotalTimelineFrames {
        get {
            if (UsesExplicitTiming) return Mathf.Max(60, Ethan.ActionEditor.ActionTiming.ContentEnd(this));
            int max = 0;
            var segs = GetEffectiveSegments();
            foreach (var s in segs) {
                int end = s.EndFrame;
                if (end > max) max = end;
            }
            // 至少60帧
            return Mathf.Max(max, 60);
        }
    }
}
