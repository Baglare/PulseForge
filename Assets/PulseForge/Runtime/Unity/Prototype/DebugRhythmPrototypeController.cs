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
        private const double LaneLookAheadSeconds = 2.0d;
        private const double LaneLookBehindSeconds = 0.35d;
        private const float LaneHeight = 96f;
        private const float LaneHitLineOffset = 120f;
        private const float LaneTargetWidth = 600f;
        private const float LaneMarkerSize = 28f;

        [SerializeField] private DebugBeatMapAsset debugBeatMapAsset = null;
        [SerializeField] private AudioClip debugAudioClip = null;
        [SerializeField] private bool useAudioClockWhenClipAssigned = true;

        private RhythmSession session;
        private ScoreTracker scoreTracker;
        private ISongClock songClock;
        private string lastFeedback = "Press Start / Restart";
        private Vector2 eventListScroll;

        private void Start()
        {
            RestartSession();
        }

        private void Update()
        {
            if (!IsSessionRunning || session == null || session.IsComplete)
            {
                return;
            }

            IReadOnlyList<HitResult> timedOutResults = session.MarkTimedOutEvents(CurrentTimeSeconds);
            for (int i = 0; i < timedOutResults.Count; i++)
            {
                RecordResult(timedOutResults[i]);
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

            GUILayout.Space(8f);
            DrawRhythmLane();

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
                RestartClock();
                lastFeedback = "Session started";
                StopIfComplete();
            }
            catch (Exception exception)
            {
                StopClock();
                session = null;
                scoreTracker = new ScoreTracker();
                lastFeedback = "Beat map error: " + exception.Message;
            }
        }

        private IReadOnlyList<BeatEventData> CreateSessionBeatEvents()
        {
            if (debugBeatMapAsset != null)
            {
                return debugBeatMapAsset.BuildBeatEvents();
            }

            return CreateDebugEventData();
        }

        private void RestartClock()
        {
            StopClock();
            songClock = CreateSongClock();
            songClock.Start();
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
            if (!IsSessionRunning || session == null)
            {
                return;
            }

            RhythmInputResolveResult result = session.ResolveInput(action, CurrentTimeSeconds);
            if (!result.HasMatch)
            {
                lastFeedback = "No match";
                return;
            }

            RecordResult(result.HitResult);
            lastFeedback = FormatFeedback(result.HitResult);
            StopIfComplete();
        }

        private void RecordResult(HitResult result)
        {
            scoreTracker.Record(result);
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

        private void DrawRhythmLane()
        {
            GUILayout.Label("Rhythm lane");

            Rect laneRect = GUILayoutUtility.GetRect(720f, LaneHeight);
            Color previousColor = GUI.color;

            GUI.color = new Color(0.14f, 0.14f, 0.14f, 1f);
            GUI.Box(laneRect, GUIContent.none);

            float hitLineX = laneRect.x + LaneHitLineOffset;
            float laneWidth = Mathf.Min(LaneTargetWidth, Mathf.Max(1f, laneRect.xMax - hitLineX - 16f));
            float centerY = laneRect.y + 42f;
            Rect baseline = new Rect(hitLineX - 110f, centerY - 1f, laneWidth + 126f, 2f);

            GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            GUI.Box(baseline, GUIContent.none);

            GUI.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            GUI.Box(new Rect(hitLineX - 1f, laneRect.y + 10f, 3f, LaneHeight - 24f), GUIContent.none);
            GUI.Label(new Rect(hitLineX - 24f, laneRect.y + 4f, 70f, 20f), "HIT");

            DrawLaneMarkers(laneRect, hitLineX, laneWidth, centerY);

            GUI.color = previousColor;
            GUILayout.Label("Next pending: " + FormatNextPendingEvent());
        }

        private void DrawLaneMarkers(Rect laneRect, float hitLineX, float laneWidth, float centerY)
        {
            if (session == null)
            {
                return;
            }

            double currentTimeSeconds = CurrentTimeSeconds;

            for (int i = 0; i < session.Events.Count; i++)
            {
                BeatEventRuntime beatEvent = session.Events[i];
                double deltaSeconds = beatEvent.Data.TargetTimeSeconds - currentTimeSeconds;
                if (deltaSeconds > LaneLookAheadSeconds || deltaSeconds < -LaneLookBehindSeconds)
                {
                    continue;
                }

                float markerX = hitLineX + (float)(deltaSeconds / LaneLookAheadSeconds) * laneWidth;
                Rect markerRect = new Rect(
                    markerX - LaneMarkerSize * 0.5f,
                    centerY - LaneMarkerSize * 0.5f,
                    LaneMarkerSize,
                    LaneMarkerSize);

                GUI.color = GetMarkerColor(beatEvent.State);
                GUI.Box(markerRect, GUIContent.none);

                GUI.color = Color.black;
                GUI.Label(new Rect(markerRect.x + 8f, markerRect.y + 5f, markerRect.width, markerRect.height), GetActionLabel(beatEvent.Data.Action));

                GUI.color = Color.white;
                GUI.Label(new Rect(markerRect.x - 18f, markerRect.yMax + 2f, 70f, 20f), beatEvent.State.ToString());
            }
        }

        private string FormatNextPendingEvent()
        {
            BeatEventRuntime nextPending = FindNextPendingEvent();
            if (nextPending == null)
            {
                return "None";
            }

            double remainingSeconds = nextPending.Data.TargetTimeSeconds - CurrentTimeSeconds;
            return nextPending.Data.Action
                + " at "
                + FormatSeconds(nextPending.Data.TargetTimeSeconds)
                + " ("
                + FormatSignedSeconds(remainingSeconds)
                + ")";
        }

        private BeatEventRuntime FindNextPendingEvent()
        {
            if (session == null)
            {
                return null;
            }

            BeatEventRuntime nextPending = null;
            for (int i = 0; i < session.Events.Count; i++)
            {
                BeatEventRuntime beatEvent = session.Events[i];
                if (beatEvent.State != BeatEventState.Pending)
                {
                    continue;
                }

                if (nextPending == null || beatEvent.Data.TargetTimeSeconds < nextPending.Data.TargetTimeSeconds)
                {
                    nextPending = beatEvent;
                }
            }

            return nextPending;
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
            get { return songClock != null && songClock.IsRunning; }
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
            if (debugBeatMapAsset == null)
            {
                return "Default Hardcoded Beat Map";
            }

            return debugBeatMapAsset.name;
        }

        private string GetEventCountText()
        {
            if (session == null)
            {
                return "0";
            }

            return session.TotalEventCount.ToString(CultureInfo.InvariantCulture);
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

        private static string FormatSignedSeconds(double seconds)
        {
            string sign = seconds >= 0d ? "+" : string.Empty;
            return sign + seconds.ToString("0.000", CultureInfo.InvariantCulture) + "s";
        }

        private static string FormatSeconds(double seconds)
        {
            return seconds.ToString("0.000", CultureInfo.InvariantCulture) + "s";
        }

        private static string GetActionLabel(RhythmAction action)
        {
            return action == RhythmAction.Guard ? "G" : "S";
        }

        private static Color GetMarkerColor(BeatEventState state)
        {
            switch (state)
            {
                case BeatEventState.Pending:
                    return new Color(1f, 0.84f, 0.25f, 1f);
                case BeatEventState.Hit:
                    return new Color(0.25f, 0.9f, 0.35f, 1f);
                case BeatEventState.Missed:
                    return new Color(1f, 0.3f, 0.25f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
