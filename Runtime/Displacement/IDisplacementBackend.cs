using UnityEngine;

/// <summary>
/// 位移后端 —— 抽象「怎么真的移动」这一件事，让同一套解算能同时服务玩家与 Boss。
///
/// - 玩家：<c>CharacterControllerBackend</c>，直接 <c>CharacterController.Move</c>。
/// - Boss：<c>NavMeshAgentBackend</c>，位移期间挂起 <c>NavMeshAgent</c>，
///   用 <c>CharacterController.Move</c> 位移，结束后 <c>agent.Warp</c> 归还控制权。
///
/// 契约：
/// - <see cref="BeginDisplacement"/> 与 <see cref="EndDisplacement"/> MUST 成对出现，
///   <see cref="EndDisplacement"/> 必须能被重复调用而不出错（中断路径可能重复触发）。
/// - <see cref="Move"/> 每帧 MUST 只被调用一次（多段位移已在驱动层合并为单个向量）。
/// - 实现 MUST NOT 直接写 <c>transform.position</c> 绕过碰撞解算。
/// </summary>
public interface IDisplacementBackend
{
    /// <summary>角色变换，供解算层读取位置与朝向。</summary>
    Transform Transform { get; }

    /// <summary>角色当前是否贴地。</summary>
    bool IsGrounded { get; }

    /// <summary>本后端当前是否具备移动能力（缺组件时为 false，驱动层据此告警并跳过位移）。</summary>
    bool CanMove { get; }

    /// <summary>挂起常规移动系统，接管位移控制权。</summary>
    void BeginDisplacement();

    /// <summary>提交本帧位移向量（世界空间，已含水平与竖直分量），经碰撞解算通道执行。</summary>
    void Move(Vector3 delta);

    /// <summary>把角色朝向对齐到指定水平方向（<c>alignFacing</c> 用）。</summary>
    void SetFacing(Vector3 horizontalDirection);

    /// <summary>开关「忽略与其他角色的碰撞阻挡」。仅影响角色间阻挡，静态几何体仍会阻挡。</summary>
    void SetActorCollisionIgnored(bool ignored);

    /// <summary>归还控制权并还原一切临时状态。必须可重复调用。</summary>
    void EndDisplacement();
}
