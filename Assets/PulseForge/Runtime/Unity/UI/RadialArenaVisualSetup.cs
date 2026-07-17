using System;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialArenaVisualSetup
    {
        public const string ArenaFoundationName = "Forge Arena Foundation";

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            RadialCombatStageView stage = RadialGroupTimingSetup.Apply(
                root,
                registerCreated,
                addComponent);
            if (stage == null)
            {
                return null;
            }

            RectTransform stageRect = stage.transform as RectTransform;
            if (stageRect == null)
            {
                throw new InvalidOperationException(
                    "Radial Combat Stage must use RectTransform.");
            }

            ConfigureStageBackground(stage, addComponent);

            RectTransform arenaRoot = EnsureRect(
                ArenaFoundationName,
                stageRect,
                registerCreated);
            Stretch(arenaRoot);
            RadialArenaGraphic arenaGraphic = EnsureComponent<RadialArenaGraphic>(
                arenaRoot.gameObject,
                addComponent);
            arenaGraphic.color = Color.white;
            arenaGraphic.raycastTarget = false;
            arenaGraphic.Configure(stage.OuterRadius, stage.JudgementRadius);
            int backgroundIndex = stage.StageBackground == null
                ? 0
                : stage.StageBackground.transform.GetSiblingIndex();
            arenaRoot.SetSiblingIndex(Mathf.Min(backgroundIndex + 1, stageRect.childCount - 1));

            ConfigureDirectionGuides(stage);
            ConfigureJudgementRing(stage, registerCreated, addComponent);
            ConfigurePlayerCore(
                stage,
                registerCreated,
                addComponent,
                out RectTransform guardAnchor,
                out RectTransform attackAnchor,
                out RectTransform dodgeAnchor);
            stage.ConfigureArenaVisuals(
                arenaGraphic,
                guardAnchor,
                attackAnchor,
                dodgeAnchor);
            return stage;
        }

        private static void ConfigureStageBackground(
            RadialCombatStageView stage,
            Func<GameObject, Type, Component> addComponent)
        {
            if (stage.StageBackground == null)
            {
                return;
            }

            Image image = EnsureComponent<Image>(stage.StageBackground, addComponent);
            image.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Backdrop, 0.94f);
            image.raycastTarget = false;
            Outline outline = EnsureComponent<Outline>(stage.StageBackground, addComponent);
            outline.effectColor = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.26f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private static void ConfigureDirectionGuides(RadialCombatStageView stage)
        {
            float guideStart = stage.JudgementRadius + 18f;
            float guideLength = Mathf.Max(40f, stage.OuterRadius - guideStart);
            for (int i = 0; i < stage.DirectionGuides.Count; i++)
            {
                RectTransform guide = stage.DirectionGuides[i];
                if (guide == null)
                {
                    continue;
                }

                float angle = (90f - i * 45f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                guide.anchoredPosition = direction * guideStart;
                guide.sizeDelta = new Vector2(guideLength, 2f);
                Image image = guide.GetComponent<Image>();
                if (image != null)
                {
                    image.color = PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.12f);
                    image.raycastTarget = false;
                }
            }
        }

        private static void ConfigureJudgementRing(
            RadialCombatStageView stage,
            Action<GameObject> registerCreated,
            Func<GameObject, Type, Component> addComponent)
        {
            RectTransform ring = stage.JudgementRing;
            if (ring == null)
            {
                return;
            }

            ring.sizeDelta = Vector2.one * (stage.JudgementRadius * 2f + 26f);
            Image oldImage = ring.GetComponent<Image>();
            if (oldImage != null)
            {
                oldImage.enabled = false;
            }
            Outline oldOutline = ring.GetComponent<Outline>();
            if (oldOutline != null)
            {
                oldOutline.enabled = false;
            }

            RectTransform surface = EnsureRect(
                "Judgement Forge Surface",
                ring,
                registerCreated);
            Stretch(surface);
            RadialJudgementRingGraphic graphic =
                EnsureComponent<RadialJudgementRingGraphic>(surface.gameObject, addComponent);
            graphic.color = Color.white;
            graphic.raycastTarget = false;
            surface.SetAsFirstSibling();
        }

        private static void ConfigurePlayerCore(
            RadialCombatStageView stage,
            Action<GameObject> registerCreated,
            Func<GameObject, Type, Component> addComponent,
            out RectTransform guardAnchor,
            out RectTransform attackAnchor,
            out RectTransform dodgeAnchor)
        {
            RectTransform core = stage.PlayerCore;
            if (core == null)
            {
                guardAnchor = null;
                attackAnchor = null;
                dodgeAnchor = null;
                return;
            }

            core.sizeDelta = new Vector2(104f, 104f);
            Image coreImage = EnsureComponent<Image>(core.gameObject, addComponent);
            coreImage.color = Color.clear;
            coreImage.raycastTarget = false;
            coreImage.enabled = false;
            Outline coreOutline = EnsureComponent<Outline>(core.gameObject, addComponent);
            coreOutline.effectColor = Color.clear;
            coreOutline.effectDistance = new Vector2(2f, -2f);
            coreOutline.useGraphicAlpha = true;
            coreOutline.enabled = false;

            ConfigureCorePlate(
                EnsureRect("Outer Forge Plate", core, registerCreated),
                new Vector2(76f, 76f),
                45f,
                PulseForgeUITheme.SurfaceRaised,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Strike, 0.72f),
                addComponent);
            ConfigureCorePlate(
                EnsureRect("Inner Forge Plate", core, registerCreated),
                new Vector2(50f, 50f),
                45f,
                PulseForgeUITheme.SurfaceSoft,
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.84f),
                addComponent);
            ConfigureCoreBar(
                EnsureRect("Core Energy Vertical", core, registerCreated),
                new Vector2(9f, 54f),
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.82f),
                addComponent);
            ConfigureCoreBar(
                EnsureRect("Core Energy Horizontal", core, registerCreated),
                new Vector2(54f, 9f),
                PulseForgeUITheme.WithAlpha(PulseForgeUITheme.Primary, 0.48f),
                addComponent);
            ConfigureCorePlate(
                EnsureRect("Ember Heart", core, registerCreated),
                new Vector2(22f, 22f),
                45f,
                PulseForgeUITheme.Strike,
                PulseForgeUITheme.Perfect,
                addComponent);

            guardAnchor = ConfigureAnchor(
                EnsureRect("Guard VFX Anchor", core, registerCreated),
                Vector2.zero,
                new Vector2(132f, 132f));
            attackAnchor = ConfigureAnchor(
                EnsureRect("Attack VFX Anchor", core, registerCreated),
                new Vector2(54f, 0f),
                new Vector2(24f, 24f));
            dodgeAnchor = ConfigureAnchor(
                EnsureRect("Dodge VFX Anchor", core, registerCreated),
                new Vector2(-54f, 0f),
                new Vector2(24f, 24f));

            Transform labelTransform = core.Find("Player");
            Text label = labelTransform == null ? null : labelTransform.GetComponent<Text>();
            if (label != null)
            {
                Stretch(label.rectTransform);
                label.fontSize = 11;
                label.fontStyle = FontStyle.Bold;
                label.color = PulseForgeUITheme.PrimaryText;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8;
                label.resizeTextMaxSize = 11;
                Outline labelOutline = EnsureComponent<Outline>(label.gameObject, addComponent);
                labelOutline.effectColor = new Color(0f, 0f, 0f, 0.92f);
                labelOutline.effectDistance = new Vector2(2f, -2f);
                labelOutline.useGraphicAlpha = true;
                label.rectTransform.SetAsLastSibling();
            }
        }

        private static void ConfigureCorePlate(
            RectTransform rect,
            Vector2 size,
            float rotation,
            Color fill,
            Color edge,
            Func<GameObject, Type, Component> addComponent)
        {
            Center(rect, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = EnsureComponent<Image>(rect.gameObject, addComponent);
            image.color = fill;
            image.raycastTarget = false;
            Outline outline = EnsureComponent<Outline>(rect.gameObject, addComponent);
            outline.effectColor = edge;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private static void ConfigureCoreBar(
            RectTransform rect,
            Vector2 size,
            Color color,
            Func<GameObject, Type, Component> addComponent)
        {
            Center(rect, size);
            Image image = EnsureComponent<Image>(rect.gameObject, addComponent);
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform ConfigureAnchor(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            Center(rect, size);
            rect.anchoredPosition = position;
            return rect;
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
                    throw new InvalidOperationException(name + " must use RectTransform.");
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
            return existing != null
                ? existing
                : addComponent == null
                    ? gameObject.AddComponent<T>()
                    : (T)addComponent(gameObject, typeof(T));
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
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
