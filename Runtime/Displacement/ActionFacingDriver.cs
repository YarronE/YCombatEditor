using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Optional facing owner. Use on the ActionPlayer actor; custom controllers can query ActionMotionPolicy instead.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ActionPlayer))]
    public sealed class ActionFacingDriver : MonoBehaviour, IActionEventHandler<Global.AdjustMotionSegment>
    {
        ActionPlayer player;
        Quaternion heldRotation;
        void OnEnable()
        {
            player = GetComponent<ActionPlayer>();
            heldRotation = transform.rotation;
            player.Started += OnStarted;
        }
        void OnStarted(SkillConfigSO config) => heldRotation = transform.rotation;
        void OnDisable() { if (player != null) player.Started -= OnStarted; }

        public void Handle(in ActionExecutionContext context, Global.AdjustMotionSegment data)
        {
            if (!isActiveAndEnabled || context.EventPhase == ActionEventPhase.Exit) return;
            var resolved = ActionMotionPolicy.Resolve(context.Config, context.Frame);
            if (!ReferenceEquals(resolved, data) || !data.allowTurning) return;
            Transform target = context.Target;
            foreach (var component in GetComponents<MonoBehaviour>())
                if (component is IActionTargetProvider provider) { target = provider.ResolveTarget(data.targetKind) ?? target; break; }
            if (target == null) return;
            transform.rotation = ActionMotionPolicy.Face(transform.rotation, target.position - transform.position, data, 1 / ActionTiming.FrameRate(context.Config));
            heldRotation = transform.rotation;
        }

        void LateUpdate()
        {
            if (player != null && player.IsPlaying && ActionMotionPolicy.HasWindows(player.CurrentConfig) &&
                !ActionMotionPolicy.AllowsTurning(player.CurrentConfig, player.CurrentFrame)) transform.rotation = heldRotation;
            else heldRotation = transform.rotation;
        }
    }
}
