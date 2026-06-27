using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class BeatEventMatcherTests
    {
        private static readonly JudgementWindows Windows = new JudgementWindows(0.05d, 0.12d);

        [Test]
        public void TryFindBestMatchRejectsNullEvents()
        {
            var matcher = new BeatEventMatcher();

            Assert.That(
                () => matcher.TryFindBestMatch(null, RhythmAction.Guard, 1d, Windows, out _),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void TryFindBestMatchRejectsNullWindows()
        {
            var matcher = new BeatEventMatcher();
            var events = new[] { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            Assert.That(
                () => matcher.TryFindBestMatch(events, RhythmAction.Guard, 1d, null, out _),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void TryFindBestMatchRejectsNullEventElement()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime[] events = { CreateRuntime("beat-001", 1d, RhythmAction.Guard), null };

            Assert.That(
                () => matcher.TryFindBestMatch(events, RhythmAction.Guard, 1d, Windows, out _),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TryFindBestMatchRejectsInvalidInputTime()
        {
            var matcher = new BeatEventMatcher();
            var events = new[] { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            Assert.That(
                () => matcher.TryFindBestMatch(events, RhythmAction.Guard, double.NaN, Windows, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => matcher.TryFindBestMatch(events, RhythmAction.Guard, double.PositiveInfinity, Windows, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => matcher.TryFindBestMatch(events, RhythmAction.Guard, double.NegativeInfinity, Windows, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void EmptyEventListReturnsFalse()
        {
            var matcher = new BeatEventMatcher();

            bool found = matcher.TryFindBestMatch(Array.Empty<BeatEventRuntime>(), RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }

        [Test]
        public void EventOutsideGoodWindowIsNotSelected()
        {
            var matcher = new BeatEventMatcher();
            var events = new[] { CreateRuntime("beat-001", 1d, RhythmAction.Guard) };

            bool found = matcher.TryFindBestMatch(events, RhythmAction.Guard, 1d + Windows.GoodWindowSeconds + 0.001d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }

        [Test]
        public void PendingActionMatchInsideGoodWindowIsSelected()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime expected = CreateRuntime("beat-001", 1d, RhythmAction.Guard);

            bool found = matcher.TryFindBestMatch(new[] { expected }, RhythmAction.Guard, 1.02d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.True);
            Assert.That(match, Is.SameAs(expected));
        }

        [Test]
        public void HitEventIsNotSelected()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime hit = CreateRuntime("beat-001", 1d, RhythmAction.Guard);
            hit.ApplyResult(new HitResult("beat-001", HitGrade.Perfect, 0d));

            bool found = matcher.TryFindBestMatch(new[] { hit }, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }

        [Test]
        public void MissedEventIsNotSelected()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime missed = CreateRuntime("beat-001", 1d, RhythmAction.Guard);
            missed.ApplyResult(new HitResult("beat-001", HitGrade.Miss, 0.2d));

            bool found = matcher.TryFindBestMatch(new[] { missed }, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }

        [Test]
        public void DifferentActionIsNotSelected()
        {
            var matcher = new BeatEventMatcher();
            var events = new[] { CreateRuntime("beat-001", 1d, RhythmAction.Strike) };

            bool found = matcher.TryFindBestMatch(events, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.False);
            Assert.That(match, Is.Null);
        }

        [Test]
        public void ClosestCandidateIsSelected()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime farther = CreateRuntime("farther", 0.94d, RhythmAction.Guard);
            BeatEventRuntime closer = CreateRuntime("closer", 1.03d, RhythmAction.Guard);

            bool found = matcher.TryFindBestMatch(new[] { farther, closer }, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.True);
            Assert.That(match, Is.SameAs(closer));
        }

        [Test]
        public void EqualDistanceChoosesEarlierTargetTime()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime earlier = CreateRuntime("earlier", 0.95d, RhythmAction.Guard);
            BeatEventRuntime later = CreateRuntime("later", 1.05d, RhythmAction.Guard);

            bool found = matcher.TryFindBestMatch(new[] { later, earlier }, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.True);
            Assert.That(match, Is.SameAs(earlier));
        }

        [Test]
        public void SameDistanceAndTargetTimeChoosesOrdinalSmallerEventId()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime ordinalLater = CreateRuntime("beat-b", 1d, RhythmAction.Guard);
            BeatEventRuntime ordinalEarlier = CreateRuntime("beat-a", 1d, RhythmAction.Guard);

            bool found = matcher.TryFindBestMatch(new[] { ordinalLater, ordinalEarlier }, RhythmAction.Guard, 1d, Windows, out BeatEventRuntime match);

            Assert.That(found, Is.True);
            Assert.That(match, Is.SameAs(ordinalEarlier));
        }

        [Test]
        public void TryFindBestMatchDoesNotChangeEventState()
        {
            var matcher = new BeatEventMatcher();
            BeatEventRuntime pending = CreateRuntime("pending", 1d, RhythmAction.Guard);
            BeatEventRuntime hit = CreateRuntime("hit", 1.01d, RhythmAction.Guard);
            hit.ApplyResult(new HitResult("hit", HitGrade.Perfect, 0d));

            matcher.TryFindBestMatch(new[] { pending, hit }, RhythmAction.Guard, 1d, Windows, out _);

            Assert.That(pending.State, Is.EqualTo(BeatEventState.Pending));
            Assert.That(pending.Result, Is.Null);
            Assert.That(hit.State, Is.EqualTo(BeatEventState.Hit));
            Assert.That(hit.Result, Is.Not.Null);
        }

        [Test]
        public void TryFindBestMatchDoesNotReferenceHitJudgeOrHitResult()
        {
            MethodInfo method = typeof(BeatEventMatcher).GetMethod(nameof(BeatEventMatcher.TryFindBestMatch));
            byte[] ilBytes = method.GetMethodBody().GetILAsByteArray();
            HashSet<Type> forbiddenTypes = new HashSet<Type> { typeof(HitJudge), typeof(HitResult) };

            IEnumerable<Type> referencedTypes = GetReferencedTypes(method.Module, ilBytes);

            Assert.That(referencedTypes, Has.None.Matches<Type>(type => forbiddenTypes.Contains(type)));
        }

        [Test]
        public void DomainAssemblyDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(BeatEventMatcher).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static BeatEventRuntime CreateRuntime(string eventId, double targetTimeSeconds, RhythmAction action)
        {
            return new BeatEventRuntime(new BeatEventData(eventId, targetTimeSeconds, action, 0.5f));
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
