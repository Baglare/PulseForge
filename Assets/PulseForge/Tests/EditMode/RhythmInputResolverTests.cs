using System;
using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RhythmInputResolverTests
    {
        private static readonly JudgementWindows Windows = new JudgementWindows(0.05d, 0.12d);

        [Test]
        public void ConstructorRejectsNullMatcher()
        {
            Assert.That(() => new RhythmInputResolver(null, new HitJudge()), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ConstructorRejectsNullJudge()
        {
            Assert.That(() => new RhythmInputResolver(new BeatEventMatcher(), null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ResolveInputRejectsNullEvents()
        {
            RhythmInputResolver resolver = CreateResolver();

            Assert.That(
                () => resolver.ResolveInput(null, RhythmAction.Guard, 1d, Windows),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ResolveInputRejectsNullWindows()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            Assert.That(
                () => resolver.ResolveInput(events, RhythmAction.Guard, 1d, null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ResolveInputRejectsInvalidInputTime()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            Assert.That(
                () => resolver.ResolveInput(events, RhythmAction.Guard, double.NaN, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => resolver.ResolveInput(events, RhythmAction.Guard, double.PositiveInfinity, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => resolver.ResolveInput(events, RhythmAction.Guard, double.NegativeInfinity, Windows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void NoSuitableEventReturnsNoMatch()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            RhythmInputResolveResult result = resolver.ResolveInput(events, RhythmAction.Guard, 1.5d, Windows);

            Assert.That(result.HasMatch, Is.False);
            Assert.That(result.MatchedEvent, Is.Null);
            Assert.That(result.HitResult, Is.Null);
        }

        [Test]
        public void NoSuitableEventDoesNotChangeEventStates()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime pending = CreateRuntime("pending", 1d, RhythmAction.Guard);
            BeatEventRuntime hit = CreateRuntime("hit", 1.01d, RhythmAction.Guard);
            hit.ApplyResult(new HitResult("hit", HitGrade.Perfect, 0d));

            resolver.ResolveInput(new[] { pending, hit }, RhythmAction.Strike, 1d, Windows);

            Assert.That(pending.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(pending.Result, Is.Null);
            Assert.That(hit.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(hit.Result, Is.Not.Null);
        }

        [Test]
        public void MatchingPendingActionEventReturnsMatch()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime expected = CreateRuntime("beat-001", 1d, RhythmAction.Guard);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { expected }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.HasMatch, Is.True);
            Assert.That(result.MatchedEvent, Is.SameAs(expected));
            Assert.That(result.HitResult, Is.Not.Null);
            Assert.That(result.HitResult.EventId, Is.EqualTo(expected.Data.EventId));
        }

        [Test]
        public void PerfectTimingSetsEventToHit()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d, RhythmAction.Guard);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.HitResult.Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(runtime.Result, Is.SameAs(result.HitResult));
        }

        [Test]
        public void GoodTimingSetsEventToHit()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d, RhythmAction.Guard);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1.08d, Windows);

            Assert.That(result.HitResult.Grade, Is.EqualTo(HitGrade.Good));
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(runtime.Result, Is.SameAs(result.HitResult));
        }

        [Test]
        public void WrongActionEventIsNotSelected()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d, RhythmAction.Strike);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.HasMatch, Is.False);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(runtime.Result, Is.Null);
        }

        [Test]
        public void ResolvedEventIsNotSelectedAgain()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d, RhythmAction.Guard);
            runtime.ApplyResult(new HitResult("beat-001", HitGrade.Perfect, 0d));

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.HasMatch, Is.False);
            Assert.That(runtime.State, Is.EqualTo(BeatEventState.Hit));
        }

        [Test]
        public void MultipleCandidatesKeepMatcherDeterministicSelection()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime laterId = CreateRuntime("beat-b", 1d, RhythmAction.Guard);
            BeatEventRuntime earlierId = CreateRuntime("beat-a", 1d, RhythmAction.Guard);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { laterId, earlierId }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.HasMatch, Is.True);
            Assert.That(result.MatchedEvent, Is.SameAs(earlierId));
            Assert.That(earlierId.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(laterId.State, Is.EqualTo(BeatEventState.Pending));
        }

        [Test]
        public void OnlySelectedEventStateChanges()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime selected = CreateRuntime("selected", 1d, RhythmAction.Guard);
            BeatEventRuntime unselected = CreateRuntime("unselected", 1.08d, RhythmAction.Guard);
            BeatEventRuntime wrongAction = CreateRuntime("wrong-action", 1d, RhythmAction.Strike);

            RhythmInputResolveResult result = resolver.ResolveInput(new[] { unselected, wrongAction, selected }, RhythmAction.Guard, 1d, Windows);

            Assert.That(result.MatchedEvent, Is.SameAs(selected));
            Assert.That(selected.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(unselected.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(unselected.Result, Is.Null);
            Assert.That(wrongAction.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(wrongAction.Result, Is.Null);
        }

        [Test]
        public void SecondInputToSameEventDoesNotMatchAfterResolution()
        {
            RhythmInputResolver resolver = CreateResolver();
            BeatEventRuntime runtime = CreateRuntime("beat-001", 1d, RhythmAction.Guard);

            RhythmInputResolveResult first = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1d, Windows);
            RhythmInputResolveResult second = resolver.ResolveInput(new[] { runtime }, RhythmAction.Guard, 1d, Windows);

            Assert.That(first.HasMatch, Is.True);
            Assert.That(second.HasMatch, Is.False);
            Assert.That(second.MatchedEvent, Is.Null);
            Assert.That(second.HitResult, Is.Null);
            Assert.That(runtime.Result, Is.SameAs(first.HitResult));
        }

        [Test]
        public void DomainAssemblyDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(RhythmInputResolver).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static RhythmInputResolver CreateResolver()
        {
            return new RhythmInputResolver(new BeatEventMatcher(), new HitJudge());
        }

        private static BeatEventRuntime CreateRuntime(string eventId, double targetTimeSeconds, RhythmAction action)
        {
            return new BeatEventRuntime(new BeatEventData(eventId, targetTimeSeconds, action, 0.5f));
        }
    }
}
