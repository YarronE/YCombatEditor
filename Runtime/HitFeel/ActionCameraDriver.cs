using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Optional native camera receiver. Own a dedicated pivot and camera lens; do not bind transforms written by another controller.</summary>
    [DisallowMultipleComponent]
    public sealed class ActionCameraDriver : MonoBehaviour, IActionEventHandler<ActionCameraCue>
    {
        public Transform cameraPivot;
        public Camera targetCamera;
        readonly Dictionary<ActionCameraCue, Vector4> samples = new Dictionary<ActionCameraCue, Vector4>();
        Transform activePivot;
        Camera activeCamera;
        Vector3 basePosition;
        Quaternion baseRotation;
        float baseFieldOfView;
        bool captured;

        public bool HasBinding => cameraPivot != null || targetCamera != null;

        public void Handle(in ActionExecutionContext context, ActionCameraCue data)
        {
            if (data == null) return;
            if (context.EventPhase == ActionEventPhase.Exit)
            {
                samples.Remove(data);
                Apply();
                return;
            }
            if (!isActiveAndEnabled || !HasBinding) return;
            if (!captured)
            {
                activePivot = cameraPivot; activeCamera = targetCamera;
                if (activePivot != null) { basePosition = activePivot.localPosition; baseRotation = activePivot.localRotation; }
                if (activeCamera != null) baseFieldOfView = activeCamera.fieldOfView;
                captured = true;
            }
            float time = (context.Frame - data.keyNumber) / ActionTiming.FrameRate(context.Config);
            float phase = time * data.shakeFrequency * 2 * Mathf.PI;
            samples[data] = new Vector4(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), 0, data.Weight(context.Frame));
            Apply();
        }

        void Apply()
        {
            if (!captured) return;
            Vector3 position = Vector3.zero, rotation = Vector3.zero;
            float fov = 0;
            foreach (var pair in samples)
            {
                var cue = pair.Key; var sample = pair.Value;
                position += (cue.positionOffset + new Vector3(sample.x, sample.y, 0) * cue.shakeAmplitude) * sample.w;
                rotation += cue.rotationOffset * sample.w;
                fov += cue.fieldOfViewOffset * sample.w;
            }
            if (activePivot != null) { activePivot.localPosition = basePosition + position; activePivot.localRotation = baseRotation * Quaternion.Euler(rotation); }
            if (activeCamera != null) activeCamera.fieldOfView = Mathf.Clamp(baseFieldOfView + fov, 1, 179);
            if (samples.Count == 0) captured = false;
        }
        void OnDisable() { samples.Clear(); Apply(); }
    }
}
