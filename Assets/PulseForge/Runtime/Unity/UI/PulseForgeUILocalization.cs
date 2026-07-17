using System;
using System.Collections.Generic;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    internal static class PulseForgeUILocalization
    {
        private static readonly Dictionary<string, string> TurkishButtons =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Play Built-in Demo", "Hazır Demoyu Oyna" },
                { "Choose Custom Audio", "Özel Ses Seç" },
                { "Analyze Song", "Şarkıyı Analiz Et" },
                { "Saved Tracks", "Kayıtlı Şarkılar" },
                { "Settings", "Ayarlar" },
                { "Start", "Başlat" },
                { "Change Settings", "Ayarları Değiştir" },
                { "Choose Another Song", "Başka Şarkı Seç" },
                { "Apply", "Uygula" },
                { "Cancel", "İptal" },
                { "Reset to Defaults", "Varsayılanlara Dön" },
                { "Reset Bindings", "Tuşları Sıfırla" },
                { "Rebind", "Yeniden Ata" },
                { "Resume", "Devam Et" },
                { "Restart", "Yeniden Başlat" },
                { "Replay", "Tekrar Oyna" },
                { "Back to Setup", "Kuruluma Dön" },
                { "Retry", "Tekrar Dene" },
                { "Back", "Geri" },
                { "Load", "Yükle" },
                { "Remove", "Kaldır" }
            };

        private static readonly Dictionary<string, string> TurkishSettingsRows =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Tooltip Language", "Açıklama Dili" },
                { "Master Volume", "Ana Ses" },
                { "Music Volume", "Müzik Sesi" },
                { "Display Mode", "Görüntü Modu" },
                { "Resolution", "Çözünürlük" },
                { "VSync", "Dikey Senkronizasyon" },
                { "Frame Rate Limit", "Kare Hızı Sınırı" },
                { "Guard", "Guard" },
                { "Light Attack", "Light Attack" },
                { "Dodge", "Dodge" },
                { "Heavy Attack", "Heavy Attack" },
                { "Pause", "Duraklatma" },
                { "Motion Effects", "Hareket Efektleri" },
                { "Default Detection", "Varsayılan Algılama" },
                { "Default Difficulty", "Varsayılan Zorluk" },
                { "Default Combat Style", "Varsayılan Dövüş Stili" },
                { "Default Coverage", "Varsayılan Kapsama" },
                { "Default Game Mode", "Varsayılan Oyun Modu" },
                { "Default Timing Assist", "Varsayılan Timing Desteği" },
                { "Show Upcoming Inputs", "Yaklaşan Inputları Göster" },
                { "Beat Pulse", "Beat Nabzı" },
                { "Forecast Lead Multiplier", "Forecast Süre Çarpanı" },
                { "Readability Mode", "Okunabilirlik Modu" },
                { "Beatmap Offset (ms)", "Beatmap Offset (ms)" },
                { "Input Timing Offset (ms)", "Input Timing Offset (ms)" }
            };

        public static void Apply(PulseForgeSceneUIRoot root, PulseForgeUILanguage language)
        {
            if (root == null) return;
            bool turkish = language == PulseForgeUILanguage.Turkish;
            ApplySetup(root.SetupPanel, turkish);
            ApplyReady(root.ReadyPanel, turkish);
            ApplySettings(root.SettingsPanel, turkish);
            ApplyKnownButtons(root, turkish);
        }

        public static string TranslateValue(string value, PulseForgeUILanguage language)
        {
            if (language != PulseForgeUILanguage.Turkish || string.IsNullOrEmpty(value)) return value;
            switch (value)
            {
                case "Amplitude": return "Genlik";
                case "Easy": return "Kolay";
                case "Hard": return "Zor";
                case "Balanced": return "Dengeli";
                case "Defensive": return "Savunmacı";
                case "Aggressive": return "Agresif";
                case "Bursty": return "Patlamalı";
                case "Relaxed": return "Rahat";
                case "Standard": return "Standart";
                case "Full Pulse": return "Tam Nabız";
                case "Survival": return "Hayatta Kalma";
                case "One Life": return "Tek Can";
                case "Practice": return "Pratik";
                case "Windowed": return "Pencereli";
                case "Fullscreen": return "Tam Ekran";
                case "Unlimited": return "Sınırsız";
                case "Assisted": return "Destekli";
                case "High Clarity": return "Yüksek Netlik";
                default: return value;
            }
        }

        private static void ApplySetup(SetupPanelView panel, bool turkish)
        {
            if (panel == null) return;
            SetText(panel.transform, "Description", turkish
                ? "Bir şarkıyı oynanabilir ritim-dövüş oturumuna dönüştür."
                : "Turn a song into a playable rhythm-combat session.");
            SetText(panel.transform, "Audio Source Label", turkish ? "Ses Kaynağı" : "Audio Source");
            SetSelector(panel, "Detection", turkish ? "Algılama" : "Detection", turkish);
            SetSelector(panel, "Difficulty", turkish ? "Zorluk" : "Difficulty", turkish);
            SetSelector(panel, "Coverage", turkish ? "Kapsama" : "Coverage", turkish);
            SetSelector(panel, "Combat Style", turkish ? "Dövüş Stili" : "Combat Style", turkish);
            SetSelector(panel, "Game Mode", turkish ? "Oyun Modu" : "Game Mode", turkish);
            SetSelector(panel, "Timing Assist", turkish ? "Timing Desteği" : "Timing Assist", turkish);

            Toggle save = panel.SaveToLibraryToggle;
            Text saveLabel = save == null ? null : save.GetComponentInChildren<Text>(true);
            if (saveLabel != null)
            {
                saveLabel.text = turkish ? "Bu kurulumu Kütüphaneye kaydet" : "Save this setup to Library";
            }
        }

        private static void ApplyReady(ReadyPanelView panel, bool turkish)
        {
            if (panel == null) return;
            SetText(panel.transform, "Heading", turkish ? "Şarkı Hazır" : "Track Ready");
            SetSelector(panel, "Coverage", turkish ? "Kapsama" : "Coverage", turkish);
            SetSelector(panel, "Game Mode", turkish ? "Oyun Modu" : "Game Mode", turkish);
            SetSelector(panel, "Timing Assist", turkish ? "Timing Desteği" : "Timing Assist", turkish);
        }

        private static void ApplySettings(SettingsPanelView panel, bool turkish)
        {
            if (panel == null) return;
            SetText(panel.transform, "Title", turkish ? "AYARLAR" : "SETTINGS");
            SetText(panel.transform, "LANGUAGE", turkish ? "DİL" : "LANGUAGE");
            SetText(panel.transform, "AUDIO", turkish ? "SES" : "AUDIO");
            SetText(panel.transform, "DISPLAY", turkish ? "GÖRÜNTÜ" : "DISPLAY");
            SetText(panel.transform, "CONTROLS", turkish ? "KONTROLLER" : "CONTROLS");
            SetText(panel.transform, "GAMEPLAY", turkish ? "OYUN" : "GAMEPLAY");

            foreach (KeyValuePair<string, string> entry in TurkishSettingsRows)
            {
                Transform row = panel.transform.FindDeepChild(entry.Key + " Row");
                Text label = row == null ? null : row.Find("Label")?.GetComponent<Text>();
                if (label != null) label.text = turkish ? entry.Value : entry.Key;
                Text value = row == null ? null : row.Find("Value")?.GetComponent<Text>();
                if (value != null)
                {
                    value.text = turkish ? TranslateValue(value.text, PulseForgeUILanguage.Turkish) : value.text;
                }
            }
        }

        private static void ApplyKnownButtons(PulseForgeSceneUIRoot root, bool turkish)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label == null) continue;
                if (turkish && TurkishButtons.TryGetValue(buttons[i].name, out string translated))
                {
                    label.text = translated;
                }
                else if (!turkish && TurkishButtons.ContainsKey(buttons[i].name))
                {
                    label.text = buttons[i].name;
                }
            }
        }

        private static void SetSelector(Component panel, string selectorName, string labelValue, bool turkish)
        {
            Transform selector = panel.transform.FindDeepChild(selectorName + " Selector");
            if (selector == null) return;
            Text label = selector.Find("Label")?.GetComponent<Text>();
            if (label != null) label.text = labelValue;
            Button[] buttons = selector.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Text text = buttons[i].GetComponentInChildren<Text>(true);
                if (text == null) continue;
                text.text = turkish
                    ? TranslateValue(buttons[i].name, PulseForgeUILanguage.Turkish)
                    : buttons[i].name;
            }
        }

        private static void SetText(Transform root, string objectName, string value)
        {
            Transform target = root.FindDeepChild(objectName);
            Text text = target == null ? null : target.GetComponent<Text>();
            if (text != null) text.text = value;
        }
    }
}
