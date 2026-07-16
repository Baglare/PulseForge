using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public enum RadialPresentationResultState
    {
        Pending,
        Perfect,
        Good,
        Miss,
        WrongInput,
        Resolved
    }

    public enum RadialTimingWindowState
    {
        Waiting,
        Good,
        Perfect,
        Late
    }

    public enum RadialCuePriority
    {
        Forecast,
        Upcoming,
        Focused
    }

    public readonly struct RadialTimingWindowVisual
    {
        public RadialTimingWindowVisual(
            float position01,
            float perfectWidth01,
            RadialTimingWindowState state)
            : this(position01, perfectWidth01, 0.5f, state)
        {
        }

        public RadialTimingWindowVisual(
            float position01,
            float perfectWidth01,
            float perfectCenter01,
            RadialTimingWindowState state)
        {
            Position01 = position01;
            PerfectWidth01 = perfectWidth01;
            PerfectCenter01 = perfectCenter01;
            State = state;
        }

        public float Position01 { get; }
        public float PerfectWidth01 { get; }
        public float PerfectCenter01 { get; }
        public RadialTimingWindowState State { get; }
    }

    public readonly struct RadialBeatPulseVisual
    {
        public RadialBeatPulseVisual(float strength, bool isSubdivision)
        {
            Strength = Mathf.Clamp01(strength);
            IsSubdivision = isSubdivision;
        }

        public float Strength { get; }
        public bool IsSubdivision { get; }
        public float Scale => 1f + (Strength * (IsSubdivision ? 0.035f : 0.075f));
        public bool IsActive => Strength > 0f;
    }

    public readonly struct RadialPresentationKey : IEquatable<RadialPresentationKey>
    {
        public RadialPresentationKey(string eventId, string targetId, string requirementId)
        {
            EventId = eventId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            RequirementId = requirementId ?? string.Empty;
        }

        public string EventId { get; }
        public string TargetId { get; }
        public string RequirementId { get; }

        public bool Equals(RadialPresentationKey other)
        {
            return string.Equals(EventId, other.EventId, StringComparison.Ordinal)
                && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal)
                && string.Equals(RequirementId, other.RequirementId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RadialPresentationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(EventId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(RequirementId);
                return hash;
            }
        }
    }

    public readonly struct RadialRangedTimeline
    {
        public RadialRangedTimeline(
            double revealTimeSeconds,
            double spawnTimeSeconds,
            double fireTimeSeconds,
            double targetTimeSeconds)
        {
            RevealTimeSeconds = revealTimeSeconds;
            SpawnTimeSeconds = spawnTimeSeconds;
            FireTimeSeconds = fireTimeSeconds;
            TargetTimeSeconds = targetTimeSeconds;
        }

        public double RevealTimeSeconds { get; }
        public double SpawnTimeSeconds { get; }
        public double FireTimeSeconds { get; }
        public double TargetTimeSeconds { get; }
    }

    public static class RadialPresentationMath
    {
        private const float Diagonal = 0.70710678118f;

        public static RadialBeatPulseVisual EvaluateBeatPulse(
            BeatGridData beatGrid,
            double songTimeSeconds)
        {
            if (beatGrid == null)
            {
                return default;
            }

            double beatDistance = FindNearestDistance(beatGrid.beatTimesSeconds, songTimeSeconds);
            if (beatDistance <= 0.16d)
            {
                return new RadialBeatPulseVisual(
                    (float)(1d - (beatDistance / 0.16d)),
                    false);
            }

            double subdivisionDistance = FindNearestDistance(
                beatGrid.subdivisionTimesSeconds,
                songTimeSeconds);
            return subdivisionDistance <= 0.11d
                ? new RadialBeatPulseVisual(
                    (float)((1d - (subdivisionDistance / 0.11d)) * 0.62d),
                    true)
                : default;
        }

        private static double FindNearestDistance(
            IReadOnlyList<double> times,
            double songTimeSeconds)
        {
            if (times == null || times.Count == 0)
            {
                return double.MaxValue;
            }

            int low = 0;
            int high = times.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                if (times[middle] < songTimeSeconds)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            double nearest = double.MaxValue;
            if (low < times.Count)
            {
                nearest = Math.Abs(times[low] - songTimeSeconds);
            }
            if (high >= 0)
            {
                nearest = Math.Min(nearest, Math.Abs(times[high] - songTimeSeconds));
            }
            return nearest;
        }

        public static Vector2 DirectionVector(RadialDirection direction)
        {
            switch (direction)
            {
                case RadialDirection.North:
                    return new Vector2(0f, 1f);
                case RadialDirection.NorthEast:
                    return new Vector2(Diagonal, Diagonal);
                case RadialDirection.East:
                    return new Vector2(1f, 0f);
                case RadialDirection.SouthEast:
                    return new Vector2(Diagonal, -Diagonal);
                case RadialDirection.South:
                    return new Vector2(0f, -1f);
                case RadialDirection.SouthWest:
                    return new Vector2(-Diagonal, -Diagonal);
                case RadialDirection.West:
                    return new Vector2(-1f, 0f);
                case RadialDirection.NorthWest:
                    return new Vector2(-Diagonal, Diagonal);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static Vector2 EvaluateApproachPosition(
            RadialDirection direction,
            double songTimeSeconds,
            double revealTimeSeconds,
            double targetTimeSeconds,
            float outerRadius,
            float judgementRadius)
        {
            float progress = EvaluateProgress(songTimeSeconds, revealTimeSeconds, targetTimeSeconds);
            float radius = Mathf.Lerp(outerRadius, judgementRadius, progress);
            return DirectionVector(direction) * radius;
        }

        public static Vector2 EvaluateProjectilePosition(
            RadialDirection direction,
            double songTimeSeconds,
            double fireTimeSeconds,
            double targetTimeSeconds,
            float outerRadius,
            float judgementRadius)
        {
            return EvaluateApproachPosition(
                direction,
                songTimeSeconds,
                fireTimeSeconds,
                targetTimeSeconds,
                outerRadius,
                judgementRadius);
        }

        public static float EvaluateProgress(
            double songTimeSeconds,
            double startTimeSeconds,
            double endTimeSeconds)
        {
            if (endTimeSeconds <= startTimeSeconds)
            {
                return songTimeSeconds < endTimeSeconds ? 0f : 1f;
            }

            return Mathf.Clamp01((float)(
                (songTimeSeconds - startTimeSeconds)
                / (endTimeSeconds - startTimeSeconds)));
        }

        public static RadialTimingWindowVisual EvaluateTimingWindow(
            double songTimeSeconds,
            double targetTimeSeconds,
            double perfectWindowSeconds,
            double goodWindowSeconds)
        {
            double goodWindow = Math.Max(0.000001d, goodWindowSeconds);
            double perfectWindow = Math.Max(
                0d,
                Math.Min(perfectWindowSeconds, goodWindow));
            double timingError = songTimeSeconds - targetTimeSeconds;
            float position = Mathf.Clamp01((float)(
                (timingError + goodWindow) / (goodWindow * 2d)));
            RadialTimingWindowState state;
            if (timingError < -goodWindow)
            {
                state = RadialTimingWindowState.Waiting;
            }
            else if (timingError > goodWindow)
            {
                state = RadialTimingWindowState.Late;
            }
            else if (Math.Abs(timingError) <= perfectWindow)
            {
                state = RadialTimingWindowState.Perfect;
            }
            else
            {
                state = RadialTimingWindowState.Good;
            }

            return new RadialTimingWindowVisual(
                position,
                (float)(perfectWindow / goodWindow),
                state);
        }

        public static RadialTimingWindowVisual EvaluateDeadlineTimingWindow(
            double songTimeSeconds,
            double windowStartTimeSeconds,
            double perfectDeadlineSeconds,
            double goodDeadlineSeconds)
        {
            double goodDeadline = Math.Max(
                windowStartTimeSeconds + 0.000001d,
                goodDeadlineSeconds);
            double perfectDeadline = Math.Max(
                windowStartTimeSeconds,
                Math.Min(perfectDeadlineSeconds, goodDeadline));
            double duration = goodDeadline - windowStartTimeSeconds;
            float position = Mathf.Clamp01((float)(
                (songTimeSeconds - windowStartTimeSeconds) / duration));
            float perfectWidth = Mathf.Clamp01((float)(
                (perfectDeadline - windowStartTimeSeconds) / duration));
            RadialTimingWindowState state = songTimeSeconds < windowStartTimeSeconds
                ? RadialTimingWindowState.Waiting
                : songTimeSeconds <= perfectDeadline
                    ? RadialTimingWindowState.Perfect
                    : songTimeSeconds <= goodDeadline
                        ? RadialTimingWindowState.Good
                        : RadialTimingWindowState.Late;
            return new RadialTimingWindowVisual(
                position,
                perfectWidth,
                perfectWidth * 0.5f,
                state);
        }

        public static double EvaluateForecastLeadSeconds(
            int difficultyLevel,
            RadialEventType eventType,
            float leadMultiplier)
        {
            double baseLead;
            switch (difficultyLevel)
            {
                case 0:
                    baseLead = 4.5d;
                    break;
                case 2:
                    baseLead = 2.9d;
                    break;
                default:
                    baseLead = 3.7d;
                    break;
            }

            double eventBonus;
            switch (eventType)
            {
                case RadialEventType.GuardHold:
                    eventBonus = 0.4d;
                    break;
                case RadialEventType.HeavyChargeRelease:
                case RadialEventType.OrderedSequence:
                case RadialEventType.TimedChain:
                case RadialEventType.SwarmChain:
                    eventBonus = 0.6d;
                    break;
                case RadialEventType.Chord:
                case RadialEventType.Choice:
                    eventBonus = 0.5d;
                    break;
                case RadialEventType.BreakTarget:
                    eventBonus = 0.8d;
                    break;
                default:
                    eventBonus = 0d;
                    break;
            }

            return (baseLead + eventBonus) * Math.Max(1d, leadMultiplier);
        }

        public static double EvaluateForecastRevealTime(
            double targetTimeSeconds,
            int difficultyLevel,
            RadialEventType eventType,
            float leadMultiplier,
            RadialStatusEffectSnapshot status)
        {
            double lead = EvaluateForecastLeadSeconds(
                difficultyLevel,
                eventType,
                leadMultiplier);
            return targetTimeSeconds - EvaluateRevealLead(lead, status);
        }

        public static double EvaluateActionLayerStart(
            double targetTimeSeconds,
            double telegraphLeadSeconds)
        {
            return targetTimeSeconds - Math.Max(0d, telegraphLeadSeconds);
        }

        public static int FocusedCueLimit(int difficultyLevel)
        {
            switch (difficultyLevel)
            {
                case 0:
                    return 1;
                case 2:
                    return 3;
                default:
                    return 2;
            }
        }

        public static float EvaluateForecastFade(
            double songTimeSeconds,
            double actionLayerStartSeconds,
            double fadeDurationSeconds)
        {
            return 1f - EvaluateProgress(
                songTimeSeconds,
                actionLayerStartSeconds,
                actionLayerStartSeconds + Math.Max(0.000001d, fadeDurationSeconds));
        }

        public static double EvaluateRevealLead(
            double baseTelegraphLeadSeconds,
            RadialStatusEffectSnapshot status)
        {
            double baseLead = Math.Max(0d, baseTelegraphLeadSeconds);
            if (!status.IsFogActive)
            {
                return baseLead;
            }

            return Math.Max(
                Math.Max(0d, status.MinimumVisibleLeadSeconds),
                baseLead * Math.Max(0f, Math.Min(1f, status.RevealLeadMultiplier)));
        }

        public static double EvaluateRevealTime(
            double targetTimeSeconds,
            double baseTelegraphLeadSeconds,
            RadialStatusEffectSnapshot status)
        {
            return targetTimeSeconds - EvaluateRevealLead(
                baseTelegraphLeadSeconds,
                status);
        }

        public static bool ShouldBeVisible(
            bool wasPreviouslyVisible,
            double songTimeSeconds,
            double revealTimeSeconds)
        {
            return wasPreviouslyVisible || songTimeSeconds >= revealTimeSeconds;
        }

        public static RadialRangedTimeline CreateRangedTimeline(
            double targetTimeSeconds,
            double telegraphLeadSeconds)
        {
            double lead = Math.Max(0d, telegraphLeadSeconds);
            double reveal = targetTimeSeconds - lead;
            double spawn = Math.Min(
                targetTimeSeconds,
                reveal + Math.Min(0.24d, lead * 0.28d));
            double projectileTravel = Math.Max(0.22d, Math.Min(0.46d, lead * 0.38d));
            double fire = Math.Max(spawn, targetTimeSeconds - projectileTravel);
            return new RadialRangedTimeline(reveal, spawn, fire, targetTimeSeconds);
        }
    }
}
