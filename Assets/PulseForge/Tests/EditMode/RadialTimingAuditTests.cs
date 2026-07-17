using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialTimingAuditTests
    {
        [TestCase(TimingAssistMode.Standard, 0.045d, 0.100d)]
        [TestCase(TimingAssistMode.Relaxed, 0.065d, 0.140d)]
        [TestCase(TimingAssistMode.Practice, 0.090d, 0.200d)]
        public void SnapshotAndSessionUseIdenticalTimingAssistBoundaries(
            TimingAssistMode mode,
            double perfectWindow,
            double goodWindow)
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("tap", RhythmAction.Guard, 2d) },
                mode);

            Assert.That(session.TryGetTimingSnapshot(
                "tap", "tap-input", 2d + perfectWindow, 0d, 0d, 1, out RadialTimingSnapshot perfect), Is.True);
            Assert.That(perfect.PerfectWindowSeconds, Is.EqualTo(perfectWindow));
            Assert.That(perfect.GoodWindowSeconds, Is.EqualTo(goodWindow));
            Assert.That(session.Press(RhythmAction.Guard, 2d + perfectWindow).RequirementResults.Single().Grade,
                Is.EqualTo(HitGrade.Perfect));

            session.Reset();
            Assert.That(session.Press(RhythmAction.Guard, 2d + goodWindow).RequirementResults.Single().Grade,
                Is.EqualTo(HitGrade.Good));
        }

        [Test]
        public void PerfectOpportunityAcceptsTheSameDisplayedActionPress()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("displayed", RhythmAction.LightAttack, 2d) });

            Assert.That(session.TryGetInputOpportunitySnapshot(
                "displayed",
                "displayed-input",
                RhythmAction.LightAttack,
                RhythmInputPhase.Pressed,
                2d,
                0d,
                0d,
                1,
                out InputOpportunitySnapshot opportunity), Is.True);
            Assert.That(opportunity.Matchable, Is.True);
            Assert.That(opportunity.RejectionReason, Is.EqualTo(InputOpportunityRejectionReason.None));
            Assert.That(MathfAbs(opportunity.DeltaMilliseconds), Is.LessThanOrEqualTo(0.0001d));

            RadialInputResolveResult result = session.Press(
                RhythmAction.LightAttack, 2d, 1L, 0d, 0d, 1);

            Assert.That(result.Consumed, Is.True);
            Assert.That(result.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void PracticePlusFiftyMillisecondsIsMatchableAndAccepted()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("practice", RhythmAction.Guard, 2d) },
                TimingAssistMode.Practice);

            session.TryGetInputOpportunitySnapshot(
                "practice", "practice-input", RhythmAction.Guard, RhythmInputPhase.Pressed,
                2.05d, 0d, 0d, 1, out InputOpportunitySnapshot opportunity);
            RadialInputResolveResult result = session.Press(
                RhythmAction.Guard, 2.05d, 1L, 0d, 0d, 1);

            Assert.That(opportunity.Matchable, Is.True);
            Assert.That(result.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void InputOffsetIsAppliedExactlyOnceToJudgementAndSnapshot()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("offset", RhythmAction.LightAttack, 2d) });

            RadialInputResolveResult result = session.Press(
                RhythmAction.LightAttack, 1.95d, 1L, 0.05d, 0d, 1);

            Assert.That(result.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(session.LastInputAudit.Timing.RawSongTimeSeconds, Is.EqualTo(1.95d));
            Assert.That(session.LastInputAudit.Timing.EffectiveJudgementTimeSeconds, Is.EqualTo(2d).Within(0.0000001d));
            Assert.That(session.LastInputAudit.Timing.DeltaMilliseconds, Is.EqualTo(0d).Within(0.0001d));
        }

        [Test]
        public void BeatMapOffsetIsAppliedExactlyOnceAndReported()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("beatmap-offset", RhythmAction.Dodge, 2d) },
                TimingAssistMode.Standard,
                0.2d);

            RadialInputResolveResult result = session.Press(
                RhythmAction.Dodge, 2.2d, 1L, 0d, 0.2d, 1);

            Assert.That(result.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(session.LastInputAudit.Timing.TargetTimeSeconds, Is.EqualTo(2.2d).Within(0.0000001d));
            Assert.That(session.LastInputAudit.Timing.BeatMapOffsetSeconds, Is.EqualTo(0.2d));
        }

        [Test]
        public void FocusedRequirementMatchesDeterministicallySelectedRequirement()
        {
            RadialRhythmSession session = new RadialRhythmSession(new[]
            {
                Tap("earlier", RhythmAction.Guard, 1d),
                Tap("nearer", RhythmAction.Guard, 1.06d)
            });

            session.Press(RhythmAction.Guard, 1.05d, 1L, 0d, 0d, 1);

            Assert.That(session.LastInputAudit.Timing.EventId, Is.EqualTo("nearer"));
            Assert.That(session.LastInputAudit.FocusedEventId, Is.EqualTo("nearer"));
            Assert.That(session.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.AcceptedPerfect));
        }

        [Test]
        public void InputAtGoodBoundaryResolvesBeforeSameFrameTimeoutUpdate()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("same-frame", RhythmAction.Guard, 1d) });

            RadialInputResolveResult input = session.Press(
                RhythmAction.Guard, 1.1d, 1L, 0d, 0d, 1);
            var timeoutResults = session.Update(1.1d);

            Assert.That(input.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Good));
            Assert.That(timeoutResults, Is.Empty);
        }

        [Test]
        public void CompoundAndFutureSequenceStepsDoNotOwnAnotherRequirementsInput()
        {
            RadialEncounterEventData chord = Encounter("chord", RadialEventType.Chord);
            InputRequirementData chordGuard = Requirement(
                "chord-guard", RhythmAction.Guard, RhythmInputPhase.Pressed, 1d, 0);
            chordGuard.gestureType = InputGestureType.Chord;
            InputRequirementData chordLight = Requirement(
                "chord-light", RhythmAction.LightAttack, RhythmInputPhase.Pressed, 1d, 0);
            chordLight.gestureType = InputGestureType.Chord;
            chord.requirements.Add(chordGuard);
            chord.requirements.Add(chordLight);
            RadialRhythmSession chordSession = new RadialRhythmSession(new[] { chord });
            chordSession.Press(RhythmAction.Guard, 1d, 1L, 0d, 0d, 1);
            chordSession.Press(RhythmAction.LightAttack, 1d, 2L, 0d, 0d, 1);
            Assert.That(chordSession.LastInputAudit.Timing.RequirementId, Is.EqualTo("chord-light"));

            RadialEncounterEventData sequence = Encounter("sequence-routing", RadialEventType.OrderedSequence);
            sequence.requirements.Add(Requirement(
                "current", RhythmAction.Guard, RhythmInputPhase.Pressed, 1d, 0));
            sequence.requirements.Add(Requirement(
                "future", RhythmAction.LightAttack, RhythmInputPhase.Pressed, 1d, 1));
            RadialRhythmSession sequenceSession = new RadialRhythmSession(new[] { sequence });
            sequenceSession.TryGetInputOpportunitySnapshot(
                "sequence-routing", "future", RhythmAction.LightAttack, RhythmInputPhase.Pressed,
                1d, 0d, 0d, 1, out InputOpportunitySnapshot future);
            sequenceSession.Press(RhythmAction.LightAttack, 1d, 1L, 0d, 0d, 1);

            Assert.That(future.Matchable, Is.False);
            Assert.That(future.RejectionReason, Is.EqualTo(InputOpportunityRejectionReason.NotCurrentStep));
            Assert.That(sequenceSession.Encounters[0].Requirements[1].IsResolved, Is.False);
        }

        [Test]
        public void EqualDistanceSelectionUsesStableEventAndRequirementIds()
        {
            RadialRhythmSession session = new RadialRhythmSession(new[]
            {
                Tap("z-earlier-target", RhythmAction.Guard, 0.95d),
                Tap("a-later-target", RhythmAction.Guard, 1.05d)
            });

            session.Press(RhythmAction.Guard, 1d, 1L, 0d, 0d, 1);

            Assert.That(session.LastInputAudit.Timing.EventId, Is.EqualTo("a-later-target"));
        }

        [Test]
        public void RejectionReasonsDistinguishWindowPhaseFutureDuplicateAndResolved()
        {
            RadialRhythmSession outside = new RadialRhythmSession(
                new[] { Tap("outside", RhythmAction.Guard, 2d) });
            outside.Press(RhythmAction.Guard, 1.5d, 1L, 0d, 0d, 1);
            Assert.That(outside.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.NoActiveRequirement));
            Assert.That(outside.LastInputAudit.Diagnostics.PendingRequirementCount, Is.EqualTo(1));
            Assert.That(outside.LastInputAudit.Diagnostics.WindowCandidateCount, Is.EqualTo(0));
            Assert.That(outside.LastInputAudit.Diagnostics.NearestRequirementId, Is.EqualTo("outside-input"));
            Assert.That(outside.LastInputAudit.Diagnostics.NearestRejectionReason,
                Is.EqualTo(InputOpportunityRejectionReason.OutsideWindow));

            RadialRhythmSession wrongPhase = new RadialRhythmSession(
                new[] { Tap("phase", RhythmAction.Guard, 2d) });
            wrongPhase.Release(RhythmAction.Guard, 2d, 1L, 0d, 0d, 1);
            Assert.That(wrongPhase.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.NoActiveRequirement));
            Assert.That(wrongPhase.LastInputAudit.Diagnostics.NearestRejectionReason,
                Is.EqualTo(InputOpportunityRejectionReason.WrongPhase));

            RadialEncounterEventData sequence = Encounter("sequence", RadialEventType.OrderedSequence);
            sequence.requirements.Add(Requirement("first", RhythmAction.Guard, RhythmInputPhase.Pressed, 1d, 0));
            sequence.requirements.Add(Requirement("future", RhythmAction.LightAttack, RhythmInputPhase.Pressed, 1.5d, 1));
            RadialRhythmSession future = new RadialRhythmSession(new[] { sequence });
            future.Press(RhythmAction.LightAttack, 1.5d, 1L, 0d, 0d, 1);
            Assert.That(future.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.NoActiveRequirement));
            Assert.That(future.LastInputAudit.Diagnostics.NearestRejectionReason,
                Is.EqualTo(InputOpportunityRejectionReason.NotCurrentStep));

            RadialRhythmSession duplicate = new RadialRhythmSession(
                new[] { Tap("duplicate", RhythmAction.Dodge, 1d) });
            duplicate.Press(RhythmAction.Dodge, 1d, 7L, 0d, 0d, 1);
            duplicate.Press(RhythmAction.Dodge, 1d, 7L, 0d, 0d, 1);
            Assert.That(duplicate.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.DuplicateInput));

            duplicate.Press(RhythmAction.Dodge, 1d, 8L, 0d, 0d, 1);
            Assert.That(duplicate.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.NoActiveRequirement));
            Assert.That(duplicate.LastInputAudit.Diagnostics.NearestRejectionReason,
                Is.EqualTo(InputOpportunityRejectionReason.AlreadyResolved));
        }

        [Test]
        public void TimedOutRequirementProducesDetailedNoActiveReason()
        {
            RadialRhythmSession session = new RadialRhythmSession(
                new[] { Tap("timed-out", RhythmAction.Guard, 1d) });
            session.Update(1.2d);

            session.Press(RhythmAction.Guard, 1.2d, 1L, 0d, 0d, 1);

            Assert.That(session.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.NoActiveRequirement));
            Assert.That(session.LastInputAudit.Diagnostics.NearestRequirementState,
                Is.EqualTo(RadialRequirementState.Resolved));
            Assert.That(session.LastInputAudit.Diagnostics.NearestRejectionReason,
                Is.EqualTo(InputOpportunityRejectionReason.TimedOut));
        }

        [Test]
        public void HoldAndHeavyReleaseAuditUseReleaseTargetAndPhase()
        {
            RadialBeatMapData fixture = RadialTimingFixture.Create();
            RadialEncounterEventData hold = fixture.encounters.Single(item => item.eventType == RadialEventType.GuardHold);
            RadialRhythmSession holdSession = new RadialRhythmSession(new[] { hold });
            holdSession.Press(RhythmAction.Guard, 5d, 1L, 0d, 0d, 1);
            holdSession.Release(RhythmAction.Guard, 6d, 2L, 0d, 0d, 1);
            Assert.That(holdSession.LastInputAudit.Timing.Phase, Is.EqualTo(RhythmInputPhase.Released));
            Assert.That(holdSession.LastInputAudit.Timing.TargetTimeSeconds, Is.EqualTo(6d));

            RadialEncounterEventData heavy = fixture.encounters.Single(item => item.eventType == RadialEventType.HeavyChargeRelease);
            RadialRhythmSession heavySession = new RadialRhythmSession(new[] { heavy });
            heavySession.Press(RhythmAction.HeavyAttack, 7d, 1L, 0d, 0d, 1);
            heavySession.Release(RhythmAction.HeavyAttack, 7.45d, 2L, 0d, 0d, 1);
            Assert.That(heavySession.LastInputAudit.Timing.RequirementId, Is.EqualTo("fixture-heavy-release"));
            Assert.That(heavySession.LastInputAudit.Timing.Phase, Is.EqualTo(RhythmInputPhase.Released));
            Assert.That(heavySession.LastInputAudit.Reason, Is.EqualTo(RadialInputAuditReason.AcceptedPerfect));
        }

        private static RadialEncounterEventData Tap(
            string eventId,
            RhythmAction action,
            double targetTimeSeconds)
        {
            RadialEncounterEventData encounter = Encounter(eventId, RadialEventType.Tap);
            encounter.telegraphLeadSeconds = 1d;
            encounter.requirements.Add(Requirement(
                eventId + "-input", action, RhythmInputPhase.Pressed, targetTimeSeconds, 0));
            return encounter;
        }

        private static RadialEncounterEventData Encounter(string id, RadialEventType type)
        {
            return new RadialEncounterEventData { eventId = id, eventType = type, telegraphLeadSeconds = 1d };
        }

        private static InputRequirementData Requirement(
            string id,
            RhythmAction action,
            RhythmInputPhase phase,
            double targetTimeSeconds,
            int order)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = RhythmActionMaskUtility.ToMask(action),
                phase = phase,
                targetTimeSeconds = targetTimeSeconds,
                orderIndex = order,
                exclusive = true
            };
        }

        private static double MathfAbs(double value)
        {
            return value < 0d ? -value : value;
        }
    }
}
