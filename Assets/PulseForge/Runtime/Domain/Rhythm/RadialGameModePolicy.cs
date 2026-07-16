using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public enum RadialGameMode
    {
        Standard,
        Survival,
        OneLife
    }

    public enum RadialRunState
    {
        Active,
        Cleared,
        Failed
    }

    public enum RadialRunOutcome
    {
        None,
        Clear,
        Failed
    }

    public enum RadialRunFailureReason
    {
        None,
        Miss,
        WrongInput
    }

    public readonly struct RadialRunObservation
    {
        public RadialRunObservation(
            bool failureObserved,
            bool duplicate,
            bool damageApplied,
            int damage,
            int remainingHealth,
            bool causedFailure)
        {
            FailureObserved = failureObserved;
            Duplicate = duplicate;
            DamageApplied = damageApplied;
            Damage = damage;
            RemainingHealth = remainingHealth;
            CausedFailure = causedFailure;
        }

        public bool FailureObserved { get; }
        public bool Duplicate { get; }
        public bool DamageApplied { get; }
        public int Damage { get; }
        public int RemainingHealth { get; }
        public bool CausedFailure { get; }
    }

    public sealed class RadialGameModePolicy
    {
        public const int MaximumHealth = 100;

        private readonly HashSet<string> observedFailureKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public RadialGameModePolicy(RadialGameMode mode = RadialGameMode.Standard)
        {
            Reset(mode);
        }

        public RadialGameMode Mode { get; private set; }
        public RadialRunState State { get; private set; }
        public RadialRunOutcome Outcome { get; private set; }
        public int CurrentHealth { get; private set; }
        public double FailureTimeSeconds { get; private set; }
        public RadialRunFailureReason FailureReason { get; private set; }
        public string FailureEventId { get; private set; }
        public string FailureRequirementId { get; private set; }
        public bool IsTerminal => State != RadialRunState.Active;

        public void Reset(RadialGameMode mode)
        {
            Mode = mode;
            State = RadialRunState.Active;
            Outcome = RadialRunOutcome.None;
            CurrentHealth = MaximumHealth;
            FailureTimeSeconds = 0d;
            FailureReason = RadialRunFailureReason.None;
            FailureEventId = string.Empty;
            FailureRequirementId = string.Empty;
            observedFailureKeys.Clear();
        }

        public RadialRunObservation Observe(
            RadialEventType eventType,
            float eventIntensity,
            RequirementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            if (State != RadialRunState.Active || !IsFailure(result))
            {
                return new RadialRunObservation(
                    false,
                    false,
                    false,
                    0,
                    CurrentHealth,
                    false);
            }

            string failureKey = BuildFailureKey(eventType, result);
            if (!observedFailureKeys.Add(failureKey))
            {
                return new RadialRunObservation(
                    true,
                    true,
                    false,
                    0,
                    CurrentHealth,
                    false);
            }

            RadialRunFailureReason reason = result.Reason == RadialResultReason.WrongInput
                ? RadialRunFailureReason.WrongInput
                : RadialRunFailureReason.Miss;
            if (Mode == RadialGameMode.Standard)
            {
                return new RadialRunObservation(
                    true,
                    false,
                    false,
                    0,
                    CurrentHealth,
                    false);
            }
            if (Mode == RadialGameMode.OneLife)
            {
                Fail(result, reason);
                return new RadialRunObservation(
                    true,
                    false,
                    false,
                    0,
                    CurrentHealth,
                    true);
            }

            int damage = CalculateDamage(eventIntensity);
            CurrentHealth = Math.Max(0, CurrentHealth - damage);
            bool causedFailure = CurrentHealth == 0;
            if (causedFailure)
            {
                Fail(result, reason);
            }
            return new RadialRunObservation(
                true,
                false,
                true,
                damage,
                CurrentHealth,
                causedFailure);
        }

        public void Complete(double songTimeSeconds)
        {
            if (State != RadialRunState.Active)
            {
                return;
            }
            if (double.IsNaN(songTimeSeconds) || double.IsInfinity(songTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(songTimeSeconds));
            }
            State = RadialRunState.Cleared;
            Outcome = RadialRunOutcome.Clear;
        }

        public static int CalculateDamage(float eventIntensity)
        {
            double safeIntensity = float.IsNaN(eventIntensity)
                || float.IsInfinity(eventIntensity)
                    ? 0d
                    : Math.Max(0d, Math.Min(1d, eventIntensity));
            int damage = (int)Math.Round(
                10d + (15d * safeIntensity),
                MidpointRounding.AwayFromZero);
            return Math.Max(10, Math.Min(25, damage));
        }

        private void Fail(RequirementResult result, RadialRunFailureReason reason)
        {
            State = RadialRunState.Failed;
            Outcome = RadialRunOutcome.Failed;
            FailureTimeSeconds = result.ResolutionTimeSeconds;
            FailureReason = reason;
            FailureEventId = result.EncounterId;
            FailureRequirementId = result.RequirementId;
        }

        private static bool IsFailure(RequirementResult result)
        {
            return result.Grade == HitGrade.Miss
                || result.Reason == RadialResultReason.WrongInput;
        }

        private static string BuildFailureKey(
            RadialEventType eventType,
            RequirementResult result)
        {
            bool perRequirement = eventType == RadialEventType.TimedChain
                || eventType == RadialEventType.OrderedSequence
                || eventType == RadialEventType.SwarmChain;
            return perRequirement
                ? result.EncounterId + "/" + result.RequirementId
                : result.EncounterId;
        }
    }
}
