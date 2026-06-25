using System;
using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class BeatEventRuntimeTests
    {
        private const string EventId = "beat-001";

        [Test]
        public void ConstructorRejectsNullBeatEventData()
        {
            Assert.That(() => new BeatEventRuntime(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void NewRuntimeStartsPending()
        {
            var runtime = CreateRuntime();

            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
        }

        [Test]
        public void NewRuntimeIsNotResolved()
        {
            var runtime = CreateRuntime();

            Assert.That(runtime.IsResolved, Is.False);
        }

        [Test]
        public void PerfectResultSetsStateToHit()
        {
            var runtime = CreateRuntime();

            runtime.ApplyResult(CreateResult(HitGrade.Perfect));

            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(runtime.IsResolved, Is.True);
        }

        [Test]
        public void GoodResultSetsStateToHit()
        {
            var runtime = CreateRuntime();

            runtime.ApplyResult(CreateResult(HitGrade.Good));

            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(runtime.IsResolved, Is.True);
        }

        [Test]
        public void MissResultSetsStateToMissed()
        {
            var runtime = CreateRuntime();

            runtime.ApplyResult(CreateResult(HitGrade.Miss));

            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(runtime.IsResolved, Is.True);
        }

        [Test]
        public void ApplyResultStoresSameResultInstance()
        {
            var runtime = CreateRuntime();
            HitResult result = CreateResult(HitGrade.Perfect);

            runtime.ApplyResult(result);

            Assert.That(runtime.Result, Is.SameAs(result));
        }

        [Test]
        public void ApplyResultRejectsDifferentEventId()
        {
            var runtime = CreateRuntime();
            var result = new HitResult("other-beat", HitGrade.Perfect, 0d);

            Assert.That(() => runtime.ApplyResult(result), Throws.TypeOf<ArgumentException>());
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(runtime.Result, Is.Null);
        }

        [Test]
        public void ApplyResultRejectsNullResult()
        {
            var runtime = CreateRuntime();

            Assert.That(() => runtime.ApplyResult(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void HitEventCannotBeChangedAgain()
        {
            var runtime = CreateRuntime();
            runtime.ApplyResult(CreateResult(HitGrade.Perfect));

            Assert.That(() => runtime.ApplyResult(CreateResult(HitGrade.Good)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
        }

        [Test]
        public void MissedEventCannotBeChangedAgain()
        {
            var runtime = CreateRuntime();
            runtime.ApplyResult(CreateResult(HitGrade.Miss));

            Assert.That(() => runtime.ApplyResult(CreateResult(HitGrade.Perfect)), Throws.TypeOf<InvalidOperationException>());
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Missed));
        }

        [Test]
        public void BeatEventRuntimeDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(BeatEventRuntime).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static BeatEventRuntime CreateRuntime()
        {
            return new BeatEventRuntime(new BeatEventData(EventId, 1d, RhythmAction.Guard, 0.5f));
        }

        private static HitResult CreateResult(HitGrade grade)
        {
            return new HitResult(EventId, grade, 0d);
        }
    }
}
