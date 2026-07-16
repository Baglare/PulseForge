using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.BeatMaps;
using PulseForge.Runtime.Unity.Persistence;
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

        public void DrawRadial(TextAsset radialBeatMapJson, float height)
        {
            if (radialBeatMapJson == null)
            {
                EditorGUILayout.HelpBox("Radial V2 beatmap not found.", MessageType.Info);
                return;
            }
            if (!RadialBeatMapArtifactSerializer.TryDeserialize(
                radialBeatMapJson.text,
                out RadialBeatMapCacheData artifact,
                out string errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Warning);
                return;
            }

            List<RadialMarker> markers = BuildRadialMarkers(artifact.radialBeatMap);
            if (markers.Count == 0)
            {
                EditorGUILayout.LabelField("Radial V2: no markers", EditorStyles.miniLabel);
                return;
            }

            double firstTime = markers[0].TimeSeconds;
            double lastTime = markers[markers.Count - 1].TimeSeconds;
            EditorGUILayout.LabelField(
                "Radial V2 | " + markers.Count.ToString(CultureInfo.InvariantCulture)
                + " markers | " + FormatSeconds(firstTime) + " - " + FormatSeconds(lastTime),
                EditorStyles.miniLabel);
            EditorGUILayout.SelectableLabel(
                BuildRadialTimelineText(markers, firstTime, lastTime, GetTimelineColumnCount()),
                EditorStyles.miniLabel,
                GUILayout.Height(18f));

            int shownCount = Math.Min(16, markers.Count);
            for (int i = 0; i < shownCount; i++)
            {
                RadialMarker marker = markers[i];
                EditorGUILayout.LabelField(
                    FormatSeconds(marker.TimeSeconds),
                    marker.ActionLabel + " / " + marker.Direction + marker.Detail,
                    EditorStyles.miniLabel);
            }
            if (markers.Count > shownCount)
            {
                EditorGUILayout.LabelField(
                    "...and " + (markers.Count - shownCount).ToString(CultureInfo.InvariantCulture)
                    + " more markers",
                    EditorStyles.miniLabel);
            }
        }

        private static List<RadialMarker> BuildRadialMarkers(RadialBeatMapData beatMap)
        {
            List<RadialMarker> markers = new List<RadialMarker>();
            if (beatMap == null || beatMap.encounters == null)
            {
                return markers;
            }

            for (int encounterIndex = 0;
                encounterIndex < beatMap.encounters.Count;
                encounterIndex++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[encounterIndex];
                if (encounter == null || encounter.requirements == null)
                {
                    continue;
                }

                if (encounter.targets == null || encounter.targets.Count == 0)
                {
                    for (int requirementIndex = 0;
                        requirementIndex < encounter.requirements.Count;
                        requirementIndex++)
                    {
                        AddRadialMarker(
                            markers,
                            encounter.requirements[requirementIndex],
                            RadialDirection.North,
                            null,
                            encounter.failureEffect);
                    }
                    continue;
                }

                for (int targetIndex = 0; targetIndex < encounter.targets.Count; targetIndex++)
                {
                    EncounterTargetData target = encounter.targets[targetIndex];
                    InputRequirementData requirement = FindRequirement(
                        encounter,
                        target == null ? string.Empty : target.requirementId);
                    if (target != null && requirement != null)
                    {
                        AddRadialMarker(
                            markers,
                            requirement,
                            target.direction,
                            target.archetype,
                            encounter.failureEffect);
                    }
                }
            }

            markers.Sort((left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
            return markers;
        }

        private static InputRequirementData FindRequirement(
            RadialEncounterEventData encounter,
            string requirementId)
        {
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                InputRequirementData requirement = encounter.requirements[i];
                if (requirement != null
                    && string.Equals(
                        requirement.requirementId,
                        requirementId,
                        StringComparison.Ordinal))
                {
                    return requirement;
                }
            }
            return encounter.requirements.Count == 1 ? encounter.requirements[0] : null;
        }

        private static void AddRadialMarker(
            ICollection<RadialMarker> markers,
            InputRequirementData requirement,
            RadialDirection direction,
            EnemyArchetype? archetype,
            FailureEffectData failureEffect)
        {
            if (requirement == null)
            {
                return;
            }
            string actionLabel = RhythmActionMaskUtility.TryGetSingleAction(
                requirement.acceptedActions,
                out RhythmAction action)
                ? action.ToString()
                : requirement.acceptedActions.ToString();
            markers.Add(new RadialMarker(
                requirement.targetTimeSeconds,
                actionLabel,
                direction,
                archetype,
                failureEffect));
        }

        private static string BuildRadialTimelineText(
            IReadOnlyList<RadialMarker> markers,
            double minTime,
            double maxTime,
            int timelineColumns)
        {
            char[] timeline = new char[timelineColumns];
            for (int i = 0; i < timeline.Length; i++)
            {
                timeline[i] = '-';
            }
            timeline[0] = '|';
            timeline[timeline.Length - 1] = '|';
            int plotColumns = Math.Max(1, timelineColumns - 2);
            double timeRange = Math.Max(0.0001d, maxTime - minTime);
            for (int i = 0; i < markers.Count; i++)
            {
                double normalizedTime = (markers[i].TimeSeconds - minTime) / timeRange;
                int column = 1 + Mathf.Clamp(
                    Mathf.RoundToInt((float)normalizedTime * (plotColumns - 1)),
                    0,
                    plotColumns - 1);
                char marker = GetRadialMarkerLabel(markers[i].ActionLabel);
                timeline[column] = timeline[column] == '-' ? marker : '*';
            }
            return new string(timeline);
        }

        private static char GetRadialMarkerLabel(string actionLabel)
        {
            if (actionLabel.IndexOf("Heavy", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 'H';
            }
            if (actionLabel.IndexOf("Dodge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 'D';
            }
            if (actionLabel.IndexOf("Light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 'L';
            }
            return 'G';
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

        private readonly struct RadialMarker
        {
            public RadialMarker(
                double timeSeconds,
                string actionLabel,
                RadialDirection direction,
                EnemyArchetype? archetype,
                FailureEffectData failureEffect)
            {
                TimeSeconds = timeSeconds;
                ActionLabel = actionLabel;
                Direction = direction;
                Detail = archetype == EnemyArchetype.Saboteur
                    ? " / Saboteur / Fog "
                        + (failureEffect == null ? 0d : failureEffect.durationSeconds)
                            .ToString("0.0", CultureInfo.InvariantCulture)
                        + "s"
                    : string.Empty;
            }

            public double TimeSeconds { get; }
            public string ActionLabel { get; }
            public RadialDirection Direction { get; }
            public string Detail { get; }
        }
    }
}
