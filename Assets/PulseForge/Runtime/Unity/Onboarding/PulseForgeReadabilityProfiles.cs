using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;

namespace PulseForge.Runtime.Unity.Onboarding
{
    public enum PulseForgeReadabilityProfile
    {
        Standard,
        Assisted,
        HighClarity
    }

    public static class PulseForgeReadabilityProfiles
    {
        public static void Apply(
            PulseForgeSettingsData settings,
            PulseForgeReadabilityProfile profile)
        {
            if (settings == null)
            {
                return;
            }

            settings.showUpcomingInputs = true;
            settings.beatPulseEnabled = true;
            switch (profile)
            {
                case PulseForgeReadabilityProfile.Standard:
                    settings.readabilityMode = RadialReadabilityMode.Standard.ToString();
                    settings.forecastLeadMultiplier = 1.25f;
                    settings.defaultTimingAssist = TimingAssistMode.Standard.ToString();
                    break;
                case PulseForgeReadabilityProfile.HighClarity:
                    settings.readabilityMode = RadialReadabilityMode.HighClarity.ToString();
                    settings.forecastLeadMultiplier = 1.75f;
                    settings.defaultTimingAssist = TimingAssistMode.Relaxed.ToString();
                    break;
                default:
                    settings.readabilityMode = RadialReadabilityMode.Assisted.ToString();
                    settings.forecastLeadMultiplier = 1.50f;
                    settings.defaultTimingAssist = TimingAssistMode.Relaxed.ToString();
                    break;
            }
        }
    }
}
