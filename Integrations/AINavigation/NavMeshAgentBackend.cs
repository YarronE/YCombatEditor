using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Boss 侧位移后端 —— 位移期间挂起 <see cref="NavMeshAgent"/>，
/// 用 <see cref="CharacterController.Move"/> 提交位移（碰撞解算），
/// 结束后 <see cref="NavMeshAgent.Warp"/> 归还控制权并还原开关。
///
/// 设计（design D6）：
///   Begin:  agent.isStopped = true; updatePosition = false; updateRotation = false; obstacleAvoidanceType = NoObstacleAvoidance
///   Tick:   cc.Move(frameDelta)
///   End:    if (!NavMesh.SamplePosition(pos, out hit, r, mask)) → 归位到 hit.position
///           agent.Warp(transform.position)
///           agent.updatePosition = true; updateRotation = true; isStopped = false
/// </summary>
public class NavMeshAgentBackend : IDisplacementBackend
{
    readonly NavMeshAgent _agent;
    readonly CharacterController _cc;
    readonly Transform _transform;

    // 保存的原始状态（Begin 记录，End 还原）。
    bool _savedStopped;
    bool _savedUpdatePosition;
    bool _savedUpdateRotation;
    ObstacleAvoidanceType _savedAvoidance;
    bool _began;

    public NavMeshAgentBackend(NavMeshAgent agent, CharacterController cc)
    {
        _agent = agent;
        _cc = cc;
        _transform = agent != null ? agent.transform : (cc != null ? cc.transform : null);
    }

    public Transform Transform => _transform;

    public bool IsGrounded => _cc != null && _cc.isGrounded;

    public bool CanMove => _cc != null && _cc.enabled;

    public void BeginDisplacement()
    {
        if (_began) return;
        _began = true;

        if (_agent != null)
        {
            _savedStopped = _agent.isStopped;
            _savedUpdatePosition = _agent.updatePosition;
            _savedUpdateRotation = _agent.updateRotation;
            _savedAvoidance = _agent.obstacleAvoidanceType;

            _agent.isStopped = true;
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }
    }

    public void Move(Vector3 delta)
    {
        if (_cc != null) _cc.Move(delta);
    }

    public void SetFacing(Vector3 horizontalDirection)
    {
        if (_transform == null || horizontalDirection.sqrMagnitude < 1e-6f) return;
        var flat = horizontalDirection;
        flat.y = 0f;
        _transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    public void SetActorCollisionIgnored(bool ignored)
    {
        // Boss 位移不做角色间碰撞忽略（Boss 通常无友方碰撞需求）。
        // 保留空实现以满足接口契约；ignoreActorCollision 段对 Boss 无效果。
    }

    public void EndDisplacement()
    {
        if (!_began) return;
        _began = false;

        if (_agent == null || _transform == null) return;

        // 1. 检查当前位置是否可导航，不可导航则归位到最近可导航点。
        Vector3 pos = _transform.position;
        if (_agent.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
            {
                // 仍在 NavMesh 上但 SamplePosition 失败：位置可能已脱离网格，用 Warp 校正。
            }
            else if (Vector3.Distance(hit.position, pos) > 0.01f)
            {
                _transform.position = hit.position;
            }
        }
        else
        {
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas))
                _transform.position = hit.position;
        }

        // 2. Warp 归还控制权（使 agent 与 transform 同步）。
        if (_agent.isOnNavMesh)
            _agent.Warp(_transform.position);

        // 3. 还原四个开关。
        if (_agent.isOnNavMesh)
        {
            _agent.obstacleAvoidanceType = _savedAvoidance;
            _agent.updatePosition = _savedUpdatePosition;
            _agent.updateRotation = _savedUpdateRotation;
            _agent.isStopped = _savedStopped;
        }
    }
}
