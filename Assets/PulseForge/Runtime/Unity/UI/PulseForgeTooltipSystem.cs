using System;
using System.Collections.Generic;
using PulseForge.Runtime.Unity.Persistence;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string tooltipKey;
        private PulseForgeTooltipView view;

        public void Configure(string key)
        {
            tooltipKey = key ?? string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(tooltipKey)) return;
            if (view == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                view = canvas == null
                    ? null
                    : canvas.GetComponentInChildren<PulseForgeTooltipView>(true);
            }
            view?.Show(this, tooltipKey, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            view?.Hide(this);
        }
    }

    public sealed class PulseForgeTooltipView : MonoBehaviour
    {
        private const float Width = 470f;
        private const float Height = 132f;
        [SerializeField] private RectTransform viewRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        private DebugRhythmPrototypeController controller;
        private PulseForgeTooltipTarget owner;
        private string activeKey = string.Empty;
        private Vector2 lastScreenPosition;
        private PulseForgeUILanguage renderedLanguage = (PulseForgeUILanguage)(-1);
        private Canvas ownerCanvas;
        private RectTransform ownerCanvasRect;

        internal static PulseForgeTooltipView Create(Transform parent)
        {
            RectTransform root = PulseForgeUIFactory.CreateRect("Tooltip Layer", parent);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = Vector2.zero;
            root.sizeDelta = new Vector2(Width, Height);
            Image background = root.gameObject.AddComponent<Image>();
            background.sprite = PulseForgeUIFactory.RoundedSprite;
            background.color = new Color(0.035f, 0.055f, 0.09f, 0.98f);
            background.raycastTarget = false;
            Outline outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = PulseForgeUITheme.Primary;
            outline.effectDistance = new Vector2(2f, -2f);

            PulseForgeTooltipView view = root.gameObject.AddComponent<PulseForgeTooltipView>();
            view.viewRoot = root;
            view.canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            view.canvasGroup.blocksRaycasts = false;
            view.canvasGroup.interactable = false;
            view.titleText = PulseForgeUIFactory.CreateText(
                "Title", root, string.Empty, 19, PulseForgeUITheme.Primary,
                TextAnchor.UpperLeft, FontStyle.Bold);
            PulseForgeUIFactory.SetTop(view.titleText.rectTransform, 30f, 18f, 18f, 12f);
            view.bodyText = PulseForgeUIFactory.CreateText(
                "Body", root, string.Empty, 15, PulseForgeUITheme.PrimaryText,
                TextAnchor.UpperLeft);
            PulseForgeUIFactory.Stretch(view.bodyText.rectTransform, 18f, 46f, 18f, 12f);
            view.bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            view.SetVisible(false);
            return view;
        }

        public void Bind(DebugRhythmPrototypeController value)
        {
            controller = value;
            ownerCanvas = GetComponentInParent<Canvas>();
            ownerCanvasRect = ownerCanvas == null ? null : ownerCanvas.transform as RectTransform;
            renderedLanguage = (PulseForgeUILanguage)(-1);
            RefreshCopy();
        }

        public void Unbind()
        {
            controller = null;
            owner = null;
            activeKey = string.Empty;
            SetVisible(false);
        }

        public void Show(PulseForgeTooltipTarget target, string key, Vector2 screenPosition)
        {
            owner = target;
            activeKey = key;
            lastScreenPosition = screenPosition;
            renderedLanguage = (PulseForgeUILanguage)(-1);
            RefreshCopy();
            SetVisible(!string.IsNullOrEmpty(titleText.text));
            UpdatePosition(screenPosition);
            viewRoot.SetAsLastSibling();
        }

        public void Hide(PulseForgeTooltipTarget target)
        {
            if (owner != target) return;
            owner = null;
            activeKey = string.Empty;
            SetVisible(false);
        }

        public void HideAll()
        {
            owner = null;
            activeKey = string.Empty;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (canvasGroup == null || canvasGroup.alpha <= 0f) return;
            if (owner == null || !owner.isActiveAndEnabled || !owner.gameObject.activeInHierarchy)
            {
                HideAll();
                return;
            }
            if (Mouse.current != null)
            {
                lastScreenPosition = Mouse.current.position.ReadValue();
            }
            UpdatePosition(lastScreenPosition);
            if (controller != null && renderedLanguage != controller.ActiveUILanguage)
            {
                RefreshCopy();
            }
        }

        private void RefreshCopy()
        {
            PulseForgeUILanguage language = controller == null
                ? PulseForgeUILanguage.English
                : controller.ActiveUILanguage;
            renderedLanguage = language;
            if (!PulseForgeTooltipCatalog.TryGet(activeKey, language, out string title, out string body))
            {
                title = string.Empty;
                body = string.Empty;
            }
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
        }

        private void UpdatePosition(Vector2 screenPosition)
        {
            if (ownerCanvas == null || ownerCanvasRect == null)
            {
                ownerCanvas = GetComponentInParent<Canvas>();
                ownerCanvasRect = ownerCanvas == null ? null : ownerCanvas.transform as RectTransform;
            }
            if (ownerCanvasRect == null) return;
            Camera camera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : ownerCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    ownerCanvasRect, screenPosition, camera, out Vector2 local)) return;
            Rect bounds = ownerCanvasRect.rect;
            float x = Mathf.Clamp(local.x + 18f, bounds.xMin + 8f, bounds.xMax - Width - 8f);
            float y = Mathf.Clamp(local.y - Height - 18f, bounds.yMin + 8f, bounds.yMax - Height - 8f);
            viewRoot.localPosition = new Vector3(x, y, 0f);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }

    public static class PulseForgeTooltipSetup
    {
        public static void Apply(PulseForgeSceneUIRoot root, Action<GameObject> registerCreated = null)
        {
            if (root == null || root.Canvas == null) return;
            PulseForgeTooltipView view = root.Canvas.GetComponentInChildren<PulseForgeTooltipView>(true);
            if (view == null)
            {
                view = PulseForgeTooltipView.Create(root.Canvas.transform);
                registerCreated?.Invoke(view.gameObject);
            }
            root.ConfigureTooltip(view);

            BindSelector(root.SetupPanel, "Detection");
            BindSelector(root.SetupPanel, "Difficulty");
            BindSelector(root.SetupPanel, "Coverage");
            BindSelector(root.SetupPanel, "Combat Style");
            BindSelector(root.SetupPanel, "Game Mode");
            BindSelector(root.SetupPanel, "Timing Assist");
            BindSelector(root.ReadyPanel, "Coverage");
            BindSelector(root.ReadyPanel, "Game Mode");
            BindSelector(root.ReadyPanel, "Timing Assist");
            BindSettingsRows(root.SettingsPanel);
            BindKnownButtons(root.SetupPanel);
            BindKnownButtons(root.ReadyPanel);
            BindKnownButtons(root.SettingsPanel);
            BindKnownButtons(root.SavedTracksPanel);
            BindKnownButtons(root.PauseOverlay);
            BindKnownButtons(root.ResultsPanel);
            BindKnownButtons(root.ErrorPanel);
            BindReadySummary(root.ReadyPanel, "Detection", "selector.detection");
            BindReadySummary(root.ReadyPanel, "Difficulty", "selector.difficulty");
            BindReadySummary(root.ReadyPanel, "Combat Style", "selector.combat-style");
            if (root.SetupPanel != null)
            {
                Transform saveToggle = root.SetupPanel.transform.FindDeepChild("Save to Library Toggle");
                if (saveToggle != null) Attach(saveToggle.gameObject, "button.save-to-library-toggle");
            }
        }

        internal static void Attach(GameObject target, string key)
        {
            if (target == null) return;
            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
            PulseForgeTooltipTarget tooltip = target.GetComponent<PulseForgeTooltipTarget>();
            if (tooltip == null) tooltip = target.AddComponent<PulseForgeTooltipTarget>();
            tooltip.Configure(key);
        }

        internal static string Slug(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("(ms)", "ms");
        }

        private static void BindSelector(Component panel, string label)
        {
            if (panel == null) return;
            Transform selector = panel.transform.FindDeepChild(label + " Selector");
            if (selector == null) return;
            string key = "selector." + Slug(label);
            Transform labelTransform = selector.Find("Label");
            if (labelTransform != null) Attach(labelTransform.gameObject, key);
            Button[] buttons = selector.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Attach(buttons[i].gameObject, key + "." + Slug(buttons[i].name));
            }
        }

        private static void BindSettingsRows(SettingsPanelView panel)
        {
            if (panel == null) return;
            Transform content = panel.transform.FindDeepChild("Content");
            if (content == null) return;
            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.name.EndsWith(" Row", StringComparison.Ordinal)) continue;
                string label = child.name.Substring(0, child.name.Length - 4);
                string key = "settings." + Slug(label);
                Attach(child.gameObject, key);
                Transform rowLabel = child.Find("Label");
                if (rowLabel != null) Attach(rowLabel.gameObject, key);
            }
        }

        private static void BindKnownButtons(Component panel)
        {
            if (panel == null) return;
            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                string key = "button." + Slug(buttons[i].name);
                if (PulseForgeTooltipCatalog.Contains(key)) Attach(buttons[i].gameObject, key);
            }
        }

        private static void BindReadySummary(Component panel, string name, string key)
        {
            if (panel == null) return;
            Transform value = panel.transform.FindDeepChild(name);
            if (value != null) Attach(value.gameObject, key);
        }
    }

    internal static class TransformTooltipExtensions
    {
        public static Transform FindDeepChild(this Transform parent, string name)
        {
            if (parent == null) return null;
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name) return children[i];
            }
            return null;
        }
    }

    internal static class PulseForgeTooltipCatalog
    {
        private readonly struct Copy
        {
            public Copy(string enTitle, string enBody, string trTitle, string trBody)
            {
                EnTitle = enTitle;
                EnBody = enBody;
                TrTitle = trTitle;
                TrBody = trBody;
            }
            public string EnTitle { get; }
            public string EnBody { get; }
            public string TrTitle { get; }
            public string TrBody { get; }
        }

        private static readonly Dictionary<string, Copy> Entries = CreateEntries();

        public static bool Contains(string key) => Entries.ContainsKey(key);

        public static bool TryGet(
            string key,
            PulseForgeUILanguage language,
            out string title,
            out string body)
        {
            if (!Entries.TryGetValue(key ?? string.Empty, out Copy copy))
            {
                title = string.Empty;
                body = string.Empty;
                return false;
            }
            bool turkish = language == PulseForgeUILanguage.Turkish;
            title = turkish ? copy.TrTitle : copy.EnTitle;
            body = turkish ? copy.TrBody : copy.EnBody;
            return true;
        }

        private static Dictionary<string, Copy> CreateEntries()
        {
            Dictionary<string, Copy> e = new Dictionary<string, Copy>(StringComparer.Ordinal);
            Add(e, "selector.detection", "Detection", "Chooses which audio features receive more weight; both modes still use adaptive V2 analysis.", "Algılama", "Ses analizinde hangi özelliklerin daha ağırlıklı kullanılacağını seçer; iki mod da uyarlanabilir V2 analizi kullanır.");
            Add(e, "selector.detection.onset", "Onset", "Prioritizes spectral flux and sharp transients. Best for drums and clearly articulated attacks.", "Onset", "Spektral değişimleri ve keskin vuruşları öne çıkarır. Davul ve belirgin ataklar için uygundur.");
            Add(e, "selector.detection.amplitude", "Amplitude", "Prioritizes RMS and energy changes. Useful for tracks whose rhythm is expressed by loudness movement.", "Genlik", "RMS ve enerji değişimlerini öne çıkarır. Ritmi ses yüksekliği değişimleriyle belirginleşen parçalar için uygundur.");
            Add(e, "selector.difficulty", "Difficulty", "Controls mechanical complexity, recovery time and simultaneous cues. Coverage controls input density separately.", "Zorluk", "Mekanik karmaşıklığı, toparlanma aralığını ve eşzamanlı ipuçlarını belirler. Input yoğunluğunu Coverage ayrı yönetir.");
            Add(e, "selector.difficulty.easy", "Easy", "Longer recovery, fewer compound mechanics and at most one focused cue. Designed for learning the radial language.", "Kolay", "Daha uzun toparlanma süresi, daha az birleşik mekanik ve en fazla bir odaklanmış ipucu sunar.");
            Add(e, "selector.difficulty.normal", "Normal", "Balanced recovery and compound mechanics, with up to two focused cues.", "Normal", "Dengeli toparlanma ve birleşik mekanikler sunar; aynı anda en fazla iki odaklanmış ipucu bulunur.");
            Add(e, "selector.difficulty.hard", "Hard", "Shorter recovery, more compound events and up to three focused cues. Timing windows do not change.", "Zor", "Daha kısa toparlanma, daha çok birleşik event ve en fazla üç odaklanmış ipucu sunar. Timing pencereleri değişmez.");
            Add(e, "selector.coverage", "Coverage", "Controls how densely accepted beats become inputs without changing mechanical difficulty.", "Kapsama", "Mekanik zorluğu değiştirmeden kabul edilen beat'lerin ne kadar yoğun input'a dönüşeceğini belirler.");
            Add(e, "selector.coverage.relaxed", "Relaxed", "Keeps strong onsets and important beats. Lowest input density and fewer compound events.", "Rahat", "Güçlü onset ve önemli beat'leri korur. En düşük input yoğunluğunu ve daha az birleşik event'i sunar.");
            Add(e, "selector.coverage.standard", "Standard", "Balanced onset and beat-grid coverage for the intended default rhythm density.", "Standart", "Varsayılan ritim yoğunluğu için onset ve beat-grid kapsamasını dengeler.");
            Add(e, "selector.coverage.full-pulse", "Full Pulse", "Covers accepted active beats and safe subdivisions, avoids silence and caps unsafe density.", "Tam Nabız", "Aktif kabul edilmiş beat ve güvenli alt bölümleri kapsar; sessizliği ve güvensiz yoğunluğu önler.");
            Add(e, "selector.combat-style", "Combat Style", "Changes how the same input budget is distributed between actions and compound event types.", "Dövüş Stili", "Aynı input bütçesinin aksiyonlar ve birleşik event türleri arasında nasıl dağıtılacağını değiştirir.");
            Add(e, "selector.combat-style.legacy", "Legacy", "Uses Guard and Light only. Saboteur encounters are disabled.", "Legacy", "Yalnız Guard ve Light kullanır. Saboteur karşılaşmaları devre dışıdır.");
            Add(e, "selector.combat-style.balanced", "Balanced", "A general mix of Guard, Dodge, Light and Heavy mechanics.", "Dengeli", "Guard, Dodge, Light ve Heavy mekaniklerini genel amaçlı dengeler.");
            Add(e, "selector.combat-style.defensive", "Defensive", "Favors Guard and Dodge while reducing attack-heavy patterns.", "Savunmacı", "Guard ve Dodge ağırlıklıdır; saldırı yoğun kalıpları azaltır.");
            Add(e, "selector.combat-style.aggressive", "Aggressive", "Favors Light and Heavy attacks and selects Saboteurs more often when eligible.", "Agresif", "Light ve Heavy saldırıları öne çıkarır; uygun olduğunda Saboteur'u daha sık seçer.");
            Add(e, "selector.combat-style.bursty", "Bursty", "Packages the same budget into chains, swarms and sweeps more often.", "Patlamalı", "Aynı bütçeyi chain, swarm ve sweep grupları hâlinde daha sık paketler.");
            Add(e, "selector.game-mode", "Game Mode", "Changes run failure rules only. It does not rebuild or alter the beatmap.", "Oyun Modu", "Yalnız koşunun başarısızlık kurallarını değiştirir. Beatmap'i yeniden üretmez veya değiştirmez.");
            Add(e, "selector.game-mode.standard", "Standard", "Misses break combo, but the song continues until its natural end.", "Standart", "Miss combo'yu bozar ancak şarkı doğal sonuna kadar devam eder.");
            Add(e, "selector.game-mode.survival", "Survival", "Starts with 100 health. Miss and Wrong Input deal intensity-based damage.", "Hayatta Kalma", "100 canla başlar. Miss ve Wrong Input, event yoğunluğuna göre hasar verir.");
            Add(e, "selector.game-mode.one-life", "One Life", "The first Miss or Wrong Input immediately ends the run.", "Tek Can", "İlk Miss veya Wrong Input koşuyu hemen bitirir.");
            Add(e, "selector.timing-assist", "Timing Assist", "Chooses judgement windows. It does not change beatmap timing or analysis.", "Timing Desteği", "Değerlendirme pencerelerini seçer. Beatmap zamanını veya analizi değiştirmez.");
            Add(e, "selector.timing-assist.standard", "Standard", "Perfect ±45 ms, Good ±100 ms.", "Standart", "Perfect ±45 ms, Good ±100 ms.");
            Add(e, "selector.timing-assist.relaxed", "Relaxed", "Perfect ±65 ms, Good ±140 ms. Scores are stored separately from Standard.", "Rahat", "Perfect ±65 ms, Good ±140 ms. Skorlar Standard'dan ayrı tutulur.");
            Add(e, "selector.timing-assist.practice", "Practice", "Perfect ±90 ms, Good ±200 ms. Intended for learning and stored separately.", "Pratik", "Perfect ±90 ms, Good ±200 ms. Öğrenme için tasarlanmıştır ve ayrı kaydedilir.");

            AddSetting(e, "tooltip-language", "Language / Dil", "Selects English or Turkish for the entire UI.", "Language / Dil", "Tüm arayüz için İngilizce veya Türkçe dilini seçer.");
            AddSetting(e, "master-volume", "Master Volume", "Controls the overall application volume.", "Ana Ses", "Uygulamanın genel ses seviyesini ayarlar.");
            AddSetting(e, "music-volume", "Music Volume", "Controls song playback volume without changing analysis.", "Müzik Sesi", "Analizi değiştirmeden şarkı oynatma sesini ayarlar.");
            AddSetting(e, "display-mode", "Display Mode", "Switches between supported window and fullscreen modes.", "Görüntü Modu", "Desteklenen pencere ve tam ekran modları arasında geçiş yapar.");
            AddSetting(e, "resolution", "Resolution", "Changes output resolution and refresh rate.", "Çözünürlük", "Çıkış çözünürlüğünü ve yenileme hızını değiştirir.");
            AddSetting(e, "vsync", "VSync", "Synchronizes frame presentation with the display to reduce tearing.", "VSync", "Ekran yırtılmasını azaltmak için kare sunumunu monitörle eşler.");
            AddSetting(e, "frame-rate-limit", "Frame Rate Limit", "Caps rendering frame rate independently of rhythm timing.", "Kare Hızı Sınırı", "Ritim zamanlamasından bağımsız olarak render kare hızını sınırlar.");
            AddSetting(e, "guard", "Guard Binding", "Key used for Guard press, hold and release requirements.", "Guard Tuşu", "Guard basma, tutma ve bırakma requirement'larında kullanılan tuştur.");
            AddSetting(e, "light-attack", "Light Attack Binding", "Key used for Light attacks, chains, swarms and Break Target hits.", "Light Attack Tuşu", "Light saldırılarında, chain, swarm ve Break Target vuruşlarında kullanılan tuştur.");
            AddSetting(e, "dodge", "Dodge Binding", "Key used to evade threats. Dodge normally does not use hold or repeat.", "Dodge Tuşu", "Tehditlerden kaçınmak için kullanılır. Dodge normalde hold veya repeat kullanmaz.");
            AddSetting(e, "heavy-attack", "Heavy Attack Binding", "Key used for charge-and-release Heavy attacks.", "Heavy Attack Tuşu", "Bas-tut-bırak kimliğine sahip Heavy saldırılarda kullanılır.");
            AddSetting(e, "pause", "Pause Binding", "Key used to pause or resume gameplay.", "Duraklatma Tuşu", "Oyunu duraklatmak veya sürdürmek için kullanılır.");
            AddSetting(e, "motion-effects", "Motion Effects", "Enables optional UI movement and transition effects.", "Hareket Efektleri", "İsteğe bağlı UI hareket ve geçiş efektlerini açar.");
            AddSetting(e, "default-detection", "Default Detection", "Detection mode selected when a new setup begins.", "Varsayılan Algılama", "Yeni bir kurulum başladığında seçilecek algılama modudur.");
            AddSetting(e, "default-difficulty", "Default Difficulty", "Difficulty selected when the application starts.", "Varsayılan Zorluk", "Uygulama açıldığında seçilecek mekanik zorluktur.");
            AddSetting(e, "default-combat-style", "Default Combat Style", "Combat distribution selected for new analyses.", "Varsayılan Dövüş Stili", "Yeni analizlerde seçilecek dövüş dağılımıdır.");
            AddSetting(e, "default-coverage", "Default Coverage", "Input-density coverage selected for new analyses.", "Varsayılan Kapsama", "Yeni analizlerde seçilecek input yoğunluğu kapsamasıdır.");
            AddSetting(e, "default-game-mode", "Default Game Mode", "Run mode selected at startup; it can be changed without reanalysis.", "Varsayılan Oyun Modu", "Başlangıçta seçilecek koşu modudur; yeniden analiz olmadan değiştirilebilir.");
            AddSetting(e, "default-timing-assist", "Default Timing Assist", "Judgement profile selected at startup.", "Varsayılan Timing Desteği", "Başlangıçta seçilecek değerlendirme penceresi profilidir.");
            AddSetting(e, "show-upcoming-inputs", "Upcoming Input Queue", "Shows the next logical requirement groups in the gameplay HUD.", "Yaklaşan Input Kuyruğu", "Gameplay HUD'da sıradaki mantıksal requirement gruplarını gösterir.");
            AddSetting(e, "beat-pulse", "Beat Pulse", "Pulses the judgement ring from beat-grid timing when available.", "Beat Nabzı", "Beat grid mevcutsa judgement ring'i ritme göre görsel olarak titreştirir.");
            AddSetting(e, "forecast-lead-multiplier", "Forecast Lead", "Scales how early forecast markers appear; gameplay timing is unchanged.", "Forecast Süresi", "Forecast işaretlerinin ne kadar erken görüneceğini ölçekler; gameplay timing değişmez.");
            AddSetting(e, "readability-mode", "Readability Mode", "Adjusts cue size, contrast and timing text without changing judgement.", "Okunabilirlik Modu", "Judgement'ı değiştirmeden ipucu boyutu, kontrastı ve timing metnini ayarlar.");
            AddSetting(e, "beatmap-offset-ms", "Beatmap Offset", "Moves beatmap target timing globally for calibration. Use small values.", "Beatmap Offset", "Kalibrasyon için bütün beatmap hedef zamanını kaydırır. Küçük değerler kullanın.");
            AddSetting(e, "input-timing-offset-ms", "Input Timing Offset", "Calibrates effective input time without modifying beatmap data.", "Input Timing Offset", "Beatmap verisini değiştirmeden etkili input zamanını kalibre eder.");

            AddButton(e, "choose-custom-audio", "Choose Custom Audio", "Select an audio file to convert and analyze.", "Özel Ses Seç", "Dönüştürülüp analiz edilecek bir ses dosyası seçer.");
            AddButton(e, "play-built-in-demo", "Built-in Demo", "Loads the bundled demo without importing a custom song.", "Hazır Demo", "Özel şarkı aktarmadan paketlenmiş demoyu yükler.");
            AddButton(e, "analyze-song", "Analyze Song", "Runs Analyzer V2, encounter planning and validation for the current setup.", "Şarkıyı Analiz Et", "Mevcut ayarlarla Analyzer V2, encounter planlama ve doğrulamayı çalıştırır.");
            AddButton(e, "saved-tracks", "Saved Tracks", "Opens cached tracks and presets that can be loaded without reanalysis.", "Kayıtlı Şarkılar", "Yeniden analiz olmadan yüklenebilen cache'li şarkı ve presetleri açar.");
            AddButton(e, "settings", "Settings", "Opens audio, display, control and gameplay defaults.", "Ayarlar", "Ses, görüntü, kontrol ve gameplay varsayılanlarını açar.");
            AddButton(e, "start", "Start", "Starts the prepared session with the selected run and timing options.", "Başlat", "Hazırlanan session'ı seçili koşu ve timing seçenekleriyle başlatır.");
            AddButton(e, "change-settings", "Change Settings", "Opens settings without rebuilding the prepared beatmap.", "Ayarları Değiştir", "Hazır beatmap'i yeniden üretmeden ayarları açar.");
            AddButton(e, "choose-another-song", "Choose Another Song", "Returns to song selection and leaves the current prepared session.", "Başka Şarkı Seç", "Şarkı seçimine döner ve mevcut hazırlanmış session'dan çıkar.");
            AddButton(e, "apply", "Apply", "Saves and applies the current settings draft.", "Uygula", "Mevcut ayar taslağını kaydeder ve uygular.");
            AddButton(e, "cancel", "Cancel", "Closes settings without saving the current draft.", "İptal", "Mevcut taslağı kaydetmeden ayarları kapatır.");
            AddButton(e, "reset-to-defaults", "Reset to Defaults", "Restores default values in the draft; use Apply to save them.", "Varsayılanlara Dön", "Taslak değerleri varsayılana döndürür; kaydetmek için Uygula'yı kullanın.");
            AddButton(e, "reset-bindings", "Reset Bindings", "Restores default gameplay keys without clearing unrelated settings.", "Tuşları Sıfırla", "Diğer ayarları silmeden gameplay tuşlarını varsayılana döndürür.");
            AddButton(e, "save-to-library-toggle", "Save to Library", "Caches the converted audio and generated radial preset after a successful analysis.", "Kütüphaneye Kaydet", "Başarılı analizden sonra dönüştürülen sesi ve üretilen radial preset'i cache'e kaydeder.");
            AddButton(e, "resume", "Resume", "Returns to the paused run.", "Devam Et", "Duraklatılmış koşuya geri döner.");
            AddButton(e, "restart", "Restart", "Restarts the current run and clears run-level state.", "Yeniden Başlat", "Mevcut koşuyu yeniden başlatır ve koşu durumunu temizler.");
            AddButton(e, "replay", "Replay", "Starts the prepared track again with the current options.", "Tekrar Oyna", "Hazır şarkıyı mevcut seçeneklerle yeniden başlatır.");
            AddButton(e, "retry", "Retry", "Retries the operation that produced the current error.", "Tekrar Dene", "Mevcut hatayı oluşturan işlemi yeniden dener.");
            AddButton(e, "back-to-setup", "Back to Setup", "Returns to setup without starting analysis automatically.", "Kuruluma Dön", "Otomatik analiz başlatmadan kurulum ekranına döner.");
            return e;
        }

        private static void AddSetting(Dictionary<string, Copy> entries, string key, string enTitle, string enBody, string trTitle, string trBody)
        {
            Add(entries, "settings." + key, enTitle, enBody, trTitle, trBody);
        }

        private static void AddButton(Dictionary<string, Copy> entries, string key, string enTitle, string enBody, string trTitle, string trBody)
        {
            Add(entries, "button." + key, enTitle, enBody, trTitle, trBody);
        }

        private static void Add(Dictionary<string, Copy> entries, string key, string enTitle, string enBody, string trTitle, string trBody)
        {
            entries[key] = new Copy(enTitle, enBody, trTitle, trBody);
        }
    }
}
