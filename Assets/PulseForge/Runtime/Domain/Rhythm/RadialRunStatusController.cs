using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public readonly struct RadialStatusEffectSnapshot
    {
        public RadialStatusEffectSnapshot(
            bool isFogActive,
            double startedAtSongTimeSeconds,
            double endsAtSongTimeSeconds,
            float revealLeadMultiplier,
            double minimumVisibleLeadSeconds)
        {
            IsFogActive = isFogActive;
            StartedAtSongTimeSeconds = startedAtSongTimeSeconds;
            EndsAtSongTimeSeconds = endsAtSongTimeSeconds;
            RevealLeadMultiplier = revealLeadMultiplier;
            MinimumVisibleLeadSeconds = minimumVisibleLeadSeconds;
        }

        public bool IsFogActive { get; }
        public double StartedAtSongTimeSeconds { get; }
        public double EndsAtSongTimeSeconds { get; }
        public float RevealLeadMultiplier { get; }
        public double MinimumVisibleLeadSeconds { get; }

        public double RemainingSeconds(double songTimeSeconds)
        {
            return IsFogActive
                ? Math.Max(0d, EndsAtSongTimeSeconds - songTimeSeconds)
                : 0d;
        }
    }

    public sealed class RadialRunStatusController
    {
        private readonly HashSet<string> appliedEncounterIds =
            new HashSet<string>(StringComparer.Ordinal);

        private bool fogActive;
        private double fogStartedAtSongTimeSeconds;
        private double fogEndsAtSongTimeSeconds;
        private float fogRevealLeadMultiplier = 1f;
        private double fogMinimumVisibleLeadSeconds;

        public bool TryApplyFailure(
            RadialEncounterEventData encounter,
            RequirementResult result,
            RadialRunState runState,
            double songTimeSeconds)
        {
            if (encounter == null
                || result == null
                || result.Grade != HitGrade.Miss
                || runState != RadialRunState.Active)
            {
                return false;
            }

            return TryApply(encounter.eventId, encounter.failureEffect, songTimeSeconds);
        }

        public bool TryApply(
            string encounterId,
            FailureEffectData effect,
            double songTimeSeconds)
        {
            if (string.IsNullOrWhiteSpace(encounterId)
                || effect == null
                || effect.effectType != FailureEffectType.Fog
                || !IsFinitePositive(effect.durationSeconds)
                || !IsFinite(songTimeSeconds)
                || !appliedEncounterIds.Add(encounterId))
            {
                return false;
            }

            Update(songTimeSeconds);
            float multiplier = IsFinite(effect.revealLeadMultiplier)
                ? Math.Max(0f, Math.Min(1f, effect.revealLeadMultiplier))
                : 1f;
            double minimumLead = IsFinite(effect.minimumVisibleLeadSeconds)
                ? Math.Max(0d, effect.minimumVisibleLeadSeconds)
                : 0d;
            double newEnd = songTimeSeconds + effect.durationSeconds;
            if (!fogActive)
            {
                fogActive = true;
                fogStartedAtSongTimeSeconds = songTimeSeconds;
                fogEndsAtSongTimeSeconds = newEnd;
                fogRevealLeadMultiplier = multiplier;
                fogMinimumVisibleLeadSeconds = minimumLead;
            }
            else
            {
                fogEndsAtSongTimeSeconds = Math.Max(fogEndsAtSongTimeSeconds, newEnd);
                fogRevealLeadMultiplier = Math.Min(fogRevealLeadMultiplier, multiplier);
                fogMinimumVisibleLeadSeconds = Math.Max(
                    fogMinimumVisibleLeadSeconds,
                    minimumLead);
            }
            return true;
        }

        public void Update(double songTimeSeconds)
        {
            if (fogActive
                && IsFinite(songTimeSeconds)
                && songTimeSeconds >= fogEndsAtSongTimeSeconds)
            {
                ClearActiveEffects();
            }
        }

        public RadialStatusEffectSnapshot GetSnapshot(double songTimeSeconds)
        {
            Update(songTimeSeconds);
            return new RadialStatusEffectSnapshot(
                fogActive,
                fogStartedAtSongTimeSeconds,
                fogEndsAtSongTimeSeconds,
                fogRevealLeadMultiplier,
                fogMinimumVisibleLeadSeconds);
        }

        public void ClearActiveEffects()
        {
            fogActive = false;
            fogStartedAtSongTimeSeconds = 0d;
            fogEndsAtSongTimeSeconds = 0d;
            fogRevealLeadMultiplier = 1f;
            fogMinimumVisibleLeadSeconds = 0d;
        }

        public void Reset()
        {
            ClearActiveEffects();
            appliedEncounterIds.Clear();
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
