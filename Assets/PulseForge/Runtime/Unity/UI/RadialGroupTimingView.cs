using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public enum RadialGroupActionState
    {
        Normal,
        Highlighted,
        Muted
    }

    public enum RadialGroupProgressKind
    {
        None,
        Chord,
        Choice,
        Step,
        Chain,
        Swarm,
        BreakTarget,
        Sweep,
        HeavyFinisher
    }

    public readonly struct RadialGroupTimingContent
    {
        public RadialGroupTimingContent(
            RhythmActionMask firstAction,
            RhythmActionMask secondAction,
            string separator,
            RadialGroupActionState firstState,
            RadialGroupActionState secondState,
            RadialGroupProgressKind progressKind,
            int currentValue,
            int totalValue,
            float progress)
        {
            FirstAction = firstAction;
            SecondAction = secondAction;
            Separator = separator ?? string.Empty;
            FirstState = firstState;
            SecondState = secondState;
            ProgressKind = progressKind;
            CurrentValue = currentValue;
            TotalValue = totalValue;
            Progress = progress;
        }

        public RhythmActionMask FirstAction { get; }
        public RhythmActionMask SecondAction { get; }
        public string Separator { get; }
        public RadialGroupActionState FirstState { get; }
        public RadialGroupActionState SecondState { get; }
        public RadialGroupProgressKind ProgressKind { get; }
        public int CurrentValue { get; }
        public int TotalValue { get; }
        public float Progress { get; }
    }

    public sealed class RadialGroupTimingView : MonoBehaviour
    {
        private const float TimingTrackWidth = 176f;

        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private CanvasGroup cueCanvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private Outline outline;
        [SerializeField] private Text timingLabel;
        [SerializeField] private Image timingGoodTrack;
        [SerializeField] private Image timingPerfectZone;
        [SerializeField] private RectTransform timingNeedle;
        [SerializeField] private Image timingNeedleImage;
        [SerializeField] private RectTransform firstActionRoot;
        [SerializeField] private Image firstActionBody;
        [SerializeField] private Text firstActionLabel;
        [SerializeField] private Text separatorLabel;
        [SerializeField] private RectTransform secondActionRoot;
        [SerializeField] private Image secondActionBody;
        [SerializeField] private Text secondActionLabel;
        [SerializeField] private Text progressLabel;
        [SerializeField] private Image progressFill;

        private RadialTimingWindowState lastTimingState = RadialTimingWindowState.Waiting;
        private RhythmActionMask cachedFirstAction;
        private RhythmActionMask cachedSecondAction;
        private string cachedSeparator = string.Empty;
        private RadialGroupActionState cachedFirstState = (RadialGroupActionState)(-1);
        private RadialGroupActionState cachedSecondState = (RadialGroupActionState)(-1);
        private RadialGroupProgressKind cachedProgressKind = (RadialGroupProgressKind)(-1);
        private int cachedCurrentValue = int.MinValue;
        private int cachedTotalValue = int.MinValue;

        public RadialPresentationKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        internal static RadialGroupTimingView Create(RectTransform parent, int index)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(
                "Group Timing " + index,
                parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(226f, 86f);

            RadialGroupTimingView view = root.gameObject.AddComponent<RadialGroupTimingView>();
            view.viewRoot = root;
            view.cueCanvasGroup = root.gameObject.AddComponent<CanvasGroup>();

            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = PulseForgeUIFactory.RoundedSprite;
            background.color = PulseForgeUITheme.WithAlpha(
                PulseForgeUITheme.SurfaceRaised,
                0.97f);
            background.raycastTarget = false;
            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.Border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            view.background = background;
            view.outline = outline;

            Text timingState = PulseForgeUIFactory.CreateText(
                "Timing State",
                root,
                "WAIT",
                11,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.UpperCenter,
                FontStyle.Bold);
            timingState.rectTransform.anchorMin = new Vector2(0f, 1f);
            timingState.rectTransform.anchorMax = new Vector2(1f, 1f);
            timingState.rectTransform.pivot = new Vector2(0.5f, 1f);
            timingState.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            timingState.rectTransform.sizeDelta = new Vector2(0f, 14f);
            view.timingLabel = timingState;

            RectTransform firstAction = CreateActionBadge(
                "First Action",
                root,
                -39f,
                out Image firstActionBody,
                out Text firstActionLabel);
            view.firstActionRoot = firstAction;
            view.firstActionBody = firstActionBody;
            view.firstActionLabel = firstActionLabel;

            Text separator = PulseForgeUIFactory.CreateText(
                "Action Separator",
                root,
                string.Empty,
                18,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            separator.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            separator.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            separator.rectTransform.pivot = new Vector2(0.5f, 1f);
            separator.rectTransform.anchoredPosition = new Vector2(0f, -17f);
            separator.rectTransform.sizeDelta = new Vector2(28f, 28f);
            view.separatorLabel = separator;

            RectTransform secondAction = CreateActionBadge(
                "Second Action",
                root,
                39f,
                out Image secondActionBody,
                out Text secondActionLabel);
            view.secondActionRoot = secondAction;
            view.secondActionBody = secondActionBody;
            view.secondActionLabel = secondActionLabel;

            RectTransform goodTrack = PulseForgeUIFactory.CreateRect("Good Window", root);
            goodTrack.anchorMin = new Vector2(0.5f, 0f);
            goodTrack.anchorMax = new Vector2(0.5f, 0f);
            goodTrack.pivot = new Vector2(0.5f, 0f);
            goodTrack.anchoredPosition = new Vector2(0f, 17f);
            goodTrack.sizeDelta = new Vector2(TimingTrackWidth, 11f);
            Image goodTrackImage = goodTrack.gameObject.AddComponent<Image>();
            goodTrackImage.sprite = PulseForgeUIFactory.RoundedSprite;
            goodTrackImage.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.42f);
            goodTrackImage.raycastTarget = false;
            view.timingGoodTrack = goodTrackImage;

            RectTransform perfectZone = PulseForgeUIFactory.CreateRect("Perfect Window", goodTrack);
            perfectZone.anchorMin = new Vector2(0.5f, 0.5f);
            perfectZone.anchorMax = new Vector2(0.5f, 0.5f);
            perfectZone.pivot = new Vector2(0.5f, 0.5f);
            perfectZone.sizeDelta = new Vector2(TimingTrackWidth * 0.45f, 11f);
            Image perfectZoneImage = perfectZone.gameObject.AddComponent<Image>();
            perfectZoneImage.sprite = PulseForgeUIFactory.RoundedSprite;
            perfectZoneImage.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, 0.82f);
            perfectZoneImage.raycastTarget = false;
            view.timingPerfectZone = perfectZoneImage;

            RectTransform needle = PulseForgeUIFactory.CreateRect("Song Time Marker", goodTrack);
            needle.anchorMin = new Vector2(0.5f, 0.5f);
            needle.anchorMax = new Vector2(0.5f, 0.5f);
            needle.pivot = new Vector2(0.5f, 0.5f);
            needle.sizeDelta = new Vector2(4f, 18f);
            Image needleImage = needle.gameObject.AddComponent<Image>();
            needleImage.sprite = PulseForgeUIFactory.RoundedSprite;
            needleImage.color = PulseForgeUITheme.PrimaryText;
            needleImage.raycastTarget = false;
            view.timingNeedle = needle;
            view.timingNeedleImage = needleImage;

            Text progress = PulseForgeUIFactory.CreateText(
                "Requirement Progress",
                root,
                string.Empty,
                9,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.LowerCenter,
                FontStyle.Bold);
            progress.rectTransform.anchorMin = Vector2.zero;
            progress.rectTransform.anchorMax = new Vector2(1f, 0f);
            progress.rectTransform.pivot = new Vector2(0.5f, 0f);
            progress.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            progress.rectTransform.sizeDelta = new Vector2(0f, 13f);
            view.progressLabel = progress;

            RectTransform progressTrack = PulseForgeUIFactory.CreateRect("Progress", root);
            progressTrack.anchorMin = new Vector2(0.08f, 0f);
            progressTrack.anchorMax = new Vector2(0.92f, 0f);
            progressTrack.pivot = new Vector2(0f, 0f);
            progressTrack.anchoredPosition = new Vector2(0f, 1f);
            progressTrack.sizeDelta = new Vector2(0f, 3f);
            Image progressImage = progressTrack.gameObject.AddComponent<Image>();
            progressImage.sprite = PulseForgeUIFactory.RoundedSprite;
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Horizontal;
            progressImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressImage.color = PulseForgeUITheme.Primary;
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
            cueCanvasGroup.alpha = 1f;
            cachedFirstAction = RhythmActionMask.None;
            cachedSecondAction = RhythmActionMask.None;
            cachedSeparator = string.Empty;
            cachedFirstState = (RadialGroupActionState)(-1);
            cachedSecondState = (RadialGroupActionState)(-1);
            cachedProgressKind = (RadialGroupProgressKind)(-1);
            cachedCurrentValue = int.MinValue;
            cachedTotalValue = int.MinValue;
        }

        public void Render(
            Vector2 position,
            double songTimeSeconds,
            double targetTimeSeconds,
            double perfectWindowSeconds,
            double goodWindowSeconds,
            bool usesDeadlineWindow,
            double windowStartTimeSeconds,
            double perfectDeadlineSeconds,
            double goodDeadlineSeconds,
            RadialGroupTimingContent content,
            RadialCuePriority priority,
            RadialReadabilityMode readabilityMode)
        {
            viewRoot.anchoredPosition = position;
            RadialTimingWindowVisual timing = usesDeadlineWindow
                ? RadialPresentationMath.EvaluateDeadlineTimingWindow(
                    songTimeSeconds,
                    windowStartTimeSeconds,
                    perfectDeadlineSeconds,
                    goodDeadlineSeconds)
                : RadialPresentationMath.EvaluateTimingWindow(
                    songTimeSeconds,
                    targetTimeSeconds,
                    perfectWindowSeconds,
                    goodWindowSeconds);
            lastTimingState = timing.State;
            timingPerfectZone.rectTransform.sizeDelta = new Vector2(
                TimingTrackWidth * timing.PerfectWidth01,
                timingPerfectZone.rectTransform.sizeDelta.y);
            timingPerfectZone.rectTransform.anchoredPosition = new Vector2(
                (timing.PerfectCenter01 - 0.5f) * TimingTrackWidth,
                0f);
            timingNeedle.anchoredPosition = new Vector2(
                (timing.Position01 - 0.5f) * TimingTrackWidth,
                0f);
            ApplyTimingState(timing.State);
            RenderContent(content);
            ApplyCuePriority(priority, readabilityMode);
        }

        public void ResetView()
        {
            Key = default(RadialPresentationKey);
            IsInUse = false;
            lastTimingState = RadialTimingWindowState.Waiting;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localScale = Vector3.one;
            }
            if (cueCanvasGroup != null)
            {
                cueCanvasGroup.alpha = 1f;
            }
            if (progressFill != null)
            {
                progressFill.fillAmount = 0f;
            }
            gameObject.SetActive(false);
        }

        private void RenderContent(RadialGroupTimingContent content)
        {
            if (cachedFirstAction != content.FirstAction
                || cachedSecondAction != content.SecondAction
                || cachedSeparator != content.Separator
                || cachedFirstState != content.FirstState
                || cachedSecondState != content.SecondState)
            {
                ConfigureAction(
                    firstActionRoot,
                    firstActionBody,
                    firstActionLabel,
                    content.FirstAction,
                    content.FirstState);
                ConfigureAction(
                    secondActionRoot,
                    secondActionBody,
                    secondActionLabel,
                    content.SecondAction,
                    content.SecondState);
                bool hasSecondAction = content.SecondAction != RhythmActionMask.None;
                separatorLabel.gameObject.SetActive(hasSecondAction);
                separatorLabel.text = hasSecondAction ? content.Separator : string.Empty;
                cachedFirstAction = content.FirstAction;
                cachedSecondAction = content.SecondAction;
                cachedSeparator = content.Separator;
                cachedFirstState = content.FirstState;
                cachedSecondState = content.SecondState;
            }

            if (cachedProgressKind != content.ProgressKind
                || cachedCurrentValue != content.CurrentValue
                || cachedTotalValue != content.TotalValue)
            {
                progressLabel.text = ResolveProgressText(
                    content.ProgressKind,
                    content.CurrentValue,
                    content.TotalValue);
                cachedProgressKind = content.ProgressKind;
                cachedCurrentValue = content.CurrentValue;
                cachedTotalValue = content.TotalValue;
            }
            progressFill.fillAmount = Mathf.Clamp01(content.Progress);
        }

        private void ApplyTimingState(RadialTimingWindowState state)
        {
            switch (state)
            {
                case RadialTimingWindowState.Perfect:
                    timingLabel.text = "PERFECT!";
                    timingLabel.color = PulseForgeUITheme.Perfect;
                    timingGoodTrack.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.76f);
                    timingPerfectZone.color = PulseForgeUITheme.Perfect;
                    timingNeedleImage.color = PulseForgeUITheme.PrimaryText;
                    outline.effectColor = PulseForgeUITheme.Perfect;
                    break;
                case RadialTimingWindowState.Good:
                    timingLabel.text = "GOOD";
                    timingLabel.color = PulseForgeUITheme.Good;
                    timingGoodTrack.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.90f);
                    timingPerfectZone.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, 0.86f);
                    timingNeedleImage.color = PulseForgeUITheme.PrimaryText;
                    outline.effectColor = PulseForgeUITheme.Good;
                    break;
                case RadialTimingWindowState.Late:
                    timingLabel.text = "LATE";
                    timingLabel.color = PulseForgeUITheme.Miss;
                    timingGoodTrack.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, 0.46f);
                    timingPerfectZone.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, 0.60f);
                    timingNeedleImage.color = PulseForgeUITheme.Miss;
                    outline.effectColor = PulseForgeUITheme.Miss;
                    break;
                default:
                    timingLabel.text = "WAIT";
                    timingLabel.color = PulseForgeUITheme.SecondaryText;
                    timingGoodTrack.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.42f);
                    timingPerfectZone.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, 0.82f);
                    timingNeedleImage.color = PulseForgeUITheme.SecondaryText;
                    outline.effectColor = PulseForgeUITheme.Border;
                    break;
            }
        }

        private void ApplyCuePriority(
            RadialCuePriority priority,
            RadialReadabilityMode readabilityMode)
        {
            bool focused = priority == RadialCuePriority.Focused;
            cueCanvasGroup.alpha = focused
                ? 1f
                : readabilityMode == RadialReadabilityMode.HighClarity
                    ? 0.40f
                    : readabilityMode == RadialReadabilityMode.Assisted ? 0.48f : 0.74f;
            float readabilityScale = readabilityMode == RadialReadabilityMode.HighClarity
                ? 1.18f
                : readabilityMode == RadialReadabilityMode.Assisted ? 1.07f : 0.94f;
            float focusScale = focused
                ? readabilityMode == RadialReadabilityMode.HighClarity ? 1.12f : 1.06f
                : 1f;
            if (lastTimingState == RadialTimingWindowState.Perfect && focused)
            {
                focusScale *= 1.05f;
            }
            viewRoot.localScale = Vector3.one * readabilityScale * focusScale;
            timingLabel.gameObject.SetActive(
                readabilityMode != RadialReadabilityMode.Standard);
            int iconFontSize = readabilityMode == RadialReadabilityMode.HighClarity
                ? 20
                : readabilityMode == RadialReadabilityMode.Assisted ? 18 : 16;
            firstActionLabel.fontSize = iconFontSize;
            secondActionLabel.fontSize = iconFontSize;
            background.color = PulseForgeUITheme.WithAlpha(
                PulseForgeUITheme.SurfaceRaised,
                focused ? 0.98f : 0.90f);
        }

        private static RectTransform CreateActionBadge(
            string name,
            RectTransform parent,
            float x,
            out Image body,
            out Text label)
        {
            RectTransform badge = PulseForgeUIFactory.CreateRect(name, parent);
            badge.anchorMin = new Vector2(0.5f, 1f);
            badge.anchorMax = new Vector2(0.5f, 1f);
            badge.pivot = new Vector2(0.5f, 1f);
            badge.anchoredPosition = new Vector2(x, -17f);
            badge.sizeDelta = new Vector2(32f, 28f);
            body = badge.gameObject.AddComponent<Image>();
            body.sprite = PulseForgeUIFactory.RoundedSprite;
            body.raycastTarget = false;
            label = PulseForgeUIFactory.CreateText(
                "Label",
                badge,
                string.Empty,
                18,
                PulseForgeUITheme.DarkText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            label.raycastTarget = false;
            return badge;
        }

        private static void ConfigureAction(
            RectTransform root,
            Image body,
            Text label,
            RhythmActionMask action,
            RadialGroupActionState state)
        {
            bool visible = action != RhythmActionMask.None;
            root.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            Color actionColor = RadialEncounterView.ResolveActionColor(action);
            body.color = state == RadialGroupActionState.Muted
                ? PulseForgeUITheme.WithAlpha(actionColor, 0.28f)
                : state == RadialGroupActionState.Highlighted
                    ? Color.Lerp(actionColor, PulseForgeUITheme.PrimaryText, 0.28f)
                    : PulseForgeUITheme.WithAlpha(actionColor, 0.88f);
            label.text = RadialEncounterView.ResolveActionLabel(action);
            label.color = state == RadialGroupActionState.Muted
                ? PulseForgeUITheme.WithAlpha(PulseForgeUITheme.DarkText, 0.42f)
                : PulseForgeUITheme.DarkText;
            root.localScale = Vector3.one
                * (state == RadialGroupActionState.Highlighted ? 1.16f : 1f);
        }

        private static string ResolveProgressText(
            RadialGroupProgressKind kind,
            int current,
            int total)
        {
            switch (kind)
            {
                case RadialGroupProgressKind.Chord:
                    return current + " / " + total + " CHORD";
                case RadialGroupProgressKind.Choice:
                    return current > 0 ? "CHOSEN" : "CHOOSE ONE";
                case RadialGroupProgressKind.Step:
                    return "STEP " + current + " / " + total;
                case RadialGroupProgressKind.Chain:
                    return current + " CHAIN LEFT";
                case RadialGroupProgressKind.Swarm:
                    return current + " SWARM LEFT";
                case RadialGroupProgressKind.BreakTarget:
                    return current + " LIGHT LEFT";
                case RadialGroupProgressKind.Sweep:
                    return total + " TARGETS / 1 INPUT";
                case RadialGroupProgressKind.HeavyFinisher:
                    return "HEAVY RELEASE";
                default:
                    return string.Empty;
            }
        }
    }
}
