using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Read-only project condition. Costs and state changes belong to successful action activation.</summary>
    public abstract class ActionFlowCondition : ScriptableObject
    {
        public abstract bool Evaluate(GameObject actor, SkillConfigSO current, SkillConfigSO next, int frame);
    }
}
