using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Tests.EditMode
{
    public sealed class RadialRuntimeCacheIntegrationTests
    {
        private string rootDirectory;
        private string sourceWavPath;

        [SetUp]
        public void SetUp()
        {
            rootDirectory = Path.Combine(
                Path.GetTempPath(),
                "PulseForge-RadialCacheTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootDirectory);
            sourceWavPath = Path.Combine(rootDirectory, "source.wav");
            File.WriteAllBytes(sourceWavPath, new byte[64]);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }

        [Test]
        public void RadialCacheWrapperSerializesAndDeserializes()
        {
            RadialBeatMapData beatMap = CreateBeatMap();
            object wrapper = CreateRuntimeObject("RadialBeatMapCacheData");
            SetField(wrapper, "beatMapCacheVersion", 3);
            SetField(wrapper, "analyzerVersion", 2);
            SetField(wrapper, "trackId", "track");
            SetField(wrapper, "presetId", "preset");
            SetField(wrapper, "beatMapFingerprint", RadialBeatMapFingerprint.Compute(beatMap));
            SetField(wrapper, "radialBeatMap", beatMap);
            SetField(wrapper, "analyzerQuality", new AnalyzerQualityReport { candidateCount = 1 });
            SetField(
                wrapper,
                "plannerQuality",
                new PlannerQualityReport { totalInputCost = 1, result = PlannerQualityResult.Pass });

            string json = JsonUtility.ToJson(wrapper);
            object restored = JsonUtility.FromJson(json, wrapper.GetType());

            Assert.That(GetField<int>(restored, "analyzerVersion"), Is.EqualTo(2));
            Assert.That(GetField<RadialBeatMapData>(restored, "radialBeatMap").encounters, Has.Count.EqualTo(1));
            Assert.That(GetField<PlannerQualityReport>(restored, "plannerQuality").totalInputCost, Is.EqualTo(1));
        }

        [Test]
        public void FingerprintIsDeterministicAndIgnoresDisplayMetadata()
        {
            RadialBeatMapData first = CreateBeatMap();
            RadialBeatMapData second = CreateBeatMap();
            second.displayName = "A different file name";

            Assert.That(
                RadialBeatMapFingerprint.Compute(first),
                Is.EqualTo(RadialBeatMapFingerprint.Compute(second)));
        }

        [Test]
        public void FingerprintIncludesFailureEffectMetadata()
        {
            RadialBeatMapData first = CreateBeatMap();
            RadialBeatMapData second = CreateBeatMap();
            first.encounters[0].failureEffect = new FailureEffectData
            {
                effectType = FailureEffectType.Fog,
                durationSeconds = 6d,
                revealLeadMultiplier = 0.55f,
                minimumVisibleLeadSeconds = 0.45d
            };
            second.encounters[0].failureEffect = new FailureEffectData
            {
                effectType = FailureEffectType.Fog,
                durationSeconds = 8d,
                revealLeadMultiplier = 0.55f,
                minimumVisibleLeadSeconds = 0.45d
            };

            Assert.That(
                RadialBeatMapFingerprint.Compute(first),
                Is.Not.EqualTo(RadialBeatMapFingerprint.Compute(second)));
        }

        [Test]
        public void EditorArtifactRoundTripUsesRuntimeFingerprint()
        {
            RadialBeatMapData beatMap = CreateBeatMap();
            Type serializer = RuntimeType("RadialBeatMapArtifactSerializer");
            object artifact = serializer.GetMethod("Create").Invoke(
                null,
                new object[]
                {
                    "editor-track",
                    "editor-preset",
                    beatMap,
                    new AnalyzerQualityReport(),
                    new PlannerQualityReport { totalInputCost = 1 },
                    null
                });
            string json = (string)serializer.GetMethod("Serialize").Invoke(
                null,
                new[] { artifact, (object)true });
            object[] arguments = { json, null, null };

            bool parsed = (bool)serializer.GetMethod("TryDeserialize").Invoke(null, arguments);
            object restored = arguments[1];

            Assert.That(parsed, Is.True, arguments[2] as string);
            Assert.That(
                GetField<string>(restored, "beatMapFingerprint"),
                Is.EqualTo(RadialBeatMapFingerprint.Compute(beatMap)));
        }

        [Test]
        public void ControllerParsesAssignedRadialV2Artifact()
        {
            RadialBeatMapData beatMap = CreateBeatMap();
            Type serializer = RuntimeType("RadialBeatMapArtifactSerializer");
            object artifact = serializer.GetMethod("Create").Invoke(
                null,
                new object[]
                {
                    "editor-track",
                    "editor-preset",
                    beatMap,
                    new AnalyzerQualityReport(),
                    new PlannerQualityReport { totalInputCost = 1 },
                    null
                });
            string json = (string)serializer.GetMethod("Serialize").Invoke(
                null,
                new[] { artifact, (object)false });
            Type controller = Type.GetType(
                "PulseForge.Runtime.Unity.Prototype.DebugRhythmPrototypeController, Assembly-CSharp",
                true);
            MethodInfo parse = controller.GetMethod(
                "ParseRadialBeatMapJson",
                BindingFlags.NonPublic | BindingFlags.Static);

            RadialBeatMapData parsed = (RadialBeatMapData)parse.Invoke(null, new object[] { json });

            Assert.That(parsed.encounters, Has.Count.EqualTo(1));
            Assert.That(parsed.encounters[0].eventId, Is.EqualTo("encounter-1"));
        }

        [Test]
        public void LegacyV1CacheReportsNeedsRebuild()
        {
            object store = CreateStore();
            object track = CreateTrack("track", 1);
            object preset = CreatePreset("preset", 0, 0);

            Assert.That(GetCacheStatus(store, track, preset), Is.EqualTo("NeedsRebuild"));
        }

        [Test]
        public void PreviousRadialV2CacheReportsNeedsRebuild()
        {
            object store = CreateStore();
            object track = CreateTrack("track", 1);
            object preset = CreatePreset("preset", 2, 2);

            Assert.That(GetCacheStatus(store, track, preset), Is.EqualTo("NeedsRebuild"));
        }

        [Test]
        public void BrokenV2CacheReportsDamaged()
        {
            object store = CreateStore();
            CacheWriteResult write = WriteCache(store, "track", "preset");
            CreateReadyMetadata(write, out object track, out object preset);
            File.WriteAllText(
                Path.Combine(rootDirectory, write.BeatMapRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                "{broken-json");

            Assert.That(GetCacheStatus(store, track, preset), Is.EqualTo("Damaged"));
        }

        [Test]
        public void WrittenRadialCacheLoadsWithMatchingFingerprint()
        {
            object store = CreateStore();
            CacheWriteResult write = WriteCache(store, "track", "preset");
            CreateReadyMetadata(write, out object track, out object preset);
            object[] arguments = { track, preset, null, null, null };

            bool loaded = (bool)store.GetType().GetMethod("TryLoadRadialPresetCache")
                .Invoke(store, arguments);

            Assert.That(loaded, Is.True, arguments[4] as string);
            object cacheData = arguments[3];
            Assert.That(
                GetField<string>(cacheData, "beatMapFingerprint"),
                Is.EqualTo(write.Fingerprint));
            Assert.That(
                GetField<RadialBeatMapData>(cacheData, "radialBeatMap").encounters,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void CachedWavAllowsSourceIndependentRebuild()
        {
            object store = CreateStore();
            CacheWriteResult write = WriteCache(store, "track", "preset");
            object track = CreateTrack("track", 1);
            SetField(track, "cachedAudioRelativePath", write.AudioRelativePath);
            object[] arguments = { track, null };

            bool available = (bool)store.GetType().GetMethod("TryGetCachedAudioPath")
                .Invoke(store, arguments);

            Assert.That(available, Is.True);
            Assert.That((string)arguments[1], Is.Not.Empty);
        }

        [Test]
        public void WritingSamePresetTwiceDoesNotCreateDuplicateRadialFiles()
        {
            object store = CreateStore();
            WriteCache(store, "track", "preset");
            WriteCache(store, "track", "preset");

            string[] files = Directory.GetFiles(
                Path.Combine(rootDirectory, "LibraryCache", "track", "presets"),
                "*.radial.json");
            Assert.That(files, Has.Length.EqualTo(1));
        }

        [Test]
        public void PresetKeyIncludesAnalysisAxesButIgnoresGameMode()
        {
            Type normalizer = RuntimeType("SaveDataNormalizer");
            MethodInfo createKey = normalizer.GetMethod(
                "PresetKey",
                new[] { typeof(string), typeof(string), typeof(string), typeof(int), typeof(string) });
            string standard = (string)createKey.Invoke(
                null,
                new object[] { "Onset", "Hard", "Aggressive", 2, "Standard" });
            string oneLife = (string)createKey.Invoke(
                null,
                new object[] { "Onset", "Hard", "Aggressive", 2, "OneLife" });

            Assert.That(standard, Does.Contain("ONSET"));
            Assert.That(standard, Does.Contain("HARD"));
            Assert.That(standard, Does.Contain("AGGRESSIVE"));
            Assert.That(standard, Does.EndWith("A2"));
            Assert.That(oneLife, Is.EqualTo(standard));
        }

        [Test]
        public void LegacyAndRadialScoresAreNotComparable()
        {
            Assert.That(
                ScoreSchema.CanCompare(ScoreSchema.LegacyV1, string.Empty, ScoreSchema.RadialV2, "abc"),
                Is.False);
            Assert.That(
                ScoreSchema.CanCompare(ScoreSchema.RadialV2, "abc", ScoreSchema.RadialV2, "def"),
                Is.False);
        }

        private object CreateStore()
        {
            return Activator.CreateInstance(RuntimeType("LibraryCacheStore"), rootDirectory);
        }

        private CacheWriteResult WriteCache(object store, string trackId, string presetId)
        {
            MethodInfo method = store.GetType().GetMethod("TryWriteRadialPresetCache");
            object[] arguments =
            {
                trackId,
                presetId,
                sourceWavPath,
                CreateBeatMap(),
                new AnalyzerQualityReport { candidateCount = 1 },
                new PlannerQualityReport { totalInputCost = 1, result = PlannerQualityResult.Pass },
                "2026-01-01T00:00:00.0000000Z",
                null,
                null,
                null,
                null
            };
            bool saved = (bool)method.Invoke(store, arguments);
            Assert.That(saved, Is.True, arguments[10] as string);
            return new CacheWriteResult((string)arguments[7], (string)arguments[8], (string)arguments[9]);
        }

        private static object CreateTrack(string trackId, int audioCacheVersion)
        {
            object track = CreateRuntimeObject("SavedTrackData");
            SetField(track, "trackId", trackId);
            SetField(track, "audioCacheVersion", audioCacheVersion);
            return track;
        }

        private static object CreatePreset(string presetId, int analyzerVersion, int cacheVersion)
        {
            object preset = CreateRuntimeObject("SavedTrackPresetData");
            SetField(preset, "presetId", presetId);
            SetField(preset, "analyzerVersion", analyzerVersion);
            SetField(preset, "beatMapCacheVersion", cacheVersion);
            return preset;
        }

        private static void CreateReadyMetadata(
            CacheWriteResult write,
            out object track,
            out object preset)
        {
            track = CreateTrack("track", 1);
            SetField(track, "cachedAudioRelativePath", write.AudioRelativePath);
            preset = CreatePreset("preset", 2, 3);
            SetField(preset, "cachedBeatmapRelativePath", write.BeatMapRelativePath);
            SetField(preset, "beatMapFingerprint", write.Fingerprint);
            SetField(preset, "eventCount", 1);
        }

        private static string GetCacheStatus(object store, object track, object preset)
        {
            object status = store.GetType().GetMethod("GetCacheStatus")
                .Invoke(store, new[] { track, preset });
            return status.ToString();
        }

        private static RadialBeatMapData CreateBeatMap()
        {
            return new RadialBeatMapData
            {
                schemaVersion = 3,
                displayName = "Fixture",
                encounters = new List<RadialEncounterEventData>
                {
                    new RadialEncounterEventData
                    {
                        eventId = "encounter-1",
                        eventType = RadialEventType.Tap,
                        intensity = 0.8f,
                        requirements = new List<InputRequirementData>
                        {
                            new InputRequirementData
                            {
                                requirementId = "requirement-1",
                                gestureType = InputGestureType.Tap,
                                acceptedActions = RhythmActionMask.LightAttack,
                                phase = RhythmInputPhase.Pressed,
                                targetTimeSeconds = 1d,
                                perfectWindowSeconds = 0.045d,
                                goodWindowSeconds = 0.1d,
                                exclusive = true
                            }
                        },
                        targets = new List<EncounterTargetData>()
                    }
                }
            };
        }

        private static object CreateRuntimeObject(string typeName)
        {
            return Activator.CreateInstance(RuntimeType(typeName));
        }

        private static Type RuntimeType(string typeName)
        {
            return Type.GetType(
                "PulseForge.Runtime.Unity.Persistence." + typeName + ", Assembly-CSharp",
                true);
        }

        private static void SetField(object instance, string fieldName, object value)
        {
            instance.GetType().GetField(fieldName).SetValue(instance, value);
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            return (T)instance.GetType().GetField(fieldName).GetValue(instance);
        }

        private readonly struct CacheWriteResult
        {
            public CacheWriteResult(string audioRelativePath, string beatMapRelativePath, string fingerprint)
            {
                AudioRelativePath = audioRelativePath;
                BeatMapRelativePath = beatMapRelativePath;
                Fingerprint = fingerprint;
            }

            public string AudioRelativePath { get; }
            public string BeatMapRelativePath { get; }
            public string Fingerprint { get; }
        }
    }
}
