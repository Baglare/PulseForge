using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialRhythmSessionTests
    {
        [TestCase(-0.045d, HitGrade.Perfect)]
        [TestCase(0.045d, HitGrade.Perfect)]
        [TestCase(-0.100d, HitGrade.Good)]
        [TestCase(0.100d, HitGrade.Good)]
        public void TapUsesInclusivePerfectAndGoodBoundaries(double offset, HitGrade expected)
        {
            RadialRhythmSession session = CreateSession(
                CreateTap("tap", RhythmAction.Guard, 2d));

            RadialInputResolveResult result = session.Press(RhythmAction.Guard, 2d + offset);

            Assert.That(result.Consumed, Is.True);
            Assert.That(result.RequirementResults.Single().Grade, Is.EqualTo(expected));
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(expected));
        }

        [Test]
        public void GuardHoldEarlyReleaseIsMissWhenOutsideConfiguredGrace()
        {
            RadialEncounterEventData hold = CreateEncounter("hold", RadialEventType.GuardHold);
            hold.requirements.Add(new InputRequirementData
            {
                requirementId = "hold-input",
                acceptedActions = RhythmActionMask.Guard,
                gestureType = InputGestureType.Hold,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = 1d,
                holdEndTimeSeconds = 1.5d,
                earlyReleaseGraceSeconds = 0.1d,
                allowEarlyReleaseAsGood = true,
                autoCompleteAtHoldEnd = true
            });
            RadialRhythmSession session = CreateSession(hold);

            session.Press(RhythmAction.Guard, 1d);
            RadialInputResolveResult release = session.Release(RhythmAction.Guard, 1.2d);

            Assert.That(release.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Miss));
            Assert.That(release.RequirementResults.Single().Reason, Is.EqualTo(RadialResultReason.EarlyRelease));
        }

        [Test]
        public void GuardHoldCanCompleteAutomaticallyWhileActionRemainsHeld()
        {
            RadialEncounterEventData hold = CreateEncounter("hold-auto", RadialEventType.GuardHold);
            hold.requirements.Add(new InputRequirementData
            {
                requirementId = "hold-input",
                acceptedActions = RhythmActionMask.Guard,
                gestureType = InputGestureType.Hold,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = 1d,
                holdEndTimeSeconds = 1.4d,
                autoCompleteAtHoldEnd = true
            });
            RadialRhythmSession session = CreateSession(hold);

            session.Press(RhythmAction.Guard, 1d);
            IReadOnlyList<RequirementResult> results = session.Update(1.4d);

            Assert.That(results.Single().Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void HeavyPressAndReleaseUseSeparateRequirementsAndWorstGrade()
        {
            RadialRhythmSession session = CreateSession(CreateHeavy("heavy"));

            RadialInputResolveResult press = session.Press(RhythmAction.HeavyAttack, 2d);
            RadialInputResolveResult release = session.Release(RhythmAction.HeavyAttack, 2.38d);

            Assert.That(press.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(release.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Good));
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Good));
        }

        [Test]
        public void HeavyReleaseOutsideConfiguredDurationIsMiss()
        {
            RadialRhythmSession session = CreateSession(CreateHeavy("heavy-short"));

            session.Press(RhythmAction.HeavyAttack, 2d);
            RadialInputResolveResult release = session.Release(RhythmAction.HeavyAttack, 2.1d);

            Assert.That(release.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Miss));
            Assert.That(release.RequirementResults.Single().Reason, Is.EqualTo(RadialResultReason.InvalidCharge));
        }

        [TestCase(0.045d, HitGrade.Perfect)]
        [TestCase(0.080d, HitGrade.Good)]
        public void ChordUsesConfiguredTwoActionSpread(double spread, HitGrade expected)
        {
            RadialEncounterEventData chord = CreateEncounter("chord", RadialEventType.Chord);
            chord.requirements.Add(CreateRequirement("guard", RhythmAction.Guard, 1d, InputGestureType.Chord));
            chord.requirements.Add(CreateRequirement("light", RhythmAction.LightAttack, 1d, InputGestureType.Chord));
            RadialRhythmSession session = CreateSession(chord);

            session.Press(RhythmAction.Guard, 1d);
            RadialInputResolveResult result = session.Press(RhythmAction.LightAttack, 1d + spread);

            Assert.That(result.RequirementResults, Has.Count.EqualTo(2));
            Assert.That(result.RequirementResults.All(item => item.Grade == expected), Is.True);
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(expected));
        }

        [Test]
        public void ChoiceIsOwnedByFirstValidAlternativeWithoutPenalizingOtherAlternative()
        {
            RadialEncounterEventData choice = CreateEncounter("choice", RadialEventType.Choice);
            InputRequirementData requirement = CreateRequirement(
                "choice-input",
                RhythmAction.Guard,
                1d,
                InputGestureType.Choice);
            requirement.acceptedActions = RhythmActionMask.Guard | RhythmActionMask.Dodge;
            choice.requirements.Add(requirement);
            RadialRhythmSession session = CreateSession(choice);

            RadialInputResolveResult first = session.Press(RhythmAction.Dodge, 1d);
            RadialInputResolveResult alternative = session.Press(RhythmAction.Guard, 1d, 2L);

            Assert.That(first.RequirementResults.Single().Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(alternative.Consumed, Is.False);
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void OrderedSequenceWrongInputMissesCurrentStepWithoutConsumingFutureStep()
        {
            RadialEncounterEventData sequence = CreateEncounter("sequence", RadialEventType.OrderedSequence);
            InputRequirementData first = CreateRequirement(
                "step-1", RhythmAction.Guard, 1d, InputGestureType.SequenceStep);
            first.orderIndex = 0;
            InputRequirementData second = CreateRequirement(
                "step-2", RhythmAction.LightAttack, 1.5d, InputGestureType.SequenceStep);
            second.orderIndex = 1;
            sequence.requirements.Add(first);
            sequence.requirements.Add(second);
            RadialRhythmSession session = CreateSession(sequence);

            RadialInputResolveResult wrong = session.Press(RhythmAction.LightAttack, 1d);

            Assert.That(wrong.RequirementResults.Single().RequirementId, Is.EqualTo("step-1"));
            Assert.That(wrong.RequirementResults.Single().Reason, Is.EqualTo(RadialResultReason.WrongInput));
            Assert.That(session.Encounters[0].Requirements[1].IsResolved, Is.False);

            session.Press(RhythmAction.LightAttack, 1.5d);
            Assert.That(session.Encounters[0].Requirements[1].Result.Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Miss));
        }

        [Test]
        public void SwarmChainKeepsIndependentRequirementAndTargetResults()
        {
            RadialEncounterEventData swarm = CreateEncounter("swarm", RadialEventType.SwarmChain);
            for (int i = 0; i < 3; i++)
            {
                string requirementId = "cue-" + i;
                InputRequirementData cue = CreateRequirement(
                    requirementId,
                    RhythmAction.LightAttack,
                    1d + (i * 0.2d),
                    InputGestureType.ChainStep);
                cue.orderIndex = i;
                swarm.requirements.Add(cue);
                swarm.targets.Add(new EncounterTargetData
                {
                    targetId = "swarm-target-" + i,
                    requirementId = requirementId,
                    direction = (RadialDirection)i,
                    archetype = EnemyArchetype.Swarm
                });
            }

            RadialRhythmSession session = CreateSession(swarm);
            session.Press(RhythmAction.LightAttack, 1d);
            session.Press(RhythmAction.LightAttack, 1.27d);
            session.Update(1.501d);

            HitGrade[] targetGrades = session.Encounters[0].Targets
                .Select(target => target.Result.Grade)
                .ToArray();
            Assert.That(targetGrades, Is.EqualTo(new[] { HitGrade.Perfect, HitGrade.Good, HitGrade.Miss }));
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Miss));
        }

        [Test]
        public void BreakTargetCompletesWithSixDebouncedLightPressesAndNoHeavy()
        {
            RadialEncounterEventData breakTarget = CreateEncounter("break", RadialEventType.BreakTarget);
            breakTarget.requirements.Add(new InputRequirementData
            {
                requirementId = "break-count",
                acceptedActions = RhythmActionMask.LightAttack,
                gestureType = InputGestureType.RepeatedPress,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = 1d,
                windowStartTimeSeconds = 1d,
                perfectDeadlineSeconds = 1.8d,
                goodDeadlineSeconds = 2d,
                requiredPressCount = 6,
                minimumPressIntervalSeconds = 0.05d
            });
            InputRequirementData optionalHeavy = CreateRequirement(
                "optional-heavy-finisher",
                RhythmAction.HeavyAttack,
                1.9d,
                InputGestureType.Tap);
            optionalHeavy.isOptional = true;
            breakTarget.requirements.Add(optionalHeavy);
            RadialRhythmSession session = CreateSession(breakTarget);

            session.Press(RhythmAction.LightAttack, 1d, 1L);
            session.Press(RhythmAction.LightAttack, 1.02d, 2L);
            for (int i = 1; i < 6; i++)
            {
                session.Press(RhythmAction.LightAttack, 1d + (i * 0.1d), i + 2L);
            }

            Assert.That(session.Encounters[0].Requirements[0].AcceptedPressCount, Is.EqualTo(6));
            Assert.That(session.Encounters[0].Requirements[1].IsResolved, Is.False);
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void RequirementTimesOutOnlyAfterInclusiveGoodBoundary()
        {
            RadialRhythmSession session = CreateSession(CreateTap("timeout", RhythmAction.Guard, 1d));

            Assert.That(session.Update(1.1d), Is.Empty);
            IReadOnlyList<RequirementResult> results = session.Update(1.100001d);

            Assert.That(results.Single().Grade, Is.EqualTo(HitGrade.Miss));
            Assert.That(results.Single().Reason, Is.EqualTo(RadialResultReason.Timeout));
        }

        [Test]
        public void SweepResolvesEveryTargetLinkedToOneRequirement()
        {
            RadialEncounterEventData sweep = CreateTap("sweep", RhythmAction.LightAttack, 1d);
            sweep.eventType = RadialEventType.Sweep;
            sweep.targets.Add(new EncounterTargetData
            {
                targetId = "target-a",
                requirementId = "sweep-input",
                direction = RadialDirection.West,
                archetype = EnemyArchetype.Raider
            });
            sweep.targets.Add(new EncounterTargetData
            {
                targetId = "target-b",
                requirementId = "sweep-input",
                direction = RadialDirection.East,
                archetype = EnemyArchetype.Raider
            });
            RadialRhythmSession session = CreateSession(sweep);

            RadialInputResolveResult result = session.Press(RhythmAction.LightAttack, 1d);

            Assert.That(result.TargetResults, Has.Count.EqualTo(2));
            Assert.That(session.Encounters[0].Targets.All(target => target.IsResolved), Is.True);
        }

        [Test]
        public void DuplicateSequenceIsIgnoredAndResetAllowsFreshResolution()
        {
            RadialRhythmSession session = CreateSession(CreateTap("reset", RhythmAction.Dodge, 1d));

            session.Press(RhythmAction.Dodge, 1d, 7L);
            RadialInputResolveResult duplicate = session.Press(RhythmAction.Dodge, 1d, 7L);
            session.Reset();
            RadialInputResolveResult afterReset = session.Press(RhythmAction.Dodge, 1d, 7L);

            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(afterReset.Consumed, Is.True);
            Assert.That(session.Encounters[0].Result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void ConcurrentEncountersCanResolveFromDifferentActions()
        {
            RadialRhythmSession session = CreateSession(
                CreateTap("guard", RhythmAction.Guard, 1d),
                CreateTap("dodge", RhythmAction.Dodge, 1d));

            session.Press(RhythmAction.Guard, 1d);
            session.Press(RhythmAction.Dodge, 1d);

            Assert.That(session.Encounters.All(encounter => encounter.IsResolved), Is.True);
        }

        private static RadialRhythmSession CreateSession(params RadialEncounterEventData[] encounters)
        {
            return new RadialRhythmSession(encounters);
        }

        private static RadialEncounterEventData CreateTap(
            string eventId,
            RhythmAction action,
            double targetTimeSeconds)
        {
            RadialEncounterEventData encounter = CreateEncounter(eventId, RadialEventType.Tap);
            encounter.requirements.Add(CreateRequirement(
                eventId + "-input",
                action,
                targetTimeSeconds,
                InputGestureType.Tap));
            return encounter;
        }

        private static RadialEncounterEventData CreateHeavy(string eventId)
        {
            RadialEncounterEventData encounter = CreateEncounter(eventId, RadialEventType.HeavyChargeRelease);
            InputRequirementData press = CreateRequirement(
                "heavy-press",
                RhythmAction.HeavyAttack,
                2d,
                InputGestureType.Charge);
            press.orderIndex = 0;
            InputRequirementData release = CreateRequirement(
                "heavy-release",
                RhythmAction.HeavyAttack,
                2.3d,
                InputGestureType.Charge);
            release.phase = RhythmInputPhase.Released;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            release.minimumHoldSeconds = 0.2d;
            release.maximumHoldSeconds = 0.5d;
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            return encounter;
        }

        private static RadialEncounterEventData CreateEncounter(
            string eventId,
            RadialEventType eventType)
        {
            return new RadialEncounterEventData
            {
                eventId = eventId,
                eventType = eventType
            };
        }

        private static InputRequirementData CreateRequirement(
            string requirementId,
            RhythmAction action,
            double targetTimeSeconds,
            InputGestureType gestureType)
        {
            return new InputRequirementData
            {
                requirementId = requirementId,
                acceptedActions = RhythmActionMaskUtility.ToMask(action),
                gestureType = gestureType,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = targetTimeSeconds,
                perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds,
                goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds,
                exclusive = true
            };
        }
    }
}
