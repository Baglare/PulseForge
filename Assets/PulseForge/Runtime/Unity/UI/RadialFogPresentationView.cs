using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialFogPresentationView : MonoBehaviour
    {
        private const double SmokeDurationSeconds = 0.58d;

        [SerializeField] private GameObject fogOverlayRoot;
        [SerializeField] private Image[] vignettePanels;
        [SerializeField] private RectTransform statusChip;
        [SerializeField] private Text remainingLabel;
        [SerializeField] private RectTransform smokeBurst;
        [SerializeField] private Image smokeImage;
        [SerializeField] private Text smokeLabel;

        private string smokeEncounterId = string.Empty;
        private double smokeStartSongTimeSeconds;

        public void Configure(
            GameObject overlayRoot,
            Image[] panels,
            RectTransform chip,
            Text remaining,
            RectTransform smoke,
            Image smokeGraphic,
            Text smokeText)
        {
            fogOverlayRoot = overlayRoot;
            vignettePanels = panels;
            statusChip = chip;
            remainingLabel = remaining;
            smokeBurst = smoke;
            smokeImage = smokeGraphic;
            smokeLabel = smokeText;
        }

        public void Render(RadialStatusEffectSnapshot status, double songTimeSeconds)
        {
            if (fogOverlayRoot != null)
            {
                fogOverlayRoot.SetActive(status.IsFogActive);
            }
            if (status.IsFogActive && remainingLabel != null)
            {
                remainingLabel.text = status.RemainingSeconds(songTimeSeconds).ToString("0.0") + "s";
            }

            RenderSmoke(songTimeSeconds);
        }

        public void ShowSaboteurSmoke(string encounterId, double songTimeSeconds)
        {
            if (string.IsNullOrEmpty(encounterId)
                || string.Equals(smokeEncounterId, encounterId, System.StringComparison.Ordinal))
            {
                return;
            }

            smokeEncounterId = encounterId;
            smokeStartSongTimeSeconds = songTimeSeconds;
            if (smokeBurst != null)
            {
                smokeBurst.gameObject.SetActive(true);
            }
        }

        public void ResetView()
        {
            smokeEncounterId = string.Empty;
            smokeStartSongTimeSeconds = 0d;
            if (fogOverlayRoot != null)
            {
                fogOverlayRoot.SetActive(false);
            }
            if (smokeBurst != null)
            {
                smokeBurst.localScale = Vector3.one;
                smokeBurst.gameObject.SetActive(false);
            }
        }

        private void RenderSmoke(double songTimeSeconds)
        {
            if (smokeBurst == null || !smokeBurst.gameObject.activeSelf)
            {
                return;
            }

            float progress = Mathf.Clamp01((float)(
                (songTimeSeconds - smokeStartSongTimeSeconds) / SmokeDurationSeconds));
            smokeBurst.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.48f, progress);
            if (smokeImage != null)
            {
                Color color = PulseForgeUITheme.WithAlpha(
                    PulseForgeUITheme.SecondaryText,
                    Mathf.Lerp(0.72f, 0f, progress));
                smokeImage.color = color;
            }
            if (smokeLabel != null)
            {
                smokeLabel.color = PulseForgeUITheme.WithAlpha(
                    PulseForgeUITheme.PrimaryText,
                    Mathf.Lerp(1f, 0f, progress));
            }
            if (progress >= 1f)
            {
                smokeBurst.gameObject.SetActive(false);
            }
        }
    }
}
