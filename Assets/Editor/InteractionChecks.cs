using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Environment;
using TheLastKnight.Input;

namespace TheLastKnight.EditorTools.InteractionChecks
{
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            var asset = InputSystem.actions;
            if (asset == null) throw new InvalidOperationException("Missing project actions.");
            result.RegisterObjectModification(asset);
            var enabled = asset.Select(a => a).Where(a => a.enabled).ToArray();
            string overrides = asset.SaveBindingOverridesAsJson();
            var originalSettings = InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(originalSettings);
            result.RegisterObjectCreation(settings);
            var root = new GameObject("Interaction regression checks");
            result.RegisterObjectCreation(root);
            Keyboard keyboard = null;
            try
            {
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                keyboard = InputSystem.AddDevice<Keyboard>();
                var handler = root.AddComponent<PlayerInputHandler>();
                var map = asset.FindActionMap("Player", true);
                var interact = map.FindAction("Interact", true);
                map.Disable();
                map.FindAction("Move", true).Enable();
                Check(map.enabled && !interact.enabled, "Reproduced a partially enabled Player map");
                handler.EnablePlayerActions();
                Check(interact.enabled, "Restoring gameplay also restores Interact");
                interact.Disable();
                typeof(PlayerInputHandler).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(handler, null);
                Check(interact.enabled, "Gameplay update repairs Interact while movement remains enabled");
                interact.ApplyBindingOverride(0, "<Keyboard>/g");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
                InputSystem.Update();
                Check(KeyRebindManager.WasPressedThisFrame("Interact"), "A quick press on the rebound key is received");

                // Use isolated props far outside the loaded map. One chest can
                // retire itself during its callback without activating another.
                root.transform.position = new Vector3(10000, 10000);
                var player = Child(root, "Player");
                var body = player.AddComponent<BoxCollider2D>();
                body.size = new Vector2(1, 2);
                var first = Child(root, "First").AddComponent<InteractionProbe>();
                var second = Child(root, "Second").AddComponent<InteractionProbe>();
                first.transform.position += Vector3.down * 1.5f;
                first.interactionRange = 0.6f;
                second.transform.position += Vector3.right * 2f;
                Physics2D.SyncTransforms();
                var closest = typeof(WorldInteractable).GetMethod("FindClosest", BindingFlags.NonPublic | BindingFlags.Static);
                Check(closest.Invoke(null, new object[] { player.transform }) == first, "Ground-level prop uses distance to the player body");
                var press = typeof(WorldInteractable).GetMethod("TryInteract", BindingFlags.NonPublic | BindingFlags.Instance);
                press.Invoke(first, new object[] { true });
                press.Invoke(second, new object[] { true });
                Check(first.Count == 1 && second.Count == 0, "One key press activates only one world object");
                Check(closest.Invoke(null, new object[] { player.transform }) == second, "Opened props no longer take selection");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                InputSystem.Update();
                Check(!KeyRebindManager.WasPressedThisFrame("Interact"), "Old key does not bypass rebinding");
                VerifyPortalRange(root, player, result);
                result.Log("PASS: Interaction regression checks.");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                result.DestroyObject(root);
                asset.Disable();
                asset.LoadBindingOverridesFromJson(overrides);
                foreach (var action in enabled) action.Enable();
                InputSystem.settings = originalSettings;
                result.DestroyObject(settings);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
        private static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException(message);
        }
        private static void VerifyPortalRange(GameObject root, GameObject player, ExecutionResult result)
        {
            foreach (var type in new[] { typeof(ScenePortal), typeof(TeleportDoor) })
            {
                var portal = Child(root, type.Name).AddComponent(type);
                var collider = portal.GetComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(2, 3);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                type.GetField("_player", flags).SetValue(portal, player);
                type.GetField("_playerCollider", flags).SetValue(portal, player.GetComponent<Collider2D>());
                var range = type.GetField(type == typeof(ScenePortal) ? "playerInRange" : "_playerInRange", flags);
                var refresh = type.GetMethod("CheckPlayerOverlap", flags);
                Physics2D.SyncTransforms();
                refresh.Invoke(portal, null);
                Check((bool)range.GetValue(portal), type.Name + " detects an overlapping player");
                portal.transform.position += Vector3.right * 20;
                Physics2D.SyncTransforms();
                refresh.Invoke(portal, null);
                Check(!(bool)range.GetValue(portal), type.Name + " clears stale range after a teleport");
                result.Log("PASS: " + type.Name + " range reconciliation.");
            }
        }
    }

    public sealed class InteractionProbe : WorldInteractable
    {
        public int Count;
        protected override bool UsesWorldPrompt => false;
        public override void Interact() { Count++; interactionRange = -1f; }
    }

    public static class InteractionChecks
    {
        [MenuItem("The Last Knight/Checks/Interaction")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode for synthetic gameplay input.");
            var result = new ExecutionResult("Interaction checks");
            new CommandScript().Execute(result);
            Debug.Log("[InteractionChecks] " + string.Join("\n", result.GetFormattedLogs()));
        }
    }
}
