using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialCompoundGroupView : MonoBehaviour
    {
        private const int LinkCapacity = 8;

        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private Image[] links;
        [SerializeField] private RectTransform indicatorRoot;
        [SerializeField] private Image indicatorBody;
        [SerializeField] private Outline indicatorOutline;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Text secondaryLabel;
        [SerializeField] private Image progressFill;

        private RhythmActionMask cachedFirstAction;
        private RhythmActionMask cachedSecondAction;
        private int cachedFirstValue = int.MinValue;
        private int cachedSecondValue = int.MinValue;
        private RadialCompoundLinkState cachedState = (RadialCompoundLinkState)(-1);

        public RadialPresentationKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        internal static RadialCompoundGroupView Create(RectTransform parent, int index)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(
                "Compound Group " + index,
                parent);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            RadialCompoundGroupView view = root.gameObject.AddComponent<RadialCompoundGroupView>();
            view.viewRoot = root;
            view.links = new Image[LinkCapacity];
            for (int linkIndex = 0; linkIndex < LinkCapacity; linkIndex++)
            {
                RectTransform link = PulseForgeUIFactory.CreateRect(
                    "Link " + linkIndex,
                    root);
                link.anchorMin = new Vector2(0.5f, 0.5f);
                link.anchorMax = new Vector2(0.5f, 0.5f);
                link.pivot = new Vector2(0f, 0.5f);
                link.sizeDelta = new Vector2(1f, 3f);
                Image image = link.gameObject.AddComponent<Image>();
                image.sprite = PulseForgeUIFactory.RoundedSprite;
                image.color = PulseForgeUITheme.Primary;
                image.raycastTarget = false;
                view.links[linkIndex] = image;
            }

            RectTransform indicator = PulseForgeUIFactory.CreateRect("Group Indicator", root);
            indicator.anchorMin = new Vector2(0.5f, 0.5f);
            indicator.anchorMax = new Vector2(0.5f, 0.5f);
            indicator.pivot = new Vector2(0.5f, 0.5f);
            indicator.sizeDelta = new Vector2(124f, 48f);
            Image indicatorImage = indicator.gameObject.AddComponent<Image>();
            indicatorImage.sprite = PulseForgeUIFactory.RoundedSprite;
            indicatorImage.color = PulseForgeUITheme.SurfaceRaised;
            indicatorImage.raycastTarget = false;
            Outline outline = indicator.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.Primary;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            view.indicatorRoot = indicator;
            view.indicatorBody = indicatorImage;
            view.indicatorOutline = outline;

            Text primary = PulseForgeUIFactory.CreateText(
                "Primary",
                indicator,
                string.Empty,
                13,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.UpperCenter,
                FontStyle.Bold);
            primary.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            primary.rectTransform.anchorMax = Vector2.one;
            primary.rectTransform.offsetMin = Vector2.zero;
            primary.rectTransform.offsetMax = Vector2.zero;
            view.primaryLabel = primary;

            Text secondary = PulseForgeUIFactory.CreateText(
                "Secondary",
                indicator,
                string.Empty,
                10,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.LowerCenter,
                FontStyle.Bold);
            secondary.rectTransform.anchorMin = Vector2.zero;
            secondary.rectTransform.anchorMax = new Vector2(1f, 0.58f);
            secondary.rectTransform.offsetMin = new Vector2(3f, 3f);
            secondary.rectTransform.offsetMax = new Vector2(-3f, -2f);
            view.secondaryLabel = secondary;

            RectTransform progress = PulseForgeUIFactory.CreateRect("Group Progress", indicator);
            progress.anchorMin = new Vector2(0.08f, 0f);
            progress.anchorMax = new Vector2(0.92f, 0f);
            progress.pivot = new Vector2(0.5f, 0f);
            progress.anchoredPosition = new Vector2(0f, 2f);
            progress.sizeDelta = new Vector2(0f, 3f);
            Image progressImage = progress.gameObject.AddComponent<Image>();
            progressImage.sprite = PulseForgeUIFactory.RoundedSprite;
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Horizontal;
            progressImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressImage.raycastTarget = false;
            view.progressFill = progressImage;
            view.ResetView();
            return view;
        }

        public void Activate(RadialPresentationKey key)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
            cachedFirstAction = RhythmActionMask.None;
            cachedSecondAction = RhythmActionMask.None;
            cachedFirstValue = int.MinValue;
            cachedSecondValue = int.MinValue;
            cachedState = (RadialCompoundLinkState)(-1);
        }

        public void SetLink(
            int index,
            Vector2 from,
            Vector2 to,
            RadialCompoundLinkState state)
        {
            if (index < 0 || index >= links.Length)
            {
                return;
            }
            Image link = links[index];
            RectTransform rect = link.rectTransform;
            Vector2 delta = to - from;
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, state == RadialCompoundLinkState.Failed ? 5f : 3f);
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            link.color = ResolveStateColor(state);
            link.gameObject.SetActive(true);
        }

        public void HideLinksFrom(int firstHiddenIndex)
        {
            int start = Mathf.Max(0, firstHiddenIndex);
            for (int i = start; i < links.Length; i++)
            {
                links[i].gameObject.SetActive(false);
            }
        }

        public void RenderChord(
            Vector2 center,
            RhythmActionMask firstAction,
            RhythmActionMask secondAction,
            RadialCompoundLinkState state)
        {
            indicatorRoot.anchoredPosition = center;
            if (cachedFirstAction != firstAction
                || cachedSecondAction != secondAction
                || cachedState != state)
            {
                primaryLabel.text = RadialEncounterView.ResolveActionLabel(firstAction)
                    + " + "
                    + RadialEncounterView.ResolveActionLabel(secondAction);
                secondaryLabel.text = state == RadialCompoundLinkState.Failed
                    ? "CHORD BROKEN"
                    : state == RadialCompoundLinkState.Partial
                        ? "1 / 2"
                        : state == RadialCompoundLinkState.Complete
                            ? "TOGETHER"
                            : "PRESS TOGETHER";
                Cache(firstAction, secondAction, 0, 0, state);
            }
            ApplyIndicatorState(state, state == RadialCompoundLinkState.Complete ? 1f : state == RadialCompoundLinkState.Partial ? 0.5f : 0f);
        }

        public void RenderSequence(
            Vector2 center,
            int activeStep,
            int stepCount,
            int completedCount,
            bool failed)
        {
            RadialCompoundLinkState state = failed
                ? RadialCompoundLinkState.Failed
                : completedCount >= stepCount
                    ? RadialCompoundLinkState.Complete
                    : completedCount > 0
                        ? RadialCompoundLinkState.Partial
                        : RadialCompoundLinkState.Pending;
            indicatorRoot.anchoredPosition = center;
            if (cachedFirstValue != activeStep
                || cachedSecondValue != completedCount
                || cachedState != state)
            {
                primaryLabel.text = activeStep >= 0
                    ? "STEP " + (activeStep + 1) + " / " + stepCount
                    : "SEQUENCE " + completedCount + " / " + stepCount;
                secondaryLabel.text = failed ? "ACTIVE STEP MISSED" : "DONE -> ACTIVE -> NEXT";
                Cache(RhythmActionMask.None, RhythmActionMask.None, activeStep, completedCount, state);
            }
            ApplyIndicatorState(state, stepCount <= 0 ? 0f : (float)completedCount / stepCount);
        }

        public void RenderChain(
            Vector2 center,
            bool swarm,
            int remaining,
            int total,
            bool failed)
        {
            int completed = Mathf.Max(0, total - remaining);
            RadialCompoundLinkState state = failed
                ? RadialCompoundLinkState.Failed
                : remaining <= 0
                    ? RadialCompoundLinkState.Complete
                    : completed > 0
                        ? RadialCompoundLinkState.Partial
                        : RadialCompoundLinkState.Pending;
            indicatorRoot.anchoredPosition = center;
            if (cachedFirstValue != remaining
                || cachedSecondValue != total
                || cachedState != state)
            {
                primaryLabel.text = swarm ? "SWARM " + remaining : "CHAIN " + remaining;
                secondaryLabel.text = failed ? "CUE MISSED" : "REMAINING / " + total;
                Cache(RhythmActionMask.None, RhythmActionMask.None, remaining, total, state);
            }
            ApplyIndicatorState(state, total <= 0 ? 0f : (float)completed / total);
        }

        public void RenderSweep(
            Vector2 center,
            RhythmActionMask action,
            int targetCount,
            RadialCompoundLinkState state,
            RadialPresentationResultState resultState)
        {
            indicatorRoot.anchoredPosition = center;
            if (cachedFirstAction != action
                || cachedFirstValue != targetCount
                || cachedState != state)
            {
                primaryLabel.text = resultState == RadialPresentationResultState.Perfect
                    ? "PERFECT SWEEP"
                    : resultState == RadialPresentationResultState.Good
                        ? "GOOD SWEEP"
                        : resultState == RadialPresentationResultState.Miss
                            || resultState == RadialPresentationResultState.WrongInput
                            ? "MISSED SWEEP"
                            : RadialEncounterView.ResolveActionLabel(action) + " SWEEP";
                secondaryLabel.text = "1 INPUT / " + targetCount + " TARGETS";
                Cache(action, RhythmActionMask.None, targetCount, 0, state);
            }
            ApplyIndicatorState(state, state == RadialCompoundLinkState.Complete ? 1f : 0f);
        }

        public void ResetView()
        {
            Key = default(RadialPresentationKey);
            IsInUse = false;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localRotation = Quaternion.identity;
            }
            if (links != null)
            {
                HideLinksFrom(0);
            }
            if (primaryLabel != null)
            {
                primaryLabel.text = string.Empty;
            }
            if (secondaryLabel != null)
            {
                secondaryLabel.text = string.Empty;
            }
            if (progressFill != null)
            {
                progressFill.fillAmount = 0f;
            }
            gameObject.SetActive(false);
        }

        private void ApplyIndicatorState(RadialCompoundLinkState state, float progress)
        {
            Color color = ResolveStateColor(state);
            indicatorBody.color = Color.Lerp(PulseForgeUITheme.SurfaceRaised, color, 0.22f);
            if (indicatorOutline != null)
            {
                indicatorOutline.effectColor = color;
            }
            primaryLabel.color = color;
            progressFill.color = color;
            progressFill.fillAmount = Mathf.Clamp01(progress);
        }

        private void Cache(
            RhythmActionMask firstAction,
            RhythmActionMask secondAction,
            int firstValue,
            int secondValue,
            RadialCompoundLinkState state)
        {
            cachedFirstAction = firstAction;
            cachedSecondAction = secondAction;
            cachedFirstValue = firstValue;
            cachedSecondValue = secondValue;
            cachedState = state;
        }

        private static Color ResolveStateColor(RadialCompoundLinkState state)
        {
            switch (state)
            {
                case RadialCompoundLinkState.Complete:
                    return PulseForgeUITheme.Perfect;
                case RadialCompoundLinkState.Partial:
                    return PulseForgeUITheme.Good;
                case RadialCompoundLinkState.Failed:
                    return PulseForgeUITheme.Miss;
                default:
                    return PulseForgeUITheme.Primary;
            }
        }
    }
}
