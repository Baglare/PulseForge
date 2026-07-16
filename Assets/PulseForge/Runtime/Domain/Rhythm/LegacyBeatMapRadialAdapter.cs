using System;
using System.Collections.Generic;

namespace PulseForge.Domain.Rhythm
{
    public static class LegacyBeatMapRadialAdapter
    {
        public static RadialBeatMapData Convert(
            IEnumerable<BeatEventData> beatEvents,
            string displayName = "Legacy Beat Map")
        {
            if (beatEvents == null)
            {
                throw new ArgumentNullException(nameof(beatEvents));
            }

            RadialBeatMapData beatMap = new RadialBeatMapData
            {
                schemaVersion = 4,
                displayName = displayName ?? string.Empty
            };

            foreach (BeatEventData beatEvent in beatEvents)
            {
                RhythmAction action;
                if (beatEvent.Action == RhythmAction.Guard)
                {
                    action = RhythmAction.Guard;
                }
                else if (beatEvent.Action == RhythmAction.LightAttack)
                {
                    action = RhythmAction.LightAttack;
                }
                else
                {
                    throw new ArgumentException(
                        "Only legacy Guard and Strike events can be adapted.",
                        nameof(beatEvents));
                }

                string requirementId = beatEvent.EventId + ":input";
                RadialEncounterEventData encounter = new RadialEncounterEventData
                {
                    eventId = beatEvent.EventId,
                    eventType = RadialEventType.Tap,
                    intensity = beatEvent.Intensity
                };
                encounter.requirements.Add(new InputRequirementData
                {
                    requirementId = requirementId,
                    acceptedActions = RhythmActionMaskUtility.ToMask(action),
                    gestureType = InputGestureType.Tap,
                    phase = RhythmInputPhase.Pressed,
                    targetTimeSeconds = beatEvent.TargetTimeSeconds,
                    perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds,
                    goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds,
                    exclusive = true
                });
                encounter.targets.Add(new EncounterTargetData
                {
                    targetId = beatEvent.EventId + ":target",
                    requirementId = requirementId,
                    direction = RadialDirection.North,
                    archetype = EnemyArchetype.Duelist
                });
                beatMap.encounters.Add(encounter);
            }

            return beatMap;
        }
    }
}
