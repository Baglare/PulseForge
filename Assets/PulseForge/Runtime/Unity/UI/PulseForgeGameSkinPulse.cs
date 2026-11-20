using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    /// <summary>Background pulse: the forge breathes on a steady beat. Static when UI motion is off.</summary>
    public sealed class PulseForgeGameSkinPulse : MonoBehaviour
    {
        private const float Bpm = 96f;

        [SerializeField] private PulseForgeSceneUIRoot root;
        [SerializeField] private Image core;
        [SerializeField] private RectTransform[] rings;
        [SerializeField] private float[] baseAlphas;
        [SerializeField] private float coreBaseAlpha;
        private Vector2[] baseSizes;

        internal void Configure(PulseForgeSceneUIRoot owner, Image coreImage, RectTransform[] ringRects, float[] alphas)
        {
            root = owner;
            core = coreImage;
            rings = ringRects;
            baseAlphas = alphas;
            coreBaseAlpha = coreImage == null ? 0f : coreImage.color.a;
            CacheSizes();
        }

        private void CacheSizes()
        {
            if (rings == null)
            {
                return;
            }

            baseSizes = new Vector2[rings.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                baseSizes[i] = rings[i] == null ? Vector2.zero : rings[i].sizeDelta;
            }
        }

        private void LateUpdate()
        {
            if (rings == null || baseAlphas == null)
            {
                return;
            }

            if (baseSizes == null || baseSizes.Length != rings.Length)
            {
                CacheSizes();
            }

            // During a run the song owns the beat: freeze and dim so the backdrop never competes with it.
            bool inRun = root != null && IsActive(root.GameplayHud);
            bool animate = !inRun && (root == null || root.EnableMotion);
            float beat = animate ? Mathf.Repeat(Time.unscaledTime * Bpm / 60f, 1f) : 0.6f;
            float dim = inRun ? 0.22f : 1f;
            for (int i = 0; i < rings.Length; i++)
            {
                RectTransform ring = rings[i];
                if (ring == null)
                {
                    continue;
                }

                // Each ring catches the beat a little later, so the hit travels outward.
                float local = Mathf.Repeat(beat - i * 0.07f, 1f);
                float kick = Mathf.Exp(-local * 6f);
                ring.localScale = Vector3.one * (1f + 0.025f * kick);
                Image image = ring.GetComponent<Image>();
                if (image != null && i < baseAlphas.Length)
                {
                    Color color = image.color;
                    color.a = baseAlphas[i] * (0.65f + 0.9f * kick) * dim;
                    image.color = color;
                }
            }

            if (core != null)
            {
                float kick = Mathf.Exp(-beat * 5f);
                core.rectTransform.localScale = Vector3.one * (1f + 0.05f * kick);
                Color coreColor = core.color;
                coreColor.a = coreBaseAlpha * dim;
                core.color = coreColor;
            }
        }

        private static bool IsActive(PulseForgePanelView view)
        {
            return view != null && view.PanelRoot != null && view.PanelRoot.activeInHierarchy;
        }
    }
}
