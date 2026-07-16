using System;
using System.Collections.Generic;
using PulseForge.AudioAnalysis;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    public sealed class RadialEncounterPlanner
    {
        private const double TimeTolerance = 0.0001d;

        public RadialEncounterPlanResult Plan(
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            CombatStyle combatStyle,
            string deterministicSeed)
        {
            if (analysis == null)
            {
                throw new ArgumentNullException(nameof(analysis));
            }

            if (analysis.durationSeconds <= 0d
                || double.IsNaN(analysis.durationSeconds)
                || double.IsInfinity(analysis.durationSeconds))
            {
                throw new ArgumentException("Analysis duration must be finite and positive.", nameof(analysis));
            }

            string seed = deterministicSeed ?? string.Empty;
            List<PlannedCue> cues = BuildCueBudget(analysis, difficulty, seed);
            StableRandom structureRandom = new StableRandom(StableHash(seed + "|structure"));
            StableRandom actionRandom = new StableRandom(StableHash(seed + "|actions"));
            List<RadialEncounterEventData> encounters = BuildEncounters(
                analysis,
                cues,
                difficulty,
                combatStyle,
                structureRandom,
                actionRandom);

            DirectionPlanner directionPlanner = new DirectionPlanner(StableHash(seed + "|directions"));
            directionPlanner.Assign(encounters);
            SaboteurEncounterPlanner.Apply(
                encounters,
                analysis.durationSeconds,
                difficulty,
                combatStyle,
                seed);

            RadialBeatMapData beatMap = new RadialBeatMapData
            {
                schemaVersion = 3,
                displayName = deterministicSeed ?? string.Empty,
                encounters = encounters
            };

            RadialBeatMapValidator validator = new RadialBeatMapValidator();
            BeatMapValidationReport validation = validator.ValidateAndRepair(
                beatMap,
                analysis,
                difficulty,
                StableHash(seed + "|repair"));

            PlannerQualityReport quality = PlannerQualityReportBuilder.Build(
                beatMap,
                analysis,
                difficulty,
                validation);
            return new RadialEncounterPlanResult
            {
                beatMap = beatMap,
                qualityReport = quality
            };
        }

        private static List<PlannedCue> BuildCueBudget(
            RadialAudioAnalysisResult analysis,
            BeatMapDifficulty difficulty,
            string seed)
        {
            List<PlannedCue> result = new List<PlannedCue>();
            if (analysis.sections == null || analysis.sections.Count == 0)
            {
                return result;
            }

            List<double> gridTimes = CollectGridTimes(analysis);
            for (int sectionIndex = 0; sectionIndex < analysis.sections.Count; sectionIndex++)
            {
                SongSectionData section = analysis.sections[sectionIndex];
                if (section == null
                    || section.activityLevel == SongSectionActivityLevel.Silent
                    || section.endTimeSeconds <= section.startTimeSeconds)
                {
                    continue;
                }

                double duration = section.endTimeSeconds - section.startTimeSeconds;
                double activity = Clamp01(section.activity);
                double targetDensity = Lerp(
                    PlannerRules.MinimumDensity(difficulty),
                    PlannerRules.MaximumDensity(difficulty),
                    activity);
                int targetCost = Math.Max(1, (int)Math.Round(duration * targetDensity));
                List<OnsetCandidateData> candidates = GetSectionCandidates(analysis, section);
                candidates.Sort(CompareCandidatePriority);

                double confidenceFloor = difficulty == BeatMapDifficulty.Easy
                    ? 0.55d
                    : difficulty == BeatMapDifficulty.Hard ? 0.28d : 0.40d;
                List<PlannedCue> selected = new List<PlannedCue>();
                for (int i = 0; i < candidates.Count && selected.Count < targetCost; i++)
                {
                    OnsetCandidateData candidate = candidates[i];
                    if (candidate.confidence + TimeTolerance < confidenceFloor
                        || !CanAddCue(selected, candidate.timeSeconds, difficulty))
                    {
                        continue;
                    }

                    selected.Add(new PlannedCue
                    {
                        TimeSeconds = candidate.timeSeconds,
                        Confidence = Clamp01(candidate.confidence),
                        Bands = candidate.supportingBands,
                        IsGridFill = false,
                        SectionIndex = sectionIndex
                    });
                }

                selected.Sort(CompareCueTime);
                bool gridUsable = IsGridUsable(analysis, section);
                if (gridUsable)
                {
                    FillLargeGaps(
                        selected,
                        gridTimes,
                        section,
                        sectionIndex,
                        difficulty,
                        targetCost);
                    FillBudget(
                        selected,
                        gridTimes,
                        section,
                        sectionIndex,
                        difficulty,
                        targetCost,
                        StableHash(seed + "|section|" + sectionIndex));
                }

                selected.Sort(CompareCueTime);
                result.AddRange(selected);
            }

            result.Sort(CompareCueTime);
            return result;
        }

        private static List<RadialEncounterEventData> BuildEncounters(
            RadialAudioAnalysisResult analysis,
            List<PlannedCue> cues,
            BeatMapDifficulty difficulty,
            CombatStyle style,
            StableRandom structureRandom,
            StableRandom actionRandom)
        {
            List<RadialEncounterEventData> encounters = new List<RadialEncounterEventData>();
            int eventIndex = 0;
            for (int i = 0; i < cues.Count;)
            {
                PlannedCue cue = cues[i];
                SongSectionData section = analysis.sections[cue.SectionIndex];
                int clusterCount = CountCluster(cues, i, 0.95d, 6);

                if (section.activityLevel == SongSectionActivityLevel.Peak
                    && clusterCount >= 4
                    && structureRandom.NextDouble() < 0.42d)
                {
                    int count = Math.Min(6, clusterCount);
                    encounters.Add(CreateBreakTarget(cues, i, count, eventIndex++, difficulty, actionRandom));
                    i += count;
                    continue;
                }

                if (clusterCount >= 3 && structureRandom.NextDouble() < 0.42d)
                {
                    int count = Math.Min(4, clusterCount);
                    bool swarm = style == CombatStyle.Bursty || cue.Bands == AudioBandMask.High;
                    encounters.Add(CreateChain(cues, i, count, eventIndex++, swarm, difficulty, actionRandom));
                    i += count;
                    continue;
                }

                if (i + 1 < cues.Count
                    && cues[i + 1].TimeSeconds - cue.TimeSeconds >= 0.20d
                    && cues[i + 1].TimeSeconds - cue.TimeSeconds <= 0.55d
                    && IsLowOrMidEmphasis(cue)
                    && structureRandom.NextDouble() < 0.24d)
                {
                    bool useHeavy = HeavyWeight(style) > 0
                        && actionRandom.NextInt(100) < HeavyWeight(style);
                    encounters.Add(useHeavy
                        ? CreateHeavy(cue, cues[i + 1], eventIndex++, difficulty, actionRandom)
                        : CreateOrderedSequence(cue, cues[i + 1], eventIndex++, style, difficulty, actionRandom));
                    i += 2;
                    continue;
                }

                if (i + 1 < cues.Count
                    && cues[i + 1].TimeSeconds - cue.TimeSeconds <= 0.32d
                    && structureRandom.NextDouble() < 0.30d)
                {
                    bool chord = cues[i + 1].TimeSeconds - cue.TimeSeconds <= 0.16d;
                    encounters.Add(chord
                        ? CreateChord(cue, cues[i + 1], eventIndex++, style, difficulty, actionRandom)
                        : CreateOrderedSequence(cue, cues[i + 1], eventIndex++, style, difficulty, actionRandom));
                    i += 2;
                    continue;
                }

                if (CanCreateHold(analysis, cues, i, difficulty) && structureRandom.NextDouble() < 0.20d)
                {
                    encounters.Add(CreateHold(analysis, cues, i, eventIndex++, difficulty, actionRandom));
                    i++;
                    continue;
                }

                encounters.Add(CreateSingle(cue, eventIndex++, style, difficulty, actionRandom));
                i++;
            }

            encounters.Sort(CompareEncounterTime);
            return encounters;
        }

        private static RadialEncounterEventData CreateSingle(
            PlannedCue cue,
            int eventIndex,
            CombatStyle style,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            bool sharpHigh = (cue.Bands & AudioBandMask.High) != 0
                && (cue.Bands & AudioBandMask.Low) == 0;
            if (sharpHigh && style != CombatStyle.Legacy && random.NextDouble() < 0.55d)
            {
                RadialEncounterEventData choice = CreateEncounter(
                    EventId(cue, eventIndex),
                    RadialEventType.Choice,
                    cue,
                    difficulty,
                    random);
                choice.requirements.Add(CreateRequirement(
                    choice.eventId + "-choice",
                    RhythmActionMask.Guard | RhythmActionMask.Dodge,
                    InputGestureType.Choice,
                    cue.TimeSeconds));
                choice.targets.Add(CreateTarget(
                    choice.eventId + "-target",
                    choice.requirements[0].requirementId,
                    EnemyArchetype.ArcherGunner));
                choice.telegraphLeadSeconds = Math.Max(choice.telegraphLeadSeconds, 1.15d);
                return choice;
            }

            RhythmAction action = ChooseAction(style, cue, random, false);
            bool sweep = action == RhythmAction.LightAttack
                && random.NextDouble() < SweepChance(style, cue);
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(cue, eventIndex),
                sweep ? RadialEventType.Sweep : RadialEventType.Tap,
                cue,
                difficulty,
                random);
            InputRequirementData requirement = CreateRequirement(
                encounter.eventId + "-input",
                RhythmActionMaskUtility.ToMask(action),
                InputGestureType.Tap,
                cue.TimeSeconds);
            encounter.requirements.Add(requirement);

            int targetCount = sweep ? 2 + random.NextInt(3) : 1;
            EnemyArchetype archetype = sweep ? EnemyArchetype.Swarm : ArchetypeForAction(action);
            for (int i = 0; i < targetCount; i++)
            {
                encounter.targets.Add(CreateTarget(
                    encounter.eventId + "-target-" + i,
                    requirement.requirementId,
                    archetype));
            }

            return encounter;
        }

        private static RadialEncounterEventData CreateHold(
            RadialAudioAnalysisResult analysis,
            List<PlannedCue> cues,
            int cueIndex,
            int eventIndex,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            PlannedCue cue = cues[cueIndex];
            SongSectionData section = analysis.sections[cue.SectionIndex];
            double nextTime = cueIndex + 1 < cues.Count ? cues[cueIndex + 1].TimeSeconds : section.endTimeSeconds;
            double end = Math.Min(section.endTimeSeconds - 0.1d, Math.Min(cue.TimeSeconds + 0.85d, nextTime - 0.32d));
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(cue, eventIndex),
                RadialEventType.GuardHold,
                cue,
                difficulty,
                random);
            InputRequirementData requirement = CreateRequirement(
                encounter.eventId + "-hold",
                RhythmActionMask.Guard,
                InputGestureType.Hold,
                cue.TimeSeconds);
            requirement.holdEndTimeSeconds = Math.Max(cue.TimeSeconds + 0.45d, end);
            requirement.earlyReleaseGraceSeconds = 0.10d;
            requirement.allowEarlyReleaseAsGood = true;
            requirement.autoCompleteAtHoldEnd = true;
            encounter.requirements.Add(requirement);
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-target",
                requirement.requirementId,
                EnemyArchetype.Duelist));
            return encounter;
        }

        private static RadialEncounterEventData CreateHeavy(
            PlannedCue pressCue,
            PlannedCue releaseCue,
            int eventIndex,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(pressCue, eventIndex),
                RadialEventType.HeavyChargeRelease,
                pressCue,
                difficulty,
                random);
            InputRequirementData press = CreateRequirement(
                encounter.eventId + "-press",
                RhythmActionMask.HeavyAttack,
                InputGestureType.Charge,
                pressCue.TimeSeconds);
            press.orderIndex = 0;
            InputRequirementData release = CreateRequirement(
                encounter.eventId + "-release",
                RhythmActionMask.HeavyAttack,
                InputGestureType.Charge,
                releaseCue.TimeSeconds);
            release.phase = RhythmInputPhase.Released;
            release.orderIndex = 1;
            release.pairedRequirementId = press.requirementId;
            double duration = releaseCue.TimeSeconds - pressCue.TimeSeconds;
            release.minimumHoldSeconds = Math.Max(0.12d, duration - 0.10d);
            release.maximumHoldSeconds = Math.Min(0.65d, duration + 0.12d);
            encounter.requirements.Add(press);
            encounter.requirements.Add(release);
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-target",
                release.requirementId,
                EnemyArchetype.Armored));
            return encounter;
        }

        private static RadialEncounterEventData CreateChord(
            PlannedCue firstCue,
            PlannedCue secondCue,
            int eventIndex,
            CombatStyle style,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(firstCue, eventIndex),
                RadialEventType.Chord,
                firstCue,
                difficulty,
                random);
            RhythmAction support = style == CombatStyle.Defensive || random.NextDouble() < 0.55d
                ? RhythmAction.Guard
                : RhythmAction.Dodge;
            double target = (firstCue.TimeSeconds + secondCue.TimeSeconds) * 0.5d;
            encounter.requirements.Add(CreateRequirement(
                encounter.eventId + "-support",
                RhythmActionMaskUtility.ToMask(support),
                InputGestureType.Chord,
                target));
            encounter.requirements.Add(CreateRequirement(
                encounter.eventId + "-light",
                RhythmActionMask.LightAttack,
                InputGestureType.Chord,
                target));
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-support-target",
                encounter.requirements[0].requirementId,
                support == RhythmAction.Guard ? EnemyArchetype.Duelist : EnemyArchetype.Brute));
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-light-target",
                encounter.requirements[1].requirementId,
                EnemyArchetype.Raider));
            return encounter;
        }

        private static RadialEncounterEventData CreateOrderedSequence(
            PlannedCue firstCue,
            PlannedCue secondCue,
            int eventIndex,
            CombatStyle style,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(firstCue, eventIndex),
                RadialEventType.OrderedSequence,
                firstCue,
                difficulty,
                random);
            RhythmAction firstAction = style == CombatStyle.Defensive
                ? RhythmAction.Guard
                : random.NextDouble() < 0.5d ? RhythmAction.Guard : RhythmAction.Dodge;
            InputRequirementData first = CreateRequirement(
                encounter.eventId + "-step-0",
                RhythmActionMaskUtility.ToMask(firstAction),
                InputGestureType.SequenceStep,
                firstCue.TimeSeconds);
            first.orderIndex = 0;
            InputRequirementData second = CreateRequirement(
                encounter.eventId + "-step-1",
                RhythmActionMask.LightAttack,
                InputGestureType.SequenceStep,
                secondCue.TimeSeconds);
            second.orderIndex = 1;
            encounter.requirements.Add(first);
            encounter.requirements.Add(second);
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-target-0",
                first.requirementId,
                firstAction == RhythmAction.Guard ? EnemyArchetype.Duelist : EnemyArchetype.Brute));
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-target-1",
                second.requirementId,
                EnemyArchetype.Duelist));
            return encounter;
        }

        private static RadialEncounterEventData CreateChain(
            List<PlannedCue> cues,
            int start,
            int count,
            int eventIndex,
            bool swarm,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            PlannedCue firstCue = cues[start];
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(firstCue, eventIndex),
                swarm ? RadialEventType.SwarmChain : RadialEventType.TimedChain,
                firstCue,
                difficulty,
                random);
            for (int i = 0; i < count; i++)
            {
                InputRequirementData requirement = CreateRequirement(
                    encounter.eventId + "-step-" + i,
                    RhythmActionMask.LightAttack,
                    InputGestureType.ChainStep,
                    cues[start + i].TimeSeconds);
                requirement.orderIndex = i;
                encounter.requirements.Add(requirement);
                encounter.targets.Add(CreateTarget(
                    encounter.eventId + "-target-" + i,
                    requirement.requirementId,
                    swarm ? EnemyArchetype.Swarm : EnemyArchetype.Raider));
            }

            return encounter;
        }

        private static RadialEncounterEventData CreateBreakTarget(
            List<PlannedCue> cues,
            int start,
            int count,
            int eventIndex,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            PlannedCue firstCue = cues[start];
            PlannedCue lastCue = cues[start + count - 1];
            RadialEncounterEventData encounter = CreateEncounter(
                EventId(firstCue, eventIndex),
                RadialEventType.BreakTarget,
                firstCue,
                difficulty,
                random);
            InputRequirementData requirement = CreateRequirement(
                encounter.eventId + "-break",
                RhythmActionMask.LightAttack,
                InputGestureType.RepeatedPress,
                firstCue.TimeSeconds);
            requirement.windowStartTimeSeconds = firstCue.TimeSeconds;
            requirement.perfectDeadlineSeconds = lastCue.TimeSeconds;
            requirement.goodDeadlineSeconds = lastCue.TimeSeconds + 0.20d;
            requirement.requiredPressCount = count;
            requirement.minimumPressIntervalSeconds = 0.05d;
            encounter.requirements.Add(requirement);
            encounter.targets.Add(CreateTarget(
                encounter.eventId + "-target",
                requirement.requirementId,
                EnemyArchetype.GiantBreaker));
            return encounter;
        }

        private static RadialEncounterEventData CreateEncounter(
            string id,
            RadialEventType type,
            PlannedCue cue,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            return new RadialEncounterEventData
            {
                eventId = id,
                eventType = type,
                intensity = (float)Clamp01(0.35d + (cue.Confidence * 0.65d)),
                telegraphLeadSeconds = TelegraphLead(type, difficulty, random)
            };
        }

        private static InputRequirementData CreateRequirement(
            string id,
            RhythmActionMask actions,
            InputGestureType gesture,
            double targetTime)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = actions,
                gestureType = gesture,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = targetTime,
                perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds,
                goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds,
                exclusive = true
            };
        }

        private static EncounterTargetData CreateTarget(
            string id,
            string requirementId,
            EnemyArchetype archetype)
        {
            return new EncounterTargetData
            {
                targetId = id,
                requirementId = requirementId,
                archetype = archetype
            };
        }

        private static RhythmAction ChooseAction(
            CombatStyle style,
            PlannedCue cue,
            StableRandom random,
            bool allowHeavy)
        {
            int guard;
            int dodge;
            int light;
            int heavy;
            GetWeights(style, out guard, out dodge, out light, out heavy);
            if (!allowHeavy)
            {
                heavy = 0;
            }

            if ((cue.Bands & AudioBandMask.High) != 0)
            {
                dodge += 8;
            }
            if ((cue.Bands & (AudioBandMask.Low | AudioBandMask.Mid)) != 0)
            {
                light += 6;
            }

            int value = random.NextInt(guard + dodge + light + heavy);
            if (value < guard)
            {
                return RhythmAction.Guard;
            }
            value -= guard;
            if (value < dodge)
            {
                return RhythmAction.Dodge;
            }
            value -= dodge;
            if (value < light)
            {
                return RhythmAction.LightAttack;
            }
            return RhythmAction.HeavyAttack;
        }

        private static void GetWeights(
            CombatStyle style,
            out int guard,
            out int dodge,
            out int light,
            out int heavy)
        {
            switch (style)
            {
                case CombatStyle.Legacy:
                    guard = 50; dodge = 0; light = 50; heavy = 0;
                    break;
                case CombatStyle.Defensive:
                    guard = 40; dodge = 30; light = 20; heavy = 10;
                    break;
                case CombatStyle.Aggressive:
                    guard = 15; dodge = 10; light = 50; heavy = 25;
                    break;
                case CombatStyle.Bursty:
                    guard = 20; dodge = 15; light = 35; heavy = 30;
                    break;
                default:
                    guard = 25; dodge = 20; light = 35; heavy = 20;
                    break;
            }
        }

        private static int HeavyWeight(CombatStyle style)
        {
            GetWeights(style, out _, out _, out _, out int heavy);
            return heavy;
        }

        private static double SweepChance(CombatStyle style, PlannedCue cue)
        {
            double chance = style == CombatStyle.Bursty ? 0.38d : style == CombatStyle.Aggressive ? 0.16d : 0.08d;
            return cue.Confidence >= 0.75d ? chance + 0.08d : chance;
        }

        private static EnemyArchetype ArchetypeForAction(RhythmAction action)
        {
            switch (action)
            {
                case RhythmAction.Guard:
                    return EnemyArchetype.Duelist;
                case RhythmAction.Dodge:
                    return EnemyArchetype.Brute;
                case RhythmAction.HeavyAttack:
                    return EnemyArchetype.Armored;
                default:
                    return EnemyArchetype.Raider;
            }
        }

        private static double TelegraphLead(
            RadialEventType type,
            BeatMapDifficulty difficulty,
            StableRandom random)
        {
            double minimum;
            double maximum;
            switch (type)
            {
                case RadialEventType.GuardHold:
                    minimum = 0.95d; maximum = 1.20d; break;
                case RadialEventType.HeavyChargeRelease:
                    minimum = 1.05d; maximum = 1.35d; break;
                case RadialEventType.Chord:
                case RadialEventType.Choice:
                case RadialEventType.OrderedSequence:
                    minimum = 0.95d; maximum = 1.25d; break;
                case RadialEventType.TimedChain:
                case RadialEventType.SwarmChain:
                    minimum = 1.00d; maximum = 1.30d; break;
                case RadialEventType.BreakTarget:
                    minimum = 1.25d; maximum = 1.60d; break;
                default:
                    minimum = 0.75d; maximum = 1.00d; break;
            }

            double difficultyOffset = difficulty == BeatMapDifficulty.Easy
                ? 0.08d
                : difficulty == BeatMapDifficulty.Hard ? -0.06d : 0d;
            return Math.Max(0.70d, Lerp(minimum, maximum, random.NextDouble()) + difficultyOffset);
        }

        private static bool CanCreateHold(
            RadialAudioAnalysisResult analysis,
            List<PlannedCue> cues,
            int index,
            BeatMapDifficulty difficulty)
        {
            PlannedCue cue = cues[index];
            SongSectionData section = analysis.sections[cue.SectionIndex];
            if (section.activityLevel != SongSectionActivityLevel.Active
                && section.activityLevel != SongSectionActivityLevel.Peak)
            {
                return false;
            }

            double next = index + 1 < cues.Count ? cues[index + 1].TimeSeconds : section.endTimeSeconds;
            return next - cue.TimeSeconds >= (difficulty == BeatMapDifficulty.Easy ? 1.05d : 0.90d)
                && section.endTimeSeconds - cue.TimeSeconds >= 0.55d;
        }

        private static bool IsLowOrMidEmphasis(PlannedCue cue)
        {
            return (cue.Bands & (AudioBandMask.Low | AudioBandMask.Mid)) != 0;
        }

        private static int CountCluster(List<PlannedCue> cues, int start, double span, int maximum)
        {
            int count = 1;
            while (start + count < cues.Count
                && count < maximum
                && cues[start + count].SectionIndex == cues[start].SectionIndex
                && cues[start + count].TimeSeconds - cues[start].TimeSeconds <= span)
            {
                count++;
            }
            return count;
        }

        private static List<OnsetCandidateData> GetSectionCandidates(
            RadialAudioAnalysisResult analysis,
            SongSectionData section)
        {
            List<OnsetCandidateData> result = new List<OnsetCandidateData>();
            if (analysis.onsetCandidates == null)
            {
                return result;
            }
            for (int i = 0; i < analysis.onsetCandidates.Count; i++)
            {
                OnsetCandidateData candidate = analysis.onsetCandidates[i];
                if (candidate != null
                    && candidate.timeSeconds >= section.startTimeSeconds
                    && candidate.timeSeconds < section.endTimeSeconds)
                {
                    result.Add(candidate);
                }
            }
            return result;
        }

        private static List<double> CollectGridTimes(RadialAudioAnalysisResult analysis)
        {
            List<double> result = new List<double>();
            if (analysis.beatGrid == null)
            {
                return result;
            }
            AddGridTimes(result, analysis.beatGrid.beatTimesSeconds);
            AddGridTimes(result, analysis.beatGrid.subdivisionTimesSeconds);
            result.Sort();
            for (int i = result.Count - 1; i > 0; i--)
            {
                if (Math.Abs(result[i] - result[i - 1]) <= TimeTolerance)
                {
                    result.RemoveAt(i);
                }
            }
            return result;
        }

        private static void AddGridTimes(List<double> destination, List<double> source)
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

        private static void FillLargeGaps(
            List<PlannedCue> selected,
            List<double> gridTimes,
            SongSectionData section,
            int sectionIndex,
            BeatMapDifficulty difficulty,
            int targetCost)
        {
            double maxGap = PlannerRules.MaximumGap(difficulty);
            bool added;
            do
            {
                selected.Sort(CompareCueTime);
                double gapStart = section.startTimeSeconds;
                double largestGap = 0d;
                double desired = 0d;
                for (int i = 0; i <= selected.Count; i++)
                {
                    double gapEnd = i < selected.Count ? selected[i].TimeSeconds : section.endTimeSeconds;
                    if (gapEnd - gapStart > largestGap)
                    {
                        largestGap = gapEnd - gapStart;
                        desired = (gapStart + gapEnd) * 0.5d;
                    }
                    gapStart = gapEnd;
                }

                added = false;
                if (largestGap > maxGap + TimeTolerance && selected.Count < targetCost)
                {
                    int gridIndex = FindNearestUsableGrid(
                        gridTimes,
                        desired,
                        section,
                        selected,
                        difficulty);
                    if (gridIndex >= 0)
                    {
                        selected.Add(CreateGridCue(gridTimes[gridIndex], sectionIndex));
                        added = true;
                    }
                }
            }
            while (added);
        }

        private static void FillBudget(
            List<PlannedCue> selected,
            List<double> gridTimes,
            SongSectionData section,
            int sectionIndex,
            BeatMapDifficulty difficulty,
            int targetCost,
            uint seed)
        {
            StableRandom random = new StableRandom(seed);
            List<double> candidates = new List<double>();
            for (int i = 0; i < gridTimes.Count; i++)
            {
                double time = gridTimes[i];
                if (time >= section.startTimeSeconds && time < section.endTimeSeconds)
                {
                    candidates.Add(time);
                }
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = random.NextInt(i + 1);
                double temporary = candidates[i];
                candidates[i] = candidates[swap];
                candidates[swap] = temporary;
            }
            for (int i = 0; i < candidates.Count && selected.Count < targetCost; i++)
            {
                if (CanAddCue(selected, candidates[i], difficulty))
                {
                    selected.Add(CreateGridCue(candidates[i], sectionIndex));
                }
            }
        }

        private static PlannedCue CreateGridCue(double time, int sectionIndex)
        {
            return new PlannedCue
            {
                TimeSeconds = time,
                Confidence = 0.25d,
                Bands = AudioBandMask.None,
                IsGridFill = true,
                SectionIndex = sectionIndex
            };
        }

        private static int FindNearestUsableGrid(
            List<double> gridTimes,
            double desired,
            SongSectionData section,
            List<PlannedCue> selected,
            BeatMapDifficulty difficulty)
        {
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < gridTimes.Count; i++)
            {
                double time = gridTimes[i];
                if (time < section.startTimeSeconds
                    || time >= section.endTimeSeconds
                    || !CanAddCue(selected, time, difficulty))
                {
                    continue;
                }
                double distance = Math.Abs(time - desired);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static bool CanAddCue(
            List<PlannedCue> existing,
            double time,
            BeatMapDifficulty difficulty)
        {
            double recovery = PlannerRules.MinimumRecovery(difficulty);
            double maximumCost = PlannerRules.MaximumDensity(difficulty) * 5d;
            int windowCost = 0;
            for (int i = 0; i < existing.Count; i++)
            {
                double distance = Math.Abs(existing[i].TimeSeconds - time);
                if (distance + TimeTolerance < recovery)
                {
                    return false;
                }
                if (existing[i].TimeSeconds >= time - 2.5d
                    && existing[i].TimeSeconds <= time + 2.5d)
                {
                    windowCost++;
                }
            }
            return windowCost + 1 <= Math.Ceiling(maximumCost);
        }

        private static bool IsGridUsable(
            RadialAudioAnalysisResult analysis,
            SongSectionData section)
        {
            double confidence = Math.Max(
                section.gridConfidence,
                analysis.beatGrid != null ? analysis.beatGrid.gridConfidence : 0d);
            if (confidence < 0.25d)
            {
                return false;
            }
            if (analysis.warnings != null)
            {
                for (int i = 0; i < analysis.warnings.Count; i++)
                {
                    if (string.Equals(
                        analysis.warnings[i],
                        "Insufficient Signal",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static string EventId(PlannedCue cue, int index)
        {
            return (cue.IsGridFill ? "grid-" : "onset-") + index.ToString("D5");
        }

        private static int CompareCandidatePriority(OnsetCandidateData left, OnsetCandidateData right)
        {
            int confidence = right.confidence.CompareTo(left.confidence);
            return confidence != 0 ? confidence : left.timeSeconds.CompareTo(right.timeSeconds);
        }

        private static int CompareCueTime(PlannedCue left, PlannedCue right)
        {
            return left.TimeSeconds.CompareTo(right.TimeSeconds);
        }

        private static int CompareEncounterTime(RadialEncounterEventData left, RadialEncounterEventData right)
        {
            return FirstRequirementTime(left).CompareTo(FirstRequirementTime(right));
        }

        internal static double FirstRequirementTime(RadialEncounterEventData encounter)
        {
            double result = double.MaxValue;
            if (encounter != null && encounter.requirements != null)
            {
                for (int i = 0; i < encounter.requirements.Count; i++)
                {
                    if (encounter.requirements[i] != null)
                    {
                        result = Math.Min(result, encounter.requirements[i].targetTimeSeconds);
                    }
                }
            }
            return result;
        }

        internal static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return hash == 0u ? 0x9E3779B9u : hash;
            }
        }

        internal static double Clamp01(double value)
        {
            return value < 0d ? 0d : value > 1d ? 1d : value;
        }

        private static double Lerp(double start, double end, double amount)
        {
            return start + ((end - start) * Clamp01(amount));
        }
    }

    internal sealed class StableRandom
    {
        private uint state;

        public StableRandom(uint seed)
        {
            state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public int NextInt(int maximumExclusive)
        {
            return maximumExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maximumExclusive);
        }

        public double NextDouble()
        {
            return (NextUInt() & 0x00FFFFFFu) / 16777216d;
        }
    }

    internal sealed class DirectionPlanner
    {
        private readonly StableRandom random;
        private int cursor;
        private RadialDirection previous;
        private RadialDirection beforePrevious;
        private int assignedEventCount;

        public DirectionPlanner(uint seed)
        {
            random = new StableRandom(seed);
            cursor = random.NextInt(8);
        }

        public void Assign(List<RadialEncounterEventData> encounters)
        {
            for (int eventIndex = 0; eventIndex < encounters.Count; eventIndex++)
            {
                RadialEncounterEventData encounter = encounters[eventIndex];
                if (encounter.targets == null || encounter.targets.Count == 0)
                {
                    continue;
                }

                int baseDirection = NextBaseDirection();
                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    int step;
                    if (encounter.eventType == RadialEventType.Sweep)
                    {
                        step = (targetIndex * 8) / encounter.targets.Count;
                    }
                    else
                    {
                        step = targetIndex == 0 ? 0 : 2 + ((targetIndex - 1) * 2);
                    }
                    encounter.targets[targetIndex].direction = (RadialDirection)((baseDirection + step) % 8);
                }

                beforePrevious = previous;
                previous = (RadialDirection)baseDirection;
                assignedEventCount++;
            }
        }

        private int NextBaseDirection()
        {
            int increment = 1 + random.NextInt(3);
            cursor = (cursor + increment) % 8;
            if (assignedEventCount >= 2
                && previous == beforePrevious
                && cursor == (int)previous)
            {
                cursor = (cursor + 2) % 8;
            }
            return cursor;
        }
    }
}
