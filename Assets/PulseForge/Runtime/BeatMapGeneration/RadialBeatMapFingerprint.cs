using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PulseForge.Domain.Rhythm;

namespace PulseForge.BeatMapGeneration
{
    public static class RadialBeatMapFingerprint
    {
        public static string Compute(RadialBeatMapData beatMap)
        {
            if (beatMap == null)
            {
                throw new ArgumentNullException(nameof(beatMap));
            }

            StringBuilder canonical = new StringBuilder(4096);
            Append(canonical, beatMap.schemaVersion);
            Append(canonical, beatMap.globalOffsetSeconds);
            int encounterCount = beatMap.encounters == null ? 0 : beatMap.encounters.Count;
            Append(canonical, encounterCount);
            for (int encounterIndex = 0; encounterIndex < encounterCount; encounterIndex++)
            {
                RadialEncounterEventData encounter = beatMap.encounters[encounterIndex]
                    ?? throw new ArgumentException("Beatmap contains a null encounter.", nameof(beatMap));
                Append(canonical, (int)encounter.eventType);
                Append(canonical, encounter.intensity);
                Append(canonical, encounter.telegraphLeadSeconds);
                Append(canonical, encounter.perfectSpreadSeconds);
                Append(canonical, encounter.goodSpreadSeconds);
                FailureEffectData failureEffect = encounter.failureEffect;
                Append(canonical, failureEffect == null
                    ? (int)FailureEffectType.None
                    : (int)failureEffect.effectType);
                Append(canonical, failureEffect == null ? 0d : failureEffect.durationSeconds);
                Append(canonical, failureEffect == null ? 1f : failureEffect.revealLeadMultiplier);
                Append(canonical, failureEffect == null ? 0d : failureEffect.minimumVisibleLeadSeconds);

                int requirementCount = encounter.requirements == null ? 0 : encounter.requirements.Count;
                Append(canonical, requirementCount);
                for (int requirementIndex = 0; requirementIndex < requirementCount; requirementIndex++)
                {
                    InputRequirementData requirement = encounter.requirements[requirementIndex]
                        ?? throw new ArgumentException("Beatmap contains a null requirement.", nameof(beatMap));
                    Append(canonical, (int)requirement.acceptedActions);
                    Append(canonical, (int)requirement.gestureType);
                    Append(canonical, (int)requirement.phase);
                    Append(canonical, requirement.targetTimeSeconds);
                    Append(canonical, requirement.perfectWindowSeconds);
                    Append(canonical, requirement.goodWindowSeconds);
                    Append(canonical, requirement.orderIndex);
                    Append(canonical, requirement.isOptional);
                    Append(canonical, requirement.exclusive);
                    Append(canonical, requirement.holdEndTimeSeconds);
                    Append(canonical, requirement.earlyReleaseGraceSeconds);
                    Append(canonical, requirement.allowEarlyReleaseAsGood);
                    Append(canonical, requirement.autoCompleteAtHoldEnd);
                    Append(canonical, FindRequirementIndex(encounter, requirement.pairedRequirementId));
                    Append(canonical, requirement.minimumHoldSeconds);
                    Append(canonical, requirement.maximumHoldSeconds);
                    Append(canonical, requirement.windowStartTimeSeconds);
                    Append(canonical, requirement.perfectDeadlineSeconds);
                    Append(canonical, requirement.goodDeadlineSeconds);
                    Append(canonical, requirement.requiredPressCount);
                    Append(canonical, requirement.minimumPressIntervalSeconds);
                }

                int targetCount = encounter.targets == null ? 0 : encounter.targets.Count;
                Append(canonical, targetCount);
                for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
                {
                    EncounterTargetData target = encounter.targets[targetIndex]
                        ?? throw new ArgumentException("Beatmap contains a null target.", nameof(beatMap));
                    Append(canonical, FindRequirementIndex(encounter, target.requirementId));
                    Append(canonical, (int)target.direction);
                    Append(canonical, (int)target.archetype);
                }
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
                StringBuilder output = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    output.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return output.ToString();
            }
        }

        private static int FindRequirementIndex(
            RadialEncounterEventData encounter,
            string requirementId)
        {
            if (string.IsNullOrEmpty(requirementId) || encounter.requirements == null)
            {
                return -1;
            }
            for (int i = 0; i < encounter.requirements.Count; i++)
            {
                if (encounter.requirements[i] != null
                    && string.Equals(
                        encounter.requirements[i].requirementId,
                        requirementId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void Append(StringBuilder builder, double value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void Append(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static void Append(StringBuilder builder, int value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
        }

        private static void Append(StringBuilder builder, bool value)
        {
            builder.Append(value ? '1' : '0').Append('|');
        }
    }
}
