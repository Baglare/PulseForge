using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialCombatPresentationTests
    {
        private const string RuntimeAssembly = "Assembly-CSharp";

        [Test]
        public void EightDirectionsMapToDistinctNormalizedVectors()
        {
            MethodInfo directionVector = PresentationMathType().GetMethod("DirectionVector");
            HashSet<string> vectors = new HashSet<string>(StringComparer.Ordinal);
            Array directions = Enum.GetValues(typeof(RadialDirection));

            foreach (RadialDirection direction in directions)
            {
                Vector2 vector = (Vector2)directionVector.Invoke(null, new object[] { direction });
                Assert.That(Math.Abs(vector.magnitude - 1f), Is.LessThan(0.0001f));
                vectors.Add(vector.x.ToString("0.000") + ":" + vector.y.ToString("0.000"));
            }

            Assert.That(vectors, Has.Count.EqualTo(8));
        }

        [Test]
        public void ApproachPositionMatchesRevealAndTargetRadii()
        {
            MethodInfo evaluate = PresentationMathType().GetMethod("EvaluateApproachPosition");
            Vector2 atReveal = (Vector2)evaluate.Invoke(
                null,
                new object[] { RadialDirection.North, 4d, 4d, 5d, 360f, 155f });
            Vector2 atTarget = (Vector2)evaluate.Invoke(
                null,
                new object[] { RadialDirection.North, 5d, 4d, 5d, 360f, 155f });

            Assert.That(Math.Abs(atReveal.y - 360f), Is.LessThan(0.001f));
            Assert.That(Math.Abs(atTarget.y - 155f), Is.LessThan(0.001f));
            Assert.That(Math.Abs(atReveal.x), Is.LessThan(0.001f));
        }

        [Test]
        public void SamePausedSongTimeProducesSamePositionAndResetClearsRegistry()
        {
            MethodInfo evaluate = PresentationMathType().GetMethod("EvaluateApproachPosition");
            object[] arguments = { RadialDirection.SouthWest, 2.4d, 2d, 3d, 360f, 155f };
            Vector2 first = (Vector2)evaluate.Invoke(null, arguments);
            Vector2 paused = (Vector2)evaluate.Invoke(null, arguments);
            Assert.That(first, Is.EqualTo(paused));

            object registry = Activator.CreateInstance(RuntimeType("RadialPresentationPoolRegistry"));
            object key = CreateKey("event", "target", "requirement");
            MethodInfo activate = registry.GetType().GetMethod("TryActivate");
            activate.Invoke(registry, new[] { key });
            registry.GetType().GetMethod("Clear").Invoke(registry, null);

            Assert.That(GetProperty<int>(registry, "Count"), Is.EqualTo(0));
            Assert.That((bool)activate.Invoke(registry, new[] { key }), Is.True);
        }

        [Test]
        public void RangedTimelineOrdersRevealSpawnFireAndTarget()
        {
            object timeline = PresentationMathType().GetMethod("CreateRangedTimeline")
                .Invoke(null, new object[] { 10d, 1.2d });
            double reveal = GetProperty<double>(timeline, "RevealTimeSeconds");
            double spawn = GetProperty<double>(timeline, "SpawnTimeSeconds");
            double fire = GetProperty<double>(timeline, "FireTimeSeconds");
            double target = GetProperty<double>(timeline, "TargetTimeSeconds");

            Assert.That(Math.Abs(reveal - 8.8d), Is.LessThan(0.0001d));
            Assert.That(spawn, Is.GreaterThan(reveal));
            Assert.That(fire, Is.GreaterThanOrEqualTo(spawn));
            Assert.That(target, Is.GreaterThan(fire));

            MethodInfo projectile = PresentationMathType().GetMethod("EvaluateProjectilePosition");
            Vector2 atFire = (Vector2)projectile.Invoke(
                null,
                new object[] { RadialDirection.East, fire, fire, target, 326f, 155f });
            Vector2 atTarget = (Vector2)projectile.Invoke(
                null,
                new object[] { RadialDirection.East, target, fire, target, 326f, 155f });
            Assert.That(Math.Abs(atFire.x - 326f), Is.LessThan(0.001f));
            Assert.That(Math.Abs(atTarget.x - 155f), Is.LessThan(0.001f));
        }

        [Test]
        public void PoolRegistryRejectsDuplicatePresentationKey()
        {
            object registry = Activator.CreateInstance(RuntimeType("RadialPresentationPoolRegistry"));
            object key = CreateKey("sweep", "target-a", "shared-requirement");
            MethodInfo activate = registry.GetType().GetMethod("TryActivate");

            Assert.That((bool)activate.Invoke(registry, new[] { key }), Is.True);
            Assert.That((bool)activate.Invoke(registry, new[] { key }), Is.False);
            Assert.That(GetProperty<int>(registry, "Count"), Is.EqualTo(1));
        }

        [Test]
        public void FogChangesRevealOnlyAndKeepsPreviouslyVisibleTarget()
        {
            RadialStatusEffectSnapshot fog = new RadialStatusEffectSnapshot(
                true,
                4d,
                12d,
                0.55f,
                0.45d);
            MethodInfo revealTime = PresentationMathType().GetMethod("EvaluateRevealTime");
            double fogReveal = (double)revealTime.Invoke(null, new object[] { 10d, 1d, fog });
            MethodInfo shouldBeVisible = PresentationMathType().GetMethod("ShouldBeVisible");

            Assert.That(fogReveal, Is.EqualTo(9.45d).Within(0.0001d));
            Assert.That((bool)shouldBeVisible.Invoke(
                null,
                new object[] { true, 9.2d, fogReveal }), Is.True);

            Vector2 atTarget = (Vector2)PresentationMathType()
                .GetMethod("EvaluateApproachPosition")
                .Invoke(null, new object[]
                {
                    RadialDirection.North,
                    10d,
                    9d,
                    10d,
                    360f,
                    155f
                });
            Assert.That(atTarget.y, Is.EqualTo(155f).Within(0.001f));
        }

        private static Type PresentationMathType()
        {
            return RuntimeType("RadialPresentationMath");
        }

        private static object CreateKey(string eventId, string targetId, string requirementId)
        {
            return Activator.CreateInstance(
                RuntimeType("RadialPresentationKey"),
                eventId,
                targetId,
                requirementId);
        }

        private static Type RuntimeType(string name)
        {
            return Type.GetType(
                "PulseForge.Runtime.Unity.UI." + name + ", " + RuntimeAssembly,
                true);
        }

        private static T GetProperty<T>(object instance, string name)
        {
            return (T)instance.GetType().GetProperty(name).GetValue(instance);
        }
    }
}
