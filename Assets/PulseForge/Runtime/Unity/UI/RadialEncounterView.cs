using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialEncounterView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private Image bodyImage;
        [SerializeField] private Outline bodyOutline;
        [SerializeField] private Text archetypeGlyph;
        [SerializeField] private Image actionBadge;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Text exclamationLabel;
        [SerializeField] private Text saboteurFuseLabel;
        [SerializeField] private Image saboteurPreparation;
        [SerializeField] private Image compoundProgress;
        [SerializeField] private Text compoundLabel;
        [SerializeField] private Text releaseMarker;
        [SerializeField] private Text stepLabel;
        [SerializeField] private RectTransform segmentRoot;
        [SerializeField] private Image[] segments;

        private Color actionColor;
        private Color baseBodyColor;
        private EnemyArchetype activeArchetype;
        private RadialCompoundTargetKind cachedKind = (RadialCompoundTargetKind)(-1);
        private RhythmActionMask cachedAcceptedActions;
        private RhythmAction? cachedSelectedAction;
        private int cachedStepIndex = int.MinValue;
        private int cachedStepCount = int.MinValue;
        private int cachedRepeatCount = int.MinValue;
        private int cachedRequiredRepeatCount = int.MinValue;
        private int cachedFlags = int.MinValue;

        public RadialPresentationKey Key { get; private set; }
        public bool IsInUse { get; private set; }

        internal static RadialEncounterView Create(RectTransform parent, int index)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect(
                "Encounter View " + index,
                parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(92f, 92f);

            RadialEncounterView view = root.gameObject.AddComponent<RadialEncounterView>();
            view.viewRoot = root;

            RectTransform body = PulseForgeUIFactory.CreateRect("Body", root);
            body.anchorMin = new Vector2(0.5f, 0.5f);
            body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(56f, 64f);
            Image bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.sprite = PulseForgeUIFactory.RoundedSprite;
            bodyImage.raycastTarget = false;
            Outline outline = body.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.Border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            view.bodyImage = bodyImage;
            view.bodyOutline = outline;

            Text archetype = PulseForgeUIFactory.CreateText(
                "Archetype Shape",
                body,
                string.Empty,
                22,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.archetypeGlyph = archetype;

            RectTransform badge = PulseForgeUIFactory.CreateRect("Action Badge", root);
            badge.anchorMin = new Vector2(1f, 0f);
            badge.anchorMax = new Vector2(1f, 0f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(-10f, 10f);
            badge.sizeDelta = new Vector2(32f, 32f);
            Image badgeImage = badge.gameObject.AddComponent<Image>();
            badgeImage.sprite = PulseForgeUIFactory.RoundedSprite;
            badgeImage.raycastTarget = false;
            view.actionBadge = badgeImage;
            Text badgeText = PulseForgeUIFactory.CreateText(
                "Action",
                badge,
                string.Empty,
                18,
                PulseForgeUITheme.DarkText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.actionLabel = badgeText;

            Text result = PulseForgeUIFactory.CreateText(
                "Result",
                root,
                string.Empty,
                14,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.UpperCenter,
                FontStyle.Bold);
            result.rectTransform.anchorMin = new Vector2(0f, 1f);
            result.rectTransform.anchorMax = new Vector2(1f, 1f);
            result.rectTransform.pivot = new Vector2(0.5f, 0f);
            result.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            result.rectTransform.sizeDelta = new Vector2(24f, 24f);
            view.resultLabel = result;

            Text exclamation = PulseForgeUIFactory.CreateText(
                "Ranged Telegraph",
                root,
                "!",
                34,
                PulseForgeUITheme.Perfect,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            exclamation.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            exclamation.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            exclamation.rectTransform.pivot = new Vector2(0.5f, 0f);
            exclamation.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            exclamation.rectTransform.sizeDelta = new Vector2(42f, 42f);
            view.exclamationLabel = exclamation;

            Text fuse = PulseForgeUIFactory.CreateText(
                "Saboteur Fuse",
                root,
                "FUSE",
                10,
                PulseForgeUITheme.Good,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            fuse.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            fuse.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fuse.rectTransform.pivot = new Vector2(0.5f, 0f);
            fuse.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            fuse.rectTransform.sizeDelta = new Vector2(54f, 18f);
            view.saboteurFuseLabel = fuse;

            RectTransform saboteurProgress = PulseForgeUIFactory.CreateRect(
                "Saboteur Preparation",
                root);
            saboteurProgress.anchorMin = new Vector2(0.5f, 0f);
            saboteurProgress.anchorMax = new Vector2(0.5f, 0f);
            saboteurProgress.pivot = new Vector2(0.5f, 1f);
            saboteurProgress.anchoredPosition = new Vector2(0f, -4f);
            saboteurProgress.sizeDelta = new Vector2(70f, 7f);
            Image saboteurProgressImage = saboteurProgress.gameObject.AddComponent<Image>();
            saboteurProgressImage.sprite = PulseForgeUIFactory.RoundedSprite;
            saboteurProgressImage.type = Image.Type.Filled;
            saboteurProgressImage.fillMethod = Image.FillMethod.Horizontal;
            saboteurProgressImage.fillOrigin = 0;
            saboteurProgressImage.raycastTarget = false;
            view.saboteurPreparation = saboteurProgressImage;

            RectTransform progress = PulseForgeUIFactory.CreateRect("Compound Progress", root);
            progress.anchorMin = new Vector2(0.5f, 0.5f);
            progress.anchorMax = new Vector2(0.5f, 0.5f);
            progress.pivot = new Vector2(0.5f, 0.5f);
            progress.sizeDelta = new Vector2(78f, 78f);
            Image progressImage = progress.gameObject.AddComponent<Image>();
            progressImage.sprite = PulseForgeUIFactory.RoundedSprite;
            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Radial360;
            progressImage.fillOrigin = (int)Image.Origin360.Top;
            progressImage.fillClockwise = true;
            progressImage.raycastTarget = false;
            view.compoundProgress = progressImage;
            Outline progressOutline = progress.gameObject.AddComponent<Outline>();
            progressOutline.effectColor = PulseForgeUITheme.Primary;
            progressOutline.effectDistance = new Vector2(2f, -2f);
            progressOutline.useGraphicAlpha = true;
            Text progressLabel = PulseForgeUIFactory.CreateText(
                "Compound Label",
                progress,
                string.Empty,
                11,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            view.compoundLabel = progressLabel;

            Text marker = PulseForgeUIFactory.CreateText(
                "Release Marker",
                root,
                "REL",
                10,
                PulseForgeUITheme.Perfect,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            marker.rectTransform.anchoredPosition = new Vector2(0f, 45f);
            marker.rectTransform.sizeDelta = new Vector2(38f, 18f);
            view.releaseMarker = marker;

            Text step = PulseForgeUIFactory.CreateText(
                "Step State",
                root,
                string.Empty,
                11,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            step.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            step.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            step.rectTransform.pivot = new Vector2(0.5f, 1f);
            step.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            step.rectTransform.sizeDelta = new Vector2(104f, 20f);
            view.stepLabel = step;

            RectTransform segmentRoot = PulseForgeUIFactory.CreateRect("Break Segments", root);
            segmentRoot.anchorMin = new Vector2(0.5f, 0f);
            segmentRoot.anchorMax = new Vector2(0.5f, 0f);
            segmentRoot.pivot = new Vector2(0.5f, 1f);
            segmentRoot.anchoredPosition = new Vector2(0f, -22f);
            segmentRoot.sizeDelta = new Vector2(102f, 12f);
            view.segmentRoot = segmentRoot;
            view.segments = new Image[12];
            for (int segmentIndex = 0; segmentIndex < view.segments.Length; segmentIndex++)
            {
                RectTransform segment = PulseForgeUIFactory.CreateRect(
                    "Armor Segment " + segmentIndex,
                    segmentRoot);
                segment.anchorMin = new Vector2(0f, 0.5f);
                segment.anchorMax = new Vector2(0f, 0.5f);
                segment.pivot = new Vector2(0f, 0.5f);
                segment.anchoredPosition = new Vector2(segmentIndex * 8.5f, 0f);
                segment.sizeDelta = new Vector2(6.5f, 10f);
                Image segmentImage = segment.gameObject.AddComponent<Image>();
                segmentImage.sprite = PulseForgeUIFactory.RoundedSprite;
                segmentImage.color = PulseForgeUITheme.Strike;
                segmentImage.raycastTarget = false;
                view.segments[segmentIndex] = segmentImage;
            }
            view.ResetView();
            return view;
        }

        public void Activate(
            RadialPresentationKey key,
            EnemyArchetype archetype,
            RhythmActionMask actions)
        {
            Key = key;
            IsInUse = true;
            gameObject.SetActive(true);
            activeArchetype = archetype;
            ConfigureArchetype(archetype);
            actionColor = ResolveActionColor(actions);
            actionBadge.color = actionColor;
            actionLabel.text = ResolveActionLabel(actions);
            ConfigureActionBadge(actions);
            baseBodyColor = Color.Lerp(PulseForgeUITheme.SurfaceRaised, actionColor, 0.30f);
            bodyImage.color = baseBodyColor;
            bodyOutline.effectColor = Color.Lerp(PulseForgeUITheme.Border, actionColor, 0.50f);
            resultLabel.gameObject.SetActive(true);
            resultLabel.text = string.Empty;
            exclamationLabel.gameObject.SetActive(false);
            saboteurFuseLabel.gameObject.SetActive(archetype == EnemyArchetype.Saboteur);
            saboteurPreparation.gameObject.SetActive(archetype == EnemyArchetype.Saboteur);
            ResetCompoundVisuals();
        }

        public void Render(
            Vector2 position,
            float scale,
            bool bodyVisible,
            bool exclamationVisible,
            RadialPresentationResultState resultState)
        {
            viewRoot.anchoredPosition = position;
            viewRoot.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            bodyImage.gameObject.SetActive(bodyVisible);
            actionBadge.gameObject.SetActive(bodyVisible);
            archetypeGlyph.gameObject.SetActive(bodyVisible);
            exclamationLabel.gameObject.SetActive(exclamationVisible);
            bool showSaboteur = activeArchetype == EnemyArchetype.Saboteur && bodyVisible;
            saboteurFuseLabel.gameObject.SetActive(showSaboteur);
            saboteurPreparation.gameObject.SetActive(showSaboteur);
            ApplyResult(resultState);
        }

        public void RenderSaboteur(float preparationProgress, bool failed)
        {
            if (activeArchetype != EnemyArchetype.Saboteur)
            {
                return;
            }
            saboteurPreparation.fillAmount = Mathf.Clamp01(preparationProgress);
            saboteurPreparation.color = failed
                ? PulseForgeUITheme.Miss
                : Color.Lerp(PulseForgeUITheme.Good, PulseForgeUITheme.Miss, preparationProgress);
            saboteurFuseLabel.text = failed ? "SMOKE" : "FUSE";
            saboteurFuseLabel.color = failed ? PulseForgeUITheme.Miss : PulseForgeUITheme.Good;
        }

        public void ApplyCompoundState(
            RadialCompoundTargetState state,
            RhythmActionMask acceptedActions)
        {
            actionBadge.gameObject.SetActive(state.ShowIndividualAction && bodyImage.gameObject.activeSelf);
            bool showsProgress = state.Kind == RadialCompoundTargetKind.GuardHold
                || state.Kind == RadialCompoundTargetKind.HeavyCharge;
            compoundProgress.gameObject.SetActive(showsProgress && bodyImage.gameObject.activeSelf);
            releaseMarker.gameObject.SetActive(
                state.Kind == RadialCompoundTargetKind.HeavyCharge
                && bodyImage.gameObject.activeSelf);
            segmentRoot.gameObject.SetActive(
                state.Kind == RadialCompoundTargetKind.BreakTarget
                && bodyImage.gameObject.activeSelf);

            compoundProgress.fillAmount = Mathf.Clamp01(state.Progress);
            compoundProgress.color = state.Failed
                ? PulseForgeUITheme.Miss
                : state.Held
                    ? PulseForgeUITheme.Guard
                    : PulseForgeUITheme.WithAlpha(actionColor, 0.78f);
            int stateFlags = GetTextStateFlags(state);
            bool textStateChanged = cachedKind != state.Kind
                || cachedAcceptedActions != acceptedActions
                || cachedSelectedAction != state.SelectedAction
                || cachedStepIndex != state.StepIndex
                || cachedStepCount != state.StepCount
                || cachedRepeatCount != state.RepeatCount
                || cachedRequiredRepeatCount != state.RequiredRepeatCount
                || cachedFlags != stateFlags;
            if (textStateChanged)
            {
                compoundLabel.text = state.Kind == RadialCompoundTargetKind.GuardHold
                    ? state.Held ? "HOLD" : "GUARD"
                    : state.Kind == RadialCompoundTargetKind.HeavyCharge
                        ? state.Released ? "RELEASED" : "CHARGE"
                        : string.Empty;

                if (state.Kind == RadialCompoundTargetKind.Choice)
                {
                    actionBadge.gameObject.SetActive(bodyImage.gameObject.activeSelf);
                    actionLabel.text = ResolveActionPairLabel(acceptedActions, " / ");
                    actionBadge.rectTransform.sizeDelta = new Vector2(62f, 32f);
                    if (state.SelectedAction.HasValue)
                    {
                        actionLabel.text = ResolveChoiceLabel(
                            acceptedActions,
                            state.SelectedAction.Value);
                        actionBadge.color = ResolveActionColor(
                            RhythmActionMaskUtility.ToMask(state.SelectedAction.Value));
                        stepLabel.text = "CHOSEN";
                        stepLabel.color = PulseForgeUITheme.PrimaryText;
                    }
                    else
                    {
                        stepLabel.text = "CHOOSE";
                        stepLabel.color = PulseForgeUITheme.SecondaryText;
                    }
                }
                else if (state.Kind == RadialCompoundTargetKind.Sequence
                    || state.Kind == RadialCompoundTargetKind.TimedChain
                    || state.Kind == RadialCompoundTargetKind.Swarm)
                {
                    if (state.CompletedStep)
                    {
                        stepLabel.text = "OK " + (state.StepIndex + 1) + "/" + state.StepCount;
                        stepLabel.color = PulseForgeUITheme.Perfect;
                    }
                    else if (state.Failed)
                    {
                        stepLabel.text = "MISS " + (state.StepIndex + 1) + "/" + state.StepCount;
                        stepLabel.color = PulseForgeUITheme.Miss;
                    }
                    else if (state.ActiveStep)
                    {
                        stepLabel.text = "> " + (state.StepIndex + 1) + "/" + state.StepCount;
                        stepLabel.color = actionColor;
                    }
                    else
                    {
                        stepLabel.text = (state.StepIndex + 1) + "/" + state.StepCount;
                        stepLabel.color = PulseForgeUITheme.SecondaryText;
                    }
                }
                else if (state.Kind == RadialCompoundTargetKind.BreakTarget)
                {
                    int visibleSegmentCount = Mathf.Min(segments.Length, Mathf.Max(0, state.RequiredRepeatCount));
                    for (int i = 0; i < segments.Length; i++)
                    {
                        bool visible = i < visibleSegmentCount;
                        segments[i].gameObject.SetActive(visible);
                        if (visible)
                        {
                            segments[i].color = i < state.RepeatCount
                                ? PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Miss, 0.30f)
                                : PulseForgeUITheme.Strike;
                        }
                    }
                    stepLabel.text = "ARMOR " + state.RepeatCount + "/" + state.RequiredRepeatCount
                        + (state.HasHeavyFinisher ? " + H FINISHER" : string.Empty);
                    stepLabel.color = state.Failed ? PulseForgeUITheme.Miss : PulseForgeUITheme.Strike;
                }
                else if (state.Kind == RadialCompoundTargetKind.HeavyCharge)
                {
                    stepLabel.text = state.EarlyFailure
                        ? "EARLY BREAK"
                        : state.LateFailure
                            ? "LATE BREAK"
                            : state.Released
                                ? "ARMOR BREAK"
                                : "PRESS - HOLD - RELEASE";
                    stepLabel.color = state.Failed ? PulseForgeUITheme.Miss : actionColor;
                }
                else if (state.Kind == RadialCompoundTargetKind.GuardHold)
                {
                    stepLabel.text = state.EarlyFailure ? "SHIELD BROKEN" : "SHIELD PRESSURE";
                    stepLabel.color = state.Failed ? PulseForgeUITheme.Miss : PulseForgeUITheme.Guard;
                }
                else
                {
                    stepLabel.text = string.Empty;
                }
                CacheTextState(state, acceptedActions, stateFlags);
            }

            if (state.Kind == RadialCompoundTargetKind.Sweep)
            {
                resultLabel.gameObject.SetActive(false);
            }

            if (state.Failed)
            {
                bodyImage.color = Color.Lerp(baseBodyColor, PulseForgeUITheme.Miss, 0.72f);
                bodyOutline.effectColor = PulseForgeUITheme.Miss;
                actionBadge.color = PulseForgeUITheme.Miss;
                releaseMarker.color = PulseForgeUITheme.Miss;
            }
            else if (state.Held || state.ActiveStep || state.Pressed)
            {
                bodyImage.color = Color.Lerp(baseBodyColor, actionColor, 0.58f);
                bodyOutline.effectColor = actionColor;
                releaseMarker.color = PulseForgeUITheme.Perfect;
            }
        }

        public void ResetView()
        {
            Key = default(RadialPresentationKey);
            IsInUse = false;
            if (viewRoot != null)
            {
                viewRoot.anchoredPosition = Vector2.zero;
                viewRoot.localScale = Vector3.one;
                viewRoot.localRotation = Quaternion.identity;
            }
            if (resultLabel != null)
            {
                resultLabel.text = string.Empty;
            }
            if (exclamationLabel != null)
            {
                exclamationLabel.gameObject.SetActive(false);
            }
            if (saboteurFuseLabel != null)
            {
                saboteurFuseLabel.gameObject.SetActive(false);
            }
            if (saboteurPreparation != null)
            {
                saboteurPreparation.fillAmount = 0f;
                saboteurPreparation.gameObject.SetActive(false);
            }
            ResetCompoundVisuals();
            gameObject.SetActive(false);
        }

        private void ResetCompoundVisuals()
        {
            cachedKind = (RadialCompoundTargetKind)(-1);
            cachedAcceptedActions = RhythmActionMask.None;
            cachedSelectedAction = null;
            cachedStepIndex = int.MinValue;
            cachedStepCount = int.MinValue;
            cachedRepeatCount = int.MinValue;
            cachedRequiredRepeatCount = int.MinValue;
            cachedFlags = int.MinValue;
            if (compoundProgress != null)
            {
                compoundProgress.fillAmount = 0f;
                compoundProgress.gameObject.SetActive(false);
            }
            if (compoundLabel != null)
            {
                compoundLabel.text = string.Empty;
            }
            if (releaseMarker != null)
            {
                releaseMarker.color = PulseForgeUITheme.Perfect;
                releaseMarker.gameObject.SetActive(false);
            }
            if (stepLabel != null)
            {
                stepLabel.text = string.Empty;
                stepLabel.gameObject.SetActive(true);
            }
            if (segmentRoot != null)
            {
                segmentRoot.gameObject.SetActive(false);
            }
            if (segments != null)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] != null)
                    {
                        segments[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private static int GetTextStateFlags(RadialCompoundTargetState state)
        {
            int flags = 0;
            if (state.Pressed) flags |= 1 << 0;
            if (state.Held) flags |= 1 << 1;
            if (state.Released) flags |= 1 << 2;
            if (state.ActiveStep) flags |= 1 << 3;
            if (state.CompletedStep) flags |= 1 << 4;
            if (state.Failed) flags |= 1 << 5;
            if (state.EarlyFailure) flags |= 1 << 6;
            if (state.LateFailure) flags |= 1 << 7;
            if (state.HasHeavyFinisher) flags |= 1 << 8;
            return flags;
        }

        private void CacheTextState(
            RadialCompoundTargetState state,
            RhythmActionMask acceptedActions,
            int stateFlags)
        {
            cachedKind = state.Kind;
            cachedAcceptedActions = acceptedActions;
            cachedSelectedAction = state.SelectedAction;
            cachedStepIndex = state.StepIndex;
            cachedStepCount = state.StepCount;
            cachedRepeatCount = state.RepeatCount;
            cachedRequiredRepeatCount = state.RequiredRepeatCount;
            cachedFlags = stateFlags;
        }

        private void ConfigureArchetype(EnemyArchetype archetype)
        {
            Vector2 size;
            float rotation;
            string glyph;
            switch (archetype)
            {
                case EnemyArchetype.Duelist:
                    size = new Vector2(42f, 68f);
                    rotation = -7f;
                    glyph = "/";
                    break;
                case EnemyArchetype.Brute:
                    size = new Vector2(72f, 68f);
                    rotation = 0f;
                    glyph = "B";
                    break;
                case EnemyArchetype.Raider:
                    size = new Vector2(52f, 58f);
                    rotation = 8f;
                    glyph = ">";
                    break;
                case EnemyArchetype.Armored:
                    size = new Vector2(66f, 72f);
                    rotation = 0f;
                    glyph = "#";
                    break;
                case EnemyArchetype.ArcherGunner:
                    size = new Vector2(48f, 64f);
                    rotation = 0f;
                    glyph = "+";
                    break;
                case EnemyArchetype.Swarm:
                    size = new Vector2(38f, 38f);
                    rotation = 45f;
                    glyph = "S";
                    break;
                case EnemyArchetype.GiantBreaker:
                    size = new Vector2(82f, 82f);
                    rotation = 0f;
                    glyph = "X";
                    break;
                case EnemyArchetype.Saboteur:
                    size = new Vector2(58f, 58f);
                    rotation = 45f;
                    glyph = "O~";
                    break;
                default:
                    size = new Vector2(54f, 62f);
                    rotation = 45f;
                    glyph = "!";
                    break;
            }

            bodyImage.rectTransform.sizeDelta = size;
            bodyImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            archetypeGlyph.text = glyph;
        }

        private void ApplyResult(RadialPresentationResultState state)
        {
            switch (state)
            {
                case RadialPresentationResultState.Perfect:
                    resultLabel.text = "PERFECT";
                    resultLabel.color = PulseForgeUITheme.Perfect;
                    break;
                case RadialPresentationResultState.Good:
                    resultLabel.text = "GOOD";
                    resultLabel.color = PulseForgeUITheme.Good;
                    break;
                case RadialPresentationResultState.Miss:
                    resultLabel.text = "MISS";
                    resultLabel.color = PulseForgeUITheme.Miss;
                    bodyImage.color = Color.Lerp(bodyImage.color, PulseForgeUITheme.Miss, 0.68f);
                    break;
                case RadialPresentationResultState.WrongInput:
                    resultLabel.text = "WRONG";
                    resultLabel.color = PulseForgeUITheme.Miss;
                    bodyImage.color = Color.Lerp(bodyImage.color, PulseForgeUITheme.Miss, 0.68f);
                    break;
                case RadialPresentationResultState.Resolved:
                    resultLabel.text = "OK";
                    resultLabel.color = PulseForgeUITheme.PrimaryText;
                    break;
                default:
                    resultLabel.text = string.Empty;
                    break;
            }
        }

        internal static string ResolveActionLabel(RhythmActionMask actions)
        {
            if ((actions & RhythmActionMask.Guard) != 0) return "G";
            if ((actions & RhythmActionMask.Dodge) != 0) return "D";
            if ((actions & RhythmActionMask.LightAttack) != 0) return "L";
            if ((actions & RhythmActionMask.HeavyAttack) != 0) return "H";
            return "-";
        }

        internal static string ResolveActionPairLabel(
            RhythmActionMask actions,
            string separator)
        {
            string result = string.Empty;
            AppendActionLabel(ref result, actions, RhythmActionMask.Guard, "G", separator);
            AppendActionLabel(ref result, actions, RhythmActionMask.Dodge, "D", separator);
            AppendActionLabel(ref result, actions, RhythmActionMask.LightAttack, "L", separator);
            AppendActionLabel(ref result, actions, RhythmActionMask.HeavyAttack, "H", separator);
            return string.IsNullOrEmpty(result) ? "-" : result;
        }

        internal static string ResolveChoiceLabel(
            RhythmActionMask actions,
            RhythmAction selected)
        {
            const string MutedPrefix = "<color=#647080>";
            const string MutedSuffix = "</color>";
            if (actions == (RhythmActionMask.Guard | RhythmActionMask.Dodge))
            {
                return selected == RhythmAction.Guard
                    ? "G / " + MutedPrefix + "D" + MutedSuffix
                    : MutedPrefix + "G" + MutedSuffix + " / D";
            }
            return ResolveActionLabel(RhythmActionMaskUtility.ToMask(selected));
        }

        internal static Color ResolveActionColor(RhythmActionMask actions)
        {
            if ((actions & RhythmActionMask.Guard) != 0) return PulseForgeUITheme.Guard;
            if ((actions & RhythmActionMask.Dodge) != 0)
            {
                return Color.Lerp(PulseForgeUITheme.Primary, new Color(0.55f, 0.38f, 0.90f), 0.58f);
            }
            if ((actions & RhythmActionMask.LightAttack) != 0) return PulseForgeUITheme.Strike;
            if ((actions & RhythmActionMask.HeavyAttack) != 0)
            {
                return Color.Lerp(PulseForgeUITheme.Perfect, PulseForgeUITheme.Strike, 0.30f);
            }
            return PulseForgeUITheme.SecondaryText;
        }

        private void ConfigureActionBadge(RhythmActionMask actions)
        {
            actionBadge.rectTransform.localRotation = Quaternion.identity;
            actionLabel.rectTransform.localRotation = Quaternion.identity;
            if (actions == RhythmActionMask.Guard)
            {
                actionBadge.rectTransform.sizeDelta = new Vector2(32f, 34f);
            }
            else if (actions == RhythmActionMask.Dodge)
            {
                actionBadge.rectTransform.sizeDelta = new Vector2(29f, 29f);
                actionBadge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                actionLabel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            }
            else if (actions == RhythmActionMask.LightAttack)
            {
                actionBadge.rectTransform.sizeDelta = new Vector2(36f, 25f);
            }
            else if (actions == RhythmActionMask.HeavyAttack)
            {
                actionBadge.rectTransform.sizeDelta = new Vector2(38f, 34f);
            }
            else
            {
                actionBadge.rectTransform.sizeDelta = new Vector2(48f, 32f);
            }
        }

        private static void AppendActionLabel(
            ref string value,
            RhythmActionMask actions,
            RhythmActionMask action,
            string label,
            string separator)
        {
            if ((actions & action) == 0)
            {
                return;
            }
            if (!string.IsNullOrEmpty(value))
            {
                value += separator;
            }
            value += label;
        }
    }
}
