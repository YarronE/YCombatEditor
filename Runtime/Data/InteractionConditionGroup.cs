using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    public enum InteractionConditionOperator { All, Any, Not }

    /// <summary>Reusable pure predicates. Invalid graphs fail closed, including below Not.</summary>
    [CreateAssetMenu(menuName = "ACT/Interaction/Condition Group")]
    public sealed class InteractionConditionGroup : InteractionCondition
    {
        public InteractionConditionOperator operation;
        public List<InteractionCondition> children = new List<InteractionCondition>();

        public override string ConfigurationError => ValidateGraph(this, new HashSet<InteractionCondition>());

        static string ValidateGraph(InteractionCondition condition, HashSet<InteractionCondition> visiting)
        {
            if (condition == null) return "Missing condition.";
            if (!visiting.Add(condition)) return "Condition graph contains a cycle.";
            try
            {
                if (!(condition is InteractionConditionGroup group)) return condition.ConfigurationError;
                if (!System.Enum.IsDefined(typeof(InteractionConditionOperator), group.operation)) return "Unknown condition operator.";
                if (group.children == null || group.children.Count == 0) return "Condition group must contain children.";
                if (group.operation == InteractionConditionOperator.Not && group.children.Count != 1) return "Not requires exactly one child.";
                foreach (var child in group.children)
                {
                    string error = ValidateGraph(child, visiting);
                    if (!string.IsNullOrEmpty(error)) return error;
                }
                return null;
            }
            finally { visiting.Remove(condition); }
        }

        public override bool Evaluate(in InteractionQuery query, out string rejection)
        {
            rejection = ConfigurationError;
            if (!string.IsNullOrEmpty(rejection)) return false;
            try { return EvaluateValid(query, null, name, out rejection); }
            catch (System.Exception exception) { rejection = "Condition failed: " + exception.Message; return false; }
        }

        internal bool EvaluateValid(in InteractionQuery query, List<InteractionConditionTrace> trace, string path, out string rejection)
        {
            bool all = true, any = false;
            rejection = null;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                string childPath = path + "/" + i + ":" + child.name;
                bool accepted = InteractionConditionEvaluation.Evaluate(child, query, trace, childPath, out string reason, true);
                all &= accepted;
                any |= accepted;
                if (!accepted && rejection == null) rejection = reason;
            }
            bool result = operation == InteractionConditionOperator.All ? all : operation == InteractionConditionOperator.Any ? any : !any;
            rejection = result ? null : rejection ?? "Condition group rejected the query.";
            return result;
        }
    }
}
