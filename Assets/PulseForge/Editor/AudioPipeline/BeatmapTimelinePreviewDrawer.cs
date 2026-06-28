using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.BeatMaps;
using UnityEditor;
using UnityEngine;

namespace PulseForge.Editor.AudioPipeline
{
    public sealed class BeatmapTimelinePreviewDrawer
    {
        private const int MinimumTimelineColumns = 32;
        private const int MaximumTimelineColumns = 120;
        private const float ApproximateCharacterWidth = 7f;

        public void Draw(TextAsset rawBeatMapJson, TextAsset playableBeatMapJson, float height)
        {
            TimelineData rawTimeline = BuildTimelineData("Raw", rawBeatMapJson, "Raw beatmap not found");
            TimelineData playableTimeline = BuildTimelineData("Playable", playableBeatMapJson, "Playable beatmap not found");

            bool hasTimeRange = TryGetTimeRange(rawTimeline, playableTimeline, out double minTime, out double maxTime);
            int timelineColumns = GetTimelineColumnCount();

            EditorGUILayout.LabelField(
                hasTimeRange
                    ? "Shared time scale: " + FormatSeconds(minTime) + " - " + FormatSeconds(maxTime)
                    : "Shared time scale: no events",
                EditorStyles.miniLabel);

            DrawLane(rawTimeline, minTime, maxTime, timelineColumns);
            DrawLane(playableTimeline, minTime, maxTime, timelineColumns);
        }

        private static TimelineData BuildTimelineData(string label, TextAsset beatMapJson, string missingMessage)
        {
            TimelineData timelineData = new TimelineData(label, beatMapJson, missingMessage);
            if (beatMapJson == null)
            {
                return timelineData;
            }

            try
            {
                IReadOnlyList<BeatEventData> events = DebugBeatMapJsonParser.BuildBeatEvents(beatMapJson.text);
                timelineData.SetEvents(events);
            }
            catch (Exception exception)
            {
                timelineData.SetError("Parse error: " + exception.Message);
            }

            return timelineData;
        }

        private static void DrawLane(TimelineData timelineData, double minTime, double maxTime, int timelineColumns)
        {
            EditorGUILayout.LabelField(timelineData.Label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(timelineData.SummaryText, EditorStyles.miniLabel);

            if (!timelineData.CanDrawEvents)
            {
                return;
            }

            EditorGUILayout.SelectableLabel(
                BuildTimelineText(timelineData.Events, minTime, maxTime, timelineColumns),
                EditorStyles.miniLabel,
                GUILayout.Height(18f));
        }

        private static string BuildTimelineText(
            IReadOnlyList<BeatEventData> events,
            double minTime,
            double maxTime,
            int timelineColumns)
        {
            int plotColumns = Math.Max(1, timelineColumns - 2);
            char[] timeline = new char[timelineColumns];
            for (int i = 0; i < timeline.Length; i++)
            {
                timeline[i] = '-';
            }

            timeline[0] = '|';
            timeline[timeline.Length - 1] = '|';

            double timeRange = Math.Max(0.0001d, maxTime - minTime);
            for (int i = 0; i < events.Count; i++)
            {
                BeatEventData beatEvent = events[i];
                double normalizedTime = (beatEvent.TargetTimeSeconds - minTime) / timeRange;
                int markerColumn = 1 + Mathf.Clamp(
                    Mathf.RoundToInt((float)normalizedTime * (plotColumns - 1)),
                    0,
                    plotColumns - 1);

                timeline[markerColumn] = GetMarkerLabel(beatEvent.Action);
            }

            return new string(timeline);
        }

        private static bool TryGetTimeRange(TimelineData rawTimeline, TimelineData playableTimeline, out double minTime, out double maxTime)
        {
            minTime = double.MaxValue;
            maxTime = double.MinValue;
            bool hasEvent = false;

            IncludeTimeline(rawTimeline, ref minTime, ref maxTime, ref hasEvent);
            IncludeTimeline(playableTimeline, ref minTime, ref maxTime, ref hasEvent);

            if (!hasEvent)
            {
                minTime = 0d;
                maxTime = 1d;
                return false;
            }

            if (maxTime - minTime < 0.0001d)
            {
                minTime = Math.Max(0d, minTime - 0.5d);
                maxTime += 0.5d;
            }

            return true;
        }

        private static void IncludeTimeline(TimelineData timelineData, ref double minTime, ref double maxTime, ref bool hasEvent)
        {
            if (!timelineData.CanDrawEvents)
            {
                return;
            }

            minTime = Math.Min(minTime, timelineData.FirstTimeSeconds);
            maxTime = Math.Max(maxTime, timelineData.LastTimeSeconds);
            hasEvent = true;
        }

        private static char GetMarkerLabel(RhythmAction action)
        {
            return action == RhythmAction.Guard ? 'G' : 'S';
        }

        private static string FormatSeconds(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "s";
        }

        private static int GetTimelineColumnCount()
        {
            float availableWidth = Mathf.Max(1f, EditorGUIUtility.currentViewWidth - 48f);
            return Mathf.Clamp(
                Mathf.FloorToInt(availableWidth / ApproximateCharacterWidth),
                MinimumTimelineColumns,
                MaximumTimelineColumns);
        }

        private sealed class TimelineData
        {
            private readonly string missingMessage;
            private string errorMessage;

            public TimelineData(string label, TextAsset asset, string missingMessage)
            {
                Label = label;
                Asset = asset;
                this.missingMessage = missingMessage;
                Events = Array.Empty<BeatEventData>();
            }

            public string Label { get; }

            public TextAsset Asset { get; }

            public IReadOnlyList<BeatEventData> Events { get; private set; }

            public double FirstTimeSeconds { get; private set; }

            public double LastTimeSeconds { get; private set; }

            public bool CanDrawEvents
            {
                get { return Asset != null && string.IsNullOrEmpty(errorMessage) && Events.Count > 0; }
            }

            public string SummaryText
            {
                get
                {
                    if (Asset == null)
                    {
                        return missingMessage;
                    }

                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        return errorMessage;
                    }

                    if (Events.Count == 0)
                    {
                        return "0 events | first: n/a | last: n/a";
                    }

                    return Events.Count.ToString(CultureInfo.InvariantCulture)
                        + " events | first: "
                        + FormatSeconds(FirstTimeSeconds)
                        + " | last: "
                        + FormatSeconds(LastTimeSeconds);
                }
            }

            public void SetEvents(IReadOnlyList<BeatEventData> events)
            {
                Events = events ?? Array.Empty<BeatEventData>();
                if (Events.Count == 0)
                {
                    FirstTimeSeconds = 0d;
                    LastTimeSeconds = 0d;
                    return;
                }

                double firstTime = double.MaxValue;
                double lastTime = double.MinValue;
                for (int i = 0; i < Events.Count; i++)
                {
                    firstTime = Math.Min(firstTime, Events[i].TargetTimeSeconds);
                    lastTime = Math.Max(lastTime, Events[i].TargetTimeSeconds);
                }

                FirstTimeSeconds = firstTime;
                LastTimeSeconds = lastTime;
            }

            public void SetError(string message)
            {
                errorMessage = message;
                Events = Array.Empty<BeatEventData>();
            }
        }
    }
}
