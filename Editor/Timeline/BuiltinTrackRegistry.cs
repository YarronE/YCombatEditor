namespace Ethan.ActionEditor.Editor
{
    internal static class BuiltinTrackRegistry
    {
        internal static string GetValidationListProperty(string track)
        {
            switch (track)
            {
                case "Flow": return "flowNodes";
                case "Camera": return "cameraCues";
                case "Animation": return "animSegments";
                case "Attack": return "attackList";
                case "Phase2Attack": return "phase2AttackList";
                case "FxAndSound": return "fxList";
                case "Phase2Fx": return "phase2FxList";
                case "Jump": return "jumpList";
                case "CancelPoint": return "cancelList";
                case "Projectile": return "projectileList";
                case "WarningCue": return "warningCueList";
                case "HitFx": return "hitFxList";
                case "SuperArmor": return "superArmorList";
                case "AdjustMotion": return "adjustMotionList";
                case "Trail": return "trailToggleList";
                case "Move": return "moveSegmentList";
                case "Interaction": return "interactionWindows";
                default: return null;
            }
        }

        // Empty events must remain selectable until a reference is assigned.
        internal static bool IsVisibleInFxTrack(Global.FxAndSound item) => item != null && (item.particleSystem != null || item.audioClip == null);
        internal static bool IsVisibleInSoundTrack(Global.FxAndSound item) => item != null && (item.audioClip != null || item.particleSystem == null);

        internal static Global.TrackType Canonical(Global.TrackType type) =>
            type == Global.TrackType.Jump || type == Global.TrackType.Cancel ? Global.TrackType.Flow : type == Global.TrackType.Sound || type == Global.TrackType.Warning ? Global.TrackType.Fx : type == Global.TrackType.AdjustMotion ? Global.TrackType.Move : type;

        internal static string GetCategory(Global.TrackType type)
        {
            switch (Canonical(type))
            {
                case Global.TrackType.Animation:
                case Global.TrackType.Fx:
                case Global.TrackType.Camera:
                case Global.TrackType.HitFx:
                case Global.TrackType.Warning:
                case Global.TrackType.Trail: return "Presentation";
                default: return "Mechanics";
            }
        }
    }
}
