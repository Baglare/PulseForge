using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class PulseForgeGameplayFeedbackTokens
    {
        public const float PerfectDuration = 0.34f;
        public const float GoodDuration = 0.26f;
        public const float MissDuration = 0.24f;
        public const float HitZoneFlashDuration = 0.16f;
        public const float LanePulseDuration = 0.20f;
        public const float ComboPulseDuration = 0.16f;
        public const float NoteHitDuration = 0.18f;
        public const float NoteMissDuration = 0.22f;
        public const float PerfectPopScale = 1.25f;
        public const float GoodPopScale = 1.12f;
        public const float MissPopScale = 1.08f;
        public const float ComboPulseScale = 1.10f;
        public const float ComboBreakScale = 0.96f;
        public const float NoteHitScale = 0.68f;
        public const float PerfectFlashAlpha = 1f;
        public const float GoodFlashAlpha = 0.68f;
        public const float MissFlashAlpha = 0.80f;
        public const float LanePulseAlpha = 0.30f;
        public const float CombatEffectMultiplier = 1.16f;

        public static readonly Vector2 MissFeedbackOffset = new Vector2(14f, 0f);
        public static readonly Vector2 NoteMissOffset = new Vector2(18f, -12f);
    }
}
