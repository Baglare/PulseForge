using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialGroupTimingSetup
    {
        public const string GroupTimingContainerName = "Group Timing Container";

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            if (root == null)
            {
                return null;
            }

            RadialCombatStageView stage = RadialForecastSetup.Apply(
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

            Transform existing = stageRect.Find(GroupTimingContainerName);
            RectTransform container = existing as RectTransform;
            if (existing != null && container == null)
            {
                throw new InvalidOperationException(
                    "Group Timing Container must use RectTransform.");
            }
            if (container == null)
            {
                container = PulseForgeUIFactory.CreateRect(
                    GroupTimingContainerName,
                    stageRect);
                registerCreated?.Invoke(container.gameObject);
            }

            container.anchorMin = Vector2.zero;
            container.anchorMax = Vector2.one;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
            container.SetAsLastSibling();
            stage.ConfigureGroupTimingContainer(container);
            return stage;
        }
    }
}
