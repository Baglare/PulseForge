using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialForecastView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image markerBody;
        [SerializeField] private Outline markerOutline;
        [SerializeField] private Text directionMarker;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text eventTypeLabel;

        public RadialPresentationKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        internal static RadialForecastView Create(RectTransform parent, int index)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(
                "Forecast Marker " + index,
                parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(72f, 72f);

            RadialForecastView view = root.gameObject.AddComponent<RadialForecastView>();
            view.viewRoot = root;
            view.canvasGroup = root.gameObject.AddComponent<CanvasGroup>();

            RectTransform body = PulseForgeUIFactory.CreateRect("Marker Body", root);
            body.anchorMin = new Vector2(0.5f, 0.5f);
            body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(52f, 52f);
            Image bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = PulseForgeUIFactory.RoundedSprite;
            bodyImage.raycastTarget = false;
            Outline outline = body.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            view.markerBody = bodyImage;
            view.markerOutline = outline;

            Text direction = PulseForgeUIFactory.CreateText(
                "Incoming Direction",
                root,
                "▼",
                16,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            direction.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            direction.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            direction.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            direction.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            direction.rectTransform.sizeDelta = new Vector2(24f, 24f);
            view.directionMarker = direction;

            Text action = PulseForgeUIFactory.CreateText(
                "Forecast Action",
                body,
                string.Empty,
                18,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            action.rectTransform.anchorMin = new Vector2(0f, 0.28f);
            action.rectTransform.anchorMax = Vector2.one;
            action.rectTransform.offsetMin = Vector2.zero;
            action.rectTransform.offsetMax = Vector2.zero;
            view.actionLabel = action;

            Text eventType = PulseForgeUIFactory.CreateText(
                "Forecast Event Type",
                body,
                string.Empty,
                14,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.LowerCenter,
                FontStyle.Bold);
            eventType.rectTransform.anchorMin = Vector2.zero;
            eventType.rectTransform.anchorMax = new Vector2(1f, 0.46f);
            eventType.rectTransform.offsetMin = Vector2.zero;
            eventType.rectTransform.offsetMax = Vector2.zero;
            view.eventTypeLabel = eventType;

            view.ResetView();
            return view;
        }

        public void Activate(
            RadialPresentationKey key,
            RadialDirection direction,
            RhythmActionMask actions,
            RadialEventType eventType)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
            Color actionColor = RadialEncounterView.ResolveActionColor(actions);
            markerBody.color = Color.Lerp(PulseForgeUITheme.SurfaceRaised, actionColor, 0.28f);
            markerOutline.effectColor = actionColor;
            actionLabel.color = actionColor;
            actionLabel.text = ResolveActionText(actions, eventType);
            eventTypeLabel.text = ResolveEventGlyph(eventType);
            directionMarker.rectTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                -45f * (int)direction);
        }

        public void Render(
            Vector2 position,
            RadialReadabilityMode readabilityMode,
            float transitionAlpha)
        {
            viewRoot.anchoredPosition = position;
            float opacity;
            float scale;
            switch (readabilityMode)
            {
                case RadialReadabilityMode.HighClarity:
                    opacity = 0.62f;
                    scale = 1.04f;
                    break;
                case RadialReadabilityMode.Assisted:
                    opacity = 0.44f;
                    scale = 0.92f;
                    break;
                default:
                    opacity = 0.34f;
                    scale = 0.84f;
                    break;
            }
            canvasGroup.alpha = opacity * Mathf.Clamp01(transitionAlpha);
            viewRoot.localScale = Vector3.one * scale;
        }

        public void ResetView()
        {
            Key = default(RadialPresentationKey);
            IsInUse = false;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localScale = Vector3.one;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (actionLabel != null)
            {
                actionLabel.text = string.Empty;
            }
            if (eventTypeLabel != null)
            {
                eventTypeLabel.text = string.Empty;
            }
            gameObject.SetActive(false);
        }

        private static string ResolveActionText(
            RhythmActionMask actions,
            RadialEventType eventType)
        {
            if (eventType == RadialEventType.Chord)
            {
                return RadialEncounterView.ResolveActionPairLabel(actions, "+");
            }
            if (eventType == RadialEventType.Choice)
            {
                return RadialEncounterView.ResolveActionPairLabel(actions, "/");
            }
            return RadialEncounterView.ResolveActionLabel(actions);
        }

        private static string ResolveEventGlyph(RadialEventType eventType)
        {
            switch (eventType)
            {
                case RadialEventType.GuardHold:
                    return "━";
                case RadialEventType.HeavyChargeRelease:
                    return "◯";
                case RadialEventType.Chord:
                    return "+";
                case RadialEventType.Choice:
                    return "/";
                case RadialEventType.OrderedSequence:
                case RadialEventType.TimedChain:
                case RadialEventType.SwarmChain:
                    return "→";
                case RadialEventType.BreakTarget:
                    return "▥";
                case RadialEventType.Sweep:
                    return "⌒";
                default:
                    return "•";
            }
        }
    }
}
