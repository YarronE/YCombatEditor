using System;
using UnityEngine;

/// <summary>
/// 敌人侧目标解析器 —— 敌人的位移目标恒为玩家。
///
/// 回退链：玩家 → null。
/// 敌人没有锁定系统也没有「最近敌人」的概念，因此 <see cref="Global.MoveTargetKind.LockedTarget"/>
/// 与 <see cref="Global.MoveTargetKind.NearestEnemy"/> 都解析为玩家，
/// 使同一份技能配置在玩家与 Boss 身上有一致的语义。
/// </summary>
public class EnemyTargetResolver : ITargetResolver
{
    readonly Func<Transform> _playerTargetProvider;

    public EnemyTargetResolver(Func<Transform> playerTargetProvider)
    {
        _playerTargetProvider = playerTargetProvider;
    }

    public Transform Resolve(Global.MoveTargetKind kind)
    {
        if (kind == Global.MoveTargetKind.None) return null;
        return _playerTargetProvider?.Invoke();
    }
}
