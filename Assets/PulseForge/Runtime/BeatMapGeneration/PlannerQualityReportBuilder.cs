using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    internal static class PlannerQualityReportBuilder
    {
        public static PlannerQualityReport Build(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            BeatMapValidationReport validation)
        {
            return Build(
                beatMap,
                analysis,
                difficulty,
                PlannerRules.DefaultCoverage(difficulty),
                validation);
        }

        public static PlannerQualityReport Build(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            CoverageMode coverage,
            BeatMapValidationReport validation)
        {
            PlannerQualityReport report = new PlannerQualityReport
            {
                coverageMode = coverage,
                angularRepairCount = validation.angularRepairCount,
                activeDurationSeconds = ActiveDuration(analysis),
                repairReasons = new List<string>(validation.repairReasons),
                dropReasons = new List<string>(validation.dropReasons)
            };

            int[] actionCounts = new int[4];
            int[] eventCounts = new int[Enum.GetValues(typeof(RadialEventType)).Length];
            int[] archetypeCounts = new int[Enum.GetValues(typeof(EnemyArchetype)).Length];
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                int cost = InputCost(encounter);
                report.totalInputCost += cost;
                if (encounter.eventId != null
                    && encounter.eventId.StartsWith("grid-", StringComparison.Ordinal))
                {
                    report.gridFillInputCost += cost;
                }
                else
                {
                    report.onsetInputCost += cost;
                }
                eventCounts[(int)encounter.eventType]++;
                if (encounter.eventType == RadialEventType.Sweep)
                {
                    report.sweepCount++;
                }
                if (IsCompound(encounter.eventType))
                {
                    report.compoundEventCount++;
                }
                CountFailureEffects(encounter, report);
                CountActions(encounter, actionCounts);
                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    archetypeCounts[(int)encounter.targets[targetIndex].archetype]++;
                }
            }

            report.overallDensity = report.activeDurationSeconds > 0d
                ? report.totalInputCost / report.activeDurationSeconds
                : 0d;
            report.onsetToGridFillRatio = report.gridFillInputCost > 0
                ? (double)report.onsetInputCost / report.gridFillInputCost
                : report.onsetInputCost;
            report.compoundEventRatio = beatMap.encounters.Count > 0
                ? (double)report.compoundEventCount / beatMap.encounters.Count
                : 0d;

            BuildSectionReports(report, beatMap, analysis);
            BuildCountLists(report, actionCounts, eventCounts, archetypeCounts);
            BuildRhythmAlignment(report, beatMap, analysis);
            report.coverageResult = validation.underCovered
                ? PlannerQualityResult.UnderCovered
                : validation.repairReasons.Count > 0 || validation.dropReasons.Count > 0
                    ? PlannerQualityResult.PassWithRepairs
                    : PlannerQualityResult.Pass;
            if (!validation.underCovered
                && report.beatAlignedRequirementRatio < 0.85d
                && report.totalInputCost > 0)
            {
                report.coverageResult = PlannerQualityResult.RhythmAlignmentLow;
                report.warnings.Add("Rhythm alignment is below the 85 percent target.");
            }
            report.result = report.coverageResult;
            return report;
        }

        private static void BuildRhythmAlignment(
            PlannerQualityReport report,
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis)
        {
            List<double> grid = new List<double>();
            if (analysis.beatGrid != null)
            {
                AddGridRange(grid, analysis.beatGrid.beatTimesSeconds);
                AddGridRange(grid, analysis.beatGrid.subdivisionTimesSeconds);
            }
            grid.Sort();
            for (int i = grid.Count - 1; i > 0; i--)
            {
                if (Math.Abs(grid[i] - grid[i - 1]) <= 0.0001d)
                {
                    grid.RemoveAt(i);
                }
            }

            List<double> activeGrid = new List<double>();
            for (int i = 0; i < grid.Count; i++)
            {
                if (IsActiveTime(analysis, grid[i]))
                {
                    activeGrid.Add(grid[i]);
                }
            }
            report.activeGridPointCount = activeGrid.Count;

            int requirementCount = 0;
            int alignedCount = 0;
            double totalDeviation = 0d;
            HashSet<int> usedGridIndices = new HashSet<int>();
            for (int encounterIndex = 0; encounterIndex < beatMap.encounters.Count; encounterIndex++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[encounterIndex];
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex];
                    if (requirement == null || requirement.isOptional)
                    {
                        continue;
                    }
                    requirementCount++;
                    int nearest = FindNearestGridIndex(activeGrid, requirement.targetTimeSeconds);
                    if (nearest < 0)
                    {
                        report.offGridRequirementCount++;
                        continue;
                    }
                    double deviation = Math.Abs(activeGrid[nearest] - requirement.targetTimeSeconds);
                    totalDeviation += deviation;
                    report.maximumGridDeviationSeconds = Math.Max(
                        report.maximumGridDeviationSeconds,
                        deviation);
                    if (deviation <= 0.13d)
                    {
                        alignedCount++;
                        usedGridIndices.Add(nearest);
                    }
                    else
                    {
                        report.offGridRequirementCount++;
                    }
                }
            }

            report.beatAlignedRequirementRatio = requirementCount > 0
                ? (double)alignedCount / requirementCount
                : 0d;
            report.averageGridDeviationSeconds = requirementCount > 0
                ? totalDeviation / requirementCount
                : 0d;
            report.usedGridPointCount = usedGridIndices.Count;
            report.usedGridPointRatio = report.activeGridPointCount > 0
                ? (double)report.usedGridPointCount / report.activeGridPointCount
                : 0d;
        }

        private static void AddGridRange(List<double> destination, List<double> source)
        {
            if (source == null)
            {
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                double value = source[i];
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    destination.Add(value);
                }
            }
        }

        private static int FindNearestGridIndex(List<double> grid, double time)
        {
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < grid.Count; i++)
            {
                double distance = Math.Abs(grid[i] - time);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool IsActiveTime(RadialAudioAnalysisResult analysis, double time)
        {
            if (analysis.sections == null)
            {
                return false;
            }
            for (int i = 0; i < analysis.sections.Count; i++)
            {
                SongSectionData section = analysis.sections[i];
                if (section != null
                    && section.activityLevel != SongSectionActivityLevel.Silent
                    && time >= section.startTimeSeconds
                    && time < section.endTimeSeconds)
                {
                    return true;
                }
            }
            return false;
        }

        private static void CountFailureEffects(
            RadialEncounterEventData encounter,
            PlannerQualityReport report)
        {
            bool hasSaboteur = false;
            if (encounter.targets != null)
            {
                for (int i = 0; i < encounter.targets.Count; i++)
                {
                    hasSaboteur |= encounter.targets[i] != null
                        && encounter.targets[i].archetype == EnemyArchetype.Saboteur;
                }
            }
            if (hasSaboteur)
            {
                report.saboteurEncounterCount++;
            }

            FailureEffectData effect = encounter.failureEffect;
            if (effect == null || effect.effectType != FailureEffectType.Fog)
            {
                return;
            }
            report.fogFailureEffectCount++;
            report.totalFogDurationSeconds += effect.durationSeconds;
            report.minimumFogDurationSeconds = report.fogFailureEffectCount == 1
                ? effect.durationSeconds
                : Math.Min(report.minimumFogDurationSeconds, effect.durationSeconds);
            report.maximumFogDurationSeconds = Math.Max(
                report.maximumFogDurationSeconds,
                effect.durationSeconds);
        }

        public static int InputCost(RadialEncounterEventData encounter)
        {
            if (encounter == null || encounter.requirements == null)
            {
                return 0;
            }
            switch (encounter.eventType)
            {
                case RadialEventType.HeavyChargeRelease:
                case RadialEventType.Chord:
                    return 2;
                case RadialEventType.OrderedSequence:
                case RadialEventType.TimedChain:
                case RadialEventType.SwarmChain:
                    return CountRequiredRequirements(encounter);
                case RadialEventType.BreakTarget:
                    int breakCost = 0;
                    for (int i = 0; i < encounter.requirements.Count; i++)
                    {
                        InputRequirementData requirement = encounter.requirements[i];
                        breakCost += requirement.gestureType == InputGestureType.RepeatedPress
                            ? Math.Max(0, requirement.requiredPressCount)
                            : 1;
                    }
                    return breakCost;
                default:
                    return encounter.requirements.Count > 0 ? 1 : 0;
            }
        }

        private static int CountRequiredRequirements(RadialEncounterEventData encounter)
        {
            int count = 0;
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                if (!encounter.requirements[i].isOptional)
                {
                    count++;
                }
            }
            return count;
        }

        private static void CountActions(RadialEncounterEventData encounter, int[] counts)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                InputRequirementData requirement = encounter.requirements[i];
                int multiplier = requirement.gestureType == InputGestureType.RepeatedPress
                    ? Math.Max(0, requirement.requiredPressCount)
                    : 1;
                IncrementIfAccepted(requirement.acceptedActions, RhythmAction.Guard, multiplier, counts);
                IncrementIfAccepted(requirement.acceptedActions, RhythmAction.LightAttack, multiplier, counts);
                IncrementIfAccepted(requirement.acceptedActions, RhythmAction.Dodge, multiplier, counts);
                IncrementIfAccepted(requirement.acceptedActions, RhythmAction.HeavyAttack, multiplier, counts);
            }
        }

        private static void IncrementIfAccepted(
            RhythmActionMask mask,
            RhythmAction action,
            int amount,
            int[] counts)
        {
            if (RhythmActionMaskUtility.Contains(mask, action))
            {
                counts[(int)action] += amount;
            }
        }

        private static void BuildSectionReports(
            PlannerQualityReport report,
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis)
        {
            double longest = 0d;
            if (analysis.sections == null)
            {
                return;
            }
            for (int sectionIndex = 0; sectionIndex < analysis.sections.Count; sectionIndex++)
            {
                SongSectionData section = analysis.sections[sectionIndex];
                if (section.activityLevel == SongSectionActivityLevel.Silent
                    || section.endTimeSeconds <= section.startTimeSeconds)
                {
                    continue;
                }
                int cost = 0;
                List<double> times = new List<double>();
                for (int encounterIndex = 0; encounterIndex < beatMap.encounters.Count; encounterIndex++)
                {
                    RadialEncounterEventData encounter = beatMap.encounters[encounterIndex];
                    double encounterTime = RadialEncounterPlanner.FirstRequirementTime(encounter);
                    if (encounterTime >= section.startTimeSeconds && encounterTime < section.endTimeSeconds)
                    {
                        cost += InputCost(encounter);
                    }
                    RadialBeatMapValidator.AddInputTimes(encounter, section, times);
                }
                times.Sort();
                RadialBeatMapValidator.FindLargestGap(
                    times,
                    section,
                    out double gapStart,
                    out double gapEnd);
                double gap = gapEnd - gapStart;
                longest = Math.Max(longest, gap);
                report.sectionDensities.Add(new SectionDensityReport
                {
                    startTimeSeconds = section.startTimeSeconds,
                    endTimeSeconds = section.endTimeSeconds,
                    activityLevel = section.activityLevel,
                    inputCost = cost,
                    density = cost / (section.endTimeSeconds - section.startTimeSeconds),
                    longestGapSeconds = gap
                });
            }
            report.longestActiveGapSeconds = longest;
        }

        private static void BuildCountLists(
            PlannerQualityReport report,
            int[] actions,
            int[] events,
            int[] archetypes)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                report.actionCounts.Add(new ActionCountData
                {
                    action = (RhythmAction)i,
                    count = actions[i]
                });
            }
            for (int i = 0; i < events.Length; i++)
            {
                report.eventTypeCounts.Add(new EventTypeCountData
                {
                    eventType = (RadialEventType)i,
                    count = events[i]
                });
            }
            for (int i = 0; i < archetypes.Length; i++)
            {
                report.enemyArchetypeCounts.Add(new EnemyArchetypeCountData
                {
                    archetype = (EnemyArchetype)i,
                    count = archetypes[i]
                });
            }
        }

        private static bool IsCompound(RadialEventType type)
        {
            return type != RadialEventType.Tap;
        }

        private static double ActiveDuration(RadialAudioAnalysisResult analysis)
        {
            double duration = 0d;
            if (analysis.sections == null)
            {
                return duration;
            }
            for (int i = 0; i < analysis.sections.Count; i++)
            {
                SongSectionData section = analysis.sections[i];
                if (section.activityLevel != SongSectionActivityLevel.Silent)
                {
                    duration += Math.Max(0d, section.endTimeSeconds - section.startTimeSeconds);
                }
            }
            return duration;
        }
    }
}
