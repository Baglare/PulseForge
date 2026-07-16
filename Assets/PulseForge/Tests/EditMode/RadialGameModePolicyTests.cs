using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialGameModePolicyTests
    {
        [Test]
        public void StandardMissDoesNotEndRun()
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.Standard);

            RadialRunObservation observation = policy.Observe(
                RadialEventType.Tap,
                1f,
                Miss("tap", "input"));
            policy.Complete(12d);

            Assert.That(observation.FailureObserved, Is.True);
            Assert.That(observation.DamageApplied, Is.False);
            Assert.That(policy.Outcome, Is.EqualTo(RadialRunOutcome.Clear));
        }

        [TestCase(0f, 10)]
        [TestCase(0.5f, 18)]
        [TestCase(1f, 25)]
        public void SurvivalDamageUsesIntensityFormula(float intensity, int expected)
        {
            Assert.That(RadialGameModePolicy.CalculateDamage(intensity), Is.EqualTo(expected));
        }

        [Test]
        public void SurvivalFailsWhenHealthReachesZero()
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.Survival);
            for (int i = 0; i < 4; i++)
            {
                policy.Observe(RadialEventType.Tap, 1f, Miss("tap-" + i, "input"));
            }

            Assert.That(policy.CurrentHealth, Is.Zero);
            Assert.That(policy.State, Is.EqualTo(RadialRunState.Failed));
            Assert.That(policy.Outcome, Is.EqualTo(RadialRunOutcome.Failed));
        }

        [Test]
        public void OneLifeFailsOnFirstWrongInput()
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.OneLife);

            RadialRunObservation result = policy.Observe(
                RadialEventType.Choice,
                0.3f,
                Miss("choice", "choice-input", RadialResultReason.WrongInput));

            Assert.That(result.CausedFailure, Is.True);
            Assert.That(policy.FailureReason, Is.EqualTo(RadialRunFailureReason.WrongInput));
        }

        [Test]
        public void EncounterLevelFailureCannotApplyDuplicateDamage()
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.Survival);

            policy.Observe(RadialEventType.HeavyChargeRelease, 0f, Miss("heavy", "press"));
            RadialRunObservation duplicate = policy.Observe(
                RadialEventType.HeavyChargeRelease,
                0f,
                Miss("heavy", "release"));

            Assert.That(policy.CurrentHealth, Is.EqualTo(90));
            Assert.That(duplicate.Duplicate, Is.True);
        }

        [TestCase(RadialEventType.TimedChain)]
        [TestCase(RadialEventType.OrderedSequence)]
        [TestCase(RadialEventType.SwarmChain)]
        public void CompoundCuesCanEachApplyDamage(RadialEventType eventType)
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.Survival);

            policy.Observe(eventType, 0f, Miss("group", "cue-1"));
            policy.Observe(eventType, 0f, Miss("group", "cue-2"));

            Assert.That(policy.CurrentHealth, Is.EqualTo(80));
        }

        [Test]
        public void ResetClearsFailureHealthAndDeduplication()
        {
            RadialGameModePolicy policy = new RadialGameModePolicy(RadialGameMode.OneLife);
            policy.Observe(RadialEventType.Tap, 1f, Miss("tap", "input"));

            policy.Reset(RadialGameMode.Survival);
            RadialRunObservation result = policy.Observe(
                RadialEventType.Tap,
                0f,
                Miss("tap", "input"));

            Assert.That(policy.State, Is.EqualTo(RadialRunState.Active));
            Assert.That(policy.CurrentHealth, Is.EqualTo(90));
            Assert.That(result.Duplicate, Is.False);
        }

        [Test]
        public void ActiveAbortIsNotTerminalButFailureIsTerminal()
        {
            RadialGameModePolicy active = new RadialGameModePolicy(RadialGameMode.Survival);
            RadialGameModePolicy failed = new RadialGameModePolicy(RadialGameMode.OneLife);
            failed.Observe(RadialEventType.Tap, 0f, Miss("tap", "input"));

            Assert.That(active.IsTerminal, Is.False);
            Assert.That(failed.IsTerminal, Is.True);
        }

        private static RequirementResult Miss(
            string encounterId,
            string requirementId,
            RadialResultReason reason = RadialResultReason.Timing)
        {
            return new RequirementResult(
                encounterId,
                requirementId,
                RhythmAction.LightAttack,
                RhythmInputPhase.Pressed,
                HitGrade.Miss,
                reason,
                2d,
                0.2d);
        }
    }
}
