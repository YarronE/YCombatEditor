using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Pure query: do not consume resources or modify actors here. Return a diagnostic on rejection.</summary>
    public abstract class InteractionCondition : ScriptableObject
    {
        public abstract bool Evaluate(in InteractionQuery query, out string rejection);
        public virtual string ConfigurationError => null;
    }
}
