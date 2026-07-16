using System;
using System.Collections.Generic;
using PulseForge.Runtime.Unity.Persistence;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class RadialPresentationPoolRegistry
    {
        private readonly HashSet<RadialPresentationKey> activeKeys =
            new HashSet<RadialPresentationKey>();

        public int Count => activeKeys.Count;

        public bool TryActivate(RadialPresentationKey key)
        {
            return activeKeys.Add(key);
        }

        public bool Release(RadialPresentationKey key)
        {
            return activeKeys.Remove(key);
        }

        public void Clear()
        {
            activeKeys.Clear();
        }
    }

    public sealed class RadialCombatStageView : MonoBehaviour
    {
        [SerializeField] private GameObject stageBackground;
        [SerializeField] private RectTransform directionGuidesRoot;
        [SerializeField] private RectTransform[] directionGuides = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform judgementRing;
        [SerializeField] private RectTransform playerCore;
        [SerializeField] private RectTransform compoundContainer;
        [SerializeField] private RectTransform forecastContainer;
        [SerializeField] private RectTransform groupTimingContainer;
        [SerializeField] private RectTransform encounterContainer;
        [SerializeField] private RectTransform projectileContainer;
        [SerializeField] private RadialFogPresentationView fogPresentation;
        [SerializeField] private float outerRadius = 360f;
        [SerializeField] private float judgementRadius = 155f;
        [SerializeField] private int baseEncounterPoolCapacity = 12;
        [SerializeField] private int baseProjectilePoolCapacity = 6;
        [SerializeField] private int baseCompoundPoolCapacity = 8;
        [SerializeField] private int baseForecastPoolCapacity = 12;
        [SerializeField] private int baseGroupTimingPoolCapacity = 8;
        [SerializeField] private int poolSafetyMargin = 4;

        private readonly List<RadialEncounterView> encounterPool =
            new List<RadialEncounterView>();
        private readonly List<RadialProjectileView> projectilePool =
            new List<RadialProjectileView>();
        private readonly List<RadialCompoundGroupView> compoundPool =
            new List<RadialCompoundGroupView>();
        private readonly List<RadialForecastView> forecastPool =
            new List<RadialForecastView>();
        private readonly List<RadialGroupTimingView> groupTimingPool =
            new List<RadialGroupTimingView>();
        private readonly Dictionary<RadialPresentationKey, RadialEncounterView> activeEncounters =
            new Dictionary<RadialPresentationKey, RadialEncounterView>();
        private readonly Dictionary<RadialPresentationKey, RadialProjectileView> activeProjectiles =
            new Dictionary<RadialPresentationKey, RadialProjectileView>();
        private readonly Dictionary<RadialPresentationKey, RadialCompoundGroupView> activeCompounds =
            new Dictionary<RadialPresentationKey, RadialCompoundGroupView>();
        private readonly Dictionary<RadialPresentationKey, RadialForecastView> activeForecasts =
            new Dictionary<RadialPresentationKey, RadialForecastView>();
        private readonly Dictionary<RadialPresentationKey, RadialGroupTimingView> activeGroupTimings =
            new Dictionary<RadialPresentationKey, RadialGroupTimingView>();
        private readonly List<RadialPresentationKey> releaseScratch =
            new List<RadialPresentationKey>();
        private readonly RadialPresentationPoolRegistry encounterRegistry =
            new RadialPresentationPoolRegistry();
        private readonly RadialPresentationPoolRegistry projectileRegistry =
            new RadialPresentationPoolRegistry();
        private readonly RadialPresentationPoolRegistry compoundRegistry =
            new RadialPresentationPoolRegistry();
        private readonly RadialPresentationPoolRegistry forecastRegistry =
            new RadialPresentationPoolRegistry();
        private readonly RadialPresentationPoolRegistry groupTimingRegistry =
            new RadialPresentationPoolRegistry();

        private bool uiStateVisible;
        private bool radialSessionVisible;
        private bool encounterPoolWarningLogged;
        private bool projectilePoolWarningLogged;
        private bool compoundPoolWarningLogged;
        private bool forecastPoolWarningLogged;
        private bool groupTimingPoolWarningLogged;
        private RadialReadabilityMode appliedReadabilityMode = (RadialReadabilityMode)(-1);

        public GameObject StageBackground => stageBackground;
        public RectTransform DirectionGuidesRoot => directionGuidesRoot;
        public IReadOnlyList<RectTransform> DirectionGuides => directionGuides;
        public RectTransform JudgementRing => judgementRing;
        public RectTransform PlayerCore => playerCore;
        public RectTransform CompoundContainer => compoundContainer;
        public RectTransform ForecastContainer => forecastContainer;
        public RectTransform GroupTimingContainer => groupTimingContainer;
        public RectTransform EncounterContainer => encounterContainer;
        public RectTransform ProjectileContainer => projectileContainer;
        public RadialFogPresentationView FogPresentation => fogPresentation;
        public float OuterRadius => outerRadius;
        public float JudgementRadius => judgementRadius;
        public int PoolSafetyMargin => Mathf.Max(0, poolSafetyMargin);
        public int ActiveEncounterCount => activeEncounters.Count;
        public int ActiveProjectileCount => activeProjectiles.Count;
        public int ActiveCompoundCount => activeCompounds.Count;
        public int ActiveForecastCount => activeForecasts.Count;
        public int ActiveGroupTimingCount => activeGroupTimings.Count;
        public int EncounterPoolCount => encounterPool.Count;
        public int ProjectilePoolCount => projectilePool.Count;
        public int CompoundPoolCount => compoundPool.Count;
        public int ForecastPoolCount => forecastPool.Count;
        public int GroupTimingPoolCount => groupTimingPool.Count;

        public void Configure(
            GameObject background,
            RectTransform guidesRoot,
            RectTransform[] guides,
            RectTransform ring,
            RectTransform core,
            RectTransform encounters,
            RectTransform projectiles)
        {
            stageBackground = background;
            directionGuidesRoot = guidesRoot;
            directionGuides = guides ?? Array.Empty<RectTransform>();
            judgementRing = ring;
            playerCore = core;
            encounterContainer = encounters;
            projectileContainer = projectiles;
        }

        public void ConfigureCompoundContainer(RectTransform compounds)
        {
            compoundContainer = compounds;
        }

        public void ConfigureForecastContainer(RectTransform forecasts)
        {
            forecastContainer = forecasts;
        }

        public void ConfigureGroupTimingContainer(RectTransform groupTimings)
        {
            groupTimingContainer = groupTimings;
        }

        public void ConfigureFogPresentation(RadialFogPresentationView value)
        {
            fogPresentation = value;
        }

        public void RenderFog(
            PulseForge.Domain.Rhythm.RadialStatusEffectSnapshot status,
            double songTimeSeconds)
        {
            fogPresentation?.Render(status, songTimeSeconds);
        }

        public void ShowSaboteurSmoke(string encounterId, double songTimeSeconds)
        {
            fogPresentation?.ShowSaboteurSmoke(encounterId, songTimeSeconds);
        }

        public void SetUIStateVisibility(PulseForgeUIState state)
        {
            uiStateVisible = state == PulseForgeUIState.Countdown
                || state == PulseForgeUIState.Playing
                || state == PulseForgeUIState.Paused
                || state == PulseForgeUIState.Completed
                || state == PulseForgeUIState.Failed;
            ApplyEffectiveVisibility();
        }

        public void SetRadialSessionVisible(bool visible)
        {
            if (radialSessionVisible == visible)
            {
                ApplyEffectiveVisibility();
                return;
            }
            radialSessionVisible = visible;
            if (!visible)
            {
                ResetPresentation();
            }
            ApplyEffectiveVisibility();
        }

        public void InitializePools(int requiredEncounterCount, int requiredProjectileCount)
        {
            InitializePools(requiredEncounterCount, requiredProjectileCount, 0, 0, 0);
        }

        public void InitializePools(
            int requiredEncounterCount,
            int requiredProjectileCount,
            int requiredCompoundCount)
        {
            InitializePools(requiredEncounterCount, requiredProjectileCount, requiredCompoundCount, 0, 0);
        }

        public void InitializePools(
            int requiredEncounterCount,
            int requiredProjectileCount,
            int requiredCompoundCount,
            int requiredForecastCount)
        {
            InitializePools(
                requiredEncounterCount,
                requiredProjectileCount,
                requiredCompoundCount,
                requiredForecastCount,
                0);
        }

        public void InitializePools(
            int requiredEncounterCount,
            int requiredProjectileCount,
            int requiredCompoundCount,
            int requiredForecastCount,
            int requiredGroupTimingCount)
        {
            int encounterCapacity = Mathf.Max(
                baseEncounterPoolCapacity,
                requiredEncounterCount + PoolSafetyMargin);
            int projectileCapacity = Mathf.Max(
                baseProjectilePoolCapacity,
                requiredProjectileCount + PoolSafetyMargin);
            int compoundCapacity = Mathf.Max(
                baseCompoundPoolCapacity,
                requiredCompoundCount + PoolSafetyMargin);
            int forecastCapacity = Mathf.Max(
                baseForecastPoolCapacity,
                requiredForecastCount + PoolSafetyMargin);
            int groupTimingCapacity = Mathf.Max(
                baseGroupTimingPoolCapacity,
                requiredGroupTimingCount + PoolSafetyMargin);
            while (encounterPool.Count < encounterCapacity)
            {
                encounterPool.Add(RadialEncounterView.Create(
                    encounterContainer,
                    encounterPool.Count));
            }
            while (projectilePool.Count < projectileCapacity)
            {
                projectilePool.Add(RadialProjectileView.Create(
                    projectileContainer,
                    projectilePool.Count));
            }
            while (compoundContainer != null && compoundPool.Count < compoundCapacity)
            {
                compoundPool.Add(RadialCompoundGroupView.Create(
                    compoundContainer,
                    compoundPool.Count));
            }
            while (forecastContainer != null && forecastPool.Count < forecastCapacity)
            {
                forecastPool.Add(RadialForecastView.Create(
                    forecastContainer,
                    forecastPool.Count));
            }
            while (groupTimingContainer != null && groupTimingPool.Count < groupTimingCapacity)
            {
                groupTimingPool.Add(RadialGroupTimingView.Create(
                    groupTimingContainer,
                    groupTimingPool.Count));
            }
            encounterPoolWarningLogged = false;
            projectilePoolWarningLogged = false;
            compoundPoolWarningLogged = false;
            forecastPoolWarningLogged = false;
            groupTimingPoolWarningLogged = false;
        }

        public bool TryAcquireGroupTiming(
            RadialPresentationKey key,
            out RadialGroupTimingView view)
        {
            if (activeGroupTimings.TryGetValue(key, out view))
            {
                return true;
            }
            if (!groupTimingRegistry.TryActivate(key))
            {
                view = null;
                return false;
            }

            view = FindFreeGroupTimingView();
            if (view == null)
            {
                groupTimingRegistry.Release(key);
                if (!groupTimingPoolWarningLogged)
                {
                    groupTimingPoolWarningLogged = true;
                    Debug.LogWarning("Radial group timing pool capacity was exhausted.", this);
                }
                return false;
            }

            activeGroupTimings.Add(key, view);
            return true;
        }

        public bool TryAcquireForecast(
            RadialPresentationKey key,
            out RadialForecastView view)
        {
            if (activeForecasts.TryGetValue(key, out view))
            {
                return true;
            }
            if (!forecastRegistry.TryActivate(key))
            {
                view = null;
                return false;
            }

            view = FindFreeForecastView();
            if (view == null)
            {
                forecastRegistry.Release(key);
                if (!forecastPoolWarningLogged)
                {
                    forecastPoolWarningLogged = true;
                    Debug.LogWarning("Radial forecast pool capacity was exhausted.", this);
                }
                return false;
            }

            activeForecasts.Add(key, view);
            return true;
        }

        public bool TryAcquireEncounter(
            RadialPresentationKey key,
            out RadialEncounterView view)
        {
            if (activeEncounters.TryGetValue(key, out view))
            {
                return true;
            }
            if (!encounterRegistry.TryActivate(key))
            {
                view = null;
                return false;
            }

            view = FindFreeEncounterView();
            if (view == null)
            {
                encounterRegistry.Release(key);
                if (!encounterPoolWarningLogged)
                {
                    encounterPoolWarningLogged = true;
                    Debug.LogWarning("Radial encounter pool capacity was exhausted.", this);
                }
                return false;
            }

            activeEncounters.Add(key, view);
            return true;
        }

        public bool TryAcquireProjectile(
            RadialPresentationKey key,
            out RadialProjectileView view)
        {
            if (activeProjectiles.TryGetValue(key, out view))
            {
                return true;
            }
            if (!projectileRegistry.TryActivate(key))
            {
                view = null;
                return false;
            }

            view = FindFreeProjectileView();
            if (view == null)
            {
                projectileRegistry.Release(key);
                if (!projectilePoolWarningLogged)
                {
                    projectilePoolWarningLogged = true;
                    Debug.LogWarning("Radial projectile pool capacity was exhausted.", this);
                }
                return false;
            }

            activeProjectiles.Add(key, view);
            return true;
        }

        public bool TryAcquireCompound(
            RadialPresentationKey key,
            out RadialCompoundGroupView view)
        {
            if (activeCompounds.TryGetValue(key, out view))
            {
                return true;
            }
            if (!compoundRegistry.TryActivate(key))
            {
                view = null;
                return false;
            }

            view = FindFreeCompoundView();
            if (view == null)
            {
                compoundRegistry.Release(key);
                if (!compoundPoolWarningLogged)
                {
                    compoundPoolWarningLogged = true;
                    Debug.LogWarning("Radial compound group pool capacity was exhausted.", this);
                }
                return false;
            }

            activeCompounds.Add(key, view);
            return true;
        }

        public void ReleaseEncountersExcept(ISet<RadialPresentationKey> desiredKeys)
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialPresentationKey, RadialEncounterView> pair in activeEncounters)
            {
                if (desiredKeys == null || !desiredKeys.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < releaseScratch.Count; i++)
            {
                ReleaseEncounter(releaseScratch[i]);
            }
        }

        public void ReleaseProjectilesExcept(ISet<RadialPresentationKey> desiredKeys)
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialPresentationKey, RadialProjectileView> pair in activeProjectiles)
            {
                if (desiredKeys == null || !desiredKeys.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < releaseScratch.Count; i++)
            {
                ReleaseProjectile(releaseScratch[i]);
            }
        }

        public void ReleaseCompoundsExcept(ISet<RadialPresentationKey> desiredKeys)
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialPresentationKey, RadialCompoundGroupView> pair in activeCompounds)
            {
                if (desiredKeys == null || !desiredKeys.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < releaseScratch.Count; i++)
            {
                ReleaseCompound(releaseScratch[i]);
            }
        }

        public void ReleaseForecastsExcept(ISet<RadialPresentationKey> desiredKeys)
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialPresentationKey, RadialForecastView> pair in activeForecasts)
            {
                if (desiredKeys == null || !desiredKeys.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < releaseScratch.Count; i++)
            {
                ReleaseForecast(releaseScratch[i]);
            }
        }

        public void ReleaseGroupTimingsExcept(ISet<RadialPresentationKey> desiredKeys)
        {
            releaseScratch.Clear();
            foreach (KeyValuePair<RadialPresentationKey, RadialGroupTimingView> pair in activeGroupTimings)
            {
                if (desiredKeys == null || !desiredKeys.Contains(pair.Key))
                {
                    releaseScratch.Add(pair.Key);
                }
            }
            for (int i = 0; i < releaseScratch.Count; i++)
            {
                ReleaseGroupTiming(releaseScratch[i]);
            }
        }

        public void ResetPresentation()
        {
            if (judgementRing != null)
            {
                judgementRing.localScale = Vector3.one;
            }
            for (int i = 0; i < encounterPool.Count; i++)
            {
                encounterPool[i].ResetView();
            }
            for (int i = 0; i < projectilePool.Count; i++)
            {
                projectilePool[i].ResetView();
            }
            for (int i = 0; i < compoundPool.Count; i++)
            {
                compoundPool[i].ResetView();
            }
            for (int i = 0; i < forecastPool.Count; i++)
            {
                forecastPool[i].ResetView();
            }
            for (int i = 0; i < groupTimingPool.Count; i++)
            {
                groupTimingPool[i].ResetView();
            }
            activeEncounters.Clear();
            activeProjectiles.Clear();
            activeCompounds.Clear();
            activeForecasts.Clear();
            activeGroupTimings.Clear();
            encounterRegistry.Clear();
            projectileRegistry.Clear();
            compoundRegistry.Clear();
            forecastRegistry.Clear();
            groupTimingRegistry.Clear();
            fogPresentation?.ResetView();
        }

        public void RenderBeatPulse(RadialBeatPulseVisual pulse, bool enabled)
        {
            if (judgementRing == null)
            {
                return;
            }
            judgementRing.localScale = Vector3.one * (enabled ? pulse.Scale : 1f);
        }

        public void CollectValidationErrors(List<string> errors)
        {
            PulseForgeUIValidation.AddMissing(errors, stageBackground, "Radial stage: Stage Background is missing.");
            PulseForgeUIValidation.AddMissing(errors, directionGuidesRoot, "Radial stage: Direction Guides is missing.");
            if (directionGuides == null || directionGuides.Length != 8)
            {
                errors.Add("Radial stage: exactly eight direction guides are required.");
            }
            else
            {
                for (int i = 0; i < directionGuides.Length; i++)
                {
                    PulseForgeUIValidation.AddMissing(
                        errors,
                        directionGuides[i],
                        "Radial stage: direction guide " + i + " is missing.");
                }
            }
            PulseForgeUIValidation.AddMissing(errors, judgementRing, "Radial stage: Judgement Ring is missing.");
            PulseForgeUIValidation.AddMissing(errors, fogPresentation, "Radial stage: Fog Presentation is missing.");
            PulseForgeUIValidation.AddMissing(errors, playerCore, "Radial stage: Player Core is missing.");
            PulseForgeUIValidation.AddMissing(errors, compoundContainer, "Radial stage: Compound Container is missing.");
            PulseForgeUIValidation.AddMissing(errors, forecastContainer, "Radial stage: Forecast Layer is missing.");
            PulseForgeUIValidation.AddMissing(errors, groupTimingContainer, "Radial stage: Group Timing Container is missing.");
            PulseForgeUIValidation.AddMissing(errors, encounterContainer, "Radial stage: Encounter Container is missing.");
            PulseForgeUIValidation.AddMissing(errors, projectileContainer, "Radial stage: Projectile Container is missing.");
        }

        private void ReleaseEncounter(RadialPresentationKey key)
        {
            if (activeEncounters.TryGetValue(key, out RadialEncounterView view))
            {
                view.ResetView();
                activeEncounters.Remove(key);
            }
            encounterRegistry.Release(key);
        }

        private void ReleaseProjectile(RadialPresentationKey key)
        {
            if (activeProjectiles.TryGetValue(key, out RadialProjectileView view))
            {
                view.ResetView();
                activeProjectiles.Remove(key);
            }
            projectileRegistry.Release(key);
        }

        private void ReleaseCompound(RadialPresentationKey key)
        {
            if (activeCompounds.TryGetValue(key, out RadialCompoundGroupView view))
            {
                view.ResetView();
                activeCompounds.Remove(key);
            }
            compoundRegistry.Release(key);
        }

        private void ReleaseForecast(RadialPresentationKey key)
        {
            if (activeForecasts.TryGetValue(key, out RadialForecastView view))
            {
                view.ResetView();
                activeForecasts.Remove(key);
            }
            forecastRegistry.Release(key);
        }

        private void ReleaseGroupTiming(RadialPresentationKey key)
        {
            if (activeGroupTimings.TryGetValue(key, out RadialGroupTimingView view))
            {
                view.ResetView();
                activeGroupTimings.Remove(key);
            }
            groupTimingRegistry.Release(key);
        }

        private RadialEncounterView FindFreeEncounterView()
        {
            for (int i = 0; i < encounterPool.Count; i++)
            {
                if (!encounterPool[i].IsInUse)
                {
                    return encounterPool[i];
                }
            }
            return null;
        }

        private RadialProjectileView FindFreeProjectileView()
        {
            for (int i = 0; i < projectilePool.Count; i++)
            {
                if (!projectilePool[i].IsInUse)
                {
                    return projectilePool[i];
                }
            }
            return null;
        }

        private RadialCompoundGroupView FindFreeCompoundView()
        {
            for (int i = 0; i < compoundPool.Count; i++)
            {
                if (!compoundPool[i].IsInUse)
                {
                    return compoundPool[i];
                }
            }
            return null;
        }

        private RadialForecastView FindFreeForecastView()
        {
            for (int i = 0; i < forecastPool.Count; i++)
            {
                if (!forecastPool[i].IsInUse)
                {
                    return forecastPool[i];
                }
            }
            return null;
        }

        private RadialGroupTimingView FindFreeGroupTimingView()
        {
            for (int i = 0; i < groupTimingPool.Count; i++)
            {
                if (!groupTimingPool[i].IsInUse)
                {
                    return groupTimingPool[i];
                }
            }
            return null;
        }

        public void ApplyReadabilityMode(RadialReadabilityMode readabilityMode)
        {
            if (appliedReadabilityMode == readabilityMode)
            {
                return;
            }
            appliedReadabilityMode = readabilityMode;
            bool highClarity = readabilityMode == RadialReadabilityMode.HighClarity;
            Image backgroundImage = stageBackground == null
                ? null
                : stageBackground.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = PulseForgeUITheme.WithAlpha(
                    PulseForgeUITheme.BackgroundSecondary,
                    highClarity ? 0.52f : 0.72f);
            }
            if (directionGuides == null)
            {
                return;
            }
            for (int i = 0; i < directionGuides.Length; i++)
            {
                Image guide = directionGuides[i] == null
                    ? null
                    : directionGuides[i].gameObject.GetComponent<Image>();
                if (guide != null)
                {
                    guide.color = PulseForgeUITheme.WithAlpha(
                        PulseForgeUITheme.Divider,
                        highClarity ? 0.28f : 0.58f);
                }
            }
        }

        private void ApplyEffectiveVisibility()
        {
            bool visible = uiStateVisible && radialSessionVisible;
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
