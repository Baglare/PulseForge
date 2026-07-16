using System;

namespace PulseForge.Domain.Rhythm
{
    public static class ScoreSchema
    {
        public const string LegacyV1 = "legacy-v1";
        public const string RadialV2 = "radial-v2";

        public static bool CanCompare(
            string leftSchema,
            string leftFingerprint,
            string rightSchema,
            string rightFingerprint)
        {
            return !string.IsNullOrWhiteSpace(leftSchema)
                && string.Equals(leftSchema, rightSchema, StringComparison.Ordinal)
                && string.Equals(
                    leftFingerprint ?? string.Empty,
                    rightFingerprint ?? string.Empty,
                    StringComparison.Ordinal);
        }
    }
}
