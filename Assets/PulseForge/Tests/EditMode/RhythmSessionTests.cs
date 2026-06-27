using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RhythmSessionTests
    {
        private static readonly JudgementWindows Windows = new JudgementWindows(0.05d, 0.12d);

        [Test]
        public void ConstructorRejectsNullEventData()
        {
            Assert.That(
                () => new RhythmSession(null, Windows, CreateInputResolver(), CreateTimeoutProcessor()),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ConstructorRejectsNullWindows()
        {
            Assert.That(
                () => new RhythmSession(Array.Empty<BeatEventData>(), null, CreateInputResolver(), CreateTimeoutProcessor()),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ConstructorRejectsNullInputResolver()
        {
            Assert.That(
                () => new RhythmSession(Array.Empty<BeatEventData>(), Windows, null, CreateTimeoutProcessor()),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ConstructorRejectsNullTimeoutProcessor()
        {
            Assert.That(
                () => new RhythmSession(Array.Empty<BeatEventData>(), Windows, CreateInputResolver(), null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void ConstructorRejectsNullEventDataElement()
        {
            BeatEventData[] eventData = { CreateData("beat-001", 1d, RhythmAction.Guard), null };

            Assert.That(
                () => CreateSession(eventData),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ConstructorRejectsDuplicateEventId()
        {
            BeatEventData[] eventData =
            {
                CreateData("beat-001", 1d, RhythmAction.Guard),
                CreateData("beat-001", 2d, RhythmAction.Strike)
            };

            Assert.That(
                () => CreateSession(eventData),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void EmptyEventDataListIsAccepted()
        {
            RhythmSession session = CreateSession(Array.Empty<BeatEventData>());

            Assert.That(session.Events, Is.Empty);
            Assert.That(session.Windows, Is.SameAs(Windows));
        }

        [Test]
        public void EmptySessionCountsAndCompletionAreCorrect()
        {
            RhythmSession session = CreateSession(Array.Empty<BeatEventData>());

            Assert.That(session.TotalEventCount, Is.EqualTo(0));
            Assert.That(session.ResolvedEventCount, Is.EqualTo(0));
            Assert.That(session.IsComplete, Is.True);
        }

        [Test]
        public void ConstructorCreatesRuntimeForEachBeatEventData()
        {
            BeatEventData first = CreateData("first", 1d, RhythmAction.Guard);
            BeatEventData second = CreateData("second", 2d, RhythmAction.Strike);

            RhythmSession session = CreateSession(new[] { first, second });

            Assert.That(session.Events, Has.Count.EqualTo(2));
            Assert.That(session.Events[0], Is.TypeOf<BeatEventRuntime>());
            Assert.That(session.Events[1], Is.TypeOf<BeatEventRuntime>());
            Assert.That(session.Events[0].Data, Is.SameAs(first));
            Assert.That(session.Events[1].Data, Is.SameAs(second));
        }

        [Test]
        public void EventsPreserveInputOrder()
        {
            BeatEventData first = CreateData("first", 1d, RhythmAction.Guard);
            BeatEventData second = CreateData("second", 2d, RhythmAction.Strike);
            BeatEventData third = CreateData("third", 3d, RhythmAction.Guard);

            RhythmSession session = CreateSession(new[] { first, second, third });

            Assert.That(session.Events.Select(runtime => runtime.Data.EventId).ToArray(), Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void EventsCollectionCannotBeModifiedThroughCollectionInterface()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });
            var eventsAsCollection = (ICollection<BeatEventRuntime>)session.Events;

            Assert.That(eventsAsCollection.IsReadOnly, Is.True);
            Assert.That(() => eventsAsCollection.Add(new BeatEventRuntime(CreateData("extra", 2d, RhythmAction.Guard))), Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void NewNonEmptySessionEventsStartPending()
        {
            RhythmSession session = CreateSession(new[]
            {
                CreateData("first", 1d, RhythmAction.Guard),
                CreateData("second", 2d, RhythmAction.Strike)
            });

            Assert.That(session.Events.All(runtime => runtime.State == BeatEventState.Pending), Is.True);
            Assert.That(session.Events.All(runtime => runtime.Result == null), Is.True);
        }

        [Test]
        public void NewNonEmptySessionIsNotComplete()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            Assert.That(session.TotalEventCount, Is.EqualTo(1));
            Assert.That(session.ResolvedEventCount, Is.EqualTo(0));
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void ResolveInputReturnsMatchWhenEventIsSuitable()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            RhythmInputResolveResult result = session.ResolveInput(RhythmAction.Guard, 1d);

            Assert.That(result.HasMatch, Is.True);
            Assert.That(result.MatchedEvent, Is.SameAs(session.Events[0]));
        }

        [Test]
        public void ResolveInputMarksMatchedEventHit()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            RhythmInputResolveResult result = session.ResolveInput(RhythmAction.Guard, 1d);

            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(session.Events[0].Result, Is.SameAs(result.HitResult));
        }

        [Test]
        public void ResolveInputWrongActionReturnsNoMatch()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Strike) });

            RhythmInputResolveResult result = session.ResolveInput(RhythmAction.Guard, 1d);

            Assert.That(result.HasMatch, Is.False);
            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(session.Events[0].Result, Is.Null);
        }

        [Test]
        public void ResolveInputOnlyAffectsSessionEvents()
        {
            RhythmSession session = CreateSession(new[] { CreateData("session-event", 1d, RhythmAction.Guard) });
            BeatEventRuntime externalRuntime = new BeatEventRuntime(CreateData("external-event", 1d, RhythmAction.Guard));

            session.ResolveInput(RhythmAction.Guard, 1d);

            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(externalRuntime.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(externalRuntime.Result, Is.Null);
        }

        [Test]
        public void MarkTimedOutEventsMarksExpiredEventsMissed()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            session.MarkTimedOutEvents(1d + Windows.GoodWindowSeconds + 0.001d);

            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Missed));
            Assert.That(session.Events[0].Result.Grade, Is.EqualTo(HitGrade.Miss));
        }

        [Test]
        public void MarkTimedOutEventsLeavesUnexpiredEventsPending()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            IReadOnlyList<HitResult> results = session.MarkTimedOutEvents(1d + Windows.GoodWindowSeconds);

            Assert.That(results, Is.Empty);
            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(session.Events[0].Result, Is.Null);
        }

        [Test]
        public void MarkTimedOutEventsReturnsCorrectHitResults()
        {
            RhythmSession session = CreateSession(new[]
            {
                CreateData("first", 1d, RhythmAction.Guard),
                CreateData("second", 2d, RhythmAction.Strike)
            });

            IReadOnlyList<HitResult> results = session.MarkTimedOutEvents(2.5d);

            Assert.That(results.Select(result => result.EventId).ToArray(), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(results.All(result => result.Grade == HitGrade.Miss), Is.True);
            Assert.That(results[0], Is.SameAs(session.Events[0].Result));
            Assert.That(results[1], Is.SameAs(session.Events[1].Result));
        }

        [Test]
        public void ResolvedEventCountUpdatesAfterInputAndTimeout()
        {
            RhythmSession session = CreateSession(new[]
            {
                CreateData("hit", 1d, RhythmAction.Guard),
                CreateData("missed", 2d, RhythmAction.Guard),
                CreateData("pending", 10d, RhythmAction.Guard)
            });

            session.ResolveInput(RhythmAction.Guard, 1d);
            Assert.That(session.ResolvedEventCount, Is.EqualTo(1));

            session.MarkTimedOutEvents(2d + Windows.GoodWindowSeconds + 0.001d);
            Assert.That(session.ResolvedEventCount, Is.EqualTo(2));
            Assert.That(session.IsComplete, Is.False);
        }

        [Test]
        public void IsCompleteIsTrueWhenAllEventsAreHitOrMissed()
        {
            RhythmSession session = CreateSession(new[]
            {
                CreateData("hit", 1d, RhythmAction.Guard),
                CreateData("missed", 2d, RhythmAction.Guard)
            });

            session.ResolveInput(RhythmAction.Guard, 1d);
            session.MarkTimedOutEvents(2d + Windows.GoodWindowSeconds + 0.001d);

            Assert.That(session.ResolvedEventCount, Is.EqualTo(2));
            Assert.That(session.IsComplete, Is.True);
        }

        [Test]
        public void ResolveInputUsesResolverBehaviorForDeterministicSelection()
        {
            RhythmSession session = CreateSession(new[]
            {
                CreateData("beat-b", 1d, RhythmAction.Guard),
                CreateData("beat-a", 1d, RhythmAction.Guard)
            });

            RhythmInputResolveResult result = session.ResolveInput(RhythmAction.Guard, 1d);

            Assert.That(result.MatchedEvent.Data.EventId, Is.EqualTo("beat-a"));
            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(session.Events[1].State, Is.EqualTo(BeatEventState.Hit));
        }

        [Test]
        public void ResolveInputUsesResolverTimingBehavior()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            RhythmInputResolveResult result = session.ResolveInput(RhythmAction.Guard, 1.08d);

            Assert.That(result.HitResult.Grade, Is.EqualTo(HitGrade.Good));
            Assert.That(result.HitResult.TimingErrorSeconds, Is.EqualTo(0.08d).Within(0.0000001d));
        }

        [Test]
        public void MarkTimedOutEventsUsesTimeoutProcessorBoundaryBehavior()
        {
            RhythmSession session = CreateSession(new[] { CreateData("beat-001", 1d, RhythmAction.Guard) });

            IReadOnlyList<HitResult> atBoundary = session.MarkTimedOutEvents(1d + Windows.GoodWindowSeconds);
            IReadOnlyList<HitResult> afterBoundary = session.MarkTimedOutEvents(1d + Windows.GoodWindowSeconds + 0.001d);

            Assert.That(atBoundary, Is.Empty);
            Assert.That(afterBoundary, Has.Count.EqualTo(1));
            Assert.That(session.Events[0].State, Is.EqualTo(BeatEventState.Missed));
        }

        [Test]
        public void RhythmSessionDoesNotReferenceMatcherOrJudgeDirectly()
        {
            Type[] forbiddenTypes = { typeof(BeatEventMatcher), typeof(HitJudge) };
            Type[] referencedTypes = GetReferencedTypes(typeof(RhythmSession)).ToArray();

            Assert.That(referencedTypes, Has.None.Matches<Type>(type => forbiddenTypes.Contains(type)));
        }

        [Test]
        public void DomainAssemblyDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(RhythmSession).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static RhythmSession CreateSession(IReadOnlyList<BeatEventData> eventData)
        {
            return new RhythmSession(eventData, Windows, CreateInputResolver(), CreateTimeoutProcessor());
        }

        private static RhythmInputResolver CreateInputResolver()
        {
            return new RhythmInputResolver(new BeatEventMatcher(), new HitJudge());
        }

        private static BeatEventTimeoutProcessor CreateTimeoutProcessor()
        {
            return new BeatEventTimeoutProcessor(new HitJudge());
        }

        private static BeatEventData CreateData(string eventId, double targetTimeSeconds, RhythmAction action)
        {
            return new BeatEventData(eventId, targetTimeSeconds, action, 0.5f);
        }

        private static IEnumerable<Type> GetReferencedTypes(Type type)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                byte[] ilBytes = method.GetMethodBody() == null ? Array.Empty<byte>() : method.GetMethodBody().GetILAsByteArray();
                foreach (Type referencedType in GetReferencedTypes(method.Module, ilBytes))
                {
                    yield return referencedType;
                }
            }
        }

        private static IEnumerable<Type> GetReferencedTypes(Module module, byte[] ilBytes)
        {
            for (int i = 0; i <= ilBytes.Length - 4; i++)
            {
                int metadataToken = BitConverter.ToInt32(ilBytes, i);
                Type referencedType = TryResolveType(module, metadataToken);

                if (referencedType != null)
                {
                    yield return referencedType;
                }
            }
        }

        private static Type TryResolveType(Module module, int metadataToken)
        {
            try
            {
                MemberInfo member = module.ResolveMember(metadataToken);
                if (member is Type type)
                {
                    return type;
                }

                if (member is MethodBase method)
                {
                    return method.DeclaringType;
                }

                if (member is FieldInfo field)
                {
                    return field.DeclaringType;
                }
            }
            catch (ArgumentException)
            {
            }

            return null;
        }
    }
}
