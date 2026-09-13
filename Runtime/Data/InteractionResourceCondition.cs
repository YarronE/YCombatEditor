using UnityEngine;

namespace Ethan.ActionEditor
{
    public interface IInteractionResourceProvider
    {
        bool TryGetInteractionResource(string resource, out float value);
    }

    [CreateAssetMenu(menuName = "Combat/Interaction Conditions/Resource", fileName = "ResourceCondition")]
    public sealed class InteractionResourceCondition : InteractionCondition
    {
        public string resource = "energy";
        public float minimum = 10;
        public override string ConfigurationError => string.IsNullOrWhiteSpace(resource) || float.IsNaN(minimum) || float.IsInfinity(minimum)
            ? "Resource name and a finite threshold are required." : null;
        public override bool Evaluate(in InteractionQuery query, out string rejection)
        {
            rejection = ConfigurationError;
            if (rejection != null) return false;
            if (query.Actor != null)
                foreach (var component in query.Actor.GetComponents<MonoBehaviour>())
                    if (component is IInteractionResourceProvider provider && provider.TryGetInteractionResource(resource, out float value))
                    {
                        if (!float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum) return true;
                        rejection = "Insufficient resource: " + resource; return false;
                    }
            rejection = "Missing resource provider: " + resource; return false;
        }
    }
}
