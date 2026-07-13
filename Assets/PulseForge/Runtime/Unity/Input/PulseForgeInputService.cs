using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PulseForge.Runtime.Unity.Input
{
    public enum PulseForgeInputAction
    {
        Guard,
        Strike,
        Pause
    }

    public sealed class PulseForgeInputService : IDisposable
    {
        private readonly InputActionMap actionMap;
        private readonly InputAction guardAction;
        private readonly InputAction strikeAction;
        private readonly InputAction pauseAction;
        private InputActionRebindingExtensions.RebindingOperation activeRebind;

        public PulseForgeInputService()
        {
            actionMap = new InputActionMap("PulseForge Gameplay");
            guardAction = actionMap.AddAction(
                "Guard",
                InputActionType.Button,
                "<Keyboard>/space");
            strikeAction = actionMap.AddAction(
                "Strike",
                InputActionType.Button,
                "<Keyboard>/j");
            pauseAction = actionMap.AddAction(
                "Pause",
                InputActionType.Button,
                "<Keyboard>/escape");
        }

        public bool IsRebinding => activeRebind != null;
        public bool GuardWasPressedThisFrame => guardAction.WasPressedThisFrame();
        public bool StrikeWasPressedThisFrame => strikeAction.WasPressedThisFrame();
        public bool PauseWasPressedThisFrame => pauseAction.WasPressedThisFrame();

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
            actionMap.Disable();
            try
            {
                actionMap.RemoveAllBindingOverrides();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    actionMap.LoadBindingOverridesFromJson(json, true);
                }

                return true;
            }
            catch (Exception exception)
            {
                actionMap.RemoveAllBindingOverrides();
                Debug.LogWarning("PulseForge input binding overrides were reset: " + exception.Message);
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

        private InputAction GetAction(PulseForgeInputAction inputAction)
        {
            switch (inputAction)
            {
                case PulseForgeInputAction.Guard:
                    return guardAction;
                case PulseForgeInputAction.Strike:
                    return strikeAction;
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
    }
}
