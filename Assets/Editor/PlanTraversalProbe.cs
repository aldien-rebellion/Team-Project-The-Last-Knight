using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using TheLastKnight.Core;
using TheLastKnight.Combat;

// Drives actual input and observes physics. Never teleports, heals or awards loot.
public static class PlanTraversalProbe
{
    private static Keyboard _keyboard, _original;
    private static Mouse _mouse, _originalMouse;
    private static float _lastAttack;
    private static InputSettings _originalSettings, _probeSettings;
    private static float _target, _startTime, _lastSample;
    private static string _destination, _source;
    private static int _frame;
    private static readonly List<string> Rows = new List<string>();
    public static string Status { get; private set; } = "Not started";

    public static string Start(float targetX, string destinationScene = "")
    {
        if (!Application.isPlaying || GameManager.Instance.Player == null) throw new InvalidOperationException("Enter gameplay first.");
        Stop("Replaced by new probe");
        // Isolate editor focus routing in memory; never dirty the project settings asset.
        _originalSettings = InputSystem.settings;
        _probeSettings = UnityEngine.Object.Instantiate(_originalSettings);
        _probeSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = _probeSettings;
        _original = Keyboard.current;
        _keyboard = InputSystem.AddDevice<Keyboard>(); _keyboard.MakeCurrent();
        _originalMouse = Mouse.current; _mouse = InputSystem.AddDevice<Mouse>(); _mouse.MakeCurrent();
        _lastAttack = -10;
        _target = targetX; _destination = destinationScene;
        _source = SceneManager.GetActiveScene().name;
        _startTime = Time.realtimeSinceStartup; _lastSample = -10; _frame = -1;
        Rows.Clear(); Rows.Add("Actual keyboard movement and timed mouse attacks; no teleport, healing, direct damage, loot grants or save writes.");
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.update += Tick;
        Status = "Running from " + _source + " toward x=" + _target;
        return Status;
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { Stop("Stopped: exited Play Mode"); return; }
        if (Time.frameCount == _frame) return;
        _frame = Time.frameCount;
        var manager = GameManager.Instance;
        if (manager == null) { Stop("Stopped: no manager"); return; }
        string scene = SceneManager.GetActiveScene().name;
        if (scene != _source) { Stop(scene == _destination ? "PASS: arrived in " + scene : "Unexpected scene " + scene); return; }
        var p = manager.Player;
        if (p == null) return;
        float elapsed = Time.realtimeSinceStartup - _startTime;
        Status = $"Running {scene}: x={p.transform.position.x:F1}, y={p.transform.position.y:F1}, HP={p.CurrentHP:F0}, seconds={elapsed:F0}";
        if (elapsed - _lastSample >= 5) { Rows.Add(Status); _lastSample = elapsed; }
        if (manager.InputBlocked) { Stop("Stopped: player input blocked at " + Status); return; }
        if (elapsed > 300) { Stop("Timed out: " + Status); return; }
        float distance = _target - p.transform.position.x;
        InputSystem.QueueStateEvent(_mouse, new MouseState());
        EnemyStats enemy = null;
        float nearest = 2.4f;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None))
        {
            float gap = Vector2.Distance(candidate.transform.position, p.transform.position);
            if (!candidate.IsDead && gap < nearest) { nearest = gap; enemy = candidate; }
        }
        if (enemy != null)
        {
            var parry = enemy.GetComponent<ParryReceiver>();
            bool strike = parry == null || parry.IsStaggered || (parry.IsWindingUp && parry.Progress >= 0.55f);
            if (strike && Time.time - _lastAttack > 0.65f)
            {
                InputSystem.QueueStateEvent(_mouse, new MouseState().WithButton(MouseButton.Left));
                _lastAttack = Time.time;
            }
            InputSystem.QueueStateEvent(_keyboard, nearest > 1.2f && (parry == null || !parry.IsWindingUp)
                ? new KeyboardState(enemy.transform.position.x < p.transform.position.x ? Key.A : Key.D)
                : new KeyboardState());
            return;
        }
        if (Mathf.Abs(distance) < 0.5f)
        {
            if (string.IsNullOrEmpty(_destination)) { Stop("PASS: reached target at " + Status); return; }
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Time.frameCount % 20 == 0 ? Key.F : Key.None));
        }
        else InputSystem.QueueStateEvent(_keyboard, new KeyboardState(distance < 0 ? Key.A : Key.D));
    }

    public static void Stop(string reason = "Stopped by operator")
    {
        if (_keyboard == null) return;
        EditorApplication.update -= Tick;
        InputSystem.RemoveDevice(_keyboard); _keyboard = null;
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        _mouse = null;
        if (_originalMouse != null && _originalMouse.added) _originalMouse.MakeCurrent();
        if (_original != null && _original.added) _original.MakeCurrent();
        if (_originalSettings != null) InputSystem.settings = _originalSettings;
        if (_probeSettings != null) UnityEngine.Object.DestroyImmediate(_probeSettings);
        _originalSettings = _probeSettings = null;
        Status = reason; Rows.Add(reason);
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Captures/TraversalQA"));
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), Rows);
    }
}
