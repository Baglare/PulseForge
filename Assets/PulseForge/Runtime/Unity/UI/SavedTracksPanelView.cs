using System;
using System.Collections.Generic;
using System.Globalization;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class SavedTracksPanelView : PulseForgePanelView
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Text emptyText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject confirmationRoot;
        [SerializeField] private Text confirmationText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private readonly List<GameObject> generatedTrackCards = new List<GameObject>();
        private DebugRhythmPrototypeController boundController;
        private Action pendingConfirmation;
        private int renderedRevision = -1;

        public static SavedTracksPanelView Create(Transform parent)
        {
            RectTransform rootRect = PulseForgeUIFactory.CreatePanel(
                "Saved Tracks Panel",
                parent,
                PulseForgeUITheme.Backdrop);
            SavedTracksPanelView view = rootRect.gameObject.AddComponent<SavedTracksPanelView>();
            view.ConfigurePanelRoot(rootRect.gameObject);

            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                "Saved Tracks Panel Card",
                rootRect,
                new Vector2(1180f, 900f),
                PulseForgeUITheme.Surface);
            Image cardImage = card.GetComponent<Image>();
            cardImage.sprite = PulseForgeUIFactory.RoundedSprite;
            cardImage.type = cardImage.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            VerticalLayoutGroup cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(38, 38, 34, 34);
            cardLayout.spacing = 12f;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            Text heading = PulseForgeUIFactory.CreateText(
                "Heading",
                card,
                "Saved Tracks",
                PulseForgeUITheme.HeadingFontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(heading, 54f);
            Text helper = PulseForgeUIFactory.CreateText(
                "Helper",
                card,
                "Load a saved setup, relink a missing file, or remove local library entries.",
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(helper, 30f);
            view.statusText = PulseForgeUIFactory.CreateText(
                "Library Status",
                card,
                string.Empty,
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.Miss,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(view.statusText, 28f);

            RectTransform scrollRoot = PulseForgeUIFactory.CreateRect("Track Scroll", card);
            PulseForgeUIFactory.SetLayoutHeight(scrollRoot, 590f);
            Image scrollBackground = scrollRoot.gameObject.AddComponent<Image>();
            scrollBackground.color = PulseForgeUITheme.BackgroundSecondary;
            scrollBackground.sprite = PulseForgeUIFactory.RoundedSprite;
            scrollBackground.type = scrollBackground.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 34f;

            RectTransform viewport = PulseForgeUIFactory.CreateStretchRect("Viewport", scrollRoot);
            PulseForgeUIFactory.Stretch(viewport, 10f, 10f, 10f, 10f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            view.contentRoot = PulseForgeUIFactory.CreateRect("Content", viewport);
            view.contentRoot.anchorMin = new Vector2(0f, 1f);
            view.contentRoot.anchorMax = new Vector2(1f, 1f);
            view.contentRoot.pivot = new Vector2(0.5f, 1f);
            view.contentRoot.offsetMin = Vector2.zero;
            view.contentRoot.offsetMax = Vector2.zero;
            VerticalLayoutGroup contentLayout = view.contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = view.contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            view.emptyText = PulseForgeUIFactory.CreateText(
                "Empty Library",
                view.contentRoot,
                "No saved tracks yet.\nEnable “Save this setup to Library” before analyzing a custom song.",
                PulseForgeUITheme.BodyFontSize,
                PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(view.emptyText, 180f);
            scrollRect.viewport = viewport;
            scrollRect.content = view.contentRoot;

            view.backButton = PulseForgeUIFactory.CreateButton(
                "Back to Setup",
                card,
                "Back to Setup",
                PulseForgeUITheme.SecondaryText);
            PulseForgeUIFactory.SetLayoutHeight(view.backButton, 54f);

            view.CreateConfirmationOverlay(rootRect);
            view.SetActive(false);
            return view;
        }

        public void EnsureCacheStatusUI(Action<GameObject> registerCreated = null)
        {
            if (statusText != null)
            {
                return;
            }

            Transform card = PanelRoot == null
                ? null
                : PanelRoot.transform.Find("Saved Tracks Panel Card");
            if (card == null)
            {
                return;
            }

            Transform existing = card.Find("Library Status");
            if (existing != null)
            {
                statusText = existing.GetComponent<Text>();
                return;
            }

            statusText = PulseForgeUIFactory.CreateText(
                "Library Status",
                card,
                string.Empty,
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.Miss,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            PulseForgeUIFactory.SetLayoutHeight(statusText, 28f);
            Transform helper = card.Find("Helper");
            if (helper != null)
            {
                statusText.transform.SetSiblingIndex(helper.GetSiblingIndex() + 1);
            }

            Transform scroll = card.Find("Track Scroll");
            if (scroll != null)
            {
                PulseForgeUIFactory.SetLayoutHeight(scroll, 590f);
            }

            registerCreated?.Invoke(statusText.gameObject);
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            Unbind();
            boundController = controller;
            if (boundController == null)
            {
                return;
            }

            PulseForgeUIFactory.BindButton(backButton, boundController.CloseSavedTracks);
            PulseForgeUIFactory.BindButton(confirmButton, ConfirmPendingAction);
            PulseForgeUIFactory.BindButton(cancelButton, CancelPendingAction);
            renderedRevision = -1;
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(backButton);
            PulseForgeUIFactory.UnbindButton(confirmButton);
            PulseForgeUIFactory.UnbindButton(cancelButton);
            pendingConfirmation = null;
            confirmationRoot?.SetActive(false);
            boundController = null;
            renderedRevision = -1;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            if (controller == null || renderedRevision == controller.SavedTrackLibraryRevision)
            {
                return;
            }

            renderedRevision = controller.SavedTrackLibraryRevision;
            if (statusText != null)
            {
                statusText.text = controller.SavedTrackLibraryMessage;
                statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusText.text));
            }

            Rebuild(controller.SavedTrackLibrary);
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, contentRoot, "Saved Tracks: content root is missing.");
            PulseForgeUIValidation.AddMissing(errors, emptyText, "Saved Tracks: empty state text is missing.");
            PulseForgeUIValidation.AddMissing(errors, statusText, "Saved Tracks: library status text is missing.");
            PulseForgeUIValidation.AddMissing(errors, backButton, "Saved Tracks: Back button is missing.");
            PulseForgeUIValidation.AddMissing(errors, confirmationRoot, "Saved Tracks: confirmation overlay is missing.");
            PulseForgeUIValidation.AddMissing(errors, confirmationText, "Saved Tracks: confirmation text is missing.");
            PulseForgeUIValidation.AddMissing(errors, confirmButton, "Saved Tracks: Confirm button is missing.");
            PulseForgeUIValidation.AddMissing(errors, cancelButton, "Saved Tracks: Cancel button is missing.");
        }

        private void Rebuild(SavedTrackLibraryData library)
        {
            for (int i = 0; i < generatedTrackCards.Count; i++)
            {
                if (generatedTrackCards[i] != null)
                {
                    generatedTrackCards[i].SetActive(false);
                    Destroy(generatedTrackCards[i]);
                }
            }

            generatedTrackCards.Clear();
            int trackCount = library == null || library.tracks == null ? 0 : library.tracks.Count;
            emptyText.gameObject.SetActive(trackCount == 0);
            for (int i = 0; i < trackCount; i++)
            {
                SavedTrackData track = library.tracks[i];
                if (track != null)
                {
                    generatedTrackCards.Add(CreateTrackCard(track).gameObject);
                }
            }
        }

        private RectTransform CreateTrackCard(SavedTrackData track)
        {
            int presetCount = track.presets == null ? 0 : track.presets.Count;
            float height = 110f + presetCount * 108f + (track.fileMissing ? 50f : 0f);
            RectTransform card = PulseForgeUIFactory.CreateRect("Track " + track.trackId, contentRoot);
            PulseForgeUIFactory.SetLayoutHeight(card, height);
            Image background = card.gameObject.AddComponent<Image>();
            background.color = PulseForgeUITheme.SurfaceRaised;
            background.sprite = PulseForgeUIFactory.RoundedSprite;
            background.type = background.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform header = PulseForgeUIFactory.CreateRect("Header", card);
            PulseForgeUIFactory.SetLayoutHeight(header, 36f);
            HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            Text title = PulseForgeUIFactory.CreateText(
                "Track Name",
                header,
                track.displayName,
                20,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            SetWidth(title, 1f, -1f);
            Button removeTrack = PulseForgeUIFactory.CreateButton(
                "Remove Track",
                header,
                "Remove Track",
                PulseForgeUITheme.Miss,
                PulseForgeUITheme.SmallFontSize);
            SetWidth(removeTrack, 0f, 136f);
            string trackId = track.trackId;
            string displayName = track.displayName;
            BindDynamicButton(
                removeTrack,
                () => ShowConfirmation(
                    "Remove “" + displayName + "” and all of its saved setups?",
                    () => boundController?.RemoveSavedTrack(trackId)));

            string metadata = FormatExtension(track.sourceExtension) + "  •  "
                + FormatDuration(track.durationSeconds) + "  •  "
                + presetCount + (presetCount == 1 ? " saved setup" : " saved setups") + "  •  Last used "
                + FormatDate(track.lastUsedAtUtc);
            bool cachedRebuildAvailable = boundController != null
                && boundController.CanRebuildSavedTrackWithoutSource(track.trackId);
            bool trackRequiresSource = TrackRequiresSource(track) && !cachedRebuildAvailable;
            Text metadataText = PulseForgeUIFactory.CreateText(
                "Metadata",
                card,
                metadata,
                PulseForgeUITheme.SmallFontSize,
                track.fileMissing && trackRequiresSource
                    ? PulseForgeUITheme.Miss
                    : PulseForgeUITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            PulseForgeUIFactory.SetLayoutHeight(metadataText, 26f);

            if (track.fileMissing)
            {
                bool requiresSource = trackRequiresSource;
                RectTransform missingRow = PulseForgeUIFactory.CreateRect("Missing File", card);
                PulseForgeUIFactory.SetLayoutHeight(missingRow, 42f);
                HorizontalLayoutGroup missingLayout = missingRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                missingLayout.spacing = 10f;
                missingLayout.childControlWidth = true;
                missingLayout.childControlHeight = true;
                missingLayout.childForceExpandWidth = false;
                missingLayout.childForceExpandHeight = true;
                Text missing = PulseForgeUIFactory.CreateText(
                    "Status",
                    missingRow,
                    requiresSource ? "Missing Source File" : "Source Missing • Cached playback available",
                    PulseForgeUITheme.SmallFontSize,
                    requiresSource ? PulseForgeUITheme.Miss : PulseForgeUITheme.SecondaryText,
                    TextAnchor.MiddleLeft,
                    FontStyle.Bold);
                SetWidth(missing, 1f, -1f);
                if (requiresSource)
                {
                    Button relink = PulseForgeUIFactory.CreateButton(
                        "Relink File",
                        missingRow,
                        "Relink File",
                        PulseForgeUITheme.SecondaryText,
                        PulseForgeUITheme.SmallFontSize);
                    SetWidth(relink, 0f, 136f);
                    BindDynamicButton(relink, () => boundController?.RelinkSavedTrack(trackId));
                }
            }

            for (int i = 0; i < presetCount; i++)
            {
                SavedTrackPresetData preset = track.presets[i];
                if (preset != null)
                {
                    CreatePresetRow(card, track, preset);
                }
            }

            return card;
        }

        private void CreatePresetRow(
            Transform parent,
            SavedTrackData track,
            SavedTrackPresetData preset)
        {
            RectTransform row = PulseForgeUIFactory.CreateRect("Preset " + preset.presetId, parent);
            PulseForgeUIFactory.SetLayoutHeight(row, 116f);
            Image background = row.gameObject.AddComponent<Image>();
            background.color = PulseForgeUITheme.SurfaceSoft;
            background.sprite = PulseForgeUIFactory.RoundedSprite;
            background.type = background.sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 10, 8, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            SavedTrackCacheStatus cacheStatus = SaveDataNormalizer.GetCacheStatus(preset);
            string cacheLabel = FormatCacheStatus(cacheStatus);
            string analyzerLabel = preset.analyzerVersion >= SaveDefaults.AnalyzerVersion
                ? "Analyzer V2"
                : "Legacy Analyzer";
            string qualityLabel = FormatPlannerResult(preset.plannerResult);
            string coverage = preset.coverage == "FullPulse" ? "Full Pulse" : preset.coverage;
            string details = "[" + preset.difficulty + "]  [" + preset.combatStyle + "]  ["
                + coverage + "]  [" + preset.detectionMode + "]\n"
                + analyzerLabel + "  •  " + preset.eventCount + " encounters  •  "
                + preset.inputCost + " inputs\n"
                + qualityLabel + "  •  " + cacheLabel
                + FormatPerformanceModes(preset);
            Text text = PulseForgeUIFactory.CreateText(
                "Details",
                row,
                details,
                PulseForgeUITheme.SmallFontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleLeft);
            SetWidth(text, 1f, -1f);

            bool canRebuildFromCache = boundController != null
                && boundController.CanRebuildSavedTrackWithoutSource(track.trackId);
            string actionLabel = cacheStatus == SavedTrackCacheStatus.Ready
                ? "Load"
                : track.fileMissing && !canRebuildFromCache ? "Relink File" : "Rebuild Cache";
            Button load = PulseForgeUIFactory.CreateButton(
                actionLabel,
                row,
                actionLabel,
                cacheStatus == SavedTrackCacheStatus.Damaged
                    ? PulseForgeUITheme.Miss
                    : PulseForgeUITheme.Primary,
                PulseForgeUITheme.SmallFontSize);
            SetWidth(load, 0f, cacheStatus == SavedTrackCacheStatus.Ready ? 92f : 158f);
            string trackId = track.trackId;
            string presetId = preset.presetId;
            if (cacheStatus == SavedTrackCacheStatus.Ready)
            {
                BindDynamicButton(load, () => boundController?.LoadSavedTrackPreset(trackId, presetId));
            }
            else if (track.fileMissing && !canRebuildFromCache)
            {
                BindDynamicButton(load, () => boundController?.RelinkSavedTrack(trackId));
            }
            else
            {
                BindDynamicButton(load, () => boundController?.RebuildSavedTrackPreset(trackId, presetId));
            }

            Button remove = PulseForgeUIFactory.CreateButton(
                "Remove",
                row,
                "Remove",
                PulseForgeUITheme.Miss,
                PulseForgeUITheme.SmallFontSize);
            SetWidth(remove, 0f, 92f);
            BindDynamicButton(
                remove,
                () => ShowConfirmation(
                    "Remove this saved setup from “" + track.displayName + "”?",
                    () => boundController?.RemoveSavedPreset(trackId, presetId)));
        }

        private static string FormatPerformanceModes(SavedTrackPresetData preset)
        {
            if (preset == null || preset.performances == null) return string.Empty;
            List<string> modes = new List<string>();
            for (int i = 0; i < preset.performances.Count; i++)
            {
                SavedTrackPerformanceData performance = preset.performances[i];
                if (performance == null
                    || Math.Max(performance.attemptCount, performance.playCount) == 0)
                {
                    continue;
                }

                string mode = string.IsNullOrWhiteSpace(performance.gameMode)
                    ? "Standard"
                    : performance.gameMode == "OneLife" ? "One Life" : performance.gameMode;
                if (!modes.Contains(mode)) modes.Add(mode);
            }
            return modes.Count == 0 ? string.Empty : "\nModes  •  " + string.Join(" / ", modes);
        }

        private void CreateConfirmationOverlay(RectTransform parent)
        {
            RectTransform overlay = PulseForgeUIFactory.CreatePanel(
                "Library Confirmation",
                parent,
                PulseForgeUITheme.Overlay);
            confirmationRoot = overlay.gameObject;
            RectTransform card = PulseForgeUIFactory.CreateCenteredCard(
                "Confirmation Card",
                overlay,
                new Vector2(560f, 250f),
                PulseForgeUITheme.SurfaceRaised);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 28, 28);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            confirmationText = PulseForgeUIFactory.CreateText(
                "Confirmation Text",
                card,
                string.Empty,
                PulseForgeUITheme.BodyFontSize,
                PulseForgeUITheme.PrimaryText,
                TextAnchor.MiddleCenter);
            PulseForgeUIFactory.SetLayoutHeight(confirmationText, 100f);
            RectTransform actions = PulseForgeUIFactory.CreateRect("Actions", card);
            PulseForgeUIFactory.SetLayoutHeight(actions, 56f);
            HorizontalLayoutGroup actionLayout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12f;
            actionLayout.childControlWidth = true;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = true;
            actionLayout.childForceExpandHeight = true;
            cancelButton = PulseForgeUIFactory.CreateButton(
                "Cancel",
                actions,
                "Cancel",
                PulseForgeUITheme.SecondaryText);
            confirmButton = PulseForgeUIFactory.CreateButton(
                "Confirm Remove",
                actions,
                "Confirm Remove",
                PulseForgeUITheme.Miss);
            confirmationRoot.SetActive(false);
        }

        private void ShowConfirmation(string message, Action confirmed)
        {
            pendingConfirmation = confirmed;
            confirmationText.text = message;
            confirmationRoot.SetActive(true);
        }

        private void ConfirmPendingAction()
        {
            Action action = pendingConfirmation;
            CancelPendingAction();
            action?.Invoke();
        }

        private void CancelPendingAction()
        {
            pendingConfirmation = null;
            confirmationRoot.SetActive(false);
        }

        private void BindDynamicButton(Button button, UnityEngine.Events.UnityAction action)
        {
            PulseForgeUIFactory.BindButton(button, action);
            PulseForgeSceneUIRoot root = GetComponentInParent<PulseForgeSceneUIRoot>(true);
            PulseForgeUIMotionRunner runner = root == null || root.MotionController == null
                ? null
                : root.MotionController.Runner;
            if (runner != null)
            {
                PulseForgeUIButtonMotion motion = button.GetComponent<PulseForgeUIButtonMotion>();
                if (motion == null)
                {
                    motion = button.gameObject.AddComponent<PulseForgeUIButtonMotion>();
                }

                motion.Configure(button, runner);
            }
        }

        private static void SetWidth(Component component, float flexibleWidth, float preferredWidth)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = component.gameObject.AddComponent<LayoutElement>();
            }

            element.flexibleWidth = flexibleWidth;
            if (preferredWidth >= 0f)
            {
                element.preferredWidth = preferredWidth;
            }
        }

        private static string FormatExtension(string extension)
        {
            return string.IsNullOrWhiteSpace(extension) ? "AUDIO" : extension.ToUpperInvariant();
        }

        private static string FormatDuration(double durationSeconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.RoundToInt((float)durationSeconds));
            return (totalSeconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":"
                + (totalSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatDate(string utcValue)
        {
            return DateTime.TryParse(
                utcValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                    ? parsed.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "never";
        }

        private static string FormatCacheStatus(SavedTrackCacheStatus status)
        {
            return status == SavedTrackCacheStatus.NeedsRebuild
                ? "Needs Rebuild"
                : status.ToString();
        }

        private static string FormatPlannerResult(string result)
        {
            if (string.Equals(result, "PassWithRepairs", StringComparison.OrdinalIgnoreCase))
            {
                return "Repaired";
            }
            if (string.Equals(result, "UnderCovered", StringComparison.OrdinalIgnoreCase))
            {
                return "Limited Coverage";
            }
            return string.Equals(result, "Pass", StringComparison.OrdinalIgnoreCase)
                ? "Pass"
                : "Needs Analysis";
        }

        private static bool TrackRequiresSource(SavedTrackData track)
        {
            if (track == null || track.presets == null)
            {
                return true;
            }

            for (int i = 0; i < track.presets.Count; i++)
            {
                if (SaveDataNormalizer.GetCacheStatus(track.presets[i])
                    != SavedTrackCacheStatus.Ready)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
