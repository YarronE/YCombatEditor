using UnityEngine;

/// <summary>
/// 位移解算的输入上下文 —— 全部是纯数值，不含 <see cref="Transform"/> 或 MonoBehaviour，
/// 使 <see cref="MoveSolver"/> 可以脱离场景做纯单元测试。
///
/// 目标解析（<see cref="ITargetResolver"/>）在驱动层完成后把结果填进来，解算层不关心目标从哪来。
/// </summary>
public struct MoveSolveContext
{
    /// <summary>角色当前世界位置。</summary>
    public Vector3 selfPosition;
    /// <summary>角色当前世界旋转。</summary>
    public Quaternion selfRotation;
    /// <summary>玩家方向输入（世界空间）。零向量表示无输入。</summary>
    public Vector3 inputDirection;

    /// <summary>本帧是否解析到了目标。</summary>
    public bool hasTarget;
    /// <summary>目标世界位置，<see cref="hasTarget"/> 为假时无意义。</summary>
    public Vector3 targetPosition;
    /// <summary>目标世界旋转，用于把目标局部空间的偏移量转到世界空间。</summary>
    public Quaternion targetRotation;

    public Vector3 SelfForward => selfRotation * Vector3.forward;
}

/// <summary>
/// 单个位移段的运行时状态 —— 由驱动层按段持有，中断时整体重置。
/// </summary>
public class MoveSegmentState
{
    /// <summary>该段是否已开始（方向是否已解算过一次）。</summary>
    public bool started;
    /// <summary>该段是否已结束（越界策略为停止、或已到达目标点、或已过结束帧）。</summary>
    public bool finished;
    /// <summary>缓存的水平位移方向（已归一化）。<c>resolveDirectionPerFrame</c> 为假时全段复用。</summary>
    public Vector3 direction;
    /// <summary>已应用的水平位移累积量（沿 <see cref="direction"/> 的有符号投影和）。</summary>
    public float appliedDistance;
    /// <summary>已应用的竖直位移累积量。</summary>
    public float appliedVertical;

    public void Reset()
    {
        started = false;
        finished = false;
        direction = Vector3.zero;
        appliedDistance = 0f;
        appliedVertical = 0f;
    }
}

/// <summary>
/// 单帧解算结果。
/// </summary>
public struct MoveSolveResult
{
    /// <summary>本帧位移向量（世界空间，含水平与竖直分量）。</summary>
    public Vector3 delta;
    /// <summary>本帧解算出的水平方向（已归一化），供 <c>alignFacing</c> 使用。</summary>
    public Vector3 direction;
    /// <summary>本帧是否产生了位移。</summary>
    public bool hasMovement;
    /// <summary>该段本帧是否结束。</summary>
    public bool finished;
}

/// <summary>
/// 位移解算器 —— 纯静态、无副作用（除写入传入的 <see cref="MoveSegmentState"/>）。
///
/// ═══════════════════════════════════════════════════════════
///  约束裁剪的固定顺序（design D4）
/// ═══════════════════════════════════════════════════════════
///  1. 曲线增量                    → 原始 delta
///  2. maxDistance 累积上限        → 硬上限，越界策略「继续」也不能突破
///  3. stopAtTargetOffset 剩余距离 → 到目标点即止
///  4. minKeepDistance 朝目标分量  → 防穿模
///  5. overshootPolicy             → 决定剩余量归属
///
///  顺序固定是为了让每个配置组合都有唯一可解释的预期结果。
///
/// ═══════════════════════════════════════════════════════════
///  记账规则（design D3，与旧 moveCurve 路径的关键差异）
/// ═══════════════════════════════════════════════════════════
///  按**实际应用量**累加：<c>appliedDistance += 实际位移</c>。
///  旧路径按未裁剪的目标值记账（<c>_moveCurveDistanceMoved = targetTotalDist</c>），
///  导致一旦被裁剪后续帧再也追不上——那是隐式的「停止」。
///  新路径把这一意图交给 <see cref="Global.OvershootPolicy"/> 显式表达。
/// </summary>
public static class MoveSolver
{
    public const float kEpsilon = 1e-5f;

    // ─── 方向解算 ─────────────────────────────────────────────

    /// <summary>
    /// 解算该段的水平位移方向（已归一化）。
    /// 任何退化情形（无目标、方向向量为零、无输入）一律回退为自身正前方，绝不放弃整段位移。
    /// </summary>
    public static Vector3 ResolveDirection(Global.MoveSegment seg, in MoveSolveContext ctx)
    {
        Vector3 forward = Flatten(ctx.SelfForward);
        forward = forward.sqrMagnitude < kEpsilon ? Vector3.forward : forward.normalized;

        switch (seg.space)
        {
            case Global.MoveSpace.TowardTarget:
            case Global.MoveSpace.AwayFromTarget:
            {
                if (!ctx.hasTarget) return forward;

                // 中心为目标时朝「目标点」（含偏移）解算，中心为自身时朝目标本体解算。
                Vector3 refPoint = seg.origin == Global.MoveOrigin.Target
                    ? ResolveTargetPoint(seg, ctx)
                    : ctx.targetPosition;

                Vector3 toRef = Flatten(refPoint - ctx.selfPosition);
                if (toRef.sqrMagnitude < kEpsilon) return forward;

                toRef.Normalize();
                return seg.space == Global.MoveSpace.AwayFromTarget ? -toRef : toRef;
            }

            case Global.MoveSpace.WorldDirection:
                return NormalizeOr(Flatten(seg.customDirection), forward);

            case Global.MoveSpace.LocalDirection:
                return NormalizeOr(Flatten(ctx.selfRotation * seg.customDirection), forward);

            case Global.MoveSpace.InputDirection:
                return NormalizeOr(Flatten(ctx.inputDirection), forward);

            case Global.MoveSpace.Forward:
            default:
                return forward;
        }
    }

    /// <summary>
    /// 解算目标点 = 目标位置 + 偏移量（按偏移空间解释）。无目标时返回自身位置。
    /// </summary>
    public static Vector3 ResolveTargetPoint(Global.MoveSegment seg, in MoveSolveContext ctx)
    {
        if (!ctx.hasTarget) return ctx.selfPosition;

        Vector3 offset = seg.offsetSpace == Global.OffsetSpace.TargetLocal
            ? ctx.targetRotation * seg.targetOffset
            : seg.targetOffset;

        return ctx.targetPosition + offset;
    }

    // ─── 单帧解算 ─────────────────────────────────────────────

    /// <summary>
    /// 解算该段在指定帧应产生的位移。<paramref name="state"/> 会被就地更新。
    /// 动画驱动段恒返回零位移（其水平位移由 root motion 提供，不走本解算器）。
    /// </summary>
    public static MoveSolveResult Solve(Global.MoveSegment seg, in MoveSolveContext ctx, int frame, MoveSegmentState state)
    {
        var result = default(MoveSolveResult);
        if (seg == null || state == null || state.finished) return result;
        if (seg.driveMode == Global.MoveDriveMode.Animation) return result;
        if (frame < seg.keyNumber) return result;

        int endFrame = seg.SegmentEndFrame;

        // 帧号跳过了结束帧（技能被快进/掉帧）时，用结束帧再解算一次把位移收敛到位，然后结束该段。
        bool pastEnd = frame > endFrame;
        if (pastEnd && !state.started)
        {
            state.finished = true;
            return result;
        }
        int solveFrame = pastEnd ? endFrame : frame;

        if (!state.started || seg.resolveDirectionPerFrame)
            state.direction = ResolveDirection(seg, ctx);
        state.started = true;

        Vector3 dir = state.direction;
        result.direction = dir;

        float t01 = seg.Normalize(solveFrame);

        // ── 竖直分量：独立记账，不参与水平约束裁剪 ──
        float verticalTarget = seg.verticalCurve != null
            ? seg.verticalCurve.Evaluate(t01) * seg.verticalDistance
            : 0f;
        float verticalDelta = verticalTarget - state.appliedVertical;
        state.appliedVertical += verticalDelta;

        // ── 水平分量 ──
        Vector3 move = Vector3.zero;
        bool clipped = false;
        bool arrived = false;

        // maxDistance 为 0 → 该段不产生任何水平位移。
        if (seg.maxDistance > kEpsilon)
        {
            // 1. 曲线增量
            float curveValue = seg.curve != null ? seg.curve.Evaluate(t01) : 0f;
            float rawDelta = curveValue * seg.maxDistance - state.appliedDistance;

            if (rawDelta < -kEpsilon)
            {
                // 曲线下降段：按越界策略处理反向增量。
                switch (seg.overshootPolicy)
                {
                    case Global.OvershootPolicy.Continue:
                        break;                                   // 允许反向位移
                    case Global.OvershootPolicy.Stop:
                        rawDelta = 0f; result.finished = true; break;
                    case Global.OvershootPolicy.Clamp:
                    default:
                        rawDelta = 0f; break;                    // 不倒退，与旧实现一致
                }
            }
            else if (rawDelta < kEpsilon)
            {
                rawDelta = 0f;
            }

            // 2. maxDistance 累积上限（硬上限，任何策略都不能突破）
            if (rawDelta > 0f)
            {
                float allowed = Mathf.Max(seg.maxDistance - state.appliedDistance, 0f);
                if (rawDelta > allowed) { rawDelta = allowed; clipped = true; }
            }

            move = dir * rawDelta;

            // 「继续」策略忽略目标点与最小保留距离这两项软约束。
            bool applySoftConstraints = seg.overshootPolicy != Global.OvershootPolicy.Continue;

            // 3. stopAtTargetOffset：到目标点剩余距离上限
            if (applySoftConstraints && seg.stopAtTargetOffset
                && seg.origin == Global.MoveOrigin.Target && ctx.hasTarget)
            {
                Vector3 toPoint = Flatten(ResolveTargetPoint(seg, ctx) - ctx.selfPosition);
                float remaining = toPoint.magnitude;
                float tolerance = Mathf.Max(seg.arriveTolerance, kEpsilon);

                if (remaining <= tolerance)
                {
                    move = Vector3.zero;
                    arrived = true;
                }
                else
                {
                    float moveLen = move.magnitude;
                    if (moveLen > remaining)
                    {
                        move = move * (remaining / moveLen);
                        clipped = true;
                        arrived = true;
                    }
                }
            }

            // 4. minKeepDistance：裁掉朝目标的分量，保留切向与远离分量
            if (applySoftConstraints && seg.minKeepDistance > kEpsilon
                && ctx.hasTarget && move.sqrMagnitude > kEpsilon)
            {
                Vector3 toTarget = Flatten(ctx.targetPosition - ctx.selfPosition);
                float curDist = toTarget.magnitude;
                if (curDist > kEpsilon)
                {
                    Vector3 towardDir = toTarget / curDist;
                    float along = Vector3.Dot(move, towardDir);
                    // 段起始时已比最小保留距离更近 → maxAlong 为 0，朝目标分量归零且不向后推开。
                    float maxAlong = Mathf.Max(curDist - seg.minKeepDistance, 0f);
                    if (along > maxAlong)
                    {
                        move -= towardDir * (along - maxAlong);
                        clipped = true;
                    }
                }
            }

            // 按实际应用量记账（沿方向的有符号投影），而非按曲线目标值。
            state.appliedDistance += Vector3.Dot(move, dir);
        }

        // 5. 越界策略决定剩余量归属
        if (arrived) result.finished = true;
        if (clipped && seg.overshootPolicy == Global.OvershootPolicy.Stop) result.finished = true;
        if (seg.overshootPolicy == Global.OvershootPolicy.Stop
            && seg.maxDistance > kEpsilon
            && state.appliedDistance >= seg.maxDistance - kEpsilon)
        {
            result.finished = true;
        }

        result.delta = move + Vector3.up * verticalDelta;
        result.hasMovement = result.delta.sqrMagnitude > kEpsilon * kEpsilon;

        if (pastEnd || frame >= endFrame) result.finished = true;
        if (result.finished) state.finished = true;

        return result;
    }

    // ─── 工具 ────────────────────────────────────────────────

    static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    static Vector3 NormalizeOr(Vector3 v, Vector3 fallback)
        => v.sqrMagnitude < kEpsilon ? fallback : v.normalized;
}
