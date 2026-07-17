using System;
using System.Reflection;
using NUnit.Framework;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialGameModePersistenceTests
    {
        [Test]
        public void LegacySettingsMigrateDefaultModeToStandard()
        {
            Type settingsType = RuntimeType("PulseForgeSettingsData");
            object settings = Activator.CreateInstance(settingsType);
            SetField(settings, "schemaVersion", 2);
            SetField(settings, "defaultDetection", "Onset");
            SetField(settings, "defaultDifficulty", "Normal");
            SetField(settings, "defaultCombatStyle", "Legacy");

            object normalized = InvokeNormalizer("NormalizeSettings", settings);

            Assert.That(GetField<string>(normalized, "defaultGameMode"), Is.EqualTo("Standard"));
            Assert.That(GetField<string>(normalized, "defaultCoverage"), Is.EqualTo("Standard"));
            Assert.That(GetField<float>(normalized, "forecastLeadMultiplier"), Is.EqualTo(1.25f));
            Assert.That(GetField<string>(normalized, "readabilityMode"), Is.EqualTo("Assisted"));
            Assert.That(GetField<string>(normalized, "defaultTimingAssist"), Is.EqualTo("Relaxed"));
            Assert.That(GetField<bool>(normalized, "showUpcomingInputs"), Is.True);
            Assert.That(GetField<bool>(normalized, "beatPulseEnabled"), Is.True);
            Assert.That(GetField<string>(normalized, "uiLanguage"), Is.EqualTo("English"));
            Assert.That(GetField<int>(normalized, "schemaVersion"), Is.EqualTo(8));
        }

        [Test]
        public void GameModeDoesNotChangePresetCacheKey()
        {
            Type normalizer = RuntimeType("SaveDataNormalizer");
            MethodInfo presetKey = normalizer.GetMethod(
                "PresetKey",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(int), typeof(string) },
                null);

            string standard = (string)presetKey.Invoke(
                null,
                new object[] { "Onset", "Normal", "Balanced", 2, "Standard" });
            string oneLife = (string)presetKey.Invoke(
                null,
                new object[] { "Onset", "Normal", "Balanced", 2, "OneLife" });

            Assert.That(oneLife, Is.EqualTo(standard));
        }

        [Test]
        public void PerformanceComparisonIsSeparatedByMode()
        {
            Type normalizer = RuntimeType("SaveDataNormalizer");
            MethodInfo compare = normalizer.GetMethod(
                "CanComparePerformance",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string),
                    typeof(string), typeof(string), typeof(string)
                },
                null);

            bool sameMode = (bool)compare.Invoke(
                null,
                new object[] { "Survival", "radial-v2", "abc", "Survival", "radial-v2", "abc" });
            bool differentMode = (bool)compare.Invoke(
                null,
                new object[] { "Standard", "radial-v2", "abc", "OneLife", "radial-v2", "abc" });

            Assert.That(sameMode, Is.True);
            Assert.That(differentMode, Is.False);
        }

        [Test]
        public void PerformanceComparisonIsSeparatedByTimingAssist()
        {
            MethodInfo compare = RuntimeType("SaveDataNormalizer").GetMethod(
                "CanComparePerformance",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(string),
                    typeof(string), typeof(string), typeof(string), typeof(string)
                },
                null);

            bool same = (bool)compare.Invoke(null, new object[]
            {
                "Standard", "Practice", "radial-v2", "abc",
                "Standard", "Practice", "radial-v2", "abc"
            });
            bool different = (bool)compare.Invoke(null, new object[]
            {
                "Standard", "Standard", "radial-v2", "abc",
                "Standard", "Practice", "radial-v2", "abc"
            });

            Assert.That(same, Is.True);
            Assert.That(different, Is.False);
        }

        private static object InvokeNormalizer(string methodName, object argument)
        {
            MethodInfo method = RuntimeType("SaveDataNormalizer").GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new[] { argument });
        }

        private static Type RuntimeType(string name)
        {
            return Type.GetType(
                "PulseForge.Runtime.Unity.Persistence." + name + ", Assembly-CSharp",
                true);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name).SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name).GetValue(target);
        }
    }
}
