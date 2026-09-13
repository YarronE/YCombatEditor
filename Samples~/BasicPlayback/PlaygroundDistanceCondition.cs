using UnityEngine;

namespace Ethan.ActionEditor.Samples
{
    [CreateAssetMenu(menuName="ACT/Samples/Distance Condition")]
    public sealed class PlaygroundDistanceCondition : InteractionCondition
    {
        public float maximumDistance = 3;
        public override string ConfigurationError => maximumDistance <= 0 || float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance) ? "Distance must be finite and positive." : null;
        public override bool Evaluate(in InteractionQuery query, out string rejection)
        {
            bool accepted=query.Actor!=null && query.Other!=null && Vector3.Distance(query.Actor.transform.position,query.Other.position)<=maximumDistance;
            rejection=accepted ? null : "Source is missing or outside the configured distance.";
            return accepted;
        }
    }
}
