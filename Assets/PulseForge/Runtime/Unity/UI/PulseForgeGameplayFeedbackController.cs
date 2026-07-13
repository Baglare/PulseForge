using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeGameplayFeedbackController : MonoBehaviour
    {
        private const int CenterFeedbackChannel = 701;
        private const int ComboChannel = 709;
        private const int HitZoneChannel = 719;
        private const int LaneChannel = 727;

        [SerializeField] private PulseForgeSceneUIRoot sceneRoot;
        [SerializeField] private PulseForgeUIMotionRunner runner;

        private DebugRhythmPrototypeController runtimeController;
        private GameplayHUDView gameplayHud;
        private RhythmLaneView rhythmLaneView;
        private Text feedbackText;
        private CanvasGroup feedbackGroup;
        private Text comboText;
        private RectTransform comboRect;
        private Image guardHitZoneImage;
        private Image strikeHitZoneImage;
        private Image guardLaneImage;
        private Image strikeLaneImage;
        private Vector2 feedbackBasePosition;
        private Vector3 feedbackBaseScale = Vector3.one;
        private Vector3 comboBaseScale = Vector3.one;
        private Color guardHitZoneBaseColor;
        private Color strikeHitZoneBaseColor;
        private Color guardLaneBaseColor;
        private Color strikeLaneBaseColor;
        private long lastResultSequence;
        private long lastComboSequence;
        private bool isFeedbackManaged;

        public void Configure(PulseForgeSceneUIRoot root, PulseForgeUIMotionRunner motionRunner)
        {
            sceneRoot = root;
            runner = motionRunner;
            CacheTargets();
            ResetPresentation();
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (runtimeController == controller)
            {
                RefreshMotionMode();
                return;
            }

            Unbind();
            runtimeController = controller;
            lastResultSequence = 0L;
            lastComboSequence = 0L;
            CacheTargets();
            rhythmLaneView?.ResetFeedbackState(true);
            if (runtimeController != null)
            {
                runtimeController.GameplayResultResolved += HandleResultResolved;
                runtimeController.GameplayComboChanged += HandleComboChanged;
                runtimeController.GameplaySessionRestarted += HandleSessionRestarted;
                runtimeController.GameplayStateChanged += HandleStateChanged;
            }

            RefreshMotionMode();
            HandleStateChanged(runtimeController == null ? PulseForgeUIState.Setup : runtimeController.UIState);
        }

        public void Unbind()
        {
            if (runtimeController != null)
            {
                runtimeController.GameplayResultResolved -= HandleResultResolved;
                runtimeController.GameplayComboChanged -= HandleComboChanged;
                runtimeController.GameplaySessionRestarted -= HandleSessionRestarted;
                runtimeController.GameplayStateChanged -= HandleStateChanged;
            }

            runtimeController = null;
            SetFeedbackManaged(false);
            ResetPresentation();
            rhythmLaneView?.ResetFeedbackState(true);
        }

        public void CollectValidationErrors(List<string> errors)
        {
            // Runtime target references are intentionally not serialized. Rebuild the cache so
            // Edit Mode validation remains accurate after a script/domain reload.
            CacheTargets();
            PulseForgeUIValidation.AddMissing(errors, sceneRoot, "M8B.2 feedback: scene UI root is missing.");
            PulseForgeUIValidation.AddMissing(errors, runner, "M8B.2 feedback: motion runner is missing.");
            PulseForgeUIValidation.AddMissing(errors, feedbackText, "M8B.2 feedback: center feedback text is missing.");
            PulseForgeUIValidation.AddMissing(errors, feedbackGroup, "M8B.2 feedback: center feedback CanvasGroup is missing.");
            PulseForgeUIValidation.AddMissing(errors, comboText, "M8B.2 feedback: combo text is missing.");
            PulseForgeUIValidation.AddMissing(errors, rhythmLaneView, "M8B.2 feedback: rhythm lane view is missing.");
            PulseForgeUIValidation.AddMissing(errors, guardHitZoneImage, "M8B.2 feedback: Guard hit zone Image is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeHitZoneImage, "M8B.2 feedback: Strike hit zone Image is missing.");
        }

        private void Update()
        {
            RefreshMotionMode();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleResultResolved(PulseForgeGameplayResultEvent feedbackEvent)
        {
            if (feedbackEvent.SequenceId <= lastResultSequence)
            {
                return;
            }

            lastResultSequence = feedbackEvent.SequenceId;
            if (!CanAnimateGameplay())
            {
                return;
            }

            ShowCenterFeedback(feedbackEvent);
            FlashLane(feedbackEvent);
            rhythmLaneView?.PlayResultFeedback(feedbackEvent, runner);
        }

        private void HandleComboChanged(PulseForgeComboChangedEvent comboEvent)
        {
            if (comboEvent.SequenceId <= lastComboSequence)
            {
                return;
            }

            lastComboSequence = comboEvent.SequenceId;
            if (!CanAnimateGameplay() || comboRect == null)
            {
                return;
            }

            if (comboEvent.CurrentCombo > comboEvent.PreviousCombo && comboEvent.CurrentCombo > 1)
            {
                runner.Scale(
                    MotionKey(comboRect, ComboChannel),
                    comboRect,
                    comboBaseScale * PulseForgeGameplayFeedbackTokens.ComboPulseScale,
                    comboBaseScale,
                    PulseForgeGameplayFeedbackTokens.ComboPulseDuration,
                    PulseForgeUIMotionTokens.Pop);
            }
            else if (comboEvent.PreviousCombo > 1 && comboEvent.CurrentCombo == 0)
            {
                runner.Scale(
                    MotionKey(comboRect, ComboChannel),
                    comboRect,
                    comboBaseScale * PulseForgeGameplayFeedbackTokens.ComboBreakScale,
                    comboBaseScale,
                    PulseForgeGameplayFeedbackTokens.ComboPulseDuration,
                    PulseForgeUIMotionTokens.EaseOut);
            }
        }

        private void HandleSessionRestarted()
        {
            lastResultSequence = 0L;
            lastComboSequence = 0L;
            ResetPresentation();
            rhythmLaneView?.ResetFeedbackState(true);
        }

        private void HandleStateChanged(PulseForgeUIState state)
        {
            if (state != PulseForgeUIState.Playing)
            {
                ResetPresentation();
            }
        }

        private void RefreshMotionMode()
        {
            bool shouldManage = sceneRoot != null && sceneRoot.EnableMotion && runner != null;
            if (shouldManage == isFeedbackManaged)
            {
                return;
            }

            SetFeedbackManaged(shouldManage);
            ResetPresentation();
        }

        private void SetFeedbackManaged(bool value)
        {
            isFeedbackManaged = value;
            gameplayHud?.SetGameplayFeedbackManaged(value);
            rhythmLaneView?.SetGameplayFeedbackManaged(value);
        }

        private bool CanAnimateGameplay()
        {
            return isFeedbackManaged
                && runtimeController != null
                && runtimeController.UIState == PulseForgeUIState.Playing
                && runner != null;
        }

        private void ShowCenterFeedback(PulseForgeGameplayResultEvent feedbackEvent)
        {
            if (feedbackText == null || feedbackGroup == null)
            {
                return;
            }

            int key = MotionKey(feedbackText, CenterFeedbackChannel);
            runner.Cancel(key, true);
            feedbackText.gameObject.SetActive(true);
            feedbackText.text = GetFeedbackText(feedbackEvent.Action, feedbackEvent.Grade);
            feedbackText.color = GetGradeColor(feedbackEvent.Grade);
            feedbackGroup.alpha = 1f;
            feedbackText.rectTransform.anchoredPosition = feedbackBasePosition;

            float duration = GetFeedbackDuration(feedbackEvent.Grade);
            if (feedbackEvent.Grade == HitGrade.Miss)
            {
                feedbackText.rectTransform.localScale = feedbackBaseScale;
                Vector2 fromPosition = feedbackBasePosition + PulseForgeGameplayFeedbackTokens.MissFeedbackOffset;
                runner.FadeSlide(
                    key,
                    feedbackGroup,
                    feedbackText.rectTransform,
                    1f,
                    0f,
                    fromPosition,
                    feedbackBasePosition,
                    duration,
                    PulseForgeUIMotionTokens.EaseOut,
                    0f,
                    HideCenterFeedback);
                return;
            }

            float popScale = feedbackEvent.Grade == HitGrade.Perfect
                ? PulseForgeGameplayFeedbackTokens.PerfectPopScale
                : PulseForgeGameplayFeedbackTokens.GoodPopScale;
            runner.FadeScale(
                key,
                feedbackGroup,
                feedbackText.rectTransform,
                1f,
                0f,
                feedbackBaseScale * popScale,
                feedbackBaseScale,
                duration,
                PulseForgeUIMotionTokens.Pop,
                0f,
                HideCenterFeedback);
        }

        private void FlashLane(PulseForgeGameplayResultEvent feedbackEvent)
        {
            Image hitZone = feedbackEvent.Action == RhythmAction.Guard
                ? guardHitZoneImage
                : strikeHitZoneImage;
            Image lane = feedbackEvent.Action == RhythmAction.Guard
                ? guardLaneImage
                : strikeLaneImage;
            Color hitZoneBase = feedbackEvent.Action == RhythmAction.Guard
                ? guardHitZoneBaseColor
                : strikeHitZoneBaseColor;
            Color laneBase = feedbackEvent.Action == RhythmAction.Guard
                ? guardLaneBaseColor
                : strikeLaneBaseColor;
            Color actionColor = feedbackEvent.Action == RhythmAction.Guard
                ? PulseForgeUITheme.Guard
                : PulseForgeUITheme.Strike;
            Color feedbackColor = feedbackEvent.Grade == HitGrade.Miss
                ? PulseForgeUITheme.Miss
                : actionColor;
            float gradeAlpha = feedbackEvent.Grade == HitGrade.Perfect
                ? PulseForgeGameplayFeedbackTokens.PerfectFlashAlpha
                : feedbackEvent.Grade == HitGrade.Good
                    ? PulseForgeGameplayFeedbackTokens.GoodFlashAlpha
                    : PulseForgeGameplayFeedbackTokens.MissFlashAlpha;
            float intensityMultiplier = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(feedbackEvent.Intensity));

            FlashGraphic(
                hitZone,
                hitZoneBase,
                feedbackColor,
                gradeAlpha * intensityMultiplier,
                PulseForgeGameplayFeedbackTokens.HitZoneFlashDuration,
                HitZoneChannel);
            FlashGraphic(
                lane,
                laneBase,
                feedbackColor,
                PulseForgeGameplayFeedbackTokens.LanePulseAlpha * intensityMultiplier,
                PulseForgeGameplayFeedbackTokens.LanePulseDuration,
                LaneChannel);
        }

        private void FlashGraphic(
            Graphic graphic,
            Color baseColor,
            Color flashColor,
            float strength,
            float duration,
            int channel)
        {
            if (graphic == null)
            {
                return;
            }

            Color startColor = Color.Lerp(baseColor, flashColor, Mathf.Clamp01(strength));
            startColor.a = Mathf.Max(baseColor.a, Mathf.Clamp01(strength));
            runner.Tint(
                MotionKey(graphic, channel),
                graphic,
                startColor,
                baseColor,
                duration,
                PulseForgeUIMotionTokens.EaseOut);
        }

        private void ResetPresentation()
        {
            ResetCenterFeedback();
            ResetGraphic(guardHitZoneImage, guardHitZoneBaseColor, HitZoneChannel);
            ResetGraphic(strikeHitZoneImage, strikeHitZoneBaseColor, HitZoneChannel);
            ResetGraphic(guardLaneImage, guardLaneBaseColor, LaneChannel);
            ResetGraphic(strikeLaneImage, strikeLaneBaseColor, LaneChannel);
            if (comboRect != null)
            {
                if (runner != null)
                {
                    runner.ApplyScaleInstant(MotionKey(comboRect, ComboChannel), comboRect, comboBaseScale);
                }
                else
                {
                    comboRect.localScale = comboBaseScale;
                }
            }

            rhythmLaneView?.ResetFeedbackState();
        }

        private void ResetCenterFeedback()
        {
            if (feedbackText == null)
            {
                return;
            }

            runner?.Cancel(MotionKey(feedbackText, CenterFeedbackChannel), false);
            feedbackText.rectTransform.anchoredPosition = feedbackBasePosition;
            feedbackText.rectTransform.localScale = feedbackBaseScale;
            if (feedbackGroup != null)
            {
                feedbackGroup.alpha = 1f;
            }

            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }

        private void HideCenterFeedback()
        {
            if (feedbackText == null)
            {
                return;
            }

            feedbackText.rectTransform.anchoredPosition = feedbackBasePosition;
            feedbackText.rectTransform.localScale = feedbackBaseScale;
            feedbackText.gameObject.SetActive(false);
        }

        private void ResetGraphic(Graphic graphic, Color baseColor, int channel)
        {
            if (graphic == null)
            {
                return;
            }

            runner?.Cancel(MotionKey(graphic, channel), false);
            graphic.color = baseColor;
        }

        private void CacheTargets()
        {
            gameplayHud = sceneRoot == null ? null : sceneRoot.GameplayHud;
            rhythmLaneView = gameplayHud == null ? null : gameplayHud.RhythmLaneView;
            feedbackText = gameplayHud == null ? null : gameplayHud.FeedbackText;
            feedbackGroup = feedbackText == null ? null : feedbackText.GetComponent<CanvasGroup>();
            comboText = gameplayHud == null ? null : gameplayHud.ComboText;
            comboRect = comboText == null ? null : comboText.rectTransform;
            guardHitZoneImage = GetImage(rhythmLaneView == null ? null : rhythmLaneView.GuardHitZone);
            strikeHitZoneImage = GetImage(rhythmLaneView == null ? null : rhythmLaneView.StrikeHitZone);
            guardLaneImage = GetImage(rhythmLaneView == null ? null : rhythmLaneView.GuardLaneRoot);
            strikeLaneImage = GetImage(rhythmLaneView == null ? null : rhythmLaneView.StrikeLaneRoot);

            if (feedbackText != null)
            {
                feedbackBasePosition = feedbackText.rectTransform.anchoredPosition;
                feedbackBaseScale = feedbackText.rectTransform.localScale;
            }

            if (comboRect != null)
            {
                comboBaseScale = comboRect.localScale;
            }

            guardHitZoneBaseColor = GetColor(guardHitZoneImage);
            strikeHitZoneBaseColor = GetColor(strikeHitZoneImage);
            guardLaneBaseColor = GetColor(guardLaneImage);
            strikeLaneBaseColor = GetColor(strikeLaneImage);
        }

        private static Image GetImage(RectTransform target)
        {
            return target == null ? null : target.GetComponent<Image>();
        }

        private static Color GetColor(Graphic graphic)
        {
            return graphic == null ? Color.white : graphic.color;
        }

        private static string GetFeedbackText(RhythmAction action, HitGrade grade)
        {
            if (grade == HitGrade.Miss)
            {
                return "MISS";
            }

            if (action == RhythmAction.Guard)
            {
                return grade == HitGrade.Perfect ? "PERFECT PARRY" : "GOOD PARRY";
            }

            return grade == HitGrade.Perfect ? "PERFECT STRIKE" : "GOOD STRIKE";
        }

        private static Color GetGradeColor(HitGrade grade)
        {
            switch (grade)
            {
                case HitGrade.Perfect:
                    return PulseForgeUITheme.Perfect;
                case HitGrade.Good:
                    return PulseForgeUITheme.Good;
                default:
                    return PulseForgeUITheme.Miss;
            }
        }

        private static float GetFeedbackDuration(HitGrade grade)
        {
            switch (grade)
            {
                case HitGrade.Perfect:
                    return PulseForgeGameplayFeedbackTokens.PerfectDuration;
                case HitGrade.Good:
                    return PulseForgeGameplayFeedbackTokens.GoodDuration;
                default:
                    return PulseForgeGameplayFeedbackTokens.MissDuration;
            }
        }

        private static int MotionKey(UnityEngine.Object target, int channel)
        {
            return target == null ? channel : unchecked(target.GetInstanceID() * 397 ^ channel);
        }
    }

    public static class PulseForgeGameplayFeedbackSetup
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
            PulseForgeGameplayFeedbackController controller =
                GetOrAdd<PulseForgeGameplayFeedbackController>(root.gameObject, addComponent);
            GameplayHUDView hud = root.GameplayHud;
            Text feedbackText = hud == null ? null : hud.FeedbackText;
            if (feedbackText != null)
            {
                GetOrAdd<CanvasGroup>(feedbackText.gameObject, addComponent);
            }

            RhythmLaneView laneView = hud == null ? null : hud.RhythmLaneView;
            laneView?.EnsureFeedbackComponents(addComponent);
            controller.Configure(root, runner);
            root.ConfigureGameplayFeedback(controller);
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
