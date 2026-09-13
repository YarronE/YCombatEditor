using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Shared flow policy. Input, AI selection and action activation remain host-owned.</summary>
    public static class ActionFlow
    {
        public static int CompleteFrame(SkillConfigSO config)
        {
            int result = -1;
            if (config?.flowNodes != null) foreach (var node in config.flowNodes)
                if (node != null && node.kind == ActionFlowKind.Complete && node.keyNumber >= 0)
                    result = result < 0 ? node.keyNumber : Mathf.Min(result, node.keyNumber);
            return result;
        }

        public static bool IsComplete(SkillConfigSO config, int frame)
        {
            int end = CompleteFrame(config);
            return end >= 0 && frame >= end;
        }

        public static bool IsOpen(SkillConfigSO config, ActionFlowNode node, int frame) =>
            config != null && node != null && node.kind != ActionFlowKind.Complete &&
            frame >= node.keyNumber && frame <= config.RuntimeEndFrame && !IsComplete(config, frame) &&
            (!node.limitWindow || frame <= node.endKeyNumber);

        public static bool ConditionsPass(ActionFlowNode node, GameObject actor, SkillConfigSO current, SkillConfigSO next, int frame)
        {
            if (node.conditions != null) foreach (var condition in node.conditions)
                if (condition == null || !condition.Evaluate(actor, current, next, frame)) return false;
            return true;
        }

        public static bool MatchesBranch(SkillConfigSO config, ActionFlowNode node, GameObject actor, int frame, string command, bool automatic) =>
            node != null && node.kind == ActionFlowKind.Branch && node.nextAction != null && IsOpen(config, node, frame) &&
            node.automatic == automatic && (automatic || (!string.IsNullOrEmpty(command) && node.command == command)) &&
            ConditionsPass(node, actor, config, node.nextAction, frame);

        public static bool CanExit(SkillConfigSO config, GameObject actor, int frame, int nextPriority, string category, SkillConfigSO next = null)
        {
            if (config?.flowNodes == null) return false;
            foreach (var node in config.flowNodes)
            {
                if (node == null || node.kind != ActionFlowKind.AllowExit || !IsOpen(config, node, frame)) continue;
                if (!string.IsNullOrEmpty(node.category) && node.category != category) continue;
                bool priority = node.priorityRule == ActionFlowPriority.Any ||
                    (node.priorityRule == ActionFlowPriority.HigherThanCurrent && nextPriority > (int)config.cancelPriority) ||
                    (node.priorityRule == ActionFlowPriority.AtLeast && nextPriority >= (int)node.minimumPriority);
                if (priority && ConditionsPass(node, actor, config, next, frame)) return true;
            }
            return false;
        }
    }
}
