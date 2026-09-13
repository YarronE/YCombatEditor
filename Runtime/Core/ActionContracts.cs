using System;
using UnityEngine;

namespace Ethan.ActionEditor
{
    public enum ActionStopReason
    {
        Completed,
        Interrupted,
        Replaced,
        InvalidConfiguration,
        OwnerDisabled
    }

    public enum ActionEventPhase
    {
        Enter,
        Tick,
        Exit
    }

    public readonly struct ActionPlayRequest
    {
        public ActionPlayRequest(
            SkillConfigSO config,
            int phase = 1,
            float speed = 1f,
            float fallbackFrameRate = 30f,
            bool requireHandlers = true,
            bool allowLegacyEmptyFx = false)
        {
            Config = config;
            Phase = Mathf.Max(1, phase);
            Speed = Mathf.Max(0f, speed);
            FallbackFrameRate = Mathf.Max(1f, fallbackFrameRate);
            RequireHandlers = requireHandlers;
            AllowLegacyEmptyFx = allowLegacyEmptyFx;
        }

        public SkillConfigSO Config { get; }
        public int Phase { get; }
        public float Speed { get; }
        public float FallbackFrameRate { get; }
        public bool RequireHandlers { get; }
        public bool AllowLegacyEmptyFx { get; }
    }

    public readonly struct ActionPlayError
    {
        public ActionPlayError(string code, string message, ActionValidationIssue[] issues = null)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Issues = issues ?? Array.Empty<ActionValidationIssue>();
        }

        public string Code { get; }
        public string Message { get; }
        public ActionValidationIssue[] Issues { get; }
        public bool HasError => !string.IsNullOrEmpty(Code);

        public static ActionPlayError None => new ActionPlayError(string.Empty, string.Empty);
    }

    public readonly struct ActionExecutionContext
    {
        public ActionExecutionContext(
            GameObject actor,
            Transform target,
            SkillConfigSO config,
            int frame,
            int phase,
            ActionEventPhase eventPhase)
        {
            Actor = actor;
            Target = target;
            Config = config;
            Frame = frame;
            Phase = phase;
            EventPhase = eventPhase;
        }

        public GameObject Actor { get; }
        public Transform Target { get; }
        public SkillConfigSO Config { get; }
        public int Frame { get; }
        public int Phase { get; }
        public ActionEventPhase EventPhase { get; }
    }

    public interface IActionAnimator
    {
        bool CanPlay(Global.AnimClipSegment segment);
        void Play(Global.AnimClipSegment segment, float fallbackFrameRate);
        void Stop(ActionStopReason reason);
    }

    public interface IActionTargetProvider
    {
        Transform ResolveTarget(Global.MoveTargetKind kind);
    }

    /// <summary>Optional animation backend synchronized to the player's absolute frame clock.</summary>
    public interface IActionAnimationClock
    {
        void Sample(float timelineFrame);
    }

    public interface IActionRootMotionSource
    {
        void SetTakeover(bool enabled);
        void Clear();
        Vector3 ConsumeDelta();
        Quaternion ConsumeDeltaRotation();
    }

    /// <summary>Marker for colliders that represent another action actor.</summary>
    public interface IActionActor
    {
    }

    public interface IActionEventHandler<TEvent>
    {
        void Handle(in ActionExecutionContext context, TEvent data);
    }
}
