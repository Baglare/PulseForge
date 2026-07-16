using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.UI
{
    public static class RadialForecastSetup
    {
        public const string ForecastLayerName = "Forecast Layer";

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
            if (stage == null)
            {
                stage = RadialSaboteurFogSetup.Apply(root, registerCreated, addComponent);
            }
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

            Transform existing = stageRect.Find(ForecastLayerName);
            RectTransform forecast = existing as RectTransform;
            if (existing != null && forecast == null)
            {
                throw new InvalidOperationException(
                    "Forecast Layer must use RectTransform.");
            }
            if (forecast == null)
            {
                forecast = PulseForgeUIFactory.CreateRect(ForecastLayerName, stageRect);
                registerCreated?.Invoke(forecast.gameObject);
            }
            forecast.anchorMin = Vector2.zero;
            forecast.anchorMax = Vector2.one;
            forecast.offsetMin = Vector2.zero;
            forecast.offsetMax = Vector2.zero;
            if (stage.EncounterContainer != null)
            {
                forecast.SetSiblingIndex(stage.EncounterContainer.GetSiblingIndex());
            }
            stage.ConfigureForecastContainer(forecast);

            PulseForgeSettingsUISetup.Apply(root, registerCreated);
            root.SettingsPanel?.EnsureForecastControls(registerCreated);
            return stage;
        }
    }
}
