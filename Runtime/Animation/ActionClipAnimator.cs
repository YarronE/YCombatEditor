using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Ethan.ActionEditor
{
    /// <summary>Two-input, manually sampled animation output. Driven only by ActionPlayer.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class ActionClipAnimator : MonoBehaviour, IActionAnimator, IActionAnimationClock
    {
        PlayableGraph graph;
        AnimationMixerPlayable blend;
        readonly AnimationClipPlayable[] inputs = new AnimationClipPlayable[2];
        int current = -1;
        float sourceStart, sourceEnd, frameRate, startFrame, blendFrames;
        float outgoingStart, outgoingEnd;
        bool sampling;

        public bool CanPlay(Global.AnimClipSegment segment) => isActiveAndEnabled &&
            segment != null && segment.clip != null && !segment.clip.legacy;

        public void Play(Global.AnimClipSegment segment, float fallbackFrameRate)
        {
            if (!CanPlay(segment)) throw new ArgumentException("A non-legacy animation clip is required.", nameof(segment));
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create("Action Clip Output");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                blend = AnimationMixerPlayable.Create(graph, 2);
                var output = AnimationPlayableOutput.Create(graph, "Actor", GetComponent<Animator>());
                output.SetSourcePlayable(blend);
                graph.Play();
            }
            int next = current < 0 ? 0 : 1 - current;
            if (current >= 0 && inputs[current].IsValid())
            {
                outgoingStart = (float)inputs[current].GetTime();
                outgoingEnd = sourceEnd;
            }
            ReleaseInput(next);
            inputs[next] = AnimationClipPlayable.Create(graph, segment.clip);
            inputs[next].SetSpeed(0);
            inputs[next].SetApplyFootIK(false);
            graph.Connect(inputs[next], 0, blend, next);
            current = next;
            frameRate = Mathf.Max(1, fallbackFrameRate);
            sourceStart = ActionTiming.SourceStart(segment);
            sourceEnd = ActionTiming.SourceEnd(segment);
            startFrame = segment.startFrame;
            blendFrames = Mathf.Max(0, segment.blendInFrames);
            sampling = true;
        }

        public void Sample(float timelineFrame)
        {
            if (!sampling || !graph.IsValid() || current < 0) return;
            if (float.IsNaN(timelineFrame) || float.IsInfinity(timelineFrame)) throw new ArgumentOutOfRangeException(nameof(timelineFrame));
            float elapsedFrames = Mathf.Max(0, timelineFrame - startFrame);
            inputs[current].SetTime(Mathf.Clamp(sourceStart + elapsedFrames / frameRate, sourceStart, sourceEnd));
            int outgoing = 1 - current;
            if (inputs[outgoing].IsValid()) inputs[outgoing].SetTime(Mathf.Clamp(outgoingStart + elapsedFrames / frameRate, outgoingStart, outgoingEnd));
            float weight = inputs[outgoing].IsValid() && blendFrames > 0 ? Mathf.Clamp01(elapsedFrames / blendFrames) : 1;
            blend.SetInputWeight(current, weight);
            blend.SetInputWeight(outgoing, 1 - weight);
            graph.Evaluate(0);
            if (weight >= 1) ReleaseInput(outgoing);
        }

        public void Stop(ActionStopReason reason) { sampling = false; }

        void ReleaseInput(int index)
        {
            if (!inputs[index].IsValid()) return;
            blend.DisconnectInput(index);
            graph.DestroyPlayable(inputs[index]);
            inputs[index] = default;
        }

        public void ResetOutput()
        {
            sampling = false;
            current = -1;
            if (graph.IsValid()) graph.Destroy();
            inputs[0] = default;
            inputs[1] = default;
        }
        void OnDisable() => ResetOutput();
    }
}
