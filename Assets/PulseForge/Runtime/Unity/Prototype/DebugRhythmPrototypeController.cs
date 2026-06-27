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

        [SerializeField] private DebugBeatMapAsset debugBeatMapAsset = null;
        [SerializeField] private AudioClip debugAudioClip = null;
        [SerializeField] private bool useAudioClockWhenClipAssigned = true;

        private RhythmSession session;
        private ScoreTracker scoreTracker;
        private ISongClock songClock;
        private string lastFeedback = "Press Start / Restart";
        private Vector2 eventListScroll;
        private readonly DebugRhythmLaneRenderer rhythmLaneRenderer = new DebugRhythmLaneRenderer();
        private readonly DebugCombatFeedbackRenderer combatFeedbackRenderer = new DebugCombatFeedbackRenderer();

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
                RestartClock();
                lastFeedback = "Session started";
                StopIfComplete();
            }
            catch (Exception exception)
            {
                StopClock();
                session = null;
                scoreTracker = new ScoreTracker();
                combatFeedbackRenderer.Clear();
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

        private static double GetPresentationTimeSeconds()
        {
            return Time.realtimeSinceStartupAsDouble;
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
    }
}
