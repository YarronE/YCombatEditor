using System;
using System.Collections.Generic;

namespace Ethan.ActionEditor
{
    [Serializable]
    public sealed class InteractionConditionTrace
    {
        public string path;
        public bool accepted;
        public string reason;
    }

    [Serializable]
    public sealed class InteractionWindowTrace
    {
        public int index, frame, priority;
        public string id, reason;
        public bool accepted, selected;
        public List<InteractionConditionTrace> conditions = new List<InteractionConditionTrace>();
    }

    internal static class InteractionConditionEvaluation
    {
        public static bool Evaluate(InteractionCondition condition, in InteractionQuery query,
            List<InteractionConditionTrace> trace, string path, out string rejection, bool propagateException = false)
        {
            bool accepted = false;
            rejection = null;
            try
            {
                rejection = condition == null ? "Missing condition." : condition.ConfigurationError;
                if (string.IsNullOrEmpty(rejection))
                    accepted = condition is InteractionConditionGroup group
                        ? group.EvaluateValid(query, trace, path, out rejection)
                        : condition.Evaluate(query, out rejection);
            }
            catch (Exception exception) { if (propagateException) throw; rejection = "Condition failed: " + exception.Message; }
            trace?.Add(new InteractionConditionTrace { path = path, accepted = accepted, reason = rejection });
            return accepted;
        }
    }
}
