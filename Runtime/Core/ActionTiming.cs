using System.Collections.Generic;
using UnityEngine;

namespace Ethan.ActionEditor
{
    /// <summary>Source trim frames belong to the clip; all other explicit-mode frames belong to the action.</summary>
    public static class ActionTiming
    {
        public static float SourceRate(Global.AnimClipSegment segment) => segment?.clip != null && segment.clip.frameRate > 0 ? segment.clip.frameRate : 30f;

        public static float FrameRate(SkillConfigSO config, float fallback = 30f)
        {
            if (config != null)
            {
                if (config.UsesExplicitTiming) return config.timelineFrameRate;
                foreach (var segment in config.GetEffectiveSegments())
                    if (segment?.clip != null && segment.clip.frameRate > 0) return segment.clip.frameRate;
            }
            return Mathf.Max(1, fallback);
        }

        public static float SourceStart(Global.AnimClipSegment segment) => segment?.clip == null ? 0 : Mathf.Clamp(segment.clipStartFrame / SourceRate(segment), 0, segment.clip.length);
        public static float SourceEnd(Global.AnimClipSegment segment) => segment?.clip == null ? 0 : segment.clipEndFrame > 0 ? Mathf.Clamp(segment.clipEndFrame / SourceRate(segment), SourceStart(segment), segment.clip.length) : segment.clip.length;
        public static float SourceTime(Global.AnimClipSegment segment, float timelineFrame, float timelineRate) =>
            Mathf.Clamp(SourceStart(segment) + Mathf.Max(0, timelineFrame - segment.startFrame) / Mathf.Max(1, timelineRate), SourceStart(segment), SourceEnd(segment));

        public static int Duration(SkillConfigSO config, Global.AnimClipSegment segment) => segment == null ? 0 :
            config != null && config.UsesExplicitTiming ? Mathf.Max(0, Mathf.CeilToInt((SourceEnd(segment) - SourceStart(segment)) * FrameRate(config) - .00001f)) : segment.Duration;
        public static int EndFrame(SkillConfigSO config, Global.AnimClipSegment segment) => segment == null ? 0 : segment.startFrame + Duration(config, segment);
        public static float BlendDuration(SkillConfigSO config, Global.AnimClipSegment segment) => segment.blendInFrames / (config != null && config.UsesExplicitTiming ? FrameRate(config) : SourceRate(segment));

        public static int AnimationEnd(SkillConfigSO config)
        {
            int end = 0;
            foreach (var segment in config.GetEffectiveSegments()) end = Mathf.Max(end, EndFrame(config, segment));
            return end;
        }

        static int Max<T>(int end, List<T> list, System.Func<T, int> getEnd) where T : class
        {
            if (list != null) foreach (var item in list) if (item != null) end = Mathf.Max(end, getEnd(item));
            return end;
        }

        public static int ContentEnd(SkillConfigSO c)
        {
            int end = AnimationEnd(c);
            end = Max(end, c.attackList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.phase2AttackList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.cameraCues, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.fxList, x => x.keyNumber);
            end = Max(end, c.phase2FxList, x => x.keyNumber);
            end = Max(end, c.jumpList, x => x.endKey);
            end = Max(end, c.cancelList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.projectileList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.warningCueList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.superArmorList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.adjustMotionList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.trailToggleList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            end = Max(end, c.moveSegmentList, x => Mathf.Max(x.keyNumber, x.endKeyNumber));
            return Max(end, c.interactionWindows, x => x.endKeyNumber);
        }
    }
}
