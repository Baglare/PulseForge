using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIMotionController : MonoBehaviour
    {
        private const int FadeChannel = 11;
        private const int TransformChannel = 23;
        private const int DynamicChannel = 37;

        [SerializeField] private PulseForgeSceneUIRoot sceneRoot;
        [SerializeField] private PulseForgeUIMotionRunner runner;

        private readonly Dictionary<RectTransform, MotionPose> basePoses =
            new Dictionary<RectTransform, MotionPose>();
        private bool hasCurrentState;
        private PulseForgeUIState currentState;
        private PulseForgeProcessingStage lastProcessingStage = PulseForgeProcessingStage.None;
        private string lastCountdownValue = string.Empty;

        public PulseForgeUIMotionRunner Runner => runner;

        public void Configure(PulseForgeSceneUIRoot root, PulseForgeUIMotionRunner motionRunner)
        {
            sceneRoot = root;
            runner = motionRunner;
            basePoses.Clear();
            CacheKnownPoses();
        }

        public void ShowState(PulseForgeUIState state, bool enableMotion)
        {
            if (sceneRoot == null)
            {
                return;
            }

            bool shouldAnimate = enableMotion
                && Application.isPlaying
                && isActiveAndEnabled
                && runner != null;
            if (hasCurrentState && currentState == state)
            {
                if (!shouldAnimate)
                {
                    runner?.CompleteAll();
                    ApplyImmediate(state);
                }

                return;
            }

            bool hadPreviousState = hasCurrentState;
            PulseForgeUIState previousState = currentState;
            runner?.CompleteAll();
            ResetButtonInteractions();
            currentState = state;
            hasCurrentState = true;

            if (!shouldAnimate)
            {
                ApplyImmediate(state);
                return;
            }

            if (hadPreviousState
                && previousState == PulseForgeUIState.Paused
                && state == PulseForgeUIState.Playing)
            {
                AnimatePauseExit();
                return;
            }

            ApplyImmediate(state);
            AnimateEntry(state, hadPreviousState ? previousState : (PulseForgeUIState?)null);
        }

        public void RefreshDynamicMotion(PulseForgeUIState state, bool enableMotion)
        {
            bool shouldAnimate = enableMotion
                && Application.isPlaying
                && isActiveAndEnabled
                && runner != null;

            if (state == PulseForgeUIState.Processing && sceneRoot.ProcessingPanel != null)
            {
                PulseForgeProcessingStage stage = sceneRoot.ProcessingPanel.CurrentStage;
                if (stage != lastProcessingStage)
                {
                    lastProcessingStage = stage;
                    Text stageText = sceneRoot.ProcessingPanel.GetStageText(stage);
                    CanvasGroup stageGroup = stageText == null ? null : stageText.GetComponent<CanvasGroup>();
                    if (stageGroup != null)
                    {
                        if (shouldAnimate)
                        {
                            stageGroup.alpha = 0.42f;
                            runner.Fade(
                                MotionKey(stageGroup, DynamicChannel),
                                stageGroup,
                                0.42f,
                                1f,
                                PulseForgeUIMotionTokens.FastDuration,
                                PulseForgeUIMotionTokens.EaseOut);
                        }
                        else
                        {
                            stageGroup.alpha = 1f;
                        }
                    }
                }
            }
            else
            {
                lastProcessingStage = PulseForgeProcessingStage.None;
            }

            if (state == PulseForgeUIState.Countdown && sceneRoot.CountdownOverlay != null)
            {
                string value = sceneRoot.CountdownOverlay.DisplayedValue;
                if (!string.Equals(value, lastCountdownValue, StringComparison.Ordinal))
                {
                    lastCountdownValue = value;
                    RectTransform countdown = sceneRoot.CountdownOverlay.CountdownText == null
                        ? null
                        : sceneRoot.CountdownOverlay.CountdownText.rectTransform;
                    if (countdown != null)
                    {
                        MotionPose pose = GetBasePose(countdown);
                        if (shouldAnimate)
                        {
                            Vector3 popScale = pose.Scale * PulseForgeUIMotionTokens.CountdownPopScale;
                            runner.Scale(
                                MotionKey(countdown, DynamicChannel),
                                countdown,
                                popScale,
                                pose.Scale,
                                PulseForgeUIMotionTokens.NormalDuration,
                                PulseForgeUIMotionTokens.Pop);
                        }
                        else
                        {
                            runner?.ApplyScaleInstant(
                                MotionKey(countdown, DynamicChannel),
                                countdown,
                                pose.Scale);
                        }
                    }
                }
            }
            else
            {
                lastCountdownValue = string.Empty;
                ResetCountdownScale();
            }
        }

        public void CompleteCurrentState()
        {
            runner?.CompleteAll();
            if (hasCurrentState)
            {
                ApplyImmediate(currentState);
            }
        }

        public void CollectValidationErrors(List<string> errors)
        {
            if (runner == null)
            {
                errors.Add("M8B.1 motion runner is missing.");
            }
        }

        private void AnimateEntry(PulseForgeUIState state, PulseForgeUIState? previousState)
        {
            switch (state)
            {
                case PulseForgeUIState.Setup:
                    AnimateFadeSlideCard(sceneRoot.SetupPanel, "Setup Card");
                    StaggerChildren(FindRect(sceneRoot.SetupPanel, "Setup Card"), false);
                    break;
                case PulseForgeUIState.Processing:
                    AnimateFadeScaleCard(sceneRoot.ProcessingPanel, "Processing Panel Card");
                    break;
                case PulseForgeUIState.Ready:
                    AnimateFadeScaleCard(sceneRoot.ReadyPanel, "Ready Panel Card");
                    StaggerChildren(FindRect(sceneRoot.ReadyPanel, "Ready Panel Card"), true);
                    break;
                case PulseForgeUIState.Countdown:
                    PrepareTopHudForPlaying();
                    break;
                case PulseForgeUIState.Playing:
                    if (previousState != PulseForgeUIState.Paused)
                    {
                        AnimateTopHud();
                    }
                    break;
                case PulseForgeUIState.Paused:
                    AnimateOverlay(sceneRoot.PauseOverlay, "Pause Card", true, false);
                    break;
                case PulseForgeUIState.Completed:
                    AnimateOverlay(sceneRoot.ResultsPanel, "Results Panel Card", true, true);
                    break;
                case PulseForgeUIState.Error:
                    AnimateOverlay(sceneRoot.ErrorPanel, "Error Panel Card", true, false);
                    break;
            }
        }

        private void AnimateFadeSlideCard(PulseForgePanelView panel, string cardName)
        {
            RectTransform card = FindRect(panel, cardName);
            CanvasGroup group = card == null ? null : card.GetComponent<CanvasGroup>();
            if (card == null || group == null)
            {
                return;
            }

            MotionPose pose = GetBasePose(card);
            Vector2 fromPosition = pose.Position + PulseForgeUIMotionTokens.PanelSlideOffset;
            PrepareGroupForEntry(group);
            runner.FadeSlide(
                MotionKey(card, TransformChannel),
                group,
                card,
                0f,
                1f,
                fromPosition,
                pose.Position,
                PulseForgeUIMotionTokens.NormalDuration,
                PulseForgeUIMotionTokens.EaseOut,
                0f,
                () => FinishGroupEntry(group));
        }

        private void AnimateFadeScaleCard(PulseForgePanelView panel, string cardName)
        {
            RectTransform card = FindRect(panel, cardName);
            CanvasGroup group = card == null ? null : card.GetComponent<CanvasGroup>();
            if (card == null || group == null)
            {
                return;
            }

            MotionPose pose = GetBasePose(card);
            PrepareGroupForEntry(group);
            runner.FadeScale(
                MotionKey(card, TransformChannel),
                group,
                card,
                0f,
                1f,
                pose.Scale * PulseForgeUIMotionTokens.ModalStartScale,
                pose.Scale,
                PulseForgeUIMotionTokens.NormalDuration,
                PulseForgeUIMotionTokens.EaseOut,
                0f,
                () => FinishGroupEntry(group));
        }

        private void AnimateOverlay(
            PulseForgePanelView panel,
            string cardName,
            bool scaleCard,
            bool staggerChildren)
        {
            if (panel == null || panel.PanelRoot == null)
            {
                return;
            }

            CanvasGroup backdrop = panel.PanelRoot.GetComponent<CanvasGroup>();
            if (backdrop != null)
            {
                PrepareGroupForEntry(backdrop);
                runner.Fade(
                    MotionKey(backdrop, FadeChannel),
                    backdrop,
                    0f,
                    1f,
                    PulseForgeUIMotionTokens.NormalDuration,
                    PulseForgeUIMotionTokens.EaseOut,
                    0f,
                    () => FinishGroupEntry(backdrop));
            }

            RectTransform card = FindRect(panel, cardName);
            CanvasGroup cardGroup = card == null ? null : card.GetComponent<CanvasGroup>();
            if (card != null && cardGroup != null)
            {
                MotionPose pose = GetBasePose(card);
                PrepareGroupForEntry(cardGroup);
                Vector3 fromScale = scaleCard
                    ? pose.Scale * PulseForgeUIMotionTokens.ModalStartScale
                    : pose.Scale;
                runner.FadeScale(
                    MotionKey(card, TransformChannel),
                    cardGroup,
                    card,
                    0f,
                    1f,
                    fromScale,
                    pose.Scale,
                    PulseForgeUIMotionTokens.NormalDuration,
                    PulseForgeUIMotionTokens.EaseOut,
                    0f,
                    () => FinishGroupEntry(cardGroup));
                if (staggerChildren)
                {
                    StaggerChildren(card, false);
                }
            }
        }

        private void AnimateTopHud()
        {
            RectTransform topHud = FindRect(sceneRoot.GameplayHud, "Top HUD");
            CanvasGroup group = topHud == null ? null : topHud.GetComponent<CanvasGroup>();
            if (group == null)
            {
                return;
            }

            PrepareGroupForEntry(group);
            runner.Fade(
                MotionKey(group, FadeChannel),
                group,
                0f,
                1f,
                PulseForgeUIMotionTokens.FastDuration,
                PulseForgeUIMotionTokens.EaseOut,
                0f,
                () => FinishGroupEntry(group));
        }

        private void PrepareTopHudForPlaying()
        {
            RectTransform topHud = FindRect(sceneRoot.GameplayHud, "Top HUD");
            CanvasGroup group = topHud == null ? null : topHud.GetComponent<CanvasGroup>();
            if (group != null)
            {
                PrepareGroupForEntry(group);
            }
        }

        private void AnimatePauseExit()
        {
            SetPanelsForPauseExit();
            PauseMenuView pause = sceneRoot.PauseOverlay;
            if (pause == null || pause.PanelRoot == null)
            {
                return;
            }

            CanvasGroup backdrop = pause.PanelRoot.GetComponent<CanvasGroup>();
            RectTransform card = FindRect(pause, "Pause Card");
            MotionPose pose = GetBasePose(card);
            if (backdrop != null)
            {
                backdrop.interactable = false;
                backdrop.blocksRaycasts = false;
                runner.Fade(
                    MotionKey(backdrop, FadeChannel),
                    backdrop,
                    backdrop.alpha,
                    0f,
                    PulseForgeUIMotionTokens.FastDuration,
                    PulseForgeUIMotionTokens.EaseInOut,
                    0f,
                    () => pause.SetActive(false));
            }
            else
            {
                pause.SetActive(false);
            }

            if (card != null)
            {
                runner.Scale(
                    MotionKey(card, TransformChannel),
                    card,
                    card.localScale,
                    pose.Scale * PulseForgeUIMotionTokens.ModalStartScale,
                    PulseForgeUIMotionTokens.FastDuration,
                    PulseForgeUIMotionTokens.EaseInOut);
            }
        }

        private void StaggerChildren(RectTransform card, bool startButtonLast)
        {
            if (card == null)
            {
                return;
            }

            List<CanvasGroup> groups = new List<CanvasGroup>();
            CanvasGroup startGroup = null;
            for (int i = 0; i < card.childCount; i++)
            {
                Transform child = card.GetChild(i);
                CanvasGroup group = child.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    continue;
                }

                if (startButtonLast && child.gameObject.name == "Start")
                {
                    startGroup = group;
                }
                else
                {
                    groups.Add(group);
                }
            }

            if (startGroup != null)
            {
                groups.Add(startGroup);
            }

            for (int i = 0; i < groups.Count; i++)
            {
                CanvasGroup group = groups[i];
                group.alpha = 0f;
                runner.Fade(
                    MotionKey(group, DynamicChannel),
                    group,
                    0f,
                    1f,
                    PulseForgeUIMotionTokens.FastDuration,
                    PulseForgeUIMotionTokens.EaseOut,
                    0.04f + i * PulseForgeUIMotionTokens.StaggerDelay);
            }
        }

        private void ApplyImmediate(PulseForgeUIState state)
        {
            SetPanel(sceneRoot.SetupPanel, state == PulseForgeUIState.Setup);
            SetPanel(sceneRoot.ProcessingPanel, state == PulseForgeUIState.Processing);
            SetPanel(sceneRoot.ReadyPanel, state == PulseForgeUIState.Ready);
            SetPanel(sceneRoot.GameplayHud, IsGameplayState(state));
            SetPanel(sceneRoot.CountdownOverlay, state == PulseForgeUIState.Countdown);
            SetPanel(sceneRoot.PauseOverlay, state == PulseForgeUIState.Paused);
            SetPanel(sceneRoot.ResultsPanel, state == PulseForgeUIState.Completed);
            SetPanel(sceneRoot.ErrorPanel, state == PulseForgeUIState.Error);
            ResetKnownVisuals();
        }

        private void SetPanelsForPauseExit()
        {
            SetPanel(sceneRoot.SetupPanel, false);
            SetPanel(sceneRoot.ProcessingPanel, false);
            SetPanel(sceneRoot.ReadyPanel, false);
            SetPanel(sceneRoot.GameplayHud, true);
            SetPanel(sceneRoot.CountdownOverlay, false);
            SetPanel(sceneRoot.PauseOverlay, true);
            SetPanel(sceneRoot.ResultsPanel, false);
            SetPanel(sceneRoot.ErrorPanel, false);
        }

        private void ResetKnownVisuals()
        {
            ResetPanel(sceneRoot.SetupPanel, "Setup Card");
            ResetPanel(sceneRoot.ProcessingPanel, "Processing Panel Card");
            ResetPanel(sceneRoot.ReadyPanel, "Ready Panel Card");
            ResetPanel(sceneRoot.PauseOverlay, "Pause Card");
            ResetPanel(sceneRoot.ResultsPanel, "Results Panel Card");
            ResetPanel(sceneRoot.ErrorPanel, "Error Panel Card");

            RectTransform topHud = FindRect(sceneRoot.GameplayHud, "Top HUD");
            ResetTarget(topHud);
            ResetCountdownScale();
        }

        private void ResetButtonInteractions()
        {
            PulseForgeUIButtonMotion[] interactions = sceneRoot.GetComponentsInChildren<PulseForgeUIButtonMotion>(true);
            for (int i = 0; i < interactions.Length; i++)
            {
                interactions[i].ResetInteraction();
            }
        }

        private void ResetPanel(PulseForgePanelView panel, string cardName)
        {
            if (panel == null)
            {
                return;
            }

            CanvasGroup rootGroup = panel.PanelRoot == null
                ? null
                : panel.PanelRoot.GetComponent<CanvasGroup>();
            ResetGroup(rootGroup);
            RectTransform card = FindRect(panel, cardName);
            ResetTarget(card);
            if (card == null)
            {
                return;
            }

            for (int i = 0; i < card.childCount; i++)
            {
                ResetGroup(card.GetChild(i).GetComponent<CanvasGroup>());
            }
        }

        private void ResetTarget(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            MotionPose pose = GetBasePose(target);
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            runner?.ApplyInstant(group, target, 1f, pose.Position, pose.Scale);
            ResetGroup(group);
        }

        private void ResetCountdownScale()
        {
            RectTransform countdown = sceneRoot == null || sceneRoot.CountdownOverlay == null
                || sceneRoot.CountdownOverlay.CountdownText == null
                    ? null
                    : sceneRoot.CountdownOverlay.CountdownText.rectTransform;
            if (countdown == null)
            {
                return;
            }

            MotionPose pose = GetBasePose(countdown);
            runner?.ApplyScaleInstant(MotionKey(countdown, DynamicChannel), countdown, pose.Scale);
        }

        private void CacheKnownPoses()
        {
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.SetupPanel, "Setup Card"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.ProcessingPanel, "Processing Panel Card"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.ReadyPanel, "Ready Panel Card"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.GameplayHud, "Top HUD"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.PauseOverlay, "Pause Card"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.ResultsPanel, "Results Panel Card"));
            CachePose(FindRect(sceneRoot == null ? null : sceneRoot.ErrorPanel, "Error Panel Card"));
            if (sceneRoot != null && sceneRoot.CountdownOverlay != null
                && sceneRoot.CountdownOverlay.CountdownText != null)
            {
                CachePose(sceneRoot.CountdownOverlay.CountdownText.rectTransform);
            }
        }

        private MotionPose GetBasePose(RectTransform target)
        {
            if (target == null)
            {
                return MotionPose.Identity;
            }

            if (!basePoses.TryGetValue(target, out MotionPose pose))
            {
                pose = new MotionPose(target.anchoredPosition, target.localScale);
                basePoses[target] = pose;
            }

            return pose;
        }

        private void CachePose(RectTransform target)
        {
            if (target != null)
            {
                basePoses[target] = new MotionPose(target.anchoredPosition, target.localScale);
            }
        }

        private static void PrepareGroupForEntry(CanvasGroup group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void FinishGroupEntry(CanvasGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private static void ResetGroup(CanvasGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private static RectTransform FindRect(PulseForgePanelView panel, string name)
        {
            if (panel == null)
            {
                return null;
            }

            RectTransform[] rects = panel.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].gameObject.name == name)
                {
                    return rects[i];
                }
            }

            return null;
        }

        private static void SetPanel(PulseForgePanelView panel, bool active)
        {
            panel?.SetActive(active);
        }

        private static bool IsGameplayState(PulseForgeUIState state)
        {
            return state == PulseForgeUIState.Countdown
                || state == PulseForgeUIState.Playing
                || state == PulseForgeUIState.Paused;
        }

        private static int MotionKey(UnityEngine.Object target, int channel)
        {
            return target == null ? channel : unchecked(target.GetInstanceID() * 397 ^ channel);
        }

        private readonly struct MotionPose
        {
            public static readonly MotionPose Identity = new MotionPose(Vector2.zero, Vector3.one);

            public MotionPose(Vector2 position, Vector3 scale)
            {
                Position = position;
                Scale = scale;
            }

            public Vector2 Position { get; }
            public Vector3 Scale { get; }
        }
    }

    public sealed class PulseForgeUIButtonMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private PulseForgeUIMotionRunner runner;

        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private bool isHovered;
        private bool isPressed;
        private bool wasInteractable;

        public void Configure(Button targetButton, PulseForgeUIMotionRunner motionRunner)
        {
            button = targetButton;
            runner = motionRunner;
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            wasInteractable = button != null && button.interactable;
        }

        public void ResetInteraction()
        {
            isHovered = false;
            isPressed = false;
            ApplyBaseScale();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (!isPressed)
            {
                AnimateTo(PulseForgeUIMotionTokens.ButtonHoverScale);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            AnimateTo(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract())
            {
                ApplyBaseScale();
                return;
            }

            isPressed = true;
            AnimateTo(PulseForgeUIMotionTokens.ButtonPressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            AnimateTo(isHovered ? PulseForgeUIMotionTokens.ButtonHoverScale : 1f);
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (runner == null)
            {
                runner = GetComponentInParent<PulseForgeUIMotionRunner>(true);
            }

            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            wasInteractable = button != null && button.interactable;
        }

        private void Update()
        {
            bool interactable = CanInteract();
            if (wasInteractable && !interactable)
            {
                isHovered = false;
                isPressed = false;
                ApplyBaseScale();
            }

            wasInteractable = interactable;
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            ApplyBaseScale();
        }

        private void OnDestroy()
        {
            if (runner != null)
            {
                runner.Cancel(GetInstanceID(), false);
            }
        }

        private void AnimateTo(float multiplier)
        {
            if (!CanInteract())
            {
                ApplyBaseScale();
                return;
            }

            if (rectTransform == null)
            {
                return;
            }

            Vector3 targetScale = baseScale * multiplier;
            PulseForgeSceneUIRoot root = GetComponentInParent<PulseForgeSceneUIRoot>(true);
            bool animate = root == null || root.EnableMotion;
            if (runner == null || !animate)
            {
                rectTransform.localScale = targetScale;
                return;
            }

            runner.Scale(
                GetInstanceID(),
                rectTransform,
                rectTransform.localScale,
                targetScale,
                PulseForgeUIMotionTokens.FastDuration,
                PulseForgeUIMotionTokens.EaseOut);
        }

        private void ApplyBaseScale()
        {
            if (rectTransform == null)
            {
                return;
            }

            if (runner != null)
            {
                runner.ApplyScaleInstant(GetInstanceID(), rectTransform, baseScale);
            }
            else
            {
                rectTransform.localScale = baseScale;
            }
        }

        private bool CanInteract()
        {
            return button != null && button.isActiveAndEnabled && button.interactable;
        }
    }

    public static class PulseForgeUIMotionSetup
    {
        public static void Apply(
            PulseForgeSceneUIRoot root,
            Func<GameObject, Type, Component> addComponent = null)
        {
            if (root == null)
            {
                return;
            }

            PulseForgeUIMotionRunner runner = GetOrAdd<PulseForgeUIMotionRunner>(root.gameObject, addComponent);
            PulseForgeUIMotionController controller = GetOrAdd<PulseForgeUIMotionController>(root.gameObject, addComponent);
            EnsurePanelTargets(root.SetupPanel, "Setup Card", true, addComponent);
            EnsurePanelTargets(root.ProcessingPanel, "Processing Panel Card", false, addComponent);
            EnsurePanelTargets(root.ReadyPanel, "Ready Panel Card", true, addComponent);
            EnsurePanelTargets(root.PauseOverlay, "Pause Card", false, addComponent);
            EnsurePanelTargets(root.ResultsPanel, "Results Panel Card", true, addComponent);
            EnsurePanelTargets(root.ErrorPanel, "Error Panel Card", false, addComponent);
            EnsurePanelRoot(root.GameplayHud, addComponent);
            EnsurePanelRoot(root.CountdownOverlay, addComponent);

            RectTransform topHud = FindRect(root.GameplayHud, "Top HUD");
            EnsureCanvasGroup(topHud == null ? null : topHud.gameObject, addComponent);

            if (root.ProcessingPanel != null)
            {
                Text[] stageTexts = root.ProcessingPanel.StageTexts;
                for (int i = 0; i < stageTexts.Length; i++)
                {
                    EnsureCanvasGroup(stageTexts[i] == null ? null : stageTexts[i].gameObject, addComponent);
                }
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                PulseForgeUIButtonMotion interaction = GetOrAdd<PulseForgeUIButtonMotion>(
                    buttons[i].gameObject,
                    addComponent);
                interaction.Configure(buttons[i], runner);
            }

            controller.Configure(root, runner);
            root.ConfigureMotion(controller);
        }

        private static void EnsurePanelTargets(
            PulseForgePanelView panel,
            string cardName,
            bool addChildGroups,
            Func<GameObject, Type, Component> addComponent)
        {
            EnsurePanelRoot(panel, addComponent);
            RectTransform card = FindRect(panel, cardName);
            EnsureCanvasGroup(card == null ? null : card.gameObject, addComponent);
            if (!addChildGroups || card == null)
            {
                return;
            }

            for (int i = 0; i < card.childCount; i++)
            {
                EnsureCanvasGroup(card.GetChild(i).gameObject, addComponent);
            }
        }

        private static void EnsurePanelRoot(
            PulseForgePanelView panel,
            Func<GameObject, Type, Component> addComponent)
        {
            EnsureCanvasGroup(panel == null ? null : panel.PanelRoot, addComponent);
        }

        private static void EnsureCanvasGroup(
            GameObject target,
            Func<GameObject, Type, Component> addComponent)
        {
            if (target != null)
            {
                GetOrAdd<CanvasGroup>(target, addComponent);
            }
        }

        private static RectTransform FindRect(PulseForgePanelView panel, string name)
        {
            if (panel == null)
            {
                return null;
            }

            RectTransform[] rects = panel.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].gameObject.name == name)
                {
                    return rects[i];
                }
            }

            return null;
        }

        private static T GetOrAdd<T>(
            GameObject target,
            Func<GameObject, Type, Component> addComponent)
            where T : Component
        {
            T existing = target.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            Component added = addComponent == null
                ? target.AddComponent<T>()
                : addComponent(target, typeof(T));
            return added as T;
        }
    }
}
