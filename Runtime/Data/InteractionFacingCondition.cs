using UnityEngine;

namespace Ethan.ActionEditor
{
    [CreateAssetMenu(menuName = "Combat/Interaction Conditions/Facing", fileName = "FacingCondition")]
    public sealed class InteractionFacingCondition : InteractionCondition
    {
        [Range(0, 360), Tooltip("Full cone angle centered on defender forward.")]
        public float angle = 120;
        public override string ConfigurationError => float.IsNaN(angle) || float.IsInfinity(angle) || angle < 0 || angle > 360
            ? "Facing angle must be between 0 and 360." : null;
        public override bool Evaluate(in InteractionQuery query, out string rejection)
        {
            rejection = ConfigurationError;
            if (rejection != null) return false;
            if (query.Actor == null || query.Other == null) { rejection = "Missing actor or incoming source."; return false; }
            Vector3 direction = query.Other.position - query.Actor.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .0001f && Vector3.Angle(query.Actor.transform.forward, direction) > angle * .5f)
            { rejection = "Incoming source is outside the facing cone."; return false; }
            return true;
        }
    }
}
