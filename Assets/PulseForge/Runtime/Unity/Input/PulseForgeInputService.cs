using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PulseForge.Runtime.Unity.Input
{
    public enum PulseForgeInputAction
    {
        Guard = 0,
        LightAttack = 1,
        Strike = LightAttack,
        Dodge = 2,
        HeavyAttack = 3,
        Pause = 4
    }

    public sealed class PulseForgeInputService : IDisposable
    {
        private readonly InputActionMap actionMap;
        private readonly InputAction guardAction;
        private readonly InputAction lightAttackAction;
        private readonly InputAction dodgeAction;
        private readonly InputAction heavyAttackAction;
        private readonly InputAction pauseAction;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;

        public PulseForgeInputService()
        {
            actionMap = new InputActionMap("PulseForge Gameplay");
            guardAction = actionMap.AddAction(
                "Guard",
                InputActionType.Button,
                "<Keyboard>/space");
            lightAttackAction = actionMap.AddAction(
                "LightAttack",
                InputActionType.Button,
                "<Keyboard>/j");
            dodgeAction = actionMap.AddAction(
                "Dodge",
                InputActionType.Button,
                "<Keyboard>/k");
            heavyAttackAction = actionMap.AddAction(
                "HeavyAttack",
                InputActionType.Button,
                "<Keyboard>/l");
            pauseAction = actionMap.AddAction(
                "Pause",
                InputActionType.Button,
                "<Keyboard>/escape");
        }

        public bool IsRebinding => activeRebind != null;
        public bool GuardWasPressedThisFrame => guardAction.WasPressedThisFrame();
        public bool GuardWasReleasedThisFrame => guardAction.WasReleasedThisFrame();
        public bool GuardIsHeld => guardAction.IsPressed();
        public bool LightAttackWasPressedThisFrame => lightAttackAction.WasPressedThisFrame();
        public bool LightAttackWasReleasedThisFrame => lightAttackAction.WasReleasedThisFrame();
        public bool LightAttackIsHeld => lightAttackAction.IsPressed();
        public bool StrikeWasPressedThisFrame => LightAttackWasPressedThisFrame;
        public bool DodgeWasPressedThisFrame => dodgeAction.WasPressedThisFrame();
        public bool DodgeWasReleasedThisFrame => dodgeAction.WasReleasedThisFrame();
        public bool DodgeIsHeld => dodgeAction.IsPressed();
        public bool HeavyAttackWasPressedThisFrame => heavyAttackAction.WasPressedThisFrame();
        public bool HeavyAttackWasReleasedThisFrame => heavyAttackAction.WasReleasedThisFrame();
        public bool HeavyAttackIsHeld => heavyAttackAction.IsPressed();
        public bool PauseWasPressedThisFrame => pauseAction.WasPressedThisFrame();

        public bool WasPressedThisFrame(PulseForgeInputAction inputAction)
        {
            return GetAction(inputAction).WasPressedThisFrame();
        }

        public bool WasReleasedThisFrame(PulseForgeInputAction inputAction)
        {
            return GetAction(inputAction).WasReleasedThisFrame();
        }

        public bool IsHeld(PulseForgeInputAction inputAction)
        {
            return GetAction(inputAction).IsPressed();
        }

        public void Enable()
        {
            actionMap.Enable();
        }

        public void Disable()
        {
            CancelInteractiveRebind();
            actionMap.Disable();
        }

        public string SaveBindingOverridesAsJson()
        {
            return actionMap.SaveBindingOverridesAsJson();
        }

        public bool LoadBindingOverridesFromJson(string json)
        {
            bool wasEnabled = actionMap.enabled;
            BindingOverrideSnapshot previousBindings = CaptureBindingOverrides();
            actionMap.Disable();
            try
            {
                actionMap.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    bool hasLegacyStrike = json.IndexOf("Strike", StringComparison.Ordinal) >= 0;
                    string migratedJson = MigrateLegacyBindingOverridesJson(json);
                    actionMap.LoadBindingOverridesFromJson(migratedJson, true);
                    bool legacyStrikeApplied;
                    TryApplyNamedBindingOverrides(json, out legacyStrikeApplied);

                    if (hasLegacyStrike && !legacyStrikeApplied)
                    {
                        lightAttackAction.RemoveBindingOverride(0);
                        Debug.LogWarning(
                            "PulseForge Strike binding could not be migrated; Light Attack was reset.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                actionMap.RemoveAllBindingOverrides();
                RestoreOverride(guardAction, previousBindings.Guard);
                RestoreOverride(dodgeAction, previousBindings.Dodge);
                RestoreOverride(heavyAttackAction, previousBindings.HeavyAttack);
                RestoreOverride(pauseAction, previousBindings.Pause);
                bool legacyStrikeApplied;
                TryApplyNamedBindingOverrides(json, out legacyStrikeApplied);
                if (json != null
                    && json.IndexOf("Strike", StringComparison.Ordinal) >= 0
                    && !legacyStrikeApplied)
                {
                    lightAttackAction.RemoveBindingOverride(0);
                }
                Debug.LogWarning(
                    "PulseForge Light Attack binding was reset; other bindings were preserved: "
                    + exception.Message);
                return false;
            }
            finally
            {
                if (wasEnabled)
                {
                    actionMap.Enable();
                }
            }
        }

        public void ResetBindings()
        {
            CancelInteractiveRebind();
            actionMap.RemoveAllBindingOverrides();
        }

        public string GetBindingDisplayString(PulseForgeInputAction inputAction)
        {
            return GetAction(inputAction).GetBindingDisplayString(0);
        }

        public void BeginInteractiveRebind(
            PulseForgeInputAction inputAction,
            Action<bool, string> completed)
        {
            CancelInteractiveRebind();
            InputAction action = GetAction(inputAction);
            string previousOverridePath = action.bindings[0].overridePath;
            action.Disable();

            activeRebind = action.PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/f1")
                .OnCancel(operation =>
                {
                    FinishRebind(operation, action);
                    completed?.Invoke(false, "Rebind cancelled.");
                })
                .OnComplete(operation =>
                {
                    string conflict = FindConflict(action);
                    if (!string.IsNullOrEmpty(conflict))
                    {
                        RestoreOverride(action, previousOverridePath);
                        FinishRebind(operation, action);
                        completed?.Invoke(false, "That key is already used by " + conflict + ".");
                        return;
                    }

                    string display = action.GetBindingDisplayString(0);
                    FinishRebind(operation, action);
                    completed?.Invoke(true, action.name + " bound to " + display + ".");
                });
            activeRebind.Start();
        }

        public void CancelInteractiveRebind()
        {
            activeRebind?.Cancel();
        }

        public void Dispose()
        {
            CancelInteractiveRebind();
            actionMap.Dispose();
        }

        public static string MigrateLegacyBindingOverridesJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            return json
                .Replace("\"Strike\"", "\"LightAttack\"")
                .Replace("/Strike\"", "/LightAttack\"");
        }

        private InputAction GetAction(PulseForgeInputAction inputAction)
        {
            switch (inputAction)
            {
                case PulseForgeInputAction.Guard:
                    return guardAction;
                case PulseForgeInputAction.LightAttack:
                    return lightAttackAction;
                case PulseForgeInputAction.Dodge:
                    return dodgeAction;
                case PulseForgeInputAction.HeavyAttack:
                    return heavyAttackAction;
                default:
                    return pauseAction;
            }
        }

        private string FindConflict(InputAction changedAction)
        {
            string changedPath = changedAction.bindings[0].effectivePath;
            if (string.IsNullOrWhiteSpace(changedPath))
            {
                return "another action";
            }

            foreach (InputAction action in actionMap.actions)
            {
                if (action == changedAction)
                {
                    continue;
                }

                string path = action.bindings[0].effectivePath;
                if (string.Equals(changedPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return action.name;
                }
            }

            return string.Empty;
        }

        private void FinishRebind(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputAction action)
        {
            if (activeRebind == operation)
            {
                activeRebind = null;
            }

            operation.Dispose();
            if (actionMap.enabled)
            {
                action.Enable();
            }
            else
            {
                actionMap.Enable();
            }
        }

        private static void RestoreOverride(InputAction action, string previousOverridePath)
        {
            if (string.IsNullOrEmpty(previousOverridePath))
            {
                action.RemoveBindingOverride(0);
            }
            else
            {
                action.ApplyBindingOverride(0, previousOverridePath);
            }
        }

        private BindingOverrideSnapshot CaptureBindingOverrides()
        {
            return new BindingOverrideSnapshot
            {
                Guard = guardAction.bindings[0].overridePath,
                Dodge = dodgeAction.bindings[0].overridePath,
                HeavyAttack = heavyAttackAction.bindings[0].overridePath,
                Pause = pauseAction.bindings[0].overridePath
            };
        }

        private bool TryApplyNamedBindingOverrides(string json, out bool legacyStrikeApplied)
        {
            legacyStrikeApplied = false;
            BindingOverrideJson root;
            try
            {
                root = JsonUtility.FromJson<BindingOverrideJson>(json);
            }
            catch (Exception)
            {
                return false;
            }

            if (root?.bindings == null)
            {
                return false;
            }

            for (int i = 0; i < root.bindings.Length; i++)
            {
                BindingOverrideRecord record = root.bindings[i];
                if (record == null
                    || string.IsNullOrWhiteSpace(record.action)
                    || string.IsNullOrWhiteSpace(record.path))
                {
                    continue;
                }

                string actionName = GetSerializedActionName(record.action);
                if (string.Equals(actionName, "Guard", StringComparison.Ordinal))
                {
                    RestoreOverride(guardAction, record.path);
                }
                else if (string.Equals(actionName, "Strike", StringComparison.Ordinal))
                {
                    RestoreOverride(lightAttackAction, record.path);
                    legacyStrikeApplied = true;
                }
                else if (string.Equals(actionName, "LightAttack", StringComparison.Ordinal))
                {
                    RestoreOverride(lightAttackAction, record.path);
                }
                else if (string.Equals(actionName, "Dodge", StringComparison.Ordinal))
                {
                    RestoreOverride(dodgeAction, record.path);
                }
                else if (string.Equals(actionName, "HeavyAttack", StringComparison.Ordinal))
                {
                    RestoreOverride(heavyAttackAction, record.path);
                }
                else if (string.Equals(actionName, "Pause", StringComparison.Ordinal))
                {
                    RestoreOverride(pauseAction, record.path);
                }
            }

            return true;
        }

        private static string GetSerializedActionName(string serializedAction)
        {
            int separatorIndex = serializedAction.LastIndexOf('/');
            return separatorIndex >= 0 && separatorIndex < serializedAction.Length - 1
                ? serializedAction.Substring(separatorIndex + 1)
                : serializedAction;
        }

        private struct BindingOverrideSnapshot
        {
            public string Guard;
            public string Dodge;
            public string HeavyAttack;
            public string Pause;
        }

        [Serializable]
        private sealed class BindingOverrideJson
        {
            public BindingOverrideRecord[] bindings;
        }

        [Serializable]
        private sealed class BindingOverrideRecord
        {
            public string action;
            public string path;
        }
    }
}
