using System;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Core;
using TheLastKnight.Input;

namespace TheLastKnight.EditorTools.ControlsChecks
{
    internal class CommandScript : IRunCommand
    {
        internal bool Preview;
        internal bool Bottom;
        internal GameLanguage? PreviewLanguage;
        internal bool Rebind;
        public void Execute(ExecutionResult result)
        {
            if (Rebind)
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
                ControlsAndLanguageChecks.OriginalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
                var menu = TheLastKnight.UI.PauseMenuUI.Instance;
                result.RegisterObjectModification(menu);
                menu.StartCoroutine(ControlsAndLanguageChecks.CheckInteractiveRebind(result));
                return;
            }
            if (Preview)
            {
                if (PreviewLanguage.HasValue) LocalizationManager.Current = PreviewLanguage.Value;
                var menu = TheLastKnight.UI.PauseMenuUI.Instance;
                if (menu == null || !Application.isPlaying) throw new InvalidOperationException("Enter Play Mode for the preview.");
                result.RegisterObjectModification(menu);
                menu.OpenSettingsFromExternal(null);
                typeof(TheLastKnight.UI.PauseMenuUI).GetMethod("ShowControlsMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, null);
                var panel = (GameObject)typeof(TheLastKnight.UI.PauseMenuUI).GetField("_panel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(menu);
                result.RegisterObjectCreation(panel);
                Canvas.ForceUpdateCanvases();
                panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = Bottom ? 0 : 1;
                Canvas.ForceUpdateCanvases();
                result.Log("Controls preview ready.");
                return;
            }
            var asset = InputSystem.actions;
            if (asset == null) throw new InvalidOperationException("Project input actions are missing.");
            result.RegisterObjectModification(asset);
            var language = LocalizationManager.Current;
            string overrides = asset.SaveBindingOverridesAsJson();
            var enabled = asset.actionMaps.SelectMany(map => map.actions).Where(action => action.enabled).ToArray();
            Keyboard keyboard = null;
            InputSettings originalSettings = InputSystem.settings;
            InputSettings testSettings = null;
            try
            {
                foreach (var row in KeyRebindManager.GetRebindableActions())
                {
                    var action = asset.FindAction(row.ActionName, true);
                    Check(row.BindingIndex >= 0 && row.BindingIndex < action.bindings.Count && !action.bindings[row.BindingIndex].isComposite, "Valid binding for " + row.ActionName);
                    foreach (var locale in new[] { GameLanguage.Thai, GameLanguage.English })
                    {
                        LocalizationManager.Current = locale;
                        Check(LocalizationManager.Get(row.LocalizationKey) != row.LocalizationKey, "Translated control " + row.LocalizationKey);
                    }
                }
                var interact = asset.FindAction("Interact", true);
                interact.Disable();
                bool canceled = false;
                KeyRebindManager.StartRebind(new RebindableActionInfo("Interact", 0, "ACTION_INTERACT"), null, () => canceled = true);
                KeyRebindManager.CancelOngoingRebind();
                Check(canceled && !interact.enabled, "Cancel preserves disabled gameplay action.");
                interact.Enable();
                KeyRebindManager.StartRebind(new RebindableActionInfo("Interact", 0, "ACTION_INTERACT"), null);
                KeyRebindManager.CancelOngoingRebind();
                Check(interact.enabled, "Cancel restores enabled action.");
                interact.Disable();
                interact.ApplyBindingOverride(0, "<Keyboard>/g");
                Check(KeyRebindManager.GetCurrentBindingDisplay("Interact", 0) == "G", "Binding display follows override.");
                Check(KeyRebindManager.UpdateBindingHints("F  Open chest") == "G  Open chest", "World prompt follows override.");
                Check(KeyRebindManager.UpdateBindingHints("[F Rest / Save]") == "[G Rest / Save]", "Medusa prompt follows override.");
                Check(KeyRebindManager.UpdateBindingHints("[ Q ]") == "[ Q ]", "Binding badges preserve their actual key.");
                string changed = asset.SaveBindingOverridesAsJson();
                interact.RemoveAllBindingOverrides();
                asset.LoadBindingOverridesFromJson(changed);
                Check(interact.bindings[0].effectivePath == "<Keyboard>/g", "Override survives serialization and reload.");
                if (Application.isPlaying)
                {
                    testSettings = UnityEngine.Object.Instantiate(originalSettings);
                    result.RegisterObjectCreation(testSettings);
                    testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = testSettings;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    asset.Enable();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
                    InputSystem.Update();
                    Check(KeyRebindManager.WasPressedThisFrame("Interact"), "Interaction responds to the customized key.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                    InputSystem.Update();
                    Check(!KeyRebindManager.WasPressedThisFrame("Interact"), "Old interaction key no longer fires.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.Update();
                    var status = asset.FindAction("ToggleStatus", true);
                    status.ApplyBindingOverride(0, "<Keyboard>/h");
                    asset.FindActionMap("Player").Disable();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                    InputSystem.Update();
                    Check(KeyRebindManager.WasPressedThisFrame("ToggleStatus"), "Customized status shortcut works with gameplay disabled.");
                }
                // Existing checks expect default hotkeys, so restore the original asset first.
                asset.LoadBindingOverridesFromJson(overrides);
                LocalizationChecks.Verify();
                LocalizationManager.Current = GameLanguage.English;
                Check(LocalizationManager.Translate("> [F พักผ่อน/บันทึก]") == "> [F Rest / Save]", "Selected Medusa row translates.");
                Check(LocalizationManager.Translate("กำลังกลับจุดเซฟ  1.2 วิ") == "Recalling to save point  1.2 s", "Recall timer translates.");
                Check(LocalizationManager.Translate("มอนสเตอร์เกิดใหม่ 3 ตัว") == "Respawned 3 monsters", "Respawn count translates.");
                foreach (string id in new[] { "thunder_scroll", "light_scroll", "fire_scroll", "earth_scroll", "ice_spellbook", "advanced_spellbook", "earth_spellbook" })
                {
                    var item = TheLastKnight.Inventory.ItemRegistry.CreateItem(id);
                    Check(!System.Text.RegularExpressions.Regex.IsMatch(LocalizationManager.Translate(item.description), @"[ก-๙]"), "English item description: " + id);
                }
                result.Log("PASS: all control rows, both languages, cancellation state, binding hints, persistence, catalog and item translations.");
            }
            finally
            {
                KeyRebindManager.CancelOngoingRebind();
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalSettings;
                if (testSettings != null) result.DestroyObject(testSettings);
                asset.Disable();
                asset.LoadBindingOverridesFromJson(overrides);
                foreach (var action in enabled) action.Enable();
                LocalizationManager.Current = language;
            }
        }

        private static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
        }
    }

    public static class ControlsAndLanguageChecks
    {
        public static string RebindStatus { get; private set; } = "Not run";
        internal static bool OriginalRunInBackground;
        public static void RunInteractiveRebind()
        {
            RebindStatus = "Running";
            new CommandScript { Rebind = true }.Execute(new ExecutionResult("Interactive rebind checks"));
        }

        internal static IEnumerator CheckInteractiveRebind(ExecutionResult result)
        {
            const string prefKey = "TheLastKnight_BindingOverridesJson";
            bool hadPreference = PlayerPrefs.HasKey(prefKey);
            string savedPreference = PlayerPrefs.GetString(prefKey);
            var asset = InputSystem.actions;
            result.RegisterObjectModification(asset);
            string overrides = asset.SaveBindingOverridesAsJson();
            var action = asset.FindAction("Interact", true);
            bool enabled = action.enabled;
            var language = LocalizationManager.Current;
            var originalSettings = InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(originalSettings);
            result.RegisterObjectCreation(settings);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            bool complete = false, canceled = false;
            try
            {
                action.Disable();
                KeyRebindManager.StartRebind(new RebindableActionInfo("Interact", 0, "ACTION_INTERACT"), () => complete = true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
                InputSystem.Update();
                yield return new WaitForSecondsRealtime(0.3f);
                if (!complete || action.enabled || action.bindings[0].effectivePath != "<Keyboard>/k")
                {
                    RebindStatus = "FAIL: complete=" + complete + ", enabled=" + action.enabled + ", path=" + action.bindings[0].effectivePath;
                    yield break;
                }
                if (!PlayerPrefs.GetString(prefKey).Contains("<Keyboard>/k"))
                {
                    RebindStatus = "FAIL: changed key was not saved";
                    yield break;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                KeyRebindManager.StartRebind(new RebindableActionInfo("Interact", 0, "ACTION_INTERACT"), null, () => canceled = true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                yield return null;
                RebindStatus = canceled && !action.enabled && action.bindings[0].effectivePath == "<Keyboard>/k"
                    ? "PASS: keyboard rebind, preference save, Escape cancellation and disabled action state"
                    : "FAIL: Escape cancellation changed the binding or action state";
            }
            finally
            {
                KeyRebindManager.CancelOngoingRebind();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalSettings;
                result.DestroyObject(settings);
                asset.LoadBindingOverridesFromJson(overrides);
                if (enabled) action.Enable(); else action.Disable();
                if (hadPreference) PlayerPrefs.SetString(prefKey, savedPreference); else PlayerPrefs.DeleteKey(prefKey);
                PlayerPrefs.Save();
                LocalizationManager.Current = language;
                Application.runInBackground = OriginalRunInBackground;
                result.Log(RebindStatus);
                Debug.Log("[ControlsAndLanguageChecks] " + RebindStatus);
            }
        }

        public static void PreviewLocale(bool thai, bool bottom)
        {
            new CommandScript { Preview = true, Bottom = bottom, PreviewLanguage = thai ? GameLanguage.Thai : GameLanguage.English }.Execute(new ExecutionResult("Controls language preview"));
        }
        public static void Preview(bool bottom)
        {
            new CommandScript { Preview = true, Bottom = bottom }.Execute(new ExecutionResult("Controls preview"));
        }
        [MenuItem("The Last Knight/Checks/Controls and Language")]
        public static string Run()
        {
            var result = new ExecutionResult("Controls and language checks");
            new CommandScript().Execute(result);
            string logs = string.Join("\n", result.GetFormattedLogs());
            Debug.Log("[ControlsAndLanguageChecks] " + logs);
            return logs;
        }
    }
}
