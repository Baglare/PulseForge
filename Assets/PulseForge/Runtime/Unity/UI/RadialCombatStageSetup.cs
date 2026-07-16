using System;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialCombatStageSetup
    {
        public const string StageName = "Radial Combat Stage";

        private static readonly string[] DirectionNames =
        {
            "North",
            "North East",
            "East",
            "South East",
            "South",
            "South West",
            "West",
            "North West"
        };

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            if (root == null || root.Canvas == null)
            {
                return null;
            }

            RectTransform canvas = root.Canvas.transform as RectTransform;
            RectTransform stageRect = EnsureRect(StageName, canvas, registerCreated);
            stageRect.anchorMin = new Vector2(0.18f, 0.15f);
            stageRect.anchorMax = new Vector2(0.82f, 0.90f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;
            if (root.GameplayHud != null)
            {
                int stageIndex = stageRect.GetSiblingIndex();
                int hudIndex = root.GameplayHud.transform.GetSiblingIndex();
                stageRect.SetSiblingIndex(stageIndex < hudIndex ? hudIndex - 1 : hudIndex);
            }

            RadialCombatStageView stage = EnsureComponent<RadialCombatStageView>(
                stageRect.gameObject,
                addComponent);
            RadialCombatPresentationController presentation =
                EnsureComponent<RadialCombatPresentationController>(
                    stageRect.gameObject,
                    addComponent);

            RectTransform background = EnsureRect(
                "Stage Background",
                stageRect,
                registerCreated);
            Stretch(background);
            Image backgroundImage = EnsureComponent<Image>(background.gameObject, addComponent);
            backgroundImage.color = PulseForgeUITheme.WithAlpha(
                PulseForgeUITheme.BackgroundSecondary,
                0.72f);
            backgroundImage.raycastTarget = false;
            Outline backgroundOutline = EnsureComponent<Outline>(
                background.gameObject,
                addComponent);
            ConfigureOutline(backgroundOutline, PulseForgeUITheme.Border, 2f);

            RectTransform guidesRoot = EnsureRect(
                "Direction Guides",
                stageRect,
                registerCreated);
            Stretch(guidesRoot);
            RectTransform[] guides = new RectTransform[DirectionNames.Length];
            for (int i = 0; i < DirectionNames.Length; i++)
            {
                RectTransform guide = EnsureRect(
                    DirectionNames[i],
                    guidesRoot,
                    registerCreated);
                guide.anchorMin = new Vector2(0.5f, 0.5f);
                guide.anchorMax = new Vector2(0.5f, 0.5f);
                guide.pivot = new Vector2(0f, 0.5f);
                guide.anchoredPosition = Vector2.zero;
                guide.sizeDelta = new Vector2(360f, 2f);
                guide.localRotation = Quaternion.Euler(0f, 0f, 90f - (i * 45f));
                Image guideImage = EnsureComponent<Image>(guide.gameObject, addComponent);
                guideImage.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Divider, 0.58f);
                guideImage.raycastTarget = false;
                guides[i] = guide;
            }

            RectTransform ring = EnsureRect(
                "Judgement Ring",
                stageRect,
                registerCreated);
            Center(ring, new Vector2(316f, 316f));
            Image ringImage = EnsureComponent<Image>(ring.gameObject, addComponent);
            ringImage.sprite = PulseForgeUIFactory.RoundedSprite;
            ringImage.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.SurfaceSoft, 0.16f);
            ringImage.raycastTarget = false;
            Outline ringOutline = EnsureComponent<Outline>(ring.gameObject, addComponent);
            ConfigureOutline(ringOutline, PulseForgeUITheme.Primary, 3f);

            RectTransform core = EnsureRect(
                "Player Core",
                stageRect,
                registerCreated);
            Center(core, new Vector2(88f, 88f));
            Image coreImage = EnsureComponent<Image>(core.gameObject, addComponent);
            coreImage.sprite = PulseForgeUIFactory.RoundedSprite;
            coreImage.color = PulseForgeUITheme.SurfaceRaised;
            coreImage.raycastTarget = false;
            Outline coreOutline = EnsureComponent<Outline>(core.gameObject, addComponent);
            ConfigureOutline(coreOutline, PulseForgeUITheme.Primary, 3f);
            Text coreLabel = EnsureText(
                "Player",
                core,
                "PLAYER",
                13,
                PulseForgeUITheme.PrimaryText,
                registerCreated,
                addComponent);
            Stretch(coreLabel.rectTransform);

            RectTransform encounters = EnsureRect(
                "Encounter Container",
                stageRect,
                registerCreated);
            Stretch(encounters);
            RectTransform projectiles = EnsureRect(
                "Projectile Container",
                stageRect,
                registerCreated);
            Stretch(projectiles);
            encounters.SetAsLastSibling();
            projectiles.SetAsLastSibling();
            core.SetAsLastSibling();

            stage.Configure(
                background.gameObject,
                guidesRoot,
                guides,
                ring,
                core,
                encounters,
                projectiles);
            presentation.Configure(stage);
            root.ConfigureRadialCombatStage(stage, presentation);
            stage.SetRadialSessionVisible(false);
            return stage;
        }

        private static RectTransform EnsureRect(
            string name,
            Transform parent,
            Action<GameObject> registerCreated)
        {
            Transform existing = parent == null ? null : parent.Find(name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect == null)
                {
                    throw new InvalidOperationException(
                        "Radial stage child '" + name + "' must use RectTransform.");
                }
                return existingRect;
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
            if (existing != null)
            {
                return existing;
            }
            return addComponent == null
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

        private static void ConfigureOutline(Outline outline, Color color, float size)
        {
            outline.effectColor = color;
            outline.effectDistance = new Vector2(size, -size);
            outline.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
