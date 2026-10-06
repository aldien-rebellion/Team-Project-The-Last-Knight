using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Core;
using TheLastKnight.Player;

namespace TheLastKnight.EditorTools.RecallChecks
{
    internal class CommandScript : IRunCommand
    {
        internal bool Preview;
        public void Execute(ExecutionResult result)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var manager = GameManager.Instance;
            result.RegisterObjectModification(manager);
            if (Preview) { PlayerRecallChecks.PreparePreview(result); return; }
            PlayerRecallChecks.PreviousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (manager.Player == null) manager.Load("Church", false);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            manager.StartCoroutine(PlayerRecallChecks.RunChecks(result));
            result.Log("Recall checks started; no save files are written.");
        }
    }

    public static class PlayerRecallChecks
    {
        internal static bool PreviousRunInBackground;
        public static string Status { get; private set; } = "Not started";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static FieldInfo Checkpoint => typeof(GameManager).GetField("_checkpoint", Private);
        private static FieldInfo HasPoint => typeof(GameManager).GetField("_hasRecallPoint", Private);

        [MenuItem("The Last Knight/Checks/Recall (Play Mode)")]
        public static string Run()
        {
            var result = new ExecutionResult("Recall runtime checks");
            new CommandScript().Execute(result);
            return string.Join("\n", result.GetFormattedLogs());
        }

        public static string Preview()
        {
            var result = new ExecutionResult("Recall visual preview");
            new CommandScript { Preview = true }.Execute(result);
            return string.Join("\n", result.GetFormattedLogs());
        }

        internal static void PreparePreview(ExecutionResult result)
        {
            var manager = GameManager.Instance;
            if (manager.Player == null) throw new InvalidOperationException("Wait for the player to finish loading.");
            result.RegisterObjectModification(manager.Player);
            manager.Capture();
            Checkpoint.SetValue(manager, manager.State.Copy());
            HasPoint.SetValue(manager, true);
            var recall = manager.Player.GetComponent<PlayerRecall>();
            if (!recall.TryBeginRecall()) throw new InvalidOperationException("Player must be alive and idle for preview.");
            typeof(PlayerRecall).GetField("_elapsed", Private).SetValue(recall, 0.8f);
            typeof(PlayerRecall).GetMethod("AnimateVisuals", Private).Invoke(recall, null);
            EditorApplication.isPaused = true;
            result.Log("Recall preview paused at 0.8 seconds.");
        }

        private static void Check(bool passed, string message, ExecutionResult result)
        {
            if (!passed) throw new InvalidOperationException(message);
            result.Log("PASS: " + message);
            Debug.Log("[RecallChecks] PASS: " + message);
        }

        internal static IEnumerator RunChecks(ExecutionResult result)
        {
            var manager = GameManager.Instance;
            var oldCheckpoint = Checkpoint.GetValue(manager);
            var oldHasPoint = HasPoint.GetValue(manager);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var originalInput = InputSystem.settings;
            var testInput = UnityEngine.Object.Instantiate(originalInput);
            result.RegisterObjectCreation(testInput);
            testInput.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInput;
            var routine = Scenarios(result, keyboard);
            Status = "Running";
            try
            {
                while (true)
                {
                    object next;
                    try { if (!routine.MoveNext()) break; next = routine.Current; }
                    catch (Exception error)
                    {
                        Status = "FAILED: " + error.Message;
                        Debug.LogError("[RecallChecks] " + Status + "\n" + error.StackTrace);
                        yield break;
                    }
                    yield return next;
                }
                Status = "Passed";
                Debug.Log("[RecallChecks] All recall checks passed.");
            }
            finally
            {
                if (manager.Player != null) manager.Player.GetComponent<PlayerRecall>()?.CancelRecall();
                Checkpoint.SetValue(manager, oldCheckpoint);
                HasPoint.SetValue(manager, oldHasPoint);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalInput;
                result.DestroyObject(testInput);
                Application.runInBackground = PreviousRunInBackground;
            }
        }

        private static IEnumerator Scenarios(ExecutionResult result, Keyboard keyboard)
        {
            var manager = GameManager.Instance;
            yield return new WaitForSeconds(0.5f);
            var player = manager.Player;
            if (player == null) throw new InvalidOperationException("Test scene did not spawn a player.");
            result.RegisterObjectModification(player);
            result.RegisterObjectModification(player.transform);
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<TheLastKnight.AI.EnemyController>(FindObjectsSortMode.None))
            {
                result.RegisterObjectModification(enemy.gameObject);
                enemy.gameObject.SetActive(false);
            }
            var recall = player.GetComponent<PlayerRecall>();
            Check(recall != null, "Automatically attached to player", result);
            HasPoint.SetValue(manager, false);
            Check(!recall.TryBeginRecall(), "Cannot recall before saving", result);
            manager.Capture();
            var checkpoint = manager.State.Copy();
            Checkpoint.SetValue(manager, checkpoint);
            HasPoint.SetValue(manager, true);
            var origin = player.transform.position;
            checkpoint.position = origin + Vector3.right * 0.25f;
            int gold = player.Gold;
            Check(recall.TryBeginRecall(), "Stationary player starts channel", result);
            yield return new WaitForSeconds(1f);
            Check(recall.IsRecalling && Vector3.Distance(player.transform.position, origin) < 0.02f,
                "Still channeling after one second", result);
            yield return new WaitForSeconds(1.15f);
            Check(!recall.IsRecalling && Vector2.Distance(player.transform.position, checkpoint.position) < 0.1f,
                "Completes after two seconds at saved position", result);
            Check(player.Gold == gold, "Recall preserves current inventory/gold", result);
            yield return new WaitForSeconds(0.2f);
            foreach (var key in new[] { Key.A, Key.Space, Key.LeftShift, Key.E })
            {
                // Reset after jumping/skills so every scenario starts from a stable pose.
                result.RegisterObjectModification(player.transform);
                player.transform.position = origin;
                var controller = player.GetComponent<PlayerController>();
                controller.ResetVelocity();
                typeof(PlayerController).GetField("<CurrentState>k__BackingField", Private).SetValue(controller, PlayerState.Idle);
                yield return new WaitForSeconds(0.4f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Check(recall.IsRecalling, "H begins recall before " + key, result);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null;
                yield return null;
                Check(!recall.IsRecalling, key + " cancels channel", result);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            player.transform.position = origin;
            player.GetComponent<PlayerController>().ResetVelocity();
            typeof(PlayerController).GetField("<CurrentState>k__BackingField", Private).SetValue(player.GetComponent<PlayerController>(), PlayerState.Idle);
            yield return new WaitForSeconds(0.4f);
            Check(recall.TryBeginRecall(), "Start displacement test", result);
            player.transform.position += Vector3.right * 0.1f;
            yield return null;
            Check(!recall.IsRecalling, "External displacement cancels channel", result);
            manager.ArenaLocked = true;
            Check(!recall.TryBeginRecall(), "Boss arena prevents recall", result);
            manager.ArenaLocked = false;
            manager.State.introSeen = true;
            manager.Travel("CityCenter");
            yield return new WaitForSeconds(1f);
            player = manager.Player;
            recall = player.GetComponent<PlayerRecall>();
            Check(recall.TryBeginRecall(), "Start recall in a different scene", result);
            yield return new WaitForSeconds(2.3f);
            for (int frame = 0; manager.Player == null && frame < 120; frame++) yield return null;
            Check(manager.Player != null, "Destination player finishes binding", result);
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<TheLastKnight.AI.EnemyController>(FindObjectsSortMode.None))
            {
                result.RegisterObjectModification(enemy.gameObject);
                enemy.gameObject.SetActive(false);
            }
            Check(SceneManager.GetActiveScene().name == checkpoint.scene &&
                Vector2.Distance(manager.Player.transform.position, checkpoint.position) < 0.15f,
                "Cross-scene recall restores saved location", result);
            Check(manager.Player.Gold == gold, "Cross-scene recall preserves gold", result);
        }
    }
}
