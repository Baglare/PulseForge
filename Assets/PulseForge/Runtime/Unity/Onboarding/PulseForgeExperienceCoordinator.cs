using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Input;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Onboarding
{
    public enum PulseForgeExperienceView
    {
        None,
        FirstTimeSetup,
        Calibration,
        TrainingLessonSelect,
        ActiveTraining,
        TrainingResult
    }

    public enum FirstTimeSetupStep
    {
        Language,
        ReadabilityProfile,
        BindingSummary,
        Calibration,
        BasicTraining,
        Complete
    }

    public enum CalibrationStep
    {
        AudioVisualAlignment,
        InputInstructions,
        InputMeasuring,
        InputResult
    }

    public sealed class PulseForgeExperienceCoordinator : IDisposable
    {
        private const double CalibrationBeatIntervalSeconds = 0.6d;
        private const double TrainingRetryDelaySeconds = 0.8d;

        private static readonly TrainingLessonId[] FirstTimeLessons =
        {
            TrainingLessonId.TimingBar,
            TrainingLessonId.GuardTap,
            TrainingLessonId.DodgeTap,
            TrainingLessonId.LightAttack,
            TrainingLessonId.HeavyChargeRelease
        };

        private readonly DebugRhythmPrototypeController host;
        private readonly PulseForgeSaveService saveService;
        private readonly PulseForgeInputService inputService;
        private readonly DspClickTrack clickTrack;
        private readonly List<double> calibrationRawTimes = new List<double>();
        private readonly List<double> calibrationTargetTimes = new List<double>();
        private readonly List<double> calibrationBeatDspTimes = new List<double>();
        private readonly List<double> trainingCueDspTimes = new List<double>();
        private bool[] calibrationBeatConsumed = Array.Empty<bool>();
        private PulseForgeSettingsData stagedSettings;
        private CalibrationOffsetDraft calibrationOffsets;
        private RadialInputCalibrationResult calibrationResult;
        private bool hasCalibrationResult;
        private bool returnCalibrationToFirstTime;
        private double calibrationStartDspTime;
        private float alignmentAdjustmentMilliseconds;
        private RadialRhythmSession trainingSession;
        private RadialBeatMapData trainingFixture;
        private TrainingAttemptProgress trainingProgress;
        private TrainingLessonId currentLesson;
        private bool firstTimeTraining;
        private int firstTimeLessonIndex;
        private double trainingStartDspTime;
        private double retryAtDspTime;
        private long trainingInputSequence;
        private TrainingFailureReason lastTrainingFailure;
        private string trainingFeedbackKey = string.Empty;
        private int timingBarStage;
        private bool skipConfirmationVisible;

        public PulseForgeExperienceCoordinator(
            DebugRhythmPrototypeController host,
            PulseForgeSaveService saveService,
            PulseForgeInputService inputService)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            this.inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
            stagedSettings = SaveDefaults.CloneSettings(saveService.Settings);
            clickTrack = new DspClickTrack(host.gameObject);
            if (!stagedSettings.firstTimeSetupCompleted)
            {
                BeginFirstTimeSetup(true);
            }
        }

        public PulseForgeExperienceView View { get; private set; }
        public FirstTimeSetupStep FirstTimeStep { get; private set; }
        public CalibrationStep CalibrationStep { get; private set; }
        public int Revision { get; private set; }
        public bool IsActive => View != PulseForgeExperienceView.None;
        public bool IsBlockingGameplay => IsActive;
        public bool SkipConfirmationVisible => skipConfirmationVisible;
        public TrainingLessonId CurrentLesson => currentLesson;
        public int SuccessfulTrainingAttempts => trainingProgress == null
            ? 0
            : trainingProgress.SuccessfulAttempts;
        public double TrainingPerfectWindowSeconds => trainingSession == null
            ? RadialTrainingTiming.TimingBarPerfectWindowSeconds
            : trainingSession.TimingProfile.PerfectWindowSeconds;
        public double TrainingGoodWindowSeconds => trainingSession == null
            ? RadialTrainingTiming.TimingBarGoodWindowSeconds
            : trainingSession.TimingProfile.GoodWindowSeconds;
        public bool HasCalibrationResult => hasCalibrationResult;
        public RadialInputCalibrationResult CalibrationResult => calibrationResult;
        public int CalibrationMeasurementCount => calibrationRawTimes.Count;
        public float AlignmentAdjustmentMilliseconds => alignmentAdjustmentMilliseconds;
        public double CandidateBeatMapOffsetMilliseconds => calibrationOffsets == null
            ? 0d
            : calibrationOffsets.OriginalBeatMapOffsetSeconds * 1000d
                + alignmentAdjustmentMilliseconds;
        public float VisualPulse01 => clickTrack.GetVisualPulse(
            CalibrationStep == CalibrationStep.AudioVisualAlignment
                ? alignmentAdjustmentMilliseconds / 1000d
                : 0d);
        public PulseForgeUILanguage Language
        {
            get
            {
                string value = stagedSettings == null
                    ? saveService.Settings.language
                    : stagedSettings.language;
                return Enum.TryParse(value, true, out PulseForgeUILanguage language)
                    ? language
                    : PulseForgeUILanguage.English;
            }
        }

        public string GuardBinding => inputService.GetBindingDisplayString(PulseForgeInputAction.Guard);

        public string BindingSummary
        {
            get
            {
                return "Guard: " + inputService.GetBindingDisplayString(PulseForgeInputAction.Guard)
                    + "\nLight Attack: " + inputService.GetBindingDisplayString(PulseForgeInputAction.LightAttack)
                    + "\nDodge: " + inputService.GetBindingDisplayString(PulseForgeInputAction.Dodge)
                    + "\nHeavy Attack: " + inputService.GetBindingDisplayString(PulseForgeInputAction.HeavyAttack)
                    + "\nPause: " + inputService.GetBindingDisplayString(PulseForgeInputAction.Pause);
            }
        }

        public string CurrentLessonName => PulseForgeM9HLocalization.LessonName(currentLesson, Language);
        public string CurrentLessonDescription => PulseForgeM9HLocalization.LessonDescription(currentLesson, Language);

        public string CurrentLessonBindings
        {
            get
            {
                TrainingLessonDefinition definition = GetDefinition(currentLesson);
                return TrainingBindingDisplay.Build(
                    definition.Actions,
                    inputService.GetBindingDisplayString(PulseForgeInputAction.Guard),
                    inputService.GetBindingDisplayString(PulseForgeInputAction.LightAttack),
                    inputService.GetBindingDisplayString(PulseForgeInputAction.Dodge),
                    inputService.GetBindingDisplayString(PulseForgeInputAction.HeavyAttack));
            }
        }

        public string TrainingPrompt
        {
            get
            {
                if (currentLesson == TrainingLessonId.GuardHold)
                {
                    return TrainingGuardIsHeld
                        ? Localize("GuardHoldContinue")
                        : Localize("GuardHoldStart");
                }
                if (currentLesson == TrainingLessonId.TimingBar && timingBarStage < 3)
                {
                    return Localize(timingBarStage == 0
                        ? "TimingBarEarly"
                        : timingBarStage == 1 ? "TimingBarGood" : "TimingBarPerfect");
                }
                return Localize("TwoSuccesses");
            }
        }

        public string TrainingTimingInstruction => currentLesson == TrainingLessonId.GuardHold
            ? (TrainingGuardIsHeld
                ? Localize("GuardHoldContinue")
                : Localize("GuardHoldStart"))
            : Localize("TimingBarInstruction");

        public string TrainingFeedback
        {
            get
            {
                if (lastTrainingFailure != TrainingFailureReason.None)
                {
                    return Localize(lastTrainingFailure.ToString());
                }
                return string.IsNullOrEmpty(trainingFeedbackKey)
                    ? string.Empty
                    : Localize(trainingFeedbackKey);
            }
        }

        public bool TryGetTrainingTimingDeltaSeconds(out double deltaSeconds)
        {
            deltaSeconds = 0d;
            if (View != PulseForgeExperienceView.ActiveTraining
                || trainingSession == null
                || trainingCueDspTimes.Count == 0)
            {
                return false;
            }

            double rawTime = AudioSettings.dspTime - trainingStartDspTime;
            double effectiveTime = RadialTimingMath.EffectiveJudgementTimeSeconds(
                rawTime,
                stagedSettings.inputTimingOffsetSeconds);
            double goodWindow = trainingSession.TimingProfile.GoodWindowSeconds;
            double targetTime = trainingCueDspTimes[trainingCueDspTimes.Count - 1]
                - trainingStartDspTime;
            for (int i = 0; i < trainingCueDspTimes.Count; i++)
            {
                double candidate = trainingCueDspTimes[i] - trainingStartDspTime;
                if (candidate >= effectiveTime - goodWindow)
                {
                    targetTime = candidate;
                    break;
                }
            }

            deltaSeconds = targetTime - effectiveTime;
            return true;
        }

        public string Localize(string key)
        {
            return PulseForgeM9HLocalization.Text(key, Language);
        }

        private bool TrainingGuardIsHeld => trainingSession != null
            && trainingSession.IsHeld(RhythmAction.Guard);

        public void Update()
        {
            clickTrack.Update();
            if (!IsActive)
            {
                return;
            }

            if (View == PulseForgeExperienceView.Calibration
                && CalibrationStep == CalibrationStep.InputMeasuring)
            {
                UpdateInputCalibration();
            }
            else if (View == PulseForgeExperienceView.ActiveTraining)
            {
                UpdateTraining();
            }

            if (inputService.PauseWasPressedThisFrame)
            {
                if (View == PulseForgeExperienceView.Calibration)
                {
                    CancelCalibration();
                }
                else if (View == PulseForgeExperienceView.ActiveTraining
                    || View == PulseForgeExperienceView.TrainingResult
                    || View == PulseForgeExperienceView.TrainingLessonSelect)
                {
                    ExitTraining();
                }
            }
        }

        public void BeginFirstTimeSetup(bool applyRecommendedProfile = false)
        {
            StopTransientRuntime();
            stagedSettings = SaveDefaults.CloneSettings(saveService.Settings);
            stagedSettings.firstTimeSetupCompleted = false;
            if (applyRecommendedProfile)
            {
                PulseForgeReadabilityProfiles.Apply(
                    stagedSettings,
                    PulseForgeReadabilityProfile.Assisted);
            }
            View = PulseForgeExperienceView.FirstTimeSetup;
            FirstTimeStep = FirstTimeSetupStep.Language;
            skipConfirmationVisible = false;
            Revision++;
        }

        public void SelectLanguage(PulseForgeUILanguage language)
        {
            if (View != PulseForgeExperienceView.FirstTimeSetup)
            {
                return;
            }
            stagedSettings.language = language.ToString();
            stagedSettings.uiLanguage = stagedSettings.language;
            Revision++;
        }

        public void SelectReadabilityProfile(PulseForgeReadabilityProfile profile)
        {
            if (View != PulseForgeExperienceView.FirstTimeSetup)
            {
                return;
            }
            PulseForgeReadabilityProfiles.Apply(stagedSettings, profile);
            Revision++;
        }

        public void NextFirstTimeStep()
        {
            if (View != PulseForgeExperienceView.FirstTimeSetup || skipConfirmationVisible)
            {
                return;
            }
            switch (FirstTimeStep)
            {
                case FirstTimeSetupStep.Language:
                    FirstTimeStep = FirstTimeSetupStep.ReadabilityProfile;
                    break;
                case FirstTimeSetupStep.ReadabilityProfile:
                    FirstTimeStep = FirstTimeSetupStep.BindingSummary;
                    break;
                case FirstTimeSetupStep.BindingSummary:
                    FirstTimeStep = FirstTimeSetupStep.Calibration;
                    break;
                case FirstTimeSetupStep.Calibration:
                    StartCalibration(true);
                    return;
                case FirstTimeSetupStep.BasicTraining:
                    BeginFirstTimeTraining();
                    return;
                default:
                    CompleteFirstTimeSetup(false);
                    return;
            }
            Revision++;
        }

        public void PreviousFirstTimeStep()
        {
            if (View != PulseForgeExperienceView.FirstTimeSetup)
            {
                return;
            }
            if (skipConfirmationVisible)
            {
                skipConfirmationVisible = false;
            }
            else if (FirstTimeStep > FirstTimeSetupStep.Language)
            {
                FirstTimeStep--;
            }
            Revision++;
        }

        public void RequestSkipFirstTimeSetup()
        {
            if (View != PulseForgeExperienceView.FirstTimeSetup)
            {
                return;
            }
            skipConfirmationVisible = true;
            Revision++;
        }

        public void ConfirmSkipFirstTimeSetup()
        {
            if (View == PulseForgeExperienceView.FirstTimeSetup && skipConfirmationVisible)
            {
                CompleteFirstTimeSetup(true);
            }
        }

        public void StartCalibration(bool returnToFirstTime = false)
        {
            StopTransientRuntime();
            returnCalibrationToFirstTime = returnToFirstTime;
            if (!returnToFirstTime)
            {
                stagedSettings = SaveDefaults.CloneSettings(saveService.Settings);
            }
            calibrationOffsets = new CalibrationOffsetDraft(
                stagedSettings.beatmapOffsetSeconds,
                stagedSettings.inputTimingOffsetSeconds);
            alignmentAdjustmentMilliseconds = 0f;
            hasCalibrationResult = false;
            View = PulseForgeExperienceView.Calibration;
            CalibrationStep = CalibrationStep.AudioVisualAlignment;
            clickTrack.StartLoop(AudioSettings.dspTime + 0.15d, CalibrationBeatIntervalSeconds);
            Revision++;
        }

        public void AdjustAudioVisualAlignment(int direction)
        {
            if (View != PulseForgeExperienceView.Calibration
                || CalibrationStep != CalibrationStep.AudioVisualAlignment)
            {
                return;
            }
            double candidate = calibrationOffsets.OriginalBeatMapOffsetSeconds * 1000d
                + alignmentAdjustmentMilliseconds
                + (Math.Sign(direction) * 5d);
            candidate = Math.Max(-500d, Math.Min(500d, candidate));
            alignmentAdjustmentMilliseconds = (float)(candidate
                - calibrationOffsets.OriginalBeatMapOffsetSeconds * 1000d);
            Revision++;
        }

        public void ApplyAudioVisualAlignment()
        {
            if (!IsAudioVisualStep())
            {
                return;
            }
            calibrationOffsets.StageBeatMapOffset(CandidateBeatMapOffsetMilliseconds / 1000d);
            BeginInputInstructions();
        }

        public void RetryAudioVisualAlignment()
        {
            if (!IsAudioVisualStep())
            {
                return;
            }
            alignmentAdjustmentMilliseconds = 0f;
            clickTrack.StartLoop(AudioSettings.dspTime + 0.15d, CalibrationBeatIntervalSeconds);
            Revision++;
        }

        public void KeepCurrentAudioVisualAlignment()
        {
            if (!IsAudioVisualStep())
            {
                return;
            }
            calibrationOffsets.StageBeatMapOffset(calibrationOffsets.OriginalBeatMapOffsetSeconds);
            BeginInputInstructions();
        }

        public void StartInputMeasurement()
        {
            if (View != PulseForgeExperienceView.Calibration
                || CalibrationStep != CalibrationStep.InputInstructions)
            {
                return;
            }

            calibrationRawTimes.Clear();
            calibrationTargetTimes.Clear();
            calibrationBeatDspTimes.Clear();
            int count = RadialInputCalibration.WarmUpBeatCount
                + RadialInputCalibration.MeasurementBeatCount;
            calibrationBeatConsumed = new bool[count];
            calibrationStartDspTime = AudioSettings.dspTime + 0.45d;
            for (int i = 0; i < count; i++)
            {
                calibrationBeatDspTimes.Add(
                    calibrationStartDspTime + (i * CalibrationBeatIntervalSeconds));
            }
            clickTrack.StartFinite(calibrationBeatDspTimes);
            hasCalibrationResult = false;
            CalibrationStep = CalibrationStep.InputMeasuring;
            Revision++;
        }

        public void RetryInputCalibration()
        {
            if (View != PulseForgeExperienceView.Calibration)
            {
                return;
            }
            CalibrationStep = CalibrationStep.InputInstructions;
            hasCalibrationResult = false;
            clickTrack.Stop();
            Revision++;
        }

        public void ApplySuggestedInputOffset()
        {
            if (View != PulseForgeExperienceView.Calibration
                || CalibrationStep != CalibrationStep.InputResult
                || !hasCalibrationResult)
            {
                return;
            }
            calibrationOffsets.StageInputOffset(calibrationResult.SuggestedInputOffsetSeconds);
            CompleteCalibration();
        }

        public void KeepCurrentInputOffset()
        {
            if (View != PulseForgeExperienceView.Calibration
                || CalibrationStep != CalibrationStep.InputResult)
            {
                return;
            }
            calibrationOffsets.StageInputOffset(calibrationOffsets.OriginalInputOffsetSeconds);
            CompleteCalibration();
        }

        public void CancelCalibration()
        {
            if (View != PulseForgeExperienceView.Calibration)
            {
                return;
            }
            calibrationOffsets?.Cancel();
            clickTrack.Stop();
            if (returnCalibrationToFirstTime)
            {
                View = PulseForgeExperienceView.FirstTimeSetup;
                FirstTimeStep = FirstTimeSetupStep.Calibration;
            }
            else
            {
                View = PulseForgeExperienceView.None;
            }
            Revision++;
        }

        public void OpenTrainingLessonSelect()
        {
            StopTransientRuntime();
            stagedSettings = SaveDefaults.CloneSettings(saveService.Settings);
            firstTimeTraining = false;
            View = PulseForgeExperienceView.TrainingLessonSelect;
            Revision++;
        }

        public void StartLesson(TrainingLessonId lessonId)
        {
            firstTimeTraining = false;
            StartLessonInternal(lessonId);
        }

        public void RetryLesson()
        {
            if (View != PulseForgeExperienceView.ActiveTraining
                && View != PulseForgeExperienceView.TrainingResult)
            {
                return;
            }
            trainingProgress = new TrainingAttemptProgress();
            timingBarStage = 0;
            View = PulseForgeExperienceView.ActiveTraining;
            StartTrainingAttempt();
        }

        public void NextLesson()
        {
            if (View != PulseForgeExperienceView.TrainingResult)
            {
                return;
            }
            if (firstTimeTraining)
            {
                firstTimeLessonIndex++;
                if (firstTimeLessonIndex >= FirstTimeLessons.Length)
                {
                    View = PulseForgeExperienceView.FirstTimeSetup;
                    FirstTimeStep = FirstTimeSetupStep.Complete;
                    firstTimeTraining = false;
                    Revision++;
                    return;
                }
                StartLessonInternal(FirstTimeLessons[firstTimeLessonIndex]);
                return;
            }

            int next = (int)currentLesson + 1;
            if (next < RadialTrainingCatalog.All.Count)
            {
                StartLessonInternal((TrainingLessonId)next);
            }
            else
            {
                View = PulseForgeExperienceView.TrainingLessonSelect;
                Revision++;
            }
        }

        public void ExitTraining()
        {
            clickTrack.Stop();
            trainingSession = null;
            if (firstTimeTraining)
            {
                View = PulseForgeExperienceView.FirstTimeSetup;
                FirstTimeStep = FirstTimeSetupStep.BasicTraining;
                firstTimeTraining = false;
            }
            else
            {
                View = PulseForgeExperienceView.None;
            }
            Revision++;
        }

        public bool IsLessonCompleted(TrainingLessonId lessonId)
        {
            PulseForgeProfileData profile = saveService.Profile;
            if (profile?.tutorialLessons == null)
            {
                return false;
            }
            for (int i = 0; i < profile.tutorialLessons.Count; i++)
            {
                TutorialLessonProgressData progress = profile.tutorialLessons[i];
                if (progress != null
                    && progress.completed
                    && string.Equals(
                        progress.lessonId,
                        lessonId.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            calibrationOffsets?.Cancel();
            StopTransientRuntime();
            clickTrack.Dispose();
        }

        private void CompleteFirstTimeSetup(bool skipped)
        {
            stagedSettings.firstTimeSetupCompleted = true;
            if (skipped)
            {
                stagedSettings.calibrationCompleted = saveService.Settings.calibrationCompleted;
            }
            host.ApplyExperienceSettings(stagedSettings);
            View = PulseForgeExperienceView.None;
            skipConfirmationVisible = false;
            Revision++;
        }

        private void BeginInputInstructions()
        {
            clickTrack.Stop();
            CalibrationStep = CalibrationStep.InputInstructions;
            Revision++;
        }

        private void UpdateInputCalibration()
        {
            double now = AudioSettings.dspTime;
            if (inputService.GuardWasPressedThisFrame)
            {
                int beatIndex = FindNearestCalibrationBeat(now);
                if (beatIndex >= 0)
                {
                    calibrationBeatConsumed[beatIndex] = true;
                    if (beatIndex >= RadialInputCalibration.WarmUpBeatCount)
                    {
                        calibrationRawTimes.Add(now - calibrationStartDspTime);
                        calibrationTargetTimes.Add(
                            calibrationBeatDspTimes[beatIndex] - calibrationStartDspTime);
                    }
                    Revision++;
                }
            }

            if (calibrationBeatDspTimes.Count == 0
                || now <= calibrationBeatDspTimes[calibrationBeatDspTimes.Count - 1] + 0.35d)
            {
                return;
            }

            clickTrack.Stop();
            hasCalibrationResult = RadialInputCalibration.TryAnalyze(
                calibrationRawTimes,
                calibrationTargetTimes,
                calibrationOffsets.OriginalInputOffsetSeconds,
                out calibrationResult);
            CalibrationStep = CalibrationStep.InputResult;
            Revision++;
        }

        private int FindNearestCalibrationBeat(double dspTime)
        {
            int nearest = -1;
            double nearestDistance = 0.30d;
            for (int i = 0; i < calibrationBeatDspTimes.Count; i++)
            {
                if (calibrationBeatConsumed[i])
                {
                    continue;
                }
                double distance = Math.Abs(calibrationBeatDspTimes[i] - dspTime);
                if (distance <= nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        private void CompleteCalibration()
        {
            calibrationOffsets.Commit();
            stagedSettings.beatmapOffsetSeconds = (float)calibrationOffsets.BeatMapOffsetSeconds;
            stagedSettings.inputTimingOffsetSeconds = (float)calibrationOffsets.InputOffsetSeconds;
            stagedSettings.calibrationCompleted = true;
            clickTrack.Stop();
            if (returnCalibrationToFirstTime)
            {
                View = PulseForgeExperienceView.FirstTimeSetup;
                FirstTimeStep = FirstTimeSetupStep.BasicTraining;
            }
            else
            {
                host.ApplyExperienceSettings(stagedSettings);
                View = PulseForgeExperienceView.None;
            }
            Revision++;
        }

        private void BeginFirstTimeTraining()
        {
            firstTimeTraining = true;
            firstTimeLessonIndex = 0;
            StartLessonInternal(FirstTimeLessons[0]);
        }

        private void StartLessonInternal(TrainingLessonId lessonId)
        {
            currentLesson = lessonId;
            trainingProgress = new TrainingAttemptProgress(GetIncompletePersistedAttempts(lessonId));
            timingBarStage = 0;
            View = PulseForgeExperienceView.ActiveTraining;
            StartTrainingAttempt();
        }

        private void StartTrainingAttempt()
        {
            trainingFixture = RadialTrainingCatalog.CreateFixture(currentLesson);
            RadialTimingProfile timingProfile = currentLesson == TrainingLessonId.TimingBar
                ? RadialTrainingTiming.TimingBarProfile
                : RadialTimingProfile.FromMode(TimingAssistMode.Practice);
            trainingSession = new RadialRhythmSession(
                trainingFixture.encounters,
                timingProfile,
                0d);
            trainingStartDspTime = AudioSettings.dspTime + 0.25d;
            retryAtDspTime = 0d;
            trainingInputSequence = 0L;
            lastTrainingFailure = TrainingFailureReason.None;
            trainingFeedbackKey = string.Empty;
            trainingCueDspTimes.Clear();
            trainingCueDspTimes.AddRange(
                CollectTrainingClickTimes(trainingFixture, trainingStartDspTime));
            clickTrack.StartFinite(trainingCueDspTimes);
            Revision++;
        }

        private void UpdateTraining()
        {
            double now = AudioSettings.dspTime;
            if (trainingSession == null)
            {
                if (retryAtDspTime > 0d && now >= retryAtDspTime)
                {
                    trainingProgress.BeginRetry();
                    StartTrainingAttempt();
                }
                return;
            }

            double rawTime = now - trainingStartDspTime;
            ForwardTrainingAction(
                RhythmAction.Guard,
                inputService.GuardWasPressedThisFrame,
                inputService.GuardWasReleasedThisFrame,
                rawTime);
            ForwardTrainingAction(
                RhythmAction.LightAttack,
                inputService.LightAttackWasPressedThisFrame,
                inputService.LightAttackWasReleasedThisFrame,
                rawTime);
            ForwardTrainingAction(
                RhythmAction.Dodge,
                inputService.DodgeWasPressedThisFrame,
                inputService.DodgeWasReleasedThisFrame,
                rawTime);
            ForwardTrainingAction(
                RhythmAction.HeavyAttack,
                inputService.HeavyAttackWasPressedThisFrame,
                inputService.HeavyAttackWasReleasedThisFrame,
                rawTime);
            if (trainingSession == null)
            {
                return;
            }

            double effectiveTime = RadialTimingMath.EffectiveJudgementTimeSeconds(
                rawTime,
                stagedSettings.inputTimingOffsetSeconds);
            IReadOnlyList<RequirementResult> updates = trainingSession.Update(
                effectiveTime,
                IsTrainingActionHeld);
            if (ProcessTrainingResults(updates))
            {
                return;
            }
            if (trainingSession.IsComplete)
            {
                CompleteTrainingAttempt();
            }
        }

        private void ForwardTrainingAction(
            RhythmAction action,
            bool pressed,
            bool released,
            double rawTime)
        {
            if (trainingSession == null)
            {
                return;
            }
            if (pressed)
            {
                RadialInputResolveResult result = trainingSession.Press(
                    action,
                    rawTime,
                    ++trainingInputSequence,
                    stagedSettings.inputTimingOffsetSeconds,
                    0d,
                    1);
                if (ProcessTrainingResults(result.RequirementResults))
                {
                    return;
                }
                if (!result.Consumed && ProcessTrainingAudit(trainingSession.LastInputAudit))
                {
                    return;
                }
            }
            if (released && trainingSession != null)
            {
                RadialInputResolveResult result = trainingSession.Release(
                    action,
                    rawTime,
                    ++trainingInputSequence,
                    stagedSettings.inputTimingOffsetSeconds,
                    0d,
                    1);
                if (!ProcessTrainingResults(result.RequirementResults) && !result.Consumed)
                {
                    ProcessTrainingAudit(trainingSession.LastInputAudit);
                }
            }
        }

        private bool ProcessTrainingAudit(RadialInputAuditRecord audit)
        {
            if (trainingSession == null || string.IsNullOrEmpty(audit.Timing.RequirementId))
            {
                return false;
            }
            switch (audit.Reason)
            {
                case RadialInputAuditReason.WrongAction:
                    FailTrainingAttempt(TrainingFailureReason.WrongKey);
                    return true;
                case RadialInputAuditReason.FutureSequenceStep:
                    FailTrainingAttempt(TrainingFailureReason.WrongSequenceOrder);
                    return true;
                case RadialInputAuditReason.WrongPhase:
                    FailTrainingAttempt(TrainingFailureReason.WrongKey);
                    return true;
                case RadialInputAuditReason.OutsideWindow:
                    FailTrainingAttempt(audit.Timing.DeltaMilliseconds < 0d
                        ? TrainingFailureReason.TooEarly
                        : TrainingFailureReason.TooLate);
                    return true;
                default:
                    return false;
            }
        }

        private bool ProcessTrainingResults(IReadOnlyList<RequirementResult> results)
        {
            if (results == null)
            {
                return false;
            }
            for (int i = 0; i < results.Count; i++)
            {
                RequirementResult result = results[i];
                if (result.Grade == HitGrade.Miss)
                {
                    RadialInputAuditReason auditReason = trainingSession == null
                        ? RadialInputAuditReason.NoActiveRequirement
                        : trainingSession.LastInputAudit.Reason;
                    FailTrainingAttempt(TrainingFailureReasonResolver.Resolve(
                        result,
                        trainingFixture.encounters[0].eventType,
                        auditReason));
                    return true;
                }
            }
            return false;
        }

        private void CompleteTrainingAttempt()
        {
            clickTrack.Stop();
            trainingSession = null;
            if (currentLesson == TrainingLessonId.TimingBar && timingBarStage < 3)
            {
                timingBarStage++;
                trainingFeedbackKey = timingBarStage < 3
                    ? "Next"
                    : "TwoSuccesses";
                retryAtDspTime = AudioSettings.dspTime + TrainingRetryDelaySeconds;
                Revision++;
                return;
            }

            trainingProgress.RecordSuccess();
            saveService.RecordTutorialSuccess(currentLesson.ToString());
            trainingFeedbackKey = "LessonComplete";
            lastTrainingFailure = TrainingFailureReason.None;
            if (trainingProgress.IsComplete)
            {
                View = PulseForgeExperienceView.TrainingResult;
                retryAtDspTime = 0d;
            }
            else
            {
                retryAtDspTime = AudioSettings.dspTime + TrainingRetryDelaySeconds;
            }
            Revision++;
        }

        private void FailTrainingAttempt(TrainingFailureReason reason)
        {
            clickTrack.Stop();
            trainingSession = null;
            trainingProgress.RecordFailure();
            lastTrainingFailure = reason == TrainingFailureReason.None
                ? TrainingFailureReason.TooLate
                : reason;
            trainingFeedbackKey = string.Empty;
            retryAtDspTime = AudioSettings.dspTime + TrainingRetryDelaySeconds;
            Revision++;
        }

        private bool IsTrainingActionHeld(RhythmAction action)
        {
            switch (action)
            {
                case RhythmAction.Guard: return inputService.GuardIsHeld;
                case RhythmAction.Dodge: return inputService.DodgeIsHeld;
                case RhythmAction.HeavyAttack: return inputService.HeavyAttackIsHeld;
                default: return inputService.LightAttackIsHeld;
            }
        }

        private static List<double> CollectTrainingClickTimes(
            RadialBeatMapData fixture,
            double startDspTime)
        {
            List<double> targets = new List<double>();
            for (int encounterIndex = 0; encounterIndex < fixture.encounters.Count; encounterIndex++)
            {
                RadialEncounterEventData encounter = fixture.encounters[encounterIndex];
                for (int requirementIndex = 0; requirementIndex < encounter.requirements.Count; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex];
                    if (encounter.eventType == RadialEventType.BreakTarget)
                    {
                        for (int count = 0; count < requirement.requiredPressCount; count++)
                        {
                            AddUniqueTarget(targets, startDspTime
                                + requirement.windowStartTimeSeconds
                                + (count * 0.22d));
                        }
                    }
                    else
                    {
                        AddUniqueTarget(targets, startDspTime + requirement.targetTimeSeconds);
                        if (encounter.eventType == RadialEventType.GuardHold)
                        {
                            AddUniqueTarget(targets, startDspTime + requirement.holdEndTimeSeconds);
                        }
                    }
                }
            }
            targets.Sort();
            return targets;
        }

        private static void AddUniqueTarget(List<double> targets, double value)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (Math.Abs(targets[i] - value) < 0.001d)
                {
                    return;
                }
            }
            targets.Add(value);
        }

        private void StopTransientRuntime()
        {
            clickTrack.Stop();
            trainingSession = null;
            retryAtDspTime = 0d;
            trainingCueDspTimes.Clear();
        }

        private bool IsAudioVisualStep()
        {
            return View == PulseForgeExperienceView.Calibration
                && CalibrationStep == CalibrationStep.AudioVisualAlignment
                && calibrationOffsets != null;
        }

        private static TrainingLessonDefinition GetDefinition(TrainingLessonId lessonId)
        {
            IReadOnlyList<TrainingLessonDefinition> lessons = RadialTrainingCatalog.All;
            for (int i = 0; i < lessons.Count; i++)
            {
                if (lessons[i].Id == lessonId)
                {
                    return lessons[i];
                }
            }
            return lessons[0];
        }

        private int GetIncompletePersistedAttempts(TrainingLessonId lessonId)
        {
            PulseForgeProfileData profile = saveService.Profile;
            if (profile?.tutorialLessons == null)
            {
                return 0;
            }
            for (int i = 0; i < profile.tutorialLessons.Count; i++)
            {
                TutorialLessonProgressData progress = profile.tutorialLessons[i];
                if (progress != null
                    && !progress.completed
                    && string.Equals(
                        progress.lessonId,
                        lessonId.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Math.Min(1, progress.successfulAttempts);
                }
            }
            return 0;
        }

    }
}
