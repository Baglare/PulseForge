using System;
using PulseForge.AudioAnalysis;
using PulseForge.BeatMapGeneration;
using PulseForge.Domain.Rhythm;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public static class RadialBeatMapArtifactSerializer
    {
        public static RadialBeatMapCacheData Create(
            string trackId,
            string presetId,
            RadialBeatMapData beatMap,
            AnalyzerQualityReport analyzerQuality,
            PlannerQualityReport plannerQuality,
            string createdAtUtc = null)
        {
            if (!HasPlayableRequirement(beatMap))
            {
                throw new ArgumentException(
                    "Radial beatmap contains no playable requirements.",
                    nameof(beatMap));
            }

            string now = SaveDefaults.UtcNow();
            return new RadialBeatMapCacheData
            {
                beatMapCacheVersion = SaveDefaults.RadialBeatMapCacheVersion,
                analyzerVersion = SaveDefaults.AnalyzerVersion,
                trackId = trackId ?? string.Empty,
                presetId = presetId ?? string.Empty,
                beatMapFingerprint = RadialBeatMapFingerprint.Compute(beatMap),
                createdAtUtc = string.IsNullOrWhiteSpace(createdAtUtc) ? now : createdAtUtc,
                updatedAtUtc = now,
                radialBeatMap = beatMap,
                analyzerQuality = analyzerQuality ?? new AnalyzerQualityReport(),
                plannerQuality = plannerQuality ?? new PlannerQualityReport()
            };
        }

        public static string Serialize(RadialBeatMapCacheData data, bool prettyPrint = true)
        {
            if (!TryValidate(data, out string errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(data));
            }
            return JsonUtility.ToJson(data, prettyPrint);
        }

        public static bool TryDeserialize(
            string json,
            out RadialBeatMapCacheData data,
            out string errorMessage)
        {
            data = null;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "Radial beatmap JSON is empty.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<RadialBeatMapCacheData>(json);
                if (!TryValidate(data, out errorMessage))
                {
                    data = null;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = "Radial beatmap JSON could not be parsed: " + exception.Message;
                data = null;
                return false;
            }
        }

        public static bool TryValidate(
            RadialBeatMapCacheData data,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (data == null
                || data.beatMapCacheVersion != SaveDefaults.RadialBeatMapCacheVersion
                || data.analyzerVersion != SaveDefaults.AnalyzerVersion
                || data.radialBeatMap == null
                || data.radialBeatMap.schemaVersion != 3
                || !HasPlayableRequirement(data.radialBeatMap))
            {
                errorMessage = "Radial beatmap artifact header is invalid.";
                return false;
            }

            string fingerprint = RadialBeatMapFingerprint.Compute(data.radialBeatMap);
            if (!string.Equals(
                fingerprint,
                data.beatMapFingerprint,
                StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Radial beatmap artifact fingerprint does not match.";
                return false;
            }
            return true;
        }

        private static bool HasPlayableRequirement(RadialBeatMapData beatMap)
        {
            if (beatMap == null || beatMap.encounters == null)
            {
                return false;
            }
            for (int i = 0; i < beatMap.encounters.Count; i++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[i];
                if (encounter != null
                    && encounter.requirements != null
                    && encounter.requirements.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
