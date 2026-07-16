using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialSaboteurPlannerTests
    {
        [TestCase(BeatMapDifficulty.Easy, 1, 45d)]
        [TestCase(BeatMapDifficulty.Normal, 2, 35d)]
        [TestCase(BeatMapDifficulty.Hard, 3, 25d)]
        public void AggressiveStyleRespectsCountAndSpacingLimits(
            BeatMapDifficulty difficulty,
            int maximum,
            double minimumSpacing)
        {
            List<RadialEncounterEventData> encounters = CreateLightTaps();

            int converted = Apply(encounters, 180d, difficulty, CombatStyle.Aggressive, "spacing");
            List<double> times = encounters
                .Where(IsSaboteur)
                .Select(item => item.requirements[0].targetTimeSeconds)
                .OrderBy(value => value)
                .ToList();

            Assert.That(converted, Is.LessThanOrEqualTo(maximum));
            Assert.That(times, Has.Count.EqualTo(converted));
            for (int i = 1; i < times.Count; i++)
            {
                Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(minimumSpacing));
            }
        }

        [Test]
        public void ConversionPreservesInputBudgetAndEventTimesWhileLegacyProducesNone()
        {
            List<RadialEncounterEventData> encounters = CreateLightTaps();
            double[] originalTimes = encounters.Select(item => item.requirements[0].targetTimeSeconds).ToArray();
            int originalCost = encounters.Sum(item => item.requirements.Count);

            int converted = Apply(encounters, 180d, BeatMapDifficulty.Hard, CombatStyle.Balanced, "budget");
            List<RadialEncounterEventData> legacy = CreateLightTaps();
            int legacyConverted = Apply(legacy, 180d, BeatMapDifficulty.Hard, CombatStyle.Legacy, "budget");

            Assert.That(converted, Is.GreaterThan(0));
            Assert.That(encounters.Sum(item => item.requirements.Count), Is.EqualTo(originalCost));
            Assert.That(encounters.Select(item => item.requirements[0].targetTimeSeconds), Is.EqualTo(originalTimes));
            Assert.That(legacyConverted, Is.Zero);
            Assert.That(legacy.Any(IsSaboteur), Is.False);
        }

        private static int Apply(
            List<RadialEncounterEventData> encounters,
            double duration,
            BeatMapDifficulty difficulty,
            CombatStyle style,
            string seed)
        {
            Type type = typeof(RadialEncounterPlanner).Assembly.GetType(
                "PulseForge.BeatMapGeneration.SaboteurEncounterPlanner",
                true);
            MethodInfo method = type.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public);
            return (int)method.Invoke(null, new object[] { encounters, duration, difficulty, style, seed });
        }

        private static List<RadialEncounterEventData> CreateLightTaps()
        {
            double[] times = { 20d, 50d, 80d, 110d, 140d, 165d };
            List<RadialEncounterEventData> result = new List<RadialEncounterEventData>();
            for (int i = 0; i < times.Length; i++)
            {
                InputRequirementData requirement = new InputRequirementData
                {
                    requirementId = "light-" + i,
                    acceptedActions = RhythmActionMask.LightAttack,
                    gestureType = InputGestureType.Tap,
                    phase = RhythmInputPhase.Pressed,
                    targetTimeSeconds = times[i]
                };
                RadialEncounterEventData encounter = new RadialEncounterEventData
                {
                    eventId = "event-" + i,
                    eventType = RadialEventType.Tap,
                    intensity = 0.5f,
                    telegraphLeadSeconds = 0.8d
                };
                encounter.requirements.Add(requirement);
                encounter.targets.Add(new EncounterTargetData
                {
                    targetId = "target-" + i,
                    requirementId = requirement.requirementId,
                    direction = (RadialDirection)(i % 8),
                    archetype = EnemyArchetype.Raider
                });
                result.Add(encounter);
            }
            return result;
        }

        private static bool IsSaboteur(RadialEncounterEventData encounter)
        {
            return encounter.targets[0].archetype == EnemyArchetype.Saboteur;
        }
    }
}
