using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialEncounterPlannerTests
    {
        [Test]
        public void SameAnalysisAndSeedProduceIdenticalBeatMap()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(24d, SongSectionActivityLevel.Active, 0.7d, 0.5d);
            RadialEncounterPlanner planner = new RadialEncounterPlanner();

            RadialEncounterPlanResult first = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Balanced, "track-42");
            RadialEncounterPlanResult second = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Balanced, "track-42");

            Assert.That(Fingerprint(first.beatMap), Is.EqualTo(Fingerprint(second.beatMap)));
        }

        [Test]
        public void DifficultyChangesInputDensityInOrder()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(30d, SongSectionActivityLevel.Active, 0.75d, 0.25d);
            RadialEncounterPlanner planner = new RadialEncounterPlanner();

            int easy = planner.Plan(analysis, BeatMapDifficulty.Easy, CombatStyle.Balanced, "density").qualityReport.totalInputCost;
            int normal = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Balanced, "density").qualityReport.totalInputCost;
            int hard = planner.Plan(analysis, BeatMapDifficulty.Hard, CombatStyle.Balanced, "density").qualityReport.totalInputCost;

            Assert.That(easy, Is.LessThan(normal));
            Assert.That(normal, Is.LessThan(hard));
        }

        [Test]
        public void SilentSectionNeverReceivesEncounter()
        {
            RadialAudioAnalysisResult analysis = CreateTwoSectionAnalysis();

            RadialEncounterPlanResult result = new RadialEncounterPlanner().Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Balanced,
                "silence");

            Assert.That(result.beatMap.encounters.All(item => FirstTime(item) >= 5d), Is.True);
        }

        [Test]
        public void ActiveGapUsesGridFiller()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(12d, SongSectionActivityLevel.Active, 0.8d, 0.5d);
            analysis.onsetCandidates.Clear();
            analysis.onsetCandidates.Add(CreateOnset(1d, 0.9d, AudioBandMask.Mid));
            analysis.onsetCandidates.Add(CreateOnset(11d, 0.9d, AudioBandMask.Mid));

            RadialEncounterPlanResult result = new RadialEncounterPlanner().Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Balanced,
                "gap");

            Assert.That(result.beatMap.encounters.Any(item => item.eventId.StartsWith("grid-", StringComparison.Ordinal)), Is.True);
            Assert.That(result.qualityReport.longestActiveGapSeconds, Is.LessThanOrEqualTo(2.25d));
        }

        [Test]
        public void CombatStylesRedistributeActionsWithoutChangingBudget()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(48d, SongSectionActivityLevel.Active, 0.65d, 0.55d);
            ClearCandidateBands(analysis);
            RadialEncounterPlanner planner = new RadialEncounterPlanner();

            PlannerQualityReport balanced = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Balanced, "styles").qualityReport;
            PlannerQualityReport aggressive = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Aggressive, "styles").qualityReport;
            PlannerQualityReport defensive = planner.Plan(analysis, BeatMapDifficulty.Normal, CombatStyle.Defensive, "styles").qualityReport;

            Assert.That(aggressive.totalInputCost, Is.EqualTo(balanced.totalInputCost));
            Assert.That(defensive.totalInputCost, Is.EqualTo(balanced.totalInputCost));
            Assert.That(ActionCount(aggressive, RhythmAction.LightAttack), Is.GreaterThan(ActionCount(defensive, RhythmAction.LightAttack)));
            Assert.That(
                ActionCount(defensive, RhythmAction.Guard) + ActionCount(defensive, RhythmAction.Dodge),
                Is.GreaterThan(ActionCount(aggressive, RhythmAction.Guard) + ActionCount(aggressive, RhythmAction.Dodge)));
        }

        [Test]
        public void BurstyPackagesMoreChainOrSweepEvents()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(36d, SongSectionActivityLevel.Active, 0.95d, 0.20d);
            RadialEncounterPlanner planner = new RadialEncounterPlanner();

            PlannerQualityReport balanced = planner.Plan(analysis, BeatMapDifficulty.Hard, CombatStyle.Balanced, "bursty").qualityReport;
            PlannerQualityReport bursty = planner.Plan(analysis, BeatMapDifficulty.Hard, CombatStyle.Bursty, "bursty").qualityReport;

            Assert.That(bursty.totalInputCost, Is.EqualTo(balanced.totalInputCost));
            Assert.That(BurstPackageCount(bursty), Is.GreaterThan(BurstPackageCount(balanced)));
        }

        [Test]
        public void ValidatorMergesSameActionSimultaneousTargetsIntoSweep()
        {
            RadialBeatMapData map = new RadialBeatMapData();
            map.encounters.Add(CreateTap("a", RhythmAction.LightAttack, 2d, RadialDirection.North));
            map.encounters.Add(CreateTap("b", RhythmAction.LightAttack, 2d, RadialDirection.South));

            new RadialBeatMapValidator().ValidateAndRepair(
                map,
                CreateAnalysis(5d, SongSectionActivityLevel.Active, 0.5d, 0.5d),
                BeatMapDifficulty.Normal);

            RadialEncounterEventData sweep = map.encounters.Single(item => Math.Abs(FirstTime(item) - 2d) < 0.01d);
            Assert.That(sweep.eventType, Is.EqualTo(RadialEventType.Sweep));
            Assert.That(sweep.requirements, Has.Count.EqualTo(1));
            Assert.That(sweep.targets, Has.Count.EqualTo(2));
        }

        [Test]
        public void ValidatorEnforcesTwoSimultaneousActions()
        {
            RadialBeatMapData map = new RadialBeatMapData();
            map.encounters.Add(CreateTap("guard", RhythmAction.Guard, 2d, RadialDirection.North));
            map.encounters.Add(CreateTap("light", RhythmAction.LightAttack, 2d, RadialDirection.East));
            map.encounters.Add(CreateTap("dodge", RhythmAction.Dodge, 2d, RadialDirection.South));

            new RadialBeatMapValidator().ValidateAndRepair(
                map,
                CreateAnalysis(5d, SongSectionActivityLevel.Active, 0.5d, 0.5d),
                BeatMapDifficulty.Normal);

            Assert.That(DistinctActionsAt(map, 2d), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void HeavyChargeIntervalIsReserved()
        {
            RadialBeatMapData map = new RadialBeatMapData();
            map.encounters.Add(CreateHeavy("heavy", 2d, 2.4d));
            map.encounters.Add(CreateTap("inside", RhythmAction.LightAttack, 2.2d, RadialDirection.East));

            BeatMapValidationReport report = new RadialBeatMapValidator().ValidateAndRepair(
                map,
                CreateAnalysis(5d, SongSectionActivityLevel.Active, 0.5d, 0.5d),
                BeatMapDifficulty.Hard);

            Assert.That(map.encounters.Any(item => item.eventId == "inside"), Is.False);
            Assert.That(report.dropReasons.Any(reason => reason.Contains("Heavy")), Is.True);
        }

        [Test]
        public void InvalidGuardDodgeChordRepairsToChoice()
        {
            RadialBeatMapData map = new RadialBeatMapData();
            RadialEncounterEventData chord = new RadialEncounterEventData
            {
                eventId = "invalid-chord",
                eventType = RadialEventType.Chord
            };
            chord.requirements.Add(CreateRequirement("guard", RhythmAction.Guard, 2d, InputGestureType.Chord));
            chord.requirements.Add(CreateRequirement("dodge", RhythmAction.Dodge, 2d, InputGestureType.Chord));
            chord.targets.Add(CreateTarget("guard-target", "guard", RadialDirection.North));
            chord.targets.Add(CreateTarget("dodge-target", "dodge", RadialDirection.NorthEast));
            map.encounters.Add(chord);

            new RadialBeatMapValidator().ValidateAndRepair(
                map,
                CreateAnalysis(5d, SongSectionActivityLevel.Active, 0.5d, 0.5d),
                BeatMapDifficulty.Normal);

            Assert.That(map.encounters[0].eventType, Is.EqualTo(RadialEventType.Choice));
            Assert.That(map.encounters[0].requirements[0].acceptedActions, Is.EqualTo(RhythmActionMask.Guard | RhythmActionMask.Dodge));
        }

        [Test]
        public void DirectionsAvoidThirdRepeatAndCompoundCollision()
        {
            RadialBeatMapData map = new RadialBeatMapData();
            map.encounters.Add(CreateTap("one", RhythmAction.Guard, 1d, RadialDirection.North));
            map.encounters.Add(CreateTap("two", RhythmAction.Guard, 2d, RadialDirection.North));
            map.encounters.Add(CreateTap("three", RhythmAction.Guard, 3d, RadialDirection.North));
            RadialEncounterEventData chord = CreateAllowedChord("chord", 4d);
            chord.targets[0].direction = RadialDirection.East;
            chord.targets[1].direction = RadialDirection.East;
            map.encounters.Add(chord);

            new RadialBeatMapValidator().ValidateAndRepair(
                map,
                CreateAnalysis(6d, SongSectionActivityLevel.Active, 0.5d, 0.5d),
                BeatMapDifficulty.Normal,
                7u);

            Assert.That(map.encounters[2].targets[0].direction, Is.Not.EqualTo(RadialDirection.North));
            int distance = DirectionDistance(
                map.encounters[3].targets[0].direction,
                map.encounters[3].targets[1].direction);
            Assert.That(distance, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void InsufficientSignalReportsUnderCoveredInsteadOfForcingFillers()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(12d, SongSectionActivityLevel.Active, 0.8d, 0.5d);
            analysis.onsetCandidates.Clear();
            analysis.beatGrid.gridConfidence = 0d;
            analysis.warnings.Add("Insufficient Signal");

            RadialEncounterPlanResult result = new RadialEncounterPlanner().Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Balanced,
                "poor");

            Assert.That(result.beatMap.encounters, Is.Empty);
            Assert.That(result.qualityReport.result, Is.EqualTo(PlannerQualityResult.UnderCovered));
        }

        [Test]
        public void GeneratedBreakTargetsRequireOnlyLightPresses()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(60d, SongSectionActivityLevel.Peak, 1d, 0.17d);

            RadialEncounterPlanResult result = new RadialEncounterPlanner().Plan(
                analysis,
                BeatMapDifficulty.Hard,
                CombatStyle.Bursty,
                "breaker");

            List<RadialEncounterEventData> breaks = result.beatMap.encounters
                .Where(item => item.eventType == RadialEventType.BreakTarget)
                .ToList();
            Assert.That(breaks, Is.Not.Empty);
            Assert.That(breaks.All(item => item.requirements.Any(requirement =>
                requirement.gestureType == InputGestureType.RepeatedPress
                && requirement.acceptedActions == RhythmActionMask.LightAttack
                && requirement.requiredPressCount > 0)), Is.True);
            Assert.That(breaks.All(item => item.requirements.All(requirement =>
                !RhythmActionMaskUtility.Contains(requirement.acceptedActions, RhythmAction.HeavyAttack))), Is.True);
        }

        [Test]
        public void StyleVariantsReuseOneAnalysisWithoutMutatingIt()
        {
            RadialAudioAnalysisResult analysis = CreateAnalysis(
                24d,
                SongSectionActivityLevel.Active,
                0.85d,
                0.35d);
            int originalCandidateCount = analysis.onsetCandidates.Count;
            RadialEncounterPlanner planner = new RadialEncounterPlanner();

            RadialEncounterPlanResult balanced = planner.Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Balanced,
                "shared-analysis");
            RadialEncounterPlanResult aggressive = planner.Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Aggressive,
                "shared-analysis");
            RadialEncounterPlanResult defensive = planner.Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Defensive,
                "shared-analysis");
            RadialEncounterPlanResult bursty = planner.Plan(
                analysis,
                BeatMapDifficulty.Normal,
                CombatStyle.Bursty,
                "shared-analysis");

            Assert.That(analysis.onsetCandidates, Has.Count.EqualTo(originalCandidateCount));
            Assert.That(balanced.qualityReport.totalInputCost, Is.GreaterThan(0));
            Assert.That(aggressive.qualityReport.totalInputCost, Is.GreaterThan(0));
            Assert.That(defensive.qualityReport.totalInputCost, Is.GreaterThan(0));
            Assert.That(bursty.qualityReport.totalInputCost, Is.GreaterThan(0));
        }

        private static RadialAudioAnalysisResult CreateAnalysis(
            double duration,
            SongSectionActivityLevel level,
            double activity,
            double spacing)
        {
            RadialAudioAnalysisResult result = new RadialAudioAnalysisResult
            {
                durationSeconds = duration,
                activeDurationSeconds = level == SongSectionActivityLevel.Silent ? 0d : duration,
                silentDurationSeconds = level == SongSectionActivityLevel.Silent ? duration : 0d,
                tempoConfidence = 0.9d,
                beatGrid = new BeatGridData
                {
                    bpm = 120d,
                    tempoConfidence = 0.9d,
                    beatIntervalSeconds = 0.5d,
                    gridConfidence = 0.9d,
                    gridStrength = 0.9d
                }
            };
            result.sections.Add(new SongSectionData
            {
                startTimeSeconds = 0d,
                endTimeSeconds = duration,
                activityLevel = level,
                activity = activity,
                gridConfidence = 0.9d,
                tempoConfidence = 0.9d,
                averageRms = 0.4d
            });
            for (double time = 0.5d; time < duration; time += 0.25d)
            {
                result.beatGrid.subdivisionTimesSeconds.Add(time);
                if (Math.Abs((time * 2d) - Math.Round(time * 2d)) < 0.001d)
                {
                    result.beatGrid.beatTimesSeconds.Add(time);
                }
            }
            for (double time = 0.6d; time < duration - 0.2d; time += spacing)
            {
                result.onsetCandidates.Add(CreateOnset(time, 0.8d, AudioBandMask.Low | AudioBandMask.Mid));
            }
            return result;
        }

        private static RadialAudioAnalysisResult CreateTwoSectionAnalysis()
        {
            RadialAudioAnalysisResult result = CreateAnalysis(10d, SongSectionActivityLevel.Active, 0.7d, 0.5d);
            result.sections.Clear();
            result.sections.Add(new SongSectionData
            {
                startTimeSeconds = 0d,
                endTimeSeconds = 5d,
                activityLevel = SongSectionActivityLevel.Silent,
                activity = 0d,
                gridConfidence = 0.9d
            });
            result.sections.Add(new SongSectionData
            {
                startTimeSeconds = 5d,
                endTimeSeconds = 10d,
                activityLevel = SongSectionActivityLevel.Active,
                activity = 0.7d,
                gridConfidence = 0.9d
            });
            result.onsetCandidates.Clear();
            result.onsetCandidates.Add(CreateOnset(2d, 0.95d, AudioBandMask.High));
            result.onsetCandidates.Add(CreateOnset(6d, 0.95d, AudioBandMask.Mid));
            return result;
        }

        private static OnsetCandidateData CreateOnset(double time, double confidence, AudioBandMask bands)
        {
            return new OnsetCandidateData
            {
                timeSeconds = time,
                confidence = confidence,
                strength = confidence,
                supportingBands = bands,
                selectedByAdaptiveThreshold = true
            };
        }

        private static void ClearCandidateBands(RadialAudioAnalysisResult analysis)
        {
            for (int i = 0; i < analysis.onsetCandidates.Count; i++)
            {
                analysis.onsetCandidates[i].supportingBands = AudioBandMask.None;
            }
        }

        private static RadialEncounterEventData CreateTap(
            string id,
            RhythmAction action,
            double time,
            RadialDirection direction)
        {
            RadialEncounterEventData encounter = new RadialEncounterEventData
            {
                eventId = id,
                eventType = RadialEventType.Tap,
                intensity = 0.8f
            };
            InputRequirementData requirement = CreateRequirement(id + "-input", action, time, InputGestureType.Tap);
            encounter.requirements.Add(requirement);
            encounter.targets.Add(CreateTarget(id + "-target", requirement.requirementId, direction));
            return encounter;
        }

        private static RadialEncounterEventData CreateHeavy(string id, double pressTime, double releaseTime)
        {
            RadialEncounterEventData encounter = new RadialEncounterEventData
            {
                eventId = id,
                eventType = RadialEventType.HeavyChargeRelease,
                intensity = 1f
            };
            InputRequirementData press = CreateRequirement(id + "-press", RhythmAction.HeavyAttack, pressTime, InputGestureType.Charge);
            press.orderIndex = 0;
            InputRequirementData release = CreateRequirement(id + "-release", RhythmAction.HeavyAttack, releaseTime, InputGestureType.Charge);
            release.phase = RhythmInputPhase.Released;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            release.minimumHoldSeconds = 0.2d;
            release.maximumHoldSeconds = 0.6d;
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            encounter.targets.Add(CreateTarget(id + "-target", release.requirementId, RadialDirection.West));
            return encounter;
        }

        private static RadialEncounterEventData CreateAllowedChord(string id, double time)
        {
            RadialEncounterEventData encounter = new RadialEncounterEventData
            {
                eventId = id,
                eventType = RadialEventType.Chord
            };
            encounter.requirements.Add(CreateRequirement(id + "-guard", RhythmAction.Guard, time, InputGestureType.Chord));
            encounter.requirements.Add(CreateRequirement(id + "-light", RhythmAction.LightAttack, time, InputGestureType.Chord));
            encounter.targets.Add(CreateTarget(id + "-guard-target", encounter.requirements[0].requirementId, RadialDirection.North));
            encounter.targets.Add(CreateTarget(id + "-light-target", encounter.requirements[1].requirementId, RadialDirection.East));
            return encounter;
        }

        private static InputRequirementData CreateRequirement(
            string id,
            RhythmAction action,
            double time,
            InputGestureType gesture)
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

        private static EncounterTargetData CreateTarget(string id, string requirementId, RadialDirection direction)
        {
            return new EncounterTargetData
            {
                targetId = id,
                requirementId = requirementId,
                direction = direction,
                archetype = EnemyArchetype.Raider
            };
        }

        private static int ActionCount(PlannerQualityReport report, RhythmAction action)
        {
            return report.actionCounts.Single(item => item.action == action).count;
        }

        private static int BurstPackageCount(PlannerQualityReport report)
        {
            return report.sweepCount
                + EventCount(report, RadialEventType.SwarmChain);
        }

        private static int EventCount(PlannerQualityReport report, RadialEventType type)
        {
            return report.eventTypeCounts.Single(item => item.eventType == type).count;
        }

        private static int DistinctActionsAt(RadialBeatMapData map, double time)
        {
            HashSet<RhythmAction> actions = new HashSet<RhythmAction>();
            foreach (RadialEncounterEventData encounter in map.encounters.Where(item => Math.Abs(FirstTime(item) - time) < 0.01d))
            {
                foreach (InputRequirementData requirement in encounter.requirements)
                {
                    foreach (RhythmAction action in new[]
                    {
                        RhythmAction.Guard,
                        RhythmAction.LightAttack,
                        RhythmAction.Dodge,
                        RhythmAction.HeavyAttack
                    })
                    {
                        if (RhythmActionMaskUtility.Contains(requirement.acceptedActions, action))
                        {
                            actions.Add(action);
                        }
                    }
                }
            }
            return actions.Count;
        }

        private static int DirectionDistance(RadialDirection first, RadialDirection second)
        {
            int distance = Math.Abs((int)first - (int)second);
            return Math.Min(distance, 8 - distance);
        }

        private static double FirstTime(RadialEncounterEventData encounter)
        {
            return encounter.requirements.Min(item => item.targetTimeSeconds);
        }

        private static string Fingerprint(RadialBeatMapData map)
        {
            return string.Join("|", map.encounters.Select(encounter =>
                encounter.eventId + ":" + encounter.eventType + ":" + encounter.telegraphLeadSeconds.ToString("R") + ":"
                + string.Join(",", encounter.requirements.Select(requirement =>
                    requirement.requirementId + "@" + requirement.targetTimeSeconds.ToString("R") + "#" + requirement.acceptedActions)) + ":"
                + string.Join(",", encounter.targets.Select(target =>
                    target.targetId + "@" + target.direction + "#" + target.archetype))));
        }
    }
}
