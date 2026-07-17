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
        [SerializeField] private CanvasGroup fogCanvasGroup;

        private string smokeEncounterId = string.Empty;
        private double smokeStartSongTimeSeconds;

        public GameObject FogOverlayRoot => fogOverlayRoot;

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

        public void ConfigurePolish(CanvasGroup canvasGroup)
        {
            fogCanvasGroup = canvasGroup;
            if (fogCanvasGroup != null)
            {
                fogCanvasGroup.blocksRaycasts = false;
                fogCanvasGroup.interactable = false;
            }
        }

        public void Render(RadialStatusEffectSnapshot status, double songTimeSeconds)
        {
            Render(status, songTimeSeconds, false);
        }

        public void Render(
            RadialStatusEffectSnapshot status,
            double songTimeSeconds,
            bool highClarity)
        {
            if (fogOverlayRoot != null)
            {
                fogOverlayRoot.SetActive(status.IsFogActive);
            }
            if (fogCanvasGroup == null && fogOverlayRoot != null)
            {
                fogCanvasGroup = fogOverlayRoot.GetComponent<CanvasGroup>();
            }
            if (fogCanvasGroup != null)
            {
                float fade = 0f;
                if (status.IsFogActive)
                {
                    float startFade = Mathf.Clamp01((float)(
                        (songTimeSeconds - status.StartedAtSongTimeSeconds)
                        / RadialVfxTokens.FogTransitionDuration));
                    float endFade = Mathf.Clamp01((float)(
                        status.RemainingSeconds(songTimeSeconds)
                        / RadialVfxTokens.FogTransitionDuration));
                    fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(startFade, endFade));
                }
                fogCanvasGroup.alpha = fade * (highClarity ? 0.66f : 0.88f);
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
            if (fogCanvasGroup != null)
            {
                fogCanvasGroup.alpha = 0f;
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
