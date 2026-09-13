using UnityEngine;

namespace Ethan.ActionEditor
{
    [CreateAssetMenu(menuName = "Combat/Interaction Conditions/Attack Filter", fileName = "AttackCondition")]
    public sealed class InteractionAttackCondition : InteractionCondition
    {
        public bool requireBlockable;
        public bool requireParryable;
        [Tooltip("Empty matches any attack tag.")]
        public string requiredTag;
        public override bool Evaluate(in InteractionQuery query, out string rejection)
        {
            rejection = null;
            if (query.Attack == null) { rejection = "No incoming attack."; return false; }
            if (requireBlockable && query.Attack.unblockable) { rejection = "Attack cannot be blocked."; return false; }
            if (requireParryable && query.Attack.unparryable) { rejection = "Attack cannot be parried."; return false; }
            if (!string.IsNullOrEmpty(requiredTag) && query.Attack.interactionTag != requiredTag)
            { rejection = "Attack tag does not match."; return false; }
            return true;
        }
    }
}
