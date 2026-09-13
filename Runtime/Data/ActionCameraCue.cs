using System;
using UnityEngine;

namespace Ethan.ActionEditor
{
    [Serializable]
    public sealed class ActionCameraCue
    {
        public string label = "Camera accent";
        public int keyNumber;
        public int endKeyNumber = 15;
        [Tooltip("Offsets are relative to a dedicated presentation pivot, not the gameplay camera controller.")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        public float fieldOfViewOffset;
        [Min(0)] public float shakeAmplitude;
        [Min(0)] public float shakeFrequency = 18;
        public AnimationCurve envelope = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, 1), new Keyframe(1, 0));

        public float Weight(float frame) => envelope == null ? 0 : envelope.Evaluate(
            endKeyNumber <= keyNumber ? 0 : Mathf.Clamp01((frame - keyNumber) / (endKeyNumber - keyNumber)));
    }
}
