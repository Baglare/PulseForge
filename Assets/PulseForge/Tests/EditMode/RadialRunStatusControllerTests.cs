using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialRunStatusControllerTests
    {
        [Test]
        public void SuccessDoesNotApplyFogButMissAndWrongInputApplyOnlyOnce()
        {
            RadialRunStatusController controller = new RadialRunStatusController();
            RadialEncounterEventData encounter = Saboteur("saboteur");

            Assert.That(controller.TryApplyFailure(
                encounter,
                Result(HitGrade.Perfect, RadialResultReason.None),
                RadialRunState.Active,
                10d), Is.False);
            Assert.That(controller.TryApplyFailure(
                encounter,
                Result(HitGrade.Miss, RadialResultReason.WrongInput),
                RadialRunState.Active,
                10d), Is.True);
            Assert.That(controller.TryApplyFailure(
                encounter,
                Result(HitGrade.Miss, RadialResultReason.Timing),
                RadialRunState.Active,
                10.1d), Is.False);
        }

        [Test]
        public void FogRefreshExtendsDurationAndUsesStrongestMultiplierWithoutStacking()
        {
            RadialRunStatusController controller = new RadialRunStatusController();
            controller.TryApply("first", Fog(6d, 0.55f), 10d);
            controller.TryApply("second", Fog(9d, 0.70f), 12d);

            RadialStatusEffectSnapshot snapshot = controller.GetSnapshot(12d);

            Assert.That(snapshot.EndsAtSongTimeSeconds, Is.EqualTo(21d));
            Assert.That(snapshot.RevealLeadMultiplier, Is.EqualTo(0.55f));
            Assert.That(snapshot.RevealLeadMultiplier, Is.Not.EqualTo(0.385f));
        }

        [Test]
        public void SongTimePauseDoesNotAdvanceFogAndResetClearsIt()
        {
            RadialRunStatusController controller = new RadialRunStatusController();
            controller.TryApply("saboteur", Fog(7d, 0.55f), 4d);

            double before = controller.GetSnapshot(5d).RemainingSeconds(5d);
            double paused = controller.GetSnapshot(5d).RemainingSeconds(5d);
            controller.Reset();

            Assert.That(paused, Is.EqualTo(before));
            Assert.That(controller.GetSnapshot(5d).IsFogActive, Is.False);
        }

        [Test]
        public void TerminalRunStatePreventsFogStart()
        {
            RadialRunStatusController controller = new RadialRunStatusController();

            bool applied = controller.TryApplyFailure(
                Saboteur("terminal"),
                Result(HitGrade.Miss, RadialResultReason.Timing),
                RadialRunState.Failed,
                3d);

            Assert.That(applied, Is.False);
            Assert.That(controller.GetSnapshot(3d).IsFogActive, Is.False);
        }

        private static RadialEncounterEventData Saboteur(string id)
        {
            return new RadialEncounterEventData
            {
                eventId = id,
                eventType = RadialEventType.Tap,
                failureEffect = Fog(7d, 0.55f)
            };
        }

        private static FailureEffectData Fog(double duration, float multiplier)
        {
            return new FailureEffectData
            {
                effectType = FailureEffectType.Fog,
                durationSeconds = duration,
                revealLeadMultiplier = multiplier,
                minimumVisibleLeadSeconds = 0.45d
            };
        }

        private static RequirementResult Result(HitGrade grade, RadialResultReason reason)
        {
            return new RequirementResult(
                "saboteur",
                "light",
                RhythmAction.LightAttack,
                RhythmInputPhase.Pressed,
                grade,
                reason,
                10d,
                grade == HitGrade.Miss ? 0.2d : 0d);
        }
    }
}
