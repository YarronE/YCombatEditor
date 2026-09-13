using System;
using System.Collections.Generic;

namespace Ethan.ActionEditor
{
    public static partial class ActionConfigValidator
    {
        static void WarnAfterComplete<T>(List<T> list,string track,Func<T,int> start,Func<T,int> end,int completion,List<ActionValidationIssue> issues) where T:class
        {
            if(list==null) return;
            for(int i=0;i<list.Count;i++) if(list[i]!=null && Math.Max(start(list[i]),end(list[i]))>=completion)
                issues.Add(new ActionValidationIssue("ACT177",ActionValidationSeverity.Warning,track,i,"Event reaches or follows Complete; gameplay at and after the completion frame will not execute."));
        }

        static void ValidateFlow(SkillConfigSO config, List<ActionValidationIssue> issues)
        {
            if (config.flowNodes == null) return;
            int completion = ActionFlow.CompleteFrame(config), completeCount = 0;
            for (int i = 0; i < config.flowNodes.Count; i++)
            {
                var n = config.flowNodes[i];
                if (n == null || !Enum.IsDefined(typeof(ActionFlowKind), n.kind) || n.keyNumber < 0 ||
                    (n.kind != ActionFlowKind.Complete && n.limitWindow && n.endKeyNumber < n.keyNumber))
                { issues.Add(Error("ACT170", "Flow", i, "Flow requires a valid kind and nonnegative, ordered frames.")); continue; }
                if (n.kind == ActionFlowKind.Complete) { completeCount++; continue; }
                if (n.kind == ActionFlowKind.Branch && (n.nextAction == null || (!n.automatic && string.IsNullOrWhiteSpace(n.command))))
                    issues.Add(Error("ACT171", "Flow", i, "Branch requires a target and a command unless Automatic is enabled."));
                if (n.kind == ActionFlowKind.Branch && n.automatic && n.keyNumber == 0 && n.nextAction == config)
                    issues.Add(Error("ACT172", "Flow", i, "An automatic branch at frame zero cannot target itself."));
                if (n.kind == ActionFlowKind.AllowExit && (!Enum.IsDefined(typeof(ActionFlowPriority), n.priorityRule) ||
                    (n.priorityRule == ActionFlowPriority.AtLeast && !Enum.IsDefined(typeof(Global.CancelPriority), n.minimumPriority))))
                    issues.Add(Error("ACT173", "Flow", i, "Allow Exit requires a valid priority rule."));
                if (n.conditions != null && n.conditions.Exists(c => c == null))
                    issues.Add(Error("ACT174", "Flow", i, "A Flow condition reference is missing."));
                if (completion >= 0 && n.keyNumber >= completion)
                    issues.Add(new ActionValidationIssue("ACT175", ActionValidationSeverity.Warning, "Flow", i, "Node starts at or after Complete and cannot execute."));
            }
            if (completion >= 0) {
                WarnAfterComplete(config.attackList,"Attack",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.phase2AttackList,"Phase2Attack",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.projectileList,"Projectile",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.moveSegmentList,"Move",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.adjustMotionList,"AdjustMotion",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.interactionWindows,"Interaction",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
                WarnAfterComplete(config.fxList,"FxAndSound",n=>n.keyNumber,n=>n.keyNumber,completion,issues);
                WarnAfterComplete(config.cameraCues,"Camera",n=>n.keyNumber,n=>n.endKeyNumber,completion,issues);
            }
            if (completeCount > 1 || (completeCount > 0 && config.exitFrame > 0))
                issues.Add(new ActionValidationIssue("ACT176", ActionValidationSeverity.Warning, "Flow", -1, "Multiple completion boundaries are configured. The earliest reachable boundary ends the action."));
        }
    }
}
