using UnityEngine;

namespace Ethan.ActionEditor
{
    public sealed partial class ActionPlayer
    {
        ActionPlayRequest _flowRequest;
        bool _evaluatingFlow;

        /// <summary>Explicit command request, then optional free exit. Rejection leaves the current action intact.</summary>
        public bool TryRequestFlow(string command, SkillConfigSO requestedAction, string category, out ActionPlayError error)
        {
            error = new ActionPlayError("ACT_FLOW_CLOSED", "No matching open flow rule.");
            if (!_playing || _externallyDriven || _stopping || _evaluatingFlow || IsPaused) return false;
            var source = _config;
            int generation = _generation;
            _evaluatingFlow = true;
            try
            {
                if (source.flowNodes != null) foreach (var node in source.flowNodes) {
                    if (ActionFlow.MatchesBranch(source, node, gameObject, _currentFrame, command, false) &&
                        TryStartFlowAction(node.nextAction, out error)) return true;
                    if (!_playing || generation != _generation) return false;
                }
                if (requestedAction != null && ActionFlow.CanExit(source, gameObject, _currentFrame,
                    (int)requestedAction.cancelPriority, category, requestedAction))
                    return TryStartFlowAction(requestedAction, out error);
                return false;
            }
            finally { _evaluatingFlow = false; }
        }

        bool TryStartFlowAction(SkillConfigSO next, out ActionPlayError error) =>
            TryPlay(new ActionPlayRequest(next, _phase, _speed, _frameRate, _flowRequest.RequireHandlers, _flowRequest.AllowLegacyEmptyFx), out error);

        bool TryAutomaticFlow(int frame)
        {
            if (_evaluatingFlow || _config?.flowNodes == null) return false;
            var source = _config;
            int generation = _generation;
            _evaluatingFlow = true;
            try
            {
                foreach (var node in source.flowNodes) {
                    if (ActionFlow.MatchesBranch(source, node, gameObject, frame, null, true) &&
                        TryStartFlowAction(node.nextAction, out _)) return true;
                    if (!_playing || generation != _generation) return true;
                }
                return false;
            }
            finally { _evaluatingFlow = false; }
        }
    }
}
