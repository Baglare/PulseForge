using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class M9HCalibrationTrainingTests
    {
        [Test]
        public void CalibrationUsesMedianMadAndRejectsOutlier()
        {
            List<double> targets = new List<double>();
            List<double> raw = new List<double>();
            for (int i = 0; i < 12; i++)
            {
                targets.Add(i + 1d);
                raw.Add(i + 1.03d);
            }
            raw[11] = targets[11] + 0.30d;

            bool analyzed = RadialInputCalibration.TryAnalyze(raw, targets, 0d, out RadialInputCalibrationResult result);

            Assert.That(analyzed, Is.True);
            Assert.That(result.ValidSampleCount, Is.EqualTo(11));
            Assert.That(result.MedianDeviationMilliseconds, Is.EqualTo(30d).Within(0.001d));
            Assert.That(result.SuggestedInputOffsetSeconds, Is.EqualTo(-0.03d).Within(0.000001d));
        }

        [Test]
        public void CalibrationRequiresEightValidSamples()
        {
            double[] targets = { 1d, 2d, 3d, 4d, 5d, 6d, 7d };
            double[] raw = { 1d, 2d, 3d, 4d, 5d, 6d, 7d };

            Assert.That(
                RadialInputCalibration.TryAnalyze(raw, targets, 0d, out _),
                Is.False);
        }

        [Test]
        public void SuggestedOffsetUsesSharedEffectiveTimeSign()
        {
            List<double> targets = new List<double>();
            List<double> raw = new List<double>();
            for (int i = 0; i < 8; i++)
            {
                targets.Add(i + 1d);
                raw.Add(i + 1.025d);
            }

            RadialInputCalibration.TryAnalyze(raw, targets, 0.010d, out RadialInputCalibrationResult result);
            double sharedDelta = RadialTimingMath.EffectiveJudgementTimeSeconds(1.025d, 0.010d) - 1d;

            Assert.That(
                result.SuggestedInputOffsetSeconds,
                Is.EqualTo(RadialTimingMath.InputOffsetForObservedDelta(0.010d, sharedDelta)).Within(0.000001d));
        }

        [Test]
        public void CalibrationCancelRestoresBothOffsets()
        {
            CalibrationOffsetDraft draft = new CalibrationOffsetDraft(0.020d, -0.015d);
            draft.StageBeatMapOffset(0.080d);
            draft.StageInputOffset(0.045d);

            draft.Cancel();

            Assert.That(draft.BeatMapOffsetSeconds, Is.EqualTo(0.020d));
            Assert.That(draft.InputOffsetSeconds, Is.EqualTo(-0.015d));
            Assert.That(draft.IsCommitted, Is.False);
        }

        [Test]
        public void FirstRunAndExistingUserMigrationAreSeparated()
        {
            Type defaultsType = RuntimeType("SaveDefaults");
            object clean = defaultsType.GetMethod("CreateSettings", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            Assert.That(GetField<bool>(clean, "firstTimeSetupCompleted"), Is.False);

            Type settingsType = RuntimeType("PulseForgeSettingsData");
            object existing = Activator.CreateInstance(settingsType);
            SetField(existing, "schemaVersion", 7);
            SetField(existing, "uiLanguage", "Turkish");
            object migrated = Normalize("NormalizeSettings", existing);

            Assert.That(GetField<bool>(migrated, "firstTimeSetupCompleted"), Is.True);
            Assert.That(GetField<string>(migrated, "language"), Is.EqualTo("Turkish"));
            Assert.That(GetField<int>(migrated, "schemaVersion"), Is.EqualTo(8));
        }

        [Test]
        public void TutorialProgressIsListBasedAndCompletesAfterTwoSuccesses()
        {
            Type defaultsType = RuntimeType("SaveDefaults");
            object profile = defaultsType.GetMethod("CreateProfile", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            Type utilityType = RuntimeType("TutorialLessonProgressUtility");
            MethodInfo record = utilityType.GetMethod("RecordSuccess", BindingFlags.Public | BindingFlags.Static);

            object first = record.Invoke(null, new[] { profile, "GuardTap", "2026-07-17T10:00:00.0000000Z" });
            object second = record.Invoke(null, new[] { profile, "GuardTap", "2026-07-17T10:01:00.0000000Z" });
            IList lessons = (IList)GetField<object>(profile, "tutorialLessons");

            Assert.That(lessons, Has.Count.EqualTo(1));
            Assert.That(GetField<bool>(first, "completed"), Is.True);
            Assert.That(GetField<bool>(second, "completed"), Is.True);
            Assert.That(GetField<int>(second, "successfulAttempts"), Is.EqualTo(2));
        }

        [Test]
        public void LessonProgressTracksFailureRetryAndTwoSuccesses()
        {
            TrainingAttemptProgress progress = new TrainingAttemptProgress();
            progress.RecordFailure();
            Assert.That(progress.RetryPending, Is.True);
            progress.BeginRetry();
            progress.RecordSuccess();
            progress.RecordSuccess();

            Assert.That(progress.RetryPending, Is.False);
            Assert.That(progress.SuccessfulAttempts, Is.EqualTo(2));
            Assert.That(progress.IsComplete, Is.True);
        }

        [Test]
        public void TrainingFixturesUsePracticeSessionWithoutScorePersistence()
        {
            foreach (TrainingLessonDefinition lesson in RadialTrainingCatalog.All)
            {
                RadialBeatMapData fixture = RadialTrainingCatalog.CreateFixture(lesson.Id);
                Assert.That(fixture.encounters, Is.Not.Empty, lesson.Id.ToString());
                Assert.DoesNotThrow(() => new RadialRhythmSession(
                    fixture.encounters,
                    TimingAssistMode.Practice,
                    0d));
            }

            Assert.That(TrainingPersistencePolicy.RecordsProfileScore, Is.False);
            Assert.That(TrainingPersistencePolicy.RecordsSavedTrackPerformance, Is.False);
        }

        [Test]
        public void TrainingBindingDisplayUsesAssignedNames()
        {
            string display = TrainingBindingDisplay.Build(
                RhythmActionMask.Guard | RhythmActionMask.LightAttack,
                "Space (Custom)",
                "Mouse 1",
                "K",
                "L");

            Assert.That(display, Does.Contain("Space (Custom)"));
            Assert.That(display, Does.Contain("Mouse 1"));
            Assert.That(display, Does.Not.Contain("K"));
        }

        [Test]
        public void RestartFirstTimeSetupPreservesExistingSettings()
        {
            Type defaultsType = RuntimeType("SaveDefaults");
            object settings = defaultsType.GetMethod("CreateSettings", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            SetField(settings, "firstTimeSetupCompleted", true);
            SetField(settings, "beatmapOffsetSeconds", 0.075f);
            SetField(settings, "inputTimingOffsetSeconds", -0.030f);
            Type restartType = RuntimeType("FirstTimeSetupSettings");

            object restarted = restartType.GetMethod("Restart", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { settings });

            Assert.That(GetField<bool>(restarted, "firstTimeSetupCompleted"), Is.False);
            Assert.That(GetField<float>(restarted, "beatmapOffsetSeconds"), Is.EqualTo(0.075f));
            Assert.That(GetField<float>(restarted, "inputTimingOffsetSeconds"), Is.EqualTo(-0.030f));
        }

        private static object Normalize(string methodName, object value)
        {
            return RuntimeType("SaveDataNormalizer")
                .GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { value });
        }

        private static Type RuntimeType(string name)
        {
            Type type = Type.GetType(
                "PulseForge.Runtime.Unity.Persistence." + name + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, name + " runtime type was not found.");
            return type;
        }

        private static T GetField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)
                .GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
