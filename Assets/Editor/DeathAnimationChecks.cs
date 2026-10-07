using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Core;

namespace TheLastKnight.EditorTools.DeathChecks
{
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Enter Play Mode first.");
            result.RegisterObjectModification(GameManager.Instance);
            if (GameManager.Instance.Player == null) GameManager.Instance.Load("Church", false);
            GameManager.Instance.StartCoroutine(DeathAnimationChecks.RunChecks(result));
        }
    }

    public static class DeathAnimationChecks
    {
        [MenuItem("The Last Knight/Checks/Death Animation (Play Mode)")]
        public static void Run() => new CommandScript().Execute(new ExecutionResult("Death animation checks"));

        private static void Check(bool passed, string message, ExecutionResult result)
        {
            if (!passed) throw new InvalidOperationException(message);
            result.Log("PASS: " + message);
            Debug.Log("[DeathAnimationChecks] PASS: " + message);
        }

        internal static IEnumerator RunChecks(ExecutionResult result)
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                var manager = GameManager.Instance;
                float bindDeadline = Time.realtimeSinceStartup + 10f;
                while (manager.Player == null && Time.realtimeSinceStartup < bindDeadline) yield return null;
                Check(manager.Player != null, "Player loaded for death checks", result);
                var player = manager.Player;
                var animator = player.GetComponent<Animator>();
                result.RegisterObjectModification(player);
                result.RegisterObjectModification(animator);
                player.AdminInvincible = false;
                animator.SetBool("IsAttacking", true);
                animator.SetBool("IsHurt", true);
                animator.speed = 3f;
                player.TakeDamage(100000f);
                manager.PlayerDied(); // Repeated notifications must not restart playback.
                Check(player.IsDead && manager.InputBlocked && GameObject.Find("YOU DIED") == null,
                    "Death immediately blocks input and keeps the UI hidden", result);
                float deadline = Time.realtimeSinceStartup + 10f;
                bool sawPlaying = false;
                float animationFinishedAt = -1f;
                while (GameObject.Find("YOU DIED") == null && Time.realtimeSinceStartup < deadline)
                {
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    sawPlaying |= state.IsName("Dead") && state.normalizedTime > 0f && state.normalizedTime < 1f;
                    if (animationFinishedAt < 0f && state.IsName("Dead") && state.normalizedTime >= 1f)
                        animationFinishedAt = Time.realtimeSinceStartup;
                    yield return null;
                }
                var finished = animator.GetCurrentAnimatorStateInfo(0);
                Check(sawPlaying && GameObject.Find("YOU DIED") != null && finished.IsName("Dead") &&
                    finished.normalizedTime >= 1f && animator.speed == 1f,
                    "Dead plays fully despite pending attack/hurt and accelerated attack speed", result);
                Check(animationFinishedAt >= 0f && Time.realtimeSinceStartup - animationFinishedAt >= 0.95f,
                    "Death UI waits one second after the animation finishes (one-frame tolerance)", result);
                GameObject.Find("Respawn").GetComponent<Button>().onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 10f;
                while (manager.Player == null && Time.realtimeSinceStartup < deadline) yield return null;
                Check(manager.Player != null && !manager.Player.IsDead && !manager.InputBlocked &&
                    GameObject.Find("YOU DIED") == null, "Respawn restores the living player and controls", result);
                result.RegisterObjectModification(manager.Player);
                manager.Player.TakeDamage(100000f);
                manager.Load("MainMenu", false);
                deadline = Time.realtimeSinceStartup + 10f;
                while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSeconds(1f);
                Check(manager.Player == null && GameObject.Find("YOU DIED") == null,
                    "Changing scenes cancels the pending death UI", result);
                Debug.Log("[DeathAnimationChecks] ALL PASSED");
            }
            finally { Application.runInBackground = background; }
        }
    }
}
