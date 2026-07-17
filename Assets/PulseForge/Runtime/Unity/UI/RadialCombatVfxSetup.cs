using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialCombatVfxSetup
    {
        public const string PresentationRootName = "Combat VFX & Reactive Polish";
        private const string PoolRootName = "Combat VFX Pool";

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            if (root == null)
            {
                return null;
            }

            RadialCombatStageView stage = root.RadialCombatStage;
            if (stage == null
                || stage.ArenaGraphic == null
                || stage.GuardVfxAnchor == null
                || stage.AttackVfxAnchor == null
                || stage.DodgeVfxAnchor == null)
            {
                return null;
            }

            RectTransform stageRect = stage.transform as RectTransform;
            if (stageRect == null)
            {
                throw new InvalidOperationException(
                    "Radial Combat Stage must use RectTransform.");
            }

            RectTransform presentationRoot = EnsureRect(
                PresentationRootName,
                stageRect,
                registerCreated);
            Stretch(presentationRoot);
            presentationRoot.SetSiblingIndex(ResolvePresentationSiblingIndex(stage));
            RadialCombatVfxLayer layer = EnsureComponent<RadialCombatVfxLayer>(
                presentationRoot.gameObject,
                addComponent);

            RectTransform poolRoot = EnsureRect(
                PoolRootName,
                presentationRoot,
                registerCreated);
            Stretch(poolRoot);
            RadialCombatVfxView[] views = new RadialCombatVfxView[RadialVfxTokens.PoolCapacity];
            for (int i = 0; i < views.Length; i++)
            {
                RectTransform slot = EnsureRect(
                    "Combat VFX " + i.ToString("00"),
                    poolRoot,
                    registerCreated);
                Center(slot, new Vector2(340f, 340f));
                RadialCombatVfxGraphic graphic = EnsureComponent<RadialCombatVfxGraphic>(
                    slot.gameObject,
                    addComponent);
                graphic.color = Color.white;
                graphic.raycastTarget = false;
                RadialCombatVfxView view = EnsureComponent<RadialCombatVfxView>(
                    slot.gameObject,
                    addComponent);
                view.Configure(slot, graphic);
                views[i] = view;
            }

            layer.Configure(poolRoot, views);
            stage.ConfigureCombatVfx(layer);
            ConfigureFogPolish(stage, addComponent);
            return stage;
        }

        private static void ConfigureFogPolish(
            RadialCombatStageView stage,
            Func<GameObject, Type, Component> addComponent)
        {
            RadialFogPresentationView fog = stage.FogPresentation;
            GameObject overlay = fog == null ? null : fog.FogOverlayRoot;
            if (overlay == null)
            {
                return;
            }

            CanvasGroup canvasGroup = EnsureComponent<CanvasGroup>(overlay, addComponent);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            fog.ConfigurePolish(canvasGroup);
        }

        private static int ResolvePresentationSiblingIndex(RadialCombatStageView stage)
        {
            int index = stage.transform.childCount - 1;
            index = MinSibling(index, stage.ForecastContainer);
            index = MinSibling(index, stage.CompoundContainer);
            index = MinSibling(index, stage.EncounterContainer);
            index = MinSibling(index, stage.ProjectileContainer);
            index = MinSibling(index, stage.GroupTimingContainer);
            if (stage.FogPresentation != null)
            {
                index = Mathf.Min(index, stage.FogPresentation.transform.GetSiblingIndex());
            }
            return Mathf.Max(0, index);
        }

        private static int MinSibling(int current, RectTransform candidate)
        {
            return candidate == null
                ? current
                : Mathf.Min(current, candidate.GetSiblingIndex());
        }

        private static RectTransform EnsureRect(
            string name,
            Transform parent,
            Action<GameObject> registerCreated)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect == null)
                {
                    throw new InvalidOperationException(name + " must use RectTransform.");
                }
                return existingRect;
            }

            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            registerCreated?.Invoke(gameObject);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
