using UnityEngine;

/// <summary>
/// 位移目标解析统一入口 —— 玩家侧与敌人侧共用同一契约。
///
/// ═══════════════════════════════════════════════════════════
///  回退链契约（实现方必须遵守）
/// ═══════════════════════════════════════════════════════════
///
/// - <see cref="Global.MoveTargetKind.None"/>       → 恒返回 null，不做任何查找。
/// - <see cref="Global.MoveTargetKind.LockedTarget"/> → 锁定目标 → 最近敌人 → null。
/// - <see cref="Global.MoveTargetKind.NearestEnemy"/> → 最近敌人 → null。
/// - <see cref="Global.MoveTargetKind.Player"/>       → 玩家 → null。
///
/// 实现方 MUST NOT 抛异常，解析不到时返回 null 即可。
/// 「目标为 null 但该位移段依赖目标」的退化行为（退化为自身正前方）由
/// <see cref="MoveSolver"/> 统一处理，不是解析器的职责。
/// </summary>
public interface ITargetResolver
{
    /// <summary>按目标来源解析目标，解析不到返回 null。</summary>
    Transform Resolve(Global.MoveTargetKind kind);
}
