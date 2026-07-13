using System.Collections.Generic;
using PulseForge.Domain.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RhythmLaneView : MonoBehaviour
    {
        private const double LookAheadSeconds = 2.0d;
        private const double LookBehindSeconds = 0.35d;
        private const float HitZoneX = 72f;
        public const int NotesPerLane = 24;
        public const int TotalPoolSize = NotesPerLane * 2;

        [SerializeField] private RectTransform guardLaneRoot;
        [SerializeField] private RectTransform guardHitZone;
        [SerializeField] private RectTransform guardNoteContainer;
        [SerializeField] private Text guardActionLabel;
        [SerializeField] private Text guardKeyHint;
        [SerializeField] private RectTransform strikeLaneRoot;
        [SerializeField] private RectTransform strikeHitZone;
        [SerializeField] private RectTransform strikeNoteContainer;
        [SerializeField] private Text strikeActionLabel;
        [SerializeField] private Text strikeKeyHint;

        private Lane guardLane;
        private Lane strikeLane;

        public RectTransform GuardLaneRoot => guardLaneRoot;
        public RectTransform GuardHitZone => guardHitZone;
        public RectTransform GuardNoteContainer => guardNoteContainer;
        public RectTransform StrikeLaneRoot => strikeLaneRoot;
        public RectTransform StrikeHitZone => strikeHitZone;
        public RectTransform StrikeNoteContainer => strikeNoteContainer;
        public int RuntimePoolCount => guardLane == null || strikeLane == null ? 0 : TotalPoolSize;

        public static RhythmLaneView Create(RectTransform parent)
        {
            RhythmLaneView view = parent.gameObject.AddComponent<RhythmLaneView>();
            LaneReferences guard = CreateStaticLane(
                parent,
                "Guard Lane",
                "G",
                "GUARD   |   SPACE",
                PulseForgeUITheme.Guard,
                138f);
            LaneReferences strike = CreateStaticLane(
                parent,
                "Strike Lane",
                "S",
                "STRIKE   |   J",
                PulseForgeUITheme.Strike,
                30f);
            view.Configure(guard, strike);
            return view;
        }

        public void InitializeRuntimePool()
        {
            if (guardLane != null && strikeLane != null)
            {
                return;
            }

            if (guardNoteContainer == null || strikeNoteContainer == null)
            {
                return;
            }

            guardLane = new Lane(guardNoteContainer, PulseForgeUITheme.Guard, "G");
            strikeLane = new Lane(strikeNoteContainer, PulseForgeUITheme.Strike, "S");
        }

        public void Refresh(IReadOnlyList<BeatEventRuntime> events, double currentTimeSeconds)
        {
            InitializeRuntimePool();
            if (guardLane == null || strikeLane == null)
            {
                return;
            }

            guardLane.ResetNotes();
            strikeLane.ResetNotes();
            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                BeatEventRuntime beatEvent = events[i];
                double deltaSeconds = beatEvent.Data.TargetTimeSeconds - currentTimeSeconds;
                if (deltaSeconds > LookAheadSeconds || deltaSeconds < -LookBehindSeconds)
                {
                    continue;
                }

                Lane lane = beatEvent.Data.Action == RhythmAction.Guard ? guardLane : strikeLane;
                lane.ShowNextNote(beatEvent, deltaSeconds);
            }
        }

        public void CollectValidationErrors(List<string> errors)
        {
            PulseForgeUIValidation.AddMissing(errors, guardLaneRoot, "Rhythm lanes: Guard Lane root is missing.");
            PulseForgeUIValidation.AddMissing(errors, guardHitZone, "Rhythm lanes: Guard Hit Zone is missing.");
            PulseForgeUIValidation.AddMissing(errors, guardNoteContainer, "Rhythm lanes: Guard Note Container is missing.");
            PulseForgeUIValidation.AddMissing(errors, guardActionLabel, "Rhythm lanes: Guard action label is missing.");
            PulseForgeUIValidation.AddMissing(errors, guardKeyHint, "Rhythm lanes: Guard key hint is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeLaneRoot, "Rhythm lanes: Strike Lane root is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeHitZone, "Rhythm lanes: Strike Hit Zone is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeNoteContainer, "Rhythm lanes: Strike Note Container is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeActionLabel, "Rhythm lanes: Strike action label is missing.");
            PulseForgeUIValidation.AddMissing(errors, strikeKeyHint, "Rhythm lanes: Strike key hint is missing.");
        }

        private void Configure(LaneReferences guard, LaneReferences strike)
        {
            guardLaneRoot = guard.Root;
            guardHitZone = guard.HitZone;
            guardNoteContainer = guard.NoteContainer;
            guardActionLabel = guard.ActionLabel;
            guardKeyHint = guard.KeyHint;
            strikeLaneRoot = strike.Root;
            strikeHitZone = strike.HitZone;
            strikeNoteContainer = strike.NoteContainer;
            strikeActionLabel = strike.ActionLabel;
            strikeKeyHint = strike.KeyHint;
        }

        private static LaneReferences CreateStaticLane(
            Transform parent,
            string name,
            string actionLabel,
            string laneLabel,
            Color accent,
            float bottom)
        {
            RectTransform lane = PulseForgeUIFactory.CreateRect(name, parent);
            lane.anchorMin = new Vector2(0f, 0f);
            lane.anchorMax = new Vector2(1f, 0f);
            lane.pivot = new Vector2(0.5f, 0f);
            lane.offsetMin = new Vector2(30f, bottom);
            lane.offsetMax = new Vector2(-30f, bottom + 92f);
            Image laneBackground = lane.gameObject.AddComponent<Image>();
            laneBackground.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.92f);

            RectTransform labelArea = PulseForgeUIFactory.CreateRect("Lane Label", lane);
            labelArea.anchorMin = new Vector2(0f, 0f);
            labelArea.anchorMax = new Vector2(0f, 1f);
            labelArea.pivot = new Vector2(0f, 0.5f);
            labelArea.offsetMin = new Vector2(18f, 10f);
            labelArea.offsetMax = new Vector2(170f, -10f);

            Text actionText = PulseForgeUIFactory.CreateText(
                "Action",
                labelArea,
                actionLabel,
                36,
                accent,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            actionText.rectTransform.anchorMax = new Vector2(0.28f, 1f);

            Text laneText = PulseForgeUIFactory.CreateText(
                "Key Hint",
                labelArea,
                laneLabel,
                16,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            laneText.rectTransform.anchorMin = new Vector2(0.30f, 0f);
            laneText.rectTransform.offsetMin = Vector2.zero;

            RectTransform noteContainer = PulseForgeUIFactory.CreateRect("Note Container", lane);
            noteContainer.anchorMin = Vector2.zero;
            noteContainer.anchorMax = Vector2.one;
            noteContainer.offsetMin = new Vector2(178f, 8f);
            noteContainer.offsetMax = new Vector2(-22f, -8f);

            RectTransform baseline = PulseForgeUIFactory.CreateRect("Baseline", noteContainer);
            baseline.anchorMin = new Vector2(0f, 0.5f);
            baseline.anchorMax = new Vector2(1f, 0.5f);
            baseline.pivot = new Vector2(0.5f, 0.5f);
            baseline.offsetMin = new Vector2(14f, -1.5f);
            baseline.offsetMax = new Vector2(-4f, 1.5f);
            Image baselineImage = baseline.gameObject.AddComponent<Image>();
            baselineImage.color = PulseForgeUITheme.Divider;

            RectTransform hitZone = PulseForgeUIFactory.CreateRect("Hit Zone", noteContainer);
            hitZone.anchorMin = new Vector2(0f, 0f);
            hitZone.anchorMax = new Vector2(0f, 1f);
            hitZone.pivot = new Vector2(0.5f, 0.5f);
            hitZone.anchoredPosition = new Vector2(HitZoneX, 0f);
            hitZone.sizeDelta = new Vector2(6f, 0f);
            Image hitZoneImage = hitZone.gameObject.AddComponent<Image>();
            hitZoneImage.color = PulseForgeUITheme.WithAlpha(accent, 0.92f);

            Text hitText = PulseForgeUIFactory.CreateText(
                "Hit Label",
                noteContainer,
                "HIT",
                14,
                accent,
                TextAnchor.UpperCenter,
                FontStyle.Bold);
            hitText.rectTransform.anchorMin = new Vector2(0f, 1f);
            hitText.rectTransform.anchorMax = new Vector2(0f, 1f);
            hitText.rectTransform.pivot = new Vector2(0.5f, 1f);
            hitText.rectTransform.anchoredPosition = new Vector2(HitZoneX, -2f);
            hitText.rectTransform.sizeDelta = new Vector2(64f, 24f);

            return new LaneReferences(lane, hitZone, noteContainer, actionText, laneText);
        }

        private readonly struct LaneReferences
        {
            public LaneReferences(RectTransform root, RectTransform hitZone, RectTransform noteContainer, Text actionLabel, Text keyHint)
            {
                Root = root;
                HitZone = hitZone;
                NoteContainer = noteContainer;
                ActionLabel = actionLabel;
                KeyHint = keyHint;
            }

            public RectTransform Root { get; }
            public RectTransform HitZone { get; }
            public RectTransform NoteContainer { get; }
            public Text ActionLabel { get; }
            public Text KeyHint { get; }
        }

        private sealed class Lane
        {
            private readonly RectTransform noteContainer;
            private readonly Color accent;
            private readonly NoteView[] notes;
            private int visibleNoteCount;

            public Lane(RectTransform noteContainer, Color accent, string actionLabel)
            {
                this.noteContainer = noteContainer;
                this.accent = accent;
                notes = new NoteView[NotesPerLane];
                for (int i = 0; i < notes.Length; i++)
                {
                    notes[i] = NoteView.GetOrCreate(noteContainer, accent, actionLabel, i);
                }
            }

            public void ResetNotes()
            {
                visibleNoteCount = 0;
                for (int i = 0; i < notes.Length; i++)
                {
                    notes[i].SetActive(false);
                }
            }

            public void ShowNextNote(BeatEventRuntime beatEvent, double deltaSeconds)
            {
                if (visibleNoteCount >= notes.Length)
                {
                    return;
                }

                float noteAreaWidth = Mathf.Max(1f, noteContainer.rect.width);
                float travelWidth = Mathf.Max(1f, noteAreaWidth - HitZoneX - 36f);
                float x = deltaSeconds >= 0d
                    ? HitZoneX + (float)(deltaSeconds / LookAheadSeconds) * travelWidth
                    : HitZoneX + (float)(deltaSeconds / LookBehindSeconds) * 42f;
                notes[visibleNoteCount++].Show(beatEvent, x, accent);
            }
        }

        private sealed class NoteView
        {
            private readonly RectTransform rectTransform;
            private readonly Image image;
            private readonly Text label;
            private readonly string pendingLabel;

            private NoteView(RectTransform rectTransform, Image image, Text label, string actionLabel)
            {
                this.rectTransform = rectTransform;
                this.image = image;
                this.label = label;
                pendingLabel = actionLabel;
                SetActive(false);
            }

            public static NoteView GetOrCreate(Transform parent, Color accent, string actionLabel, int index)
            {
                string objectName = "Pooled Note " + index;
                Transform existing = parent.Find(objectName);
                RectTransform rectTransform;
                Image image;
                Text label;
                if (existing == null)
                {
                    rectTransform = PulseForgeUIFactory.CreateRect(objectName, parent);
                    rectTransform.anchorMin = new Vector2(0f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = new Vector2(48f, 48f);
                    image = rectTransform.gameObject.AddComponent<Image>();
                    image.color = accent;
                    Outline outline = rectTransform.gameObject.AddComponent<Outline>();
                    outline.effectColor = PulseForgeUITheme.WithAlpha(Color.white, 0.42f);
                    outline.effectDistance = new Vector2(2f, -2f);
                    label = PulseForgeUIFactory.CreateText(
                        "Label", rectTransform, actionLabel, 24, Color.white,
                        TextAnchor.MiddleCenter, FontStyle.Bold);
                    PulseForgeUIFactory.Stretch(label.rectTransform);
                }
                else
                {
                    rectTransform = existing.GetComponent<RectTransform>();
                    image = existing.GetComponent<Image>();
                    label = existing.GetComponentInChildren<Text>(true);
                }

                return new NoteView(rectTransform, image, label, actionLabel);
            }

            public void SetActive(bool isActive)
            {
                rectTransform.gameObject.SetActive(isActive);
            }

            public void Show(BeatEventRuntime beatEvent, float x, Color accent)
            {
                rectTransform.gameObject.SetActive(true);
                rectTransform.anchoredPosition = new Vector2(x, 0f);
                float scale = Mathf.Lerp(0.88f, 1.12f, beatEvent.Data.Intensity);
                rectTransform.localScale = Vector3.one * scale;

                switch (beatEvent.State)
                {
                    case BeatEventState.Hit:
                        bool isPerfect = beatEvent.Result != null && beatEvent.Result.Grade == HitGrade.Perfect;
                        image.color = isPerfect ? PulseForgeUITheme.Perfect : PulseForgeUITheme.Good;
                        label.text = isPerfect ? "P" : "+";
                        label.color = new Color(0.03f, 0.05f, 0.07f, 1f);
                        break;
                    case BeatEventState.Missed:
                        image.color = PulseForgeUITheme.Miss;
                        label.text = "X";
                        label.color = Color.white;
                        break;
                    default:
                        image.color = accent;
                        label.text = pendingLabel;
                        label.color = Color.white;
                        break;
                }
            }
        }
    }
}
