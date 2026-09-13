using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Actor-neutral facing windows. Explicit locks win; gaps hold facing once windows are authored.</summary>
    public static class ActionMotionPolicy
    {
        public static bool HasWindows(SkillConfigSO config) => config?.adjustMotionList != null && config.adjustMotionList.Count > 0;

        public static Global.AdjustMotionSegment Resolve(SkillConfigSO config, int frame)
        {
            Global.AdjustMotionSegment selected = null;
            if (!HasWindows(config)) return null;
            foreach (var window in config.adjustMotionList)
            {
                if (window == null || frame < window.keyNumber || frame > Mathf.Max(window.keyNumber, window.endKeyNumber)) continue;
                if (!window.allowTurning) return window;
                if (selected == null) selected = window;
            }
            return selected;
        }

        public static bool AllowsTurning(SkillConfigSO config, int frame) => !HasWindows(config) || Resolve(config, frame)?.allowTurning == true;

        public static Quaternion Face(Quaternion current, Vector3 direction, Global.AdjustMotionSegment window, float deltaTime)
        {
            if (window == null || !window.allowTurning) return current;
            if (window.yAxisOnly) direction.y = 0;
            if (direction.sqrMagnitude < .000001f) return current;
            var target = Quaternion.LookRotation(direction.normalized);
            return window.rotationSpeed <= 0 ? target : Quaternion.RotateTowards(current, target, window.rotationSpeed * Mathf.Max(0, deltaTime));
        }
    }
}
