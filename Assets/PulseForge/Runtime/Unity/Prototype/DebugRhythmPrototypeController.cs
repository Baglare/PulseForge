using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;
using PulseForge.Runtime.Unity.BeatMaps;
using PulseForge.Runtime.Unity.Input;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Timing;
using PulseForge.Runtime.Unity.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugRhythmPrototypeController : MonoBehaviour
    {
        private const double PerfectWindowSeconds = 0.045d;
        private const double GoodWindowSeconds = 0.100d;
        private const float RhythmLaneHeight = 116f;
        private const float LayoutMargin = 10f;
        private const float PanelGap = 10f;
        private const float RightPanelPreferredWidth = 360f;
        private const float RightPanelMinWidth = 300f;
        private const float MainAreaMinWidth = 260f;
        private const float PanelPadding = 10f;
        private const float CardSpacing = 10f;

        private static readonly Color HudPanelColor = new Color(0.055f, 0.065f, 0.08f, 0.94f);
        private static readonly Color CardColor = new Color(0.105f, 0.12f, 0.145f, 0.96f);
        private static readonly Color TextPrimaryColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color TextMutedColor = new Color(0.62f, 0.68f, 0.74f, 1f);
        private static readonly Color GuardColor = new Color(0.22f, 0.82f, 1f, 1f);
        private static readonly Color StrikeColor = new Color(1f, 0.38f, 0.22f, 1f);
        private static readonly Color PerfectColor = new Color(0.98f, 0.94f, 0.42f, 1f);
        private static readonly Color GoodColor = new Color(0.44f, 0.92f, 0.55f, 1f);
        private static readonly Color MissColor = new Color(1f, 0.24f, 0.22f, 1f);
        private static readonly Color PendingColor = new Color(0.82f, 0.86f, 0.9f, 1f);

        [SerializeField] private TextAsset debugBeatMapJson = null;
        [SerializeField] private TextAsset debugRadialBeatMapJson = null;
        [SerializeField] private DebugBeatMapAsset debugBeatMapAsset = null;
        [SerializeField] private AudioClip debugAudioClip = null;
        [SerializeField] private bool useAudioClockWhenClipAssigned = true;
        [SerializeField] private float startCountdownSeconds = 1.0f;
        [SerializeField] private bool rejectAmbiguousSimultaneousActions = true;
        [SerializeField] private float simultaneousInputWindowSeconds = 0.035f;
        [SerializeField] private float debugBeatMapOffsetSeconds = 0f;
        [SerializeField] private float inputTimingOffsetSeconds = 0f;
        [SerializeField] private bool useDeterministicTimingFixture = false;
        [SerializeField] private DebugCombatSceneView combatSceneView = null;

        private RhythmSession session;
        private RadialRhythmSession radialSession;
        private ScoreTracker scoreTracker;
        private ISongClock songClock;
        private string lastFeedback = "Press Start / Restart";
        private Vector2 eventListScroll;
        private Vector2 rightPanelScroll;
        private readonly DebugRhythmLaneRenderer rhythmLaneRenderer = new DebugRhythmLaneRenderer();
        private readonly DebugCombatFeedbackRenderer combatFeedbackRenderer = new DebugCombatFeedbackRenderer();
        private bool isCountdownActive;
        private double countdownStartedAtSeconds;
        private double countdownDurationSeconds;
        private bool hasPendingInput;
        private RhythmAction pendingInputAction;
        private double pendingInputSongTimeSeconds;
        private double pendingInputDeadlineRealtimeSeconds;
        private bool pendingInputIsAmbiguous;
        private bool hasLastInputTimingError;
        private double lastInputTimingErrorMs;
        private GUIStyle titleStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle mutedLabelStyle;
        private GUIStyle metricLabelStyle;
        private GUIStyle metricValueStyle;
        private GUIStyle badgeStyle;
        private AudioClip runtimeAudioClip;
        private IReadOnlyList<BeatEventData> runtimeBeatEvents;
        private RadialBeatMapData runtimeRadialBeatMap;
        private BeatGridData runtimeBeatGrid;
        private BeatGridData activeBeatGrid;
        private AnalyzerQualityReport runtimeAnalyzerQuality;
        private PlannerQualityReport runtimePlannerQuality;
        private string runtimeBeatMapFingerprint = string.Empty;
        private bool activeSessionUsesRadialV2ScoreSchema;
        private long radialInputSequence;
        private readonly HashSet<string> scoredRadialUnits = new HashSet<string>(StringComparer.Ordinal);
        private string runtimeAudioDisplayName = string.Empty;
        private string runtimeAudioImportStatus = "Select MP3, WAV, M4A, AAC, FLAC, OGG, OPUS, WMA or AIFF.";
        private bool isRuntimeAudioImportBusy;
        private RuntimeDetectionMode runtimeDetectionMode = RuntimeDetectionMode.Onset;
        private RuntimeDifficulty runtimeDifficulty = RuntimeDifficulty.Normal;
        private RuntimeCombatStyle runtimeCombatStyle = RuntimeCombatStyle.Legacy;
        private RuntimeCoverage runtimeCoverage = RuntimeCoverage.Standard;
        private RadialGameMode selectedGameMode = RadialGameMode.Standard;
        private TimingAssistMode selectedTimingAssist = TimingAssistMode.Relaxed;
        private bool showUpcomingInputs = true;
        private bool beatPulseEnabled = true;
        private float forecastLeadMultiplier = 1.25f;
        private RadialReadabilityMode readabilityMode = RadialReadabilityMode.Assisted;
        private readonly RadialGameModePolicy radialRunPolicy = new RadialGameModePolicy();
        private readonly RadialRunStatusController radialStatusEffects =
            new RadialRunStatusController();
        private int runDamageRevision;
        private RuntimeAudioPipelineSettings appliedRuntimePipelineSettings = RuntimeAudioPipelineSettings.Default;
        private readonly PulseForgeRuntimeFlow runtimeFlow = new PulseForgeRuntimeFlow();
        private bool useRuntimeSessionSource;
        private bool legacyDebugOverlayVisible;
        private bool timingAuditVisible;
        private bool hasTimingAudit;
        private double activeRadialBeatMapOffsetSeconds;
        private string setupStatusMessage = "Choose a song, select your settings, then analyze.";
        private long gameplayFeedbackSequence;
        private bool hasPublishedGameplayState;
        private PulseForgeUIState lastPublishedGameplayState;
        private PulseForgeSaveService saveService;
        private PulseForgeSceneUIRoot sceneUIRoot;
        private bool saveSetupToLibrary;
        private bool isSavedTracksOpen;
        private bool completedSessionRecorded;
        private int savedTrackLibraryRevision;
        private string activeSavedTrackId = string.Empty;
        private string activeSavedPresetId = string.Empty;
        private string savedTrackLibraryMessage = string.Empty;
        private PulseForgeInputService inputService;
        private PulseForgeSettingsData settingsDraft;
        private PulseForgeSettingsData settingsPreviewBaseline;
        private IReadOnlyList<PulseForgeResolutionOption> availableResolutions = Array.Empty<PulseForgeResolutionOption>();
        private bool isSettingsOpen;
        private string settingsMessage = string.Empty;
        private int settingsDraftRevision;

        public event Action<PulseForgeGameplayResultEvent> GameplayResultResolved;
        public event Action<PulseForgeComboChangedEvent> GameplayComboChanged;
        public event Action GameplaySessionRestarted;
        public event Action<PulseForgeUIState> GameplayStateChanged;

        public bool SaveSetupToLibrary => saveSetupToLibrary;
        public bool IsSavedTracksOpen => isSavedTracksOpen;
        public int SavedTrackLibraryRevision => savedTrackLibraryRevision;
        public string SavedTrackLibraryMessage => savedTrackLibraryMessage;
        public bool IsSettingsOpen => isSettingsOpen;
        public bool IsInputRebinding => inputService != null && inputService.IsRebinding;
        public string SettingsMessage => settingsMessage;
        public int SettingsDraftRevision => settingsDraftRevision;
        public RadialGameMode SelectedGameMode => selectedGameMode;
        public string SelectedGameModeLabel => selectedGameMode == RadialGameMode.OneLife
            ? "One Life"
            : selectedGameMode.ToString();
        public RadialGameMode RunGameMode => radialRunPolicy.Mode;
        public string RunGameModeLabel => radialRunPolicy.Mode == RadialGameMode.OneLife
            ? "One Life"
            : radialRunPolicy.Mode.ToString();
        public RadialRunState RunState => radialRunPolicy.State;
        public RadialRunOutcome RunOutcome => radialRunPolicy.Outcome;
        public int RunHealth => radialRunPolicy.CurrentHealth;
        public int RunDamageRevision => runDamageRevision;
        public double RunFailureTimeSeconds => radialRunPolicy.FailureTimeSeconds;
        public string RunFailureReason => radialRunPolicy.FailureReason == RadialRunFailureReason.WrongInput
            ? "Wrong Input"
            : radialRunPolicy.FailureReason == RadialRunFailureReason.Miss ? "Miss" : string.Empty;
        public bool IsFogActive => radialStatusEffects
            .GetSnapshot(CurrentSongTimeSeconds)
            .IsFogActive;
        public PulseForgeSettingsData SettingsDraft => settingsDraft;
        public PulseForgeUILanguage ActiveUILanguage
        {
            get
            {
                string value = isSettingsOpen && settingsDraft != null
                    ? settingsDraft.uiLanguage
                    : saveService?.Settings?.uiLanguage;
                return Enum.TryParse(value, true, out PulseForgeUILanguage language)
                    ? language
                    : PulseForgeUILanguage.English;
            }
        }
        public IReadOnlyList<PulseForgeResolutionOption> AvailableResolutions => availableResolutions;
        public bool MotionEnabledSetting
        {
            get
            {
                EnsureSaveService();
                return saveService.Settings.enableMotion;
            }
        }

        public SavedTrackLibraryData SavedTrackLibrary
        {
            get
            {
                EnsureSaveService();
                return saveService.Library;
            }
        }

        public PulseForgeUIState UIState
        {
            get { return runtimeFlow.State; }
        }

        public PulseForgeProcessingStage ProcessingStage
        {
            get { return runtimeFlow.ProcessingStage; }
        }

        public RuntimeAudioPipelineSettings SelectedPipelineSettings
        {
            get
            {
                return new RuntimeAudioPipelineSettings(
                    runtimeDetectionMode,
                    runtimeDifficulty,
                    runtimeCombatStyle,
                    runtimeCoverage);
            }
        }

        public RuntimeAudioPipelineSettings AppliedPipelineSettings
        {
            get { return appliedRuntimePipelineSettings; }
        }

        public ScoreSnapshot Score
        {
            get { return GetSnapshot(); }
        }

        public IReadOnlyList<BeatEventRuntime> SessionEvents
        {
            get
            {
                return radialSession != null || session == null
                    ? Array.Empty<BeatEventRuntime>()
                    : session.Events;
            }
        }

        public int SessionEventCount
        {
            get
            {
                return radialSession != null
                    ? radialSession.TotalEncounterCount
                    : session == null ? 0 : session.TotalEventCount;
            }
        }

        public int SessionInputCost => !useRuntimeSessionSource || runtimePlannerQuality == null
            ? SessionEventCount
            : runtimePlannerQuality.totalInputCost;

        public string AnalysisQualitySummary => GetAnalysisQualitySummary();

        public bool IsRadialSessionActive => radialSession != null;

        public bool UsesRadialCombatPresentation => radialSession != null
            && activeSessionUsesRadialV2ScoreSchema;

        internal IReadOnlyList<RadialEncounterRuntime> RadialPresentationEncounters =>
            UsesRadialCombatPresentation
                ? radialSession.Encounters
                : Array.Empty<RadialEncounterRuntime>();

        internal RadialStatusEffectSnapshot RadialStatusForPresentation =>
            radialStatusEffects.GetSnapshot(CurrentSongTimeSeconds);

        internal int RadialPresentationDifficultyLevel => (int)(useRuntimeSessionSource
            ? appliedRuntimePipelineSettings.Difficulty
            : runtimeDifficulty);

        internal float ForecastLeadMultiplierForPresentation => forecastLeadMultiplier;

        internal RadialReadabilityMode ReadabilityModeForPresentation => readabilityMode;

        internal bool ShowUpcomingInputsForPresentation => showUpcomingInputs;

        internal bool BeatPulseEnabledForPresentation => beatPulseEnabled;

        internal BeatGridData ActiveBeatGridForPresentation => activeBeatGrid;

        internal RadialTimingProfile ActiveTimingProfileForPresentation => radialSession == null
            ? RadialTimingProfile.FromMode(selectedTimingAssist)
            : radialSession.TimingProfile;

        internal bool TryGetRadialTimingSnapshot(
            string eventId,
            string requirementId,
            double rawSongTimeSeconds,
            int focusedCueLimit,
            out RadialTimingSnapshot snapshot)
        {
            if (radialSession == null)
            {
                snapshot = default(RadialTimingSnapshot);
                return false;
            }
            return radialSession.TryGetTimingSnapshot(
                eventId,
                requirementId,
                rawSongTimeSeconds,
                inputTimingOffsetSeconds,
                activeRadialBeatMapOffsetSeconds,
                focusedCueLimit,
                out snapshot);
        }

        internal bool TryGetRadialInputOpportunity(
            string eventId,
            string requirementId,
            RhythmAction action,
            RhythmInputPhase phase,
            double rawSongTimeSeconds,
            int focusedCueLimit,
            out InputOpportunitySnapshot snapshot)
        {
            if (radialSession == null)
            {
                snapshot = default(InputOpportunitySnapshot);
                return false;
            }
            return radialSession.TryGetInputOpportunitySnapshot(
                eventId,
                requirementId,
                action,
                phase,
                rawSongTimeSeconds,
                inputTimingOffsetSeconds,
                activeRadialBeatMapOffsetSeconds,
                focusedCueLimit,
                out snapshot);
        }

        public TimingAssistMode SelectedTimingAssist => selectedTimingAssist;

        internal bool IsRadialActionHeldForPresentation(RhythmAction action)
        {
            return UsesRadialCombatPresentation && radialSession.IsHeld(action);
        }

        public bool HasBuiltInDemo
        {
            get
            {
                return debugAudioClip != null
                    && (debugRadialBeatMapJson != null
                        || debugBeatMapJson != null
                        || debugBeatMapAsset != null);
            }
        }

        public bool HasSelectedAudio
        {
            get { return !string.IsNullOrWhiteSpace(runtimeFlow.SelectedAudioPath); }
        }

        public string SelectedAudioFileName
        {
            get
            {
                return HasSelectedAudio
                    ? Path.GetFileName(runtimeFlow.SelectedAudioPath)
                    : string.Empty;
            }
        }

        public string SetupStatusMessage
        {
            get { return setupStatusMessage; }
        }

        public string RuntimeAudioImportStatus
        {
            get { return runtimeAudioImportStatus; }
        }

        public string ErrorMessage
        {
            get { return runtimeFlow.ErrorMessage; }
        }

        public string SongName
        {
            get
            {
                string value = useRuntimeSessionSource && !string.IsNullOrWhiteSpace(runtimeAudioDisplayName)
                    ? runtimeAudioDisplayName
                    : debugAudioClip == null ? "Built-in Demo" : debugAudioClip.name;
                return value.Replace('_', ' ');
            }
        }

        public string AppliedDetectionLabel
        {
            get
            {
                return useRuntimeSessionSource
                    ? appliedRuntimePipelineSettings.DetectionMode.ToString()
                    : "Built-in";
            }
        }

        public string AppliedDifficultyLabel
        {
            get
            {
                return useRuntimeSessionSource
                    ? appliedRuntimePipelineSettings.Difficulty.ToString()
                    : ResolveBuiltInDifficultyLabel();
            }
        }

        public string AppliedCombatStyleLabel
        {
            get
            {
                return useRuntimeSessionSource
                    ? appliedRuntimePipelineSettings.CombatStyle.ToString()
                    : ResolveBuiltInCombatStyleLabel();
            }
        }

        public string AppliedCoverageLabel
        {
            get
            {
                return useRuntimeSessionSource
                    ? FormatCoverage(appliedRuntimePipelineSettings.Coverage)
                    : FormatCoverage(runtimeCoverage);
            }
        }

        public bool CanAnalyzeSelectedAudio
        {
            get
            {
                return UIState == PulseForgeUIState.Setup
                    && HasSelectedAudio
                    && !isRuntimeAudioImportBusy;
            }
        }

        public bool CanStart
        {
            get { return UIState == PulseForgeUIState.Ready && HasActiveSession && !isRuntimeAudioImportBusy; }
        }

        public bool CanPause
        {
            get { return UIState == PulseForgeUIState.Playing && CanTogglePause; }
        }

        public bool IsPaused
        {
            get { return UIState == PulseForgeUIState.Paused; }
        }

        public bool IsComplete
        {
            get { return UIState == PulseForgeUIState.Completed; }
        }

        public bool IsFailed => UIState == PulseForgeUIState.Failed;

        public double CurrentSongTimeSeconds
        {
            get
            {
                return UIState == PulseForgeUIState.Failed
                    ? radialRunPolicy.FailureTimeSeconds
                    : CurrentTimeSeconds;
            }
        }

        public double SessionDurationSeconds
        {
            get { return GetSessionDurationSeconds(); }
        }

        public double CountdownRemainingSeconds
        {
            get { return GetCountdownRemainingSeconds(); }
        }

        public PulseForgeFeedbackPresentation CurrentFeedback
        {
            get { return combatFeedbackRenderer.GetPresentation(GetPresentationTimeSeconds()); }
        }

        private void Start()
        {
            EnsureSaveService();
            ApplyLoadedSettings();
            EnsureInputService();
            PulseForgeRuntimeSettingsApplier.ApplyAudio(saveService.Settings, GetOrAddAudioSource());
            PulseForgeRuntimeSettingsApplier.ApplyDisplay(saveService.Settings);
            runtimeFlow.ReturnToSetup(false);
            PrepareSession(false);
            PulseForgeUIController uiController = PulseForgeUIBootstrap.EnsureFor(this);
            sceneUIRoot = uiController == null ? null : uiController.SceneRoot;
            ApplyLoadedMotionSetting();
            PublishGameplayStateIfChanged();
        }

        private void OnDestroy()
        {
            SaveCurrentSettings();
            inputService?.Dispose();
            inputService = null;
            StopClock();
            if (runtimeAudioClip != null)
            {
                Destroy(runtimeAudioClip);
            }
        }

        private void Update()
        {
            double frameSongTimeSeconds = CurrentTimeSeconds;
            HandleRuntimeKeyboardInput(frameSongTimeSeconds);

            if (radialSession != null)
            {
                radialStatusEffects.Update(frameSongTimeSeconds);
            }

            if (isCountdownActive)
            {
                UpdateCountdown();
                return;
            }

            if (!IsSessionRunning || !HasActiveSession || IsActiveSessionComplete)
            {
                return;
            }

            if (radialSession != null)
            {
                IReadOnlyList<RequirementResult> radialResults = radialSession.Update(
                    frameSongTimeSeconds + inputTimingOffsetSeconds,
                    IsActionHeld);
                ProcessRadialRequirementResults(radialResults);
                StopIfComplete();
                return;
            }

            ProcessPendingInputBuffer(GetPresentationTimeSeconds(), false);
            if (hasPendingInput || !IsSessionRunning || session == null || session.IsComplete)
            {
                return;
            }

            IReadOnlyList<HitResult> timedOutResults = session.MarkTimedOutEvents(CurrentTimeSeconds);
            double presentationTimeSeconds = GetPresentationTimeSeconds();
            for (int i = 0; i < timedOutResults.Count; i++)
            {
                RecordResult(timedOutResults[i]);
                combatFeedbackRenderer.ShowMiss(presentationTimeSeconds);
                ShowCombatSceneMiss(timedOutResults[i]);
                lastFeedback = FormatFeedback(timedOutResults[i]);
            }

            StopIfComplete();
        }

        private void OnGUI()
        {
            if (!legacyDebugOverlayVisible && !timingAuditVisible)
            {
                return;
            }

            EnsureHudStyles();
            if (legacyDebugOverlayVisible)
            {
                CalculateHudRects(out Rect mainAreaRect, out Rect rightPanelRect);
                DrawMainArea(mainAreaRect);
                DrawRightPanel(rightPanelRect);
            }
            if (timingAuditVisible)
            {
                DrawTimingAuditPanel();
            }
        }

        private static void CalculateHudRects(out Rect mainAreaRect, out Rect rightPanelRect)
        {
            float contentWidth = Mathf.Max(1f, Screen.width - LayoutMargin * 2f);
            float contentHeight = Mathf.Max(1f, Screen.height - LayoutMargin * 2f);
            float rightPanelWidth = CalculateRightPanelWidth(contentWidth);
            float mainAreaWidth = Mathf.Max(1f, contentWidth - PanelGap - rightPanelWidth);

            mainAreaRect = new Rect(LayoutMargin, LayoutMargin, mainAreaWidth, contentHeight);
            rightPanelRect = new Rect(mainAreaRect.xMax + PanelGap, LayoutMargin, rightPanelWidth, contentHeight);
        }

        private void DrawTimingAuditPanel()
        {
            const float width = 430f;
            const float height = 282f;
            Rect rect = new Rect(
                Mathf.Max(10f, Screen.width - width - 14f),
                14f,
                width,
                height);
            DrawSolidRect(rect, HudPanelColor);
            GUILayout.BeginArea(InsetRect(rect, PanelPadding));
            GUILayout.Label("TIMING AUDIT (F2)", sectionTitleStyle);
            if (!hasTimingAudit || radialSession == null)
            {
                GUILayout.Label("Waiting for radial press / release...", mutedLabelStyle);
                GUILayout.EndArea();
                return;
            }

            RadialInputAuditRecord audit = radialSession.LastInputAudit;
            RadialTimingSnapshot timing = audit.Timing;
            GUILayout.Label(
                timing.Action + " / " + timing.Phase + "  ->  " + audit.Reason,
                metricValueStyle);
            GUILayout.Label(
                "Target: " + timing.TargetTimeSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                + "s   Effective: " + timing.EffectiveJudgementTimeSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                + "s   Delta: " + timing.DeltaMilliseconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " ms",
                metricLabelStyle);
            GUILayout.Label(
                "Windows: +/-" + (timing.PerfectWindowSeconds * 1000d).ToString("0", CultureInfo.InvariantCulture)
                + " / +/-" + (timing.GoodWindowSeconds * 1000d).ToString("0", CultureInfo.InvariantCulture)
                + " ms   Assist: " + timing.TimingAssist,
                metricLabelStyle);
            GUILayout.Label(
                "Offsets: input " + (timing.InputOffsetSeconds * 1000d).ToString("+0;-0;0", CultureInfo.InvariantCulture)
                + " ms, beatmap " + (timing.BeatMapOffsetSeconds * 1000d).ToString("+0;-0;0", CultureInfo.InvariantCulture) + " ms",
                metricLabelStyle);
            GUILayout.Label(
                "Matched: " + EmptyAsDash(timing.EventId) + " / " + EmptyAsDash(timing.RequirementId),
                metricLabelStyle);
            GUILayout.Label(
                "Focused: " + EmptyAsDash(audit.FocusedEventId) + " / " + EmptyAsDash(audit.FocusedRequirementId)
                + "   State: " + timing.RequirementState + " / " + timing.FocusState,
                mutedLabelStyle);
            if (audit.Reason == RadialInputAuditReason.NoActiveRequirement)
            {
                InputOpportunityDiagnostics diagnostics = audit.Diagnostics;
                GUILayout.Label(
                    "Candidates: pending " + diagnostics.PendingRequirementCount
                    + ", in-window " + diagnostics.WindowCandidateCount,
                    metricLabelStyle);
                GUILayout.Label(
                    "Nearest: " + EmptyAsDash(diagnostics.NearestRequirementId)
                    + " / " + diagnostics.NearestRequirementState
                    + " / " + diagnostics.NearestDeltaMilliseconds.ToString(
                        "+0.0;-0.0;0.0",
                        CultureInfo.InvariantCulture)
                    + " ms",
                    metricLabelStyle);
                GUILayout.Label(
                    "Rejected: " + diagnostics.NearestRejectionReason,
                    mutedLabelStyle);
            }
            GUILayout.EndArea();
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static float CalculateRightPanelWidth(float contentWidth)
        {
            float maximumWidthWithReadableMainArea = contentWidth - PanelGap - MainAreaMinWidth;
            if (maximumWidthWithReadableMainArea >= RightPanelMinWidth)
            {
                return Mathf.Min(RightPanelPreferredWidth, maximumWidthWithReadableMainArea);
            }

            return Mathf.Max(1f, Mathf.Min(RightPanelMinWidth, contentWidth * 0.45f));
        }

        private void DrawMainArea(Rect area)
        {
            DrawSolidRect(area, HudPanelColor);
            GUILayout.BeginArea(InsetRect(area, PanelPadding));
            eventListScroll = GUILayout.BeginScrollView(eventListScroll);

            GUILayout.Label("PulseForge Debug Rhythm Prototype", titleStyle);
            GUILayout.Label("Debug rhythm-combat HUD", mutedLabelStyle);
            GUILayout.Space(CardSpacing);
            DrawRuntimeSummary();

            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Rhythm Lane");
            Rect rhythmLaneRect = GUILayoutUtility.GetRect(1f, RhythmLaneHeight, GUILayout.ExpandWidth(true));
            rhythmLaneRenderer.Draw(
                rhythmLaneRect,
                radialSession != null || session == null ? null : session.Events,
                CurrentTimeSeconds);

            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Combat Debug");
            Rect combatPanelRect = GUILayoutUtility.GetRect(1f, DebugCombatFeedbackRenderer.PanelHeight, GUILayout.ExpandWidth(true));
            combatFeedbackRenderer.Draw(combatPanelRect, GetPresentationTimeSeconds(), IsSessionRunning);

            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Events");
            DrawEventList();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRightPanel(Rect area)
        {
            DrawSolidRect(area, HudPanelColor);
            GUILayout.BeginArea(InsetRect(area, PanelPadding));
            rightPanelScroll = GUILayout.BeginScrollView(rightPanelScroll);

            DrawSessionControls();
            DrawCustomSongPanel();
            DrawManualInputControls();
            DrawSourceInfo();
            DrawTimingCalibrationPanel();
            DrawLastFeedbackPanel();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawRuntimeSummary()
        {
            ScoreSnapshot snapshot = GetSnapshot();
            DrawCard(() =>
            {
                GUILayout.BeginHorizontal();
                DrawMetric("Status", GetSessionStatusText());
                DrawMetric("Score", snapshot.TotalScore.ToString(CultureInfo.InvariantCulture));
                DrawMetric(
                    "Combo",
                    snapshot.CurrentCombo.ToString(CultureInfo.InvariantCulture)
                    + " / "
                    + snapshot.MaxCombo.ToString(CultureInfo.InvariantCulture));
                GUILayout.EndHorizontal();

                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                DrawBadge("Perfect " + snapshot.PerfectCount.ToString(CultureInfo.InvariantCulture), PerfectColor);
                DrawBadge("Good " + snapshot.GoodCount.ToString(CultureInfo.InvariantCulture), GoodColor);
                DrawBadge("Miss " + snapshot.MissCount.ToString(CultureInfo.InvariantCulture), MissColor);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            });
        }

        private void DrawSessionControls()
        {
            DrawSectionTitle("Session Controls");
            DrawCard(() =>
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Start / Restart"))
                {
                    RestartSession();
                }

                bool previousGuiEnabled = GUI.enabled;
                GUI.enabled = CanTogglePause;
                string pauseButtonLabel = songClock != null && songClock.IsPaused ? "Resume" : "Pause";
                if (GUILayout.Button(pauseButtonLabel))
                {
                    TogglePause();
                }

                GUI.enabled = previousGuiEnabled;
                GUILayout.EndHorizontal();

                GUILayout.Label("Status: " + GetSessionStatusText(), mutedLabelStyle);
                if (isCountdownActive)
                {
                    GUILayout.Label("Starting in: " + FormatCountdownSeconds(GetCountdownRemainingSeconds()), metricValueStyle);
                }

                GUILayout.Label("Current time: " + FormatSeconds(CurrentTimeSeconds), mutedLabelStyle);
            });
        }

        private void DrawManualInputControls()
        {
            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Manual Inputs");
            DrawCard(() =>
            {
                bool previousGuiEnabled = GUI.enabled;
                GUI.enabled = IsSessionRunning;
                GUILayout.BeginHorizontal();
                if (DrawActionButton("Guard (Space)", GuardColor))
                {
                    HandleInput(RhythmAction.Guard);
                }

                if (DrawActionButton("Light Attack (J)", StrikeColor))
                {
                    HandleInput(RhythmAction.LightAttack);
                }

                GUILayout.EndHorizontal();
                GUI.enabled = previousGuiEnabled;
                GUILayout.Label("Space = Guard", mutedLabelStyle);
                GUILayout.Label("J = Strike", mutedLabelStyle);
                GUILayout.Label("Ambiguous input: " + GetAmbiguousInputStatusText(), mutedLabelStyle);
            });
        }

        private void DrawCustomSongPanel()
        {
            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Runtime Audio Pipeline");
            DrawCard(() =>
            {
                bool previousGuiEnabled = GUI.enabled;
                GUI.enabled = !isRuntimeAudioImportBusy;

                if (GUILayout.Button("Detection: " + runtimeDetectionMode))
                {
                    SetDetectionMode(runtimeDetectionMode == RuntimeDetectionMode.Onset
                        ? RuntimeDetectionMode.Amplitude
                        : RuntimeDetectionMode.Onset);
                }

                if (GUILayout.Button("Difficulty: " + runtimeDifficulty))
                {
                    SetDifficulty(GetNextDifficulty(runtimeDifficulty));
                }

                if (GUILayout.Button("Combat Style: " + runtimeCombatStyle))
                {
                    SetCombatStyle(GetNextCombatStyle(runtimeCombatStyle));
                }

                if (GUILayout.Button("Coverage: " + FormatCoverage(runtimeCoverage)))
                {
                    SetCoverage(GetNextCoverage(runtimeCoverage));
                }

                if (GUILayout.Button(isRuntimeAudioImportBusy ? "Running Pipeline..." : "Choose Audio & Run Pipeline"))
                {
                    BeginRuntimeAudioImport();
                }

                GUI.enabled = previousGuiEnabled;
                GUILayout.Label(runtimeAudioImportStatus, mutedLabelStyle);
                GUILayout.Label("Click a setting to cycle its value. The selected audio is converted to WAV and analyzed with these settings.", mutedLabelStyle);
                GUILayout.Label("After a successful pipeline run, press Start / Restart to begin.", mutedLabelStyle);
            });
        }

        private static RuntimeDifficulty GetNextDifficulty(RuntimeDifficulty current)
        {
            switch (current)
            {
                case RuntimeDifficulty.Easy:
                    return RuntimeDifficulty.Normal;
                case RuntimeDifficulty.Normal:
                    return RuntimeDifficulty.Hard;
                default:
                    return RuntimeDifficulty.Easy;
            }
        }

        private static RuntimeCombatStyle GetNextCombatStyle(RuntimeCombatStyle current)
        {
            switch (current)
            {
                case RuntimeCombatStyle.Legacy:
                    return RuntimeCombatStyle.Balanced;
                case RuntimeCombatStyle.Balanced:
                    return RuntimeCombatStyle.Defensive;
                case RuntimeCombatStyle.Defensive:
                    return RuntimeCombatStyle.Aggressive;
                case RuntimeCombatStyle.Aggressive:
                    return RuntimeCombatStyle.Bursty;
                default:
                    return RuntimeCombatStyle.Legacy;
            }
        }

        private static RuntimeCoverage GetNextCoverage(RuntimeCoverage current)
        {
            switch (current)
            {
                case RuntimeCoverage.Relaxed:
                    return RuntimeCoverage.Standard;
                case RuntimeCoverage.Standard:
                    return RuntimeCoverage.FullPulse;
                default:
                    return RuntimeCoverage.Relaxed;
            }
        }

        private static string FormatCoverage(RuntimeCoverage coverage)
        {
            return coverage == RuntimeCoverage.FullPulse ? "Full Pulse" : coverage.ToString();
        }

        public void SetDetectionMode(RuntimeDetectionMode detectionMode)
        {
            if (UIState == PulseForgeUIState.Setup && !isRuntimeAudioImportBusy)
            {
                if (runtimeDetectionMode != detectionMode)
                {
                    ClearActiveSavedPreset();
                }

                runtimeDetectionMode = detectionMode;
                SaveCurrentSettings();
            }
        }

        public void SetDifficulty(RuntimeDifficulty difficulty)
        {
            if (UIState == PulseForgeUIState.Setup && !isRuntimeAudioImportBusy)
            {
                if (runtimeDifficulty != difficulty)
                {
                    ClearActiveSavedPreset();
                }

                runtimeDifficulty = difficulty;
                SaveCurrentSettings();
            }
        }

        public void SetCombatStyle(RuntimeCombatStyle combatStyle)
        {
            if (UIState == PulseForgeUIState.Setup && !isRuntimeAudioImportBusy)
            {
                if (runtimeCombatStyle != combatStyle)
                {
                    ClearActiveSavedPreset();
                }

                runtimeCombatStyle = combatStyle;
                SaveCurrentSettings();
            }
        }

        public void SetCoverage(RuntimeCoverage coverage)
        {
            if ((UIState != PulseForgeUIState.Setup && UIState != PulseForgeUIState.Ready)
                || isRuntimeAudioImportBusy
                || runtimeCoverage == coverage)
            {
                return;
            }

            runtimeCoverage = coverage;
            ClearActiveSavedPreset();
            SaveCurrentSettings();
            if (UIState == PulseForgeUIState.Ready)
            {
                setupStatusMessage = "Coverage changed. Analyze the track again to prepare this preset.";
                ReturnToSetup(false);
            }
        }

        public void SetTimingAssist(TimingAssistMode timingAssist)
        {
            if ((UIState != PulseForgeUIState.Setup && UIState != PulseForgeUIState.Ready)
                || selectedTimingAssist == timingAssist)
            {
                return;
            }

            selectedTimingAssist = timingAssist;
            SaveCurrentSettings();
            if (UIState == PulseForgeUIState.Ready)
            {
                PrepareSession(false);
            }
        }

        public void SetGameMode(RadialGameMode gameMode)
        {
            if ((UIState == PulseForgeUIState.Setup || UIState == PulseForgeUIState.Ready)
                && !isRuntimeAudioImportBusy)
            {
                selectedGameMode = gameMode;
                SaveCurrentSettings();
            }
        }

        public void SetSaveSetupToLibrary(bool value)
        {
            if (UIState == PulseForgeUIState.Setup && !isRuntimeAudioImportBusy)
            {
                saveSetupToLibrary = value && HasSelectedAudio;
            }
        }

        public void OpenSavedTracks()
        {
            if (UIState != PulseForgeUIState.Setup || isRuntimeAudioImportBusy)
            {
                return;
            }

            EnsureSaveService();
            saveService.RefreshLibraryFileStates();
            savedTrackLibraryMessage = string.Empty;
            savedTrackLibraryRevision++;
            isSavedTracksOpen = true;
        }

        public void CloseSavedTracks()
        {
            isSavedTracksOpen = false;
        }

        public void SelectAudioFile()
        {
            if (isRuntimeAudioImportBusy)
            {
                return;
            }

            if (!WindowsAudioFilePicker.TryPickAudioFile(out string selectedPath, out string pickerError))
            {
                if (!string.IsNullOrEmpty(pickerError))
                {
                    runtimeAudioImportStatus = pickerError;
                    setupStatusMessage = pickerError;
                    lastFeedback = pickerError;
                    runtimeFlow.MarkError(pickerError);
                }

                return;
            }

            if (runtimeFlow.SelectAudioPath(selectedPath))
            {
                ResetLibrarySelectionForNewSong();
                runtimeAudioImportStatus = "Audio selected: " + Path.GetFileName(selectedPath);
                setupStatusMessage = "Song selected. Choose settings, then analyze.";
            }
        }

        public void AnalyzeSelectedAudio()
        {
            if (isRuntimeAudioImportBusy || !runtimeFlow.BeginProcessing())
            {
                return;
            }

            RuntimeAudioPipelineSettings settings = SelectedPipelineSettings;
            StartCoroutine(ImportRuntimeAudio(runtimeFlow.SelectedAudioPath, settings));
        }

        public void RetryAnalysis()
        {
            if (isRuntimeAudioImportBusy)
            {
                return;
            }

            if (!HasSelectedAudio)
            {
                runtimeFlow.ReturnToSetup(false);
                return;
            }

            AnalyzeSelectedAudio();
        }

        public void PlayBuiltInDemo()
        {
            if (!HasBuiltInDemo || isRuntimeAudioImportBusy)
            {
                return;
            }

            StopClock();
            isCountdownActive = false;
            ClearPendingInput();
            ResetLibrarySelectionForNewSong();
            useRuntimeSessionSource = false;
            if (PrepareSession(false))
            {
                runtimeFlow.MarkReady();
            }
        }

        public void StartSession()
        {
            if (CanStart)
            {
                RestartSession();
            }
        }

        public void RestartSession()
        {
            if (isRuntimeAudioImportBusy)
            {
                return;
            }

            runtimeFlow.MarkReady();
            PrepareSession(true);
        }

        public void PauseSession()
        {
            if (!CanPause)
            {
                return;
            }

            ClearPendingInput();
            songClock.Pause();
            runtimeFlow.Pause();
            lastFeedback = "Session paused";
        }

        public void ResumeSession()
        {
            if (UIState != PulseForgeUIState.Paused || songClock == null || !songClock.IsPaused)
            {
                return;
            }

            ClearPendingInput();
            songClock.Resume();
            runtimeFlow.Resume();
            lastFeedback = "Session resumed";
        }

        public void RequestGuard()
        {
            HandleInput(RhythmAction.Guard);
        }

        public void RequestStrike()
        {
            HandleInput(RhythmAction.LightAttack);
        }

        public void ChangeSettings()
        {
            ReturnToSetup(false);
        }

        public void ChooseAnotherSong()
        {
            ReturnToSetup(true);
        }

        public void LoadSavedTrackPreset(string trackId, string presetId)
        {
            if (isRuntimeAudioImportBusy || UIState != PulseForgeUIState.Setup)
            {
                return;
            }

            EnsureSaveService();
            if (!saveService.TryGetCachedPreset(
                trackId,
                presetId,
                out SavedTrackCacheLoadData loadData,
                out string cacheError))
            {
                saveService.MarkPresetCacheDamaged(trackId, presetId);
                savedTrackLibraryMessage = "Saved track cache is missing or damaged."
                    + (string.IsNullOrWhiteSpace(cacheError) ? string.Empty : " " + cacheError);
                savedTrackLibraryRevision++;
                return;
            }

            StartCoroutine(LoadSavedTrackFromCache(loadData));
        }

        public void RebuildSavedTrackPreset(string trackId, string presetId)
        {
            if (isRuntimeAudioImportBusy || UIState != PulseForgeUIState.Setup)
            {
                return;
            }

            EnsureSaveService();
            if (!saveService.TryGetPreset(
                trackId,
                presetId,
                out SavedTrackData track,
                out SavedTrackPresetData preset,
                out RuntimeAudioPipelineSettings settings))
            {
                return;
            }

            runtimeDetectionMode = settings.DetectionMode;
            runtimeDifficulty = settings.Difficulty;
            runtimeCombatStyle = settings.CombatStyle;
            runtimeCoverage = settings.Coverage;
            activeSavedTrackId = track.trackId;
            activeSavedPresetId = preset.presetId;

            if (saveService.TryGetCachedAudioForRebuild(trackId, out string cachedAudioPath))
            {
                string flowPath = string.IsNullOrWhiteSpace(track.originalFilePath)
                    ? cachedAudioPath
                    : track.originalFilePath;
                if (!runtimeFlow.SelectAudioPath(flowPath) || !runtimeFlow.BeginProcessing())
                {
                    return;
                }
                isSavedTracksOpen = false;
                StartCoroutine(RebuildSavedTrackFromCachedWav(
                    track,
                    preset,
                    settings,
                    cachedAudioPath));
                return;
            }

            if (track.fileMissing || string.IsNullOrWhiteSpace(track.originalFilePath)
                || !File.Exists(track.originalFilePath))
            {
                savedTrackLibraryMessage = "Saved track cache is missing or damaged. Relink the source file before rebuilding.";
                savedTrackLibraryRevision++;
                return;
            }

            if (!runtimeFlow.SelectAudioPath(track.originalFilePath))
            {
                return;
            }
            if (!runtimeFlow.BeginProcessing())
            {
                return;
            }

            saveSetupToLibrary = true;
            isSavedTracksOpen = false;
            runtimeAudioImportStatus = "Audio selected: " + Path.GetFileName(track.originalFilePath);
            setupStatusMessage = "Rebuilding saved track from its source audio.";
            savedTrackLibraryMessage = string.Empty;
            SaveCurrentSettings();
            StartCoroutine(ImportRuntimeAudio(track.originalFilePath, settings));
        }

        public bool CanRebuildSavedTrackWithoutSource(string trackId)
        {
            EnsureSaveService();
            return saveService.TryGetCachedAudioForRebuild(trackId, out _);
        }

        private IEnumerator RebuildSavedTrackFromCachedWav(
            SavedTrackData track,
            SavedTrackPresetData preset,
            RuntimeAudioPipelineSettings settings,
            string cachedAudioPath)
        {
            isRuntimeAudioImportBusy = true;
            runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.LoadingConvertedAudio);
            savedTrackLibraryMessage = "Loading cached audio for rebuild";
            savedTrackLibraryRevision++;
            AudioClip loadedClip = null;
            string loadError = string.Empty;
            yield return RuntimeAudioImportService.LoadCachedWav(
                cachedAudioPath,
                track.displayName,
                clip => loadedClip = clip,
                error => loadError = error);
            if (loadedClip == null || !string.IsNullOrWhiteSpace(loadError))
            {
                isRuntimeAudioImportBusy = false;
                savedTrackLibraryMessage = "Cached audio is damaged; relink the source file to rebuild.";
                runtimeFlow.ReturnToSetup(false);
                isSavedTracksOpen = true;
                savedTrackLibraryRevision++;
                yield break;
            }

            RuntimeAudioImportResult rebuildResult = null;
            string rebuildError = string.Empty;
            yield return RuntimeAudioImportService.AnalyzeLoadedAudio(
                loadedClip,
                track.originalFilePath,
                cachedAudioPath,
                track.displayName,
                settings,
                SetRuntimeAudioImportStatus,
                result => rebuildResult = result,
                error => rebuildError = error,
                track.trackId,
                false);
            if (rebuildResult == null || !string.IsNullOrWhiteSpace(rebuildError))
            {
                Destroy(loadedClip);
                isRuntimeAudioImportBusy = false;
                savedTrackLibraryMessage = string.IsNullOrWhiteSpace(rebuildError)
                    ? "Saved track rebuild failed."
                    : rebuildError;
                runtimeFlow.ReturnToSetup(false);
                isSavedTracksOpen = true;
                savedTrackLibraryRevision++;
                yield break;
            }

            StopClock();
            if (runtimeAudioClip != null && runtimeAudioClip != loadedClip)
            {
                Destroy(runtimeAudioClip);
            }
            runtimeAudioClip = loadedClip;
            runtimeBeatEvents = null;
            runtimeRadialBeatMap = rebuildResult.RadialBeatMap;
            runtimeBeatGrid = rebuildResult.BeatGrid;
            runtimeAnalyzerQuality = rebuildResult.AnalyzerQuality;
            runtimePlannerQuality = rebuildResult.PlannerQuality;
            runtimeAudioDisplayName = track.displayName;
            appliedRuntimePipelineSettings = settings;
            useRuntimeSessionSource = true;
            if (!PrepareSession(false)
                || !saveService.TrySaveRebuiltPreset(
                    track.trackId,
                    preset.presetId,
                    settings,
                    cachedAudioPath,
                    runtimeRadialBeatMap,
                    runtimeAnalyzerQuality,
                    runtimePlannerQuality,
                    runtimeBeatGrid,
                    out runtimeBeatMapFingerprint))
            {
                isRuntimeAudioImportBusy = false;
                savedTrackLibraryMessage = "Saved track rebuild could not be prepared or cached.";
                runtimeFlow.ReturnToSetup(false);
                isSavedTracksOpen = true;
                savedTrackLibraryRevision++;
                yield break;
            }

            isRuntimeAudioImportBusy = false;
            saveSetupToLibrary = false;
            isSavedTracksOpen = false;
            savedTrackLibraryMessage = string.Empty;
            runtimeAudioImportStatus = "Ready";
            setupStatusMessage = runtimePlannerQuality != null
                && runtimePlannerQuality.result == PlannerQualityResult.UnderCovered
                ? "Saved track rebuilt with limited coverage."
                : "Saved track rebuilt from cached audio.";
            savedTrackLibraryRevision++;
            runtimeFlow.MarkReady();
        }

        public void RelinkSavedTrack(string trackId)
        {
            if (isRuntimeAudioImportBusy || UIState != PulseForgeUIState.Setup)
            {
                return;
            }

            if (!WindowsAudioFilePicker.TryPickAudioFile(out string selectedPath, out string pickerError))
            {
                if (!string.IsNullOrEmpty(pickerError))
                {
                    Debug.LogWarning("PulseForge relink picker: " + pickerError);
                }

                return;
            }

            EnsureSaveService();
            if (!saveService.TryRelinkTrack(trackId, selectedPath, out string errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    Debug.LogWarning("PulseForge library relink: " + errorMessage);
                }

                return;
            }

            savedTrackLibraryRevision++;
        }

        public void RemoveSavedTrack(string trackId)
        {
            if (isRuntimeAudioImportBusy || UIState != PulseForgeUIState.Setup)
            {
                return;
            }

            EnsureSaveService();
            if (saveService.RemoveTrack(trackId))
            {
                if (string.Equals(activeSavedTrackId, trackId, StringComparison.OrdinalIgnoreCase))
                {
                    ClearActiveSavedPreset();
                }

                savedTrackLibraryRevision++;
            }
        }

        public void RemoveSavedPreset(string trackId, string presetId)
        {
            if (isRuntimeAudioImportBusy || UIState != PulseForgeUIState.Setup)
            {
                return;
            }

            EnsureSaveService();
            if (saveService.RemovePreset(trackId, presetId))
            {
                if (string.Equals(activeSavedTrackId, trackId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(activeSavedPresetId, presetId, StringComparison.Ordinal))
                {
                    ClearActiveSavedPreset();
                }

                savedTrackLibraryRevision++;
            }
        }

        public void SetMotionEnabled(bool value)
        {
            sceneUIRoot?.SetEnableMotion(value);
            EnsureSaveService();
            saveService.Settings.enableMotion = value;
            SaveCurrentSettings();
        }

        public void ResetSettings()
        {
            EnsureSaveService();
            saveService.ResetSettings();
            ApplyLoadedSettings();
            ApplyLoadedMotionSetting();
        }

        public void ResetProfile()
        {
            EnsureSaveService();
            saveService.ResetProfile();
        }

        public void ClearSavedTrackLibrary()
        {
            EnsureSaveService();
            saveService.ClearSavedTrackLibrary();
            ClearActiveSavedPreset();
            savedTrackLibraryRevision++;
        }

        private void ReturnToSetup(bool clearSelectedAudio)
        {
            StopClock();
            radialStatusEffects.Reset();
            isCountdownActive = false;
            ClearPendingInput();
            isSavedTracksOpen = false;
            runtimeFlow.ReturnToSetup(clearSelectedAudio);
            if (clearSelectedAudio)
            {
                ResetLibrarySelectionForNewSong();
            }
            setupStatusMessage = clearSelectedAudio
                ? "Choose a song, select your settings, then analyze."
                : HasSelectedAudio
                    ? "Adjust settings, then analyze the song again."
                    : "Choose a song, select your settings, then analyze.";
        }

        private void DrawSourceInfo()
        {
            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Source Info");
            DrawCard(() =>
            {
                GUILayout.Label("Active clock: " + GetClockName(), mutedLabelStyle);
                GUILayout.Label("Audio clip: " + GetAudioClipStatus(), mutedLabelStyle);
                GUILayout.Label("Beatmap source:", mutedLabelStyle);
                GUILayout.Label(GetBeatMapSourceName(), metricValueStyle);
                GUILayout.Label("Event count: " + GetEventCountText(), mutedLabelStyle);
            });
        }

        private void DrawLastFeedbackPanel()
        {
            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Last Feedback");
            DrawCard(() =>
            {
                GUILayout.Label(lastFeedback, metricValueStyle);
                GUILayout.Label("Last timing: " + GetLastInputTimingErrorText(), mutedLabelStyle);
            });
        }

        private void EnsureHudStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = TextPrimaryColor;

            sectionTitleStyle = new GUIStyle(GUI.skin.label);
            sectionTitleStyle.fontSize = 13;
            sectionTitleStyle.fontStyle = FontStyle.Bold;
            sectionTitleStyle.normal.textColor = TextPrimaryColor;

            mutedLabelStyle = new GUIStyle(GUI.skin.label);
            mutedLabelStyle.normal.textColor = TextMutedColor;
            mutedLabelStyle.wordWrap = true;

            metricLabelStyle = new GUIStyle(GUI.skin.label);
            metricLabelStyle.fontSize = 10;
            metricLabelStyle.normal.textColor = TextMutedColor;

            metricValueStyle = new GUIStyle(GUI.skin.label);
            metricValueStyle.fontSize = 14;
            metricValueStyle.fontStyle = FontStyle.Bold;
            metricValueStyle.normal.textColor = TextPrimaryColor;
            metricValueStyle.wordWrap = true;

            badgeStyle = new GUIStyle(GUI.skin.box);
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = Color.black;
        }

        private void DrawSectionTitle(string title)
        {
            GUILayout.Label(title, sectionTitleStyle);
        }

        private void DrawCard(Action drawContent)
        {
            Color previousColor = GUI.color;
            GUI.color = CardColor;
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.color = previousColor;
            GUILayout.Space(2f);
            drawContent();
            GUILayout.Space(2f);
            GUILayout.EndVertical();
        }

        private void DrawMetric(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(86f));
            GUILayout.Label(label, metricLabelStyle);
            GUILayout.Label(value, metricValueStyle);
            GUILayout.EndVertical();
        }

        private void DrawActionAccent(RhythmAction action)
        {
            Rect rect = GUILayoutUtility.GetRect(5f, 20f, GUILayout.Width(5f));
            DrawSolidRect(rect, GetActionColor(action));
        }

        private void DrawActionBadge(RhythmAction action)
        {
            string text = action == RhythmAction.Guard ? "Guard" : "Strike";
            DrawBadge(text, GetActionColor(action), GUILayout.Width(62f));
        }

        private void DrawStateBadge(BeatEventState state)
        {
            DrawBadge(state.ToString(), GetStateColor(state), GUILayout.Width(66f));
        }

        private void DrawBadge(string text, Color color, params GUILayoutOption[] options)
        {
            Color previousColor = GUI.color;
            Color previousContentColor = GUI.contentColor;
            GUI.color = color;
            GUI.contentColor = Color.black;
            GUILayout.Box(text, badgeStyle, options);
            GUI.color = previousColor;
            GUI.contentColor = previousContentColor;
        }

        private bool DrawActionButton(string label, Color color)
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            GUI.backgroundColor = color;
            GUI.contentColor = Color.black;
            bool clicked = GUILayout.Button(label);
            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;
            return clicked;
        }

        private static Rect InsetRect(Rect rect, float inset)
        {
            return new Rect(
                rect.x + inset,
                rect.y + inset,
                Mathf.Max(1f, rect.width - inset * 2f),
                Mathf.Max(1f, rect.height - inset * 2f));
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static Color GetActionColor(RhythmAction action)
        {
            return action == RhythmAction.Guard ? GuardColor : StrikeColor;
        }

        private static Color GetStateColor(BeatEventState state)
        {
            switch (state)
            {
                case BeatEventState.Hit:
                    return GoodColor;
                case BeatEventState.Missed:
                    return MissColor;
                default:
                    return PendingColor;
            }
        }

        private string GetSessionStatusText()
        {
            if (isCountdownActive)
            {
                return "Waiting";
            }

            if (IsActiveSessionComplete)
            {
                return "Complete";
            }

            if (songClock != null && songClock.IsPaused)
            {
                return "Paused";
            }

            if (IsSessionRunning)
            {
                return "Running";
            }

            return "Waiting";
        }

        private string GetAmbiguousInputStatusText()
        {
            if (!rejectAmbiguousSimultaneousActions)
            {
                return "Off";
            }

            return "On ("
                + (Mathf.Max(0f, simultaneousInputWindowSeconds) * 1000f).ToString("0", CultureInfo.InvariantCulture)
                + " ms)";
        }

        private bool PrepareSession(bool startAfterPreparation)
        {
            ResetCombatSceneView();

            try
            {
                if (useRuntimeSessionSource && runtimeRadialBeatMap != null)
                {
                    activeSessionUsesRadialV2ScoreSchema = true;
                    activeBeatGrid = runtimeBeatGrid;
                    activeRadialBeatMapOffsetSeconds = runtimeRadialBeatMap.globalOffsetSeconds
                        + debugBeatMapOffsetSeconds;
                    radialSession = new RadialRhythmSession(
                        runtimeRadialBeatMap.encounters,
                        selectedTimingAssist,
                        activeRadialBeatMapOffsetSeconds);
                    session = null;
                }
                else
                {
                    RadialBeatMapData radialBeatMap = CreateConfiguredRadialBeatMap(
                        out BeatGridData configuredBeatGrid);
                    if (radialBeatMap != null && radialBeatMap.encounters.Count > 0)
                    {
                        activeSessionUsesRadialV2ScoreSchema = debugRadialBeatMapJson != null;
                        activeBeatGrid = configuredBeatGrid;
                        activeRadialBeatMapOffsetSeconds = radialBeatMap.globalOffsetSeconds
                            + debugBeatMapOffsetSeconds;
                        radialSession = new RadialRhythmSession(
                            radialBeatMap.encounters,
                            selectedTimingAssist,
                            activeRadialBeatMapOffsetSeconds);
                        session = null;
                    }
                    else
                    {
                        activeSessionUsesRadialV2ScoreSchema = false;
                        session = new RhythmSession(
                            CreateSessionBeatEvents(),
                            new JudgementWindows(PerfectWindowSeconds, GoodWindowSeconds),
                            new RhythmInputResolver(new BeatEventMatcher(), new HitJudge()),
                            new BeatEventTimeoutProcessor(new HitJudge()));
                        radialSession = null;
                        activeBeatGrid = null;
                        activeRadialBeatMapOffsetSeconds = 0d;
                    }
                }
                scoreTracker = new ScoreTracker();
                radialInputSequence = 0L;
                hasTimingAudit = false;
                scoredRadialUnits.Clear();
                completedSessionRecorded = false;
                radialRunPolicy.Reset(selectedGameMode);
                radialStatusEffects.Reset();
                runDamageRevision = 0;
                combatFeedbackRenderer.Clear();
                ClearPendingInput();
                ClearLastInputTimingError();
                PrepareClockForCountdown();
                if (startAfterPreparation)
                {
                    StartCountdownOrGameplay();
                }
                else
                {
                    isCountdownActive = false;
                    lastFeedback = "Press Start / Restart";
                }

                RefreshCombatSceneVisibility();
                GameplaySessionRestarted?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                StopClock();
                isCountdownActive = false;
                ClearPendingInput();
                session = null;
                radialSession = null;
                activeBeatGrid = null;
                activeRadialBeatMapOffsetSeconds = 0d;
                hasTimingAudit = false;
                radialStatusEffects.Reset();
                activeSessionUsesRadialV2ScoreSchema = false;
                scoreTracker = new ScoreTracker();
                combatFeedbackRenderer.Clear();
                ClearLastInputTimingError();
                lastFeedback = FormatBeatMapError(exception);
                runtimeFlow.MarkError(lastFeedback);
                return false;
            }
        }

        private IReadOnlyList<BeatEventData> CreateSessionBeatEvents()
        {
            return ApplyDebugBeatMapOffset(CreateUnshiftedSessionBeatEvents());
        }

        private IReadOnlyList<BeatEventData> CreateUnshiftedSessionBeatEvents()
        {
            IReadOnlyList<BeatEventData> beatEvents;
            if (useRuntimeSessionSource && runtimeBeatEvents != null)
            {
                beatEvents = runtimeBeatEvents;
            }
            else if (debugBeatMapJson != null)
            {
                beatEvents = DebugBeatMapJsonParser.BuildBeatEvents(debugBeatMapJson.text);
            }
            else if (debugBeatMapAsset != null)
            {
                beatEvents = debugBeatMapAsset.BuildBeatEvents();
            }
            else
            {
                beatEvents = CreateDebugEventData();
            }

            return beatEvents;
        }

        private RadialBeatMapData CreateConfiguredRadialBeatMap()
        {
            return CreateConfiguredRadialBeatMap(out _);
        }

        private RadialBeatMapData CreateConfiguredRadialBeatMap(out BeatGridData beatGrid)
        {
            beatGrid = null;
            if (useDeterministicTimingFixture)
            {
                return RadialTimingFixture.Create();
            }
            if (debugRadialBeatMapJson != null)
            {
                if (!RadialBeatMapArtifactSerializer.TryDeserialize(
                    debugRadialBeatMapJson.text,
                    out RadialBeatMapCacheData artifact,
                    out string errorMessage))
                {
                    throw new FormatException(errorMessage);
                }
                beatGrid = artifact.beatGrid;
                return artifact.radialBeatMap;
            }

            IReadOnlyList<BeatEventData> legacyEvents = CreateUnshiftedSessionBeatEvents();
            return legacyEvents == null || legacyEvents.Count == 0
                ? null
                : LegacyBeatMapRadialAdapter.Convert(legacyEvents);
        }

        private static RadialBeatMapData ParseRadialBeatMapJson(string json)
        {
            if (!RadialBeatMapArtifactSerializer.TryDeserialize(
                json,
                out RadialBeatMapCacheData artifact,
                out string errorMessage))
            {
                throw new FormatException(errorMessage);
            }
            return artifact.radialBeatMap;
        }

        private IReadOnlyList<BeatEventData> ApplyDebugBeatMapOffset(IReadOnlyList<BeatEventData> beatEvents)
        {
            if (beatEvents == null || beatEvents.Count == 0)
            {
                return beatEvents ?? Array.Empty<BeatEventData>();
            }

            double offsetSeconds = debugBeatMapOffsetSeconds;
            if (double.IsNaN(offsetSeconds) || double.IsInfinity(offsetSeconds))
            {
                throw new ArgumentException("Debug beat map offset must be finite.");
            }

            if (offsetSeconds == 0d)
            {
                return beatEvents;
            }

            BeatEventData[] adjustedEvents = new BeatEventData[beatEvents.Count];
            for (int i = 0; i < beatEvents.Count; i++)
            {
                BeatEventData beatEvent = beatEvents[i];
                double targetTimeSeconds = beatEvent.TargetTimeSeconds + offsetSeconds;
                if (targetTimeSeconds < 0d)
                {
                    targetTimeSeconds = 0d;
                }

                adjustedEvents[i] = new BeatEventData(
                    beatEvent.EventId,
                    targetTimeSeconds,
                    beatEvent.Action,
                    beatEvent.Intensity);
            }

            return adjustedEvents;
        }

        private void PrepareClockForCountdown()
        {
            StopClock();
            songClock = CreateSongClock();
        }

        private void StartCountdownOrGameplay()
        {
            countdownDurationSeconds = Mathf.Max(0f, startCountdownSeconds);
            countdownStartedAtSeconds = GetPresentationTimeSeconds();
            ClearPendingInput();
            runtimeFlow.BeginSession(countdownDurationSeconds > 0d);

            if (countdownDurationSeconds <= 0d)
            {
                StartGameplayClock();
                return;
            }

            isCountdownActive = true;
            lastFeedback = "Starting in " + FormatCountdownSeconds(countdownDurationSeconds);
        }

        private void UpdateCountdown()
        {
            if (GetCountdownRemainingSeconds() > 0d)
            {
                return;
            }

            StartGameplayClock();
        }

        private void StartGameplayClock()
        {
            isCountdownActive = false;
            ClearPendingInput();

            if (songClock == null)
            {
                songClock = CreateSongClock();
            }

            songClock.Start();
            runtimeFlow.MarkPlaying();
            lastFeedback = "Session started";
            StopIfComplete();
        }

        private void LateUpdate()
        {
            PublishGameplayStateIfChanged();
            RefreshCombatSceneVisibility();
        }

        private void StopClock()
        {
            if (songClock != null)
            {
                songClock.Stop();
            }
        }

        private ISongClock CreateSongClock()
        {
            AudioClip activeAudioClip = GetActiveAudioClip();
            if (useAudioClockWhenClipAssigned && activeAudioClip != null)
            {
                return new DspAudioSongClock(GetOrAddAudioSource(), activeAudioClip);
            }

            return new RealtimeSongClock();
        }

        private AudioSource GetOrAddAudioSource()
        {
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (saveService != null && saveService.Settings != null && saveService.Settings.audio != null)
            {
                audioSource.volume = saveService.Settings.audio.musicVolume;
            }

            return audioSource;
        }

        private bool CanTogglePause
        {
            get
            {
                return !isCountdownActive
                    && HasActiveSession
                    && !IsActiveSessionComplete
                    && songClock != null
                    && (songClock.IsRunning || songClock.IsPaused);
            }
        }

        private void TogglePause()
        {
            if (!CanTogglePause)
            {
                return;
            }

            if (songClock.IsPaused)
            {
                ResumeSession();
            }
            else
            {
                PauseSession();
            }
        }

        private AudioClip GetActiveAudioClip()
        {
            return useRuntimeSessionSource && runtimeAudioClip != null
                ? runtimeAudioClip
                : debugAudioClip;
        }

        private void BeginRuntimeAudioImport()
        {
            if (isRuntimeAudioImportBusy)
            {
                return;
            }

            if (!WindowsAudioFilePicker.TryPickAudioFile(out string selectedPath, out string pickerError))
            {
                if (!string.IsNullOrEmpty(pickerError))
                {
                    runtimeAudioImportStatus = pickerError;
                    lastFeedback = pickerError;
                }

                return;
            }

            if (runtimeFlow.SelectAudioPath(selectedPath))
            {
                ResetLibrarySelectionForNewSong();
                AnalyzeSelectedAudio();
            }
        }

        private IEnumerator ImportRuntimeAudio(
            string sourcePath,
            RuntimeAudioPipelineSettings pipelineSettings)
        {
            isRuntimeAudioImportBusy = true;
            runtimeAudioImportStatus = "Audio selected";
            runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.AudioSelected);
            yield return null;

            RuntimeAudioImportResult importResult = null;
            string importError = string.Empty;
            yield return RuntimeAudioImportService.ImportAudio(
                sourcePath,
                pipelineSettings,
                SetRuntimeAudioImportStatus,
                result => importResult = result,
                error => importError = error);

            isRuntimeAudioImportBusy = false;
            if (!string.IsNullOrEmpty(importError) || importResult == null)
            {
                runtimeAudioImportStatus = string.IsNullOrEmpty(importError)
                    ? "Audio import did not return a result."
                    : importError;
                lastFeedback = runtimeAudioImportStatus;
                runtimeFlow.MarkError(runtimeAudioImportStatus);
                yield break;
            }

            StopClock();
            AudioClip previousRuntimeAudioClip = runtimeAudioClip;
            IReadOnlyList<BeatEventData> previousRuntimeBeatEvents = runtimeBeatEvents;
            RadialBeatMapData previousRuntimeRadialBeatMap = runtimeRadialBeatMap;
            BeatGridData previousRuntimeBeatGrid = runtimeBeatGrid;
            AnalyzerQualityReport previousAnalyzerQuality = runtimeAnalyzerQuality;
            PlannerQualityReport previousPlannerQuality = runtimePlannerQuality;
            string previousFingerprint = runtimeBeatMapFingerprint;
            string previousRuntimeAudioDisplayName = runtimeAudioDisplayName;
            RuntimeAudioPipelineSettings previousAppliedSettings = appliedRuntimePipelineSettings;
            bool previousUseRuntimeSessionSource = useRuntimeSessionSource;
            runtimeAudioClip = importResult.AudioClip;
            runtimeBeatEvents = null;
            runtimeRadialBeatMap = importResult.RadialBeatMap;
            runtimeBeatGrid = importResult.BeatGrid;
            runtimeAnalyzerQuality = importResult.AnalyzerQuality;
            runtimePlannerQuality = importResult.PlannerQuality;
            runtimeBeatMapFingerprint = RadialBeatMapFingerprint.Compute(runtimeRadialBeatMap);
            runtimeAudioDisplayName = importResult.DisplayName;
            appliedRuntimePipelineSettings = pipelineSettings;
            useRuntimeSessionSource = true;

            if (!PrepareSession(false))
            {
                string preparationError = lastFeedback;
                runtimeAudioClip = previousRuntimeAudioClip;
                runtimeBeatEvents = previousRuntimeBeatEvents;
                runtimeRadialBeatMap = previousRuntimeRadialBeatMap;
                runtimeBeatGrid = previousRuntimeBeatGrid;
                runtimeAnalyzerQuality = previousAnalyzerQuality;
                runtimePlannerQuality = previousPlannerQuality;
                runtimeBeatMapFingerprint = previousFingerprint;
                runtimeAudioDisplayName = previousRuntimeAudioDisplayName;
                appliedRuntimePipelineSettings = previousAppliedSettings;
                useRuntimeSessionSource = previousUseRuntimeSessionSource;
                Destroy(importResult.AudioClip);
                PrepareSession(false);
                runtimeAudioImportStatus = preparationError;
                lastFeedback = preparationError;
                runtimeFlow.MarkError(preparationError);
                yield break;
            }

            if (previousRuntimeAudioClip != null && previousRuntimeAudioClip != runtimeAudioClip)
            {
                Destroy(previousRuntimeAudioClip);
            }

            runtimeAudioImportStatus = "Ready";
            runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.Ready);
            yield return null;
            runtimeFlow.MarkReady();
            setupStatusMessage = runtimePlannerQuality != null
                && runtimePlannerQuality.result == PlannerQualityResult.UnderCovered
                ? "Song is ready with limited coverage."
                : "Song analyzed and ready to play.";
            if (saveSetupToLibrary)
            {
                SaveAnalyzedTrackToLibrary(
                    importResult.SourcePath,
                    importResult.ConvertedWavPath,
                    pipelineSettings);
            }
        }

        private IEnumerator LoadSavedTrackFromCache(SavedTrackCacheLoadData loadData)
        {
            if (loadData == null)
            {
                yield break;
            }

            isRuntimeAudioImportBusy = true;
            savedTrackLibraryMessage = "Loading Saved Track";
            savedTrackLibraryRevision++;
            AudioClip loadedClip = null;
            string loadError = string.Empty;
            yield return RuntimeAudioImportService.LoadCachedWav(
                loadData.CachedAudioPath,
                loadData.Track.displayName,
                clip => loadedClip = clip,
                error => loadError = error);

            if (loadedClip == null || !string.IsNullOrWhiteSpace(loadError))
            {
                isRuntimeAudioImportBusy = false;
                saveService.MarkPresetCacheDamaged(
                    loadData.Track.trackId,
                    loadData.Preset.presetId);
                savedTrackLibraryMessage = "Saved track cache is missing or damaged."
                    + (string.IsNullOrWhiteSpace(loadError) ? string.Empty : " " + loadError);
                savedTrackLibraryRevision++;
                yield break;
            }

            StopClock();
            AudioClip previousRuntimeAudioClip = runtimeAudioClip;
            IReadOnlyList<BeatEventData> previousRuntimeBeatEvents = runtimeBeatEvents;
            RadialBeatMapData previousRuntimeRadialBeatMap = runtimeRadialBeatMap;
            BeatGridData previousRuntimeBeatGrid = runtimeBeatGrid;
            AnalyzerQualityReport previousAnalyzerQuality = runtimeAnalyzerQuality;
            PlannerQualityReport previousPlannerQuality = runtimePlannerQuality;
            string previousFingerprint = runtimeBeatMapFingerprint;
            string previousRuntimeAudioDisplayName = runtimeAudioDisplayName;
            RuntimeAudioPipelineSettings previousAppliedSettings = appliedRuntimePipelineSettings;
            bool previousUseRuntimeSessionSource = useRuntimeSessionSource;
            runtimeAudioClip = loadedClip;
            runtimeBeatEvents = null;
            runtimeRadialBeatMap = loadData.RadialBeatMap;
            runtimeBeatGrid = loadData.BeatGrid;
            runtimeAnalyzerQuality = loadData.AnalyzerQuality;
            runtimePlannerQuality = loadData.PlannerQuality;
            runtimeBeatMapFingerprint = loadData.BeatMapFingerprint;
            runtimeAudioDisplayName = loadData.Track.displayName;
            appliedRuntimePipelineSettings = loadData.Settings;
            useRuntimeSessionSource = true;

            if (!PrepareSession(false))
            {
                string preparationError = lastFeedback;
                runtimeAudioClip = previousRuntimeAudioClip;
                runtimeBeatEvents = previousRuntimeBeatEvents;
                runtimeRadialBeatMap = previousRuntimeRadialBeatMap;
                runtimeBeatGrid = previousRuntimeBeatGrid;
                runtimeAnalyzerQuality = previousAnalyzerQuality;
                runtimePlannerQuality = previousPlannerQuality;
                runtimeBeatMapFingerprint = previousFingerprint;
                runtimeAudioDisplayName = previousRuntimeAudioDisplayName;
                appliedRuntimePipelineSettings = previousAppliedSettings;
                useRuntimeSessionSource = previousUseRuntimeSessionSource;
                Destroy(loadedClip);
                PrepareSession(false);
                runtimeFlow.ReturnToSetup(false);
                isSavedTracksOpen = true;
                isRuntimeAudioImportBusy = false;
                saveService.MarkPresetCacheDamaged(
                    loadData.Track.trackId,
                    loadData.Preset.presetId);
                savedTrackLibraryMessage = "Saved track cache is missing or damaged. "
                    + preparationError;
                savedTrackLibraryRevision++;
                yield break;
            }

            if (previousRuntimeAudioClip != null && previousRuntimeAudioClip != runtimeAudioClip)
            {
                Destroy(previousRuntimeAudioClip);
            }

            if (!string.IsNullOrWhiteSpace(loadData.Track.originalFilePath))
            {
                runtimeFlow.SelectAudioPath(loadData.Track.originalFilePath);
            }

            runtimeDetectionMode = loadData.Settings.DetectionMode;
            runtimeDifficulty = loadData.Settings.Difficulty;
            runtimeCombatStyle = loadData.Settings.CombatStyle;
            runtimeCoverage = loadData.Settings.Coverage;
            activeSavedTrackId = loadData.Track.trackId;
            activeSavedPresetId = loadData.Preset.presetId;
            saveSetupToLibrary = false;
            isSavedTracksOpen = false;
            isRuntimeAudioImportBusy = false;
            savedTrackLibraryMessage = string.Empty;
            runtimeAudioImportStatus = "Ready";
            setupStatusMessage = "Saved track loaded from cache.";
            saveService.MarkTrackUsed(loadData.Track.trackId);
            savedTrackLibraryRevision++;
            SaveCurrentSettings();
            runtimeFlow.MarkReady();
        }

        private void SetRuntimeAudioImportStatus(string status)
        {
            runtimeAudioImportStatus = status ?? string.Empty;
            if (runtimeAudioImportStatus.IndexOf("Converting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.ConvertingToWav);
            }
            else if (runtimeAudioImportStatus.IndexOf("Loading", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.LoadingConvertedAudio);
            }
            else if (runtimeAudioImportStatus.IndexOf("Analyzing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.AnalyzingAudioFeatures);
            }
            else if (runtimeAudioImportStatus.IndexOf("Planning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.PlanningRadialEncounters);
            }
            else if (runtimeAudioImportStatus.IndexOf("Validating", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.ValidatingBeatMap);
            }
            else if (runtimeAudioImportStatus.IndexOf("Preparing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.PreparingSession);
            }
            else if (runtimeAudioImportStatus.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.Ready);
            }
        }

        private void HandleRuntimeKeyboardInput(double frameSongTimeSeconds)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                legacyDebugOverlayVisible = !legacyDebugOverlayVisible;
            }
            if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
            {
                timingAuditVisible = !timingAuditVisible;
            }

            EnsureInputService();
            if (isSettingsOpen || inputService == null)
            {
                return;
            }

            if (inputService.PauseWasPressedThisFrame)
            {
                if (UIState == PulseForgeUIState.Playing)
                {
                    PauseSession();
                }
                else if (UIState == PulseForgeUIState.Paused)
                {
                    ResumeSession();
                }
            }

            if (UIState != PulseForgeUIState.Playing)
            {
                return;
            }

            if (radialSession != null)
            {
                ForwardRadialInput(
                    RhythmAction.Guard,
                    inputService.GuardWasPressedThisFrame,
                    inputService.GuardWasReleasedThisFrame,
                    frameSongTimeSeconds);
                ForwardRadialInput(
                    RhythmAction.LightAttack,
                    inputService.LightAttackWasPressedThisFrame,
                    inputService.LightAttackWasReleasedThisFrame,
                    frameSongTimeSeconds);
                ForwardRadialInput(
                    RhythmAction.Dodge,
                    inputService.DodgeWasPressedThisFrame,
                    inputService.DodgeWasReleasedThisFrame,
                    frameSongTimeSeconds);
                ForwardRadialInput(
                    RhythmAction.HeavyAttack,
                    inputService.HeavyAttackWasPressedThisFrame,
                    inputService.HeavyAttackWasReleasedThisFrame,
                    frameSongTimeSeconds);
                return;
            }

            if (inputService.GuardWasPressedThisFrame)
            {
                HandleInput(RhythmAction.Guard);
            }

            if (inputService.LightAttackWasPressedThisFrame)
            {
                HandleInput(RhythmAction.LightAttack);
            }
        }

        private void HandleInput(RhythmAction action)
        {
            if (!IsSessionRunning || !HasActiveSession || isCountdownActive)
            {
                return;
            }

            if (radialSession != null)
            {
                ResolveRadialInput(action, RhythmInputPhase.Pressed, CurrentTimeSeconds);
                return;
            }

            QueueInput(action, CurrentTimeSeconds, GetPresentationTimeSeconds());
        }

        private void ForwardRadialInput(
            RhythmAction action,
            bool pressed,
            bool released,
            double frameSongTimeSeconds)
        {
            if (pressed)
            {
                ResolveRadialInput(action, RhythmInputPhase.Pressed, frameSongTimeSeconds);
            }
            if (released)
            {
                ResolveRadialInput(action, RhythmInputPhase.Released, frameSongTimeSeconds);
            }
        }

        private void ResolveRadialInput(
            RhythmAction action,
            RhythmInputPhase phase,
            double songTimeSeconds)
        {
            if (!IsSessionRunning || radialSession == null || radialSession.IsComplete)
            {
                return;
            }

            long sequenceId = ++radialInputSequence;
            int focusedLimit = RadialPresentationMath.FocusedCueLimit(
                RadialPresentationDifficultyLevel);
            RadialInputResolveResult result = phase == RhythmInputPhase.Pressed
                ? radialSession.Press(
                    action,
                    songTimeSeconds,
                    sequenceId,
                    inputTimingOffsetSeconds,
                    activeRadialBeatMapOffsetSeconds,
                    focusedLimit)
                : radialSession.Release(
                    action,
                    songTimeSeconds,
                    sequenceId,
                    inputTimingOffsetSeconds,
                    activeRadialBeatMapOffsetSeconds,
                    focusedLimit);
            hasTimingAudit = true;
            ProcessRadialRequirementResults(result.RequirementResults);
            if (!result.Consumed && result.RequirementResults.Count == 0)
            {
                lastFeedback = "No match";
            }
            StopIfComplete();
        }

        private bool IsActionHeld(RhythmAction action)
        {
            if (inputService == null)
            {
                return radialSession != null && radialSession.IsHeld(action);
            }

            switch (action)
            {
                case RhythmAction.Guard:
                    return inputService.GuardIsHeld;
                case RhythmAction.LightAttack:
                    return inputService.LightAttackIsHeld;
                case RhythmAction.Dodge:
                    return inputService.DodgeIsHeld;
                case RhythmAction.HeavyAttack:
                    return inputService.HeavyAttackIsHeld;
                default:
                    return false;
            }
        }

        private void ProcessRadialRequirementResults(
            IReadOnlyList<RequirementResult> requirementResults)
        {
            if (requirementResults == null || radialSession == null)
            {
                return;
            }

            bool runFailed = false;
            for (int i = 0; i < requirementResults.Count; i++)
            {
                RequirementResult requirementResult = requirementResults[i];
                RadialEncounterRuntime encounter = FindRadialEncounter(
                    requirementResult.EncounterId);
                if (encounter == null)
                {
                    continue;
                }

                RadialRunObservation observation = radialRunPolicy.Observe(
                    encounter.Data.eventType,
                    encounter.Data.intensity,
                    requirementResult);
                if (observation.DamageApplied)
                {
                    runDamageRevision++;
                }
                runFailed |= observation.CausedFailure;
                radialStatusEffects.TryApplyFailure(
                    encounter.Data,
                    requirementResult,
                    radialRunPolicy.State,
                    CurrentTimeSeconds);

                bool scoresPerStep = encounter.Data.eventType == RadialEventType.TimedChain
                    || encounter.Data.eventType == RadialEventType.OrderedSequence
                    || encounter.Data.eventType == RadialEventType.SwarmChain;
                string scoreId;
                HitGrade grade;
                if (scoresPerStep)
                {
                    scoreId = encounter.Data.eventId + "/" + requirementResult.RequirementId;
                    grade = requirementResult.Grade;
                }
                else
                {
                    if (!encounter.IsResolved)
                    {
                        continue;
                    }
                    scoreId = encounter.Data.eventId;
                    grade = encounter.Result.Grade;
                }

                if (!scoredRadialUnits.Add(scoreId))
                {
                    continue;
                }

                HitResult hitResult = new HitResult(
                    scoreId,
                    grade,
                    requirementResult.TimingErrorSeconds);
                SetLastInputTimingError(hitResult);
                RecordRadialResult(
                    hitResult,
                    requirementResult.Action,
                    encounter.Data.intensity);
                lastFeedback = FormatFeedback(hitResult);
                if (requirementResult.Reason != RadialResultReason.None)
                {
                    lastFeedback += " / " + requirementResult.Reason;
                }
            }

            if (runFailed)
            {
                FailRun();
            }
        }

        private RadialEncounterRuntime FindRadialEncounter(string encounterId)
        {
            if (radialSession == null)
            {
                return null;
            }
            for (int i = 0; i < radialSession.Encounters.Count; i++)
            {
                RadialEncounterRuntime encounter = radialSession.Encounters[i];
                if (string.Equals(encounter.Data.eventId, encounterId, StringComparison.Ordinal))
                {
                    return encounter;
                }
            }
            return null;
        }

        private void RecordRadialResult(
            HitResult result,
            RhythmAction action,
            float intensity)
        {
            ScoreSnapshot previousSnapshot = GetSnapshot();
            scoreTracker.Record(result);
            ScoreSnapshot currentSnapshot = GetSnapshot();
            long sequenceId = ++gameplayFeedbackSequence;
            GameplayResultResolved?.Invoke(new PulseForgeGameplayResultEvent(
                sequenceId,
                result.EventId,
                action,
                result.Grade,
                intensity,
                previousSnapshot.CurrentCombo,
                currentSnapshot.CurrentCombo));
            if (previousSnapshot.CurrentCombo != currentSnapshot.CurrentCombo)
            {
                GameplayComboChanged?.Invoke(new PulseForgeComboChangedEvent(
                    sequenceId,
                    previousSnapshot.CurrentCombo,
                    currentSnapshot.CurrentCombo));
            }

            double presentationTime = GetPresentationTimeSeconds();
            if (result.Grade == HitGrade.Miss)
            {
                combatFeedbackRenderer.ShowMiss(presentationTime);
            }
            else
            {
                combatFeedbackRenderer.ShowHit(action, result.Grade, presentationTime);
            }
        }

        private void QueueInput(RhythmAction action, double songTimeSeconds, double realtimeSeconds)
        {
            ProcessPendingInputBuffer(realtimeSeconds, false);
            if (!IsSessionRunning || session == null || session.IsComplete)
            {
                return;
            }

            double inputWindowSeconds = Mathf.Max(0f, simultaneousInputWindowSeconds);
            if (!rejectAmbiguousSimultaneousActions || inputWindowSeconds <= 0d)
            {
                ResolveInput(action, songTimeSeconds);
                return;
            }

            if (!hasPendingInput)
            {
                hasPendingInput = true;
                pendingInputAction = action;
                pendingInputSongTimeSeconds = songTimeSeconds;
                pendingInputDeadlineRealtimeSeconds = realtimeSeconds + inputWindowSeconds;
                pendingInputIsAmbiguous = false;
                return;
            }

            if (pendingInputAction != action && realtimeSeconds <= pendingInputDeadlineRealtimeSeconds)
            {
                pendingInputIsAmbiguous = true;
            }
        }

        private void ProcessPendingInputBuffer(double realtimeSeconds, bool force)
        {
            if (!hasPendingInput)
            {
                return;
            }

            if (!force && realtimeSeconds <= pendingInputDeadlineRealtimeSeconds)
            {
                return;
            }

            if (pendingInputIsAmbiguous)
            {
                ClearPendingInput();
                combatFeedbackRenderer.Clear();
                lastFeedback = "Ambiguous input ignored";
                return;
            }

            RhythmAction action = pendingInputAction;
            double songTimeSeconds = pendingInputSongTimeSeconds;
            ClearPendingInput();
            ResolveInput(action, songTimeSeconds);
        }

        private void ClearPendingInput()
        {
            hasPendingInput = false;
            pendingInputAction = RhythmAction.Guard;
            pendingInputSongTimeSeconds = 0d;
            pendingInputDeadlineRealtimeSeconds = 0d;
            pendingInputIsAmbiguous = false;
        }

        private void ResolveInput(RhythmAction action, double songTimeSeconds)
        {
            if (!IsSessionRunning || session == null)
            {
                return;
            }

            double adjustedInputTimeSeconds = songTimeSeconds + inputTimingOffsetSeconds;
            RhythmInputResolveResult result = session.ResolveInput(action, adjustedInputTimeSeconds);
            if (!result.HasMatch)
            {
                lastFeedback = "No match";
                return;
            }

            SetLastInputTimingError(result.HitResult);
            RecordResult(result.HitResult);
            double presentationTimeSeconds = GetPresentationTimeSeconds();
            float eventIntensity = GetMatchedEventIntensity(result);
            if (result.HitResult.Grade == HitGrade.Miss)
            {
                combatFeedbackRenderer.ShowMiss(presentationTimeSeconds);
                ShowCombatSceneMiss(eventIntensity);
            }
            else
            {
                combatFeedbackRenderer.ShowHit(action, result.HitResult.Grade, presentationTimeSeconds);
                ShowCombatSceneHit(action, result.HitResult.Grade, eventIntensity);
            }

            lastFeedback = FormatFeedback(result.HitResult);
            StopIfComplete();
        }

        private void ResetCombatSceneView()
        {
            ResolveCombatSceneView();
            if (combatSceneView != null)
            {
                combatSceneView.ResetView();
            }
        }

        private void RefreshCombatSceneVisibility()
        {
            ResolveCombatSceneView();
            if (combatSceneView == null)
            {
                return;
            }

            bool isVisible = UIState == PulseForgeUIState.Countdown
                || UIState == PulseForgeUIState.Playing
                || UIState == PulseForgeUIState.Paused;
            isVisible = isVisible && !UsesRadialCombatPresentation;
            combatSceneView.SetVisible(isVisible);
        }

        private void ShowCombatSceneHit(RhythmAction action, HitGrade grade, float intensity)
        {
            ResolveCombatSceneView();
            if (combatSceneView != null)
            {
                combatSceneView.ShowHit(action, grade, intensity);
            }
        }

        private void ShowCombatSceneMiss()
        {
            ResolveCombatSceneView();
            if (combatSceneView != null)
            {
                combatSceneView.ShowMiss();
            }
        }

        private void ShowCombatSceneMiss(float intensity)
        {
            ResolveCombatSceneView();
            if (combatSceneView != null)
            {
                combatSceneView.ShowMiss(intensity);
            }
        }

        private void ShowCombatSceneMiss(HitResult result)
        {
            ResolveCombatSceneView();
            if (combatSceneView == null)
            {
                return;
            }

            float eventIntensity;
            if (TryGetEventIntensity(result, out eventIntensity))
            {
                combatSceneView.ShowMiss(eventIntensity);
                return;
            }

            combatSceneView.ShowMiss();
        }

        private void ResolveCombatSceneView()
        {
            if (combatSceneView == null)
            {
                combatSceneView = FindFirstObjectByType<DebugCombatSceneView>();
            }
        }

        private static float GetMatchedEventIntensity(RhythmInputResolveResult result)
        {
            if (result == null || result.MatchedEvent == null || result.MatchedEvent.Data == null)
            {
                return 1f;
            }

            return result.MatchedEvent.Data.Intensity;
        }

        private bool TryGetEventIntensity(HitResult result, out float intensity)
        {
            intensity = 1f;
            if (result == null || session == null)
            {
                return false;
            }

            for (int i = 0; i < session.Events.Count; i++)
            {
                BeatEventRuntime beatEvent = session.Events[i];
                if (string.Equals(beatEvent.Data.EventId, result.EventId, StringComparison.Ordinal))
                {
                    intensity = beatEvent.Data.Intensity;
                    return true;
                }
            }

            return false;
        }

        private void RecordResult(HitResult result)
        {
            ScoreSnapshot previousSnapshot = GetSnapshot();
            scoreTracker.Record(result);
            ScoreSnapshot currentSnapshot = GetSnapshot();
            long sequenceId = ++gameplayFeedbackSequence;

            if (TryGetEventFeedbackData(result, out RhythmAction action, out float intensity))
            {
                GameplayResultResolved?.Invoke(new PulseForgeGameplayResultEvent(
                    sequenceId,
                    result.EventId,
                    action,
                    result.Grade,
                    intensity,
                    previousSnapshot.CurrentCombo,
                    currentSnapshot.CurrentCombo));
            }

            if (previousSnapshot.CurrentCombo != currentSnapshot.CurrentCombo)
            {
                GameplayComboChanged?.Invoke(new PulseForgeComboChangedEvent(
                    sequenceId,
                    previousSnapshot.CurrentCombo,
                    currentSnapshot.CurrentCombo));
            }
        }

        private bool TryGetEventFeedbackData(
            HitResult result,
            out RhythmAction action,
            out float intensity)
        {
            action = RhythmAction.Guard;
            intensity = 1f;
            if (result == null || session == null)
            {
                return false;
            }

            for (int i = 0; i < session.Events.Count; i++)
            {
                BeatEventRuntime beatEvent = session.Events[i];
                if (!string.Equals(beatEvent.Data.EventId, result.EventId, StringComparison.Ordinal))
                {
                    continue;
                }

                action = beatEvent.Data.Action;
                intensity = beatEvent.Data.Intensity;
                return true;
            }

            return false;
        }

        private void PublishGameplayStateIfChanged()
        {
            PulseForgeUIState state = UIState;
            if (hasPublishedGameplayState && lastPublishedGameplayState == state)
            {
                return;
            }

            hasPublishedGameplayState = true;
            lastPublishedGameplayState = state;
            if (state != PulseForgeUIState.Playing)
            {
                ResetCombatSceneView();
            }

            GameplayStateChanged?.Invoke(state);
        }

        private void SetLastInputTimingError(HitResult result)
        {
            hasLastInputTimingError = true;
            lastInputTimingErrorMs = result.TimingErrorSeconds * 1000d;
        }

        private void ClearLastInputTimingError()
        {
            hasLastInputTimingError = false;
            lastInputTimingErrorMs = 0d;
        }

        private void StopIfComplete()
        {
            if (radialRunPolicy.State == RadialRunState.Failed
                || !HasActiveSession
                || !IsActiveSessionComplete)
            {
                return;
            }

            radialRunPolicy.Complete(CurrentTimeSeconds);
            radialStatusEffects.ClearActiveEffects();
            songClock.Stop();
            runtimeFlow.Complete();
            lastFeedback = "Session complete";
            RecordTerminalSessionPersistence();
        }

        private void FailRun()
        {
            if (radialRunPolicy.State != RadialRunState.Failed)
            {
                return;
            }

            songClock.Stop();
            radialStatusEffects.ClearActiveEffects();
            isCountdownActive = false;
            ClearPendingInput();
            runtimeFlow.Fail();
            lastFeedback = "Run failed / " + RunFailureReason;
            RecordTerminalSessionPersistence();
        }

        private void SaveAnalyzedTrackToLibrary(
            string sourcePath,
            string convertedWavPath,
            RuntimeAudioPipelineSettings pipelineSettings)
        {
            EnsureSaveService();
            double duration = runtimeAudioClip == null ? GetSessionDurationSeconds() : runtimeAudioClip.length;
            if (saveService.TrySaveTrackSetup(
                sourcePath,
                runtimeAudioDisplayName,
                duration,
                pipelineSettings,
                convertedWavPath,
                runtimeRadialBeatMap,
                runtimeAnalyzerQuality,
                runtimePlannerQuality,
                runtimeBeatGrid,
                out SavedTrackPresetReference reference))
            {
                activeSavedTrackId = reference.TrackId;
                activeSavedPresetId = reference.PresetId;
                savedTrackLibraryRevision++;
            }
            else
            {
                ClearActiveSavedPreset();
                setupStatusMessage = "Song is ready, but its playable library cache could not be saved.";
            }
        }

        private void RecordTerminalSessionPersistence()
        {
            if (completedSessionRecorded
                || !radialRunPolicy.IsTerminal
                || (UIState != PulseForgeUIState.Completed && UIState != PulseForgeUIState.Failed))
            {
                return;
            }

            completedSessionRecorded = true;
            EnsureSaveService();
            saveService.RecordCompletedSession(
                GetSnapshot(),
                useRuntimeSessionSource ? activeSavedTrackId : string.Empty,
                useRuntimeSessionSource ? activeSavedPresetId : string.Empty,
                activeSessionUsesRadialV2ScoreSchema ? ScoreSchema.RadialV2 : ScoreSchema.LegacyV1,
                activeSessionUsesRadialV2ScoreSchema ? runtimeBeatMapFingerprint : string.Empty,
                radialRunPolicy.Mode,
                selectedTimingAssist,
                radialRunPolicy.Outcome);
            savedTrackLibraryRevision++;
        }

        private void EnsureSaveService()
        {
            if (saveService == null)
            {
                saveService = new PulseForgeSaveService();
            }

            saveService.Initialize();
        }

        private void ApplyLoadedSettings()
        {
            EnsureSaveService();
            PulseForgeSettingsData settings = SaveDataNormalizer.NormalizeSettings(saveService.Settings);
            if (SaveDataNormalizer.TryGetPipelineSettings(
                settings.defaultDetection,
                settings.defaultDifficulty,
                settings.defaultCombatStyle,
                settings.defaultCoverage,
                out RuntimeAudioPipelineSettings pipeline))
            {
                runtimeDetectionMode = pipeline.DetectionMode;
                runtimeDifficulty = pipeline.Difficulty;
                runtimeCombatStyle = pipeline.CombatStyle;
                runtimeCoverage = pipeline.Coverage;
            }
            if (!Enum.TryParse(settings.defaultGameMode, true, out selectedGameMode))
            {
                selectedGameMode = RadialGameMode.Standard;
            }
            if (!Enum.TryParse(settings.defaultTimingAssist, true, out selectedTimingAssist))
            {
                selectedTimingAssist = TimingAssistMode.Relaxed;
            }
            showUpcomingInputs = settings.showUpcomingInputs;
            beatPulseEnabled = settings.beatPulseEnabled;
            forecastLeadMultiplier = settings.forecastLeadMultiplier;
            if (!Enum.TryParse(settings.readabilityMode, true, out readabilityMode))
            {
                readabilityMode = RadialReadabilityMode.Assisted;
            }

            debugBeatMapOffsetSeconds = settings.beatmapOffsetSeconds;
            inputTimingOffsetSeconds = settings.inputTimingOffsetSeconds;
        }

        private void ApplyLoadedMotionSetting()
        {
            if (saveService != null && saveService.Settings != null)
            {
                sceneUIRoot?.SetEnableMotion(saveService.Settings.enableMotion);
            }
        }

        private void EnsureInputService()
        {
            if (inputService != null)
            {
                return;
            }

            EnsureSaveService();
            inputService = new PulseForgeInputService();
            inputService.LoadBindingOverridesFromJson(
                saveService.Settings.input == null
                    ? string.Empty
                    : saveService.Settings.input.inputBindingOverridesJson);
            inputService.Enable();
        }

        private void SaveCurrentSettings()
        {
            if (saveService == null)
            {
                return;
            }

            bool enableMotion = sceneUIRoot == null
                ? saveService.Settings.enableMotion
                : sceneUIRoot.EnableMotion;
            PulseForgeSettingsData data = SaveDefaults.CloneSettings(saveService.Settings);
            data.enableMotion = enableMotion;
            data.defaultDetection = runtimeDetectionMode.ToString();
            data.defaultDifficulty = runtimeDifficulty.ToString();
            data.defaultCombatStyle = runtimeCombatStyle.ToString();
            data.defaultCoverage = runtimeCoverage.ToString();
            data.defaultGameMode = selectedGameMode.ToString();
            data.defaultTimingAssist = selectedTimingAssist.ToString();
            data.showUpcomingInputs = showUpcomingInputs;
            data.beatPulseEnabled = beatPulseEnabled;
            data.forecastLeadMultiplier = forecastLeadMultiplier;
            data.readabilityMode = readabilityMode.ToString();
            data.beatmapOffsetSeconds = debugBeatMapOffsetSeconds;
            data.inputTimingOffsetSeconds = inputTimingOffsetSeconds;
            if (inputService != null && data.input != null)
            {
                data.input.inputBindingOverridesJson = inputService.SaveBindingOverridesAsJson();
            }

            saveService.SaveSettings(data);
        }

        public void OpenSettings()
        {
            if (isSettingsOpen
                || (UIState != PulseForgeUIState.Setup && UIState != PulseForgeUIState.Paused))
            {
                return;
            }

            EnsureSaveService();
            EnsureInputService();
            settingsPreviewBaseline = SaveDefaults.CloneSettings(saveService.Settings);
            settingsDraft = SaveDefaults.CloneSettings(settingsPreviewBaseline);
            inputService.LoadBindingOverridesFromJson(settingsDraft.input.inputBindingOverridesJson);
            availableResolutions = PulseForgeRuntimeSettingsApplier.GetAvailableResolutions();
            settingsMessage = string.Empty;
            isSettingsOpen = true;
            settingsDraftRevision++;
        }

        public void ApplySettingsDraft()
        {
            if (!isSettingsOpen || settingsDraft == null)
            {
                return;
            }

            inputService?.CancelInteractiveRebind();
            settingsDraft.input.inputBindingOverridesJson = inputService == null
                ? settingsDraft.input.inputBindingOverridesJson
                : inputService.SaveBindingOverridesAsJson();
            PulseForgeSettingsData applied = SaveDataNormalizer.NormalizeSettings(
                SaveDefaults.CloneSettings(settingsDraft));
            saveService.SaveSettings(applied);
            ApplySettingsData(applied, true);
            settingsPreviewBaseline = null;
            settingsDraft = null;
            settingsMessage = string.Empty;
            isSettingsOpen = false;
            settingsDraftRevision++;
        }

        public void CancelSettings()
        {
            if (!isSettingsOpen)
            {
                return;
            }

            inputService?.CancelInteractiveRebind();
            if (settingsPreviewBaseline != null)
            {
                inputService?.LoadBindingOverridesFromJson(
                    settingsPreviewBaseline.input.inputBindingOverridesJson);
                PulseForgeRuntimeSettingsApplier.ApplyAudio(settingsPreviewBaseline, GetOrAddAudioSource());
                sceneUIRoot?.SetEnableMotion(settingsPreviewBaseline.enableMotion);
            }

            settingsPreviewBaseline = null;
            settingsDraft = null;
            settingsMessage = string.Empty;
            isSettingsOpen = false;
            settingsDraftRevision++;
        }

        public void ResetSettingsDraftToDefaults()
        {
            if (!isSettingsOpen)
            {
                return;
            }

            inputService?.CancelInteractiveRebind();
            settingsDraft = SaveDefaults.CreateSettings();
            inputService?.ResetBindings();
            settingsDraft.input.inputBindingOverridesJson = string.Empty;
            PreviewDraftAudioAndMotion();
            settingsMessage = "Defaults loaded. Apply to save them.";
            settingsDraftRevision++;
        }

        public void SetDraftMasterVolume(float value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.audio.masterVolume = Mathf.Clamp01(value);
            PreviewDraftAudioAndMotion();
            settingsDraftRevision++;
        }

        public void SetDraftMusicVolume(float value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.audio.musicVolume = Mathf.Clamp01(value);
            PreviewDraftAudioAndMotion();
            settingsDraftRevision++;
        }

        public void SetDraftMotion(bool value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.enableMotion = value;
            sceneUIRoot?.SetEnableMotion(value);
            settingsDraftRevision++;
        }

        public void SetDraftVSync(bool value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.display.vSync = value;
            settingsDraftRevision++;
        }

        public void SetDraftBeatmapOffsetMilliseconds(string value)
        {
            SetDraftOffset(value, true);
        }

        public void SetDraftInputOffsetMilliseconds(string value)
        {
            SetDraftOffset(value, false);
        }

        public void CycleDraftDisplayMode(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            PulseForgeDisplayMode current;
            if (!Enum.TryParse(draft.display.displayMode, true, out current)) current = PulseForgeDisplayMode.Windowed;
            PulseForgeDisplayMode[] values = (PulseForgeDisplayMode[])Enum.GetValues(typeof(PulseForgeDisplayMode));
            draft.display.displayMode = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftResolution(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft) || availableResolutions.Count == 0) return;
            int current = 0;
            for (int i = 0; i < availableResolutions.Count; i++)
            {
                PulseForgeResolutionOption option = availableResolutions[i];
                if (option.Width == draft.display.resolutionWidth && option.Height == draft.display.resolutionHeight
                    && option.RefreshRate == draft.display.refreshRate) { current = i; break; }
            }

            PulseForgeResolutionOption selected = availableResolutions[WrapIndex(current + direction, availableResolutions.Count)];
            draft.display.resolutionWidth = selected.Width;
            draft.display.resolutionHeight = selected.Height;
            draft.display.refreshRate = selected.RefreshRate;
            settingsDraftRevision++;
        }

        public void CycleDraftFrameRate(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            int[] values = { 60, 120, 144, 165, 240, -1 };
            int current = Array.IndexOf(values, draft.display.frameRateLimit);
            draft.display.frameRateLimit = values[WrapIndex((current < 0 ? 0 : current) + direction, values.Length)];
            settingsDraftRevision++;
        }

        public void CycleDraftDetection(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            RuntimeDetectionMode current;
            if (!Enum.TryParse(draft.defaultDetection, true, out current)) current = RuntimeDetectionMode.Onset;
            RuntimeDetectionMode[] values = (RuntimeDetectionMode[])Enum.GetValues(typeof(RuntimeDetectionMode));
            draft.defaultDetection = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftDifficulty(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            RuntimeDifficulty current;
            if (!Enum.TryParse(draft.defaultDifficulty, true, out current)) current = RuntimeDifficulty.Normal;
            RuntimeDifficulty[] values = (RuntimeDifficulty[])Enum.GetValues(typeof(RuntimeDifficulty));
            draft.defaultDifficulty = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftCombatStyle(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            RuntimeCombatStyle current;
            if (!Enum.TryParse(draft.defaultCombatStyle, true, out current)) current = RuntimeCombatStyle.Legacy;
            RuntimeCombatStyle[] values = (RuntimeCombatStyle[])Enum.GetValues(typeof(RuntimeCombatStyle));
            draft.defaultCombatStyle = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftCoverage(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            RuntimeCoverage current;
            if (!Enum.TryParse(draft.defaultCoverage, true, out current)) current = RuntimeCoverage.Standard;
            RuntimeCoverage[] values = (RuntimeCoverage[])Enum.GetValues(typeof(RuntimeCoverage));
            draft.defaultCoverage = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftGameMode(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            RadialGameMode current;
            if (!Enum.TryParse(draft.defaultGameMode, true, out current)) current = RadialGameMode.Standard;
            RadialGameMode[] values = (RadialGameMode[])Enum.GetValues(typeof(RadialGameMode));
            draft.defaultGameMode = values[WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftTimingAssist(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            if (!Enum.TryParse(draft.defaultTimingAssist, true, out TimingAssistMode current))
            {
                current = TimingAssistMode.Relaxed;
            }
            TimingAssistMode[] values = (TimingAssistMode[])Enum.GetValues(typeof(TimingAssistMode));
            draft.defaultTimingAssist = values[
                WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void SetDraftShowUpcomingInputs(bool value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.showUpcomingInputs = value;
            settingsDraftRevision++;
        }

        public void SetDraftBeatPulseEnabled(bool value)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            draft.beatPulseEnabled = value;
            settingsDraftRevision++;
        }

        public void CycleDraftForecastLeadMultiplier(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            float[] values = { 1f, 1.25f, 1.5f, 1.75f };
            int current = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (Mathf.Abs(values[i] - draft.forecastLeadMultiplier) < 0.001f)
                {
                    current = i;
                    break;
                }
            }
            draft.forecastLeadMultiplier = values[WrapIndex(current + direction, values.Length)];
            settingsDraftRevision++;
        }

        public void CycleDraftReadabilityMode(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            if (!Enum.TryParse(
                draft.readabilityMode,
                true,
                out RadialReadabilityMode current))
            {
                current = RadialReadabilityMode.Assisted;
            }
            RadialReadabilityMode[] values =
                (RadialReadabilityMode[])Enum.GetValues(typeof(RadialReadabilityMode));
            draft.readabilityMode = values[
                WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public void CycleDraftUILanguage(int direction)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            PulseForgeUILanguage current = Enum.TryParse(
                draft.uiLanguage,
                true,
                out PulseForgeUILanguage parsed)
                    ? parsed
                    : PulseForgeUILanguage.English;
            PulseForgeUILanguage[] values =
                (PulseForgeUILanguage[])Enum.GetValues(typeof(PulseForgeUILanguage));
            draft.uiLanguage = values[
                WrapIndex((int)current + direction, values.Length)].ToString();
            settingsDraftRevision++;
        }

        public string GetDraftBindingDisplay(PulseForgeInputAction action)
        {
            EnsureInputService();
            return inputService.GetBindingDisplayString(action);
        }

        public string GetActiveBindingDisplay(RhythmAction action)
        {
            EnsureInputService();
            switch (action)
            {
                case RhythmAction.Guard:
                    return inputService.GetBindingDisplayString(PulseForgeInputAction.Guard);
                case RhythmAction.Dodge:
                    return inputService.GetBindingDisplayString(PulseForgeInputAction.Dodge);
                case RhythmAction.HeavyAttack:
                    return inputService.GetBindingDisplayString(PulseForgeInputAction.HeavyAttack);
                default:
                    return inputService.GetBindingDisplayString(PulseForgeInputAction.LightAttack);
            }
        }

        public void BeginDraftRebind(PulseForgeInputAction action)
        {
            if (!isSettingsOpen) return;
            settingsMessage = "Press a key…";
            settingsDraftRevision++;
            inputService.BeginInteractiveRebind(action, (success, message) =>
            {
                if (!isSettingsOpen || settingsDraft == null) return;
                if (success)
                {
                    settingsDraft.input.inputBindingOverridesJson = inputService.SaveBindingOverridesAsJson();
                }

                settingsMessage = message;
                settingsDraftRevision++;
            });
        }

        public void CancelDraftRebind()
        {
            inputService?.CancelInteractiveRebind();
        }

        public void ResetDraftBindings()
        {
            if (!isSettingsOpen || settingsDraft == null) return;
            inputService.ResetBindings();
            settingsDraft.input.inputBindingOverridesJson = string.Empty;
            settingsMessage = "Bindings reset in the draft.";
            settingsDraftRevision++;
        }

        private void ApplySettingsData(PulseForgeSettingsData settings, bool applyDisplay)
        {
            PulseForgeSettingsData normalized = SaveDataNormalizer.NormalizeSettings(settings);
            SaveDataNormalizer.TryGetPipelineSettings(normalized.defaultDetection, normalized.defaultDifficulty,
                normalized.defaultCombatStyle, normalized.defaultCoverage, out RuntimeAudioPipelineSettings pipeline);
            runtimeDetectionMode = pipeline.DetectionMode;
            runtimeDifficulty = pipeline.Difficulty;
            runtimeCombatStyle = pipeline.CombatStyle;
            runtimeCoverage = pipeline.Coverage;
            if (Enum.TryParse(normalized.defaultGameMode, true, out RadialGameMode gameMode))
            {
                selectedGameMode = gameMode;
            }
            if (!Enum.TryParse(normalized.defaultTimingAssist, true, out selectedTimingAssist))
            {
                selectedTimingAssist = TimingAssistMode.Relaxed;
            }
            showUpcomingInputs = normalized.showUpcomingInputs;
            beatPulseEnabled = normalized.beatPulseEnabled;
            forecastLeadMultiplier = normalized.forecastLeadMultiplier;
            if (!Enum.TryParse(normalized.readabilityMode, true, out readabilityMode))
            {
                readabilityMode = RadialReadabilityMode.Assisted;
            }
            debugBeatMapOffsetSeconds = normalized.beatmapOffsetSeconds;
            inputTimingOffsetSeconds = normalized.inputTimingOffsetSeconds;
            inputService?.LoadBindingOverridesFromJson(normalized.input.inputBindingOverridesJson);
            sceneUIRoot?.SetEnableMotion(normalized.enableMotion);
            PulseForgeRuntimeSettingsApplier.ApplyAudio(normalized, GetOrAddAudioSource());
            if (applyDisplay) PulseForgeRuntimeSettingsApplier.ApplyDisplay(normalized);
        }

        private bool TryGetSettingsDraft(out PulseForgeSettingsData draft)
        {
            draft = settingsDraft;
            return isSettingsOpen && draft != null;
        }

        private void PreviewDraftAudioAndMotion()
        {
            PulseForgeRuntimeSettingsApplier.ApplyAudio(settingsDraft, GetOrAddAudioSource());
            sceneUIRoot?.SetEnableMotion(settingsDraft.enableMotion);
        }

        private void SetDraftOffset(string value, bool beatmap)
        {
            if (!TryGetSettingsDraft(out PulseForgeSettingsData draft)) return;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float milliseconds))
            {
                settingsMessage = "Enter an offset between -500 and +500 ms.";
                settingsDraftRevision++;
                return;
            }

            float seconds = Mathf.Clamp(milliseconds, -500f, 500f) / 1000f;
            if (beatmap) draft.beatmapOffsetSeconds = seconds; else draft.inputTimingOffsetSeconds = seconds;
            settingsMessage = string.Empty;
            settingsDraftRevision++;
        }

        private static int WrapIndex(int value, int count)
        {
            return count <= 0 ? 0 : (value % count + count) % count;
        }

        private void ResetLibrarySelectionForNewSong()
        {
            saveSetupToLibrary = false;
            isSavedTracksOpen = false;
            ClearActiveSavedPreset();
        }

        private void ClearActiveSavedPreset()
        {
            activeSavedTrackId = string.Empty;
            activeSavedPresetId = string.Empty;
        }

        private void DrawEventList()
        {
            DrawCard(() =>
            {
                if (!HasActiveSession)
                {
                    GUILayout.Label("No session", mutedLabelStyle);
                    return;
                }

                if (radialSession != null)
                {
                    GUILayout.Label(
                        "Radial V2 active. Legacy lane event rendering is disabled.",
                        mutedLabelStyle);
                    GUILayout.Label(
                        radialSession.TotalEncounterCount.ToString(CultureInfo.InvariantCulture)
                        + " encounters / "
                        + SessionInputCost.ToString(CultureInfo.InvariantCulture)
                        + " inputs",
                        mutedLabelStyle);
                    return;
                }

                for (int i = 0; i < session.Events.Count; i++)
                {
                    BeatEventRuntime beatEvent = session.Events[i];
                    GUILayout.BeginHorizontal();
                    DrawActionAccent(beatEvent.Data.Action);
                    GUILayout.Label(FormatSeconds(beatEvent.Data.TargetTimeSeconds), mutedLabelStyle, GUILayout.Width(64f));
                    DrawActionBadge(beatEvent.Data.Action);
                    DrawStateBadge(beatEvent.State);
                    GUILayout.Label(beatEvent.Data.EventId, mutedLabelStyle);
                    GUILayout.EndHorizontal();
                }
            });
        }

        private void DrawTimingCalibrationPanel()
        {
            GUILayout.Space(CardSpacing);
            DrawSectionTitle("Timing Calibration");
            DrawCard(() =>
            {
                GUILayout.Label("Beatmap offset: " + FormatSignedMilliseconds(debugBeatMapOffsetSeconds), metricValueStyle);
                GUILayout.Label("Input offset: " + FormatSignedMilliseconds(inputTimingOffsetSeconds), metricValueStyle);
                GUILayout.Label("Last input timing error: " + GetLastInputTimingErrorText(), mutedLabelStyle);
                GUILayout.Label("Beatmap offset shifts event times. Applies on next Start/Restart.", mutedLabelStyle);
                GUILayout.Label("Input offset shifts judgement input time.", mutedLabelStyle);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Beatmap -10 ms"))
                {
                    AdjustBeatMapOffset(-10f);
                }

                if (GUILayout.Button("Beatmap +10 ms"))
                {
                    AdjustBeatMapOffset(10f);
                }

                if (GUILayout.Button("Beatmap reset"))
                {
                    debugBeatMapOffsetSeconds = 0f;
                    SaveCurrentSettings();
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Input -10 ms"))
                {
                    AdjustInputTimingOffset(-10f);
                }

                if (GUILayout.Button("Input +10 ms"))
                {
                    AdjustInputTimingOffset(10f);
                }

                if (GUILayout.Button("Input reset"))
                {
                    inputTimingOffsetSeconds = 0f;
                    SaveCurrentSettings();
                }

                GUILayout.EndHorizontal();
            });
        }

        private void AdjustBeatMapOffset(float milliseconds)
        {
            debugBeatMapOffsetSeconds += milliseconds / 1000f;
            SaveCurrentSettings();
        }

        private void AdjustInputTimingOffset(float milliseconds)
        {
            inputTimingOffsetSeconds += milliseconds / 1000f;
            SaveCurrentSettings();
        }

        private string GetLastInputTimingErrorText()
        {
            if (!hasLastInputTimingError)
            {
                return "n/a";
            }

            string sign = lastInputTimingErrorMs >= 0d ? "+" : string.Empty;
            return sign + lastInputTimingErrorMs.ToString("0", CultureInfo.InvariantCulture) + " ms";
        }

        private ScoreSnapshot GetSnapshot()
        {
            if (scoreTracker == null)
            {
                return new ScoreSnapshot(0, 0, 0, 0, 0, 0, 0);
            }

            return scoreTracker.CreateSnapshot();
        }

        private double CurrentTimeSeconds
        {
            get
            {
                if (songClock == null)
                {
                    return 0d;
                }

                return songClock.CurrentTimeSeconds;
            }
        }

        private bool IsSessionRunning
        {
            get { return !isCountdownActive && songClock != null && songClock.IsRunning; }
        }

        private bool HasActiveSession => session != null || radialSession != null;

        private bool IsActiveSessionComplete => radialSession != null
            ? radialSession.IsComplete
            : session != null && session.IsComplete;

        private string GetClockName()
        {
            return songClock == null ? "None" : songClock.GetType().Name;
        }

        private string GetAudioClipStatus()
        {
            if (useRuntimeSessionSource && runtimeAudioClip != null)
            {
                return "Runtime: " + runtimeAudioDisplayName;
            }

            return debugAudioClip == null ? "Not assigned" : "Assigned: " + debugAudioClip.name;
        }

        private string GetBeatMapSourceName()
        {
            if (useRuntimeSessionSource && runtimeRadialBeatMap != null)
            {
                return "Runtime radial V2: "
                    + runtimeAudioDisplayName
                    + " / "
                    + appliedRuntimePipelineSettings.DetectionMode
                    + " / "
                    + appliedRuntimePipelineSettings.Difficulty
                    + " / "
                    + appliedRuntimePipelineSettings.CombatStyle;
            }

            if (debugRadialBeatMapJson != null)
            {
                return "Inspector radial V2 JSON: " + debugRadialBeatMapJson.name;
            }

            if (debugBeatMapJson != null)
            {
                return "JSON: " + debugBeatMapJson.name;
            }

            if (debugBeatMapAsset == null)
            {
                return "Default Hardcoded Beat Map";
            }

            return "ScriptableObject: " + debugBeatMapAsset.name;
        }

        private string GetEventCountText()
        {
            return SessionEventCount.ToString(CultureInfo.InvariantCulture);
        }

        private static double GetPresentationTimeSeconds()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        private string FormatBeatMapError(Exception exception)
        {
            if (useRuntimeSessionSource && runtimeRadialBeatMap != null)
            {
                return "Runtime radial beat map error: " + exception.Message;
            }

            if (debugRadialBeatMapJson != null)
            {
                return "Inspector radial beat map JSON error: " + exception.Message;
            }

            if (debugBeatMapJson != null)
            {
                return "Beat map JSON error: " + exception.Message;
            }

            return "Beat map error: " + exception.Message;
        }

        private double GetSessionDurationSeconds()
        {
            AudioClip activeAudioClip = GetActiveAudioClip();
            if (activeAudioClip != null && activeAudioClip.length > 0f)
            {
                return activeAudioClip.length;
            }

            if (radialSession != null && runtimeRadialBeatMap != null)
            {
                double lastRequirementTime = 0d;
                for (int i = 0; i < runtimeRadialBeatMap.encounters.Count; i++)
                {
                    RadialEncounterEventData encounter = runtimeRadialBeatMap.encounters[i];
                    if (encounter == null || encounter.requirements == null)
                    {
                        continue;
                    }
                    for (int requirementIndex = 0;
                        requirementIndex < encounter.requirements.Count;
                        requirementIndex++)
                    {
                        lastRequirementTime = Math.Max(
                            lastRequirementTime,
                            encounter.requirements[requirementIndex].targetTimeSeconds);
                    }
                }
                return lastRequirementTime + GoodWindowSeconds;
            }

            if (session == null || session.Events.Count == 0)
            {
                return 0d;
            }

            double lastEventTimeSeconds = 0d;
            for (int i = 0; i < session.Events.Count; i++)
            {
                lastEventTimeSeconds = Math.Max(
                    lastEventTimeSeconds,
                    session.Events[i].Data.TargetTimeSeconds);
            }

            return lastEventTimeSeconds + GoodWindowSeconds;
        }

        private string GetAnalysisQualitySummary()
        {
            if (!useRuntimeSessionSource || runtimePlannerQuality == null)
            {
                return debugRadialBeatMapJson == null
                    ? radialSession == null ? "Legacy V1" : "Legacy V1 adapted to radial"
                    : "Analyzer V2 JSON";
            }

            string label;
            switch (runtimePlannerQuality.result)
            {
                case PlannerQualityResult.PassWithRepairs:
                    label = "Repaired";
                    break;
                case PlannerQualityResult.UnderCovered:
                    label = "Limited Coverage";
                    break;
                default:
                    label = "Pass";
                    break;
            }

            if (runtimeAnalyzerQuality != null
                && runtimeAnalyzerQuality.warnings != null
                && runtimeAnalyzerQuality.warnings.Count > 0)
            {
                label += " / " + runtimeAnalyzerQuality.warnings[0];
            }
            return label;
        }

        private string ResolveBuiltInDifficultyLabel()
        {
            string sourceName = GetBuiltInBeatMapName();
            if (sourceName.IndexOf("Easy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RuntimeDifficulty.Easy.ToString();
            }

            if (sourceName.IndexOf("Hard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RuntimeDifficulty.Hard.ToString();
            }

            return RuntimeDifficulty.Normal.ToString();
        }

        private string ResolveBuiltInCombatStyleLabel()
        {
            string sourceName = GetBuiltInBeatMapName();
            RuntimeCombatStyle[] styles =
            {
                RuntimeCombatStyle.Balanced,
                RuntimeCombatStyle.Defensive,
                RuntimeCombatStyle.Aggressive,
                RuntimeCombatStyle.Bursty
            };

            for (int i = 0; i < styles.Length; i++)
            {
                string styleName = styles[i].ToString();
                if (sourceName.IndexOf(styleName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return styleName;
                }
            }

            return RuntimeCombatStyle.Legacy.ToString();
        }

        private string GetBuiltInBeatMapName()
        {
            if (debugRadialBeatMapJson != null)
            {
                return debugRadialBeatMapJson.name;
            }
            if (debugBeatMapJson != null)
            {
                return debugBeatMapJson.name;
            }

            return debugBeatMapAsset == null ? string.Empty : debugBeatMapAsset.name;
        }

        private double GetCountdownRemainingSeconds()
        {
            if (!isCountdownActive)
            {
                return 0d;
            }

            double elapsedSeconds = GetPresentationTimeSeconds() - countdownStartedAtSeconds;
            double remainingSeconds = countdownDurationSeconds - elapsedSeconds;
            return remainingSeconds < 0d ? 0d : remainingSeconds;
        }

        private static IReadOnlyList<BeatEventData> CreateDebugEventData()
        {
            return new[]
            {
                new BeatEventData("event-001", 1.00d, RhythmAction.Guard, 1f),
                new BeatEventData("event-002", 1.50d, RhythmAction.Guard, 1f),
                new BeatEventData("event-003", 2.00d, RhythmAction.LightAttack, 1f),
                new BeatEventData("event-004", 2.50d, RhythmAction.Guard, 1f),
                new BeatEventData("event-005", 3.00d, RhythmAction.LightAttack, 1f),
                new BeatEventData("event-006", 3.25d, RhythmAction.LightAttack, 1f),
                new BeatEventData("event-007", 3.75d, RhythmAction.Guard, 1f),
                new BeatEventData("event-008", 4.25d, RhythmAction.LightAttack, 1f),
                new BeatEventData("event-009", 4.75d, RhythmAction.Guard, 1f),
                new BeatEventData("event-010", 5.25d, RhythmAction.LightAttack, 1f)
            };
        }

        private static string FormatFeedback(HitResult result)
        {
            if (result.Grade == HitGrade.Miss)
            {
                return "Miss " + result.EventId;
            }

            return result.Grade + " " + result.EventId + " " + FormatSignedMilliseconds(result.TimingErrorSeconds);
        }

        private static string FormatSignedMilliseconds(double timingErrorSeconds)
        {
            double milliseconds = timingErrorSeconds * 1000d;
            string sign = milliseconds >= 0d ? "+" : string.Empty;
            return sign + milliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";
        }

        private static string FormatSeconds(double seconds)
        {
            return seconds.ToString("0.000", CultureInfo.InvariantCulture) + "s";
        }

        private static string FormatCountdownSeconds(double seconds)
        {
            return seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }
    }
}
