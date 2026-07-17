using System.Collections.Generic;
using System.Globalization;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Onboarding;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class TrainingTimingBarView : MonoBehaviour
    {
        private const double VisibleHalfRangeSeconds = 2.5d;

        [SerializeField] private Text instruction;
        [SerializeField] private RectTransform goodRange;
        [SerializeField] private RectTransform perfectRange;
        [SerializeField] private RectTransform beatCenter;
        [SerializeField] private RectTransform incomingMarker;

        public static TrainingTimingBarView Create(Transform parent)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect("Training Timing Bar", parent);
            PulseForgeUIFactory.SetLayoutHeight(root, 86f);
            TrainingTimingBarView view = root.gameObject.AddComponent<TrainingTimingBarView>();

            view.instruction = PulseForgeUIFactory.CreateText(
                "Timing Bar Instruction",
                root,
                string.Empty,
                15,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            PulseForgeUIFactory.SetTop(view.instruction.rectTransform, 24f, 0f, 0f, 0f);

            RectTransform track = PulseForgeUIFactory.CreateRect("Timing Track", root);
            track.anchorMin = new Vector2(0.04f, 0.10f);
            track.anchorMax = new Vector2(0.96f, 0.62f);
            track.offsetMin = Vector2.zero;
            track.offsetMax = Vector2.zero;
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = PulseForgeUITheme.SurfaceSoft;
            trackImage.raycastTarget = false;

            RadialTimingProfile practice = RadialTrainingTiming.TimingBarProfile;
            float goodWidth = (float)(practice.GoodWindowSeconds / VisibleHalfRangeSeconds);
            float perfectWidth = (float)(practice.PerfectWindowSeconds / VisibleHalfRangeSeconds);
            view.goodRange = CreateRange(
                "Good Range",
                track,
                goodWidth,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Good, 0.46f));
            view.perfectRange = CreateRange(
                "Perfect Range",
                track,
                perfectWidth,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Perfect, 0.82f));
            view.beatCenter = CreateMarker(
                "Beat Center",
                track,
                0.5f,
                14f,
                PulseForgeUITheme.PrimaryText);
            view.incomingMarker = CreateMarker(
                "Incoming Beat",
                track,
                1f,
                22f,
                PulseForgeUITheme.Primary);
            return view;
        }

        public void Refresh(PulseForgeExperienceCoordinator experience)
        {
            if (experience == null)
            {
                return;
            }

            bool hasCue = experience.TryGetTrainingTimingDeltaSeconds(out double deltaSeconds);
            incomingMarker.gameObject.SetActive(hasCue);
            if (!hasCue)
            {
                instruction.text = experience.TrainingTimingInstruction;
                return;
            }

            instruction.fontSize = 18;
            SetRangeWidth(
                goodRange,
                (float)(experience.TrainingGoodWindowSeconds / VisibleHalfRangeSeconds));
            SetRangeWidth(
                perfectRange,
                (float)(experience.TrainingPerfectWindowSeconds / VisibleHalfRangeSeconds));
            SetMarkerWidth(beatCenter, 14f);
            SetMarkerWidth(incomingMarker, 22f);
            double perfectWindow = experience.TrainingPerfectWindowSeconds;
            instruction.text = experience.TrainingTimingInstruction + "  •  "
                + (Mathf.Abs((float)deltaSeconds) <= perfectWindow
                    ? experience.Localize("Now") + "!"
                    : experience.Localize("BeatIn") + ": "
                        + FormatSeconds(Mathf.Max(0f, (float)deltaSeconds), experience)
                        + " s");

            float position = Mathf.Clamp01(
                0.5f + (float)(deltaSeconds / (VisibleHalfRangeSeconds * 2d)));
            SetMarkerPosition(incomingMarker, position);
        }

        private static string FormatSeconds(
            float seconds,
            PulseForgeExperienceCoordinator experience)
        {
            string value = seconds.ToString("0.0", CultureInfo.InvariantCulture);
            return experience.Language == PulseForge.Runtime.Unity.Persistence.PulseForgeUILanguage.Turkish
                ? value.Replace('.', ',')
                : value;
        }

        public void CollectValidationErrors(List<string> errors)
        {
            PulseForgeUIValidation.AddMissing(errors, instruction, "M9H Training Timing Bar: Instruction is missing.");
            PulseForgeUIValidation.AddMissing(errors, goodRange, "M9H Training Timing Bar: Good range is missing.");
            PulseForgeUIValidation.AddMissing(errors, perfectRange, "M9H Training Timing Bar: Perfect range is missing.");
            PulseForgeUIValidation.AddMissing(errors, beatCenter, "M9H Training Timing Bar: Beat center is missing.");
            PulseForgeUIValidation.AddMissing(errors, incomingMarker, "M9H Training Timing Bar: Incoming marker is missing.");
        }

        private static RectTransform CreateRange(
            string name,
            Transform parent,
            float normalizedWidth,
            Color color)
        {
            RectTransform range = PulseForgeUIFactory.CreateRect(name, parent);
            float halfWidth = normalizedWidth * 0.5f;
            range.anchorMin = new Vector2(0.5f - halfWidth, 0f);
            range.anchorMax = new Vector2(0.5f + halfWidth, 1f);
            range.offsetMin = Vector2.zero;
            range.offsetMax = Vector2.zero;
            Image image = range.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return range;
        }

        private static void SetRangeWidth(RectTransform range, float normalizedWidth)
        {
            float halfWidth = normalizedWidth * 0.5f;
            range.anchorMin = new Vector2(0.5f - halfWidth, 0f);
            range.anchorMax = new Vector2(0.5f + halfWidth, 1f);
        }

        private static RectTransform CreateMarker(
            string name,
            Transform parent,
            float normalizedPosition,
            float width,
            Color color)
        {
            RectTransform marker = PulseForgeUIFactory.CreateRect(name, parent);
            SetMarkerPosition(marker, normalizedPosition);
            marker.offsetMin = new Vector2(-width * 0.5f, -6f);
            marker.offsetMax = new Vector2(width * 0.5f, 6f);
            Image image = marker.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return marker;
        }

        private static void SetMarkerPosition(RectTransform marker, float normalizedPosition)
        {
            marker.anchorMin = new Vector2(normalizedPosition, 0f);
            marker.anchorMax = new Vector2(normalizedPosition, 1f);
        }

        private static void SetMarkerWidth(RectTransform marker, float width)
        {
            marker.offsetMin = new Vector2(-width * 0.5f, marker.offsetMin.y);
            marker.offsetMax = new Vector2(width * 0.5f, marker.offsetMax.y);
        }
    }
}
