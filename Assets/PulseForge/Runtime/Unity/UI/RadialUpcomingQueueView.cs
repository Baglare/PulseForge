using System;
using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialUpcomingQueueView : MonoBehaviour
    {
        private const int MaximumCards = 5;

        [SerializeField] private RectTransform cardsRoot;
        [SerializeField] private Text[] actionTexts = Array.Empty<Text>();
        [SerializeField] private Text[] symbolTexts = Array.Empty<Text>();
        [SerializeField] private Text[] directionTexts = Array.Empty<Text>();
        [SerializeField] private Text[] timeTexts = Array.Empty<Text>();
        [SerializeField] private CanvasGroup[] cardGroups = Array.Empty<CanvasGroup>();

        private readonly List<RadialUpcomingCue> cues = new List<RadialUpcomingCue>(MaximumCards);

        public static RadialUpcomingQueueView Create(Transform parent)
        {
            RectTransform root = PulseForgeUIFactory.CreatePanel(
                "Upcoming Input Queue",
                parent,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Surface, 0.82f));
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(690f, 92f);
            root.anchoredPosition = new Vector2(0f, -148f);

            RadialUpcomingQueueView view = root.gameObject.AddComponent<RadialUpcomingQueueView>();
            view.cardsRoot = root;
            view.actionTexts = new Text[MaximumCards];
            view.symbolTexts = new Text[MaximumCards];
            view.directionTexts = new Text[MaximumCards];
            view.timeTexts = new Text[MaximumCards];
            view.cardGroups = new CanvasGroup[MaximumCards];
            for (int i = 0; i < MaximumCards; i++)
            {
                view.CreateCard(i);
            }
            return view;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            bool visible = controller != null
                && controller.UsesRadialCombatPresentation
                && controller.ShowUpcomingInputsForPresentation;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
            if (!visible)
            {
                return;
            }

            int cardLimit = CardLimit(controller.ReadabilityModeForPresentation);
            RadialUpcomingQueueBuilder.Fill(
                controller.RadialPresentationEncounters,
                cardLimit,
                cues);
            double songTimeSeconds = controller.CurrentSongTimeSeconds;
            for (int i = 0; i < MaximumCards; i++)
            {
                bool active = i < cues.Count && i < cardLimit;
                cardGroups[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                RadialUpcomingCue cue = cues[i];
                actionTexts[i].text = FormatAction(cue);
                symbolTexts[i].text = FormatEventSymbol(cue.EventType);
                directionTexts[i].text = FormatDirection(cue.Direction);
                double timeToHit = cue.TargetTimeSeconds - songTimeSeconds;
                timeTexts[i].text = timeToHit <= 0.05d
                    ? "NOW"
                    : timeToHit.ToString("0.0") + "s";
                cardGroups[i].alpha = i == 0 ? 1f : Mathf.Max(0.32f, 0.78f - (i * 0.13f));
                float scale = controller.ReadabilityModeForPresentation == RadialReadabilityMode.HighClarity
                    ? (i == 0 ? 1.10f : 1.03f)
                    : (i == 0 ? 1.04f : 1f);
                cardGroups[i].transform.localScale = Vector3.one * scale;
            }
        }

        public void ResetView()
        {
            cues.Clear();
            for (int i = 0; i < cardGroups.Length; i++)
            {
                if (cardGroups[i] != null)
                {
                    cardGroups[i].gameObject.SetActive(false);
                }
            }
        }

        private void CreateCard(int index)
        {
            RectTransform card = PulseForgeUIFactory.CreatePanel(
                "Cue " + (index + 1),
                cardsRoot,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceSoft, 0.96f));
            float width = 1f / MaximumCards;
            card.anchorMin = new Vector2(index * width, 0f);
            card.anchorMax = new Vector2((index + 1) * width, 1f);
            card.offsetMin = new Vector2(5f, 7f);
            card.offsetMax = new Vector2(-5f, -7f);
            cardGroups[index] = card.gameObject.AddComponent<CanvasGroup>();

            symbolTexts[index] = PulseForgeUIFactory.CreateText(
                "Type", card, "•", 14, PulseForgeUITheme.SecondaryText,
                TextAnchor.UpperLeft, FontStyle.Bold);
            symbolTexts[index].rectTransform.anchorMin = new Vector2(0.08f, 0.56f);
            symbolTexts[index].rectTransform.anchorMax = new Vector2(0.28f, 0.94f);
            symbolTexts[index].rectTransform.offsetMin = Vector2.zero;
            symbolTexts[index].rectTransform.offsetMax = Vector2.zero;

            actionTexts[index] = PulseForgeUIFactory.CreateText(
                "Action", card, "G", 20, PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            actionTexts[index].rectTransform.anchorMin = new Vector2(0.18f, 0.34f);
            actionTexts[index].rectTransform.anchorMax = new Vector2(0.82f, 0.95f);
            actionTexts[index].rectTransform.offsetMin = Vector2.zero;
            actionTexts[index].rectTransform.offsetMax = Vector2.zero;
            actionTexts[index].resizeTextForBestFit = true;
            actionTexts[index].resizeTextMinSize = 11;
            actionTexts[index].resizeTextMaxSize = 22;

            directionTexts[index] = PulseForgeUIFactory.CreateText(
                "Direction", card, "↑", 20, PulseForgeUITheme.Primary,
                TextAnchor.LowerLeft, FontStyle.Bold);
            directionTexts[index].rectTransform.anchorMin = new Vector2(0.08f, 0.06f);
            directionTexts[index].rectTransform.anchorMax = new Vector2(0.42f, 0.40f);
            directionTexts[index].rectTransform.offsetMin = Vector2.zero;
            directionTexts[index].rectTransform.offsetMax = Vector2.zero;

            timeTexts[index] = PulseForgeUIFactory.CreateText(
                "Time", card, "0.0s", 13, PulseForgeUITheme.SecondaryText,
                TextAnchor.LowerRight, FontStyle.Normal);
            timeTexts[index].rectTransform.anchorMin = new Vector2(0.42f, 0.06f);
            timeTexts[index].rectTransform.anchorMax = new Vector2(0.92f, 0.40f);
            timeTexts[index].rectTransform.offsetMin = Vector2.zero;
            timeTexts[index].rectTransform.offsetMax = Vector2.zero;
        }

        private static int CardLimit(RadialReadabilityMode readabilityMode)
        {
            switch (readabilityMode)
            {
                case RadialReadabilityMode.Standard:
                    return 3;
                case RadialReadabilityMode.HighClarity:
                    return 5;
                default:
                    return 4;
            }
        }

        private static string FormatAction(RadialUpcomingCue cue)
        {
            switch (cue.EventType)
            {
                case RadialEventType.GuardHold:
                    return "G HOLD";
                case RadialEventType.HeavyChargeRelease:
                    return "H HOLD/RELEASE";
                case RadialEventType.Chord:
                    return FormatActions(cue.PrimaryActions, " + ");
                case RadialEventType.Choice:
                    return FormatActions(cue.PrimaryActions, " / ");
                case RadialEventType.OrderedSequence:
                    return FormatActions(cue.PrimaryActions, string.Empty)
                        + (cue.SecondaryActions == RhythmActionMask.None
                            ? string.Empty
                            : " → " + FormatActions(cue.SecondaryActions, string.Empty));
                case RadialEventType.TimedChain:
                case RadialEventType.SwarmChain:
                case RadialEventType.BreakTarget:
                    return FormatActions(cue.PrimaryActions, string.Empty)
                        + " × " + Math.Max(1, cue.RemainingCount);
                default:
                    return FormatActions(cue.PrimaryActions, " + ");
            }
        }

        private static string FormatActions(RhythmActionMask actions, string separator)
        {
            string result = string.Empty;
            AppendAction(ref result, actions, RhythmActionMask.Guard, "G", separator);
            AppendAction(ref result, actions, RhythmActionMask.Dodge, "D", separator);
            AppendAction(ref result, actions, RhythmActionMask.LightAttack, "L", separator);
            AppendAction(ref result, actions, RhythmActionMask.HeavyAttack, "H", separator);
            return string.IsNullOrEmpty(result) ? "?" : result;
        }

        private static void AppendAction(
            ref string result,
            RhythmActionMask actions,
            RhythmActionMask action,
            string label,
            string separator)
        {
            if ((actions & action) == 0)
            {
                return;
            }
            if (!string.IsNullOrEmpty(result))
            {
                result += separator;
            }
            result += label;
        }

        private static string FormatEventSymbol(RadialEventType eventType)
        {
            switch (eventType)
            {
                case RadialEventType.GuardHold: return "—";
                case RadialEventType.HeavyChargeRelease: return "○";
                case RadialEventType.Chord: return "+";
                case RadialEventType.Choice: return "/";
                case RadialEventType.OrderedSequence:
                case RadialEventType.TimedChain: return "→";
                case RadialEventType.SwarmChain: return "×";
                case RadialEventType.BreakTarget: return "▥";
                default: return "•";
            }
        }

        private static string FormatDirection(RadialDirection direction)
        {
            switch (direction)
            {
                case RadialDirection.North: return "↑";
                case RadialDirection.NorthEast: return "↗";
                case RadialDirection.East: return "→";
                case RadialDirection.SouthEast: return "↘";
                case RadialDirection.South: return "↓";
                case RadialDirection.SouthWest: return "↙";
                case RadialDirection.West: return "←";
                default: return "↖";
            }
        }
    }
}
