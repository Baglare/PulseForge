using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Prototype
{
    public sealed class DebugRhythmLaneRenderer
    {
        private const double LaneLookAheadSeconds = 2.0d;
        private const double LaneLookBehindSeconds = 0.35d;
        private const float LaneHitLineOffset = 120f;
        private const float LaneTargetWidth = 600f;
        private const float LaneMarkerSize = 28f;

        public void Draw(
            Rect area,
            IReadOnlyList<BeatEventRuntime> events,
            double currentTimeSeconds)
        {
            Color previousColor = GUI.color;

            try
            {
                DrawLaneBackground(area);

                float hitLineX = area.x + LaneHitLineOffset;
                float laneWidth = Mathf.Min(LaneTargetWidth, Mathf.Max(1f, area.xMax - hitLineX - 16f));
                float centerY = area.y + 42f;

                DrawBaseline(hitLineX, laneWidth, centerY);
                DrawHitLine(area, hitLineX);
                DrawLaneMarkers(events, currentTimeSeconds, hitLineX, laneWidth, centerY);
            }
            finally
            {
                GUI.color = previousColor;
            }

            GUILayout.Label("Next pending: " + FormatNextPendingEvent(events, currentTimeSeconds));
        }

        private static void DrawLaneBackground(Rect area)
        {
            GUI.color = new Color(0.14f, 0.14f, 0.14f, 1f);
            GUI.Box(area, GUIContent.none);
        }

        private static void DrawBaseline(float hitLineX, float laneWidth, float centerY)
        {
            Rect baseline = new Rect(hitLineX - 110f, centerY - 1f, laneWidth + 126f, 2f);

            GUI.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            GUI.Box(baseline, GUIContent.none);
        }

        private static void DrawHitLine(Rect area, float hitLineX)
        {
            GUI.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            GUI.Box(new Rect(hitLineX - 1f, area.y + 10f, 3f, area.height - 24f), GUIContent.none);
            GUI.Label(new Rect(hitLineX - 24f, area.y + 4f, 70f, 20f), "HIT");
        }

        private static void DrawLaneMarkers(
            IReadOnlyList<BeatEventRuntime> events,
            double currentTimeSeconds,
            float hitLineX,
            float laneWidth,
            float centerY)
        {
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                BeatEventRuntime beatEvent = events[i];
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
                GUI.Label(
                    new Rect(markerRect.x + 8f, markerRect.y + 5f, markerRect.width, markerRect.height),
                    GetActionLabel(beatEvent.Data.Action));

                GUI.color = Color.white;
                GUI.Label(new Rect(markerRect.x - 18f, markerRect.yMax + 2f, 70f, 20f), beatEvent.State.ToString());
            }
        }

        private static string FormatNextPendingEvent(
            IReadOnlyList<BeatEventRuntime> events,
            double currentTimeSeconds)
        {
            BeatEventRuntime nextPending = FindNextPendingEvent(events);
            if (nextPending == null)
            {
                return "None";
            }

            double remainingSeconds = nextPending.Data.TargetTimeSeconds - currentTimeSeconds;
            return nextPending.Data.Action
                + " at "
                + FormatSeconds(nextPending.Data.TargetTimeSeconds)
                + " ("
                + FormatSignedSeconds(remainingSeconds)
                + ")";
        }

        private static BeatEventRuntime FindNextPendingEvent(IReadOnlyList<BeatEventRuntime> events)
        {
            if (events == null)
            {
                return null;
            }

            BeatEventRuntime nextPending = null;
            for (int i = 0; i < events.Count; i++)
            {
                BeatEventRuntime beatEvent = events[i];
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
