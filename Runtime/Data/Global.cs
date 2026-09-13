using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局数据定义 — ACT 技能编辑器
/// 
/// ═══════════════════════════════════════════════════════════════
///  架构说明 (Action + TimeLine + Event/Command)
/// ═══════════════════════════════════════════════════════════════
///
///  ┌─ Action (行为/技能) ─────────────────────────────────────┐
///  │  一个 SkillConfigSO 就是一个 Action，描述技能的完整行为。  │
///  │                                                          │
///  │  ┌─ TimeLine (时间轴) ─────────────────────────────────┐ │
///  │  │  由多条 Track (轨道) 组成，每条 Track 承载一类指令：  │ │
///  │  │                                                      │ │
///  │  │  Track: Animation  ── AnimClipSegment (动画片段)      │ │
///  │  │  Track: Attack     ── Attack Event    (攻击判定)      │ │
///  │  │  Track: Fx         ── FxAndSound Event(特效指令)      │ │
///  │  │  Track: Sound      ── FxAndSound Event(音效指令)      │ │
///  │  │  Track: Jump       ── Jump Event      (连招窗口)      │ │
///  │  │  Track: Move       ── MoveCurve       (位移曲线)      │ │
///  │  │  Track: Warning    ── WarningCue Event(警告指令)      │ │
///  │  │                                                      │ │
///  │  │  每种轨道类型唯一(不可重复创建同类型轨道)。            │ │
///  │  │  轨道内的 Event 可以有多条，重叠时自动分 lane 显示。   │ │
///  │  └──────────────────────────────────────────────────────┘ │
///  └──────────────────────────────────────────────────────────┘
///
/// </summary>
public class Global
{
    // ═══════════════════════════════════════════════════════════
    //  枚举定义
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 技能拥有者类型：用于区分玩家技能和敌人技能
    /// </summary>
    public enum SkillOwnerType
    {
        Player = 0,
        Enemy = 1
    }

    /// <summary>
    /// 轨道类型枚举 — 每条 Track 承载一类 Event/Command
    /// 在 TimeLine 中动态创建，每种类型唯一（一个 Action 内不可重复同类型轨道）。
    /// </summary>
    public enum TrackType
    {
        Animation  = 0,   // 动画轨道 — Event: AnimClipSegment（多段 Clip 拼接）
        Attack     = 1,   // 攻击轨道 — Event: Attack（攻击判定指令）
        Fx         = 2,   // 特效轨道 — Event: FxAndSound（特效指令）
        Sound      = 3,   // 音效轨道 — Event: FxAndSound（音效指令）
        Jump       = 4,   // 连招轨道 — Event: Jump（连招窗口指令）
        Move       = 5,   // 位移轨道 — Command: MoveCurve（位移曲线）
        Warning    = 6,   // 警告轨道 — Event: WarningCue（敌人攻击前警告指令）
        Cancel     = 7,   // 取消点轨道 — Event: CancelPoint（后摇取消窗口）
        Projectile = 8,   // 投掷物轨道 — Event: Projectile（飞行物/射击指令）
        HitFx      = 9,   // 受击特效轨道 — Event: HitFx（命中时在受击方播放的特效/音效）
        SuperArmor = 10,  // 霸体轨道 — Event: SuperArmorSegment（帧段内角色获得霸体）
        AdjustMotion = 11, // 朝向调整轨道 — Event: AdjustMotionSegment（帧段内持续面朝目标）
        Trail        = 12, // 刀光轨道 — Event: TrailToggle（刀光/拖尾开关控制）
        Camera       = 14,
        Interaction  = 13, // 可扩展交互窗口（追加数值，保留旧资产枚举身份）
    }

    /// <summary>
    /// 冲击力等级 — 对应设计文档中的轻/中/重三级
    /// 轻: 可打断敌人非攻击状态
    /// 中: 可打断敌人非霸体攻击
    /// 重: 可打断敌人霸体攻击并击退
    /// </summary>
    public enum ImpactLevel
    {
        Light = 0,   // 普攻
        Medium = 1,  // 特殊攻击前两段、分支派生第一段上踢
        Heavy = 2    // 特殊攻击最后一段、分支派生第二段
    }

    /// <summary>
    /// 取消优先级 — 纯数值层级系统，高层级可取消低层级技能。
    /// 不再绑定到具体动作类型（如"Basic Attack"/"Dodge"），由设计者自由分配。
    /// 典型分配示例：移动/跳跃=0, 普攻=1, 闪避=2, 特殊攻击=3, E技能=5
    /// </summary>
    public enum CancelPriority
    {
        Lv0 = 0,
        Lv1 = 1,
        Lv2 = 2,
        Lv3 = 3,
        Lv4 = 4,
        Lv5 = 5,
        Lv6 = 6,
        Lv7 = 7,
        Lv8 = 8,
        Lv9 = 9,
        Uncancellable = 99 // 不可被取消
    }

    /// <summary>
    /// 敌人攻击类型：普通攻击 vs 霸体攻击
    /// </summary>
    public enum EnemyAttackType
    {
        Normal = 0,      // 普通攻击（可被中/重冲击力打断）
        SuperArmor = 1   // 霸体攻击（仅重冲击力可打断）
    }

    /// <summary>
    /// 伤害模式 — 控制攻击判定在帧窗口内的触发方式。
    /// </summary>
    public enum DamageMode
    {
        /// <summary>一次性伤害：帧窗口内只要命中一次就标记完毕，不再重复判定（默认行为）。</summary>
        OneShot = 0,
        /// <summary>持续伤害：帧窗口内每隔 tickInterval 帧重复判定一次，可多次命中同一目标。</summary>
        Continuous = 1
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 跳转条件 (连招窗口指令)
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class Jump
    {
        public SkillConfigSO nextSkill; // 跳转的目标 Action（技能配置）
        public int beginKey;            // 指令生效开始帧
        public int endKey;              // 指令生效结束帧
        [Tooltip("Device-independent command ID interpreted by the host. Takes precedence over the legacy trigger key when nonempty.") ]
        public string triggerCommandId;
        public KeyCode triggerKey = KeyCode.None; // 触发按键
        public bool autoTrigger = false; // 自动衔接：到达帧范围自动跳转，无需按键

        [Tooltip("Animation transition blend duration in seconds. Zero uses the host default.")]
        public float fadeDuration = 0f;

        // ═══ 能量门槛 ═══
        [Header("Energy requirements")]
        [Tooltip("Minimum energy required by the host rules. Zero means no requirement.")]
        public float requiredEnergy = 0f;

        [Tooltip("Energy consumed by the host after the requirement succeeds. Zero means no cost.")]
        public float energyCost = 0f;

        [Tooltip("Request a slow-motion decision window when the transition triggers. Implemented by the host.")]
        public bool requestSlowPromptOnJump = false;

        // ═══ 派生提示特效 ═══
        [Header("Transition cue")]
        [Tooltip("Effect shown during a transition window when energy requirements are met.")]
        public ParticleSystem derivePromptVfx;
        [Tooltip("Local offset of the transition cue.")]
        public Vector3 derivePromptOffset = Vector3.zero;
        [Tooltip("Scale of the transition cue.")]
        public Vector3 derivePromptScale = Vector3.one;
        [Tooltip("Whether the cue follows the actor.")]
        public bool derivePromptFollow = true;
        [Tooltip("Optional audio when the cue appears.")]
        public AudioClip derivePromptSound;
        [Tooltip("Cue audio volume.")]
        [Range(0f, 1f)]
        public float derivePromptVolume = 1f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 攻击判定指令
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class Attack
    {
        [Header("Interaction filters")]
        public bool unblockable;
        public bool unparryable;
        public string interactionTag;
        public int keyNumber;       // 指令开始帧
        public int endKeyNumber;    // 指令结束帧（<=keyNumber 表示单帧触发）
        public bool isReWrite;      // 是否重写判定框参数
        public int shapeType;       // 0:球, 1:盒

        // 范围参数
        public float parameter1;    // 半径/长
        public float parameter2;    // 宽
        public Vector3 offset;      // 偏移

        [Header("Base damage")]
        public float damageRatio = 1.0f;     // 伤害倍率
        public float knockbackForce = 5.0f;  // 击退力度
        public bool autoAim = true;          // 攻击时是否自动吸附向敌人

        [Header("Damage mode")]
        [Tooltip("Single Hit: one hit within the frame window. Repeated Hits: query every tick interval.")]
        public DamageMode damageMode = DamageMode.OneShot;

        [Tooltip("Frame interval between repeated hit queries. Minimum one.")]
        public int tickInterval = 5;

        // ═══════════ Feel 打击感配置（每帧独立开关） ═══════════

        [Header("Hit stop")]
        [Tooltip("Request hit stop on a confirmed hit.")]
        public bool enableHitPause = true;
        [Tooltip("Hit stop duration in seconds; typical range 0.02 to 0.08.")]
        public float hitPauseDuration = 0.04f;

        [Header("Camera shake")]
        [Tooltip("Request camera shake.")]
        public bool enableCameraShake = true;
        [Tooltip("Shake duration in seconds.")]
        public float cameraShakeDuration = 0.15f;
        [Tooltip("Shake amplitude.")]
        public float cameraShakeAmplitude = 0.3f;
        [Tooltip("Shake frequency in Hz.")]
        public float cameraShakeFrequency = 40f;

        [Header("Chromatic aberration")]
        [Tooltip("Request chromatic aberration on a confirmed hit. Requires a host receiver.")]
        public bool enableChromaticAberration = false;
        [Tooltip("Chromatic effect duration in seconds.")]
        public float chromaticAberrationDuration = 0.12f;
        [Tooltip("Peak chromatic intensity from zero to one.")]
        public float chromaticAberrationIntensity = 0.6f;

        [Header("Impact and toughness")]
        [Tooltip("Impact tier used by the host interruption rules.")]
        public ImpactLevel impactLevel = ImpactLevel.Light;

        [Tooltip("Toughness damage applied by the host.")]
        public float toughnessDamage = 10f;

        [Header("Actor-specific rules")]
        [Tooltip("Legacy enemy attack classification.")]
        public EnemyAttackType enemyAttackType = EnemyAttackType.Normal;

        [Tooltip("Deprecated. Use impactLevel to select Light, Medium or Heavy hit reactions.")]
        [System.Obsolete("Use impactLevel for hit reactions.")]
        public bool causePlayerStagger = false;

        [Header("Host combat rules")]
        [Tooltip("Energy gained (positive) or spent (negative) on a confirmed hit.")]
        public float energyDeltaOnHit = 0f;

        [Tooltip("Request a slow-motion decision window on hit; the host checks energy.")]
        public bool requestSlowPromptOnHit = false;

        [Tooltip("Minimum energy required for the slow-motion request.")]
        public float slowPromptRequiredEnergy = 0f;

        [Tooltip("Request a glow cue on hit; implemented by the host presentation layer.")]
        public bool requestGlowPromptOnHit = false;

        // ═══════════ 命中特效/音效 (Hit Impact) ═══════════

        [Header("Hit effect")]
        [Tooltip("Particle effect played on the hit target.")]
        public ParticleSystem hitVfx;

        [Tooltip("Effect offset relative to the target.")]
        public Vector3 hitVfxOffset = Vector3.zero;

        [Tooltip("Effect Euler rotation.")]
        public Vector3 hitVfxRotation = Vector3.zero;

        [Tooltip("Hit effect scale.")]
        public Vector3 hitVfxScale = Vector3.one;

        [Tooltip("Playback speed multiplier; one is normal speed.")]
        public float hitVfxPlaybackSpeed = 1f;

        [Tooltip("Whether the effect follows the target.")]
        public bool hitVfxFollowTarget = false;

        [Tooltip("Use attacker orientation when enabled; otherwise use target orientation.")]
        public bool hitVfxUseAttackerRotation = true;

        [Header("Hit audio")]
        [Tooltip("Audio played on a confirmed hit.")]
        public AudioClip hitSound;

        [Tooltip("Hit volume from zero to one.")]
        [Range(0f, 1f)]
        public float hitSoundVolume = 1f;

        [Tooltip("Randomize hit audio.")]
        public bool hitSoundRandomize = false;

        [Tooltip("Pitch variation around the base value.")]
        [Range(0f, 0.5f)]
        public float hitSoundPitchVariation = 0.1f;

        [Tooltip("Volume variation around the base value.")]
        [Range(0f, 0.3f)]
        public float hitSoundVolumeVariation = 0.05f;

        // ═══════════ 骨骼跟踪偏移（判定框跟随武器/拳脚移动） ═══════════

        [Header("Bone tracking")]
        [Tooltip("Sample hit shape offsets per frame to follow a bone path.")]
        public bool useBoneTracking = false;

        [Tooltip("Offset zero belongs to the start frame; offset i belongs to start+i. Count must equal end-start+1.")]
        public List<Vector3> boneOffsets = new List<Vector3>();

        [Tooltip("Editor bone path relative to the actor root.")]
        public string trackBonePath = "";

        /// <summary>
        /// 获取指定全局帧号对应的偏移。
        /// 如果启用了骨骼跟踪且 boneOffsets 有数据，返回对应帧的偏移；
        /// 否则返回静态 offset。
        /// </summary>
        public Vector3 GetOffsetAtFrame(int frame)
        {
            if (!useBoneTracking || boneOffsets == null || boneOffsets.Count == 0)
                return offset;
            int idx = frame - keyNumber;
            if (idx < 0) idx = 0;
            if (idx >= boneOffsets.Count) idx = boneOffsets.Count - 1;
            return boneOffsets[idx];
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 特效/音效指令
    // ═══════════════════════════════════════════════════════════

    public enum EffectContentKind { Legacy = 0, VisualEffect = 1, Audio = 2 }

    [System.Serializable]
    public class FxAndSound
    {
        // Zero preserves existing assets, including events that contain both references.
        [HideInInspector] public EffectContentKind contentKind;
        public int keyNumber;                 // 指令开始帧
        public int endKeyNumber;              // 指令结束帧（<=keyNumber 表示单帧触发）
        public ParticleSystem particleSystem; // 特效预制体
        public Vector3 offset;                // 偏移
        public AudioClip audioClip;           // 音效
        public bool followCharacter = true;   // 特效是否跟随角色移动

        [Header("Effect transform")]
        [Tooltip("Effect Euler rotation.")]
        public Vector3 rotation = Vector3.zero;

        [Tooltip("Independent effect scale on each axis.")]
        public Vector3 scale = Vector3.one;

        [Tooltip("Particle playback multiplier; one is normal speed.")]
        public float playbackSpeed = 1f;

        [Tooltip("Use world coordinates instead of actor-local coordinates.")]
        public bool useWorldSpace = false;

        [Tooltip("Effect cleanup delay in seconds. Zero uses the particle duration.")]
        public float customLifetime = 0f;

        // ═══════════ 音频随机化（避免重复播放疲劳感） ═══════════

        [Header("Audio variation")]
        [Tooltip("Randomize audio pitch and volume.")]
        public bool audioRandomize = false;

        [Tooltip("Pitch is one plus or minus this value.")]
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.1f;

        [Tooltip("Volume is base volume plus or minus this value, clamped to zero through one.")]
        [Range(0f, 0.3f)]
        public float volumeVariation = 0.05f;

        [Tooltip("Base volume from zero to one.")]
        [Range(0f, 1f)]
        public float baseVolume = 1f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 受击特效/音效指令（命中时在受击方身上播放）
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class HitFx
    {
        [Tooltip("Attack event index; minus one applies to every attack.")]
        public int attackIndex = -1;

        [Header("Hit effect")]
        [Tooltip("Effect prefab played on the hit target.")]
        public ParticleSystem hitVfx;

        [Tooltip("Effect offset relative to the target.")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("Effect Euler rotation.")]
        public Vector3 rotation = Vector3.zero;

        [Tooltip("Effect scale.")]
        public Vector3 scale = Vector3.one;

        [Tooltip("Effect playback multiplier.")]
        public float playbackSpeed = 1f;

        [Tooltip("Whether the effect follows the target.")]
        public bool followTarget = false;

        [Tooltip("Use attacker orientation instead of target orientation.")]
        public bool useAttackerRotation = true;

        [Header("Hit audio")]
        [Tooltip("Audio played on a confirmed hit.")]
        public AudioClip hitSound;

        [Tooltip("Volume from zero to one.")]
        [Range(0f, 1f)]
        public float volume = 1f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 敌人攻击前警告指令
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class WarningCue
    {
        public int keyNumber;                  // 指令开始帧
        public int endKeyNumber;               // 指令结束帧（<=keyNumber 表示单帧触发）
        public AudioClip warningSound;         // 警告音效
        public ParticleSystem warningVfx;      // 警告特效（闪光）
        public Vector3 vfxOffset;              // 特效偏移
        public Vector3 vfxRotation;            // 特效旋转（欧拉角）
        public Vector3 vfxScale = Vector3.one; // 特效缩放
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 投掷物/射击指令
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 投掷物指令 — 在指定帧从角色发射飞行物（子弹/能量弹等）。
    /// 运行时由 CharacterSkillPlayer 在帧命中时 Instantiate 预制体并赋予速度。
    /// 投掷物预制体需挂载 ProjectileBehaviour 脚本处理飞行/碰撞/伤害。
    /// </summary>
    [System.Serializable]
    public class Projectile
    {
        public int keyNumber;                    // 发射帧（单帧触发）
        public int endKeyNumber;                 // 结束帧（endKeyNumber > keyNumber 表示连续发射范围，每帧一发）

        [Header("Projectile")]
        [Tooltip("Projectile prefab. The host provides its movement and collision receiver.")]
        public GameObject prefab;

        [Tooltip("Child path to the muzzle relative to the actor root. Empty uses spawn offset.")]
        public string firePointPath;

        [Tooltip("Local spawn offset used when no muzzle path is assigned.")]
        public Vector3 spawnOffset;

        [Tooltip("Launch speed in world units per second.")]
        public float speed = 20f;

        [Tooltip("Lifetime in seconds before host cleanup.")]
        public float lifetime = 3f;

        [Header("Damage")]
        [Tooltip("Damage multiplier.")]
        public float damageRatio = 1.0f;

        [Tooltip("Knockback strength.")]
        public float knockbackForce = 3.0f;

        [Tooltip("Impact tier.")]
        public ImpactLevel impactLevel = ImpactLevel.Light;

        [Header("Projectile scale")]
        [Tooltip("Scale applied to the spawned projectile; one preserves prefab scale.")]
        public Vector3 scale = Vector3.one;

        [Header("Projectile rotation")]
        [Tooltip("Additional Euler rotation relative to the flight direction, for prefab orientation correction.")]
        public Vector3 spawnRotation = Vector3.zero;

        [Header("Homing")]
        [Tooltip("Continuously track a target during flight.")]
        public bool tracking = false;

        [Tooltip("Homing duration in seconds from launch; zero tracks for the full lifetime.")]
        public float trackingDuration = 0f;

        [Tooltip("Homing turn speed in degrees per second.")]
        public float trackingTurnSpeed = 180f;

        [Tooltip("Delay before homing starts, in seconds.")]
        public float trackingDelay = 0f;

        [Header("Launch settings")]
        [Tooltip("Aim at the nearest enemy; otherwise use actor forward.")]
        public bool autoAim = true;

        [Tooltip("Number of projectiles per shot; values above one form a fan.")]
        public int shotCount = 1;

        [Tooltip("Fan spread angle in degrees, used for multiple projectiles.")]
        public float spreadAngle = 15f;

        [Header("Effects")]
        [Tooltip("Optional muzzle effect.")]
        public ParticleSystem muzzleVfx;

        [Tooltip("Optional launch audio.")]
        public AudioClip fireSound;

        [Header("Destructible projectile")]
        [Tooltip("Whether incoming attacks can destroy this projectile.")]
        public bool destroyable = false;

        [Tooltip("Optional destruction effect.")]
        public ParticleSystem destroyVfx;

        [Tooltip("Optional destruction audio.")]
        public AudioClip destroySound;

        [Header("Impact effect")]
        [Tooltip("Optional impact effect overriding the host projectile default.")]
        public ParticleSystem impactVfx;

        [Tooltip("Effect offset relative to the impact point.")]
        public Vector3 impactVfxOffset = Vector3.zero;

        [Tooltip("Impact effect Euler rotation.")]
        public Vector3 impactVfxRotation = Vector3.zero;

        [Tooltip("Impact effect scale.")]
        public Vector3 impactVfxScale = Vector3.one;

        [Tooltip("Playback multiplier; one is normal speed.")]
        public float impactVfxPlaybackSpeed = 1f;

        [Tooltip("Optional impact audio overriding the host projectile default.")]
        public AudioClip impactSound;

        [Tooltip("Impact volume from zero to one.")]
        [Range(0f, 1f)]
        public float impactSoundVolume = 1f;

        // ═══════════ 发射打击感（后坐力） ═══════════

        [Header("Launch shake")]
        [Tooltip("Request camera shake on launch.")]
        public bool fireShake = false;
        [Tooltip("Launch shake duration in seconds.")]
        public float fireShakeDuration = 0.1f;
        [Tooltip("Launch shake amplitude.")]
        public float fireShakeAmplitude = 0.2f;
        [Tooltip("Launch shake frequency in Hz.")]
        public float fireShakeFrequency = 40f;

        [Header("Launch chromatic effect")]
        [Tooltip("Request a chromatic flash on launch. Requires host presentation.")]
        public bool fireChromaticAberration = false;
        [Tooltip("Launch chromatic duration in seconds.")]
        public float fireChromaticDuration = 0.08f;
        [Tooltip("Peak launch chromatic intensity from zero to one.")]
        public float fireChromaticIntensity = 0.4f;

        [Header("Launch hit stop")]
        [Tooltip("Request a short action pause on launch.")]
        public bool fireFreeze = false;
        [Tooltip("Launch hit stop duration in seconds; typical range 0.01 to 0.03.")]
        public float fireFreezeDuration = 0.02f;

        // ═══════════ 命中打击感 ═══════════

        [Header("Impact hit stop")]
        [Tooltip("Request hit stop on impact.")]
        public bool hitFreeze = false;
        [Tooltip("Impact hit stop duration in seconds.")]
        public float hitFreezeDuration = 0.04f;

        [Header("Impact shake")]
        [Tooltip("Request shake on impact.")]
        public bool hitShake = false;
        [Tooltip("Impact shake duration in seconds.")]
        public float hitShakeDuration = 0.15f;
        [Tooltip("Impact shake amplitude.")]
        public float hitShakeAmplitude = 0.3f;
        [Tooltip("Impact shake frequency in Hz.")]
        public float hitShakeFrequency = 40f;

        [Header("Impact chromatic effect")]
        [Tooltip("Request a chromatic flash on impact.")]
        public bool hitChromaticAberration = false;
        [Tooltip("Impact chromatic duration in seconds.")]
        public float hitChromaticDuration = 0.12f;
        [Tooltip("Peak impact chromatic intensity from zero to one.")]
        public float hitChromaticIntensity = 0.6f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 取消点（后摇取消窗口）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 取消点指令 — 定义从 keyNumber 帧开始，取消优先级 >= minCancelPriority 的动作
    /// 可以取消当前动作的后摇。与连招（Jump）独立：
    /// - Jump：指定"Next action"，需要明确的 nextSkill
    /// - CancelPoint：只声明"First frame at which interruption is allowed."，任何满足优先级的动作都能取消
    /// 
    /// 优先级层级说明：
    /// 所有动作（移动/跳跃/闪避/攻击/E技能）的优先级统一由其 SkillConfigSO.cancelPriority 或
    /// CharacterSkillPlayer 上配置的 moveCancelPriority / jumpCancelPriority / dodgeCancelPriority 决定。
    /// 取消点只需设置 minCancelPriority 门槛，>= 门槛的任何动作都能取消。
    /// </summary>
    [System.Serializable]
    public class CancelPoint
    {
        public int keyNumber;               // 取消窗口开始帧
        public int endKeyNumber;            // 取消窗口结束帧（<=keyNumber 表示从此帧到动画结束）

        [Tooltip("Only actions at or above this priority may cancel the current recovery.")]
        public CancelPriority minCancelPriority = CancelPriority.Lv0;

        [Tooltip("Cancel blend duration in seconds. Zero uses the host default.")]
        public float fadeDuration = 0.1f;

        // ── 向后兼容：旧数据中的布尔开关保留序列化字段但不再使用 ──
        [HideInInspector] public bool allowMoveCancel = true;
        [HideInInspector] public bool allowDodgeCancel = true;
        [HideInInspector] public bool allowJumpCancel = true;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 动画片段（Animation Track 中的指令单元，支持多段拼接）
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class AnimClipSegment
    {
        public AnimationClip clip;        // 动画Clip
        public int startFrame;            // 在Timeline上的起始帧（全局帧号）
        public int clipStartFrame;        // clip内部裁剪起始帧（默认0）
        public int clipEndFrame;          // clip内部裁剪结束帧（0=使用完整clip）

        [Tooltip("Blend frames from the previous segment. Zero is a hard cut; applies from the second segment onward.")]
        public int blendInFrames = 0;     // 融合帧数（segment间CrossFade时长=blendInFrames/frameRate）

        /// <summary>此片段在 Timeline 上占据的帧数</summary>
        public int Duration {
            get {
                if (clip == null) return 0;
                int totalClipF = Mathf.Max(1, Mathf.FloorToInt(clip.length * clip.frameRate));
                int cs = Mathf.Clamp(clipStartFrame, 0, totalClipF);
                int ce = clipEndFrame > 0 ? Mathf.Clamp(clipEndFrame, cs, totalClipF) : totalClipF;
                return ce - cs;
            }
        }
        /// <summary>此片段在 Timeline 上的结束帧（不含）</summary>
        public int EndFrame => startFrame + Duration;
        /// <summary>融合时间(秒)，根据clip帧率计算</summary>
        public float BlendInDuration {
            get {
                if (blendInFrames <= 0) return 0f;
                float fr = (clip != null && clip.frameRate > 0) ? clip.frameRate : 30f;
                return blendInFrames / fr;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 霸体帧段（SuperArmor Track 中的指令单元）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 霸体帧段 — 在 keyNumber ~ endKeyNumber 帧范围内角色获得霸体。
    /// 替代原来的 isSuperArmorSkill 整体开关，支持更精细的帧段级霸体控制。
    /// </summary>
    [System.Serializable]
    public class SuperArmorSegment
    {
        public int keyNumber;          // 霸体开始帧
        public int endKeyNumber;       // 霸体结束帧（<=keyNumber 表示单帧）

        [Tooltip("Host armor tier: zero is L1, one is L2. Immunity and interruption are host rules.")]
        public int armorLevel = 0;

        [Tooltip("Optional armor tint for host presentation.")]
        public Color tintColor = new Color(1f, 0.6f, 0.2f, 1f);
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 朝向调整帧段（AdjustMotion Track 中的指令单元）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 朝向调整帧段 — 在 keyNumber ~ endKeyNumber 帧范围内，
    /// 敌人持续调整自己的朝向面向玩家（平滑旋转）。
    /// 用于让 Boss 攻击前摇/蓄力时能跟踪玩家移动方向。
    /// </summary>
    [System.Serializable]
    public class AdjustMotionSegment
    {
        [Tooltip("Allow target tracking in this window. Disable to lock facing; locks override overlapping turn windows.")]
        public bool allowTurning = true;
        public MoveTargetKind targetKind = MoveTargetKind.LockedTarget;
        public int keyNumber;           // 开始帧
        public int endKeyNumber;        // 结束帧（<=keyNumber 表示单帧）

        [Tooltip("Turn speed in degrees per second. Zero snaps to the target.")]
        public float rotationSpeed = 360f;

        [Tooltip("Restrict rotation to the vertical axis; otherwise rotate in 3D.")]
        public bool yAxisOnly = true;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 刀光/拖尾开关（Trail Track 中的指令单元）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 刀光/拖尾开关帧段 — 在 keyNumber ~ endKeyNumber 帧范围内启用指定的拖尾效果。
    /// 
    /// 支持两种方式（二选一）：
    /// 1. **ParticleSystem 直接引用**（推荐）— 在 Inspector 中直接拖入粒子特效，运行时通过 Play/Stop 控制。
    /// 2. **trailPath 路径查找**（旧方式）— 通过路径字符串定位子物体，支持 TrailRenderer.emitting 或 SetActive。
    /// 
    /// 优先级：particle 不为 null 时使用 particle 模式，否则走 trailPath 模式。
    /// </summary>
    [System.Serializable]
    public class TrailToggle
    {
        public int keyNumber;           // 开启帧
        public int endKeyNumber;        // 关闭帧（<=keyNumber 表示从此帧到动画结束保持开启）

        [Header("Particle binding")]
        [Tooltip("Optional particle reference. Host receivers decide how to resolve and control it; persistent actions should use actor-relative bindings.")]
        public ParticleSystem particle;

        [Header("Actor-relative trail path")]
        [Tooltip("Child path relative to the actor root. Ignored when a particle reference is assigned.")]
        public string trailPath = "";

        [Tooltip("Use TrailRenderer.emitting instead of toggling the GameObject. " +
                 "Emitting preserves existing trail segments; toggling the object hides them immediately. Applies to path bindings.")]
        public bool useEmitting = true;

        /// <summary>是否使用 particle 直接引用模式</summary>
        public bool UseParticleMode => particle != null;
    }


    // ═══════════════════════════════════════════════════════════
    //  Track: 轨道描述（TimeLine 中的一条轨道，承载同类型 Event）
    // ═══════════════════════════════════════════════════════════

    [System.Serializable]
    public class SkillTrack
    {
        public TrackType type;          // 轨道类型（决定承载哪种 Event）
        public string displayName;      // 自定义显示名称（可选，空则用默认名）
        public bool expanded = true;    // 是否展开
    }

    // ═══════════════════════════════════════════════════════════
    //  共鸣标记配置
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 共鸣标记运行时数据 — 挂载在被标记目标上的运行时状态。
    /// 不需要序列化，完全由运行时维护。
    /// </summary>
    public class ResonanceMark
    {
        /// <summary>当前层数（0=无标记）</summary>
        public int stacks = 0;
        /// <summary>标记刷新计时器（每次叠加重置）</summary>
        public float timer = 0f;

        /// <summary>清空标记</summary>
        public void Clear()
        {
            stacks = 0;
            timer = 0f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  蓄力射击阶段配置
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 蓄力射击的阶段参数 — 每个阶段定义蓄力达到该时间后的效果提升。
    /// </summary>
    [System.Serializable]
    public class ChargeShootStage
    {
        [Tooltip("Charge time needed to enter this stage, in seconds.")]
        public float chargeTime = 0.5f;

        [Tooltip("Damage multiplier for this stage.")]
        public float damageMultiplier = 1.5f;

        [Tooltip("Toughness damage multiplier for this stage.")]
        public float toughnessMultiplier = 1.5f;

        [Tooltip("Impact tier for this stage.")]
        public ImpactLevel impactLevel = ImpactLevel.Medium;

        [Tooltip("Knockback multiplier for this stage.")]
        public float knockbackMultiplier = 1.5f;
    }

    // ═══════════════════════════════════════════════════════════
    //  Event: 位移段（Move Track 中的指令单元）
    // ═══════════════════════════════════════════════════════════

    /// <summary>位移驱动方式 — 程序解算 vs 动画自带（root motion）</summary>
    public enum MoveDriveMode
    {
        /// <summary>程序位移：由曲线、方向、目标点等参数完整解算。</summary>
        Program = 0,
        /// <summary>动画位移：水平位移完全来自动画 root motion，程序只管重力/贴地/碰撞。</summary>
        Animation = 1
    }

    /// <summary>位移方向来源</summary>
    public enum MoveSpace
    {
        Forward = 0,          // 自身正前方
        TowardTarget = 1,     // 朝向目标
        AwayFromTarget = 2,   // 远离目标
        WorldDirection = 3,   // 指定世界方向（用 customDirection）
        LocalDirection = 4,   // 指定自身局部方向（用 customDirection）
        InputDirection = 5    // 玩家输入方向（无输入时回退 Forward）
    }

    /// <summary>位移中心 — 相对移动 vs 朝目标点收敛</summary>
    public enum MoveOrigin
    {
        /// <summary>以自身为中心：从当前位置沿解算方向的相对移动。</summary>
        Self = 0,
        /// <summary>以目标为中心：朝「目标位置+偏移」解算出的目标点收敛。</summary>
        Target = 1
    }

    /// <summary>位移目标来源。不可用时按回退链取下一个，全部不可用则退化为 Forward。</summary>
    public enum MoveTargetKind
    {
        None = 0,          // 不依赖目标
        LockedTarget = 1,  // 锁定目标 → 最近敌人 → null
        NearestEnemy = 2,  // 最近敌人 → null
        Player = 3         // 玩家（敌人技能用）
    }

    /// <summary>目标偏移量的参考空间</summary>
    public enum OffsetSpace
    {
        World = 0,       // 世界空间
        TargetLocal = 1  // 目标局部空间（随目标朝向旋转）
    }

    /// <summary>位移被约束裁剪后，剩余曲线增量的处理策略</summary>
    public enum OvershootPolicy
    {
        /// <summary>裁剪：本帧只应用允许的部分，后续帧继续尝试（默认，与旧实现行为一致）。</summary>
        Clamp = 0,
        /// <summary>停止：立即结束该段，放弃剩余位移。</summary>
        Stop = 1,
        /// <summary>继续：忽略该项约束继续位移。</summary>
        Continue = 2
    }

    /// <summary>
    /// 位移段 — Move 轨道上的一个指令单元。一条技能可含多段，各段参数独立。
    ///
    /// 与旧的 SkillConfigSO.moveCurve / totalMoveDistance 的关系：
    /// - moveSegmentList 非空时只走本类，旧字段被忽略；为空时只走旧字段。二者永不叠加。
    /// - 【重要】旧 moveCurve 的横轴是「整条技能归一化时间」，本类 curve 的横轴是
    ///   「段内归一化时间」（0=keyNumber 帧，1=endKeyNumber 帧）。二者语义不同，不可混用。
    /// - 纵轴含义相同：已完成位移量占该段总位移量的比例（累积值，非速度）。
    /// </summary>
    [System.Serializable]
    public class MoveSegment
    {
        public int keyNumber;       // 位移开始帧
        public int endKeyNumber;    // 位移结束帧（<=keyNumber 表示单帧瞬时位移）

        [Header("Drive mode")]
        [Tooltip("Program mode solves authored movement. Animation mode uses horizontal root motion; the host owns gravity and collision.")]
        public MoveDriveMode driveMode = MoveDriveMode.Program;

        // ═══════════ 动画驱动专用 ═══════════

        [Tooltip("Animation mode: horizontal root-motion scale. Zero removes horizontal motion without changing the pose.")]
        public float rootMotionScale = 1f;

        // ═══════════ 程序驱动专用 ═══════════

        [Header("Direction and target")]
        [Tooltip("Program mode: movement direction source.")]
        public MoveSpace space = MoveSpace.Forward;

        [Tooltip("Program mode: Self moves relatively; Target converges on a destination.")]
        public MoveOrigin origin = MoveOrigin.Self;

        [Tooltip("Program mode: target source. Missing targets use the resolver fallback, then actor forward.")]
        public MoveTargetKind target = MoveTargetKind.LockedTarget;

        [Tooltip("Program mode: recompute direction every frame. Disabled locks the initial direction.")]
        public bool resolveDirectionPerFrame = false;

        [Tooltip("Program mode: custom direction for World Direction or Local Direction.")]
        public Vector3 customDirection = Vector3.forward;

        [Header("Horizontal displacement")]
        [Tooltip("Normalized cumulative distance curve within this segment: time zero to one maps to the start and end frames.")]
        public AnimationCurve curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1));

        [Tooltip("Hard cap on cumulative horizontal distance. Zero produces no horizontal displacement.")]
        public float maxDistance = 0f;

        [Header("Destination constraints")]
        [Tooltip("Program mode: offset added to the target position.")]
        public Vector3 targetOffset = Vector3.zero;

        [Tooltip("Offset space; Target Local rotates with the target.")]
        public OffsetSpace offsetSpace = OffsetSpace.TargetLocal;

        [Tooltip("Stop at the destination even if the curve is unfinished; do not overshoot.")]
        public bool stopAtTargetOffset = false;

        [Tooltip("Arrival tolerance in world units to avoid oscillation near moving targets.")]
        public float arriveTolerance = 0.05f;

        [Tooltip("Minimum separation from the target. If already closer, toward-target motion is suppressed without pushing the actor backward.")]
        public float minKeepDistance = 0f;

        [Header("Vertical displacement")]
        [Tooltip("Normalized cumulative vertical curve within this segment.")]
        public AnimationCurve verticalCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0));

        [Tooltip("Total vertical displacement. Zero disables the vertical component.")]
        public float verticalDistance = 0f;

        [Header("Options")]
        [Tooltip("Align actor facing to displacement direction, subject to authored facing locks.")]
        public bool alignFacing = false;

        [Tooltip("Ignore gravity while this segment is active. Overlapping requests combine.")]
        public bool ignoreGravity = false;

        [Tooltip("Ignore actor collisions while retaining static geometry collision. Requires backend support.")]
        public bool ignoreActorCollision = false;

        [Tooltip("Policy for remaining curve distance after constraints clip movement.")]
        public OvershootPolicy overshootPolicy = OvershootPolicy.Clamp;

        /// <summary>该段在时间轴上的结束帧（endKeyNumber &lt;= keyNumber 时等于 keyNumber，即单帧段）</summary>
        public int SegmentEndFrame => endKeyNumber > keyNumber ? endKeyNumber : keyNumber;

        /// <summary>该段占据的帧数（单帧段为 0）</summary>
        public int FrameLength => SegmentEndFrame - keyNumber;

        /// <summary>指定全局帧号是否落在本段区间内（含首尾）</summary>
        public bool ContainsFrame(int frame) => frame >= keyNumber && frame <= SegmentEndFrame;

        /// <summary>把全局帧号换算成段内归一化时间 0~1。单帧段恒返回 1。</summary>
        public float Normalize(int frame)
        {
            int len = FrameLength;
            if (len <= 0) return 1f;
            return Mathf.Clamp01((frame - keyNumber) / (float)len);
        }
    }
}
