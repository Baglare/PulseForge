using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialCombatPresentationController : MonoBehaviour
    {
        private const double CleanupSeconds = 0.52d;
        private const double ResolveMotionSeconds = 0.24d;
        private const double ForecastTransitionSeconds = 0.18d;
        private const double GroupTimingResolveHoldSeconds = 0.20d;
        private static readonly RhythmActionMask[] OrderedActionMasks =
        {
            RhythmActionMask.Guard,
            RhythmActionMask.Dodge,
            RhythmActionMask.LightAttack,
            RhythmActionMask.HeavyAttack
        };

        [SerializeField] private RadialCombatStageView stageView;

        private readonly HashSet<RadialPresentationKey> desiredEncounterKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> desiredProjectileKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> desiredCompoundKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> desiredForecastKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> desiredGroupTimingKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> revealedTargetKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<RadialPresentationKey> revealedForecastKeys =
            new HashSet<RadialPresentationKey>();
        private readonly HashSet<string> focusedEncounterIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CuePriorityCandidate> priorityCandidates =
            new List<CuePriorityCandidate>();
        private readonly List<PresentationWindow> presentationWindows =
            new List<PresentationWindow>();
        private readonly float[] directionEmphasis = new float[8];
        private DebugRhythmPrototypeController boundController;
        private IReadOnlyList<RadialEncounterRuntime> preparedEncounters;
        private RadialStatusEffectSnapshot currentStatus;
        private RadialActionBindingDisplay bindingDisplay;
        private int firstPresentationWindowIndex;
        private double lastPresentationSongTime = double.NaN;
        private float coreReaction;
        private float reactiveEventIntensity;

        public RadialCombatStageView StageView => stageView;

        public void Configure(RadialCombatStageView value)
        {
            stageView = value;
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (boundController == controller)
            {
                Refresh(controller);
                return;
            }

            Unbind();
            boundController = controller;
            if (boundController != null)
            {
                boundController.GameplaySessionRestarted += HandleSessionRestarted;
            }
            PrepareCurrentSession();
        }

        public void Unbind()
        {
            if (boundController != null)
            {
                boundController.GameplaySessionRestarted -= HandleSessionRestarted;
            }
            boundController = null;
            preparedEncounters = null;
            presentationWindows.Clear();
            firstPresentationWindowIndex = 0;
            lastPresentationSongTime = double.NaN;
            desiredEncounterKeys.Clear();
            desiredProjectileKeys.Clear();
            desiredCompoundKeys.Clear();
            desiredForecastKeys.Clear();
            desiredGroupTimingKeys.Clear();
            revealedTargetKeys.Clear();
            revealedForecastKeys.Clear();
            focusedEncounterIds.Clear();
            priorityCandidates.Clear();
            if (stageView != null)
            {
                stageView.SetRadialSessionVisible(false);
            }
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            if (controller != boundController)
            {
                Bind(controller);
                return;
            }
            if (stageView == null || boundController == null)
            {
                return;
            }

            bool usesRadialStage = boundController.UsesRadialCombatPresentation;
            stageView.SetRadialSessionVisible(usesRadialStage);
            stageView.SetUIStateVisibility(boundController.UIState);
            if (!usesRadialStage)
            {
                stageView.RenderBeatPulse(default, false);
                stageView.RenderReactivePolish(default(RadialReactiveVisual), 0f, false);
                ClearForecastPresentation();
                ClearGroupTimingPresentation();
                preparedEncounters = null;
                return;
            }

            IReadOnlyList<RadialEncounterRuntime> encounters =
                boundController.RadialPresentationEncounters;
            if (!ReferenceEquals(preparedEncounters, encounters))
            {
                Prepare(encounters);
            }

            PulseForgeUIState state = boundController.UIState;
            if (state != PulseForgeUIState.Countdown
                && state != PulseForgeUIState.Playing
                && state != PulseForgeUIState.Paused
                && state != PulseForgeUIState.Completed
                && state != PulseForgeUIState.Failed)
            {
                stageView.RenderBeatPulse(default, false);
                stageView.RenderReactivePolish(default(RadialReactiveVisual), 0f, false);
                ClearForecastPresentation();
                ClearGroupTimingPresentation();
                return;
            }

            double songTimeSeconds = boundController.CurrentSongTimeSeconds;
            stageView.RenderBeatPulse(
                RadialPresentationMath.EvaluateBeatPulse(
                    boundController.ActiveBeatGridForPresentation,
                    songTimeSeconds),
                boundController.BeatPulseEnabledForPresentation);
            Render(encounters, songTimeSeconds);
            if (state == PulseForgeUIState.Completed || state == PulseForgeUIState.Failed)
            {
                ClearForecastPresentation();
                ClearGroupTimingPresentation();
            }
        }

        public void ResetPresentation()
        {
            desiredEncounterKeys.Clear();
            desiredProjectileKeys.Clear();
            desiredCompoundKeys.Clear();
            desiredForecastKeys.Clear();
            desiredGroupTimingKeys.Clear();
            revealedTargetKeys.Clear();
            revealedForecastKeys.Clear();
            focusedEncounterIds.Clear();
            priorityCandidates.Clear();
            firstPresentationWindowIndex = 0;
            lastPresentationSongTime = double.NaN;
            currentStatus = default(RadialStatusEffectSnapshot);
            coreReaction = 0f;
            reactiveEventIntensity = 0f;
            stageView?.ResetPresentation();
        }

        private void ClearForecastPresentation()
        {
            desiredForecastKeys.Clear();
            revealedForecastKeys.Clear();
            stageView?.ReleaseForecastsExcept(desiredForecastKeys);
        }

        private void ClearGroupTimingPresentation()
        {
            desiredGroupTimingKeys.Clear();
            stageView?.ReleaseGroupTimingsExcept(desiredGroupTimingKeys);
        }

        private void HandleSessionRestarted()
        {
            PrepareCurrentSession();
        }

        private void PrepareCurrentSession()
        {
            if (stageView == null || boundController == null)
            {
                return;
            }
            bool usesRadialStage = boundController.UsesRadialCombatPresentation;
            stageView.SetRadialSessionVisible(usesRadialStage);
            Prepare(usesRadialStage
                ? boundController.RadialPresentationEncounters
                : null);
        }

        private void Prepare(IReadOnlyList<RadialEncounterRuntime> encounters)
        {
            preparedEncounters = encounters;
            ResetPresentation();
            if (encounters == null || stageView == null)
            {
                presentationWindows.Clear();
                return;
            }

            bindingDisplay = new RadialActionBindingDisplay(
                boundController.GetActiveBindingDisplay(RhythmAction.Guard),
                boundController.GetActiveBindingDisplay(RhythmAction.Dodge),
                boundController.GetActiveBindingDisplay(RhythmAction.LightAttack),
                boundController.GetActiveBindingDisplay(RhythmAction.HeavyAttack));

            int difficultyLevel = GetDifficultyLevel();
            float forecastLeadMultiplier = GetForecastLeadMultiplier();
            BuildPresentationWindows(
                encounters,
                difficultyLevel,
                forecastLeadMultiplier);

            stageView.InitializePools(
                CalculateMaximumVisibleCount(encounters, false),
                CalculateMaximumVisibleCount(encounters, true),
                CalculateMaximumCompoundVisibleCount(encounters),
                CalculateMaximumForecastVisibleCount(
                    encounters,
                    difficultyLevel,
                    forecastLeadMultiplier),
                CalculateMaximumGroupTimingVisibleCount(encounters));
        }

        private void Render(
            IReadOnlyList<RadialEncounterRuntime> encounters,
            double songTimeSeconds)
        {
            desiredEncounterKeys.Clear();
            desiredProjectileKeys.Clear();
            desiredCompoundKeys.Clear();
            desiredForecastKeys.Clear();
            desiredGroupTimingKeys.Clear();
            Array.Clear(directionEmphasis, 0, directionEmphasis.Length);
            coreReaction = 0f;
            reactiveEventIntensity = 0f;
            stageView.BeginCombatVfxFrame();
            currentStatus = boundController == null
                ? default(RadialStatusEffectSnapshot)
                : boundController.RadialStatusForPresentation;
            RadialReadabilityMode readabilityMode = GetReadabilityMode();
            int difficultyLevel = GetDifficultyLevel();
            stageView.ApplyReadabilityMode(readabilityMode);
            stageView.RenderFog(currentStatus, songTimeSeconds);
            if (encounters == null)
            {
                stageView.ReleaseEncountersExcept(desiredEncounterKeys);
                stageView.ReleaseProjectilesExcept(desiredProjectileKeys);
                stageView.ReleaseCompoundsExcept(desiredCompoundKeys);
                stageView.ReleaseForecastsExcept(desiredForecastKeys);
                stageView.ReleaseGroupTimingsExcept(desiredGroupTimingKeys);
                stageView.EndCombatVfxFrame();
                stageView.RenderDirectionEmphasis(directionEmphasis);
                RenderReactivePolish(songTimeSeconds, readabilityMode);
                return;
            }

            GetActivePresentationRange(
                songTimeSeconds,
                out int firstWindowIndex,
                out int endWindowIndex);
            BuildFocusedEncounterSet(
                songTimeSeconds,
                difficultyLevel,
                firstWindowIndex,
                endWindowIndex);

            for (int windowIndex = firstWindowIndex;
                windowIndex < endWindowIndex;
                windowIndex++)
            {
                PresentationWindow window = presentationWindows[windowIndex];
                if (songTimeSeconds > window.EndTimeSeconds)
                {
                    continue;
                }
                RadialEncounterRuntime encounter = window.Encounter;
                reactiveEventIntensity = Mathf.Max(
                    reactiveEventIntensity,
                    Mathf.Clamp01(encounter.Data.intensity));
                double encounterResolutionTime = GetEncounterResolutionTime(encounter);
                double cleanupTime = encounter.IsResolved
                    ? encounterResolutionTime + CleanupSeconds
                    : double.PositiveInfinity;
                for (int targetIndex = 0; targetIndex < encounter.Targets.Count; targetIndex++)
                {
                    EncounterTargetRuntime target = encounter.Targets[targetIndex];
                    InputRequirementRuntime requirement = FindRequirement(
                        encounter,
                        target.Data.requirementId);
                    if (requirement == null)
                    {
                        continue;
                    }

                    RenderForecast(
                        encounter,
                        target,
                        requirement,
                        songTimeSeconds,
                        difficultyLevel,
                        readabilityMode);

                    double targetCleanupTime = cleanupTime;
                    if (encounter.Data.eventType == RadialEventType.SwarmChain
                        && requirement.IsResolved
                        && requirement.Result != null)
                    {
                        targetCleanupTime = requirement.Result.ResolutionTimeSeconds + CleanupSeconds;
                    }

                    RenderTarget(
                        encounter,
                        target,
                        requirement,
                        targetIndex,
                        songTimeSeconds,
                        targetCleanupTime);
                }

                RenderCompoundGroup(encounter, songTimeSeconds, cleanupTime);
                RenderGroupTiming(encounter, songTimeSeconds, cleanupTime);
            }

            stageView.ReleaseEncountersExcept(desiredEncounterKeys);
            stageView.ReleaseProjectilesExcept(desiredProjectileKeys);
            stageView.ReleaseCompoundsExcept(desiredCompoundKeys);
            stageView.ReleaseForecastsExcept(desiredForecastKeys);
            stageView.ReleaseGroupTimingsExcept(desiredGroupTimingKeys);
            stageView.EndCombatVfxFrame();
            stageView.RenderDirectionEmphasis(directionEmphasis);
            RenderReactivePolish(songTimeSeconds, readabilityMode);
        }

        private void RenderTarget(
            RadialEncounterRuntime encounter,
            EncounterTargetRuntime target,
            InputRequirementRuntime requirement,
            int targetIndex,
            double songTimeSeconds,
            double cleanupTime)
        {
            InputRequirementData requirementData = requirement.Data;
            EncounterTargetData targetData = target.Data;
            double baseRevealTime = RadialPresentationMath.EvaluateActionLayerStart(
                requirementData.targetTimeSeconds,
                encounter.Data.telegraphLeadSeconds);
            RadialPresentationKey key = new RadialPresentationKey(
                encounter.Data.eventId,
                targetData.targetId,
                targetData.requirementId);
            if (songTimeSeconds > cleanupTime
                || !RadialPresentationMath.ShouldBeVisible(
                    revealedTargetKeys.Contains(key),
                    songTimeSeconds,
                    baseRevealTime))
            {
                return;
            }
            revealedTargetKeys.Add(key);
            desiredEncounterKeys.Add(key);
            if (!stageView.TryAcquireEncounter(key, out RadialEncounterView encounterView))
            {
                return;
            }
            if (!encounterView.Key.Equals(key))
            {
                encounterView.Activate(
                    key,
                    targetData.archetype,
                    requirementData.acceptedActions,
                    bindingDisplay);
            }

            bool isRanged = targetData.archetype == EnemyArchetype.ArcherGunner;
            bool isSaboteur = targetData.archetype == EnemyArchetype.Saboteur;
            RadialPresentationResultState resultState =
                encounter.Data.eventType == RadialEventType.HeavyChargeRelease
                    ? ResolveEncounterResultState(encounter)
                    : ResolveResultState(target, requirement);
            RadialCuePriority targetCuePriority = ResolveTargetCuePriority(
                encounter,
                requirement,
                resultState);
            RadialCompoundTargetState compoundState = BuildTargetState(
                encounter,
                requirement,
                targetIndex,
                songTimeSeconds);
            Vector2 position;
            bool bodyVisible = true;
            bool exclamationVisible = false;
            if (isRanged)
            {
                RadialRangedTimeline timeline = RadialPresentationMath.CreateRangedTimeline(
                    requirementData.targetTimeSeconds,
                    encounter.Data.telegraphLeadSeconds);
                position = RadialPresentationMath.DirectionVector(targetData.direction)
                    * stageView.OuterRadius;
                bodyVisible = songTimeSeconds >= timeline.SpawnTimeSeconds;
                exclamationVisible = songTimeSeconds >= timeline.RevealTimeSeconds
                    && songTimeSeconds < timeline.SpawnTimeSeconds;
                RenderProjectile(
                    key,
                    targetData,
                    requirement,
                    timeline,
                    songTimeSeconds,
                    cleanupTime,
                    resultState,
                    compoundState,
                    targetCuePriority);
            }
            else if (isSaboteur)
            {
                position = RadialPresentationMath.DirectionVector(targetData.direction)
                    * stageView.OuterRadius;
            }
            else
            {
                position = RadialPresentationMath.EvaluateApproachPosition(
                    targetData.direction,
                    songTimeSeconds,
                    baseRevealTime,
                    requirementData.targetTimeSeconds,
                    stageView.OuterRadius,
                    stageView.JudgementRadius);
            }

            int directionIndex = (int)targetData.direction;
            if (directionIndex >= 0 && directionIndex < directionEmphasis.Length)
            {
                directionEmphasis[directionIndex] = Mathf.Max(
                    directionEmphasis[directionIndex],
                    bodyVisible ? 1f : 0.62f);
            }

            float scale = 1f;
            InputRequirementRuntime motionRequirement =
                encounter.Data.eventType == RadialEventType.HeavyChargeRelease
                    ? FindRequirementByPhase(encounter, RhythmInputPhase.Released) ?? requirement
                    : requirement;
            ApplyResolutionMotion(
                targetData.direction,
                motionRequirement,
                songTimeSeconds,
                resultState,
                encounter.Data.eventType == RadialEventType.HeavyChargeRelease
                    || encounter.Data.eventType == RadialEventType.BreakTarget,
                ref position,
                ref scale);
            RenderCombatEffects(
                key,
                encounter,
                targetData,
                motionRequirement,
                resultState,
                position,
                songTimeSeconds);
            encounterView.Render(
                position,
                scale,
                bodyVisible,
                exclamationVisible,
                resultState);
            encounterView.ApplyCompoundState(compoundState, requirementData.acceptedActions);
            InputRequirementRuntime individualTimingRequirement =
                encounter.Data.eventType == RadialEventType.HeavyChargeRelease
                    ? FindRequirementByPhase(encounter, RhythmInputPhase.Pressed) ?? requirement
                    : requirement;
            InputRequirementData individualTimingData = individualTimingRequirement.Data;
            bool showTimingWindow = resultState == RadialPresentationResultState.Pending
                && bodyVisible
                && RadialCompoundPresentationMath.ShouldShowIndividualTiming(
                    encounter.Data.eventType,
                    individualTimingData.gestureType,
                    individualTimingData.phase,
                    individualTimingRequirement.HasCapturedInput);
            if (TryGetInputOpportunity(
                encounter,
                individualTimingRequirement,
                songTimeSeconds,
                difficultyLevel: GetDifficultyLevel(),
                out InputOpportunitySnapshot opportunity))
            {
                encounterView.RenderTimingWindow(
                    targetData.direction,
                    opportunity,
                    showTimingWindow);
            }
            else
            {
                encounterView.RenderTimingWindow(
                    targetData.direction,
                    default(InputOpportunitySnapshot),
                    false);
            }
            encounterView.ApplyCuePriority(
                targetCuePriority,
                GetReadabilityMode(),
                RadialPresentationMath.EvaluateProgress(
                    songTimeSeconds,
                    baseRevealTime,
                    baseRevealTime + ForecastTransitionSeconds));
            if (isSaboteur)
            {
                bool failed = resultState == RadialPresentationResultState.Miss
                    || resultState == RadialPresentationResultState.WrongInput;
                encounterView.RenderSaboteur(
                    RadialPresentationMath.EvaluateProgress(
                        songTimeSeconds,
                        baseRevealTime,
                        requirementData.targetTimeSeconds),
                    failed);
                if (failed && requirement.Result != null)
                {
                    stageView.ShowSaboteurSmoke(
                        encounter.Data.eventId,
                        requirement.Result.ResolutionTimeSeconds);
                }
            }
        }

        private void RenderProjectile(
            RadialPresentationKey key,
            EncounterTargetData target,
            InputRequirementRuntime requirement,
            RadialRangedTimeline timeline,
            double songTimeSeconds,
            double cleanupTime,
            RadialPresentationResultState resultState,
            RadialCompoundTargetState compoundState,
            RadialCuePriority cuePriority)
        {
            if (songTimeSeconds < timeline.FireTimeSeconds || songTimeSeconds > cleanupTime)
            {
                return;
            }

            desiredProjectileKeys.Add(key);
            if (!stageView.TryAcquireProjectile(key, out RadialProjectileView projectileView))
            {
                return;
            }
            if (!projectileView.Key.Equals(key))
            {
                projectileView.Activate(key, requirement.Data.acceptedActions, bindingDisplay);
            }
            Vector2 position = RadialPresentationMath.EvaluateProjectilePosition(
                target.direction,
                songTimeSeconds,
                timeline.FireTimeSeconds,
                timeline.TargetTimeSeconds,
                stageView.OuterRadius - 34f,
                stageView.JudgementRadius);
            projectileView.ApplyCuePriority(cuePriority, GetReadabilityMode());
            RequirementResult result = requirement.Result;
            float reactionProgress = result == null || !IsMotionEnabled()
                ? 0f
                : RadialPresentationMath.EvaluateProgress(
                    songTimeSeconds,
                    result.ResolutionTimeSeconds,
                    result.ResolutionTimeSeconds + RadialVfxTokens.ProjectileReactionDuration);
            projectileView.Render(
                position,
                target.direction,
                resultState,
                result == null ? (RhythmAction?)null : result.Action,
                reactionProgress);
            projectileView.ApplyCompoundState(compoundState, requirement.Data.acceptedActions);
        }

        private void RenderCombatEffects(
            RadialPresentationKey targetKey,
            RadialEncounterRuntime encounter,
            EncounterTargetData target,
            InputRequirementRuntime requirement,
            RadialPresentationResultState resultState,
            Vector2 targetPosition,
            double songTimeSeconds)
        {
            if (!IsMotionEnabled())
            {
                return;
            }

            RadialPresentationKey effectKey = new RadialPresentationKey(
                targetKey.EventId,
                targetKey.TargetId,
                requirement.Data.requirementId);
            float eventIntensity = Mathf.Clamp01(encounter.Data.intensity);
            float clarity = GetReadabilityMode() == RadialReadabilityMode.HighClarity
                ? RadialVfxTokens.HighClarityVfxMultiplier
                : 1f;
            float baseIntensity = Mathf.Clamp01((0.68f + eventIntensity * 0.32f) * clarity);
            float directionAngle = DirectionAngle(target.direction);

            if (encounter.Data.eventType == RadialEventType.BreakTarget)
            {
                InputRequirementRuntime repeated = FindRequirementByGesture(
                    encounter,
                    InputGestureType.RepeatedPress);
                if (repeated != null && repeated.AcceptedPressCount > 0)
                {
                    bool finalSegment = repeated.AcceptedPressCount
                        >= repeated.Data.requiredPressCount;
                    RenderCombatCue(
                        new RadialCombatVfxKey(
                            effectKey,
                            finalSegment
                                ? RadialCombatVfxKind.BreakFinal
                                : RadialCombatVfxKind.BreakSegment,
                            repeated.AcceptedPressCount),
                        targetPosition,
                        directionAngle,
                        repeated.LastAcceptedPressTimeSeconds,
                        finalSegment
                            ? RadialVfxTokens.HeavyEffectDuration
                            : RadialVfxTokens.BreakSegmentDuration,
                        finalSegment ? 1.14f : 0.82f,
                        baseIntensity,
                        songTimeSeconds);
                    if (finalSegment && HasHeavyFinisher(encounter))
                    {
                        RenderCombatCue(
                            new RadialCombatVfxKey(
                                effectKey,
                                RadialCombatVfxKind.HeavyImpact,
                                repeated.AcceptedPressCount),
                            targetPosition,
                            directionAngle,
                            repeated.LastAcceptedPressTimeSeconds,
                            RadialVfxTokens.HeavyEffectDuration,
                            RadialVfxTokens.HeavyEffectScale,
                            baseIntensity,
                            songTimeSeconds);
                    }
                }
            }

            RequirementResult result = requirement.Result;
            if (result == null)
            {
                return;
            }

            float gradeMultiplier = result.Grade == HitGrade.Perfect
                ? RadialVfxTokens.PerfectMultiplier
                : result.Grade == HitGrade.Good
                    ? RadialVfxTokens.GoodMultiplier
                    : RadialVfxTokens.MissReaction;
            float intensity = Mathf.Clamp01(baseIntensity * gradeMultiplier);
            bool missed = resultState == RadialPresentationResultState.Miss
                || resultState == RadialPresentationResultState.WrongInput
                || result.Grade == HitGrade.Miss;
            if (missed)
            {
                RenderCombatCue(
                    new RadialCombatVfxKey(effectKey, RadialCombatVfxKind.MissImpact),
                    Vector2.zero,
                    directionAngle,
                    result.ResolutionTimeSeconds,
                    RadialVfxTokens.CombatEffectDuration,
                    0.92f,
                    intensity,
                    songTimeSeconds);
                if (songTimeSeconds >= result.ResolutionTimeSeconds
                    && songTimeSeconds <= result.ResolutionTimeSeconds
                        + RadialVfxTokens.CombatEffectDuration)
                {
                    float missProgress = RadialPresentationMath.EvaluateProgress(
                        songTimeSeconds,
                        result.ResolutionTimeSeconds,
                        result.ResolutionTimeSeconds + RadialVfxTokens.CombatEffectDuration);
                    coreReaction = Mathf.Max(
                        coreReaction,
                        Mathf.Sin(missProgress * Mathf.PI) * RadialVfxTokens.MissReaction);
                }
                return;
            }

            switch (result.Action)
            {
                case RhythmAction.Guard:
                    RenderCombatCue(
                        new RadialCombatVfxKey(effectKey, RadialCombatVfxKind.GuardArc),
                        Vector2.zero,
                        directionAngle,
                        result.ResolutionTimeSeconds,
                        RadialVfxTokens.CombatEffectDuration,
                        RadialVfxTokens.GuardEffectScale,
                        intensity,
                        songTimeSeconds);
                    RenderCombatCue(
                        new RadialCombatVfxKey(effectKey, RadialCombatVfxKind.CoreBurst),
                        Vector2.zero,
                        directionAngle,
                        result.ResolutionTimeSeconds,
                        RadialVfxTokens.CombatEffectDuration,
                        0.82f,
                        intensity,
                        songTimeSeconds);
                    if (target.archetype == EnemyArchetype.ArcherGunner)
                    {
                        RenderCombatCue(
                            new RadialCombatVfxKey(
                                effectKey,
                                RadialCombatVfxKind.ProjectileDeflect),
                            Vector2.zero,
                            directionAngle,
                            result.ResolutionTimeSeconds,
                            RadialVfxTokens.ProjectileReactionDuration,
                            0.92f,
                            intensity,
                            songTimeSeconds);
                    }
                    break;
                case RhythmAction.Dodge:
                    RenderCombatCue(
                        new RadialCombatVfxKey(
                            effectKey,
                            RadialCombatVfxKind.DodgeAfterimage),
                        Vector2.zero,
                        directionAngle,
                        result.ResolutionTimeSeconds,
                        RadialVfxTokens.CombatEffectDuration,
                        RadialVfxTokens.DodgeEffectScale,
                        intensity,
                        songTimeSeconds);
                    break;
                case RhythmAction.LightAttack:
                    RenderCombatCue(
                        new RadialCombatVfxKey(effectKey, RadialCombatVfxKind.LightSlash),
                        targetPosition,
                        directionAngle,
                        result.ResolutionTimeSeconds,
                        RadialVfxTokens.CombatEffectDuration,
                        RadialVfxTokens.LightEffectScale,
                        intensity,
                        songTimeSeconds);
                    break;
                case RhythmAction.HeavyAttack:
                    RenderCombatCue(
                        new RadialCombatVfxKey(effectKey, RadialCombatVfxKind.HeavyImpact),
                        targetPosition,
                        directionAngle,
                        result.ResolutionTimeSeconds,
                        RadialVfxTokens.HeavyEffectDuration,
                        RadialVfxTokens.HeavyEffectScale,
                        intensity,
                        songTimeSeconds);
                    if (target.archetype == EnemyArchetype.Armored
                        || target.archetype == EnemyArchetype.GiantBreaker)
                    {
                        RenderCombatCue(
                            new RadialCombatVfxKey(
                                effectKey,
                                RadialCombatVfxKind.ArmorBreak),
                            targetPosition,
                            directionAngle,
                            result.ResolutionTimeSeconds,
                            RadialVfxTokens.HeavyEffectDuration,
                            1.06f,
                            intensity,
                            songTimeSeconds);
                    }
                    break;
            }
        }

        private void RenderCombatCue(
            RadialCombatVfxKey key,
            Vector2 position,
            float rotationDegrees,
            double startTimeSeconds,
            float durationSeconds,
            float scale,
            float intensity,
            double songTimeSeconds)
        {
            stageView.RenderCombatVfx(
                new RadialCombatVfxCue(
                    key,
                    position,
                    rotationDegrees,
                    startTimeSeconds,
                    durationSeconds,
                    scale,
                    intensity),
                songTimeSeconds);
        }

        private void RenderReactivePolish(
            double songTimeSeconds,
            RadialReadabilityMode readabilityMode)
        {
            bool motionEnabled = IsMotionEnabled();
            stageView.RenderReactivePolish(
                RadialReactivePresentationMath.Evaluate(
                    boundController == null
                        ? null
                        : boundController.ActiveBeatGridForPresentation,
                    songTimeSeconds,
                    reactiveEventIntensity,
                    motionEnabled
                        && boundController != null
                        && boundController.BeatPulseEnabledForPresentation,
                    readabilityMode == RadialReadabilityMode.HighClarity),
                coreReaction,
                motionEnabled);
        }

        private bool IsMotionEnabled()
        {
            return boundController == null || boundController.MotionEnabledSetting;
        }

        private static float DirectionAngle(RadialDirection direction)
        {
            Vector2 vector = RadialPresentationMath.DirectionVector(direction);
            return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        }

        private void RenderForecast(
            RadialEncounterRuntime encounter,
            EncounterTargetRuntime target,
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            int difficultyLevel,
            RadialReadabilityMode readabilityMode)
        {
            if (requirement.IsResolved)
            {
                return;
            }

            InputRequirementData requirementData = requirement.Data;
            EncounterTargetData targetData = target.Data;
            RadialPresentationKey key = new RadialPresentationKey(
                encounter.Data.eventId,
                targetData.targetId,
                targetData.requirementId);
            double actionLayerStart = RadialPresentationMath.EvaluateActionLayerStart(
                requirementData.targetTimeSeconds,
                encounter.Data.telegraphLeadSeconds);
            double forecastReveal = RadialPresentationMath.EvaluateForecastRevealTime(
                requirementData.targetTimeSeconds,
                difficultyLevel,
                encounter.Data.eventType,
                GetForecastLeadMultiplier(),
                currentStatus);
            bool wasVisible = revealedForecastKeys.Contains(key);
            if (!RadialPresentationMath.ShouldBeVisible(
                    wasVisible,
                    songTimeSeconds,
                    forecastReveal)
                || songTimeSeconds >= actionLayerStart + ForecastTransitionSeconds)
            {
                return;
            }

            revealedForecastKeys.Add(key);
            desiredForecastKeys.Add(key);
            int directionIndex = (int)targetData.direction;
            if (directionIndex >= 0 && directionIndex < directionEmphasis.Length)
            {
                directionEmphasis[directionIndex] = Mathf.Max(
                    directionEmphasis[directionIndex],
                    0.34f);
            }
            if (!stageView.TryAcquireForecast(key, out RadialForecastView forecastView))
            {
                return;
            }
            if (!forecastView.Key.Equals(key))
            {
                forecastView.Activate(
                    key,
                    targetData.direction,
                    requirementData.acceptedActions,
                    encounter.Data.eventType);
            }

            float transitionAlpha = songTimeSeconds < actionLayerStart
                ? 1f
                : RadialPresentationMath.EvaluateForecastFade(
                    songTimeSeconds,
                    actionLayerStart,
                    ForecastTransitionSeconds);
            forecastView.Render(
                RadialPresentationMath.DirectionVector(targetData.direction)
                    * stageView.OuterRadius,
                readabilityMode,
                transitionAlpha);
        }

        private void RenderCompoundGroup(
            RadialEncounterRuntime encounter,
            double songTimeSeconds,
            double cleanupTime)
        {
            if (!UsesCompoundGroup(encounter.Data.eventType)
                || songTimeSeconds > cleanupTime)
            {
                return;
            }

            Vector2 center = Vector2.zero;
            int visibleTargetCount = 0;
            for (int i = 0; i < encounter.Targets.Count; i++)
            {
                EncounterTargetRuntime target = encounter.Targets[i];
                if (TryGetTargetPosition(encounter, target, songTimeSeconds, out Vector2 position))
                {
                    center += position;
                    visibleTargetCount++;
                }
            }
            if (visibleTargetCount == 0)
            {
                return;
            }

            center /= visibleTargetCount;
            Vector2 indicatorPosition = GetIndicatorPosition(center);
            RadialPresentationKey key = new RadialPresentationKey(
                encounter.Data.eventId,
                "compound",
                "group");
            desiredCompoundKeys.Add(key);
            if (!stageView.TryAcquireCompound(key, out RadialCompoundGroupView groupView))
            {
                return;
            }
            if (!groupView.Key.Equals(key))
            {
                groupView.Activate(key, bindingDisplay);
            }

            bool failed = HasFailedRequirement(encounter);
            RadialCompoundLinkState groupState = ResolveGroupState(encounter, failed);
            RadialCompoundLinkState linkState = encounter.Data.eventType == RadialEventType.OrderedSequence
                && failed
                    ? RadialCompoundLinkState.Partial
                    : groupState;
            int linkCount = RenderGroupLinks(
                groupView,
                encounter,
                songTimeSeconds,
                linkState);
            groupView.HideLinksFrom(linkCount);

            switch (encounter.Data.eventType)
            {
                case RadialEventType.Chord:
                    InputRequirementRuntime first = encounter.Requirements.Count > 0
                        ? encounter.Requirements[0]
                        : null;
                    InputRequirementRuntime second = encounter.Requirements.Count > 1
                        ? encounter.Requirements[1]
                        : null;
                    RadialCompoundLinkState chordState =
                        RadialCompoundPresentationMath.EvaluateChordState(
                            first != null && first.HasCapturedInput,
                            second != null && second.HasCapturedInput,
                            failed);
                    groupView.RenderChord(
                        indicatorPosition,
                        first == null ? RhythmActionMask.None : first.Data.acceptedActions,
                        second == null ? RhythmActionMask.None : second.Data.acceptedActions,
                        chordState);
                    break;
                case RadialEventType.OrderedSequence:
                    int activeStep = RadialCompoundPresentationMath.FindActiveStepIndex(
                        encounter.Requirements);
                    groupView.RenderSequence(
                        indicatorPosition,
                        activeStep,
                        encounter.Requirements.Count,
                        CountSuccessfulRequirements(encounter),
                        activeStep >= 0 && activeStep < encounter.Requirements.Count
                            ? encounter.Requirements[activeStep].Data.acceptedActions
                            : RhythmActionMask.None,
                        false);
                    break;
                case RadialEventType.TimedChain:
                    InputRequirementRuntime activeChain =
                        FindActiveGroupTimingRequirement(encounter);
                    groupView.RenderChain(
                        indicatorPosition,
                        false,
                        CountUnresolvedRequirements(encounter),
                        encounter.Requirements.Count,
                        activeChain == null
                            ? RhythmActionMask.None
                            : activeChain.Data.acceptedActions,
                        failed);
                    break;
                case RadialEventType.SwarmChain:
                    InputRequirementRuntime activeSwarm =
                        FindActiveGroupTimingRequirement(encounter);
                    groupView.RenderChain(
                        indicatorPosition,
                        true,
                        RadialCompoundPresentationMath.CountRemainingTargets(encounter.Targets),
                        encounter.Targets.Count,
                        activeSwarm == null
                            ? RhythmActionMask.LightAttack
                            : activeSwarm.Data.acceptedActions,
                        failed);
                    break;
                case RadialEventType.Sweep:
                    InputRequirementRuntime sweep = encounter.Requirements.Count > 0
                        ? encounter.Requirements[0]
                        : null;
                    groupView.RenderSweep(
                        indicatorPosition,
                        sweep == null ? RhythmActionMask.None : sweep.Data.acceptedActions,
                        encounter.Targets.Count,
                        groupState,
                        sweep == null
                            ? RadialPresentationResultState.Pending
                            : ResolveRequirementResultState(sweep));
                    break;
            }
            groupView.ApplyCuePriority(
                ResolveCuePriority(
                    encounter,
                    ResolveEncounterResultState(encounter)),
                GetReadabilityMode());
        }

        private void RenderGroupTiming(
            RadialEncounterRuntime encounter,
            double songTimeSeconds,
            double cleanupTime)
        {
            if (!RadialCompoundPresentationMath.UsesGroupTiming(encounter.Data.eventType)
                || songTimeSeconds > cleanupTime)
            {
                return;
            }

            bool resolvedLinger = encounter.IsResolved
                && songTimeSeconds <= GetEncounterResolutionTime(encounter)
                    + GroupTimingResolveHoldSeconds;
            if (encounter.IsResolved && !resolvedLinger)
            {
                return;
            }

            InputRequirementRuntime requirement = encounter.IsResolved
                ? FindLastResolvedRequirement(encounter)
                : FindActiveGroupTimingRequirement(encounter);
            if (requirement == null)
            {
                return;
            }

            double actionLayerStart = RadialPresentationMath.EvaluateActionLayerStart(
                requirement.Data.targetTimeSeconds,
                encounter.Data.telegraphLeadSeconds);
            if (songTimeSeconds < actionLayerStart
                || !TryGetGroupTimingPosition(
                    encounter,
                    requirement,
                    songTimeSeconds,
                    out Vector2 position))
            {
                return;
            }

            bool followsActiveStep = encounter.Data.eventType == RadialEventType.OrderedSequence
                || encounter.Data.eventType == RadialEventType.TimedChain
                || encounter.Data.eventType == RadialEventType.SwarmChain;
            string timingGroupId = encounter.Data.eventType == RadialEventType.Chord
                ? "chord"
                : followsActiveStep ? "active" : requirement.Data.requirementId;
            RadialPresentationKey key = new RadialPresentationKey(
                encounter.Data.eventId,
                "group-timing",
                timingGroupId);
            desiredGroupTimingKeys.Add(key);
            if (!stageView.TryAcquireGroupTiming(key, out RadialGroupTimingView groupTimingView))
            {
                return;
            }
            if (!groupTimingView.Key.Equals(key))
            {
                groupTimingView.Activate(key);
            }

            if (!TryGetInputOpportunity(
                encounter,
                requirement,
                songTimeSeconds,
                GetDifficultyLevel(),
                out InputOpportunitySnapshot opportunity))
            {
                return;
            }
            groupTimingView.Render(
                position,
                opportunity,
                BuildGroupTimingContent(encounter, requirement),
                ResolveCuePriority(
                    encounter,
                    ResolveEncounterResultState(encounter)),
                GetReadabilityMode(),
                bindingDisplay);
        }

        private bool TryGetGroupTimingPosition(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            out Vector2 position)
        {
            Vector2 center = Vector2.zero;
            int count = 0;
            for (int pass = 0; pass < 2 && count == 0; pass++)
            {
                for (int i = 0; i < encounter.Targets.Count; i++)
                {
                    EncounterTargetRuntime target = encounter.Targets[i];
                    bool belongsToRequirement = string.Equals(
                        target.Data.requirementId,
                        requirement.Data.requirementId,
                        StringComparison.Ordinal);
                    if (pass == 0
                        && encounter.Data.eventType != RadialEventType.Chord
                        && !belongsToRequirement)
                    {
                        continue;
                    }
                    if (TryGetTargetPosition(
                        encounter,
                        target,
                        songTimeSeconds,
                        out Vector2 targetPosition))
                    {
                        center += targetPosition;
                        count++;
                    }
                }
            }

            if (count == 0)
            {
                position = Vector2.zero;
                return false;
            }

            center /= count;
            if (center.sqrMagnitude < 2500f)
            {
                position = new Vector2(0f, stageView.JudgementRadius + 92f);
                return true;
            }
            float radius = Mathf.Min(
                Mathf.Max(
                    center.magnitude - 72f,
                    stageView.JudgementRadius + 58f),
                stageView.OuterRadius - 82f);
            position = center.normalized * radius;
            return true;
        }

        private static InputRequirementRuntime FindActiveGroupTimingRequirement(
            RadialEncounterRuntime encounter)
        {
            if (encounter == null || encounter.Requirements.Count == 0)
            {
                return null;
            }
            if (encounter.Data.eventType == RadialEventType.Chord)
            {
                return encounter.Requirements[0];
            }
            int activeIndex = RadialCompoundPresentationMath.FindActiveStepIndex(
                encounter.Requirements);
            return activeIndex < 0 ? null : encounter.Requirements[activeIndex];
        }

        private static InputRequirementRuntime FindLastResolvedRequirement(
            RadialEncounterRuntime encounter)
        {
            InputRequirementRuntime latest = null;
            double latestTime = double.MinValue;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                InputRequirementRuntime requirement = encounter.Requirements[i];
                if (requirement.Result != null
                    && requirement.Result.ResolutionTimeSeconds >= latestTime)
                {
                    latest = requirement;
                    latestTime = requirement.Result.ResolutionTimeSeconds;
                }
            }
            return latest;
        }

        private static RadialGroupTimingContent BuildGroupTimingContent(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime activeRequirement)
        {
            switch (encounter.Data.eventType)
            {
                case RadialEventType.Chord:
                    InputRequirementRuntime first = encounter.Requirements.Count > 0
                        ? encounter.Requirements[0]
                        : null;
                    InputRequirementRuntime second = encounter.Requirements.Count > 1
                        ? encounter.Requirements[1]
                        : null;
                    int chordCompleted = CountCapturedOrSuccessful(first)
                        + CountCapturedOrSuccessful(second);
                    return new RadialGroupTimingContent(
                        first == null ? RhythmActionMask.None : first.Data.acceptedActions,
                        second == null ? RhythmActionMask.None : second.Data.acceptedActions,
                        "+",
                        ResolveGroupActionState(first),
                        ResolveGroupActionState(second),
                        RadialGroupProgressKind.Chord,
                        chordCompleted,
                        2,
                        RadialCompoundPresentationMath.EvaluateGroupProgress(
                            chordCompleted,
                            2));
                case RadialEventType.Choice:
                    SplitActions(
                        activeRequirement.Data.acceptedActions,
                        out RhythmActionMask firstChoice,
                        out RhythmActionMask secondChoice);
                    RhythmAction? selected =
                        RadialCompoundPresentationMath.GetSelectedChoiceAction(
                            activeRequirement);
                    RhythmActionMask selectedMask = selected.HasValue
                        ? RhythmActionMaskUtility.ToMask(selected.Value)
                        : RhythmActionMask.None;
                    return new RadialGroupTimingContent(
                        firstChoice,
                        secondChoice,
                        "/",
                        ResolveChoiceActionState(firstChoice, selectedMask),
                        ResolveChoiceActionState(secondChoice, selectedMask),
                        RadialGroupProgressKind.Choice,
                        selected.HasValue ? 1 : 0,
                        1,
                        selected.HasValue ? 1f : 0f);
                case RadialEventType.OrderedSequence:
                    int activeStep = FindRequirementIndex(encounter, activeRequirement);
                    int successfulSteps = CountSuccessfulRequirements(encounter);
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.Step,
                        activeStep + 1,
                        encounter.Requirements.Count,
                        RadialCompoundPresentationMath.EvaluateGroupProgress(
                            successfulSteps,
                            encounter.Requirements.Count));
                case RadialEventType.TimedChain:
                    int chainRemaining = CountUnresolvedRequirements(encounter);
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.Chain,
                        chainRemaining,
                        encounter.Requirements.Count,
                        RadialCompoundPresentationMath.EvaluateGroupProgress(
                            encounter.Requirements.Count - chainRemaining,
                            encounter.Requirements.Count));
                case RadialEventType.SwarmChain:
                    int swarmRemaining = RadialCompoundPresentationMath.CountRemainingTargets(
                        encounter.Targets);
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.Swarm,
                        swarmRemaining,
                        encounter.Targets.Count,
                        RadialCompoundPresentationMath.EvaluateGroupProgress(
                            encounter.Targets.Count - swarmRemaining,
                            encounter.Targets.Count));
                case RadialEventType.BreakTarget:
                    if (activeRequirement.Data.gestureType != InputGestureType.RepeatedPress)
                    {
                        return SingleActionContent(
                            activeRequirement,
                            RadialGroupProgressKind.HeavyFinisher,
                            0,
                            1,
                            activeRequirement.IsResolved ? 1f : 0f);
                    }
                    int required = Math.Max(1, activeRequirement.Data.requiredPressCount);
                    int remaining = RadialCompoundPresentationMath.RemainingBreakSegments(
                        required,
                        activeRequirement.AcceptedPressCount);
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.BreakTarget,
                        remaining,
                        required,
                        RadialCompoundPresentationMath.EvaluateGroupProgress(
                            activeRequirement.AcceptedPressCount,
                            required));
                case RadialEventType.Sweep:
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.Sweep,
                        activeRequirement.IsResolved ? 1 : 0,
                        encounter.Targets.Count,
                        activeRequirement.IsResolved ? 1f : 0f);
                default:
                    return SingleActionContent(
                        activeRequirement,
                        RadialGroupProgressKind.None,
                        0,
                        0,
                        0f);
            }
        }

        private static RadialGroupTimingContent SingleActionContent(
            InputRequirementRuntime requirement,
            RadialGroupProgressKind progressKind,
            int currentValue,
            int totalValue,
            float progress)
        {
            return new RadialGroupTimingContent(
                requirement.Data.acceptedActions,
                RhythmActionMask.None,
                string.Empty,
                ResolveGroupActionState(requirement),
                RadialGroupActionState.Normal,
                progressKind,
                currentValue,
                totalValue,
                progress);
        }

        private static RadialGroupActionState ResolveGroupActionState(
            InputRequirementRuntime requirement)
        {
            if (requirement == null)
            {
                return RadialGroupActionState.Muted;
            }
            if (requirement.Result != null)
            {
                return requirement.Result.Grade == HitGrade.Miss
                    ? RadialGroupActionState.Muted
                    : RadialGroupActionState.Highlighted;
            }
            return requirement.HasCapturedInput
                ? RadialGroupActionState.Highlighted
                : RadialGroupActionState.Normal;
        }

        private static RadialGroupActionState ResolveChoiceActionState(
            RhythmActionMask action,
            RhythmActionMask selected)
        {
            if (selected == RhythmActionMask.None)
            {
                return RadialGroupActionState.Normal;
            }
            return action == selected
                ? RadialGroupActionState.Highlighted
                : RadialGroupActionState.Muted;
        }

        private static int CountCapturedOrSuccessful(InputRequirementRuntime requirement)
        {
            if (requirement == null)
            {
                return 0;
            }
            return requirement.HasCapturedInput
                || (requirement.Result != null && requirement.Result.Grade != HitGrade.Miss)
                ? 1
                : 0;
        }

        private static void SplitActions(
            RhythmActionMask actions,
            out RhythmActionMask first,
            out RhythmActionMask second)
        {
            first = RhythmActionMask.None;
            second = RhythmActionMask.None;
            for (int i = 0; i < OrderedActionMasks.Length; i++)
            {
                if ((actions & OrderedActionMasks[i]) == 0)
                {
                    continue;
                }
                if (first == RhythmActionMask.None)
                {
                    first = OrderedActionMasks[i];
                }
                else
                {
                    second = OrderedActionMasks[i];
                    return;
                }
            }
        }

        private RadialCompoundTargetState BuildTargetState(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            int targetIndex,
            double songTimeSeconds)
        {
            RadialCompoundTargetKind kind = ResolveTargetKind(encounter.Data.eventType);
            int requirementIndex = FindRequirementIndex(encounter, requirement);
            int activeStep = RadialCompoundPresentationMath.FindActiveStepIndex(
                encounter.Requirements);
            bool failed = requirement.Result != null
                && requirement.Result.Grade == HitGrade.Miss;
            bool pressed = requirement.AcceptedPressCount > 0
                || (requirement.HasCapturedInput
                    && requirement.CapturedPhase == RhythmInputPhase.Pressed);
            bool released = (requirement.Result != null
                    && requirement.Result.Phase == RhythmInputPhase.Released)
                || (requirement.HasCapturedInput
                    && requirement.CapturedPhase == RhythmInputPhase.Released);
            bool held = false;
            float progress = 0f;
            bool earlyFailure = requirement.Result != null
                && requirement.Result.Reason == RadialResultReason.EarlyRelease;
            bool lateFailure = false;
            RhythmAction? selectedAction = null;
            int repeatCount = requirement.AcceptedPressCount;
            int requiredRepeatCount = requirement.Data.requiredPressCount;
            bool hasHeavyFinisher = false;

            if (encounter.Data.eventType == RadialEventType.GuardHold)
            {
                pressed = requirement.HasCapturedInput;
                held = pressed
                    && !requirement.IsResolved
                    && boundController != null
                    && boundController.IsRadialActionHeldForPresentation(
                        requirement.CapturedAction);
                progress = RadialCompoundPresentationMath.EvaluateHoldProgress(
                    songTimeSeconds,
                    requirement.Data.targetTimeSeconds,
                    requirement.Data.holdEndTimeSeconds,
                    pressed);
            }
            else if (encounter.Data.eventType == RadialEventType.HeavyChargeRelease)
            {
                InputRequirementRuntime pressRequirement = FindRequirementByPhase(
                    encounter,
                    RhythmInputPhase.Pressed);
                InputRequirementRuntime releaseRequirement = FindRequirementByPhase(
                    encounter,
                    RhythmInputPhase.Released);
                pressed = pressRequirement != null && pressRequirement.HasCapturedInput;
                released = releaseRequirement != null && releaseRequirement.IsResolved;
                held = pressed
                    && !released
                    && boundController != null
                    && boundController.IsRadialActionHeldForPresentation(RhythmAction.HeavyAttack);
                progress = RadialCompoundPresentationMath.EvaluateChargeProgress(
                    songTimeSeconds,
                    pressRequirement == null
                        ? requirement.Data.targetTimeSeconds
                        : pressRequirement.Data.targetTimeSeconds,
                    releaseRequirement == null
                        ? requirement.Data.targetTimeSeconds
                        : releaseRequirement.Data.targetTimeSeconds,
                    pressed);
                failed = HasFailedRequirement(encounter);
                if (releaseRequirement != null && releaseRequirement.Result != null && failed)
                {
                    double releaseTime = releaseRequirement.Result.ResolutionTimeSeconds;
                    earlyFailure = releaseTime < releaseRequirement.Data.targetTimeSeconds;
                    lateFailure = !earlyFailure;
                }
            }
            else if (encounter.Data.eventType == RadialEventType.Choice)
            {
                selectedAction = RadialCompoundPresentationMath.GetSelectedChoiceAction(requirement);
            }
            else if (encounter.Data.eventType == RadialEventType.BreakTarget)
            {
                InputRequirementRuntime repeated = FindRequirementByGesture(
                    encounter,
                    InputGestureType.RepeatedPress);
                if (repeated != null)
                {
                    repeatCount = repeated.AcceptedPressCount;
                    requiredRepeatCount = repeated.Data.requiredPressCount;
                    failed = repeated.Result != null
                        && repeated.Result.Grade == HitGrade.Miss;
                }
                hasHeavyFinisher = HasHeavyFinisher(encounter);
            }

            bool isStepEvent = encounter.Data.eventType == RadialEventType.OrderedSequence
                || encounter.Data.eventType == RadialEventType.TimedChain
                || encounter.Data.eventType == RadialEventType.SwarmChain;
            return new RadialCompoundTargetState(
                kind,
                progress,
                pressed,
                held,
                released,
                isStepEvent && requirementIndex == activeStep,
                isStepEvent && requirement.IsResolved && !failed,
                failed,
                earlyFailure,
                lateFailure,
                requirementIndex >= 0 ? requirementIndex : targetIndex,
                encounter.Requirements.Count,
                repeatCount,
                requiredRepeatCount,
                hasHeavyFinisher,
                selectedAction,
                encounter.Data.eventType != RadialEventType.Sweep);
        }

        private bool TryGetTargetPosition(
            RadialEncounterRuntime encounter,
            EncounterTargetRuntime target,
            double songTimeSeconds,
            out Vector2 position)
        {
            InputRequirementRuntime requirement = FindRequirement(
                encounter,
                target.Data.requirementId);
            if (requirement == null)
            {
                position = Vector2.zero;
                return false;
            }
            double baseRevealTime = RadialPresentationMath.EvaluateActionLayerStart(
                requirement.Data.targetTimeSeconds,
                encounter.Data.telegraphLeadSeconds);
            RadialPresentationKey key = new RadialPresentationKey(
                encounter.Data.eventId,
                target.Data.targetId,
                target.Data.requirementId);
            if (!RadialPresentationMath.ShouldBeVisible(
                revealedTargetKeys.Contains(key),
                songTimeSeconds,
                baseRevealTime))
            {
                position = Vector2.zero;
                return false;
            }
            if (target.Data.archetype == EnemyArchetype.ArcherGunner
                || target.Data.archetype == EnemyArchetype.Saboteur)
            {
                position = RadialPresentationMath.DirectionVector(target.Data.direction)
                    * stageView.OuterRadius;
                return true;
            }

            position = RadialPresentationMath.EvaluateApproachPosition(
                target.Data.direction,
                songTimeSeconds,
                baseRevealTime,
                requirement.Data.targetTimeSeconds,
                stageView.OuterRadius,
                stageView.JudgementRadius);
            return true;
        }

        private int RenderGroupLinks(
            RadialCompoundGroupView groupView,
            RadialEncounterRuntime encounter,
            double songTimeSeconds,
            RadialCompoundLinkState state)
        {
            bool hasPrevious = false;
            Vector2 previous = Vector2.zero;
            int linkIndex = 0;
            for (int i = 0; i < encounter.Targets.Count; i++)
            {
                EncounterTargetRuntime target = encounter.Targets[i];
                if (encounter.Data.eventType == RadialEventType.SwarmChain
                    && target.IsResolved)
                {
                    continue;
                }
                if (!TryGetTargetPosition(encounter, target, songTimeSeconds, out Vector2 position))
                {
                    continue;
                }
                if (hasPrevious)
                {
                    groupView.SetLink(linkIndex, previous, position, state);
                    linkIndex++;
                }
                previous = position;
                hasPrevious = true;
            }
            return linkIndex;
        }

        private Vector2 GetIndicatorPosition(Vector2 center)
        {
            if (center.sqrMagnitude < 2500f)
            {
                return new Vector2(0f, stageView.JudgementRadius * 0.62f);
            }
            return center.normalized * Mathf.Min(
                center.magnitude,
                stageView.JudgementRadius + 78f);
        }

        private static void ApplyResolutionMotion(
            RadialDirection direction,
            InputRequirementRuntime requirement,
            double songTimeSeconds,
            RadialPresentationResultState resultState,
            bool strongResolve,
            ref Vector2 position,
            ref float scale)
        {
            if (!requirement.IsResolved || requirement.Result == null)
            {
                return;
            }

            float progress = RadialPresentationMath.EvaluateProgress(
                songTimeSeconds,
                requirement.Result.ResolutionTimeSeconds,
                requirement.Result.ResolutionTimeSeconds + ResolveMotionSeconds);
            Vector2 directionVector = RadialPresentationMath.DirectionVector(direction);
            if (resultState == RadialPresentationResultState.Miss
                || resultState == RadialPresentationResultState.WrongInput)
            {
                position = Vector2.Lerp(position, Vector2.zero, progress * 0.72f);
                scale = Mathf.Lerp(1.08f, 0.78f, progress);
                return;
            }

            float knockback = strongResolve
                ? resultState == RadialPresentationResultState.Perfect ? 108f : 78f
                : resultState == RadialPresentationResultState.Perfect ? 68f : 42f;
            position += directionVector * (knockback * progress);
            float resolvedScale = strongResolve
                ? resultState == RadialPresentationResultState.Perfect ? 0.30f : 0.46f
                : resultState == RadialPresentationResultState.Perfect ? 0.52f : 0.68f;
            scale = Mathf.Lerp(1f, resolvedScale, progress);
        }

        private static RadialPresentationResultState ResolveResultState(
            EncounterTargetRuntime target,
            InputRequirementRuntime requirement)
        {
            RadialResultReason reason = target.Result == null
                ? requirement.Result == null ? RadialResultReason.None : requirement.Result.Reason
                : target.Result.Reason;
            if (reason == RadialResultReason.WrongInput)
            {
                return RadialPresentationResultState.WrongInput;
            }

            HitGrade? grade = target.Result == null
                ? requirement.Result == null ? (HitGrade?)null : requirement.Result.Grade
                : target.Result.Grade;
            if (!grade.HasValue)
            {
                return RadialPresentationResultState.Pending;
            }
            switch (grade.Value)
            {
                case HitGrade.Perfect:
                    return RadialPresentationResultState.Perfect;
                case HitGrade.Good:
                    return RadialPresentationResultState.Good;
                case HitGrade.Miss:
                    return RadialPresentationResultState.Miss;
                default:
                    return RadialPresentationResultState.Resolved;
            }
        }

        private RadialCuePriority ResolveTargetCuePriority(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            RadialPresentationResultState resultState)
        {
            bool stepEvent = encounter.Data.eventType == RadialEventType.OrderedSequence
                || encounter.Data.eventType == RadialEventType.TimedChain
                || encounter.Data.eventType == RadialEventType.SwarmChain;
            if (stepEvent && resultState == RadialPresentationResultState.Pending)
            {
                int activeStep = RadialCompoundPresentationMath.FindActiveStepIndex(
                    encounter.Requirements);
                if (FindRequirementIndex(encounter, requirement) != activeStep)
                {
                    return RadialCuePriority.Upcoming;
                }
            }
            return ResolveCuePriority(encounter, resultState);
        }

        private static RadialPresentationResultState ResolveRequirementResultState(
            InputRequirementRuntime requirement)
        {
            if (requirement == null || requirement.Result == null)
            {
                return RadialPresentationResultState.Pending;
            }
            if (requirement.Result.Reason == RadialResultReason.WrongInput)
            {
                return RadialPresentationResultState.WrongInput;
            }
            switch (requirement.Result.Grade)
            {
                case HitGrade.Perfect:
                    return RadialPresentationResultState.Perfect;
                case HitGrade.Good:
                    return RadialPresentationResultState.Good;
                case HitGrade.Miss:
                    return RadialPresentationResultState.Miss;
                default:
                    return RadialPresentationResultState.Resolved;
            }
        }

        private static RadialPresentationResultState ResolveEncounterResultState(
            RadialEncounterRuntime encounter)
        {
            if (!encounter.IsResolved || encounter.Result == null)
            {
                return RadialPresentationResultState.Pending;
            }
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                RequirementResult result = encounter.Requirements[i].Result;
                if (result != null && result.Reason == RadialResultReason.WrongInput)
                {
                    return RadialPresentationResultState.WrongInput;
                }
            }
            switch (encounter.Result.Grade)
            {
                case HitGrade.Perfect:
                    return RadialPresentationResultState.Perfect;
                case HitGrade.Good:
                    return RadialPresentationResultState.Good;
                case HitGrade.Miss:
                    return RadialPresentationResultState.Miss;
                default:
                    return RadialPresentationResultState.Resolved;
            }
        }

        private static bool UsesCompoundGroup(RadialEventType eventType)
        {
            return eventType == RadialEventType.Chord
                || eventType == RadialEventType.OrderedSequence
                || eventType == RadialEventType.TimedChain
                || eventType == RadialEventType.SwarmChain
                || eventType == RadialEventType.Sweep;
        }

        private static RadialCompoundTargetKind ResolveTargetKind(RadialEventType eventType)
        {
            switch (eventType)
            {
                case RadialEventType.GuardHold:
                    return RadialCompoundTargetKind.GuardHold;
                case RadialEventType.HeavyChargeRelease:
                    return RadialCompoundTargetKind.HeavyCharge;
                case RadialEventType.Chord:
                    return RadialCompoundTargetKind.Chord;
                case RadialEventType.Choice:
                    return RadialCompoundTargetKind.Choice;
                case RadialEventType.OrderedSequence:
                    return RadialCompoundTargetKind.Sequence;
                case RadialEventType.TimedChain:
                    return RadialCompoundTargetKind.TimedChain;
                case RadialEventType.SwarmChain:
                    return RadialCompoundTargetKind.Swarm;
                case RadialEventType.BreakTarget:
                    return RadialCompoundTargetKind.BreakTarget;
                case RadialEventType.Sweep:
                    return RadialCompoundTargetKind.Sweep;
                default:
                    return RadialCompoundTargetKind.Tap;
            }
        }

        private static RadialCompoundLinkState ResolveGroupState(
            RadialEncounterRuntime encounter,
            bool failed)
        {
            if (failed)
            {
                return RadialCompoundLinkState.Failed;
            }
            if (encounter.IsResolved)
            {
                return RadialCompoundLinkState.Complete;
            }
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                InputRequirementRuntime requirement = encounter.Requirements[i];
                if (requirement.IsResolved
                    || requirement.HasCapturedInput
                    || requirement.AcceptedPressCount > 0)
                {
                    return RadialCompoundLinkState.Partial;
                }
            }
            return RadialCompoundLinkState.Pending;
        }

        private static bool HasFailedRequirement(RadialEncounterRuntime encounter)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                RequirementResult result = encounter.Requirements[i].Result;
                if (result != null && result.Grade == HitGrade.Miss)
                {
                    return true;
                }
            }
            return false;
        }

        private static int CountSuccessfulRequirements(RadialEncounterRuntime encounter)
        {
            int completed = 0;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                RequirementResult result = encounter.Requirements[i].Result;
                if (result != null && result.Grade != HitGrade.Miss)
                {
                    completed++;
                }
            }
            return completed;
        }

        private static int CountUnresolvedRequirements(RadialEncounterRuntime encounter)
        {
            int remaining = 0;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (!encounter.Requirements[i].IsResolved)
                {
                    remaining++;
                }
            }
            return remaining;
        }

        private static int FindRequirementIndex(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (ReferenceEquals(encounter.Requirements[i], requirement))
                {
                    return i;
                }
            }
            return -1;
        }

        private static InputRequirementRuntime FindRequirementByPhase(
            RadialEncounterRuntime encounter,
            RhythmInputPhase phase)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (encounter.Requirements[i].Data.phase == phase)
                {
                    return encounter.Requirements[i];
                }
            }
            return null;
        }

        private static InputRequirementRuntime FindRequirementByGesture(
            RadialEncounterRuntime encounter,
            InputGestureType gesture)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if (encounter.Requirements[i].Data.gestureType == gesture)
                {
                    return encounter.Requirements[i];
                }
            }
            return null;
        }

        private static bool HasHeavyFinisher(RadialEncounterRuntime encounter)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                if ((encounter.Requirements[i].Data.acceptedActions
                    & RhythmActionMask.HeavyAttack) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void BuildFocusedEncounterSet(
            double songTimeSeconds,
            int difficultyLevel,
            int firstWindowIndex,
            int endWindowIndex)
        {
            focusedEncounterIds.Clear();
            for (int windowIndex = firstWindowIndex;
                windowIndex < endWindowIndex;
                windowIndex++)
            {
                PresentationWindow window = presentationWindows[windowIndex];
                if (songTimeSeconds > window.EndTimeSeconds)
                {
                    continue;
                }
                RadialEncounterRuntime encounter = window.Encounter;
                if (encounter.IsResolved)
                {
                    continue;
                }

                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementRuntime requirement = encounter.Requirements[requirementIndex];
                    if (!requirement.IsResolved
                        && TryGetInputOpportunity(
                            encounter,
                            requirement,
                            songTimeSeconds,
                            difficultyLevel,
                            out InputOpportunitySnapshot snapshot)
                        && snapshot.Timing.FocusState == RadialTimingFocusState.Focused
                        && snapshot.Matchable)
                    {
                        focusedEncounterIds.Add(encounter.Data.eventId);
                        break;
                    }
                }
            }
        }

        private void InsertPriorityCandidate(CuePriorityCandidate candidate)
        {
            int insertIndex = priorityCandidates.Count;
            for (int i = 0; i < priorityCandidates.Count; i++)
            {
                CuePriorityCandidate existing = priorityCandidates[i];
                int timeComparison = candidate.TargetTimeSeconds.CompareTo(
                    existing.TargetTimeSeconds);
                if (timeComparison < 0
                    || (timeComparison == 0
                        && string.CompareOrdinal(candidate.EventId, existing.EventId) < 0))
                {
                    insertIndex = i;
                    break;
                }
            }
            priorityCandidates.Insert(insertIndex, candidate);
        }

        private RadialCuePriority ResolveCuePriority(
            RadialEncounterRuntime encounter,
            RadialPresentationResultState resultState)
        {
            if (resultState != RadialPresentationResultState.Pending
                || focusedEncounterIds.Contains(encounter.Data.eventId))
            {
                return RadialCuePriority.Focused;
            }
            return RadialCuePriority.Upcoming;
        }

        private int GetDifficultyLevel()
        {
            return boundController == null
                ? 1
                : boundController.RadialPresentationDifficultyLevel;
        }

        private float GetForecastLeadMultiplier()
        {
            return boundController == null
                ? 1.25f
                : boundController.ForecastLeadMultiplierForPresentation;
        }

        private RadialReadabilityMode GetReadabilityMode()
        {
            return boundController == null
                ? RadialReadabilityMode.Assisted
                : boundController.ReadabilityModeForPresentation;
        }

        private RadialTimingProfile GetTimingProfile()
        {
            return boundController == null
                ? RadialTimingProfile.FromMode(TimingAssistMode.Standard)
                : boundController.ActiveTimingProfileForPresentation;
        }

        private bool TryGetInputOpportunity(
            RadialEncounterRuntime encounter,
            InputRequirementRuntime requirement,
            double rawSongTimeSeconds,
            int difficultyLevel,
            out InputOpportunitySnapshot snapshot)
        {
            int focusedLimit = RadialPresentationMath.FocusedCueLimit(difficultyLevel);
            InputRequirementData data = requirement.Data;
            RhythmAction action;
            if (!RhythmActionMaskUtility.TryGetSingleAction(data.acceptedActions, out action))
            {
                action = (data.acceptedActions & RhythmActionMask.Guard) != 0
                    ? RhythmAction.Guard
                    : RhythmAction.LightAttack;
            }
            if (boundController != null)
            {
                RhythmAction requestedAction = requirement.HasCapturedInput
                    ? requirement.CapturedAction
                    : action;
                RhythmInputPhase requestedPhase = encounter.Data.eventType == RadialEventType.GuardHold
                    && requirement.HasCapturedInput
                        ? RhythmInputPhase.Released
                        : data.phase;
                return boundController.TryGetRadialInputOpportunity(
                    encounter.Data.eventId,
                    requirement.Data.requirementId,
                    requestedAction,
                    requestedPhase,
                    rawSongTimeSeconds,
                    focusedLimit,
                    out snapshot);
            }

            bool capturedHold = encounter.Data.eventType == RadialEventType.GuardHold
                && requirement.HasCapturedInput;
            RadialTimingSnapshot timing = new RadialTimingSnapshot(
                encounter.Data.eventId,
                data.requirementId,
                requirement.HasCapturedInput ? requirement.CapturedAction : action,
                capturedHold ? RhythmInputPhase.Released : data.phase,
                rawSongTimeSeconds,
                rawSongTimeSeconds,
                capturedHold ? data.holdEndTimeSeconds : data.targetTimeSeconds,
                GetTimingProfile(),
                0d,
                0d,
                requirement.IsResolved
                    ? RadialRequirementState.Resolved
                    : requirement.HasCapturedInput
                        ? RadialRequirementState.Captured
                        : RadialRequirementState.Pending,
                RadialTimingFocusState.Focused,
                encounter.Data.eventType == RadialEventType.BreakTarget
                    && data.gestureType == InputGestureType.RepeatedPress,
                data.windowStartTimeSeconds,
                data.perfectDeadlineSeconds,
                data.goodDeadlineSeconds);
            bool matchable = !requirement.IsResolved
                && Math.Abs(timing.DeltaMilliseconds)
                    <= timing.GoodWindowSeconds * 1000d + 0.000001d;
            snapshot = new InputOpportunitySnapshot(
                timing,
                data.orderIndex,
                matchable,
                matchable
                    ? InputOpportunityRejectionReason.None
                    : requirement.IsResolved
                        ? InputOpportunityRejectionReason.AlreadyResolved
                        : InputOpportunityRejectionReason.OutsideWindow);
            return true;
        }

        private static InputRequirementRuntime FindRequirement(
            RadialEncounterRuntime encounter,
            string requirementId)
        {
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                InputRequirementRuntime requirement = encounter.Requirements[i];
                if (string.Equals(
                    requirement.Data.requirementId,
                    requirementId,
                    StringComparison.Ordinal))
                {
                    return requirement;
                }
            }
            return encounter.Requirements.Count == 1 ? encounter.Requirements[0] : null;
        }

        private static double GetEncounterResolutionTime(RadialEncounterRuntime encounter)
        {
            double latest = 0d;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                RequirementResult result = encounter.Requirements[i].Result;
                if (result != null)
                {
                    latest = Math.Max(latest, result.ResolutionTimeSeconds);
                }
            }
            return latest;
        }

        private static int CalculateMaximumVisibleCount(
            IReadOnlyList<RadialEncounterRuntime> encounters,
            bool rangedOnly)
        {
            List<VisibilityEdge> edges = new List<VisibilityEdge>();
            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                double encounterEnd = GetEncounterPlannedEnd(encounter) + CleanupSeconds;
                for (int targetIndex = 0; targetIndex < encounter.Targets.Count; targetIndex++)
                {
                    EncounterTargetData target = encounter.Targets[targetIndex].Data;
                    if (rangedOnly && target.archetype != EnemyArchetype.ArcherGunner)
                    {
                        continue;
                    }
                    InputRequirementRuntime requirement = FindRequirement(
                        encounter,
                        target.requirementId);
                    if (requirement == null)
                    {
                        continue;
                    }
                    double start = requirement.Data.targetTimeSeconds
                        - Math.Max(0d, encounter.Data.telegraphLeadSeconds);
                    if (rangedOnly)
                    {
                        start = RadialPresentationMath.CreateRangedTimeline(
                            requirement.Data.targetTimeSeconds,
                            encounter.Data.telegraphLeadSeconds).FireTimeSeconds;
                    }
                    edges.Add(new VisibilityEdge(start, 1));
                    edges.Add(new VisibilityEdge(encounterEnd, -1));
                }
            }

            edges.Sort((left, right) =>
            {
                int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return time != 0 ? time : right.Delta.CompareTo(left.Delta);
            });
            int active = 0;
            int maximum = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                active += edges[i].Delta;
                maximum = Math.Max(maximum, active);
            }
            return maximum;
        }

        private static int CalculateMaximumCompoundVisibleCount(
            IReadOnlyList<RadialEncounterRuntime> encounters)
        {
            List<VisibilityEdge> edges = new List<VisibilityEdge>();
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = encounters[i];
                if (!UsesCompoundGroup(encounter.Data.eventType))
                {
                    continue;
                }
                double start = double.MaxValue;
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    start = Math.Min(
                        start,
                        encounter.Requirements[requirementIndex].Data.targetTimeSeconds
                            - Math.Max(0d, encounter.Data.telegraphLeadSeconds));
                }
                edges.Add(new VisibilityEdge(start, 1));
                edges.Add(new VisibilityEdge(
                    GetEncounterPlannedEnd(encounter) + CleanupSeconds,
                    -1));
            }

            edges.Sort((left, right) =>
            {
                int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return time != 0 ? time : right.Delta.CompareTo(left.Delta);
            });
            int active = 0;
            int maximum = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                active += edges[i].Delta;
                maximum = Math.Max(maximum, active);
            }
            return maximum;
        }

        private static int CalculateMaximumGroupTimingVisibleCount(
            IReadOnlyList<RadialEncounterRuntime> encounters)
        {
            List<VisibilityEdge> edges = new List<VisibilityEdge>();
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = encounters[i];
                if (!RadialCompoundPresentationMath.UsesGroupTiming(
                    encounter.Data.eventType))
                {
                    continue;
                }
                double start = double.MaxValue;
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementData requirement =
                        encounter.Requirements[requirementIndex].Data;
                    start = Math.Min(
                        start,
                        RadialPresentationMath.EvaluateActionLayerStart(
                            requirement.targetTimeSeconds,
                            encounter.Data.telegraphLeadSeconds));
                }
                edges.Add(new VisibilityEdge(start, 1));
                edges.Add(new VisibilityEdge(
                    GetEncounterPlannedEnd(encounter)
                        + GroupTimingResolveHoldSeconds,
                    -1));
            }

            edges.Sort((left, right) =>
            {
                int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return time != 0 ? time : right.Delta.CompareTo(left.Delta);
            });
            int active = 0;
            int maximum = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                active += edges[i].Delta;
                maximum = Math.Max(maximum, active);
            }
            return maximum;
        }

        private static int CalculateMaximumForecastVisibleCount(
            IReadOnlyList<RadialEncounterRuntime> encounters,
            int difficultyLevel,
            float leadMultiplier)
        {
            List<VisibilityEdge> edges = new List<VisibilityEdge>();
            for (int encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
            {
                RadialEncounterRuntime encounter = encounters[encounterIndex];
                double forecastLead = RadialPresentationMath.EvaluateForecastLeadSeconds(
                    difficultyLevel,
                    encounter.Data.eventType,
                    leadMultiplier);
                for (int targetIndex = 0; targetIndex < encounter.Targets.Count; targetIndex++)
                {
                    EncounterTargetData target = encounter.Targets[targetIndex].Data;
                    InputRequirementRuntime requirement = FindRequirement(
                        encounter,
                        target.requirementId);
                    if (requirement == null)
                    {
                        continue;
                    }
                    double start = requirement.Data.targetTimeSeconds - forecastLead;
                    double end = RadialPresentationMath.EvaluateActionLayerStart(
                        requirement.Data.targetTimeSeconds,
                        encounter.Data.telegraphLeadSeconds) + ForecastTransitionSeconds;
                    edges.Add(new VisibilityEdge(start, 1));
                    edges.Add(new VisibilityEdge(Math.Max(start, end), -1));
                }
            }

            edges.Sort((left, right) =>
            {
                int time = left.TimeSeconds.CompareTo(right.TimeSeconds);
                return time != 0 ? time : right.Delta.CompareTo(left.Delta);
            });
            int active = 0;
            int maximum = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                active += edges[i].Delta;
                maximum = Math.Max(maximum, active);
            }
            return maximum;
        }

        private static double GetEncounterPlannedEnd(RadialEncounterRuntime encounter)
        {
            double latest = 0d;
            for (int i = 0; i < encounter.Requirements.Count; i++)
            {
                InputRequirementData requirement = encounter.Requirements[i].Data;
                latest = Math.Max(latest, requirement.targetTimeSeconds + requirement.goodWindowSeconds);
                latest = Math.Max(latest, requirement.holdEndTimeSeconds);
                latest = Math.Max(latest, requirement.goodDeadlineSeconds);
            }
            return latest;
        }

        private void BuildPresentationWindows(
            IReadOnlyList<RadialEncounterRuntime> encounters,
            int difficultyLevel,
            float forecastLeadMultiplier)
        {
            presentationWindows.Clear();
            firstPresentationWindowIndex = 0;
            lastPresentationSongTime = double.NaN;
            for (int i = 0; i < encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = encounters[i];
                if (encounter == null || encounter.Requirements.Count == 0)
                {
                    continue;
                }

                double forecastLead = RadialPresentationMath.EvaluateForecastLeadSeconds(
                    difficultyLevel,
                    encounter.Data.eventType,
                    forecastLeadMultiplier);
                double start = double.PositiveInfinity;
                for (int requirementIndex = 0;
                    requirementIndex < encounter.Requirements.Count;
                    requirementIndex++)
                {
                    InputRequirementData requirement =
                        encounter.Requirements[requirementIndex].Data;
                    start = Math.Min(start, requirement.targetTimeSeconds - forecastLead);
                    start = Math.Min(
                        start,
                        RadialPresentationMath.EvaluateActionLayerStart(
                            requirement.targetTimeSeconds,
                            encounter.Data.telegraphLeadSeconds));
                }
                presentationWindows.Add(new PresentationWindow(
                    encounter,
                    start,
                    GetEncounterPlannedEnd(encounter) + CleanupSeconds));
            }
            presentationWindows.Sort((left, right) =>
            {
                int time = left.StartTimeSeconds.CompareTo(right.StartTimeSeconds);
                return time != 0
                    ? time
                    : string.CompareOrdinal(
                        left.Encounter.Data.eventId,
                        right.Encounter.Data.eventId);
            });
        }

        private void GetActivePresentationRange(
            double songTimeSeconds,
            out int firstIndex,
            out int endIndex)
        {
            if (!double.IsNaN(lastPresentationSongTime)
                && songTimeSeconds + 0.001d < lastPresentationSongTime)
            {
                firstPresentationWindowIndex = 0;
            }
            lastPresentationSongTime = songTimeSeconds;

            while (firstPresentationWindowIndex < presentationWindows.Count
                && presentationWindows[firstPresentationWindowIndex].EndTimeSeconds
                    < songTimeSeconds)
            {
                firstPresentationWindowIndex++;
            }
            firstIndex = firstPresentationWindowIndex;

            int low = firstIndex;
            int high = presentationWindows.Count;
            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                if (presentationWindows[middle].StartTimeSeconds <= songTimeSeconds)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }
            endIndex = low;
        }

        private readonly struct VisibilityEdge
        {
            public VisibilityEdge(double timeSeconds, int delta)
            {
                TimeSeconds = timeSeconds;
                Delta = delta;
            }

            public double TimeSeconds { get; }
            public int Delta { get; }
        }

        private readonly struct PresentationWindow
        {
            public PresentationWindow(
                RadialEncounterRuntime encounter,
                double startTimeSeconds,
                double endTimeSeconds)
            {
                Encounter = encounter;
                StartTimeSeconds = startTimeSeconds;
                EndTimeSeconds = endTimeSeconds;
            }

            public RadialEncounterRuntime Encounter { get; }
            public double StartTimeSeconds { get; }
            public double EndTimeSeconds { get; }
        }

        private readonly struct CuePriorityCandidate
        {
            public CuePriorityCandidate(string eventId, double targetTimeSeconds)
            {
                EventId = eventId ?? string.Empty;
                TargetTimeSeconds = targetTimeSeconds;
            }

            public string EventId { get; }
            public double TargetTimeSeconds { get; }
        }
    }
}
