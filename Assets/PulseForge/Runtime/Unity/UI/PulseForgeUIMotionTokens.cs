using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class PulseForgeUIMotionTokens
    {
        public const float FastDuration = 0.12f;
        public const float NormalDuration = 0.22f;
        public const float SlowDuration = 0.35f;
        public const float StaggerDelay = 0.028f;
        public const float ModalStartScale = 0.95f;
        public const float CountdownPopScale = 1.22f;
        public const float ButtonHoverScale = 1.02f;
        public const float ButtonPressedScale = 0.98f;

        public static readonly Vector2 PanelSlideOffset = new Vector2(0f, -20f);

        public static readonly AnimationCurve EaseOut = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.2f),
            new Keyframe(1f, 1f, 0f, 0f));

        public static readonly AnimationCurve EaseInOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public static readonly AnimationCurve Pop = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3.8f),
            new Keyframe(0.78f, 1.025f, 0.25f, 0.25f),
            new Keyframe(1f, 1f, 0f, 0f));
    }
}
