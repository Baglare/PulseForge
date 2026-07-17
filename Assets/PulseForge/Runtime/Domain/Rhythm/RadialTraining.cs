using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public enum TrainingLessonId
    {
        TimingBar,
        GuardTap,
        DodgeTap,
        LightAttack,
        HeavyChargeRelease,
        GuardHold,
        Choice,
        Chord,
        OrderedSequence,
        SwarmChain,
        BreakTarget
    }

    public static class RadialTrainingTiming
    {
        public const double TimingBarPerfectWindowSeconds = 0.200d;
        public const double TimingBarGoodWindowSeconds = 0.450d;

        public static RadialTimingProfile TimingBarProfile => RadialTimingProfile.Create(
            TimingAssistMode.Practice,
            TimingBarPerfectWindowSeconds,
            TimingBarGoodWindowSeconds);
    }

    public enum TrainingFailureReason
    {
        None,
        TooEarly,
        TooLate,
        WrongKey,
        ReleasedTooSoon,
        ReleasedTooLate,
        MissingSecondInput,
        WrongSequenceOrder
    }

    public readonly struct TrainingLessonDefinition
    {
        public TrainingLessonDefinition(
            TrainingLessonId id,
            bool advanced,
            RhythmActionMask actions)
        {
            Id = id;
            Advanced = advanced;
            Actions = actions;
        }

        public TrainingLessonId Id { get; }
        public bool Advanced { get; }
        public RhythmActionMask Actions { get; }
    }

    public static class RadialTrainingCatalog
    {
        private static readonly TrainingLessonDefinition[] Lessons =
        {
            new TrainingLessonDefinition(TrainingLessonId.TimingBar, false, RhythmActionMask.Guard),
            new TrainingLessonDefinition(TrainingLessonId.GuardTap, false, RhythmActionMask.Guard),
            new TrainingLessonDefinition(TrainingLessonId.DodgeTap, false, RhythmActionMask.Dodge),
            new TrainingLessonDefinition(TrainingLessonId.LightAttack, false, RhythmActionMask.LightAttack),
            new TrainingLessonDefinition(TrainingLessonId.HeavyChargeRelease, false, RhythmActionMask.HeavyAttack),
            new TrainingLessonDefinition(TrainingLessonId.GuardHold, true, RhythmActionMask.Guard),
            new TrainingLessonDefinition(TrainingLessonId.Choice, true, RhythmActionMask.Guard | RhythmActionMask.Dodge),
            new TrainingLessonDefinition(TrainingLessonId.Chord, true, RhythmActionMask.Guard | RhythmActionMask.LightAttack),
            new TrainingLessonDefinition(TrainingLessonId.OrderedSequence, true, RhythmActionMask.Guard | RhythmActionMask.LightAttack | RhythmActionMask.Dodge),
            new TrainingLessonDefinition(TrainingLessonId.SwarmChain, true, RhythmActionMask.LightAttack),
            new TrainingLessonDefinition(TrainingLessonId.BreakTarget, true, RhythmActionMask.LightAttack)
        };

        public static IReadOnlyList<TrainingLessonDefinition> All => Lessons;

        public static RadialBeatMapData CreateFixture(TrainingLessonId lessonId)
        {
            RadialBeatMapData beatMap = new RadialBeatMapData
            {
                displayName = "Training - " + lessonId,
                globalOffsetSeconds = 0d
            };
            switch (lessonId)
            {
                case TrainingLessonId.TimingBar:
                    beatMap.encounters.Add(Tap("training-guard", RhythmAction.Guard, 3d));
                    break;
                case TrainingLessonId.GuardTap:
                    beatMap.encounters.Add(Tap("training-guard", RhythmAction.Guard, 1.5d));
                    break;
                case TrainingLessonId.DodgeTap:
                    beatMap.encounters.Add(Tap("training-dodge", RhythmAction.Dodge, 1.5d));
                    break;
                case TrainingLessonId.LightAttack:
                    beatMap.encounters.Add(Tap("training-light", RhythmAction.LightAttack, 1.5d));
                    break;
                case TrainingLessonId.HeavyChargeRelease:
                    beatMap.encounters.Add(Heavy());
                    break;
                case TrainingLessonId.GuardHold:
                    beatMap.encounters.Add(Hold());
                    break;
                case TrainingLessonId.Choice:
                    beatMap.encounters.Add(Choice());
                    break;
                case TrainingLessonId.Chord:
                    beatMap.encounters.Add(Chord());
                    break;
                case TrainingLessonId.OrderedSequence:
                    beatMap.encounters.Add(Sequence());
                    break;
                case TrainingLessonId.SwarmChain:
                    beatMap.encounters.Add(Swarm());
                    break;
                default:
                    beatMap.encounters.Add(BreakTarget());
                    break;
            }
            return beatMap;
        }

        private static RadialEncounterEventData Tap(
            string id,
            RhythmAction action,
            double targetTime)
        {
            RadialEncounterEventData encounter = Encounter(id, RadialEventType.Tap);
            encounter.requirements.Add(Requirement(
                id + "-input",
                action,
                targetTime,
                InputGestureType.Tap));
            AddTarget(encounter, id + "-input", RadialDirection.North, EnemyArchetype.Duelist);
            return encounter;
        }

        private static RadialEncounterEventData Heavy()
        {
            RadialEncounterEventData encounter = Encounter(
                "training-heavy",
                RadialEventType.HeavyChargeRelease);
            InputRequirementData press = Requirement(
                "training-heavy-press",
                RhythmAction.HeavyAttack,
                1.2d,
                InputGestureType.Charge);
            InputRequirementData release = Requirement(
                "training-heavy-release",
                RhythmAction.HeavyAttack,
                1.65d,
                InputGestureType.Charge);
            press.orderIndex = 0;
            press.pairedRequirementId = release.requirementId;
            release.phase = RhythmInputPhase.Released;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            release.minimumHoldSeconds = 0.30d;
            release.maximumHoldSeconds = 0.65d;
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            AddTarget(encounter, release.requirementId, RadialDirection.South, EnemyArchetype.Armored);
            return encounter;
        }

        private static RadialEncounterEventData Hold()
        {
            RadialEncounterEventData encounter = Encounter("training-hold", RadialEventType.GuardHold);
            InputRequirementData hold = Requirement(
                "training-hold-input",
                RhythmAction.Guard,
                1.2d,
                InputGestureType.Hold);
            hold.holdEndTimeSeconds = 1.9d;
            hold.earlyReleaseGraceSeconds = 0.35d;
            hold.allowEarlyReleaseAsGood = true;
            hold.autoCompleteAtHoldEnd = true;
            encounter.requirements.Add(hold);
            AddTarget(encounter, hold.requirementId, RadialDirection.NorthWest, EnemyArchetype.Duelist);
            return encounter;
        }

        private static RadialEncounterEventData Choice()
        {
            RadialEncounterEventData encounter = Encounter("training-choice", RadialEventType.Choice);
            InputRequirementData choice = Requirement(
                "training-choice-input",
                RhythmAction.Guard,
                1.5d,
                InputGestureType.Choice);
            choice.acceptedActions = RhythmActionMask.Guard | RhythmActionMask.Dodge;
            encounter.requirements.Add(choice);
            AddTarget(encounter, choice.requirementId, RadialDirection.East, EnemyArchetype.Raider);
            return encounter;
        }

        private static RadialEncounterEventData Chord()
        {
            RadialEncounterEventData encounter = Encounter("training-chord", RadialEventType.Chord);
            InputRequirementData guard = Requirement(
                "training-chord-guard",
                RhythmAction.Guard,
                1.5d,
                InputGestureType.Chord);
            InputRequirementData light = Requirement(
                "training-chord-light",
                RhythmAction.LightAttack,
                1.5d,
                InputGestureType.Chord);
            encounter.requirements.Add(guard);
            encounter.requirements.Add(light);
            AddTarget(encounter, guard.requirementId, RadialDirection.NorthEast, EnemyArchetype.Duelist);
            AddTarget(encounter, light.requirementId, RadialDirection.SouthWest, EnemyArchetype.Raider);
            return encounter;
        }

        private static RadialEncounterEventData Sequence()
        {
            RadialEncounterEventData encounter = Encounter(
                "training-sequence",
                RadialEventType.OrderedSequence);
            encounter.requirements.Add(OrderedRequirement(
                "training-sequence-guard", RhythmAction.Guard, 1.2d, 0, InputGestureType.SequenceStep));
            encounter.requirements.Add(OrderedRequirement(
                "training-sequence-light", RhythmAction.LightAttack, 1.65d, 1, InputGestureType.SequenceStep));
            encounter.requirements.Add(OrderedRequirement(
                "training-sequence-dodge", RhythmAction.Dodge, 2.1d, 2, InputGestureType.SequenceStep));
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                AddTarget(encounter, encounter.requirements[i].requirementId, (RadialDirection)(i * 2), EnemyArchetype.Raider);
            }
            return encounter;
        }

        private static RadialEncounterEventData Swarm()
        {
            RadialEncounterEventData encounter = Encounter(
                "training-swarm",
                RadialEventType.SwarmChain);
            for (int i = 0; i < 3; i++)
            {
                InputRequirementData step = OrderedRequirement(
                    "training-swarm-" + i,
                    RhythmAction.LightAttack,
                    1.2d + (i * 0.35d),
                    i,
                    InputGestureType.ChainStep);
                encounter.requirements.Add(step);
                AddTarget(encounter, step.requirementId, (RadialDirection)(i * 3), EnemyArchetype.Swarm);
            }
            return encounter;
        }

        private static RadialEncounterEventData BreakTarget()
        {
            RadialEncounterEventData encounter = Encounter(
                "training-break",
                RadialEventType.BreakTarget);
            encounter.requirements.Add(new InputRequirementData
            {
                requirementId = "training-break-count",
                acceptedActions = RhythmActionMask.LightAttack,
                gestureType = InputGestureType.RepeatedPress,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = 1.1d,
                windowStartTimeSeconds = 1.1d,
                perfectDeadlineSeconds = 1.9d,
                goodDeadlineSeconds = 2.2d,
                requiredPressCount = 4,
                minimumPressIntervalSeconds = 0.08d
            });
            AddTarget(encounter, "training-break-count", RadialDirection.South, EnemyArchetype.GiantBreaker);
            return encounter;
        }

        private static RadialEncounterEventData Encounter(string id, RadialEventType type)
        {
            return new RadialEncounterEventData
            {
                eventId = id,
                eventType = type,
                telegraphLeadSeconds = 0.9d
            };
        }

        private static InputRequirementData Requirement(
            string id,
            RhythmAction action,
            double targetTime,
            InputGestureType gesture)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = RhythmActionMaskUtility.ToMask(action),
                gestureType = gesture,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = targetTime,
                exclusive = true
            };
        }

        private static InputRequirementData OrderedRequirement(
            string id,
            RhythmAction action,
            double targetTime,
            int order,
            InputGestureType gesture)
        {
            InputRequirementData requirement = Requirement(id, action, targetTime, gesture);
            requirement.orderIndex = order;
            return requirement;
        }

        private static void AddTarget(
            RadialEncounterEventData encounter,
            string requirementId,
            RadialDirection direction,
            EnemyArchetype archetype)
        {
            encounter.targets.Add(new EncounterTargetData
            {
                targetId = requirementId + "-target",
                requirementId = requirementId,
                direction = direction,
                archetype = archetype
            });
        }
    }

    public sealed class TrainingAttemptProgress
    {
        public TrainingAttemptProgress(int successfulAttempts = 0)
        {
            SuccessfulAttempts = Math.Max(0, Math.Min(2, successfulAttempts));
        }

        public int SuccessfulAttempts { get; private set; }
        public bool RetryPending { get; private set; }
        public bool IsComplete => SuccessfulAttempts >= 2;

        public void RecordSuccess()
        {
            SuccessfulAttempts++;
            RetryPending = false;
        }

        public void RecordFailure()
        {
            RetryPending = true;
        }

        public void BeginRetry()
        {
            RetryPending = false;
        }
    }

    public static class TrainingFailureReasonResolver
    {
        public static TrainingFailureReason Resolve(
            RequirementResult result,
            RadialEventType eventType,
            RadialInputAuditReason auditReason)
        {
            if (auditReason == RadialInputAuditReason.FutureSequenceStep
                || (eventType == RadialEventType.OrderedSequence
                    && result != null
                    && result.Reason == RadialResultReason.WrongInput))
            {
                return TrainingFailureReason.WrongSequenceOrder;
            }
            if (result == null)
            {
                return TrainingFailureReason.TooLate;
            }
            switch (result.Reason)
            {
                case RadialResultReason.WrongInput:
                    return TrainingFailureReason.WrongKey;
                case RadialResultReason.EarlyRelease:
                    return TrainingFailureReason.ReleasedTooSoon;
                case RadialResultReason.InvalidCharge:
                    return result.TimingErrorSeconds >= 0d
                        ? TrainingFailureReason.ReleasedTooLate
                        : TrainingFailureReason.ReleasedTooSoon;
                case RadialResultReason.MissingChordMember:
                    return TrainingFailureReason.MissingSecondInput;
                case RadialResultReason.Timeout:
                    return eventType == RadialEventType.Chord
                        ? TrainingFailureReason.MissingSecondInput
                        : TrainingFailureReason.TooLate;
                default:
                    return result.TimingErrorSeconds < 0d
                        ? TrainingFailureReason.TooEarly
                        : TrainingFailureReason.TooLate;
            }
        }
    }

    public static class TrainingPersistencePolicy
    {
        public const bool RecordsProfileScore = false;
        public const bool RecordsSavedTrackPerformance = false;
    }

    public static class TrainingBindingDisplay
    {
        public static string Build(
            RhythmActionMask actions,
            string guard,
            string lightAttack,
            string dodge,
            string heavyAttack)
        {
            List<string> bindings = new List<string>();
            Append(bindings, actions, RhythmActionMask.Guard, guard);
            Append(bindings, actions, RhythmActionMask.LightAttack, lightAttack);
            Append(bindings, actions, RhythmActionMask.Dodge, dodge);
            Append(bindings, actions, RhythmActionMask.HeavyAttack, heavyAttack);
            return string.Join("  +  ", bindings);
        }

        private static void Append(
            ICollection<string> bindings,
            RhythmActionMask actions,
            RhythmActionMask action,
            string displayName)
        {
            if ((actions & action) == action)
            {
                bindings.Add(displayName ?? string.Empty);
            }
        }
    }
}
