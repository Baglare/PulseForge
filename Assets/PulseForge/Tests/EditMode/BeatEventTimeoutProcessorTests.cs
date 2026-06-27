using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class BeatEventTimeoutProcessorTests
    {
        private static readonly JudgementWindows Windows = new JudgementWindows(0.05d, 0.12d);

        [Test]
        public void ConstructorRejectsNullJudge()
        {
            Assert.That(() => new BeatEventTimeoutProcessor(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void MarkTimedOutEventsRejectsNullEvents()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();

            Assert.That(
                () => processor.MarkTimedOutEvents(null, 1d, Windows),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void MarkTimedOutEventsRejectsNullWindows()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d) };

            Assert.That(
                () => processor.MarkTimedOutEvents(events, 1d, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void MarkTimedOutEventsRejectsNullEventElement()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d), null };

            Assert.That(
                () => processor.MarkTimedOutEvents(events, 2d, Windows),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void MarkTimedOutEventsRejectsInvalidCurrentTime()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d) };

            Assert.That(
                () => processor.MarkTimedOutEvents(events, double.NaN, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => processor.MarkTimedOutEvents(events, double.PositiveInfinity, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => processor.MarkTimedOutEvents(events, double.NegativeInfinity, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => processor.MarkTimedOutEvents(events, -0.001d, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void EmptyEventListReturnsEmptyResults()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(Array.Empty<BeatEventRuntime>(), 1d, Windows);

            Assert.That(results, Is.Empty);
        }

        [Test]
        public void PendingEventBeforeTargetTimeIsNotMissed()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 0.9d, Windows);

            Assert.That(results, Is.Empty);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(runtime.Result, Is.Null);
        }

        [Test]
        public void PendingEventInsideGoodWindowIsNotMissed()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 1.08d, Windows);

            Assert.That(results, Is.Empty);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(runtime.Result, Is.Null);
        }

        [Test]
        public void PendingEventAtExactGoodBoundaryIsNotMissed()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 1d + Windows.GoodWindowSeconds, Windows);

            Assert.That(results, Is.Empty);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(runtime.Result, Is.Null);
        }

        [Test]
        public void PendingEventPastGoodBoundaryIsMissed()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 1d + Windows.GoodWindowSeconds + 0.001d, Windows);

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(runtime.Result, Is.SameAs(results[0]));
        }

        [Test]
        public void TimedOutResultIsMissWithCorrectEventId()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 1.3d, Windows);

            Assert.That(results[0].Grade, Is.EqualTo(HitGrade.Miss));
            Assert.That(results[0].EventId, Is.EqualTo("beat-001"));
        }

        [Test]
        public void MultipleTimedOutEventsAreAllMissed()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime first = CreateRuntime("first", 1d);
            BeatEventRuntime second = CreateRuntime("second", 1.1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { first, second }, 1.5d, Windows);

            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(first.State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(second.State, Is.EqualTo(BeatEventState.Missed));
        }

        [Test]
        public void ResultsKeepInputEventOrder()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime first = CreateRuntime("first", 1d);
            BeatEventRuntime second = CreateRuntime("second", 1.1d);
            BeatEventRuntime third = CreateRuntime("third", 1.2d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { first, second, third }, 1.5d, Windows);

            Assert.That(results.Select(result => result.EventId).ToArray(), Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void HitEventIsNotChanged()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);
            HitResult originalResult = new HitResult("beat-001", HitGrade.Perfect, 0d);
            runtime.ApplyResult(originalResult);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 2d, Windows);

            Assert.That(results, Is.Empty);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(runtime.Result, Is.SameAs(originalResult));
        }

        [Test]
        public void MissedEventIsNotChanged()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);
            HitResult originalResult = new HitResult("beat-001", HitGrade.Miss, 0.2d);
            runtime.ApplyResult(originalResult);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 2d, Windows);

            Assert.That(results, Is.Empty);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(runtime.Result, Is.SameAs(originalResult));
        }

        [Test]
        public void UntimedOutEventRemainsPendingWhenOtherEventTimesOut()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime timedOut = CreateRuntime("timed-out", 1d);
            BeatEventRuntime pending = CreateRuntime("pending", 2d);

            processor.MarkTimedOutEvents(new[] { timedOut, pending }, 1.5d, Windows);

            Assert.That(timedOut.State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(pending.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(pending.Result, Is.Null);
        }

        [Test]
        public void TimedOutResultTimingErrorUsesCurrentTimeMinusTargetTime()
        {
            BeatEventTimeoutProcessor processor = CreateProcessor();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d);

            IReadOnlyList<HitResult> results = processor.MarkTimedOutEvents(new[] { runtime }, 1.3d, Windows);

            Assert.That(results[0].TimingErrorSeconds, Is.EqualTo(0.3d).Within(0.0000001d));
        }

        [Test]
        public void DomainAssemblyDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(BeatEventTimeoutProcessor).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static BeatEventTimeoutProcessor CreateProcessor()
        {
            return new BeatEventTimeoutProcessor(new HitJudge());
        }

        private static BeatEventRuntime CreateRuntime(string eventId, double targetTimeSeconds)
        {
            return new BeatEventRuntime(new BeatEventData(eventId, targetTimeSeconds, RhythmAction.Guard, 0.5f));
        }
    }
}
