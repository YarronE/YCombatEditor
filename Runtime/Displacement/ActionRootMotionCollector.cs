using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Opt-in root delta capture, independent of any animation middleware.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class ActionRootMotionCollector : MonoBehaviour, IActionRootMotionSource
    {
        Animator source;
        bool capture;
        Vector3 translation;
        Quaternion rotation = Quaternion.identity;

        /// <summary>Disable when a controller owns actor motion, including outside authored animation segments.</summary>
        public bool ApplyUnclaimedRootMotion { get; set; } = true;

        void Awake() { source = GetComponent<Animator>(); }
        public void SetTakeover(bool enabled) { capture = enabled; Clear(); }
        public void Clear() { translation = Vector3.zero; rotation = Quaternion.identity; }
        public Vector3 ConsumeDelta() { var result = translation; translation = Vector3.zero; return result; }
        public Quaternion ConsumeDeltaRotation() { var result = rotation; rotation = Quaternion.identity; return result; }

        void OnAnimatorMove()
        {
            if (!capture) { if (ApplyUnclaimedRootMotion) source.ApplyBuiltinRootMotion(); return; }
            translation += source.deltaPosition;
            rotation = rotation * source.deltaRotation;
        }
        void OnDisable() { capture = false; Clear(); }
    }
}
