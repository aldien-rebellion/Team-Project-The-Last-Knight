using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Environment;
using TheLastKnight.Player;
using TheLastKnight.Core;
using TheLastKnight.Physics;
using TheLastKnight.Stats;
using TheLastKnight.AI;

namespace TheLastKnight.EditorTools.FairyChecks
{
    internal class CommandScript : IRunCommand
    {
        internal string Operation;
        public void Execute(ExecutionResult result)
        {
            if (Operation == "prepare")
            {
                if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
                // Session-only override; the user's main-menu preference is preserved.
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Maps/DemonCastle.unity");
                result.Log("Prepared DemonCastle runtime checks.");
                return;
            }
            if (Operation == "restore")
            {
                TheLastKnight.Editor.PlayModeStartSceneHelper.ApplySetting();
                result.Log("Restored the user's Play Mode start scene preference.");
                return;
            }
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var lift = UnityEngine.Object.FindAnyObjectByType<DemonCastleFairyLift>();
            result.RegisterObjectModification(lift);
            if (Operation == "diagnose") { DemonCastleFairyLiftChecks.Diagnose(lift, result); return; }
            if (Operation == "preview" || Operation == "dustpreview") { DemonCastleFairyLiftChecks.Preview(lift, result, Operation == "dustpreview" ? 0.2f : 1.3f); return; }
            lift.StartCoroutine(DemonCastleFairyLiftChecks.RunChecks(lift, result));
        }
    }

    public static class DemonCastleFairyLiftChecks
    {
        public static string Status { get; private set; } = "Not started";
        public static string Run(string operation)
        {
            var result = new ExecutionResult("DemonCastle fairy checks");
            new CommandScript { Operation = operation }.Execute(result);
            return string.Join("\n", result.GetFormattedLogs());
        }

        private static void Check(bool passed, string message, ExecutionResult result)
        {
            if (!passed) throw new InvalidOperationException(message);
            result.Log("PASS: " + message);
            Debug.Log("[FairyLiftChecks] PASS: " + message);
        }

        private static void Place(PlayerController player, Collider2D source, float x, ExecutionResult result)
        {
            result.RegisterObjectModification(player);
            var body = player.GetComponent<BoxCollider2D>();
            float bottomOffset = body.bounds.min.y - player.transform.position.y;
            var position = new Vector2(x, source.bounds.max.y + 0.018f - bottomOffset);
            player.ResetVelocity();
            player.GetComponent<Rigidbody2D>().position = position;
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
            Physics2D.SyncTransforms();
            player.GetComponent<KinematicCharacterController2D>().Move(Vector2.zero, 0f);
        }

        internal static void Diagnose(DemonCastleFairyLift lift, ExecutionResult result)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var body = player.GetComponent<Collider2D>();
            var source = GameObject.Find("F3_GrandHall_UpperShelf (3)").GetComponent<Collider2D>();
            Place(player, source, source.bounds.center.x, result);
            var left = GameObject.Find("F3_GrandHall_MidShelf (1)").GetComponent<Collider2D>();
            float bottomOffset = body.bounds.min.y - player.transform.position.y;
            var start = (Vector2)player.transform.position;
            var cruise = new Vector2(start.x, left.bounds.max.y + 0.16f - bottomOffset);
            var cross = new Vector2(left.bounds.max.x - body.bounds.extents.x - 0.85f, cruise.y);
            var destination = new Vector2(cross.x, left.bounds.max.y + 0.045f - bottomOffset);
            var points = new[] { start, cruise, cross, destination };
            Vector2 offset = (Vector2)body.bounds.center - start;
            for (int segment = 0; segment < points.Length - 1; segment++)
                for (int box = 0; box < 3; box++)
                {
                    var extra = box == 0 ? Vector2.zero : new Vector2((box == 1 ? -1f : 1f) * (body.bounds.extents.x + 0.22f), body.bounds.extents.y * 0.65f);
                    var size = box == 0 ? (Vector2)body.bounds.size : Vector2.one * 0.704f;
                    var from = points[segment] + offset + extra;
                    var to = points[segment + 1] + offset + extra;
                    result.Log("Segment " + segment + " box " + box + " from=" + from + " to=" + to + " size=" + size);
                    foreach (var c in Physics2D.OverlapBoxAll(from, size, 0f))
                        if (!c.isTrigger && !c.transform.IsChildOf(player.transform)) result.Log("START BLOCK " + c.name);
                    foreach (var hit in Physics2D.BoxCastAll(from, size, 0f, (to-from).normalized, (to-from).magnitude))
                        if (!hit.collider.isTrigger && !hit.collider.transform.IsChildOf(player.transform)) result.Log("BLOCK " + hit.collider.name + " at " + hit.point + " distance=" + hit.distance + " bounds=" + hit.collider.bounds);
                    foreach (var c in Physics2D.OverlapBoxAll(to, size, 0f))
                        if (!c.isTrigger && !c.transform.IsChildOf(player.transform)) result.Log("END BLOCK " + c.name);
                }
            EditorApplication.isPaused = true;
        }

        internal static void Preview(DemonCastleFairyLift lift, ExecutionResult result, float pauseTime)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var source = GameObject.Find("F3_GrandHall_UpperShelf (3)").GetComponent<Collider2D>();
            lift.CancelLift();
            Place(player, source, source.bounds.center.x, result);
            if (!lift.TryStartLift()) throw new InvalidOperationException("Preview could not start.");
            // Freeze after shoulder contact using a coroutine, allowing Animator to evaluate.
            lift.StartCoroutine(PreviewFrame(lift, pauseTime));
        }

        private static IEnumerator PreviewFrame(DemonCastleFairyLift lift, float pauseTime)
        {
            yield return new WaitForSeconds(pauseTime);
            EditorApplication.isPaused = true;
            Debug.Log("[FairyLiftChecks] Preview paused with fairies carrying the player.");
        }

        internal static IEnumerator RunChecks(DemonCastleFairyLift lift, ExecutionResult result)
        {
            Status = "Running";
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            var oldSettings = InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(oldSettings);
            result.RegisterObjectCreation(settings);
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).ToDictionary(x => x, x => x.enabled);
            foreach (var enemy in enemies.Keys) { result.RegisterObjectModification(enemy); enemy.enabled = false; }
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var originalPosition = player.transform.position;
            var stats = player.GetComponent<PlayerStats>();
            bool oldInvincible = stats.AdminInvincible;
            result.RegisterObjectModification(stats);
            stats.AdminInvincible = true;
            var randomState = UnityEngine.Random.state;
            var routine = Scenarios(lift, player, keyboard, result);
            try
            {
                while (true)
                {
                    object next;
                    try { if (!routine.MoveNext()) break; next = routine.Current; }
                    catch (Exception error)
                    {
                        Status = "FAILED: " + error.Message;
                        Debug.LogError("[FairyLiftChecks] " + Status + "\n" + error.StackTrace);
                        yield break;
                    }
                    yield return next;
                }
                Status = "Passed";
                Debug.Log("[FairyLiftChecks] All checks passed.");
            }
            finally
            {
                lift.CancelLift();
                player.transform.position = originalPosition;
                player.GetComponent<Rigidbody2D>().position = originalPosition;
                player.ResetVelocity();
                stats.AdminInvincible = oldInvincible;
                foreach (var enemy in enemies) if (enemy.Key != null) enemy.Key.enabled = enemy.Value;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = oldSettings;
                result.DestroyObject(settings);
                UnityEngine.Random.state = randomState;
                Application.runInBackground = oldBackground;
            }
        }

        private static IEnumerator Scenarios(DemonCastleFairyLift lift, PlayerController player, Keyboard keyboard, ExecutionResult result)
        {
            var source = GameObject.Find("F3_GrandHall_UpperShelf (3)").GetComponent<Collider2D>();
            var body = player.GetComponent<Collider2D>();
            yield return new WaitForSeconds(0.5f);
            lift.CancelLift();
            Place(player, source, source.bounds.center.x, result);
            yield return new WaitForSeconds(0.15f);
            foreach (float x in new[] { source.bounds.min.x + 0.1f, source.bounds.max.x - 0.1f })
            {
                Place(player, source, x, result);
                yield return new WaitForSeconds(0.1f);
                for (int side = 0; side < 2; side++)
                {
                    List<Vector2> edgeRoute;
                    Check(lift.TryBuildRoute(side, out edgeRoute), "Safe route from source edge " + x + " to landing " + side, result);
                }
            }
            Place(player, source, source.bounds.center.x, result);
            lift.CancelLift();
            for (int destination = 0; destination < 2; destination++)
            {
                List<Vector2> route;
                Check(lift.TryBuildRoute(destination, out route), "Collision-free route to landing " + destination, result);
            }
            yield return new WaitForSeconds(1.5f);
            Check(lift.Phase == DemonCastleFairyLift.LiftPhase.Waiting && lift.ActiveFairyCount == 0, "No fairies before two seconds", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;
            Check(lift.IdleElapsed < 0.05f, "Movement resets idle countdown", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.1f);
            Place(player, source, source.bounds.center.x, result);
            yield return new WaitForSeconds(2.15f);
            Check(lift.Phase == DemonCastleFairyLift.LiftPhase.Approaching && lift.ActiveFairyCount == 2, "Two fairies appear after two idle seconds", result);
            Check(lift.GetComponentsInChildren<Collider2D>().Length == 0, "Event fairies have no colliders", result);
            var fairies = lift.GetComponentsInChildren<SpriteRenderer>();
            foreach (var fairy in fairies)
                foreach (var collider in Physics2D.OverlapBoxAll(fairy.bounds.center, fairy.bounds.size, 0f))
                    Check(collider.isTrigger || collider.transform.IsChildOf(player.transform), "Fairy spawn is clear of solid colliders", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;
            Check(lift.ActiveFairyCount == 0 && !player.IsFairyCarried, "Movement cancels approach immediately", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.1f);
            Place(player, source, source.bounds.center.x, result);
            yield return new WaitForSeconds(3.15f);
            Check(lift.Phase == DemonCastleFairyLift.LiftPhase.Carrying && player.IsFairyCarried, "Fairies reach shoulders and lift player", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;
            Check(lift.ActiveFairyCount == 0 && !player.IsFairyCarried && Mathf.Abs(player.Velocity.x) > 0.1f, "Movement cancels flight and restores control", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.15f);

            for (int destination = 0; destination < 2; destination++)
            {
                lift.CancelLift();
                Place(player, source, source.bounds.center.x, result);
                yield return new WaitForSeconds(0.15f);
                // Seed only the random draw; exercise the normal public event path.
                for (int seed = 0; seed < 100; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if (UnityEngine.Random.Range(0, 2) != destination) continue;
                    UnityEngine.Random.InitState(seed);
                    break;
                }
                Check(lift.TryStartLift(), "Start delivery to random landing " + destination, result);
                Check(lift.LastLandingIndex == destination, "Selected expected random destination", result);
                float timeout = Time.time + 12f;
                while (lift.Phase != DemonCastleFairyLift.LiftPhase.Lingering && Time.time < timeout)
                {
                    if (lift.Phase == DemonCastleFairyLift.LiftPhase.Waiting) throw new InvalidOperationException("Delivery was cancelled unexpectedly.");
                    yield return null;
                }
                Check(lift.Phase == DemonCastleFairyLift.LiftPhase.Lingering && !player.IsFairyCarried, "Delivery finished and released player", result);
                var landing = GameObject.Find(destination == 0 ? "F3_GrandHall_MidShelf (1)" : "F3_GrandHall_UpperMezzanine (1)").GetComponent<Collider2D>();
                Check(Mathf.Abs(body.bounds.min.y - landing.bounds.max.y) < 0.04f && body.bounds.min.x >= landing.bounds.min.x && body.bounds.max.x <= landing.bounds.max.x, "Player lands safely on requested corner", result);
                yield return new WaitForSeconds(1.75f);
                Check(lift.ActiveFairyCount == 2, "Fairies remain for almost two seconds after delivery", result);
                yield return new WaitForSeconds(0.4f);
                Check(lift.ActiveFairyCount == 0, "Fairies disappear after two seconds", result);
            }

            var colors = new HashSet<int>();
            var pairs = new HashSet<string>();
            for (int visit = 0; visit < 15; visit++)
            {
                Place(player, source, source.bounds.center.x, result);
                yield return new WaitForSeconds(0.1f);
                Check(lift.TryStartLift(), "Repeat visit starts", result);
                colors.Add(lift.LeftColor); colors.Add(lift.RightColor);
                pairs.Add(lift.LeftColor + ":" + lift.RightColor);
                lift.CancelLift();
            }
            Check(colors.Count == 3 && pairs.Count > 1, "All three colours and freshly randomized fairy pairs are used", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            yield return null;
            Place(player, source, source.bounds.center.x, result);
            yield return new WaitForSeconds(2.25f);
            Check(lift.ActiveFairyCount == 0 && lift.IdleElapsed == 0f, "Holding a non-movement key prevents idle activation", result);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
    }
}
