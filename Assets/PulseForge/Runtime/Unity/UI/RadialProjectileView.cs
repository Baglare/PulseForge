using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialProjectileView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private CanvasGroup cueCanvasGroup;
        [SerializeField] private Image projectileImage;
        [SerializeField] private Outline projectileOutline;
        [SerializeField] private Image actionBadge;
        [SerializeField] private Text actionLabel;

        private Color baseColor;
        private RhythmActionMask cachedChoiceActions;
        private RhythmAction? cachedSelectedAction;
        private bool cachedChoiceFailure;

        public RadialPresentationKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        internal static RadialProjectileView Create(RectTransform parent, int index)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(
                "Projectile View " + index,
                parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(34f, 12f);

            RadialProjectileView view = root.gameObject.AddComponent<RadialProjectileView>();
            view.viewRoot = root;
            view.cueCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = PulseForgeUIFactory.RoundedSprite;
            image.color = PulseForgeUITheme.Perfect;
            image.raycastTarget = false;
            view.projectileImage = image;
            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.DarkText;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            view.projectileOutline = outline;

            RectTransform badge = PulseForgeUIFactory.CreateRect("Projectile Action", root);
            badge.anchorMin = new Vector2(0.5f, 0.5f);
            badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(0f, 18f);
            badge.sizeDelta = new Vector2(34f, 24f);
            Image badgeImage = badge.gameObject.AddComponent<Image>();
            badgeImage.sprite = PulseForgeUIFactory.RoundedSprite;
            badgeImage.raycastTarget = false;
            view.actionBadge = badgeImage;
            Text badgeLabel = PulseForgeUIFactory.CreateText(
                "Action",
                badge,
                string.Empty,
                12,
                PulseForgeUITheme.DarkText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.actionLabel = badgeLabel;
            view.ResetView();
            return view;
        }

        public void Activate(RadialPresentationKey key, RhythmActionMask actions)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
            baseColor = ResolveColor(actions);
            projectileImage.color = baseColor;
            projectileOutline.effectColor = PulseForgeUITheme.DarkText;
            actionBadge.color = baseColor;
            actionBadge.rectTransform.sizeDelta = new Vector2(34f, 24f);
            actionLabel.text = RadialEncounterView.ResolveActionLabel(actions);
            cachedChoiceActions = RhythmActionMask.None;
            cachedSelectedAction = null;
            cachedChoiceFailure = false;
            cueCanvasGroup.alpha = 1f;
        }

        public void ApplyCuePriority(
            RadialCuePriority priority,
            RadialReadabilityMode readabilityMode)
        {
            bool focused = priority == RadialCuePriority.Focused;
            cueCanvasGroup.alpha = focused
                ? 1f
                : readabilityMode == RadialReadabilityMode.Assisted
                    ? 0.48f
                    : readabilityMode == RadialReadabilityMode.HighClarity ? 0.62f : 0.76f;
            viewRoot.localScale = Vector3.one * (focused ? 1.08f : 1f);
            float iconScale = readabilityMode == RadialReadabilityMode.HighClarity
                ? 1.30f
                : readabilityMode == RadialReadabilityMode.Assisted ? 1.16f : 1f;
            actionBadge.rectTransform.localScale = Vector3.one * iconScale;
        }

        public void Render(
            Vector2 position,
            RadialDirection direction,
            RadialPresentationResultState resultState)
        {
            viewRoot.anchoredPosition = position;
            Vector2 vector = RadialPresentationMath.DirectionVector(direction);
            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            viewRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                angle);
            actionBadge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (resultState == RadialPresentationResultState.Miss
                || resultState == RadialPresentationResultState.WrongInput)
            {
                projectileImage.color = PulseForgeUITheme.Miss;
                actionBadge.color = PulseForgeUITheme.Miss;
            }
        }

        public void ApplyCompoundState(
            RadialCompoundTargetState state,
            RhythmActionMask acceptedActions)
        {
            if (state.Kind != RadialCompoundTargetKind.Choice)
            {
                return;
            }

            if (cachedChoiceActions == acceptedActions
                && cachedSelectedAction == state.SelectedAction
                && cachedChoiceFailure == state.Failed)
            {
                return;
            }

            actionBadge.rectTransform.sizeDelta = new Vector2(58f, 24f);
            if (state.Failed)
            {
                actionLabel.text = "CHOICE MISS";
                actionBadge.color = PulseForgeUITheme.Miss;
            }
            else if (state.SelectedAction.HasValue)
            {
                RhythmActionMask selected = RhythmActionMaskUtility.ToMask(state.SelectedAction.Value);
                actionLabel.text = RadialEncounterView.ResolveChoiceLabel(
                    acceptedActions,
                    state.SelectedAction.Value);
                actionBadge.color = RadialEncounterView.ResolveActionColor(selected);
            }
            else
            {
                actionLabel.text = RadialEncounterView.ResolveActionPairLabel(acceptedActions, " / ");
                actionBadge.color = baseColor;
            }
            cachedChoiceActions = acceptedActions;
            cachedSelectedAction = state.SelectedAction;
            cachedChoiceFailure = state.Failed;
        }

        public void ResetView()
        {
            Key = default(RadialPresentationKey);
            IsInUse = false;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localRotation = Quaternion.identity;
                viewRoot.localScale = Vector3.one;
            }
            if (cueCanvasGroup != null)
            {
                cueCanvasGroup.alpha = 1f;
            }
            if (actionBadge != null)
            {
                actionBadge.rectTransform.localScale = Vector3.one;
            }
            if (actionLabel != null)
            {
                actionLabel.text = string.Empty;
            }
            cachedChoiceActions = RhythmActionMask.None;
            cachedSelectedAction = null;
            cachedChoiceFailure = false;
            gameObject.SetActive(false);
        }

        private static Color ResolveColor(RhythmActionMask actions)
        {
            if ((actions & RhythmActionMask.Guard) != 0) return PulseForgeUITheme.Guard;
            if ((actions & RhythmActionMask.Dodge) != 0)
            {
                return Color.Lerp(PulseForgeUITheme.Primary, new Color(0.55f, 0.38f, 0.90f), 0.58f);
            }
            if ((actions & RhythmActionMask.LightAttack) != 0) return PulseForgeUITheme.Strike;
            return PulseForgeUITheme.Perfect;
        }
    }
}
