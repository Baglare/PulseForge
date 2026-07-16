using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialCompoundVisualSetup
    {
        public const string CompoundContainerName = "Compound Container";

        public static RadialCombatStageView Apply(
            PulseForgeSceneUIRoot root,
            Action<GameObject> registerCreated = null,
            Func<GameObject, Type, Component> addComponent = null)
        {
            RadialCombatStageView stage = RadialCombatStageSetup.Apply(
                root,
                registerCreated,
                addComponent);
            if (stage == null)
            {
                return null;
            }

            Transform existing = stage.transform.Find(CompoundContainerName);
            RectTransform compoundContainer;
            if (existing != null)
            {
                compoundContainer = existing as RectTransform;
                if (compoundContainer == null)
                {
                    throw new InvalidOperationException(
                        "Radial compound container must use RectTransform.");
                }
            }
            else
            {
                compoundContainer = PulseForgeUIFactory.CreateRect(
                    CompoundContainerName,
                    stage.transform);
                registerCreated?.Invoke(compoundContainer.gameObject);
            }

            compoundContainer.anchorMin = Vector2.zero;
            compoundContainer.anchorMax = Vector2.one;
            compoundContainer.offsetMin = Vector2.zero;
            compoundContainer.offsetMax = Vector2.zero;
            if (stage.EncounterContainer != null)
            {
                compoundContainer.SetSiblingIndex(stage.EncounterContainer.GetSiblingIndex());
            }
            stage.ConfigureCompoundContainer(compoundContainer);
            return stage;
        }
    }
}
