using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    public enum ActionFlowKind { AllowExit, Branch, Complete }
    public enum ActionFlowPriority { HigherThanCurrent, Any, AtLeast }

    [Serializable]
    public sealed class ActionFlowNode
    {
        public ActionFlowKind kind;
        public int keyNumber;
        public bool limitWindow;
        public int endKeyNumber;
        public ActionFlowPriority priorityRule = ActionFlowPriority.HigherThanCurrent;
        public Global.CancelPriority minimumPriority;
        [Tooltip("Empty allows any category. Otherwise matches the category supplied by the host, e.g. Dodge or Movement.")]
        public string category;
        public SkillConfigSO nextAction;
        public bool automatic;
        public string command = "Attack";
        public List<ActionFlowCondition> conditions = new List<ActionFlowCondition>();
    }
}
