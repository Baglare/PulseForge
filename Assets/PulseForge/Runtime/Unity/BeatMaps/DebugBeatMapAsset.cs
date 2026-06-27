using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.BeatMaps
{
    [CreateAssetMenu(menuName = "PulseForge/Debug Beat Map")]
    public sealed class DebugBeatMapAsset : ScriptableObject
    {
        [SerializeField] private string displayName = "Debug Beat Map";
        [SerializeField] private string description = string.Empty;
        [SerializeField] private float globalOffsetSeconds = 0f;
        [SerializeField] private List<DebugBeatEvent> events = new List<DebugBeatEvent>();

        public string DisplayName
        {
            get { return displayName; }
        }

        public string Description
        {
            get { return description; }
        }

        public IReadOnlyList<BeatEventData> BuildBeatEvents()
        {
            BeatEventData[] beatEvents = new BeatEventData[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                DebugBeatEvent beatEvent = events[i];
                string eventId = CreateEventId(beatEvent.EventId, i);
                double targetTimeSeconds = beatEvent.TargetTimeSeconds + globalOffsetSeconds;
                float intensity = Mathf.Clamp01(beatEvent.Intensity);
                beatEvents[i] = new BeatEventData(eventId, targetTimeSeconds, beatEvent.Action, intensity);
            }

            return beatEvents;
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
        private sealed class DebugBeatEvent
        {
            [SerializeField] private string eventId = string.Empty;
            [SerializeField] private float targetTimeSeconds = 0f;
            [SerializeField] private RhythmAction action = RhythmAction.Guard;
            [SerializeField] private float intensity = 1f;

            public string EventId
            {
                get { return eventId; }
            }

            public float TargetTimeSeconds
            {
                get { return targetTimeSeconds; }
            }

            public RhythmAction Action
            {
                get { return action; }
            }

            public float Intensity
            {
                get { return intensity; }
            }
        }
    }
}
