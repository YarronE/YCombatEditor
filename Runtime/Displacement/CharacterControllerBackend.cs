using UnityEngine;
using Ethan.ActionEditor;

/// <summary>
/// 玩家侧位移后端 —— 用 <see cref="CharacterController.Move"/> 提交位移，
/// 碰撞由 CharacterController 内建解算（含墙体阻挡）。
///
/// <see cref="SetActorCollisionIgnored"/> 用 <see cref="Physics.IgnoreCollision"/> 实现：
/// 收集范围内所有带 Collider 的角色，与本角色互相忽略；还原时恢复。
/// 静态几何体（非角色）不受影响。
/// </summary>
public class CharacterControllerBackend : IDisplacementBackend
{
    readonly CharacterController _cc;
    readonly Transform _transform;

    Collider _selfCollider;
    readonly System.Collections.Generic.List<Collider> _ignored = new System.Collections.Generic.List<Collider>();

    public CharacterControllerBackend(CharacterController cc)
    {
        _cc = cc;
        _transform = cc != null ? cc.transform : null;
    }

    public Transform Transform => _transform;

    public bool IsGrounded => _cc != null && _cc.isGrounded;

    public bool CanMove => _cc != null && _cc.enabled;

    public void BeginDisplacement()
    {
        _selfCollider = _cc != null ? _cc.GetComponent<Collider>() : null;
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
        if (_selfCollider == null) return;

        if (ignored)
        {
            if (_ignored.Count > 0) return;

            var cols = Physics.OverlapSphere(_transform.position, 10f);
            foreach (var col in cols)
            {
                if (col == _selfCollider) continue;
                if (col.transform.root == _transform.root) continue;
                if (col.GetComponent<IActionActor>() == null
                    && col.GetComponentInParent<IActionActor>() == null) continue;

                Physics.IgnoreCollision(_selfCollider, col, true);
                _ignored.Add(col);
            }
        }
        else
        {
            foreach (var col in _ignored)
            {
                if (col != null) Physics.IgnoreCollision(_selfCollider, col, false);
            }
            _ignored.Clear();
        }
    }

    public void EndDisplacement()
    {
        if (_ignored.Count > 0)
        {
            SetActorCollisionIgnored(false);
        }
        _selfCollider = null;
    }
}
