using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.BeatMaps
{
    public static class DebugBeatMapJsonParser
    {
        private const int SupportedSchemaVersion = 1;

        public static IReadOnlyList<BeatEventData> BuildBeatEvents(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Beat map JSON must not be null, empty, or whitespace.", nameof(json));
            }

            DebugBeatMapJson beatMap = ParseBeatMap(json);
            if (beatMap.schemaVersion != SupportedSchemaVersion)
            {
                throw new ArgumentException(
                    "Unsupported beat map schemaVersion "
                    + beatMap.schemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ". Supported schemaVersion is "
                    + SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                    + ".",
                    nameof(json));
            }

            if (beatMap.events == null)
            {
                throw new ArgumentException("Beat map JSON must contain an events array.", nameof(json));
            }

            if (beatMap.events.Length == 0)
            {
                return Array.Empty<BeatEventData>();
            }

            var usedEventIds = new HashSet<string>(StringComparer.Ordinal);
            BeatEventData[] beatEvents = new BeatEventData[beatMap.events.Length];
            for (int i = 0; i < beatMap.events.Length; i++)
            {
                beatEvents[i] = BuildBeatEvent(beatMap.events[i], beatMap.globalOffsetSeconds, i, usedEventIds);
            }

            return beatEvents;
        }

        private static DebugBeatMapJson ParseBeatMap(string json)
        {
            try
            {
                DebugBeatMapJson beatMap = JsonUtility.FromJson<DebugBeatMapJson>(json);
                if (beatMap == null)
                {
                    throw new ArgumentException("Beat map JSON could not be parsed.");
                }

                return beatMap;
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException("Invalid beat map JSON: " + exception.Message, nameof(json), exception);
            }
            catch (Exception exception)
            {
                throw new ArgumentException("Invalid beat map JSON: " + exception.Message, nameof(json), exception);
            }
        }

        private static BeatEventData BuildBeatEvent(
            DebugBeatEventJson beatEvent,
            float globalOffsetSeconds,
            int index,
            HashSet<string> usedEventIds)
        {
            if (beatEvent == null)
            {
                throw new ArgumentException("Beat map event at index " + index.ToString(CultureInfo.InvariantCulture) + " is null.");
            }

            string eventId = CreateEventId(beatEvent.eventId, index);
            if (!usedEventIds.Add(eventId))
            {
                throw new ArgumentException("Duplicate beat map eventId '" + eventId + "'.");
            }

            RhythmAction action = ParseAction(beatEvent.action, index);
            double targetTimeSeconds = beatEvent.targetTimeSeconds + globalOffsetSeconds;
            float intensity = Mathf.Clamp01(beatEvent.intensity);

            try
            {
                return new BeatEventData(eventId, targetTimeSeconds, action, intensity);
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    "Invalid beat map event at index "
                    + index.ToString(CultureInfo.InvariantCulture)
                    + ": "
                    + exception.Message,
                    exception);
            }
        }

        private static RhythmAction ParseAction(string action, int index)
        {
            if (string.Equals(action, nameof(RhythmAction.Guard), StringComparison.OrdinalIgnoreCase))
            {
                return RhythmAction.Guard;
            }

            if (string.Equals(action, nameof(RhythmAction.Strike), StringComparison.OrdinalIgnoreCase))
            {
                return RhythmAction.Strike;
            }

            string actionText = string.IsNullOrWhiteSpace(action) ? "<empty>" : action;
            throw new ArgumentException(
                "Unknown beat map action '"
                + actionText
                + "' at index "
                + index.ToString(CultureInfo.InvariantCulture)
                + ". Supported actions are Guard and Strike.");
        }

        private static string CreateEventId(string eventId, int index)
        {
            if (!string.IsNullOrWhiteSpace(eventId))
            {
                return eventId;
            }

            return "event-" + (index + 1).ToString("000", CultureInfo.InvariantCulture);
        }

        [Serializable]
        private sealed class DebugBeatMapJson
        {
            public int schemaVersion = 0;
            public string displayName = string.Empty;
            public float globalOffsetSeconds = 0f;
            public DebugBeatEventJson[] events = null;
        }

        [Serializable]
        private sealed class DebugBeatEventJson
        {
            public string eventId = string.Empty;
            public float targetTimeSeconds = 0f;
            public string action = string.Empty;
            public float intensity = 1f;
        }
    }
}
