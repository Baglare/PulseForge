using System;
using System.Linq;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class ScoreTrackerTests
    {
        [Test]
        public void NewScoreTrackerStartsWithZeroValues()
        {
            var tracker = new ScoreTracker();

            Assert.That(tracker.TotalScore, Is.EqualTo(0));
            Assert.That(tracker.CurrentCombo, Is.EqualTo(0));
            Assert.That(tracker.MaxCombo, Is.EqualTo(0));
            Assert.That(tracker.PerfectCount, Is.EqualTo(0));
            Assert.That(tracker.GoodCount, Is.EqualTo(0));
            Assert.That(tracker.MissCount, Is.EqualTo(0));
            Assert.That(tracker.TotalJudgedCount, Is.EqualTo(0));
        }

        [Test]
        public void RecordRejectsNullResult()
        {
            var tracker = new ScoreTracker();

            Assert.That(() => tracker.Record(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void PerfectIncreasesScoreCountAndCombo()
        {
            var tracker = new ScoreTracker();

            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));

            Assert.That(tracker.TotalScore, Is.EqualTo(1000));
            Assert.That(tracker.PerfectCount, Is.EqualTo(1));
            Assert.That(tracker.CurrentCombo, Is.EqualTo(1));
            Assert.That(tracker.MaxCombo, Is.EqualTo(1));
        }

        [Test]
        public void GoodIncreasesScoreCountAndCombo()
        {
            var tracker = new ScoreTracker();

            tracker.Record(CreateResult("beat-001", HitGrade.Good));

            Assert.That(tracker.TotalScore, Is.EqualTo(500));
            Assert.That(tracker.GoodCount, Is.EqualTo(1));
            Assert.That(tracker.CurrentCombo, Is.EqualTo(1));
            Assert.That(tracker.MaxCombo, Is.EqualTo(1));
        }

        [Test]
        public void MissIncreasesCountResetsComboAndDoesNotAddScore()
        {
            var tracker = new ScoreTracker();
            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));

            tracker.Record(CreateResult("beat-002", HitGrade.Miss));

            Assert.That(tracker.TotalScore, Is.EqualTo(1000));
            Assert.That(tracker.MissCount, Is.EqualTo(1));
            Assert.That(tracker.CurrentCombo, Is.EqualTo(0));
            Assert.That(tracker.MaxCombo, Is.EqualTo(1));
        }

        [Test]
        public void MaxComboTracksBestCombo()
        {
            var tracker = new ScoreTracker();

            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));
            tracker.Record(CreateResult("beat-002", HitGrade.Good));
            tracker.Record(CreateResult("beat-003", HitGrade.Miss));
            tracker.Record(CreateResult("beat-004", HitGrade.Good));

            Assert.That(tracker.CurrentCombo, Is.EqualTo(1));
            Assert.That(tracker.MaxCombo, Is.EqualTo(2));
        }

        [Test]
        public void TotalJudgedCountReturnsPerfectGoodAndMissTotal()
        {
            var tracker = new ScoreTracker();

            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));
            tracker.Record(CreateResult("beat-002", HitGrade.Good));
            tracker.Record(CreateResult("beat-003", HitGrade.Miss));

            Assert.That(tracker.TotalJudgedCount, Is.EqualTo(3));
        }

        [Test]
        public void SameEventIdCannotBeRecordedTwice()
        {
            var tracker = new ScoreTracker();
            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));

            Assert.That(
                () => tracker.Record(CreateResult("beat-001", HitGrade.Good)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ResetClearsAllValues()
        {
            var tracker = new ScoreTracker();
            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));
            tracker.Record(CreateResult("beat-002", HitGrade.Good));
            tracker.Record(CreateResult("beat-003", HitGrade.Miss));

            tracker.Reset();

            Assert.That(tracker.TotalScore, Is.EqualTo(0));
            Assert.That(tracker.CurrentCombo, Is.EqualTo(0));
            Assert.That(tracker.MaxCombo, Is.EqualTo(0));
            Assert.That(tracker.PerfectCount, Is.EqualTo(0));
            Assert.That(tracker.GoodCount, Is.EqualTo(0));
            Assert.That(tracker.MissCount, Is.EqualTo(0));
            Assert.That(tracker.TotalJudgedCount, Is.EqualTo(0));
        }

        [Test]
        public void ResetAllowsSameEventIdToBeRecordedAgain()
        {
            var tracker = new ScoreTracker();
            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));

            tracker.Reset();
            tracker.Record(CreateResult("beat-001", HitGrade.Good));

            Assert.That(tracker.TotalScore, Is.EqualTo(500));
            Assert.That(tracker.GoodCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateSnapshotCarriesCurrentValues()
        {
            var tracker = new ScoreTracker();
            tracker.Record(CreateResult("beat-001", HitGrade.Perfect));
            tracker.Record(CreateResult("beat-002", HitGrade.Good));
            tracker.Record(CreateResult("beat-003", HitGrade.Miss));

            ScoreSnapshot snapshot = tracker.CreateSnapshot();

            Assert.That(snapshot.TotalScore, Is.EqualTo(1500));
            Assert.That(snapshot.CurrentCombo, Is.EqualTo(0));
            Assert.That(snapshot.MaxCombo, Is.EqualTo(2));
            Assert.That(snapshot.PerfectCount, Is.EqualTo(1));
            Assert.That(snapshot.GoodCount, Is.EqualTo(1));
            Assert.That(snapshot.MissCount, Is.EqualTo(1));
            Assert.That(snapshot.TotalJudgedCount, Is.EqualTo(3));
        }

        [Test]
        public void DomainAssemblyDoesNotReferenceUnityEngine()
        {
            string[] referencedAssemblyNames = typeof(ScoreTracker).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name)
                .ToArray();

            Assert.That(referencedAssemblyNames, Has.None.StartsWith("UnityEngine"));
        }

        private static HitResult CreateResult(string eventId, HitGrade grade)
        {
            return new HitResult(eventId, grade, 0d);
        }
    }
}
