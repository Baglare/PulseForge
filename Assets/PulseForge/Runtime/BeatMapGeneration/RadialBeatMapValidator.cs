using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    public sealed class RadialBeatMapValidator
    {
        private const double SimultaneousTolerance = 0.001d;

        public BeatMapValidationReport ValidateAndRepair(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            uint deterministicSeed = 1u)
        {
            if (beatMap == null)
            {
                throw new ArgumentNullException(nameof(beatMap));
            }
            if (analysis == null)
            {
                throw new ArgumentNullException(nameof(analysis));
            }

            BeatMapValidationReport report = new BeatMapValidationReport();
            if (beatMap.encounters == null)
            {
                beatMap.encounters = new List<RadialEncounterEventData>();
            }

            RemoveNullEntries(beatMap, report);
            RepairFailureEffects(beatMap, report);
            EnsureUniqueIds(beatMap, report);
            MergeSimultaneousSameActionEvents(beatMap, report);
            RepairChords(beatMap, analysis.durationSeconds, report);
            RepairDodgeGestures(beatMap, report);
            ClampTimesAndDropSilentEvents(beatMap, analysis, report);
            ShiftLowConfidenceOnsetsToGrid(beatMap, analysis, report);
            ShortenInvalidChains(beatMap, report);
            ReserveHeavyIntervals(beatMap, report);
            EnforceHoldOverlapRules(beatMap, difficulty, report);
            EnforceTwoActionLimit(beatMap, report);
            EnforceRecoveryAndWindowDensity(beatMap, difficulty, report);
            RepairDirections(beatMap, deterministicSeed, report);
            FillActiveGaps(beatMap, analysis, difficulty, deterministicSeed, report);
            RepairDirections(beatMap, deterministicSeed, report);
            EnsureUniqueIds(beatMap, report);
            beatMap.encounters.Sort(CompareEncounterTime);
            EvaluateCoverage(beatMap, analysis, difficulty, report);
            return report;
        }

        private static void RepairFailureEffects(
            RadialBeatMapData beatMap,
            BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                bool hasSaboteur = false;
                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    hasSaboteur |= encounter.targets[targetIndex].archetype == EnemyArchetype.Saboteur;
                }

                bool validSaboteur = hasSaboteur && IsSaboteurLightTap(encounter);
                if (hasSaboteur && !validSaboteur)
                {
                    for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                    {
                        if (encounter.targets[targetIndex].archetype == EnemyArchetype.Saboteur)
                        {
                            encounter.targets[targetIndex].archetype = EnemyArchetype.Raider;
                        }
                    }
                    encounter.failureEffect = new FailureEffectData();
                    report.repairReasons.Add("Invalid Saboteur reduced to a Raider Tap.");
                    continue;
                }

                if (!validSaboteur)
                {
                    if (encounter.failureEffect == null
                        || encounter.failureEffect.effectType != FailureEffectType.None)
                    {
                        encounter.failureEffect = new FailureEffectData();
                        report.repairReasons.Add("Unsupported failure effect removed.");
                    }
                    continue;
                }

                FailureEffectData effect = encounter.failureEffect;
                if (effect == null || effect.effectType != FailureEffectType.Fog)
                {
                    encounter.failureEffect = CreateDefaultFog(encounter.intensity);
                    report.repairReasons.Add("Saboteur Fog metadata restored.");
                    continue;
                }

                double duration = effect.durationSeconds;
                float multiplier = effect.revealLeadMultiplier;
                double minimumLead = effect.minimumVisibleLeadSeconds;
                bool invalid = double.IsNaN(duration)
                    || double.IsInfinity(duration)
                    || duration <= 0d
                    || float.IsNaN(multiplier)
                    || float.IsInfinity(multiplier)
                    || multiplier <= 0f
                    || multiplier > 1f
                    || double.IsNaN(minimumLead)
                    || double.IsInfinity(minimumLead)
                    || minimumLead < 0d;
                if (invalid)
                {
                    encounter.failureEffect = CreateDefaultFog(encounter.intensity);
                    report.repairReasons.Add("Invalid Saboteur Fog metadata restored.");
                }
            }
        }

        private static bool IsSaboteurLightTap(RadialEncounterEventData encounter)
        {
            return encounter.eventType == RadialEventType.Tap
                && encounter.requirements.Count == 1
                && encounter.targets.Count == 1
                && encounter.requirements[0].acceptedActions == RhythmActionMask.LightAttack
                && encounter.requirements[0].gestureType == InputGestureType.Tap
                && encounter.requirements[0].phase == RhythmInputPhase.Pressed;
        }

        private static FailureEffectData CreateDefaultFog(float intensity)
        {
            double normalized = intensity < 0f ? 0d : intensity > 1f ? 1d : intensity;
            return new FailureEffectData
            {
                effectType = FailureEffectType.Fog,
                durationSeconds = 5d + (4d * normalized),
                revealLeadMultiplier = 0.55f,
                minimumVisibleLeadSeconds = 0.45d
            };
        }

        private static void RemoveNullEntries(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            for (int i = beatMap.encounters.Count - 1; i >= 0; i--)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (encounter == null || encounter.requirements == null || encounter.requirements.Count == 0)
                {
                    beatMap.encounters.RemoveAt(i);
                    report.dropReasons.Add("Invalid empty encounter dropped.");
                    continue;
                }
                if (encounter.targets == null)
                {
                    encounter.targets = new List<EncounterTargetData>();
                    report.repairReasons.Add("Null target list initialized.");
                }
                for (int requirementIndex = encounter.requirements.Count - 1; requirementIndex >= 0; requirementIndex--)
                {
                    if (encounter.requirements[requirementIndex] == null)
                    {
                        encounter.requirements.RemoveAt(requirementIndex);
                        report.repairReasons.Add("Null input requirement removed.");
                    }
                }
                for (int targetIndex = encounter.targets.Count - 1; targetIndex >= 0; targetIndex--)
                {
                    if (encounter.targets[targetIndex] == null)
                    {
                        encounter.targets.RemoveAt(targetIndex);
                        report.repairReasons.Add("Null encounter target removed.");
                    }
                }
                if (encounter.requirements.Count == 0)
                {
                    beatMap.encounters.RemoveAt(i);
                    report.dropReasons.Add("Encounter with no valid requirements dropped.");
                }
            }
        }

        private static void EnsureUniqueIds(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int repairIndex = 0;
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (string.IsNullOrWhiteSpace(encounter.eventId) || !ids.Add(encounter.eventId))
                {
                    encounter.eventId = NextUniqueId(ids, "event", ref repairIndex);
                    report.repairReasons.Add("Duplicate or empty event id replaced.");
                }
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex];
                    string oldId = requirement.requirementId;
                    if (string.IsNullOrWhiteSpace(oldId) || !ids.Add(oldId))
                    {
                        requirement.requirementId = NextUniqueId(ids, encounter.eventId + "-requirement", ref repairIndex);
                        for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                        {
                            if (encounter.targets[targetIndex].requirementId == oldId)
                            {
                                encounter.targets[targetIndex].requirementId = requirement.requirementId;
                            }
                        }
                        for (int pairedIndex = 0; pairedIndex < encounter.requirements.Count; pairedIndex++)
                        {
                            if (encounter.requirements[pairedIndex].pairedRequirementId == oldId)
                            {
                                encounter.requirements[pairedIndex].pairedRequirementId = requirement.requirementId;
                            }
                        }
                        report.repairReasons.Add("Duplicate or empty requirement id replaced.");
                    }
                }
                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    EncounterTargetData target = encounter.targets[targetIndex];
                    if (string.IsNullOrWhiteSpace(target.targetId) || !ids.Add(target.targetId))
                    {
                        target.targetId = NextUniqueId(ids, encounter.eventId + "-target", ref repairIndex);
                        report.repairReasons.Add("Duplicate or empty target id replaced.");
                    }
                }
            }
        }

        private static string NextUniqueId(HashSet<string> ids, string prefix, ref int index)
        {
            string candidate;
            do
            {
                candidate = prefix + "-repair-" + (index++).ToString("D4");
            }
            while (!ids.Add(candidate));
            return candidate;
        }

        private static void MergeSimultaneousSameActionEvents(
            RadialBeatMapData beatMap,
            BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData first = beatMap.encounters[i];
                if (!TryGetSingleTapAction(first, out RhythmAction firstAction, out InputRequirementData firstRequirement))
                {
                    continue;
                }
                for (int j = beatMap.encounters.Count - 1; j > i; j--)
                {
                    RadialEncounterEventData second = beatMap.encounters[j];
                    if (!TryGetSingleTapAction(second, out RhythmAction secondAction, out InputRequirementData secondRequirement)
                        || firstAction != secondAction
                        || Math.Abs(firstRequirement.targetTimeSeconds - secondRequirement.targetTimeSeconds) > SimultaneousTolerance
                        || first.targets.Count + second.targets.Count > 4)
                    {
                        continue;
                    }

                    first.eventType = RadialEventType.Sweep;
                    for (int targetIndex = 0; targetIndex < second.targets.Count; targetIndex++)
                    {
                        EncounterTargetData target = second.targets[targetIndex];
                        target.requirementId = firstRequirement.requirementId;
                        first.targets.Add(target);
                    }
                    beatMap.encounters.RemoveAt(j);
                    report.repairReasons.Add("Simultaneous same-action targets merged into Sweep.");
                }
            }
        }

        private static bool TryGetSingleTapAction(
            RadialEncounterEventData encounter,
            out RhythmAction action,
            out InputRequirementData requirement)
        {
            action = default(RhythmAction);
            requirement = null;
            if (encounter == null
                || encounter.requirements == null
                || encounter.requirements.Count != 1
                || (encounter.eventType != RadialEventType.Tap && encounter.eventType != RadialEventType.Sweep))
            {
                return false;
            }
            requirement = encounter.requirements[0];
            return requirement != null
                && requirement.phase == RhythmInputPhase.Pressed
                && RhythmActionMaskUtility.TryGetSingleAction(requirement.acceptedActions, out action);
        }

        private static void RepairChords(
            RadialBeatMapData beatMap,
            double songDuration,
            BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (encounter.eventType != RadialEventType.Chord || IsAllowedChord(encounter))
                {
                    continue;
                }

                bool hasGuard = HasAction(encounter, RhythmAction.Guard);
                bool hasDodge = HasAction(encounter, RhythmAction.Dodge);
                bool hasLight = HasAction(encounter, RhythmAction.LightAttack);
                bool hasHeavy = HasAction(encounter, RhythmAction.HeavyAttack);
                double targetTime = Math.Min(songDuration, Math.Max(0d, RadialEncounterPlanner.FirstRequirementTime(encounter)));

                if (hasHeavy)
                {
                    ConvertToHeavy(encounter, targetTime);
                    report.repairReasons.Add("Invalid Heavy chord converted to charge-release.");
                }
                else if (hasLight && (hasGuard || hasDodge))
                {
                    RhythmAction support = hasGuard ? RhythmAction.Guard : RhythmAction.Dodge;
                    KeepAllowedChordPair(encounter, support, targetTime);
                    report.repairReasons.Add("Invalid chord reduced to an allowed two-action pair.");
                }
                else if (hasGuard && hasDodge)
                {
                    ConvertToChoice(encounter, targetTime);
                    report.repairReasons.Add("Guard+Dodge chord reduced to Choice.");
                }
                else
                {
                    ConvertToTap(encounter, hasLight ? RhythmAction.LightAttack : hasGuard ? RhythmAction.Guard : RhythmAction.Dodge, targetTime);
                    report.repairReasons.Add("Invalid chord reduced to Tap.");
                }
            }
        }

        private static bool IsAllowedChord(RadialEncounterEventData encounter)
        {
            if (encounter.requirements == null || encounter.requirements.Count != 2)
            {
                return false;
            }
            if (!RhythmActionMaskUtility.TryGetSingleAction(encounter.requirements[0].acceptedActions, out RhythmAction first)
                || !RhythmActionMaskUtility.TryGetSingleAction(encounter.requirements[1].acceptedActions, out RhythmAction second)
                || first == second)
            {
                return false;
            }
            return (first == RhythmAction.LightAttack && (second == RhythmAction.Guard || second == RhythmAction.Dodge))
                || (second == RhythmAction.LightAttack && (first == RhythmAction.Guard || first == RhythmAction.Dodge));
        }

        private static bool HasAction(RadialEncounterEventData encounter, RhythmAction action)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                if (RhythmActionMaskUtility.Contains(encounter.requirements[i].acceptedActions, action))
                {
                    return true;
                }
            }
            return false;
        }

        private static void KeepAllowedChordPair(
            RadialEncounterEventData encounter,
            RhythmAction support,
            double targetTime)
        {
            encounter.requirements.Clear();
            encounter.requirements.Add(NewRequirement(encounter.eventId + "-support", support, InputGestureType.Chord, targetTime));
            encounter.requirements.Add(NewRequirement(encounter.eventId + "-light", RhythmAction.LightAttack, InputGestureType.Chord, targetTime));
            KeepTargets(encounter, 2);
            encounter.targets[0].requirementId = encounter.requirements[0].requirementId;
            encounter.targets[1].requirementId = encounter.requirements[1].requirementId;
        }

        private static void ConvertToChoice(RadialEncounterEventData encounter, double targetTime)
        {
            encounter.eventType = RadialEventType.Choice;
            encounter.requirements.Clear();
            InputRequirementData requirement = NewRequirement(
                encounter.eventId + "-choice",
                RhythmAction.Guard,
                InputGestureType.Choice,
                targetTime);
            requirement.acceptedActions = RhythmActionMask.Guard | RhythmActionMask.Dodge;
            encounter.requirements.Add(requirement);
            KeepTargets(encounter, 1);
            encounter.targets[0].requirementId = requirement.requirementId;
            encounter.targets[0].archetype = EnemyArchetype.ArcherGunner;
        }

        private static void ConvertToTap(
            RadialEncounterEventData encounter,
            RhythmAction action,
            double targetTime)
        {
            encounter.eventType = RadialEventType.Tap;
            encounter.requirements.Clear();
            InputRequirementData requirement = NewRequirement(
                encounter.eventId + "-tap",
                action,
                InputGestureType.Tap,
                targetTime);
            encounter.requirements.Add(requirement);
            KeepTargets(encounter, 1);
            encounter.targets[0].requirementId = requirement.requirementId;
        }

        private static void ConvertToHeavy(RadialEncounterEventData encounter, double releaseTime)
        {
            encounter.eventType = RadialEventType.HeavyChargeRelease;
            encounter.requirements.Clear();
            double pressTime = Math.Max(0d, releaseTime - 0.30d);
            InputRequirementData press = NewRequirement(
                encounter.eventId + "-press",
                RhythmAction.HeavyAttack,
                InputGestureType.Charge,
                pressTime);
            press.orderIndex = 0;
            InputRequirementData release = NewRequirement(
                encounter.eventId + "-release",
                RhythmAction.HeavyAttack,
                InputGestureType.Charge,
                releaseTime);
            release.phase = RhythmInputPhase.Released;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            release.minimumHoldSeconds = Math.Max(0.12d, releaseTime - pressTime - 0.10d);
            release.maximumHoldSeconds = releaseTime - pressTime + 0.12d;
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            KeepTargets(encounter, 1);
            encounter.targets[0].requirementId = release.requirementId;
            encounter.targets[0].archetype = EnemyArchetype.Armored;
        }

        private static void KeepTargets(RadialEncounterEventData encounter, int count)
        {
            while (encounter.targets.Count > count)
            {
                encounter.targets.RemoveAt(encounter.targets.Count - 1);
            }
            while (encounter.targets.Count < count)
            {
                encounter.targets.Add(new EncounterTargetData
                {
                    targetId = encounter.eventId + "-repair-target-" + encounter.targets.Count
                });
            }
        }

        private static InputRequirementData NewRequirement(
            string id,
            RhythmAction action,
            InputGestureType gesture,
            double time)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = RhythmActionMaskUtility.ToMask(action),
                gestureType = gesture,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = time,
                perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds,
                goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds,
                exclusive = true
            };
        }

        private static void RepairDodgeGestures(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex];
                    if (requirement.acceptedActions == RhythmActionMask.Dodge
                        && (requirement.gestureType == InputGestureType.Hold
                            || requirement.gestureType == InputGestureType.RepeatedPress))
                    {
                        requirement.gestureType = InputGestureType.Tap;
                        encounter.eventType = RadialEventType.Tap;
                        requirement.holdEndTimeSeconds = 0d;
                        requirement.requiredPressCount = 0;
                        report.repairReasons.Add("Dodge Hold/Repeat reduced to Tap.");
                    }
                }
            }
        }

        private static void ClampTimesAndDropSilentEvents(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapValidationReport report)
        {
            for (int encounterIndex = beatMap.encounters.Count - 1; encounterIndex >= 0; encounterIndex--)
            {
                RadialEncounterEventData encounter = beatMap.encounters[encounterIndex];
                double primaryTime = RadialEncounterPlanner.FirstRequirementTime(encounter);
                if (IsSilentTime(analysis, primaryTime))
                {
                    beatMap.encounters.RemoveAt(encounterIndex);
                    report.dropReasons.Add("Event inside a Silent section dropped.");
                    continue;
                }
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex];
                    double original = requirement.targetTimeSeconds;
                    requirement.targetTimeSeconds = Clamp(requirement.targetTimeSeconds, 0d, analysis.durationSeconds);
                    if (encounter.eventType == RadialEventType.GuardHold)
                    {
                        requirement.holdEndTimeSeconds = Clamp(
                            Math.Max(requirement.targetTimeSeconds, requirement.holdEndTimeSeconds),
                            requirement.targetTimeSeconds,
                            analysis.durationSeconds);
                    }
                    if (encounter.eventType == RadialEventType.BreakTarget
                        && requirement.gestureType == InputGestureType.RepeatedPress)
                    {
                        requirement.windowStartTimeSeconds = Clamp(requirement.windowStartTimeSeconds, 0d, analysis.durationSeconds);
                        requirement.perfectDeadlineSeconds = Clamp(
                            Math.Max(requirement.windowStartTimeSeconds, requirement.perfectDeadlineSeconds),
                            requirement.windowStartTimeSeconds,
                            analysis.durationSeconds);
                        requirement.goodDeadlineSeconds = Clamp(
                            Math.Max(requirement.perfectDeadlineSeconds, requirement.goodDeadlineSeconds),
                            requirement.perfectDeadlineSeconds,
                            analysis.durationSeconds);
                    }
                    if (Math.Abs(original - requirement.targetTimeSeconds) > SimultaneousTolerance)
                    {
                        report.repairReasons.Add("Requirement time clamped to song bounds.");
                    }
                }
            }
        }

        private static void ShiftLowConfidenceOnsetsToGrid(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapValidationReport report)
        {
            List<double> grid = CollectGrid(analysis);
            if (grid.Count == 0 || analysis.onsetCandidates == null)
            {
                return;
            }
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (encounter.eventId == null || !encounter.eventId.StartsWith("onset-", StringComparison.Ordinal))
                {
                    continue;
                }
                double time = RadialEncounterPlanner.FirstRequirementTime(encounter);
                OnsetCandidateData onset = FindNearestOnset(analysis.onsetCandidates, time, 0.20d);
                if (onset == null || onset.confidence >= 0.35d)
                {
                    continue;
                }
                double gridTime = FindNearestGrid(grid, time, 0.25d);
                if (double.IsNaN(gridTime) || IsSilentTime(analysis, gridTime))
                {
                    continue;
                }
                ShiftEncounter(encounter, gridTime - time);
                report.repairReasons.Add("Low-confidence cue shifted to a nearby grid point.");
            }
        }

        private static void ShortenInvalidChains(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (encounter.eventType != RadialEventType.TimedChain
                    && encounter.eventType != RadialEventType.SwarmChain)
                {
                    continue;
                }
                bool shortened = false;
                while (encounter.requirements.Count > 5 || DistinctActionCount(encounter.requirements) > 2)
                {
                    InputRequirementData removed = encounter.requirements[encounter.requirements.Count - 1];
                    encounter.requirements.RemoveAt(encounter.requirements.Count - 1);
                    RemoveTargetsForRequirement(encounter, removed.requirementId);
                    shortened = true;
                }
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    encounter.requirements[requirementIndex].orderIndex = requirementIndex;
                }
                if (shortened)
                {
                    report.repairReasons.Add("Chain shortened to preserve playability.");
                }
            }
        }

        private static void ReserveHeavyIntervals(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData heavy = beatMap.encounters[i];
                if (heavy.eventType != RadialEventType.HeavyChargeRelease || heavy.requirements.Count < 2)
                {
                    continue;
                }
                double start = double.MaxValue;
                double end = double.MinValue;
                for (int requirementIndex = 0; requirementIndex < heavy.requirements.Count; requirementIndex++)
                {
                    start = Math.Min(start, heavy.requirements[requirementIndex].targetTimeSeconds);
                    end = Math.Max(end, heavy.requirements[requirementIndex].targetTimeSeconds);
                }
                for (int otherIndex = beatMap.encounters.Count - 1; otherIndex >= 0; otherIndex--)
                {
                    if (otherIndex == i)
                    {
                        continue;
                    }
                    RadialEncounterEventData other = beatMap.encounters[otherIndex];
                    if (HasRequirementInside(other, start, end))
                    {
                        beatMap.encounters.RemoveAt(otherIndex);
                        if (otherIndex < i)
                        {
                            i--;
                        }
                        report.dropReasons.Add("Requirement overlapping a Heavy charge interval dropped.");
                    }
                }
            }
        }

        private static bool HasRequirementInside(RadialEncounterEventData encounter, double start, double end)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                double time = encounter.requirements[i].targetTimeSeconds;
                if (time > start + SimultaneousTolerance && time < end - SimultaneousTolerance)
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnforceHoldOverlapRules(
            RadialBeatMapData beatMap,
            BeatMapDifficulty difficulty,
            BeatMapValidationReport report)
        {
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData hold = beatMap.encounters[i];
                if (hold.eventType != RadialEventType.GuardHold || hold.requirements.Count != 1)
                {
                    continue;
                }
                double start = hold.requirements[0].targetTimeSeconds;
                double end = hold.requirements[0].holdEndTimeSeconds;
                int permittedLightCount = 0;
                for (int otherIndex = beatMap.encounters.Count - 1; otherIndex >= 0; otherIndex--)
                {
                    if (otherIndex == i)
                    {
                        continue;
                    }
                    RadialEncounterEventData other = beatMap.encounters[otherIndex];
                    double time = RadialEncounterPlanner.FirstRequirementTime(other);
                    if (time <= start + SimultaneousTolerance || time >= end - SimultaneousTolerance)
                    {
                        continue;
                    }
                    bool permitted = difficulty != BeatMapDifficulty.Easy
                        && permittedLightCount == 0
                        && time - start >= 0.30d
                        && end - time >= 0.30d
                        && IsSingleLightTap(other);
                    if (permitted)
                    {
                        permittedLightCount++;
                        continue;
                    }
                    beatMap.encounters.RemoveAt(otherIndex);
                    if (otherIndex < i)
                    {
                        i--;
                    }
                    report.dropReasons.Add("Event violating Guard Hold overlap rules dropped.");
                }
            }
        }

        private static bool IsSingleLightTap(RadialEncounterEventData encounter)
        {
            return encounter.eventType == RadialEventType.Tap
                && encounter.requirements.Count == 1
                && encounter.requirements[0].acceptedActions == RhythmActionMask.LightAttack;
        }

        private static void EnforceTwoActionLimit(RadialBeatMapData beatMap, BeatMapValidationReport report)
        {
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < beatMap.encounters.Count && !changed; i++)
                {
                    double time = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i]);
                    HashSet<RhythmAction> actions = new HashSet<RhythmAction>();
                    List<int> simultaneous = new List<int>();
                    for (int j = 0; j < beatMap.encounters.Count; j++)
                    {
                        if (Math.Abs(RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[j]) - time) <= SimultaneousTolerance)
                        {
                            simultaneous.Add(j);
                            AddActions(beatMap.encounters[j], actions);
                        }
                    }
                    if (actions.Count <= 2)
                    {
                        continue;
                    }
                    int drop = SelectLowestPriorityEncounter(beatMap.encounters, simultaneous);
                    beatMap.encounters.RemoveAt(drop);
                    report.dropReasons.Add("Third simultaneous action dropped.");
                    changed = true;
                }
            }
            while (changed);
        }

        private static void EnforceRecoveryAndWindowDensity(
            RadialBeatMapData beatMap,
            BeatMapDifficulty difficulty,
            BeatMapValidationReport report)
        {
            beatMap.encounters.Sort(CompareEncounterTime);
            double recovery = PlannerRules.MinimumRecovery(difficulty);
            for (int i = beatMap.encounters.Count - 1; i > 0; i--)
            {
                double current = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i]);
                double previous = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i - 1]);
                if (Math.Abs(current - previous) <= SimultaneousTolerance
                    || current - previous + SimultaneousTolerance >= recovery)
                {
                    continue;
                }
                int drop = LowerPriorityIndex(beatMap.encounters, i - 1, i);
                beatMap.encounters.RemoveAt(drop);
                report.dropReasons.Add("Encounter below the difficulty recovery interval dropped.");
            }

            int maximumCost = (int)Math.Ceiling(PlannerRules.MaximumDensity(difficulty) * 5d);
            bool dropped;
            do
            {
                dropped = false;
                for (int i = 0; i < beatMap.encounters.Count; i++)
                {
                    double windowStart = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i]);
                    int cost = 0;
                    List<int> indices = new List<int>();
                    for (int j = i; j < beatMap.encounters.Count; j++)
                    {
                        double time = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[j]);
                        if (time - windowStart > 5d + SimultaneousTolerance)
                        {
                            break;
                        }
                        cost += PlannerQualityReportBuilder.InputCost(beatMap.encounters[j]);
                        indices.Add(j);
                    }
                    if (cost <= maximumCost)
                    {
                        continue;
                    }
                    int drop = SelectLowestPriorityEncounter(beatMap.encounters, indices);
                    beatMap.encounters.RemoveAt(drop);
                    report.dropReasons.Add("Encounter exceeding five-second density cap dropped.");
                    dropped = true;
                    break;
                }
            }
            while (dropped);
        }

        private static int SelectLowestPriorityEncounter(
            List<RadialEncounterEventData> encounters,
            List<int> indices)
        {
            int selected = indices[indices.Count - 1];
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (IsGridFill(encounters[index]) && !IsGridFill(encounters[selected]))
                {
                    selected = index;
                }
                else if (IsGridFill(encounters[index]) == IsGridFill(encounters[selected])
                    && encounters[index].intensity < encounters[selected].intensity)
                {
                    selected = index;
                }
            }
            return selected;
        }

        private static int LowerPriorityIndex(List<RadialEncounterEventData> encounters, int first, int second)
        {
            if (IsGridFill(encounters[first]) != IsGridFill(encounters[second]))
            {
                return IsGridFill(encounters[first]) ? first : second;
            }
            return encounters[first].intensity <= encounters[second].intensity ? first : second;
        }

        private static bool IsGridFill(RadialEncounterEventData encounter)
        {
            return encounter.eventId != null && encounter.eventId.StartsWith("grid-", StringComparison.Ordinal);
        }

        private static void RepairDirections(
            RadialBeatMapData beatMap,
            uint seed,
            BeatMapValidationReport report)
        {
            StableRandom random = new StableRandom(seed);
            RadialDirection previous = RadialDirection.North;
            RadialDirection beforePrevious = RadialDirection.South;
            for (int encounterIndex = 0; encounterIndex < beatMap.encounters.Count; encounterIndex++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[encounterIndex];
                HashSet<RadialDirection> occupied = new HashSet<RadialDirection>();
                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    EncounterTargetData target = encounter.targets[targetIndex];
                    int direction = (int)target.direction;
                    int directionAttempts = 0;
                    while (occupied.Contains((RadialDirection)direction)
                        || IsUnreadableAgainstEarlierEncounter(
                            beatMap,
                            encounterIndex,
                            encounter,
                            target,
                            (RadialDirection)direction))
                    {
                        direction = (direction + 1) % 8;
                        directionAttempts++;
                        report.repairReasons.Add("Overlapping direction rotated.");
                        if (directionAttempts >= 8)
                        {
                            break;
                        }
                    }
                    if (targetIndex > 0 && HasDifferentActionsForTargets(encounter, 0, targetIndex))
                    {
                        int firstDirection = (int)encounter.targets[0].direction;
                        if (CircularDistance(firstDirection, direction) < 2)
                        {
                            direction = (firstDirection + 2 + random.NextInt(5)) % 8;
                            report.repairReasons.Add("Compound target directions separated by at least 90 degrees.");
                        }
                    }
                    target.direction = (RadialDirection)direction;
                    occupied.Add(target.direction);
                }
                if (encounter.targets.Count > 0)
                {
                    RadialDirection current = encounter.targets[0].direction;
                    if (encounterIndex >= 2 && current == previous && current == beforePrevious)
                    {
                        current = (RadialDirection)(((int)current + 2) % 8);
                        encounter.targets[0].direction = current;
                        report.repairReasons.Add("Third consecutive direction rotated.");
                    }
                    beforePrevious = previous;
                    previous = current;
                }
            }
        }

        private static bool IsUnreadableAgainstEarlierEncounter(
            RadialBeatMapData beatMap,
            int encounterIndex,
            RadialEncounterEventData currentEncounter,
            EncounterTargetData currentTarget,
            RadialDirection proposedDirection)
        {
            InputRequirementData currentRequirement = FindRequirement(
                currentEncounter,
                currentTarget.requirementId);
            if (currentRequirement == null)
            {
                return false;
            }
            for (int previousIndex = 0; previousIndex < encounterIndex; previousIndex++)
            {
                RadialEncounterEventData previous = beatMap.encounters[previousIndex];
                for (int targetIndex = 0; targetIndex < previous.targets.Count; targetIndex++)
                {
                    EncounterTargetData previousTarget = previous.targets[targetIndex];
                    InputRequirementData previousRequirement = FindRequirement(previous, previousTarget.requirementId);
                    if (previousRequirement == null
                        || Math.Abs(previousRequirement.targetTimeSeconds - currentRequirement.targetTimeSeconds)
                            > SimultaneousTolerance)
                    {
                        continue;
                    }
                    if (previousTarget.direction == proposedDirection)
                    {
                        return true;
                    }
                    if (previousRequirement.acceptedActions != currentRequirement.acceptedActions
                        && CircularDistance((int)previousTarget.direction, (int)proposedDirection) < 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasDifferentActionsForTargets(
            RadialEncounterEventData encounter,
            int firstTarget,
            int secondTarget)
        {
            InputRequirementData first = FindRequirement(encounter, encounter.targets[firstTarget].requirementId);
            InputRequirementData second = FindRequirement(encounter, encounter.targets[secondTarget].requirementId);
            return first != null && second != null && first.acceptedActions != second.acceptedActions;
        }

        private static InputRequirementData FindRequirement(RadialEncounterEventData encounter, string id)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                if (encounter.requirements[i].requirementId == id)
                {
                    return encounter.requirements[i];
                }
            }
            return null;
        }

        private static int CircularDistance(int first, int second)
        {
            int distance = Math.Abs(first - second);
            return Math.Min(distance, 8 - distance);
        }

        private static void FillActiveGaps(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            uint seed,
            BeatMapValidationReport report)
        {
            if (!CanUseGridFillers(analysis))
            {
                return;
            }
            List<double> grid = CollectGrid(analysis);
            StableRandom random = new StableRandom(seed);
            int fillerIndex = 0;
            for (int sectionIndex = 0; sectionIndex < analysis.sections.Count; sectionIndex++)
            {
                SongSectionData section = analysis.sections[sectionIndex];
                if (section.activityLevel == SongSectionActivityLevel.Silent)
                {
                    continue;
                }
                bool added;
                do
                {
                    List<double> times = GetInputTimes(beatMap, section);
                    FindLargestGap(times, section, out double gapStart, out double gapEnd);
                    if (gapEnd - gapStart <= PlannerRules.MaximumGap(difficulty) + SimultaneousTolerance)
                    {
                        break;
                    }
                    double desired = (gapStart + gapEnd) * 0.5d;
                    double gridTime = FindNearestGridInsideGap(
                        grid,
                        desired,
                        gapStart,
                        gapEnd,
                        analysis,
                        beatMap,
                        difficulty);
                    added = !double.IsNaN(gridTime);
                    if (added)
                    {
                        RadialEncounterEventData filler = CreateFiller(
                            gridTime,
                            fillerIndex++,
                            random.NextInt(8));
                        beatMap.encounters.Add(filler);
                        report.repairReasons.Add("Active gap filled from beat grid.");
                    }
                }
                while (added);
            }
        }

        private static RadialEncounterEventData CreateFiller(double time, int index, int direction)
        {
            string id = "grid-repair-" + index.ToString("D5");
            InputRequirementData requirement = NewRequirement(
                id + "-input",
                RhythmAction.LightAttack,
                InputGestureType.Tap,
                time);
            RadialEncounterEventData encounter = new RadialEncounterEventData
            {
                eventId = id,
                eventType = RadialEventType.Tap,
                intensity = 0.25f,
                telegraphLeadSeconds = 0.85d
            };
            encounter.requirements.Add(requirement);
            encounter.targets.Add(new EncounterTargetData
            {
                targetId = id + "-target",
                requirementId = requirement.requirementId,
                direction = (RadialDirection)direction,
                archetype = EnemyArchetype.Raider
            });
            return encounter;
        }

        private static double FindNearestGridInsideGap(
            List<double> grid,
            double desired,
            double gapStart,
            double gapEnd,
            RadialAudioAnalysisResult analysis,
            RadialBeatMapData beatMap,
            BeatMapDifficulty difficulty)
        {
            double best = double.NaN;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < grid.Count; i++)
            {
                double time = grid[i];
                if (time <= gapStart + PlannerRules.MinimumRecovery(difficulty)
                    || time >= gapEnd - PlannerRules.MinimumRecovery(difficulty)
                    || IsSilentTime(analysis, time)
                    || !CanPlaceFillerAt(beatMap, time, difficulty))
                {
                    continue;
                }
                double distance = Math.Abs(time - desired);
                if (distance < bestDistance)
                {
                    best = time;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool CanPlaceFillerAt(
            RadialBeatMapData beatMap,
            double time,
            BeatMapDifficulty difficulty)
        {
            int cost = 1;
            double recovery = PlannerRules.MinimumRecovery(difficulty);
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                double encounterTime = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i]);
                if (Math.Abs(encounterTime - time) < recovery - SimultaneousTolerance)
                {
                    return false;
                }
                if (encounterTime >= time - 2.5d && encounterTime <= time + 2.5d)
                {
                    cost += PlannerQualityReportBuilder.InputCost(beatMap.encounters[i]);
                }
            }
            return cost <= Math.Ceiling(PlannerRules.MaximumDensity(difficulty) * 5d);
        }

        private static void EvaluateCoverage(
            RadialBeatMapData beatMap,
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            BeatMapValidationReport report)
        {
            for (int i = 0; i < analysis.sections.Count; i++)
            {
                SongSectionData section = analysis.sections[i];
                if (section.activityLevel == SongSectionActivityLevel.Silent
                    || section.endTimeSeconds <= section.startTimeSeconds)
                {
                    continue;
                }
                int cost = InputCostInSection(beatMap, section);
                double density = cost / (section.endTimeSeconds - section.startTimeSeconds);
                List<double> times = GetInputTimes(beatMap, section);
                FindLargestGap(times, section, out double gapStart, out double gapEnd);
                if (density + SimultaneousTolerance < PlannerRules.MinimumDensity(difficulty) * 0.90d
                    || gapEnd - gapStart > PlannerRules.MaximumGap(difficulty) + 0.05d)
                {
                    report.underCovered = true;
                }
            }
        }

        private static int InputCostInSection(RadialBeatMapData beatMap, SongSectionData section)
        {
            int cost = 0;
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                double time = RadialEncounterPlanner.FirstRequirementTime(beatMap.encounters[i]);
                if (time >= section.startTimeSeconds && time < section.endTimeSeconds)
                {
                    cost += PlannerQualityReportBuilder.InputCost(beatMap.encounters[i]);
                }
            }
            return cost;
        }

        private static List<double> GetInputTimes(RadialBeatMapData beatMap, SongSectionData section)
        {
            List<double> result = new List<double>();
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                AddInputTimes(beatMap.encounters[i], section, result);
            }
            result.Sort();
            return result;
        }

        internal static void AddInputTimes(
            RadialEncounterEventData encounter,
            SongSectionData section,
            List<double> destination)
        {
            for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
            {
                InputRequirementData requirement = encounter.requirements[requirementIndex];
                if (requirement.gestureType == InputGestureType.RepeatedPress
                    && requirement.requiredPressCount > 0)
                {
                    double start = requirement.windowStartTimeSeconds;
                    double end = Math.Max(start, requirement.perfectDeadlineSeconds);
                    for (int pressIndex = 0; pressIndex < requirement.requiredPressCount; pressIndex++)
                    {
                        double amount = requirement.requiredPressCount == 1
                            ? 0d
                            : (double)pressIndex / (requirement.requiredPressCount - 1);
                        double time = start + ((end - start) * amount);
                        if (time >= section.startTimeSeconds && time < section.endTimeSeconds)
                        {
                            destination.Add(time);
                        }
                    }
                }
                else if (requirement.targetTimeSeconds >= section.startTimeSeconds
                    && requirement.targetTimeSeconds < section.endTimeSeconds)
                {
                    destination.Add(requirement.targetTimeSeconds);
                }
            }
        }

        internal static void FindLargestGap(
            List<double> times,
            SongSectionData section,
            out double gapStart,
            out double gapEnd)
        {
            gapStart = section.startTimeSeconds;
            gapEnd = section.endTimeSeconds;
            double cursor = section.startTimeSeconds;
            double largest = -1d;
            for (int i = 0; i <= times.Count; i++)
            {
                double end = i < times.Count ? times[i] : section.endTimeSeconds;
                if (end - cursor > largest)
                {
                    largest = end - cursor;
                    gapStart = cursor;
                    gapEnd = end;
                }
                cursor = end;
            }
        }

        private static bool CanUseGridFillers(RadialAudioAnalysisResult analysis)
        {
            if (analysis.beatGrid == null || analysis.beatGrid.gridConfidence < 0.25d)
            {
                return false;
            }
            if (analysis.warnings != null)
            {
                for (int i = 0; i < analysis.warnings.Count; i++)
                {
                    if (string.Equals(analysis.warnings[i], "Insufficient Signal", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsSilentTime(RadialAudioAnalysisResult analysis, double time)
        {
            if (analysis.sections == null)
            {
                return false;
            }
            for (int i = 0; i < analysis.sections.Count; i++)
            {
                SongSectionData section = analysis.sections[i];
                if (time >= section.startTimeSeconds && time < section.endTimeSeconds)
                {
                    return section.activityLevel == SongSectionActivityLevel.Silent;
                }
            }
            return false;
        }

        private static List<double> CollectGrid(RadialAudioAnalysisResult analysis)
        {
            List<double> result = new List<double>();
            if (analysis.beatGrid == null)
            {
                return result;
            }
            if (analysis.beatGrid.beatTimesSeconds != null)
            {
                result.AddRange(analysis.beatGrid.beatTimesSeconds);
            }
            if (analysis.beatGrid.subdivisionTimesSeconds != null)
            {
                result.AddRange(analysis.beatGrid.subdivisionTimesSeconds);
            }
            result.Sort();
            return result;
        }

        private static OnsetCandidateData FindNearestOnset(
            List<OnsetCandidateData> onsets,
            double time,
            double maximumDistance)
        {
            OnsetCandidateData best = null;
            double distance = maximumDistance;
            for (int i = 0; i < onsets.Count; i++)
            {
                OnsetCandidateData onset = onsets[i];
                if (onset == null)
                {
                    continue;
                }
                double candidateDistance = Math.Abs(onset.timeSeconds - time);
                if (candidateDistance <= distance)
                {
                    best = onset;
                    distance = candidateDistance;
                }
            }
            return best;
        }

        private static double FindNearestGrid(List<double> grid, double time, double maximumDistance)
        {
            double best = double.NaN;
            double distance = maximumDistance;
            for (int i = 0; i < grid.Count; i++)
            {
                double candidateDistance = Math.Abs(grid[i] - time);
                if (candidateDistance <= distance)
                {
                    best = grid[i];
                    distance = candidateDistance;
                }
            }
            return best;
        }

        private static void ShiftEncounter(RadialEncounterEventData encounter, double delta)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                InputRequirementData requirement = encounter.requirements[i];
                requirement.targetTimeSeconds += delta;
                if (encounter.eventType == RadialEventType.GuardHold)
                {
                    requirement.holdEndTimeSeconds += delta;
                }
                if (encounter.eventType == RadialEventType.BreakTarget
                    && requirement.gestureType == InputGestureType.RepeatedPress)
                {
                    requirement.windowStartTimeSeconds += delta;
                    requirement.perfectDeadlineSeconds += delta;
                    requirement.goodDeadlineSeconds += delta;
                }
            }
        }

        private static void AddActions(RadialEncounterEventData encounter, HashSet<RhythmAction> actions)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                InputRequirementData requirement = encounter.requirements[i];
                AddActionIfPresent(requirement.acceptedActions, RhythmAction.Guard, actions);
                AddActionIfPresent(requirement.acceptedActions, RhythmAction.Dodge, actions);
                AddActionIfPresent(requirement.acceptedActions, RhythmAction.LightAttack, actions);
                AddActionIfPresent(requirement.acceptedActions, RhythmAction.HeavyAttack, actions);
            }
        }

        private static void AddActionIfPresent(
            RhythmActionMask mask,
            RhythmAction action,
            HashSet<RhythmAction> actions)
        {
            if (RhythmActionMaskUtility.Contains(mask, action))
            {
                actions.Add(action);
            }
        }

        private static int DistinctActionCount(List<InputRequirementData> requirements)
        {
            HashSet<RhythmAction> actions = new HashSet<RhythmAction>();
            for (int i = 0; i < requirements.Count; i++)
            {
                AddActionIfPresent(requirements[i].acceptedActions, RhythmAction.Guard, actions);
                AddActionIfPresent(requirements[i].acceptedActions, RhythmAction.Dodge, actions);
                AddActionIfPresent(requirements[i].acceptedActions, RhythmAction.LightAttack, actions);
                AddActionIfPresent(requirements[i].acceptedActions, RhythmAction.HeavyAttack, actions);
            }
            return actions.Count;
        }

        private static void RemoveTargetsForRequirement(RadialEncounterEventData encounter, string requirementId)
        {
            for (int i = encounter.targets.Count - 1; i >= 0; i--)
            {
                if (encounter.targets[i].requirementId == requirementId)
                {
                    encounter.targets.RemoveAt(i);
                }
            }
        }

        private static int CompareEncounterTime(RadialEncounterEventData left, RadialEncounterEventData right)
        {
            return RadialEncounterPlanner.FirstRequirementTime(left)
                .CompareTo(RadialEncounterPlanner.FirstRequirementTime(right));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
