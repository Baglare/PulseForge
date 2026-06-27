using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.BeatMaps;
using PulseForge.Runtime.Unity.Timing;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugRhythmPrototypeController : MonoBehaviour
    {
        private const double PerfectWindowSeconds = 0.045d;
        private const double GoodWindowSeconds = 0.100d;
        private const float RhythmLaneHeight = 96f;

        [SerializeField] private TextAsset debugBeatMapJson = null;
        [SerializeField] private DebugBeatMapAsset debugBeatMapAsset = null;
        [SerializeField] private AudioClip debugAudioClip = null;
        [SerializeField] private bool useAudioClockWhenClipAssigned = true;
        [SerializeField] private float startCountdownSeconds = 1.0f;
        [SerializeField] private bool rejectAmbiguousSimultaneousActions = true;
        [SerializeField] private float simultaneousInputWindowSeconds = 0.035f;
        [SerializeField] private float debugBeatMapOffsetSeconds = 0f;
        [SerializeField] private float inputTimingOffsetSeconds = 0f;

        private RhythmSession session;
        private ScoreTracker scoreTracker;
        private ISongClock songClock;
        private string lastFeedback = "Press Start / Restart";
        private Vector2 eventListScroll;
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

        private void Start()
        {
            RestartSession();
        }

        private void Update()
        {
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
                lastFeedback = FormatFeedback(timedOutResults[i]);
            }

            StopIfComplete();
        }

        private void OnGUI()
        {
            HandleKeyboardEvent(Event.current);

            float panelWidth = Mathf.Min(780f, Screen.width - 20f);
            GUILayout.BeginArea(new Rect(10f, 10f, panelWidth, Screen.height - 20f), GUI.skin.box);
            eventListScroll = GUILayout.BeginScrollView(eventListScroll);

            GUILayout.Label("PulseForge Debug Rhythm Prototype");

            if (GUILayout.Button("Start / Restart"))
            {
                RestartSession();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Beat map: " + GetBeatMapSourceName());
            GUILayout.Label("Event count: " + GetEventCountText());
            GUILayout.Label("Running: " + (IsSessionRunning ? "Yes" : "No"));
            GUILayout.Label("Clock: " + GetClockName());
            GUILayout.Label("Audio clip: " + GetAudioClipStatus());
            if (isCountdownActive)
            {
                GUILayout.Label("Starting in: " + FormatCountdownSeconds(GetCountdownRemainingSeconds()));
            }

            GUILayout.Label("Current time: " + FormatSeconds(CurrentTimeSeconds));

            ScoreSnapshot snapshot = GetSnapshot();
            GUILayout.Label("Score: " + snapshot.TotalScore.ToString(CultureInfo.InvariantCulture));
            GUILayout.Label("Current combo: " + snapshot.CurrentCombo.ToString(CultureInfo.InvariantCulture));
            GUILayout.Label("Max combo: " + snapshot.MaxCombo.ToString(CultureInfo.InvariantCulture));
            GUILayout.Label(
                "Perfect / Good / Miss: "
                + snapshot.PerfectCount.ToString(CultureInfo.InvariantCulture)
                + " / "
                + snapshot.GoodCount.ToString(CultureInfo.InvariantCulture)
                + " / "
                + snapshot.MissCount.ToString(CultureInfo.InvariantCulture));
            GUILayout.Label("Last feedback: " + lastFeedback);

            DrawTimingCalibrationPanel();

            GUILayout.Space(8f);
            GUILayout.Label("Combat Debug");
            Rect combatPanelRect = GUILayoutUtility.GetRect(720f, DebugCombatFeedbackRenderer.PanelHeight);
            combatFeedbackRenderer.Draw(combatPanelRect, GetPresentationTimeSeconds(), IsSessionRunning);

            GUILayout.Space(8f);
            GUILayout.Label("Rhythm lane");
            Rect rhythmLaneRect = GUILayoutUtility.GetRect(720f, RhythmLaneHeight);
            rhythmLaneRenderer.Draw(rhythmLaneRect, session == null ? null : session.Events, CurrentTimeSeconds);

            GUILayout.Space(8f);
            GUILayout.Label("Events");
            DrawEventList();

            GUILayout.Space(8f);
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = IsSessionRunning;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Guard (Space)"))
            {
                HandleInput(RhythmAction.Guard);
            }

            if (GUILayout.Button("Strike (J)"))
            {
                HandleInput(RhythmAction.Strike);
            }

            GUILayout.EndHorizontal();
            GUI.enabled = previousGuiEnabled;

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void RestartSession()
        {
            try
            {
                session = new RhythmSession(
                    CreateSessionBeatEvents(),
                    new JudgementWindows(PerfectWindowSeconds, GoodWindowSeconds),
                    new RhythmInputResolver(new BeatEventMatcher(), new HitJudge()),
                    new BeatEventTimeoutProcessor(new HitJudge()));
                scoreTracker = new ScoreTracker();
                combatFeedbackRenderer.Clear();
                ClearPendingInput();
                ClearLastInputTimingError();
                PrepareClockForCountdown();
                StartCountdownOrGameplay();
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
            }
        }

        private IReadOnlyList<BeatEventData> CreateSessionBeatEvents()
        {
            IReadOnlyList<BeatEventData> beatEvents;
            if (debugBeatMapJson != null)
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
            lastFeedback = "Session started";
            StopIfComplete();
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
            if (useAudioClockWhenClipAssigned && debugAudioClip != null)
            {
                return new DspAudioSongClock(GetOrAddAudioSource(), debugAudioClip);
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

        private void HandleKeyboardEvent(Event currentEvent)
        {
            if (!IsSessionRunning || currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            if (currentEvent.keyCode == KeyCode.Space)
            {
                HandleInput(RhythmAction.Guard);
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.J)
            {
                HandleInput(RhythmAction.Strike);
                currentEvent.Use();
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
            if (result.HitResult.Grade == HitGrade.Miss)
            {
                combatFeedbackRenderer.ShowMiss(presentationTimeSeconds);
            }
            else
            {
                combatFeedbackRenderer.ShowHit(action, result.HitResult.Grade, presentationTimeSeconds);
            }

            lastFeedback = FormatFeedback(result.HitResult);
            StopIfComplete();
        }

        private void RecordResult(HitResult result)
        {
            scoreTracker.Record(result);
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
            lastFeedback = "Session complete";
        }

        private void DrawEventList()
        {
            if (session == null)
            {
                GUILayout.Label("No session");
                return;
            }

            for (int i = 0; i < session.Events.Count; i++)
            {
                BeatEventRuntime beatEvent = session.Events[i];
                GUILayout.Label(
                    FormatSeconds(beatEvent.Data.TargetTimeSeconds)
                    + "  "
                    + beatEvent.Data.Action
                    + "  "
                    + beatEvent.State
                    + "  "
                    + beatEvent.Data.EventId);
            }
        }

        private void DrawTimingCalibrationPanel()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Timing Calibration");
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Beatmap offset: " + FormatSignedMilliseconds(debugBeatMapOffsetSeconds));
            GUILayout.Label("Input offset: " + FormatSignedMilliseconds(inputTimingOffsetSeconds));
            GUILayout.Label("Last input timing error: " + GetLastInputTimingErrorText());
            GUILayout.Label("Beatmap offset shifts event times. Applies on next Start/Restart.");
            GUILayout.Label("Input offset shifts judgement input time.");

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
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void AdjustBeatMapOffset(float milliseconds)
        {
            debugBeatMapOffsetSeconds += milliseconds / 1000f;
        }

        private void AdjustInputTimingOffset(float milliseconds)
        {
            inputTimingOffsetSeconds += milliseconds / 1000f;
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
            return debugAudioClip == null ? "None" : debugAudioClip.name;
        }

        private string GetBeatMapSourceName()
        {
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
            if (debugBeatMapJson != null)
            {
                return "Beat map JSON error: " + exception.Message;
            }

            return "Beat map error: " + exception.Message;
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
