using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PulseForge.Domain.Rhythm
{
    public sealed class RhythmSession
    {
        private readonly RhythmInputResolver inputResolver;
        private readonly BeatEventTimeoutProcessor timeoutProcessor;

        public RhythmSession(
            IReadOnlyList<BeatEventData> eventData,
            JudgementWindows windows,
            RhythmInputResolver inputResolver,
            BeatEventTimeoutProcessor timeoutProcessor)
        {
            if (eventData == null)
            {
                throw new ArgumentNullException(nameof(eventData));
            }

            Windows = windows ?? throw new ArgumentNullException(nameof(windows));
            this.inputResolver = inputResolver ?? throw new ArgumentNullException(nameof(inputResolver));
            this.timeoutProcessor = timeoutProcessor ?? throw new ArgumentNullException(nameof(timeoutProcessor));

            Events = CreateRuntimeEvents(eventData);
        }

        public IReadOnlyList<BeatEventRuntime> Events { get; }

        public JudgementWindows Windows { get; }

        public int TotalEventCount
        {
            get { return Events.Count; }
        }

        public int ResolvedEventCount
        {
            get
            {
                int resolvedCount = 0;
                for (int i = 0; i < Events.Count; i++)
                {
                    if (Events[i].IsResolved)
                    {
                        resolvedCount++;
                    }
                }

                return resolvedCount;
            }
        }

        public bool IsComplete
        {
            get { return ResolvedEventCount == TotalEventCount; }
        }

        public RhythmInputResolveResult ResolveInput(RhythmAction action, double inputTimeSeconds)
        {
            return inputResolver.ResolveInput(Events, action, inputTimeSeconds, Windows);
        }

        public IReadOnlyList<HitResult> MarkTimedOutEvents(double currentTimeSeconds)
        {
            return timeoutProcessor.MarkTimedOutEvents(Events, currentTimeSeconds, Windows);
        }

        private static IReadOnlyList<BeatEventRuntime> CreateRuntimeEvents(IReadOnlyList<BeatEventData> eventData)
        {
            var seenEventIds = new HashSet<string>(StringComparer.Ordinal);
            var runtimeEvents = new List<BeatEventRuntime>(eventData.Count);

            for (int i = 0; i < eventData.Count; i++)
            {
                BeatEventData data = eventData[i];
                if (data == null)
                {
                    throw new ArgumentException("Event data must not contain null elements.", nameof(eventData));
                }

                if (!seenEventIds.Add(data.EventId))
                {
                    throw new ArgumentException("Event data must not contain duplicate event ids.", nameof(eventData));
                }

                runtimeEvents.Add(new BeatEventRuntime(data));
            }

            return new ReadOnlyCollection<BeatEventRuntime>(runtimeEvents);
        }
    }
}
