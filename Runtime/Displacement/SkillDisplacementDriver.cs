using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能位移驱动 —— 位移系统与角色播放器之间的唯一桥接。
///
/// 不持有 MonoBehaviour，只持接口（<see cref="IDisplacementBackend"/> / <see cref="ITargetResolver"/>），
/// 解算逻辑全部委托给纯静态的 <see cref="MoveSolver"/>，因此可脱离场景做单元测试。
///
/// 生命周期：
///   Begin(skill) → Tick(frame) ×N → Abort()/End
/// 中断与正常结束都走同一个 <see cref="Abort"/> 清理出口（design D4）。
/// </summary>
public class SkillDisplacementDriver
{
    readonly IDisplacementBackend _backend;
    readonly ITargetResolver _targetResolver;
    readonly Ethan.ActionEditor.IActionRootMotionSource _rootMotion;
    readonly Animator _animator;

    /// <summary>当前技能的位移段定义（null = 无位移）。</summary>
    List<Global.MoveSegment> _segments;

    /// <summary>每段的运行时状态（与 <see cref="_segments"/> 等长）。</summary>
    List<MoveSegmentState> _states;

    bool _active;
    bool _ignoreActorCollisionSet;
    bool _applyRootMotionSet;
    bool _savedApplyRootMotion;

    public bool IsActive => _active;
    public System.Func<int, bool> AllowFacing { get; set; }

    public SkillDisplacementDriver(IDisplacementBackend backend, ITargetResolver targetResolver)
        : this(backend, targetResolver, null, null) { }

    public SkillDisplacementDriver(IDisplacementBackend backend, ITargetResolver targetResolver,
        Ethan.ActionEditor.IActionRootMotionSource rootMotion, Animator animator)
    {
        _backend = backend;
        _targetResolver = targetResolver;
        _rootMotion = rootMotion;
        _animator = animator;
    }

    /// <summary>
    /// 开始一次技能的位移驱动。segments 为空时直接返回（走旧路径，本驱动不接管）。
    /// </summary>
    public void Begin(List<Global.MoveSegment> segments)
    {
        Abort();

        if (segments == null || segments.Count == 0) return;
        if (_backend == null || !_backend.CanMove) return;

        _segments = segments;
        _states = new List<MoveSegmentState>(segments.Count);
        for (int i = 0; i < segments.Count; i++)
            _states.Add(new MoveSegmentState());

        // 含动画驱动段时开启 root motion（Animator 级全局开关，记录原值段结束还原）。
        if (_animator != null && HasAnimationSegment())
        {
            _savedApplyRootMotion = _animator.applyRootMotion;
            _applyRootMotionSet = true;
            _animator.applyRootMotion = true;
        }
        // 动画段激活时接管根运动；纯程序段时保持 Animator 默认行为（根运动直接应用）。
        _rootMotion?.SetTakeover(HasAnimationSegment());
        _rootMotion?.Clear();

        _backend.BeginDisplacement();
        _active = true;
    }

    bool HasAnimationSegment()
    {
        for (int i = 0; i < _segments.Count; i++)
            if (_segments[i].driveMode == Global.MoveDriveMode.Animation) return true;
        return false;
    }

    /// <summary>
    /// 每帧解算并提交位移。返回本帧是否有位移发生。
    /// </summary>
    public bool Tick(int frame, Vector3 inputDirection)
    {
        if (!_active || _segments == null) return false;

        // 解析本帧目标（一次解析，所有段共享）。
        Global.MoveTargetKind targetKind = ResolveDominantTargetKind();
        Transform target = targetKind == Global.MoveTargetKind.None
            ? null
            : _targetResolver?.Resolve(targetKind);

        var ctx = BuildContext(inputDirection, target);

        Vector3 programDelta = Vector3.zero;
        Vector3 programVertical = Vector3.zero;
        bool hasAnimationSegment = false;
        bool ignoreActorCollision = false;
        // Determine the active animation owner before accumulating any program delta.
        for (int i = 0; i < _segments.Count; i++)
            if (_segments[i].driveMode == Global.MoveDriveMode.Animation && IsInRange(_segments[i], frame))
                hasAnimationSegment = true;

        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            var state = _states[i];
            if (state == null || state.finished) continue;

            if (seg.driveMode == Global.MoveDriveMode.Animation)
            {
                if (IsInRange(seg, frame)) ignoreActorCollision |= seg.ignoreActorCollision;
                // 动画段的水平位移由 root motion 提供，不在本驱动内解算。
                // 竖直分量仍由程序负责（重力/贴地），动画段自身不产生竖直位移。
                continue;
            }

            var result = MoveSolver.Solve(seg, ctx, frame, state);

            // 拆水平/竖直：动画驱动优先（design D8）——同帧存在动画段时程序段水平分量置零，竖直分量仍参与。
            Vector3 horizontal = result.delta;
            horizontal.y = 0f;
            float vertical = result.delta.y;

            if (hasAnimationSegment)
                horizontal = Vector3.zero;

            programDelta += horizontal;
            programVertical += new Vector3(0f, vertical, 0f);

            if (IsInRange(seg, frame)) ignoreActorCollision |= seg.ignoreActorCollision;
        }

        Vector3 total = programDelta + programVertical;

        // 动画驱动段：消费 root motion 缓冲并按 rootMotionScale 缩放。
        // 同帧多个动画段取最宽松缩放（首段）；root motion 是全局的，无法按段拆分。
        if (hasAnimationSegment && _rootMotion != null)
        {
            Vector3 rm = _rootMotion.ConsumeDelta();
            float scale = AnimationRootMotionScale(frame);
            Vector3 rmHorizontal = rm;
            rmHorizontal.y = 0f;
            total += rmHorizontal * scale;
        }
        else _rootMotion?.Clear(); // Never replay deltas accumulated outside an animation segment.

        // 提交后端（本帧一次 Move）。
        if (total.sqrMagnitude > 1e-8f)
            _backend.Move(total);

        // 对齐朝向（取任一需要对齐的活动段方向；简单起见用最后解算出的方向）。
        // alignFacing 由 MoveSolver 结果携带，但跨段合并后此处统一处理首个需要对齐的段。
        if (AllowFacing == null || AllowFacing(frame)) ApplyFacingOnce(frame, ctx);

        // 段级忽略重力/碰撞的提交（取逻辑或）。
        if (ignoreActorCollision)
        {
            _backend.SetActorCollisionIgnored(true);
            _ignoreActorCollisionSet = true;
        }
        else if (_ignoreActorCollisionSet)
        {
            _backend.SetActorCollisionIgnored(false);
            _ignoreActorCollisionSet = false;
        }

        return total.sqrMagnitude > 1e-8f;
    }

    /// <summary>
    /// 结束/中断：清空所有段累积量、还原碰撞忽略与 root motion 开关、归还后端控制权。
    /// 可被重复调用（中断路径可能多次触发）。
    /// </summary>
    public void Abort()
    {
        if (_ignoreActorCollisionSet)
        {
            _backend?.SetActorCollisionIgnored(false);
            _ignoreActorCollisionSet = false;
        }
        if (_applyRootMotionSet)
        {
            if (_animator != null) _animator.applyRootMotion = _savedApplyRootMotion;
            _applyRootMotionSet = false;
        }
        _rootMotion?.SetTakeover(false);
        _rootMotion?.Clear();
        if (_active)
        {
            _backend?.EndDisplacement();
        }
        _segments = null;
        _states = null;
        _active = false;
    }

    static bool IsInRange(Global.MoveSegment segment, int frame) => frame >= segment.keyNumber &&
        frame <= Mathf.Max(segment.keyNumber, segment.endKeyNumber);

    public bool IgnoresGravityAtFrame(int frame)
    {
        if (!_active || _segments == null) return false;
        foreach (var segment in _segments)
            if (segment.ignoreGravity && IsInRange(segment, frame)) return true;
        return false;
    }

    float AnimationRootMotionScale(int frame)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            if (seg.driveMode == Global.MoveDriveMode.Animation
                && IsInRange(seg, frame))
                return seg.rootMotionScale;
        }
        return 1f;
    }

    // ─── 内部 ──────────────────────────────────────────────────

    Global.MoveTargetKind ResolveDominantTargetKind()
    {
        // 任一活动段依赖目标即解析目标；优先采用第一个非 None 的目标来源。
        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            if (_states[i] != null && _states[i].finished) continue;
            if (seg.driveMode == Global.MoveDriveMode.Animation) continue;
            if (seg.target != Global.MoveTargetKind.None)
                return seg.target;
        }
        return Global.MoveTargetKind.None;
    }

    MoveSolveContext BuildContext(Vector3 inputDirection, Transform target)
    {
        var ctx = new MoveSolveContext
        {
            selfPosition = _backend.Transform.position,
            selfRotation = _backend.Transform.rotation,
            inputDirection = inputDirection,
            hasTarget = target != null,
        };
        if (target != null)
        {
            ctx.targetPosition = target.position;
            ctx.targetRotation = target.rotation;
        }
        return ctx;
    }

    void ApplyFacingOnce(int frame, in MoveSolveContext ctx)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            var state = _states[i];
            if (seg == null || state == null || state.finished) continue;
            if (seg.driveMode == Global.MoveDriveMode.Animation) continue;
            if (!seg.alignFacing) continue;

            var dir = state.direction;
            if (dir.sqrMagnitude > 1e-6f)
                _backend.SetFacing(dir);
            return;
        }
    }
}
