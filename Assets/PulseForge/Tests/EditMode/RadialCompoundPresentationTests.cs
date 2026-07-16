using System;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialCompoundPresentationTests
    {
        private const string RuntimeAssembly = "Assembly-CSharp";

        [Test]
        public void HoldAndChargeProgressUseAuthoredTiming()
        {
            MethodInfo hold = CompoundMathType().GetMethod("EvaluateHoldProgress");
            MethodInfo charge = CompoundMathType().GetMethod("EvaluateChargeProgress");

            Assert.That((float)hold.Invoke(null, new object[] { 1.5d, 1d, 2d, true }), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That((float)hold.Invoke(null, new object[] { 1.5d, 1d, 2d, false }), Is.Zero);
            Assert.That((float)charge.Invoke(null, new object[] { 2.25d, 2d, 2.5d, true }), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ChordLinkStateTracksPartialCompleteAndFailure()
        {
            MethodInfo evaluate = CompoundMathType().GetMethod("EvaluateChordState");

            Assert.That(evaluate.Invoke(null, new object[] { false, false, false }).ToString(), Is.EqualTo("Pending"));
            Assert.That(evaluate.Invoke(null, new object[] { true, false, false }).ToString(), Is.EqualTo("Partial"));
            Assert.That(evaluate.Invoke(null, new object[] { true, true, false }).ToString(), Is.EqualTo("Complete"));
            Assert.That(evaluate.Invoke(null, new object[] { true, false, true }).ToString(), Is.EqualTo("Failed"));
        }

        [Test]
        public void ChoicePresentationReadsSelectedAction()
        {
            RadialEncounterEventData choice = Encounter("choice", RadialEventType.Choice);
            InputRequirementData requirement = Requirement(
                "choice-input",
                RhythmActionMask.Guard | RhythmActionMask.Dodge,
                1d,
                InputGestureType.Choice);
            choice.requirements.Add(requirement);
            RadialRhythmSession session = new RadialRhythmSession(new[] { choice });

            session.Press(RhythmAction.Dodge, 1d);
            object selected = CompoundMathType().GetMethod("GetSelectedChoiceAction")
                .Invoke(null, new object[] { session.Encounters[0].Requirements[0] });

            Assert.That((RhythmAction)selected, Is.EqualTo(RhythmAction.Dodge));
        }

        [Test]
        public void OrderedSequencePresentationAdvancesOnlyActiveStep()
        {
            RadialEncounterEventData sequence = Encounter("sequence", RadialEventType.OrderedSequence);
            sequence.requirements.Add(OrderedRequirement("step-0", RhythmActionMask.Guard, 1d, 0));
            sequence.requirements.Add(OrderedRequirement("step-1", RhythmActionMask.LightAttack, 1.3d, 1));
            sequence.requirements.Add(OrderedRequirement("step-2", RhythmActionMask.Dodge, 1.6d, 2));
            RadialRhythmSession session = new RadialRhythmSession(new[] { sequence });

            session.Press(RhythmAction.Guard, 1d);
            object active = CompoundMathType().GetMethod("FindActiveStepIndex")
                .Invoke(null, new object[] { session.Encounters[0].Requirements });

            Assert.That((int)active, Is.EqualTo(1));
            Assert.That(session.Encounters[0].Requirements[2].IsResolved, Is.False);
        }

        [Test]
        public void SwarmPresentationCountsRemainingTargets()
        {
            RadialEncounterEventData swarm = Encounter("swarm", RadialEventType.SwarmChain);
            for (int i = 0; i < 3; i++)
            {
                string id = "cue-" + i;
                swarm.requirements.Add(OrderedRequirement(
                    id,
                    RhythmActionMask.LightAttack,
                    1d + (i * 0.2d),
                    i));
                swarm.targets.Add(Target("target-" + i, id, (RadialDirection)i));
            }
            RadialRhythmSession session = new RadialRhythmSession(new[] { swarm });

            session.Press(RhythmAction.LightAttack, 1d);
            object remaining = CompoundMathType().GetMethod("CountRemainingTargets")
                .Invoke(null, new object[] { session.Encounters[0].Targets });

            Assert.That((int)remaining, Is.EqualTo(2));
        }

        [Test]
        public void BreakTargetPresentationReducesArmorSegments()
        {
            object remaining = CompoundMathType().GetMethod("RemainingBreakSegments")
                .Invoke(null, new object[] { 6, 2 });

            Assert.That((int)remaining, Is.EqualTo(4));
        }

        [Test]
        public void SweepSharesOneRequirementAcrossAllTargets()
        {
            RadialEncounterEventData sweep = Encounter("sweep", RadialEventType.Sweep);
            sweep.requirements.Add(Requirement(
                "shared",
                RhythmActionMask.LightAttack,
                1d,
                InputGestureType.Tap));
            sweep.targets.Add(Target("a", "shared", RadialDirection.North));
            sweep.targets.Add(Target("b", "shared", RadialDirection.East));
            sweep.targets.Add(Target("c", "shared", RadialDirection.South));
            RadialRhythmSession session = new RadialRhythmSession(new[] { sweep });

            RadialInputResolveResult result = session.Press(RhythmAction.LightAttack, 1d);
            object groupCount = CompoundMathType().GetMethod("CountDistinctRequirementGroups")
                .Invoke(null, new object[] { session.Encounters[0].Targets });

            Assert.That(result.RequirementResults, Has.Count.EqualTo(1));
            Assert.That(result.TargetResults, Has.Count.EqualTo(3));
            Assert.That((int)groupCount, Is.EqualTo(1));
        }

        [Test]
        public void RestartResetClearsCompoundVisualRegistry()
        {
            object registry = Activator.CreateInstance(RuntimeType("RadialPresentationPoolRegistry"));
            MethodInfo activate = registry.GetType().GetMethod("TryActivate");
            activate.Invoke(registry, new[] { CreateKey("one") });
            activate.Invoke(registry, new[] { CreateKey("two") });

            registry.GetType().GetMethod("Clear").Invoke(registry, null);

            Assert.That((int)registry.GetType().GetProperty("Count").GetValue(registry), Is.Zero);
        }

        private static RadialEncounterEventData Encounter(string id, RadialEventType type)
        {
            return new RadialEncounterEventData
            {
                eventId = id,
                eventType = type
            };
        }

        private static InputRequirementData OrderedRequirement(
            string id,
            RhythmActionMask action,
            double time,
            int order)
        {
            InputRequirementData requirement = Requirement(
                id,
                action,
                time,
                InputGestureType.SequenceStep);
            requirement.orderIndex = order;
            return requirement;
        }

        private static InputRequirementData Requirement(
            string id,
            RhythmActionMask actions,
            double time,
            InputGestureType gesture)
        {
            return new InputRequirementData
            {
                requirementId = id,
                acceptedActions = actions,
                gestureType = gesture,
                phase = RhythmInputPhase.Pressed,
                targetTimeSeconds = time,
                perfectWindowSeconds = RadialTimingDefaults.PerfectWindowSeconds,
                goodWindowSeconds = RadialTimingDefaults.GoodWindowSeconds,
                exclusive = true
            };
        }

        private static EncounterTargetData Target(
            string id,
            string requirementId,
            RadialDirection direction)
        {
            return new EncounterTargetData
            {
                targetId = id,
                requirementId = requirementId,
                direction = direction,
                archetype = EnemyArchetype.Swarm
            };
        }

        private static Type CompoundMathType()
        {
            return RuntimeType("RadialCompoundPresentationMath");
        }

        private static object CreateKey(string id)
        {
            return Activator.CreateInstance(
                RuntimeType("RadialPresentationKey"),
                id,
                "compound",
                "group");
        }

        private static Type RuntimeType(string name)
        {
            return Type.GetType(
                "PulseForge.Runtime.Unity.UI." + name + ", " + RuntimeAssembly,
                true);
        }
    }
}
