using System;
using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Unscaled, local action hold. Other actions and Time.timeScale are untouched.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(ActionPlayer))]
    public sealed class ActionHitStop : MonoBehaviour
    {
        ActionPlayer player;
        float remaining;
        public bool AutoAdvance { get; set; } = true;
        public float Remaining => remaining;

        public void Request(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!isActiveAndEnabled || seconds == 0) return;
            if (player == null) player = GetComponent<ActionPlayer>();
            remaining = Mathf.Max(remaining, seconds);
            player.SetPresentationPaused(this, true);
        }
        public void Advance(float unscaledDeltaTime)
        {
            if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime) || unscaledDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            remaining = Mathf.Max(0, remaining - unscaledDeltaTime);
            if (remaining == 0 && player != null) player.SetPresentationPaused(this, false);
        }
        void Update() { if (AutoAdvance) Advance(Time.unscaledDeltaTime); }
        void OnDisable()
        {
            remaining = 0;
            if (player != null) player.SetPresentationPaused(this, false);
        }
    }
}
