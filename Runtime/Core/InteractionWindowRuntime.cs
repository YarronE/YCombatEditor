using System;
using System.Collections.Generic;

namespace Ethan.ActionEditor
{
    /// <summary>Per-action state. Evaluate is read-only; Resolve commits exactly one winning window.</summary>
    public sealed class InteractionWindowRuntime
    {
        readonly Dictionary<int, int> activations = new Dictionary<int, int>();
        readonly List<InteractionWindowTrace> diagnostics = new List<InteractionWindowTrace>();
        public bool DiagnosticsEnabled { get; set; }
        public IReadOnlyList<InteractionWindowTrace> LastEvaluation => diagnostics;
        public void Clear() { activations.Clear(); diagnostics.Clear(); }

        public bool Evaluate(IReadOnlyList<InteractionWindow> windows, int frame, in InteractionQuery query,
            out InteractionResolution result, out string rejection)
        {
            result = default;
            diagnostics.Clear();
            rejection = "No active window for this signal.";
            if (windows == null) return false;
            int winner = -1;
            for (int i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                var trace = DiagnosticsEnabled ? new InteractionWindowTrace { index = i, frame = frame, id = window?.id, priority = window?.priority ?? 0 } : null;
                if (trace != null) diagnostics.Add(trace);
                if (window == null || frame < window.keyNumber || frame > window.endKeyNumber || window.signal != query.Signal)
                { string inactive = window==null ? "Missing window." : window.signal!=query.Signal ? "Signal mismatch." : frame<window.keyNumber ? "Before window." : "After window."; if (trace != null) trace.reason = inactive; rejection=inactive; continue; }
                if (window.sourceAngle < 360)
                {
                    if (query.Actor == null || query.Other == null) { rejection = "Missing interaction source."; if (trace != null) trace.reason = rejection; continue; }
                    var direction = query.Other.position - query.Actor.transform.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude > .0001f && UnityEngine.Vector3.Angle(query.Actor.transform.forward, direction) > window.sourceAngle * .5f)
                    { rejection = "Source outside window angle."; if (trace != null) trace.reason = rejection; continue; }
                }
                if (window.maxActivations > 0 && activations.TryGetValue(i, out int count) && count >= window.maxActivations)
                { rejection = "Window activation limit reached."; if (trace != null) trace.reason = rejection; continue; }
                if (query.Attack != null && ((window.response == InteractionResponse.Parry && query.Attack.unparryable)
                    || (window.response == InteractionResponse.Block && query.Attack.unblockable)))
                { rejection = "Incoming attack disallows this response."; if (trace != null) trace.reason = rejection; continue; }
                bool accepted = true;
                if (window.conditions != null)
                    foreach (var condition in window.conditions)
                    {
                        if (!InteractionConditionEvaluation.Evaluate(condition, query, trace?.conditions, condition == null ? "missing" : condition.name, out var reason))
                        { accepted = false; rejection = reason; if (!DiagnosticsEnabled) break; }
                    }
                if (trace != null) { trace.accepted = accepted; trace.reason = accepted ? null : rejection; }
                if (accepted && (winner < 0 || window.priority > windows[winner].priority)) winner = i;
            }
            if (winner < 0) return false;
            result = new InteractionResolution(windows[winner], winner, frame, !activations.ContainsKey(winner));
            if (DiagnosticsEnabled) diagnostics[winner].selected = true;
            rejection = null;
            return true;
        }

        public bool Resolve(IReadOnlyList<InteractionWindow> windows, int frame, in InteractionQuery query,
            out InteractionResolution result, out string rejection)
        {
            if (!Evaluate(windows, frame, query, out result, out rejection)) return false;
            activations.TryGetValue(result.Index, out int count);
            activations[result.Index] = count + 1;
            return true;
        }
    }
}
