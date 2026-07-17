namespace PulseForge.Domain.Rhythm
{
    public static class RadialTimingFixture
    {
        public static RadialBeatMapData Create()
        {
            RadialBeatMapData beatMap = new RadialBeatMapData
            {
                displayName = "Timing Audit Fixture"
            };
            beatMap.encounters.Add(CreateTap("fixture-guard", 2d, RhythmActionMask.Guard, RadialDirection.North));
            beatMap.encounters.Add(CreateTap("fixture-light", 3d, RhythmActionMask.LightAttack, RadialDirection.East));
            beatMap.encounters.Add(CreateTap("fixture-dodge", 4d, RhythmActionMask.Dodge, RadialDirection.South));
            beatMap.encounters.Add(CreateHold());
            beatMap.encounters.Add(CreateHeavy());
            beatMap.encounters.Add(CreateChord());
            return beatMap;
        }

        private static RadialEncounterEventData CreateTap(
            string id,
            double timeSeconds,
            RhythmActionMask action,
            RadialDirection direction)
        {
            RadialEncounterEventData encounter = CreateEncounter(id, RadialEventType.Tap, 1d);
            encounter.requirements.Add(CreateRequirement(id + "-input", action, RhythmInputPhase.Pressed, timeSeconds));
            encounter.targets.Add(CreateTarget(id + "-target", id + "-input", direction, EnemyArchetype.Duelist));
            return encounter;
        }

        private static RadialEncounterEventData CreateHold()
        {
            RadialEncounterEventData encounter = CreateEncounter("fixture-hold", RadialEventType.GuardHold, 1.1d);
            InputRequirementData requirement = CreateRequirement(
                "fixture-hold-input",
                RhythmActionMask.Guard,
                RhythmInputPhase.Pressed,
                5d);
            requirement.gestureType = InputGestureType.Hold;
            requirement.holdEndTimeSeconds = 6d;
            encounter.requirements.Add(requirement);
            encounter.targets.Add(CreateTarget(
                "fixture-hold-target",
                requirement.requirementId,
                RadialDirection.NorthWest,
                EnemyArchetype.Duelist));
            return encounter;
        }

        private static RadialEncounterEventData CreateHeavy()
        {
            RadialEncounterEventData encounter = CreateEncounter(
                "fixture-heavy",
                RadialEventType.HeavyChargeRelease,
                1.2d);
            InputRequirementData press = CreateRequirement(
                "fixture-heavy-press",
                RhythmActionMask.HeavyAttack,
                RhythmInputPhase.Pressed,
                7d);
            press.gestureType = InputGestureType.Charge;
            press.pairedRequirementId = "fixture-heavy-release";
            InputRequirementData release = CreateRequirement(
                "fixture-heavy-release",
                RhythmActionMask.HeavyAttack,
                RhythmInputPhase.Released,
                7.45d);
            release.gestureType = InputGestureType.Charge;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            release.minimumHoldSeconds = 0.30d;
            release.maximumHoldSeconds = 0.65d;
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            encounter.targets.Add(CreateTarget(
                "fixture-heavy-target",
                release.requirementId,
                RadialDirection.SouthWest,
                EnemyArchetype.Armored));
            return encounter;
        }

        private static RadialEncounterEventData CreateChord()
        {
            RadialEncounterEventData encounter = CreateEncounter("fixture-chord", RadialEventType.Chord, 1.1d);
            InputRequirementData guard = CreateRequirement(
                "fixture-chord-guard",
                RhythmActionMask.Guard,
                RhythmInputPhase.Pressed,
                9d);
            guard.gestureType = InputGestureType.Chord;
            InputRequirementData light = CreateRequirement(
                "fixture-chord-light",
                RhythmActionMask.LightAttack,
                RhythmInputPhase.Pressed,
                9d);
            light.gestureType = InputGestureType.Chord;
            encounter.requirements.Add(guard);
            encounter.requirements.Add(light);
            encounter.targets.Add(CreateTarget(
                "fixture-chord-guard-target",
                guard.requirementId,
                RadialDirection.NorthEast,
                EnemyArchetype.Duelist));
            encounter.targets.Add(CreateTarget(
                "fixture-chord-light-target",
                light.requirementId,
                RadialDirection.SouthWest,
                EnemyArchetype.Raider));
            return encounter;
        }

        private static RadialEncounterEventData CreateEncounter(
            string id,
            RadialEventType type,
            double telegraphSeconds)
        {
            return new RadialEncounterEventData
            {
                eventId = id,
                eventType = type,
                telegraphLeadSeconds = telegraphSeconds
            };
        }

        private static InputRequirementData CreateRequirement(
            string id,
            RhythmActionMask action,
            RhythmInputPhase phase,
            double timeSeconds)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = action,
                phase = phase,
                targetTimeSeconds = timeSeconds
            };
        }

        private static EncounterTargetData CreateTarget(
            string id,
            string requirementId,
            RadialDirection direction,
            EnemyArchetype archetype)
        {
            return new EncounterTargetData
            {
                targetId = id,
                requirementId = requirementId,
                direction = direction,
                archetype = archetype
            };
        }
    }
}
