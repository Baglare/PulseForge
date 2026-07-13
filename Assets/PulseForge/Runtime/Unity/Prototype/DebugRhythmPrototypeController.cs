using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Audio;
using PulseForge.Runtime.Unity.BeatMaps;
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
        [SerializeField] private DebugBeatMapAsset debugBeatMapAsset = null;
        [SerializeField] private AudioClip debugAudioClip = null;
        [SerializeField] private bool useAudioClockWhenClipAssigned = true;
        [SerializeField] private float startCountdownSeconds = 1.0f;
        [SerializeField] private bool rejectAmbiguousSimultaneousActions = true;
        [SerializeField] private float simultaneousInputWindowSeconds = 0.035f;
        [SerializeField] private float debugBeatMapOffsetSeconds = 0f;
        [SerializeField] private float inputTimingOffsetSeconds = 0f;
        [SerializeField] private DebugCombatSceneView combatSceneView = null;

        private RhythmSession session;
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
        private string runtimeAudioDisplayName = string.Empty;
        private string runtimeAudioImportStatus = "Select MP3, WAV, M4A, AAC, FLAC, OGG, OPUS, WMA or AIFF.";
        private bool isRuntimeAudioImportBusy;
        private RuntimeDetectionMode runtimeDetectionMode = RuntimeDetectionMode.Onset;
        private RuntimeDifficulty runtimeDifficulty = RuntimeDifficulty.Normal;
        private RuntimeCombatStyle runtimeCombatStyle = RuntimeCombatStyle.Legacy;
        private RuntimeAudioPipelineSettings appliedRuntimePipelineSettings = RuntimeAudioPipelineSettings.Default;
        private readonly PulseForgeRuntimeFlow runtimeFlow = new PulseForgeRuntimeFlow();
        private bool useRuntimeSessionSource;
        private bool legacyDebugOverlayVisible;
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

        public event Action<PulseForgeGameplayResultEvent> GameplayResultResolved;
        public event Action<PulseForgeComboChangedEvent> GameplayComboChanged;
        public event Action GameplaySessionRestarted;
        public event Action<PulseForgeUIState> GameplayStateChanged;

        public bool SaveSetupToLibrary => saveSetupToLibrary;
        public bool IsSavedTracksOpen => isSavedTracksOpen;
        public int SavedTrackLibraryRevision => savedTrackLibraryRevision;
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
            get { return new RuntimeAudioPipelineSettings(runtimeDetectionMode, runtimeDifficulty, runtimeCombatStyle); }
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
            get { return session == null ? Array.Empty<BeatEventRuntime>() : session.Events; }
        }

        public int SessionEventCount
        {
            get { return session == null ? 0 : session.TotalEventCount; }
        }

        public bool HasBuiltInDemo
        {
            get
            {
                return debugAudioClip != null
                    && (debugBeatMapJson != null || debugBeatMapAsset != null);
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
            get { return UIState == PulseForgeUIState.Ready && session != null && !isRuntimeAudioImportBusy; }
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

        public double CurrentSongTimeSeconds
        {
            get { return CurrentTimeSeconds; }
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
            StopClock();
            if (runtimeAudioClip != null)
            {
                Destroy(runtimeAudioClip);
            }
        }

        private void Update()
        {
            HandleRuntimeKeyboardInput();

            if (isCountdownActive)
            {
                UpdateCountdown();
                return;
            }

            if (!IsSessionRunning || session == null || session.IsComplete)
            {
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
            if (!legacyDebugOverlayVisible)
            {
                return;
            }

            EnsureHudStyles();

            CalculateHudRects(out Rect mainAreaRect, out Rect rightPanelRect);
            DrawMainArea(mainAreaRect);
            DrawRightPanel(rightPanelRect);
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
            rhythmLaneRenderer.Draw(rhythmLaneRect, session == null ? null : session.Events, CurrentTimeSeconds);

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

                if (DrawActionButton("Strike (J)", StrikeColor))
                {
                    HandleInput(RhythmAction.Strike);
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
            HandleInput(RhythmAction.Strike);
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

            if (track.fileMissing || string.IsNullOrWhiteSpace(track.originalFilePath)
                || !File.Exists(track.originalFilePath))
            {
                saveService.RefreshLibraryFileStates();
                savedTrackLibraryRevision++;
                return;
            }

            if (!runtimeFlow.SelectAudioPath(track.originalFilePath))
            {
                return;
            }

            runtimeDetectionMode = settings.DetectionMode;
            runtimeDifficulty = settings.Difficulty;
            runtimeCombatStyle = settings.CombatStyle;
            activeSavedTrackId = track.trackId;
            activeSavedPresetId = preset.presetId;
            saveService.MarkTrackUsed(track.trackId);
            savedTrackLibraryRevision++;
            saveSetupToLibrary = false;
            isSavedTracksOpen = false;
            runtimeAudioImportStatus = "Audio selected: " + Path.GetFileName(track.originalFilePath);
            setupStatusMessage = "Saved setup loaded. Analyze Song when you are ready.";
            SaveCurrentSettings();
        }

        public void RelinkSavedTrack(string trackId)
        {
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

            if (session != null && session.IsComplete)
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
                session = new RhythmSession(
                    CreateSessionBeatEvents(),
                    new JudgementWindows(PerfectWindowSeconds, GoodWindowSeconds),
                    new RhythmInputResolver(new BeatEventMatcher(), new HitJudge()),
                    new BeatEventTimeoutProcessor(new HitJudge()));
                scoreTracker = new ScoreTracker();
                completedSessionRecorded = false;
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

                GameplaySessionRestarted?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                StopClock();
                isCountdownActive = false;
                ClearPendingInput();
                session = null;
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

            return ApplyDebugBeatMapOffset(beatEvents);
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

            return audioSource;
        }

        private bool CanTogglePause
        {
            get
            {
                return !isCountdownActive
                    && session != null
                    && !session.IsComplete
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
            string previousRuntimeAudioDisplayName = runtimeAudioDisplayName;
            RuntimeAudioPipelineSettings previousAppliedSettings = appliedRuntimePipelineSettings;
            bool previousUseRuntimeSessionSource = useRuntimeSessionSource;
            runtimeAudioClip = importResult.AudioClip;
            runtimeBeatEvents = importResult.BeatEvents;
            runtimeAudioDisplayName = importResult.DisplayName;
            appliedRuntimePipelineSettings = pipelineSettings;
            useRuntimeSessionSource = true;

            if (!PrepareSession(false))
            {
                string preparationError = lastFeedback;
                runtimeAudioClip = previousRuntimeAudioClip;
                runtimeBeatEvents = previousRuntimeBeatEvents;
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
            setupStatusMessage = "Song analyzed and ready to play.";
            if (saveSetupToLibrary)
            {
                SaveAnalyzedTrackToLibrary(importResult.SourcePath, pipelineSettings);
            }
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
            else if (runtimeAudioImportStatus.IndexOf("Detecting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.DetectingRhythm);
            }
            else if (runtimeAudioImportStatus.IndexOf("Building", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.BuildingCombatSequence);
            }
            else if (runtimeAudioImportStatus.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                runtimeFlow.SetProcessingStage(PulseForgeProcessingStage.Ready);
            }
        }

        private void HandleRuntimeKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                legacyDebugOverlayVisible = !legacyDebugOverlayVisible;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
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

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                HandleInput(RhythmAction.Guard);
            }

            if (keyboard.jKey.wasPressedThisFrame)
            {
                HandleInput(RhythmAction.Strike);
            }
        }

        private void HandleInput(RhythmAction action)
        {
            if (!IsSessionRunning || session == null || isCountdownActive)
            {
                return;
            }

            QueueInput(action, CurrentTimeSeconds, GetPresentationTimeSeconds());
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
            if (session == null || !session.IsComplete)
            {
                return;
            }

            songClock.Stop();
            runtimeFlow.Complete();
            lastFeedback = "Session complete";
            RecordCompletedSessionPersistence();
        }

        private void SaveAnalyzedTrackToLibrary(
            string sourcePath,
            RuntimeAudioPipelineSettings pipelineSettings)
        {
            EnsureSaveService();
            double duration = runtimeAudioClip == null ? GetSessionDurationSeconds() : runtimeAudioClip.length;
            int eventCount = session == null ? 0 : session.TotalEventCount;
            if (saveService.TrySaveTrackSetup(
                sourcePath,
                runtimeAudioDisplayName,
                duration,
                pipelineSettings,
                eventCount,
                out SavedTrackPresetReference reference))
            {
                activeSavedTrackId = reference.TrackId;
                activeSavedPresetId = reference.PresetId;
                savedTrackLibraryRevision++;
            }
            else
            {
                ClearActiveSavedPreset();
            }
        }

        private void RecordCompletedSessionPersistence()
        {
            if (completedSessionRecorded || UIState != PulseForgeUIState.Completed)
            {
                return;
            }

            completedSessionRecorded = true;
            EnsureSaveService();
            saveService.RecordCompletedSession(
                GetSnapshot(),
                useRuntimeSessionSource ? activeSavedTrackId : string.Empty,
                useRuntimeSessionSource ? activeSavedPresetId : string.Empty);
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
                out RuntimeAudioPipelineSettings pipeline))
            {
                runtimeDetectionMode = pipeline.DetectionMode;
                runtimeDifficulty = pipeline.Difficulty;
                runtimeCombatStyle = pipeline.CombatStyle;
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

        private void SaveCurrentSettings()
        {
            if (saveService == null)
            {
                return;
            }

            bool enableMotion = sceneUIRoot == null
                ? saveService.Settings.enableMotion
                : sceneUIRoot.EnableMotion;
            saveService.SaveSettings(
                enableMotion,
                SelectedPipelineSettings,
                debugBeatMapOffsetSeconds,
                inputTimingOffsetSeconds);
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
                if (session == null)
                {
                    GUILayout.Label("No session", mutedLabelStyle);
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
            if (useRuntimeSessionSource && runtimeBeatEvents != null)
            {
                return "Runtime pipeline: "
                    + runtimeAudioDisplayName
                    + " / "
                    + appliedRuntimePipelineSettings.DetectionMode
                    + " / "
                    + appliedRuntimePipelineSettings.Difficulty
                    + " / "
                    + appliedRuntimePipelineSettings.CombatStyle;
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
            if (session == null)
            {
                return "0";
            }

            return session.TotalEventCount.ToString(CultureInfo.InvariantCulture);
        }

        private static double GetPresentationTimeSeconds()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        private string FormatBeatMapError(Exception exception)
        {
            if (useRuntimeSessionSource && runtimeBeatEvents != null)
            {
                return "Runtime beat map error: " + exception.Message;
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
                new BeatEventData("event-003", 2.00d, RhythmAction.Strike, 1f),
                new BeatEventData("event-004", 2.50d, RhythmAction.Guard, 1f),
                new BeatEventData("event-005", 3.00d, RhythmAction.Strike, 1f),
                new BeatEventData("event-006", 3.25d, RhythmAction.Strike, 1f),
                new BeatEventData("event-007", 3.75d, RhythmAction.Guard, 1f),
                new BeatEventData("event-008", 4.25d, RhythmAction.Strike, 1f),
                new BeatEventData("event-009", 4.75d, RhythmAction.Guard, 1f),
                new BeatEventData("event-010", 5.25d, RhythmAction.Strike, 1f)
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
