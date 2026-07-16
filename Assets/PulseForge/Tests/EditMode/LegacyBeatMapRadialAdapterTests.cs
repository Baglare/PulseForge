using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PulseForge.Domain.Rhythm;

namespace PulseForge.Tests.EditMode
{
    public sealed class LegacyBeatMapRadialAdapterTests
    {
        [Test]
        public void RhythmActionNumericValuesPreserveLegacyStrikeValue()
        {
            Assert.That((int)RhythmAction.Guard, Is.EqualTo(0));
            Assert.That((int)RhythmAction.LightAttack, Is.EqualTo(1));
            Assert.That((int)RhythmAction.Strike, Is.EqualTo((int)RhythmAction.LightAttack));
            Assert.That((int)RhythmAction.Dodge, Is.EqualTo(2));
            Assert.That((int)RhythmAction.HeavyAttack, Is.EqualTo(3));
        }

        [Test]
        public void LegacyStrikeBecomesLightAttackTapWithoutChangingSourceEvent()
        {
            BeatEventData source = new BeatEventData("legacy-strike", 2.5d, RhythmAction.Strike, 0.8f);

            RadialBeatMapData result = LegacyBeatMapRadialAdapter.Convert(new[] { source });

            Assert.That(result.encounters, Has.Count.EqualTo(1));
            Assert.That(result.encounters[0].eventType, Is.EqualTo(RadialEventType.Tap));
            Assert.That(
                result.encounters[0].requirements[0].acceptedActions,
                Is.EqualTo(RhythmActionMask.LightAttack));
            Assert.That(result.encounters[0].requirements[0].targetTimeSeconds, Is.EqualTo(2.5d));
            Assert.That(source.Action, Is.EqualTo(RhythmAction.LightAttack));
        }

        [Test]
        public void LegacyGuardBecomesGuardTap()
        {
            BeatEventData source = new BeatEventData("legacy-guard", 1d, RhythmAction.Guard, 1f);

            RadialBeatMapData result = LegacyBeatMapRadialAdapter.Convert(new[] { source });

            Assert.That(result.encounters[0].eventType, Is.EqualTo(RadialEventType.Tap));
            Assert.That(
                result.encounters[0].requirements[0].acceptedActions,
                Is.EqualTo(RhythmActionMask.Guard));
        }

        [Test]
        public void LegacyBeatMapCanPrepareARadialSession()
        {
            RadialBeatMapData beatMap = LegacyBeatMapRadialAdapter.Convert(new[]
            {
                new BeatEventData("guard", 1d, RhythmAction.Guard, 1f),
                new BeatEventData("strike", 2d, RhythmAction.Strike, 1f)
            });

            RadialRhythmSession session = new RadialRhythmSession(beatMap.encounters);

            Assert.That(session.TotalEncounterCount, Is.EqualTo(2));
            Assert.That(session.Press(RhythmAction.Guard, 1d, 1).Consumed, Is.True);
            Assert.That(session.Press(RhythmAction.LightAttack, 2d, 2).Consumed, Is.True);
            Assert.That(session.IsComplete, Is.True);
        }

        [Test]
        public void LegacyJsonStrikeNameParsesAsLightAttack()
        {
            Type parserType = Type.GetType(
                "PulseForge.Runtime.Unity.BeatMaps.DebugBeatMapJsonParser, Assembly-CSharp",
                true);
            MethodInfo buildBeatEvents = parserType.GetMethod(
                "BuildBeatEvents",
                BindingFlags.Public | BindingFlags.Static);
            const string json =
                "{\"schemaVersion\":1,\"events\":[{\"eventId\":\"legacy\","
                + "\"targetTimeSeconds\":1.0,\"action\":\"Strike\",\"intensity\":1.0}]}";

            IReadOnlyList<BeatEventData> events =
                (IReadOnlyList<BeatEventData>)buildBeatEvents.Invoke(null, new object[] { json });

            Assert.That(events[0].Action, Is.EqualTo(RhythmAction.LightAttack));
        }
    }
}
