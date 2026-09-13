using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="MoveSolver"/> 单元测试 —— 覆盖 segment-model 与 program-drive 两份 spec 的全部数值场景。
///
/// 放在预定义程序集 <c>Assembly-CSharp-Editor</c>（本目录无 asmdef），
/// 因为运行时代码位于 <c>Assembly-CSharp</c>，asmdef 无法引用预定义程序集。
/// </summary>
public class MoveSolverTests
{
    const float kTol = 1e-3f;

    // ─── 测试脚手架 ───────────────────────────────────────────

    /// <summary>逐帧推进解算，每帧把位移累加回上下文位置，模拟真实运行。</summary>
    class Sim
    {
        public readonly Global.MoveSegment seg;
        public MoveSolveContext ctx;
        public readonly MoveSegmentState state = new MoveSegmentState();

        public Vector3 startPosition;
        public Vector3 total;
        public bool finished;
        public int finishedFrame = -1;
        public float minAppliedStep = float.MaxValue;

        public Sim(Global.MoveSegment segment, MoveSolveContext context)
        {
            seg = segment;
            ctx = context;
            startPosition = context.selfPosition;
        }

        public void Run(int fromFrame, int toFrame)
        {
            float prevApplied = state.appliedDistance;
            for (int f = fromFrame; f <= toFrame; f++)
            {
                ctx.selfPosition = startPosition + total;
                var r = MoveSolver.Solve(seg, ctx, f, state);
                total += r.delta;

                minAppliedStep = Mathf.Min(minAppliedStep, state.appliedDistance - prevApplied);
                prevApplied = state.appliedDistance;

                if (r.finished) { finished = true; finishedFrame = f; break; }
            }
        }

        public Vector3 Position => startPosition + total;
        public Vector3 HorizontalTotal => new Vector3(total.x, 0f, total.z);
    }

    static Global.MoveSegment Seg(int start, int end)
    {
        return new Global.MoveSegment { keyNumber = start, endKeyNumber = end };
    }

    static MoveSolveContext Ctx(Vector3 self, bool hasTarget = false, Vector3 target = default, Quaternion? targetRot = null)
    {
        return new MoveSolveContext
        {
            selfPosition = self,
            selfRotation = Quaternion.identity,
            inputDirection = Vector3.zero,
            hasTarget = hasTarget,
            targetPosition = target,
            targetRotation = targetRot ?? Quaternion.identity,
        };
    }

    static AnimationCurve Curve(params Keyframe[] keys) => new AnimationCurve(keys);

    // ═══════════════════════════════════════════════════════════
    //  segment-model spec
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 段内进度计算_归一化05处曲线08总量5_累计位移为4()
    {
        var seg = Seg(10, 20);
        seg.maxDistance = 5f;
        seg.curve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0.8f), new Keyframe(1f, 1f));

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(10, 15);

        Assert.AreEqual(4f, sim.state.appliedDistance, kTol);
        Assert.AreEqual(4f, sim.HorizontalTotal.magnitude, kTol);
    }

    [Test]
    public void 单帧段_结束帧小于等于起始帧_当帧一次性应用全部位移()
    {
        var seg = Seg(5, 0);          // endKeyNumber <= keyNumber
        seg.maxDistance = 3f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(5, 5);

        Assert.AreEqual(3f, sim.HorizontalTotal.magnitude, kTol);
        Assert.IsTrue(sim.finished);
    }

    [Test]
    public void 段正常结束_总量6曲线终值1无裁剪_累计位移为6()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 6f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.AreEqual(6f, sim.HorizontalTotal.magnitude, kTol);
    }

    [Test]
    public void 曲线非单调下降_默认裁剪策略_累计位移不倒退()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 4f;
        seg.curve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.2f));
        seg.overshootPolicy = Global.OvershootPolicy.Clamp;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        // 每一帧的累计量增量都不为负 —— 曲线下降不会把角色往回拽。
        Assert.GreaterOrEqual(sim.minAppliedStep, -kTol);
        Assert.AreEqual(4f, sim.state.appliedDistance, kTol);
    }

    [Test]
    public void 帧号跳过结束帧_位移仍收敛到目标值()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 6f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 3);       // 先正常走几帧
        sim.Run(25, 25);     // 再直接跳到结束帧之后

        Assert.AreEqual(6f, sim.HorizontalTotal.magnitude, kTol);
        Assert.IsTrue(sim.finished);
    }

    [Test]
    public void 动画驱动段_解算器不产生位移()
    {
        var seg = Seg(0, 10);
        seg.driveMode = Global.MoveDriveMode.Animation;
        seg.maxDistance = 6f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.AreEqual(0f, sim.total.magnitude, kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 方向来源
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 输入方向来源但无输入_回退为自身正前方()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.InputDirection;

        var ctx = Ctx(Vector3.zero);
        ctx.selfRotation = Quaternion.Euler(0f, 90f, 0f);   // 面朝 +X

        var actual = MoveSolver.ResolveDirection(seg, ctx);
        Assert.AreEqual(0f, Vector3.Distance(Vector3.right, actual), kTol);
    }

    [Test]
    public void 朝向目标但无目标_回退为自身正前方()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;

        var ctx = Ctx(Vector3.zero);   // hasTarget = false

        Assert.AreEqual(Vector3.forward, MoveSolver.ResolveDirection(seg, ctx));
    }

    [Test]
    public void 朝向目标只解算一次_目标侧移后方向保持不变()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.maxDistance = 4f;
        seg.resolveDirectionPerFrame = false;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f)));
        sim.Run(0, 5);
        sim.ctx.targetPosition = new Vector3(10f, 0f, 10f);   // 目标侧移
        sim.Run(6, 10);

        // 全程沿起始方向 +Z 直线移动，X 分量恒为 0。
        Assert.AreEqual(0f, sim.total.x, kTol);
        Assert.AreEqual(4f, sim.total.z, kTol);
    }

    [Test]
    public void 朝向目标且逐帧重解算_目标侧移后方向跟随更新()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.maxDistance = 4f;
        seg.resolveDirectionPerFrame = true;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f)));
        sim.Run(0, 5);
        sim.ctx.targetPosition = new Vector3(10f, 0f, 10f);
        sim.Run(6, 10);

        // 方向跟随后产生了 X 分量 —— 轨迹是弧线而非直线。
        Assert.Greater(sim.total.x, kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 目标点与偏移
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 目标局部空间偏移_随目标朝向旋转()
    {
        var seg = Seg(0, 10);
        seg.offsetSpace = Global.OffsetSpace.TargetLocal;
        seg.targetOffset = new Vector3(0f, 0f, 1.5f);        // 目标局部正前方 1.5 米

        var facingNorth = Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f), Quaternion.identity);
        Assert.AreEqual(new Vector3(0f, 0f, 11.5f), MoveSolver.ResolveTargetPoint(seg, facingNorth));

        var facingEast = Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 90f, 0f));
        var point = MoveSolver.ResolveTargetPoint(seg, facingEast);
        Assert.AreEqual(1.5f, point.x, kTol);
        Assert.AreEqual(10f, point.z, kTol);
    }

    [Test]
    public void 世界空间偏移_不随目标朝向旋转()
    {
        var seg = Seg(0, 10);
        seg.offsetSpace = Global.OffsetSpace.World;
        seg.targetOffset = new Vector3(0f, 0f, 1.5f);

        // 目标面朝东，但世界空间偏移不受影响，目标点恒在目标 +Z 侧 1.5 米。
        var ctx = Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f), Quaternion.Euler(0f, 90f, 0f));

        Assert.AreEqual(new Vector3(0f, 0f, 11.5f), MoveSolver.ResolveTargetPoint(seg, ctx));
    }

    [Test]
    public void 到达目标点即停止_总量10目标点距3米_移动约3米后停止()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Target;
        seg.stopAtTargetOffset = true;
        seg.resolveDirectionPerFrame = true;
        seg.maxDistance = 10f;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 3f)));
        sim.Run(0, 10);

        Assert.AreEqual(3f, sim.HorizontalTotal.magnitude, seg.arriveTolerance + kTol);
        Assert.IsTrue(sim.finished);
        Assert.Less(sim.finishedFrame, 10);          // 曲线未走完就提前结束
    }

    [Test]
    public void 未启用到达即停止_总量10目标点距3米_按曲线走满并穿过目标点()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Target;
        seg.stopAtTargetOffset = false;
        seg.resolveDirectionPerFrame = false;        // 固定方向，避免穿过后掉头
        seg.maxDistance = 10f;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 3f)));
        sim.Run(0, 10);

        Assert.AreEqual(10f, sim.HorizontalTotal.magnitude, kTol);
        Assert.Greater(sim.Position.z, 3f);          // 确实穿过了目标点
    }

    [Test]
    public void 中心为目标的环绕位移_移动至目标右侧2米且不越过()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Target;
        seg.offsetSpace = Global.OffsetSpace.TargetLocal;
        seg.targetOffset = new Vector3(2f, 0f, 0f);  // 目标右侧 2 米
        seg.stopAtTargetOffset = true;
        seg.resolveDirectionPerFrame = true;
        seg.maxDistance = 20f;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 10f)));
        sim.Run(0, 10);

        var expected = new Vector3(2f, 0f, 10f);
        Assert.AreEqual(0f, Vector3.Distance(sim.Position, expected), seg.arriveTolerance + kTol);
    }

    [Test]
    public void 中心为自身的纯相对位移_与是否存在目标无关()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.Forward;
        seg.origin = Global.MoveOrigin.Self;
        seg.maxDistance = 4f;

        var noTarget = new Sim(seg, Ctx(Vector3.zero));
        noTarget.Run(0, 10);

        var withTarget = new Sim(Seg(0, 10), Ctx(Vector3.zero, true, new Vector3(9f, 0f, 1f)));
        withTarget.seg.space = Global.MoveSpace.Forward;
        withTarget.seg.origin = Global.MoveOrigin.Self;
        withTarget.seg.maxDistance = 4f;
        withTarget.Run(0, 10);

        Assert.AreEqual(new Vector3(0f, 0f, 4f).x, noTarget.total.x, kTol);
        Assert.AreEqual(4f, noTarget.total.z, kTol);
        Assert.AreEqual(0f, Vector3.Distance(noTarget.total, withTarget.total), kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 最大距离
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 目标远于最大距离_只移动8米且未到达目标点()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Target;
        seg.stopAtTargetOffset = true;
        seg.resolveDirectionPerFrame = true;
        seg.maxDistance = 8f;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 20f)));
        sim.Run(0, 10);

        Assert.AreEqual(8f, sim.HorizontalTotal.magnitude, kTol);
        Assert.Less(sim.Position.z, 20f);
    }

    [Test]
    public void 最大距离为0_不产生任何水平位移()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 0f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.AreEqual(0f, sim.HorizontalTotal.magnitude, kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 最小保留距离
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 突进接近目标_最小保留距离12_最多移动至相距12米()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Self;
        seg.resolveDirectionPerFrame = true;
        seg.maxDistance = 10f;
        seg.minKeepDistance = 1.2f;

        var target = new Vector3(0f, 0f, 5f);
        var sim = new Sim(seg, Ctx(Vector3.zero, true, target));
        sim.Run(0, 10);

        float finalDist = Vector3.Distance(sim.Position, target);
        Assert.AreEqual(1.2f, finalDist, kTol);
    }

    [Test]
    public void 起始已比最小保留距离更近_朝目标分量为0且不向后推开()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Self;
        seg.resolveDirectionPerFrame = true;
        seg.maxDistance = 10f;
        seg.minKeepDistance = 1.2f;

        var target = new Vector3(0f, 0f, 0.8f);
        var sim = new Sim(seg, Ctx(Vector3.zero, true, target));
        sim.Run(0, 10);

        Assert.AreEqual(0f, sim.HorizontalTotal.magnitude, kTol);
        Assert.AreEqual(0.8f, Vector3.Distance(sim.Position, target), kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 竖直分量
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 抛物线突进_竖直先升后降_段结束回到起始高度()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 6f;
        seg.verticalDistance = 2f;
        seg.verticalCurve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        seg.ignoreGravity = true;

        var sim = new Sim(seg, Ctx(Vector3.zero));

        sim.Run(0, 5);
        Assert.Greater(sim.Position.y, 0.5f);        // 中段确实升起来了

        sim.Run(6, 10);
        Assert.AreEqual(0f, sim.Position.y, kTol);   // 段结束回到起始高度
        Assert.AreEqual(6f, sim.HorizontalTotal.magnitude, kTol);
    }

    [Test]
    public void 未配置竖直分量_竖直位移恒为0()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 4f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.AreEqual(0f, sim.total.y, kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  program-drive spec — 越界策略
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 策略为停止_达到最大距离后立即结束该段()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 4f;
        seg.curve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1f));
        seg.overshootPolicy = Global.OvershootPolicy.Stop;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.IsTrue(sim.finished);
        Assert.Less(sim.finishedFrame, 10);
        Assert.AreEqual(4f, sim.HorizontalTotal.magnitude, kTol);
    }

    [Test]
    public void 策略为裁剪_达到最大距离后位移为0但段保持存活至结束帧()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 4f;
        seg.curve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1f));
        seg.overshootPolicy = Global.OvershootPolicy.Clamp;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 9);

        Assert.IsFalse(sim.finished);                          // 未提前结束
        Assert.AreEqual(4f, sim.HorizontalTotal.magnitude, kTol);

        var before = sim.total;
        sim.Run(9, 9);
        Assert.AreEqual(0f, (sim.total - before).magnitude, kTol);   // 后续帧位移为 0
    }

    [Test]
    public void 策略为继续_忽略最小保留距离约束继续位移()
    {
        var seg = Seg(0, 10);
        seg.space = Global.MoveSpace.TowardTarget;
        seg.origin = Global.MoveOrigin.Self;
        seg.resolveDirectionPerFrame = false;
        seg.maxDistance = 10f;
        seg.minKeepDistance = 1.2f;
        seg.overshootPolicy = Global.OvershootPolicy.Continue;

        var sim = new Sim(seg, Ctx(Vector3.zero, true, new Vector3(0f, 0f, 5f)));
        sim.Run(0, 10);

        Assert.AreEqual(10f, sim.HorizontalTotal.magnitude, kTol);   // 最小保留距离被忽略
    }

    [Test]
    public void 策略为继续_最大距离作为硬上限仍不可突破()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 4f;
        seg.curve = Curve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1f));
        seg.overshootPolicy = Global.OvershootPolicy.Continue;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 10);

        Assert.AreEqual(4f, sim.HorizontalTotal.magnitude, kTol);
    }

    // ═══════════════════════════════════════════════════════════
    //  中断语义
    // ═══════════════════════════════════════════════════════════

    [Test]
    public void 段状态重置后_下次播放从零开始不补齐()
    {
        var seg = Seg(0, 10);
        seg.maxDistance = 6f;

        var sim = new Sim(seg, Ctx(Vector3.zero));
        sim.Run(0, 5);
        Assert.Greater(sim.state.appliedDistance, 0f);

        sim.state.Reset();
        Assert.AreEqual(0f, sim.state.appliedDistance, kTol);
        Assert.IsFalse(sim.state.finished);
        Assert.IsFalse(sim.state.started);
    }
    sealed class Backend : IDisplacementBackend
    {
        public Transform Transform { get; set; }
        public bool IsGrounded => true;
        public bool CanMove => true;
        public Vector3 delta;
        public bool ignored;
        public void BeginDisplacement() { }
        public void EndDisplacement() { }
        public void Move(Vector3 value) { delta += value; }
        public void SetFacing(Vector3 value) { }
        public void SetActorCollisionIgnored(bool value) { ignored = value; }
    }
    sealed class RootMotion : Ethan.ActionEditor.IActionRootMotionSource
    {
        public Vector3 delta;
        public void SetTakeover(bool enabled) { }
        public void Clear() { delta = Vector3.zero; }
        public Vector3 ConsumeDelta() { var value = delta; Clear(); return value; }
        public Quaternion ConsumeDeltaRotation() => Quaternion.identity;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActiveAnimationOverridesProgramRegardlessOfListOrder(bool animationFirst)
    {
        var actor = new GameObject("driver");
        try
        {
            var backend = new Backend { Transform = actor.transform };
            var root = new RootMotion();
            var driver = new SkillDisplacementDriver(backend, null, root, null);
            var program = Seg(0, 10); program.maxDistance = 10;
            var animation = Seg(0, 10); animation.driveMode = Global.MoveDriveMode.Animation;
            driver.Begin(animationFirst ? new System.Collections.Generic.List<Global.MoveSegment> { animation, program }
                : new System.Collections.Generic.List<Global.MoveSegment> { program, animation });
            root.delta = Vector3.forward * 2;
            driver.Tick(5, Vector3.forward);
            Assert.That(backend.delta.z, Is.EqualTo(2).Within(kTol));
            driver.Abort();
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void AnimationFlagsAndRootMotionAreLimitedToItsFrameInterval()
    {
        var actor = new GameObject("driver");
        try
        {
            var backend = new Backend { Transform = actor.transform };
            var root = new RootMotion();
            var driver = new SkillDisplacementDriver(backend, null, root, null);
            var animation = Seg(5, 7); animation.driveMode = Global.MoveDriveMode.Animation;
            animation.ignoreGravity = true; animation.ignoreActorCollision = true;
            driver.Begin(new System.Collections.Generic.List<Global.MoveSegment> { animation });
            root.delta = Vector3.forward * 100;
            driver.Tick(4, Vector3.zero);
            Assert.AreEqual(Vector3.zero, backend.delta);
            Assert.IsFalse(backend.ignored); Assert.IsFalse(driver.IgnoresGravityAtFrame(4));
            root.delta = Vector3.forward;
            driver.Tick(5, Vector3.zero);
            Assert.AreEqual(Vector3.forward, backend.delta);
            Assert.IsTrue(backend.ignored); Assert.IsTrue(driver.IgnoresGravityAtFrame(7));
            root.delta = Vector3.forward * 100;
            driver.Tick(8, Vector3.zero);
            Assert.AreEqual(Vector3.forward, backend.delta);
            Assert.IsFalse(backend.ignored); Assert.IsFalse(driver.IgnoresGravityAtFrame(8));
            driver.Abort(); Assert.IsFalse(driver.IgnoresGravityAtFrame(5));
        }
        finally { Object.DestroyImmediate(actor); }
    }
}
