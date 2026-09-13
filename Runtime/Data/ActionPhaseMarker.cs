using System;
namespace Ethan.ActionEditor
{
    [Serializable] public sealed class ActionPhaseMarker
    {
        public string id="Startup";
        public int beginFrame,endFrame;
    }
    /// <summary>Optional consumer facts. Implementations must be read-only during condition evaluation.</summary>
    public interface IInteractionFacts
    {
        bool TryGetFact(string key,out string value);
    }
}
