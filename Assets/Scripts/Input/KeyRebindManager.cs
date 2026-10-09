using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastKnight.Input
{
    public class RebindableActionInfo
    {
        public string ActionName { get; set; }
        public int BindingIndex { get; set; }
        public string LocalizationKey { get; set; }

        public RebindableActionInfo(string actionName, int bindingIndex, string locKey)
        {
            ActionName = actionName;
            BindingIndex = bindingIndex;
            LocalizationKey = locKey;
        }
    }

    public static class KeyRebindManager
    {
        private const string PrefKey = "TheLastKnight_BindingOverridesJson";
        private static InputActionRebindingExtensions.RebindingOperation _currentRebindOp;
        public static bool IsRebinding => _currentRebindOp != null;
        public static event Action OnBindingsChanged;
        public static int RebindFinishedFrame { get; private set; } = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoLoad()
        {
            LoadSavedBindings();
            InputSystem.actions?.FindActionMap("Shortcuts")?.Enable();
        }

        public static List<RebindableActionInfo> GetRebindableActions()
        {
            return new List<RebindableActionInfo>
            {
                new RebindableActionInfo("Move", 6, "ACTION_MOVE_LEFT"),
                new RebindableActionInfo("Move", 8, "ACTION_MOVE_RIGHT"),
                new RebindableActionInfo("Move", 7, "ACTION_MOVE_LEFT_ALT"),
                new RebindableActionInfo("Move", 9, "ACTION_MOVE_RIGHT_ALT"),
                new RebindableActionInfo("Jump", 0, "ACTION_JUMP"),
                new RebindableActionInfo("Sprint", 0, "ACTION_SPRINT"),
                new RebindableActionInfo("Sprint", 1, "ACTION_SPRINT_ALT"),
                new RebindableActionInfo("Attack", 1, "ACTION_ATTACK"),
                new RebindableActionInfo("Attack", 5, "ACTION_ATTACK_ALT"),
                new RebindableActionInfo("Dash", 0, "ACTION_DASH"),
                new RebindableActionInfo("Dash", 2, "ACTION_DASH_ALT"),
                new RebindableActionInfo("CounterAttack", 0, "ACTION_PARRY"),
                new RebindableActionInfo("UseDrink", 0, "ACTION_DRINK"),
                new RebindableActionInfo("UseSkill", 0, "ACTION_SKILL"),
                new RebindableActionInfo("UseBuff", 0, "ACTION_BUFF"),
                new RebindableActionInfo("UseExcalibur", 0, "ACTION_EXCALIBUR"),
                new RebindableActionInfo("Interact", 0, "ACTION_INTERACT"),
                new RebindableActionInfo("Previous", 0, "ACTION_PREVIOUS"),
                new RebindableActionInfo("Next", 0, "ACTION_NEXT"),
                new RebindableActionInfo("ToggleStatus", 0, "ACTION_STATUS"),
                new RebindableActionInfo("ToggleMap", 0, "ACTION_MAP"),
                new RebindableActionInfo("SwitchMapView", 0, "ACTION_MAP_VIEW"),
                new RebindableActionInfo("Recall", 0, "ACTION_RECALL"),
                new RebindableActionInfo("DropItem", 0, "ACTION_DROP_ITEM")
            };
        }

        public static bool WasPressedThisFrame(string actionName)
        {
            var action = InputSystem.actions?.FindAction(actionName);
            // Menu shortcuts must remain available to close their own windows while gameplay is blocked.
            if (action != null && action.actionMap.name == "Shortcuts" && !action.enabled) action.Enable();
            return action != null && action.enabled && action.WasPressedThisFrame();
        }

        public static string UpdateBindingHints(string text)
        {
            if (string.IsNullOrEmpty(text) || InputSystem.actions == null) return text;
            // Binding badges already contain the current key; only rewrite explanatory text.
            if (Regex.IsMatch(text, @"^\[\s*\w+\s*\]$")) return text;
            return Regex.Replace(text, @"(?<![\w])(?:\[\s*[QFET]\s*\]|F(?=  | พักผ่อน| สำรวจ| บังคับ| หรือ| Rest| Examine| Respawn| or click| to continue| เพื่อไปต่อ))", match =>
            {
                string token = match.Value.Trim('[', ']', ' ');
                string action = token == "F" ? "Interact" : token == "Q" ? "UseDrink" : token == "E" ? "UseSkill" : "UseExcalibur";
                return match.Value.Replace(token, GetCurrentBindingDisplay(action, 0));
            });
        }

        public static string GetCurrentBindingDisplay(string actionName, int bindingIndex)
        {
            if (InputSystem.actions == null) return TheLastKnight.Core.LocalizationManager.Translate("Unknown");
            var action = InputSystem.actions.FindAction(actionName);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return "N/A";

            var binding = action.bindings[bindingIndex];
            string path = binding.effectivePath;
            return FormatPathToEnglish(path);
        }

        public static string FormatPathToEnglish(string path)
        {
            if (string.IsNullOrEmpty(path)) return "None";

            // Mouse buttons
            if (path.Equals("<Mouse>/leftButton", StringComparison.OrdinalIgnoreCase)) return "LMB";
            if (path.Equals("<Mouse>/rightButton", StringComparison.OrdinalIgnoreCase)) return "RMB";
            if (path.Equals("<Mouse>/middleButton", StringComparison.OrdinalIgnoreCase)) return "MMB";
            if (path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase))
            {
                string mName = path.Substring("<Mouse>/".Length);
                return mName.ToUpperInvariant();
            }

            // Keyboard keys
            if (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                string key = path.Substring("<Keyboard>/".Length);
                if (key.Length == 1) return key.ToUpperInvariant();
                switch (key.ToLowerInvariant())
                {
                    case "space": return "Space";
                    case "leftshift": return "Shift";
                    case "rightshift": return "Right Shift";
                    case "leftctrl": return "Ctrl";
                    case "rightctrl": return "Right Ctrl";
                    case "leftalt": return "Alt";
                    case "rightalt": return "Right Alt";
                    case "enter": return "Enter";
                    case "escape": return "Esc";
                    case "tab": return "Tab";
                    case "backspace": return "Backspace";
                    case "capslock": return "Caps Lock";
                    case "uparrow": return "Up Arrow";
                    case "downarrow": return "Down Arrow";
                    case "leftarrow": return "Left Arrow";
                    case "rightarrow": return "Right Arrow";
                    case "comma": return ",";
                    case "period": return ".";
                    case "slash": return "/";
                    case "semicolon": return ";";
                    case "quote": return "'";
                    case "backslash": return "\\";
                    case "leftbracket": return "[";
                    case "rightbracket": return "]";
                    case "minus": return "-";
                    case "equals": return "=";
                    case "backquote": return "`";
                    default:
                        if (key.StartsWith("numpad", StringComparison.OrdinalIgnoreCase))
                            return "Num " + key.Substring(6).ToUpperInvariant();
                        return char.ToUpperInvariant(key[0]) + key.Substring(1);
                }
            }

            // Gamepad
            if (path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase))
            {
                string btn = path.Substring("<Gamepad>/".Length);
                switch (btn.ToLowerInvariant())
                {
                    case "buttonsouth": return "A / Cross";
                    case "buttonwest": return "X / Square";
                    case "buttonnorth": return "Y / Triangle";
                    case "buttoneast": return "B / Circle";
                    case "leftshoulder": return "LB";
                    case "rightshoulder": return "RB";
                    case "lefttrigger": return "LT";
                    case "righttrigger": return "RT";
                    default:
                        return "GP: " + btn;
                }
            }

            return path;
        }

        public static void StartRebind(RebindableActionInfo item, Action onComplete, Action onCancel = null)
        {
            if (InputSystem.actions == null)
            {
                onCancel?.Invoke();
                return;
            }

            var action = InputSystem.actions.FindAction(item.ActionName);
            if (action == null || item.BindingIndex < 0 || item.BindingIndex >= action.bindings.Count || action.bindings[item.BindingIndex].isComposite)
            {
                onCancel?.Invoke();
                return;
            }

            CancelOngoingRebind();
            bool wasEnabled = action.enabled;
            action.Disable();

            _currentRebindOp = action.PerformInteractiveRebinding(item.BindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Mouse>/clickCount")
                .WithExpectedControlType("Button")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    op.Dispose();
                    _currentRebindOp = null;
                    if (wasEnabled) action.Enable();
                    RebindFinishedFrame = Time.frameCount;
                    SaveBindings();
                    OnBindingsChanged?.Invoke();
                    onComplete?.Invoke();
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    _currentRebindOp = null;
                    if (wasEnabled) action.Enable();
                    RebindFinishedFrame = Time.frameCount;
                    onCancel?.Invoke();
                });

            _currentRebindOp.Start();
        }

        public static void CancelOngoingRebind()
        {
            if (_currentRebindOp != null)
            {
                // OnCancel disposes the operation and clears the reference.
                _currentRebindOp.Cancel();
            }
        }

        public static void SaveBindings()
        {
            if (InputSystem.actions == null) return;
            string json = InputActionRebindingExtensions.SaveBindingOverridesAsJson(InputSystem.actions);
            PlayerPrefs.SetString(PrefKey, json);
            PlayerPrefs.Save();
        }

        public static void LoadSavedBindings()
        {
            if (InputSystem.actions == null) return;
            string json = PlayerPrefs.GetString(PrefKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                InputActionRebindingExtensions.LoadBindingOverridesFromJson(InputSystem.actions, json);
            }
            OnBindingsChanged?.Invoke();
        }

        public static void ResetAllToDefaults()
        {
            CancelOngoingRebind();
            if (InputSystem.actions != null)
            {
                InputActionRebindingExtensions.RemoveAllBindingOverrides(InputSystem.actions);
            }
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            OnBindingsChanged?.Invoke();
        }
    }
}
