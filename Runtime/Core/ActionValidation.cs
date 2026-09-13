using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    public enum ActionValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public readonly struct ActionValidationIssue
    {
        public ActionValidationIssue(
            string code,
            ActionValidationSeverity severity,
            string track,
            int eventIndex,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            Track = track ?? string.Empty;
            EventIndex = eventIndex;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public ActionValidationSeverity Severity { get; }
        public string Track { get; }
        public int EventIndex { get; }
        public string Message { get; }
    }

    public sealed class ActionValidationCapabilities
    {
        public Func<Type, bool> CanHandleEvent { get; set; }
        public bool HasAnimator { get; set; } = true;
        public bool RequireEventHandlers { get; set; }
        public bool AllowLegacyEmptyFx { get; set; }
    }

    public static class ActionConfigValidator
    {
        public static List<ActionValidationIssue> Validate(
            SkillConfigSO config,
            ActionValidationCapabilities capabilities = null)
        {
            var issues = new List<ActionValidationIssue>();
            if (config == null)
            {
                issues.Add(Error("ACT000", "Config", -1, "Action config is null."));
                return issues;
            }

            if (config.timingVersion < 0 || config.timingVersion > 1)
                issues.Add(Error("ACT150", "Timing", -1, "Unsupported timing version."));
            if (config.UsesExplicitTiming && (float.IsNaN(config.timelineFrameRate) || float.IsInfinity(config.timelineFrameRate) || config.timelineFrameRate < 1 || config.timelineFrameRate > 240))
                issues.Add(Error("ACT151", "Timing", -1, "Timeline FPS must be finite and between 1 and 240."));
            if (!config.UsesExplicitTiming)
            {
                float firstRate = ActionTiming.FrameRate(config);
                foreach (var segment in config.GetEffectiveSegments())
                    if (segment?.clip != null && !Mathf.Approximately(firstRate, ActionTiming.SourceRate(segment)))
                    { issues.Add(new ActionValidationIssue("ACT152", ActionValidationSeverity.Warning, "Timing", -1, "Legacy mixed clip FPS: preview and host timing may differ. Explicit migration required.")); break; }
            }
            int lastExecutableFrame = config.exitFrame > 0 ? config.exitFrame : -1;
            ValidateAnimation(config, capabilities, lastExecutableFrame, issues);
            ValidateRangeList(config.jumpList, "Jump", (x) => x.beginKey, (x) => x.endKey, true, issues, lastExecutableFrame);
            // Most legacy tracks intentionally treat end <= start as a one-frame event.
            // Keep that serialized contract so productization does not invalidate old assets.
            ValidateRangeList(config.attackList, "Attack", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.fxList, "FxAndSound", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.cancelList, "CancelPoint", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.projectileList, "Projectile", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.warningCueList, "WarningCue", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.superArmorList, "SuperArmor", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.adjustMotionList, "AdjustMotion", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.trailToggleList, "Trail", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.moveSegmentList, "Move", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.phase2AttackList, "Phase2Attack", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);
            ValidateRangeList(config.phase2FxList, "Phase2Fx", (x) => x.keyNumber, (x) => x.endKeyNumber, false, issues, lastExecutableFrame);

            ValidateRangeList(config.cameraCues, "Camera", x => x.keyNumber, x => x.endKeyNumber, true, issues, lastExecutableFrame);
            ValidateHandler<ActionCameraCue>(config.cameraCues, "Camera", capabilities, issues);
            if (config.cameraCues != null) for (int i = 0; i < config.cameraCues.Count; i++) {
                var cue = config.cameraCues[i];
                if (cue != null && (!Finite(cue.shakeAmplitude) || cue.shakeAmplitude < 0 || !Finite(cue.shakeFrequency) || cue.shakeFrequency < 0 || !Finite(cue.fieldOfViewOffset) || !Finite(cue.positionOffset) || !Finite(cue.rotationOffset) || cue.envelope == null || cue.envelope.length == 0 || cue.endKeyNumber < cue.keyNumber))
                    issues.Add(Error("ACT160", "Camera", i, "Camera values must be finite, shake values nonnegative, and an envelope is required."));
            }
            if(config.cameraCues!=null) for(int i=0;i<config.cameraCues.Count;i++) {
                var envelope=config.cameraCues[i]?.envelope;
                if(envelope!=null) foreach(var key in envelope.keys)
                    if(!Finite(key.time)||!Finite(key.value)) {issues.Add(Error("ACT162","Camera",i,"Envelope key times and values must be finite."));break;}
            }
            if (config.adjustMotionList != null) for (int i = 0; i < config.adjustMotionList.Count; i++) {
                var turn = config.adjustMotionList[i];
                if (turn != null && (!Finite(turn.rotationSpeed) || turn.rotationSpeed < 0 || !Enum.IsDefined(typeof(Global.MoveTargetKind), turn.targetKind)))
                    issues.Add(Error("ACT161", "AdjustMotion", i, "Turning requires a valid target kind and a finite nonnegative speed."));
            }
            ValidateProjectileReferences(config.projectileList, issues);
            bool legacyEmptyFx = !config.UsesExplicitTiming && capabilities?.AllowLegacyEmptyFx == true;
            ValidateEffectReferences(config.fxList, "FxAndSound", issues, legacyEmptyFx);
            ValidateEffectReferences(config.phase2FxList, "Phase2Fx", issues, legacyEmptyFx);
            ValidateHitEffectReferences(config.hitFxList, issues);
            ValidateWarningReferences(config.warningCueList, issues);
            ValidateMoveSegments(config.moveSegmentList, issues);
            ValidateAdjustMotionConflicts(config.adjustMotionList, issues);
            ValidateInteractions(config.interactionWindows, lastExecutableFrame, issues);
            if(config.phases!=null)for(int i=0;i<config.phases.Count;i++) {
                var phase=config.phases[i];
                if(phase==null || string.IsNullOrWhiteSpace(phase.id) || phase.beginFrame<0 || phase.endFrame<phase.beginFrame)
                    issues.Add(Error("ACT153","Phases",i,"Phase requires an ID and an ordered non-negative frame range."));
            }
            ValidateHandler<Global.Jump>(config.jumpList, "Jump", capabilities, issues);
            ValidateHandler<Global.Attack>(config.attackList, "Attack", capabilities, issues);
            ValidateHandler<Global.FxAndSound>(config.fxList, "FxAndSound", capabilities, issues);
            ValidateHandler<Global.CancelPoint>(config.cancelList, "CancelPoint", capabilities, issues);
            ValidateHandler<Global.Projectile>(config.projectileList, "Projectile", capabilities, issues);
            ValidateHandler<Global.WarningCue>(config.warningCueList, "WarningCue", capabilities, issues);
            ValidateHandler<Global.SuperArmorSegment>(config.superArmorList, "SuperArmor", capabilities, issues);
            ValidateHandler<Global.AdjustMotionSegment>(config.adjustMotionList, "AdjustMotion", capabilities, issues);
            ValidateHandler<Global.TrailToggle>(config.trailToggleList, "Trail", capabilities, issues);
            ValidateHandler<Global.MoveSegment>(config.moveSegmentList, "Move", capabilities, issues);
            ValidateHandler<Global.Attack>(config.phase2AttackList, "Phase2Attack", capabilities, issues);
            ValidateHandler<Global.FxAndSound>(config.phase2FxList, "Phase2Fx", capabilities, issues);

            if (config.phase2RangeMultiplier <= 0f)
                issues.Add(Error("ACT121", "Phase2", -1, "Phase 2 range multiplier must be greater than zero."));
            if (config.moveCancelFrame < -1)
                issues.Add(Error("ACT122", "Config", -1, "Move cancel frame must be -1 or a non-negative frame."));
            if (config.exitFrame < 0)
                issues.Add(Error("ACT125", "Config", -1, "Exit frame cannot be negative."));

            return issues;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        public static bool HasErrors(IReadOnlyList<ActionValidationIssue> issues)
        {
            if (issues == null) return false;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == ActionValidationSeverity.Error) return true;
            return false;
        }

        static void ValidateInteractions(List<InteractionWindow> windows, int exitFrame, List<ActionValidationIssue> issues)
        {
            if (windows == null) return;
            var ids = new HashSet<string>();
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                string error = null;
                if (w == null) error = "Interaction window is null.";
                else if (string.IsNullOrWhiteSpace(w.id) || !ids.Add(w.id)) error = "Window IDs must be nonempty and unique within an action.";
                else if (string.IsNullOrWhiteSpace(w.signal)) error = "Interaction signal cannot be empty.";
                else if (w.keyNumber < 0 || w.endKeyNumber < w.keyNumber || (exitFrame >= 0 && w.endKeyNumber > exitFrame)) error = "Window must have an inclusive range inside the action exit frame.";
                else if (!Enum.IsDefined(typeof(InteractionResponse), w.response)) error = "Unknown interaction response.";
                else if (float.IsNaN(w.sourceAngle) || float.IsInfinity(w.sourceAngle) || w.sourceAngle < 0 || w.sourceAngle > 360) error = "Source angle must be between 0 and 360.";
                else if (w.maxActivations < 0 || float.IsNaN(w.damageMultiplier) || float.IsInfinity(w.damageMultiplier) || w.damageMultiplier < 0 || w.damageMultiplier > 1) error = "Activation limit must be >= 0 and damage multiplier between 0 and 1.";
                if (error == null && w.conditions != null)
                    foreach (var condition in w.conditions)
                    {
                        if (condition == null) { error = "Interaction condition reference is missing."; break; }
                        if (condition.ConfigurationError != null) { error = condition.ConfigurationError; break; }
                    }
                if (error != null) issues.Add(Error("ACT140", "Interaction", i, error));
            }
        }

        static void ValidateAnimation(
            SkillConfigSO config,
            ActionValidationCapabilities capabilities,
            int lastExecutableFrame,
            List<ActionValidationIssue> issues)
        {
            var segments = config.GetEffectiveSegments();
            if (segments == null || segments.Count == 0)
            {
                issues.Add(Error("ACT101", "Animation", -1, "At least one animation segment is required."));
                return;
            }

            if (capabilities != null && !capabilities.HasAnimator)
                issues.Add(Error("ACT102", "Animation", -1, "No IActionAnimator capability is available."));

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null || segment.clip == null)
                {
                    issues.Add(Error("ACT103", "Animation", i, "Animation segment has no clip."));
                    continue;
                }

                if (config.UsesExplicitTiming && ActionTiming.SourceEnd(segment) <= ActionTiming.SourceStart(segment))
                    issues.Add(Error("ACT153", "Animation", i, "Explicit animation crop must have a positive duration."));
                if (segment.startFrame < 0 || segment.clipStartFrame < 0 || segment.clipEndFrame < 0)
                    issues.Add(Error("ACT104", "Animation", i, "Animation frames cannot be negative."));
                if (segment.clipEndFrame > 0 && segment.clipEndFrame < segment.clipStartFrame)
                    issues.Add(Error("ACT105", "Animation", i, "Clip end frame is earlier than clip start frame."));
                if (lastExecutableFrame >= 0 && segment.startFrame > lastExecutableFrame)
                    issues.Add(Error("ACT116", "Animation", i, "Animation starts after the configured exit frame."));
            }
        }

        static void ValidateRangeList<T>(
            List<T> list,
            string track,
            Func<T, int> getStart,
            Func<T, int> getEnd,
            bool rejectReversedPositiveRange,
            List<ActionValidationIssue> issues,
            int lastExecutableFrame = -1)
        {
            if (list == null)
            {
                issues.Add(new ActionValidationIssue("ACT110", ActionValidationSeverity.Warning, track, -1, "Event list is null; initialize it before authoring."));
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], null))
                {
                    issues.Add(Error("ACT111", track, i, "Event entry is null."));
                    continue;
                }

                int start = getStart(list[i]);
                int end = getEnd(list[i]);
                if (start < 0 || end < 0)
                    issues.Add(Error("ACT112", track, i, "Event frames cannot be negative."));
                else if (rejectReversedPositiveRange && end > 0 && end < start)
                    issues.Add(Error("ACT113", track, i, "Event end frame is earlier than its start frame."));
                int effectiveEnd = end > start ? end : start;
                if (lastExecutableFrame >= 0 && (start > lastExecutableFrame || effectiveEnd > lastExecutableFrame))
                    issues.Add(Error("ACT116", track, i, "Event is outside the configured exit frame."));
            }
        }

        static void ValidateEffectReferences(
            List<Global.FxAndSound> effects,
            string track,
            List<ActionValidationIssue> issues, bool allowEmpty = false)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect != null && effect.contentKind != Global.EffectContentKind.Legacy)
                {
                    bool valid = effect.contentKind == Global.EffectContentKind.VisualEffect
                        ? effect.particleSystem != null && effect.audioClip == null
                        : effect.contentKind == Global.EffectContentKind.Audio && effect.audioClip != null && effect.particleSystem == null;
                    if (!valid) issues.Add(Error("ACT163", track, i,
                        "Typed Effect event requires exactly its matching reference: Visual Effect uses a particle system; Audio uses an audio clip."));
                    continue;
                }
                if (effect != null && effect.particleSystem == null && effect.audioClip == null)
                    issues.Add(new ActionValidationIssue("ACT115", allowEmpty ? ActionValidationSeverity.Warning : ActionValidationSeverity.Error,
                        track, i, allowEmpty ? "Legacy empty FX/Sound placeholder is skipped during playback." : "FX/Sound event requires a particle system or audio clip."));
            }
        }

        static void ValidateHitEffectReferences(List<Global.HitFx> effects, List<ActionValidationIssue> issues)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect != null && effect.hitVfx == null && effect.hitSound == null)
                    issues.Add(Error("ACT117", "HitFx", i, "Hit effect requires a VFX or audio reference."));
            }
        }

        static void ValidateWarningReferences(List<Global.WarningCue> warnings, List<ActionValidationIssue> issues)
        {
            if (warnings == null) return;
            for (int i = 0; i < warnings.Count; i++)
            {
                var warning = warnings[i];
                if (warning != null && warning.warningVfx == null && warning.warningSound == null)
                    issues.Add(Error("ACT118", "WarningCue", i, "Warning cue requires a VFX or audio reference."));
            }
        }

        static void ValidateProjectileReferences(
            List<Global.Projectile> projectiles,
            List<ActionValidationIssue> issues)
        {
            if (projectiles == null) return;
            for (int i = 0; i < projectiles.Count; i++)
            {
                var projectile = projectiles[i];
                if (projectile != null && projectile.prefab == null)
                    issues.Add(Error("ACT114", "Projectile", i, "Projectile prefab is required."));
            }
        }

        static void ValidateMoveSegments(List<Global.MoveSegment> segments, List<ActionValidationIssue> issues)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null) continue;
                if (segment.endKeyNumber == segment.keyNumber && Mathf.Approximately(segment.maxDistance, 0f))
                    issues.Add(new ActionValidationIssue("ACT120", ActionValidationSeverity.Warning, "Move", i, "Move segment has no duration and no distance."));
                if (segment.maxDistance < 0f || segment.arriveTolerance < 0f || segment.minKeepDistance < 0f)
                    issues.Add(Error("ACT123", "Move", i, "Move distances and tolerances cannot be negative."));
                if (segment.driveMode == Global.MoveDriveMode.Program && segment.curve == null)
                    issues.Add(Error("ACT124", "Move", i, "Program-driven move segment requires a cumulative movement curve."));
            }
        }

        static void ValidateAdjustMotionConflicts(
            List<Global.AdjustMotionSegment> segments,
            List<ActionValidationIssue> issues)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Count; i++)
            {
                var left = segments[i];
                if (left == null) continue;
                int leftEnd = left.endKeyNumber > left.keyNumber ? left.endKeyNumber : left.keyNumber;
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var right = segments[j];
                    if (right == null) continue;
                    int rightEnd = right.endKeyNumber > right.keyNumber ? right.endKeyNumber : right.keyNumber;
                    bool overlaps = left.keyNumber <= rightEnd && right.keyNumber <= leftEnd;
                    if (overlaps && (!Mathf.Approximately(left.rotationSpeed, right.rotationSpeed) || left.yAxisOnly != right.yAxisOnly))
                    {
                        issues.Add(Error("ACT130", "AdjustMotion", j, $"Conflicts with overlapping AdjustMotion event {i}."));
                    }
                }
            }
        }

        static void ValidateHandler<T>(
            List<T> events,
            string track,
            ActionValidationCapabilities capabilities,
            List<ActionValidationIssue> issues)
        {
            if (events == null || events.Count == 0 || capabilities == null || !capabilities.RequireEventHandlers)
                return;
            if (capabilities.CanHandleEvent == null || !capabilities.CanHandleEvent(typeof(T)))
                issues.Add(Error("ACT201", track, -1, $"No IActionEventHandler<{typeof(T).Name}> is registered."));
        }

        static ActionValidationIssue Error(string code, string track, int index, string message)
        {
            return new ActionValidationIssue(code, ActionValidationSeverity.Error, track, index, message);
        }
    }
}
