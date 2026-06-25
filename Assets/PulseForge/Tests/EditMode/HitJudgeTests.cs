using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class HitJudgeTests
    {
        private const double TargetTime = 10d;
        private const double PerfectWindow = 0.05d;
        private const double GoodWindow = 0.12d;
        private const double Tolerance = 0.0000001d;

        private static readonly BeatEventData BeatEvent = new BeatEventData("beat-001", TargetTime, RhythmAction.Guard, 0.75f);
        private static readonly JudgementWindows Windows = new JudgementWindows(PerfectWindow, GoodWindow);

        [Test]
        public void ExactTargetTimeIsPerfect()
        {
            HitResult result = Judge(TargetTime);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Perfect));
            Assert.That(result.TimingErrorSeconds, Is.EqualTo(0d).Within(Tolerance));
        }

        [Test]
        public void EarlyInputInsidePerfectIsPerfect()
        {
            HitResult result = Judge(TargetTime - 0.025d);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void LateInputInsidePerfectIsPerfect()
        {
            HitResult result = Judge(TargetTime + 0.025d);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void ExactPositivePerfectBoundaryIsPerfect()
        {
            HitResult result = Judge(TargetTime + PerfectWindow);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void ExactNegativePerfectBoundaryIsPerfect()
        {
            HitResult result = Judge(TargetTime - PerfectWindow);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Perfect));
        }

        [Test]
        public void JustOutsidePerfectButInsideGoodIsGood()
        {
            HitResult result = Judge(TargetTime + PerfectWindow + 0.001d);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Good));
        }

        [Test]
        public void ExactPositiveGoodBoundaryIsGood()
        {
            HitResult result = Judge(TargetTime + GoodWindow);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Good));
        }

        [Test]
        public void ExactNegativeGoodBoundaryIsGood()
        {
            HitResult result = Judge(TargetTime - GoodWindow);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Good));
        }

        [Test]
        public void OutsideGoodIsMiss()
        {
            HitResult result = Judge(TargetTime + GoodWindow + 0.001d);

            Assert.That(result.Grade, Is.EqualTo(HitGrade.Miss));
        }

        [Test]
        public void TimingErrorPreservesEarlyAndLateSign()
        {
            HitResult early = Judge(TargetTime - 0.03d);
            HitResult late = Judge(TargetTime + 0.04d);

            Assert.That(early.TimingErrorSeconds, Is.EqualTo(-0.03d).Within(Tolerance));
            Assert.That(late.TimingErrorSeconds, Is.EqualTo(0.04d).Within(Tolerance));
        }

        [Test]
        public void InvalidBeatEventDataConstructorValuesAreRejected()
        {
            Assert.That(() => new BeatEventData(null, TargetTime, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData(string.Empty, TargetTime, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("   ", TargetTime, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", double.NaN, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", double.PositiveInfinity, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", double.NegativeInfinity, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", -0.001d, RhythmAction.Guard, 0.5f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", TargetTime, RhythmAction.Guard, float.NaN), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", TargetTime, RhythmAction.Guard, float.PositiveInfinity), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", TargetTime, RhythmAction.Guard, float.NegativeInfinity), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", TargetTime, RhythmAction.Guard, -0.001f), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new BeatEventData("beat", TargetTime, RhythmAction.Guard, 1.001f), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void InvalidJudgementWindowsConstructorValuesAreRejected()
        {
            Assert.That(() => new JudgementWindows(double.NaN, GoodWindow), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(double.PositiveInfinity, GoodWindow), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(double.NegativeInfinity, GoodWindow), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(0d, GoodWindow), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(-0.001d, GoodWindow), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(PerfectWindow, double.NaN), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(PerfectWindow, double.PositiveInfinity), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(PerfectWindow, double.NegativeInfinity), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(PerfectWindow, 0d), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(PerfectWindow, -0.001d), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => new JudgementWindows(0.2d, 0.1d), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void InvalidInputTimeIsRejected()
        {
            var judge = new HitJudge();

            Assert.That(() => judge.Judge(double.NaN, BeatEvent, Windows), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => judge.Judge(double.PositiveInfinity, BeatEvent, Windows), Throws.InstanceOf<System.ArgumentException>());
            Assert.That(() => judge.Judge(double.NegativeInfinity, BeatEvent, Windows), Throws.InstanceOf<System.ArgumentException>());
        }

        [Test]
        public void NullJudgeArgumentsAreRejected()
        {
            var judge = new HitJudge();

            Assert.That(() => judge.Judge(TargetTime, null, Windows), Throws.InstanceOf<System.ArgumentNullException>());
            Assert.That(() => judge.Judge(TargetTime, BeatEvent, null), Throws.InstanceOf<System.ArgumentNullException>());
        }

        private static HitResult Judge(double inputTimeSeconds)
        {
            return new HitJudge().Judge(inputTimeSeconds, BeatEvent, Windows);
        }
    }
}
