using System;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialSaboteurFogSetup
    {
        public const string PresentationRootName = "Saboteur & Fog Presentation";

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            RadialCombatStageView stage = RadialCompoundVisualSetup.Apply(
                root,
                registerCreated,
                addComponent);
            if (stage == null)
            {
                return null;
            }

            RectTransform presentationRoot = EnsureRect(
                PresentationRootName,
                stage.transform,
                registerCreated);
            Stretch(presentationRoot);
            RadialFogPresentationView fogView = EnsureComponent<RadialFogPresentationView>(
                presentationRoot.gameObject,
                addComponent);

            RectTransform overlay = EnsureRect("Fog Overlay", presentationRoot, registerCreated);
            Stretch(overlay);
            Image[] panels = new Image[4];
            panels[0] = ConfigurePanel(
                EnsureRect("Fog Top", overlay, registerCreated),
                new Vector2(0f, 0.68f),
                Vector2.one,
                addComponent);
            panels[1] = ConfigurePanel(
                EnsureRect("Fog Bottom", overlay, registerCreated),
                Vector2.zero,
                new Vector2(1f, 0.32f),
                addComponent);
            panels[2] = ConfigurePanel(
                EnsureRect("Fog Left", overlay, registerCreated),
                new Vector2(0f, 0.32f),
                new Vector2(0.32f, 0.68f),
                addComponent);
            panels[3] = ConfigurePanel(
                EnsureRect("Fog Right", overlay, registerCreated),
                new Vector2(0.68f, 0.32f),
                new Vector2(1f, 0.68f),
                addComponent);

            RectTransform chip = EnsureRect("Fog Status Chip", overlay, registerCreated);
            chip.anchorMin = new Vector2(0.5f, 1f);
            chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -16f);
            chip.sizeDelta = new Vector2(154f, 44f);
            Image chipImage = EnsureComponent<Image>(chip.gameObject, addComponent);
            chipImage.sprite = PulseForgeUIFactory.RoundedSprite;
            chipImage.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceRaised, 0.94f);
            chipImage.raycastTarget = false;
            Outline chipOutline = EnsureComponent<Outline>(chip.gameObject, addComponent);
            chipOutline.effectColor = PulseForgeUITheme.SecondaryText;
            chipOutline.effectDistance = new Vector2(2f, -2f);
            chipOutline.useGraphicAlpha = true;
            Text fogLabel = EnsureText(
                "Fog Label",
                chip,
                "FOG",
                15,
                PulseForgeUITheme.PrimaryText,
                registerCreated,
                addComponent);
            fogLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            fogLabel.rectTransform.anchorMax = new Vector2(0.60f, 1f);
            fogLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            fogLabel.rectTransform.offsetMax = Vector2.zero;
            Text remaining = EnsureText(
                "Fog Remaining",
                chip,
                "0.0s",
                14,
                PulseForgeUITheme.Good,
                registerCreated,
                addComponent);
            remaining.rectTransform.anchorMin = new Vector2(0.60f, 0f);
            remaining.rectTransform.anchorMax = Vector2.one;
            remaining.rectTransform.offsetMin = Vector2.zero;
            remaining.rectTransform.offsetMax = new Vector2(-8f, 0f);

            RectTransform smoke = EnsureRect("Saboteur Smoke Burst", presentationRoot, registerCreated);
            smoke.anchorMin = new Vector2(0.5f, 0.5f);
            smoke.anchorMax = new Vector2(0.5f, 0.5f);
            smoke.pivot = new Vector2(0.5f, 0.5f);
            smoke.anchoredPosition = Vector2.zero;
            smoke.sizeDelta = new Vector2(148f, 148f);
            Image smokeImage = EnsureComponent<Image>(smoke.gameObject, addComponent);
            smokeImage.sprite = PulseForgeUIFactory.RoundedSprite;
            smokeImage.raycastTarget = false;
            Text smokeLabel = EnsureText(
                "Smoke Label",
                smoke,
                "SMOKE",
                17,
                PulseForgeUITheme.PrimaryText,
                registerCreated,
                addComponent);
            Stretch(smokeLabel.rectTransform);

            fogView.Configure(
                overlay.gameObject,
                panels,
                chip,
                remaining,
                smoke,
                smokeImage,
                smokeLabel);
            stage.ConfigureFogPresentation(fogView);
            presentationRoot.SetAsLastSibling();
            fogView.ResetView();
            return stage;
        }

        private static Image ConfigurePanel(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Func<GameObject, Type, Component> addComponent)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = EnsureComponent<Image>(rect.gameObject, addComponent);
            image.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.BackgroundSecondary, 0.46f);
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform EnsureRect(
            string name,
            Transform parent,
            Action<GameObject> registerCreated)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform rect = existing as RectTransform;
                if (rect == null)
                {
                    throw new InvalidOperationException(name + " must use RectTransform.");
                }
                return rect;
            }
            RectTransform created = PulseForgeUIFactory.CreateRect(name, parent);
            registerCreated?.Invoke(created.gameObject);
            return created;
        }

        private static T EnsureComponent<T>(
            GameObject gameObject,
            Func<GameObject, Type, Component> addComponent)
            where T : Component
        {
            T existing = gameObject.GetComponent<T>();
            return existing != null
                ? existing
                : addComponent == null
                    ? gameObject.AddComponent<T>()
                    : (T)addComponent(gameObject, typeof(T));
        }

        private static Text EnsureText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            Color color,
            Action<GameObject> registerCreated,
            Func<GameObject, Type, Component> addComponent)
        {
            RectTransform rect = EnsureRect(name, parent, registerCreated);
            Text text = EnsureComponent<Text>(rect.gameObject, addComponent);
            text.font = PulseForgeUIFactory.DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
