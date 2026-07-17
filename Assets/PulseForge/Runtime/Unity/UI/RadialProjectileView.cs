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
        [SerializeField] private Image energyTrail;
        [SerializeField] private Image energyCore;
        [SerializeField] private Image noseGlow;
        [SerializeField] private Image upperFin;
        [SerializeField] private Image lowerFin;
        [SerializeField] private Image actionBadge;
        [SerializeField] private Text actionLabel;

        private Color baseColor;
        private RhythmActionMask cachedChoiceActions;
        private RhythmAction? cachedSelectedAction;
        private bool cachedChoiceFailure;
        private RadialActionBindingDisplay bindingDisplay;

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
            root.sizeDelta = new Vector2(46f, 16f);

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

            view.energyTrail = CreateLayer(
                "Energy Trail",
                root,
                new Vector2(-28f, 0f),
                new Vector2(30f, 4f),
                Quaternion.identity);
            view.energyCore = CreateLayer(
                "Projectile Core",
                root,
                Vector2.zero,
                new Vector2(31f, 4f),
                Quaternion.identity);
            view.noseGlow = CreateLayer(
                "Projectile Nose",
                root,
                new Vector2(19f, 0f),
                new Vector2(9f, 9f),
                Quaternion.Euler(0f, 0f, 45f));
            view.upperFin = CreateLayer(
                "Upper Fin",
                root,
                new Vector2(-8f, 8f),
                new Vector2(14f, 3f),
                Quaternion.Euler(0f, 0f, 24f));
            view.lowerFin = CreateLayer(
                "Lower Fin",
                root,
                new Vector2(-8f, -8f),
                new Vector2(14f, 3f),
                Quaternion.Euler(0f, 0f, -24f));

            RectTransform badge = PulseForgeUIFactory.CreateRect("Projectile Action", root);
            badge.anchorMin = new Vector2(0.5f, 0.5f);
            badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(0f, 26f);
            badge.sizeDelta = new Vector2(50f, 28f);
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
            badgeLabel.resizeTextForBestFit = true;
            badgeLabel.resizeTextMinSize = 7;
            badgeLabel.resizeTextMaxSize = 12;
            Outline labelOutline = badgeLabel.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.90f);
            labelOutline.effectDistance = new Vector2(2f, -2f);
            labelOutline.useGraphicAlpha = true;
            view.actionLabel = badgeLabel;
            view.ResetView();
            return view;
        }

        public void Activate(
            RadialPresentationKey key,
            RhythmActionMask actions,
            RadialActionBindingDisplay bindings)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
            bindingDisplay = bindings;
            baseColor = ResolveColor(actions);
            projectileImage.color = PulseForgeUITheme.SurfaceRaised;
            projectileOutline.effectColor = Color.Lerp(PulseForgeUITheme.Border, baseColor, 0.72f);
            energyTrail.color = PulseForgeUITheme.WithAlpha(baseColor, 0.34f);
            energyCore.color = baseColor;
            noseGlow.color = Color.Lerp(baseColor, PulseForgeUITheme.PrimaryText, 0.28f);
            upperFin.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.72f);
            lowerFin.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.72f);
            actionBadge.color = Color.Lerp(PulseForgeUITheme.SurfaceRaised, baseColor, 0.34f);
            actionBadge.rectTransform.sizeDelta = new Vector2(50f, 28f);
            actionLabel.text = bindingDisplay.Resolve(actions);
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
            Render(position, direction, resultState, null, 0f);
        }

        public void Render(
            Vector2 position,
            RadialDirection direction,
            RadialPresentationResultState resultState,
            RhythmAction? resolvedAction,
            float resolutionProgress)
        {
            Vector2 vector = RadialPresentationMath.DirectionVector(direction);
            float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
            float reaction = Mathf.Clamp01(resolutionProgress);
            bool successful = resultState == RadialPresentationResultState.Perfect
                || resultState == RadialPresentationResultState.Good
                || resultState == RadialPresentationResultState.Resolved;
            if (successful && resolvedAction == RhythmAction.Guard)
            {
                Vector2 tangent = new Vector2(-vector.y, vector.x);
                position += tangent * (72f * reaction) + vector * (104f * reaction);
                angle += 112f * reaction;
                cueCanvasGroup.alpha *= 1f - reaction * 0.72f;
            }
            else if (successful && resolvedAction == RhythmAction.Dodge)
            {
                position = Vector2.Lerp(position, -vector * 72f, reaction);
                cueCanvasGroup.alpha *= 1f - reaction;
            }

            viewRoot.anchoredPosition = position;
            viewRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                angle);
            actionBadge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (resultState == RadialPresentationResultState.Miss
                || resultState == RadialPresentationResultState.WrongInput)
            {
                viewRoot.anchoredPosition = Vector2.Lerp(position, Vector2.zero, reaction);
                projectileImage.color = PulseForgeUITheme.Miss;
                actionBadge.color = PulseForgeUITheme.Miss;
                energyTrail.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, 0.38f);
                energyCore.color = PulseForgeUITheme.Miss;
                noseGlow.color = PulseForgeUITheme.Miss;
                viewRoot.localScale *= 1f + Mathf.Sin(reaction * Mathf.PI) * 0.16f;
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

            actionBadge.rectTransform.sizeDelta = new Vector2(70f, 28f);
            if (state.Failed)
            {
                actionLabel.text = "CHOICE MISS";
                actionBadge.color = PulseForgeUITheme.Miss;
            }
            else if (state.SelectedAction.HasValue)
            {
                RhythmActionMask selected = RhythmActionMaskUtility.ToMask(state.SelectedAction.Value);
                actionLabel.text = bindingDisplay.ResolveChoice(
                    acceptedActions,
                    state.SelectedAction.Value);
                actionBadge.color = RadialEncounterView.ResolveActionColor(selected);
            }
            else
            {
                actionLabel.text = bindingDisplay.ResolvePair(acceptedActions, " / ");
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

        private static Image CreateLayer(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Quaternion rotation)
        {
            RectTransform rect = PulseForgeUIFactory.CreateRect(name, parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = rotation;
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
