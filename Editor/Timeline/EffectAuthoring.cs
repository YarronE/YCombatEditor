namespace Ethan.ActionEditor.Editor
{
    /// <summary>Effect subtype and field policy shared by the timeline and inspector.</summary>
    public static class EffectAuthoring
    {
        public static Global.EffectContentKind Resolve(Global.FxAndSound effect)
        {
            if (effect.contentKind != Global.EffectContentKind.Legacy) return effect.contentKind;
            return effect.audioClip != null && effect.particleSystem == null
                ? Global.EffectContentKind.Audio : Global.EffectContentKind.VisualEffect;
        }

        public static string Label(Global.FxAndSound effect)
        {
            if (effect.contentKind == Global.EffectContentKind.Legacy && effect.particleSystem != null && effect.audioClip != null)
                return "VFX + Audio: " + effect.particleSystem.name + " / " + effect.audioClip.name;
            return Resolve(effect) == Global.EffectContentKind.Audio
                ? "Audio: " + (effect.audioClip != null ? effect.audioClip.name : "Assign clip")
                : "VFX: " + (effect.particleSystem != null ? effect.particleSystem.name : "Assign effect");
        }

        public static bool IsFieldVisible(Global.EffectContentKind kind, string field, bool advanced)
        {
            if (field == "keyNumber" || (advanced && field == "endKeyNumber")) return true;
            if (kind == Global.EffectContentKind.Audio)
                return field == "audioClip" || field == "baseVolume" ||
                    (advanced && (field == "audioRandomize" || field == "pitchVariation" || field == "volumeVariation"));
            return field == "particleSystem" || field == "offset" || field == "rotation" || field == "scale" ||
                field == "followCharacter" || field == "customLifetime" ||
                (advanced && (field == "endKeyNumber" || field == "playbackSpeed" || field == "useWorldSpace"));
        }
    }
}
