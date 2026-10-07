using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Core;
using TheLastKnight.UI;
using TheLastKnight.AI;

namespace TheLastKnight.EditorTools.MinimapChecks
{
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            result.RegisterObjectModification(GameManager.Instance);
            // Skip the opening story for this temporary test session.
            GameManager.Instance.State.introSeen = true;
            GameManager.Instance.StartCoroutine(MinimapChecks.RunChecks(result));
            result.Log("Minimap runtime checks started.");
        }
    }

    public static class MinimapChecks
    {
        [MenuItem("The Last Knight/Checks/Minimap (Play Mode)")]
        public static void Run()
        {
            var result = new ExecutionResult("Minimap checks");
            new CommandScript().Execute(result);
        }

        private static void Check(bool value, string message, ExecutionResult result)
        {
            if (!value) throw new InvalidOperationException(message);
            result.Log("PASS: " + message);
            Debug.Log("[MinimapChecks] PASS: " + message);
        }

        internal static IEnumerator RunChecks(ExecutionResult result)
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var originalSettings = InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(originalSettings);
            result.RegisterObjectCreation(settings);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            var enemies = new Dictionary<EnemyController, bool>();
            var scenarios = Scenarios(result, keyboard, enemies);
            try
            {
                while (true)
                {
                    object next;
                    try { if (!scenarios.MoveNext()) break; next = scenarios.Current; }
                    catch (Exception error) { Debug.LogError("[MinimapChecks] FAILED: " + error); yield break; }
                    yield return next;
                }
                Debug.Log("[MinimapChecks] ALL PASSED. CityCenter map preview paused; no save files written.");
                EditorApplication.isPaused = true;
            }
            finally
            {
                foreach (var enemy in enemies) if (enemy.Key != null) enemy.Key.enabled = enemy.Value;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalSettings;
                result.DestroyObject(settings);
                Application.runInBackground = background;
            }
        }

        private static IEnumerator Load(string name)
        {
            GameManager.Instance.Load(name, false);
            float timeout = Time.realtimeSinceStartup + 15;
            if (name == "MainMenu")
            {
                while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null;
                yield break;
            }
            while (GameManager.Instance.Player == null && Time.realtimeSinceStartup < timeout) yield return null;
            if (GameManager.Instance.Player == null) throw new InvalidOperationException("Player did not load in " + name);
            yield return null;
        }

        private static IEnumerator KeyPress(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static IEnumerator Scenarios(ExecutionResult result, Keyboard keyboard, Dictionary<EnemyController, bool> enemies)
        {
            var map = MinimapUI.Instance;
            Check(map != null, "GameManager automatically installs the map", result);
            Check(!map.IsOpen, "Map starts closed", result);
            yield return Load("CityCenter");
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>())
            {
                result.RegisterObjectModification(enemy);
                enemies.Add(enemy, enemy.enabled);
                enemy.enabled = false;
            }
            yield return KeyPress(keyboard, Key.M);
            Check(map.IsOpen, "M opens the map", result);
            Check(map.IsWorldView && map.WorldMap.isActiveAndEnabled && !map.Map.isActiveAndEnabled, "M initially opens the overhead world atlas", result);
            Check(map.WorldMap.RegionCount == 6 && map.WorldMap.CurrentRegionIndex == 1, "Whole kingdom shown with the current CityCenter region", result);
            VerifyConnections(result);
            yield return KeyPress(keyboard, Key.Tab);
            Check(!map.IsWorldView && map.Map.isActiveAndEnabled && !map.WorldMap.isActiveAndEnabled, "Tab keeps the original side map accessible", result);
            var worldButton = map.GetComponentsInChildren<Button>(true);
            foreach (var button in worldButton) if (button.name == "WorldViewTab") button.onClick.Invoke();
            Check(map.IsWorldView && map.WorldMap.isActiveAndEnabled, "World tab button switches back to the overhead map", result);
            Check(map.Map.RouteCount > 0 && map.Map.MarkerCount > 0, "Real scene lanes and markers discovered", result);
            Check(!GameManager.Instance.InputBlocked && Time.timeScale == 1f, "Map allows live exploration", result);
            var a = map.Map.WorldToMap(Vector2.zero);
            var b = map.Map.WorldToMap(Vector2.right);
            var c = map.Map.WorldToMap(Vector2.up);
            Check(b.x > a.x && c.y > a.y && Mathf.Abs((b.x - a.x) - (c.y - a.y)) < 0.01f, "World projection preserves direction and aspect", result);
            yield return KeyPress(keyboard, Key.M);
            Check(!map.IsOpen, "M closes the map", result);
            yield return KeyPress(keyboard, Key.M);
            yield return KeyPress(keyboard, Key.Escape);
            Check(!map.IsOpen && !PauseMenuUI.Instance.IsOpen, "Escape closes only the map", result);

            var field = new GameObject("MinimapTypingCheck", typeof(RectTransform), typeof(InputField));
            result.RegisterObjectCreation(field);
            RuntimeUI.EnsureEventSystem();
            EventSystem.current.SetSelectedGameObject(field);
            yield return KeyPress(keyboard, Key.M);
            Check(!map.IsOpen, "M is ignored while typing", result);
            EventSystem.current.SetSelectedGameObject(null);
            result.DestroyObject(field);
            yield return KeyPress(keyboard, Key.M);
            GameManager.Instance.SetInputBlocked(true);
            yield return null; yield return null;
            Check(!map.IsOpen, "Blocking UI hides the map", result);
            GameManager.Instance.SetInputBlocked(false);

            yield return Load("DemonCastle");
            Check(!map.IsOpen && map.Map.MarkerCount == 0, "Scene changes close and clear the previous map", result);
            yield return KeyPress(keyboard, Key.M);
            Check(map.IsOpen && map.Map.RouteCount > 1, "Castle layout appears from its authored geometry", result);
            Check(map.WorldMap.CurrentRegionIndex == 5, "Current region updates after entering DemonCastle", result);
            Debug.Log("[MinimapChecks] Castle: " + map.Map.RoomCount + " rooms, " + map.Map.RouteCount + " lanes, " + map.Map.MarkerCount + " markers.");
            yield return Load("MainMenu");
            yield return KeyPress(keyboard, Key.M);
            Check(!map.IsOpen, "Map remains hidden in MainMenu", result);
            yield return Load("CityCenter");
            map.Toggle();
            Canvas.ForceUpdateCanvases();
        }

        private static void VerifyConnections(ExecutionResult result)
        {
            var names = new HashSet<string>();
            foreach (var region in WorldMapGraphic.Regions)
            {
                names.Add(region.Scene);
                Check(System.IO.File.Exists("Assets/Scenes/Maps/" + region.Scene + ".unity"), "Atlas region exists: " + region.Scene, result);
            }
            foreach (var edge in WorldMapGraphic.Connections)
            {
                string from = WorldMapGraphic.Regions[edge.x].Scene;
                string to = WorldMapGraphic.Regions[edge.y].Scene;
                string source = System.IO.File.ReadAllText("Assets/Scenes/Maps/" + from + ".unity");
                bool portal = System.Text.RegularExpressions.Regex.IsMatch(source,
                    @"propertyPath: targetSceneName\s+value: " + to + @"\s")
                    || source.Contains("targetSceneName: " + to);
                Check(portal, "Atlas route matches scene portal: " + from + " -> " + to, result);
            }
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (!scene.enabled || !scene.path.StartsWith("Assets/Scenes/Maps/")) continue;
                Check(names.Contains(System.IO.Path.GetFileNameWithoutExtension(scene.path)), "Every playable map is covered by the atlas", result);
            }
        }
    }
}
