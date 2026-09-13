using System;
using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Position-only impulse on a dedicated camera pivot. Do not attach to an externally animated transform.</summary>
    [DisallowMultipleComponent]
    public sealed class ActionCameraShake : MonoBehaviour
    {
        Vector3 origin;
        float age, duration, amplitude, frequency;
        bool active;
        public bool AutoAdvance { get; set; } = true;

        public void Request(float seconds, float distance, float hertz = 18)
        {
            if (!Finite(seconds) || !Finite(distance) || !Finite(hertz) || seconds < 0 || distance < 0 || hertz <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration/amplitude must be nonnegative and frequency positive.");
            if (!isActiveAndEnabled) return;
            if (!active) origin = transform.localPosition;
            age = 0; duration = seconds; amplitude = distance; frequency = hertz;
            active = true;
            if (seconds == 0) Restore();
        }
        public void Advance(float unscaledDeltaTime)
        {
            if (!Finite(unscaledDeltaTime) || unscaledDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            if (!active) return;
            age += unscaledDeltaTime;
            if (age >= duration) { Restore(); return; }
            float envelope = 1 - age / duration;
            float phase = age * frequency * 2 * Mathf.PI;
            transform.localPosition = origin + amplitude * envelope * envelope *
                new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void Restore() { if (active) transform.localPosition = origin; active = false; }
        void LateUpdate() { if (AutoAdvance) Advance(Time.unscaledDeltaTime); }
        void OnDisable() { Restore(); }
    }
}
