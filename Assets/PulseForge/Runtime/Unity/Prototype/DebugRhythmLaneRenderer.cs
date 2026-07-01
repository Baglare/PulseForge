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

        private static readonly Color LaneBackgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        private static readonly Color LaneBaselineColor = new Color(0.34f, 0.4f, 0.46f, 1f);
        private static readonly Color HitLineColor = new Color(0.92f, 0.96f, 1f, 1f);
        private static readonly Color GuardColor = new Color(0.22f, 0.82f, 1f, 1f);
        private static readonly Color StrikeColor = new Color(1f, 0.38f, 0.22f, 1f);
        private static readonly Color HitColor = new Color(0.42f, 0.92f, 0.48f, 1f);
        private static readonly Color MissedColor = new Color(1f, 0.22f, 0.2f, 1f);
        private static readonly Color MutedTextColor = new Color(0.64f, 0.7f, 0.76f, 1f);

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
                DrawLegend(area);
            }
            finally
            {
                GUI.color = previousColor;
            }

            DrawNextPendingEvent(events, currentTimeSeconds);
        }

        private static void DrawLaneBackground(Rect area)
        {
            GUI.color = LaneBackgroundColor;
            GUI.Box(area, GUIContent.none);
        }

        private static void DrawBaseline(float hitLineX, float laneWidth, float centerY)
        {
            Rect baseline = new Rect(hitLineX - 110f, centerY - 1f, laneWidth + 126f, 2f);

            GUI.color = LaneBaselineColor;
            GUI.Box(baseline, GUIContent.none);
        }

        private static void DrawHitLine(Rect area, float hitLineX)
        {
            GUI.color = HitLineColor;
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

                GUI.color = GetActionColor(beatEvent.Data.Action);
                GUI.Box(markerRect, GUIContent.none);

                Rect innerMarkerRect = new Rect(
                    markerRect.x + 4f,
                    markerRect.y + 4f,
                    markerRect.width - 8f,
                    markerRect.height - 8f);
                GUI.color = GetMarkerFillColor(beatEvent.Data.Action, beatEvent.State);
                GUI.Box(innerMarkerRect, GUIContent.none);

                GUI.color = GetMarkerTextColor(beatEvent.State);
                GUI.Label(
                    new Rect(markerRect.x + 8f, markerRect.y + 5f, markerRect.width, markerRect.height),
                    GetActionLabel(beatEvent.Data.Action));

                GUI.color = GetStateTextColor(beatEvent.State);
                GUI.Label(new Rect(markerRect.x - 18f, markerRect.yMax + 2f, 70f, 20f), beatEvent.State.ToString());
            }
        }

        private static void DrawNextPendingEvent(
            IReadOnlyList<BeatEventRuntime> events,
            double currentTimeSeconds)
        {
            BeatEventRuntime nextPending = FindNextPendingEvent(events);
            Color previousContentColor = GUI.contentColor;
            GUILayout.BeginHorizontal();
            GUI.contentColor = MutedTextColor;
            GUILayout.Label("Next pending:", GUILayout.Width(92f));

            if (nextPending == null)
            {
                GUILayout.Label("None");
                GUILayout.EndHorizontal();
                GUI.contentColor = previousContentColor;
                return;
            }

            double remainingSeconds = nextPending.Data.TargetTimeSeconds - currentTimeSeconds;
            GUI.contentColor = GetActionColor(nextPending.Data.Action);
            GUILayout.Label(
                nextPending.Data.Action
                + " at "
                + FormatSeconds(nextPending.Data.TargetTimeSeconds)
                + " ("
                + FormatSignedSeconds(remainingSeconds)
                + ")");
            GUILayout.EndHorizontal();
            GUI.contentColor = previousContentColor;
        }

        private static void DrawLegend(Rect area)
        {
            float x = area.x + 12f;
            float y = area.yMax - 22f;
            DrawLegendItem(ref x, y, GuardColor, "Guard");
            DrawLegendItem(ref x, y, StrikeColor, "Strike");
            DrawLegendItem(ref x, y, HitColor, "Hit");
            DrawLegendItem(ref x, y, MissedColor, "Missed");
        }

        private static void DrawLegendItem(ref float x, float y, Color color, string label)
        {
            GUI.color = color;
            GUI.Box(new Rect(x, y + 4f, 10f, 10f), GUIContent.none);
            GUI.color = MutedTextColor;
            GUI.Label(new Rect(x + 14f, y, 58f, 18f), label);
            x += 72f;
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

        private static Color GetActionColor(RhythmAction action)
        {
            return action == RhythmAction.Guard ? GuardColor : StrikeColor;
        }

        private static Color GetMarkerFillColor(RhythmAction action, BeatEventState state)
        {
            switch (state)
            {
                case BeatEventState.Pending:
                    return Color.Lerp(GetActionColor(action), Color.black, 0.18f);
                case BeatEventState.Hit:
                    return HitColor;
                case BeatEventState.Missed:
                    return MissedColor;
                default:
                    return Color.white;
            }
        }

        private static Color GetMarkerTextColor(BeatEventState state)
        {
            return state == BeatEventState.Pending ? Color.white : Color.black;
        }

        private static Color GetStateTextColor(BeatEventState state)
        {
            switch (state)
            {
                case BeatEventState.Hit:
                    return HitColor;
                case BeatEventState.Missed:
                    return MissedColor;
                default:
                    return MutedTextColor;
            }
        }
    }
}
