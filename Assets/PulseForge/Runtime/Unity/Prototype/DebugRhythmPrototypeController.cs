using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugRhythmPrototypeController : MonoBehaviour
    {
        private const double PerfectWindowSeconds = 0.045d;
        private const double GoodWindowSeconds = 0.100d;

        private RhythmSession session;
        private ScoreTracker scoreTracker;
        private double sessionStartTime;
        private double stoppedTimeSeconds;
        private bool isRunning;
        private string lastFeedback = "Press Start / Restart";
        private Vector2 eventListScroll;

        private void Start()
        {
            RestartSession();
        }

        private void Update()
        {
            if (!isRunning || session == null || session.IsComplete)
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

            GUILayout.BeginArea(new Rect(10f, 10f, 540f, Screen.height - 20f), GUI.skin.box);
            eventListScroll = GUILayout.BeginScrollView(eventListScroll);

            GUILayout.Label("PulseForge Debug Rhythm Prototype");

            if (GUILayout.Button("Start / Restart"))
            {
                RestartSession();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Running: " + (isRunning ? "Yes" : "No"));
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
            GUILayout.Label("Events");
            DrawEventList();

            GUILayout.Space(8f);
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = isRunning;
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
            session = new RhythmSession(
                CreateDebugEventData(),
                new JudgementWindows(PerfectWindowSeconds, GoodWindowSeconds),
                new RhythmInputResolver(new BeatEventMatcher(), new HitJudge()),
                new BeatEventTimeoutProcessor(new HitJudge()));
            scoreTracker = new ScoreTracker();
            sessionStartTime = Time.realtimeSinceStartupAsDouble;
            stoppedTimeSeconds = 0d;
            isRunning = true;
            lastFeedback = "Session started";
        }

        private void HandleKeyboardEvent(Event currentEvent)
        {
            if (!isRunning || currentEvent == null || currentEvent.type != EventType.KeyDown)
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
            if (!isRunning || session == null)
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

            stoppedTimeSeconds = CurrentTimeSeconds;
            isRunning = false;
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
                if (session == null)
                {
                    return 0d;
                }

                if (!isRunning)
                {
                    return stoppedTimeSeconds;
                }

                return Time.realtimeSinceStartupAsDouble - sessionStartTime;
            }
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
