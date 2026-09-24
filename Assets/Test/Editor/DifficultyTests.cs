using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class DifficultyTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static void Invoke(Component target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);
        private static float Read(Component target, string property) => (float)target.GetType().GetProperty(property).GetValue(target);

        [TestCase(0, 10f, 1f)]
        [TestCase(1, 13f, 0.7f)]
        [TestCase(2, 16f, 0.4f)]
        public void ActualDamageFlow_UsesSelectedDifficulty(int mode, float incoming, float outgoingMultiplier)
        {
            var difficulty = RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("Current");
            var originalMode = difficulty.GetValue(null);
            var originalRandom = UnityEngine.Random.state;
            GameObject player = null, enemy = null;
            var popupType = RuntimeType("TheLastKnight.Combat.FloatingCombatText");
            var existingPopups = UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None);
            try
            {
                difficulty.SetValue(null, Enum.ToObject(difficulty.PropertyType, mode));
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(10000, 10000);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                float before = Read(stats, "CurrentHP");
                stats.GetType().GetMethod("TakeDamage").Invoke(stats, new object[] { 10f });
                Assert.That(before - Read(stats, "CurrentHP"), Is.EqualTo(incoming).Within(0.001f));

                enemy = new GameObject("Difficulty target", typeof(BoxCollider2D));
                enemy.transform.position = player.transform.position + Vector3.right;
                var target = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                Invoke(target, "Awake");
                Physics2D.SyncTransforms();
                UnityEngine.Random.InitState(823);
                bool critical = UnityEngine.Random.value * 100f < Read(stats, "CriticalChance");
                UnityEngine.Random.InitState(823);
                float enemyBefore = Read(target, "CurrentHealth");
                Invoke(controller, "StartAttack");
                Invoke(controller, "ApplyAttackHits");
                float expected = Read(stats, "AttackPower") * outgoingMultiplier * (critical ? 2f : 1f);
                Assert.That(enemyBefore - Read(target, "CurrentHealth"), Is.EqualTo(expected).Within(0.001f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None))
                    if (!existingPopups.Contains(popup)) UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
                difficulty.SetValue(null, originalMode);
                UnityEngine.Random.state = originalRandom;
            }
        }

        [TestCase(0, 0.02f)]
        [TestCase(1, 0.02f)]
        [TestCase(2, 0.01f)]
        public void HPRegeneration_ScalesWithDifficulty(int mode, float expectedRate)
        {
            var difficulty = RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("Current");
            var originalMode = difficulty.GetValue(null);
            GameObject player = null;
            try
            {
                difficulty.SetValue(null, Enum.ToObject(difficulty.PropertyType, mode));
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(10000, 10000);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");

                float maxHP = Read(stats, "MaxHP");
                stats.GetType().GetMethod("TakeDamage").Invoke(stats, new object[] { 20f });
                float hpAfterDamage = Read(stats, "CurrentHP");

                var stateProp = controller.GetType().GetProperty("CurrentState");
                var playerStateType = RuntimeType("TheLastKnight.Player.PlayerState");

                // Immediately after damage (< 5s) even if Idle, no regeneration
                stateProp.SetValue(controller, Enum.ToObject(playerStateType, 0)); // PlayerState.Idle
                stats.GetType().GetMethod("RegenerateHP").Invoke(stats, new object[] { 1f });
                Assert.That(Read(stats, "CurrentHP"), Is.EqualTo(hpAfterDamage).Within(0.001f));

                // Fast forward damage timer past 5s delay
                stats.GetType().GetMethod("SetLastDamageTimeForTesting").Invoke(stats, new object[] { Time.time - 5.1f });

                // Regenerate 1 second while Idle
                stats.GetType().GetMethod("RegenerateHP").Invoke(stats, new object[] { 1f });
                float expectedGain = maxHP * expectedRate;
                Assert.That(Read(stats, "CurrentHP"), Is.EqualTo(hpAfterDamage + expectedGain).Within(0.001f));

                // Large deltaTime clamps to MaxHP
                stats.GetType().GetMethod("RegenerateHP").Invoke(stats, new object[] { 1000f });
                Assert.That(Read(stats, "CurrentHP"), Is.EqualTo(maxHP).Within(0.001f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                difficulty.SetValue(null, originalMode);
            }
        }

        [Test]
        public void HPRegeneration_SuppressedWhenNotIdleOrWalking()
        {
            var difficulty = RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("Current");
            var originalMode = difficulty.GetValue(null);
            GameObject player = null;
            try
            {
                difficulty.SetValue(null, Enum.ToObject(difficulty.PropertyType, 0));
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(10000, 10000);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");

                stats.GetType().GetMethod("TakeDamage").Invoke(stats, new object[] { 20f });
                stats.GetType().GetMethod("SetLastDamageTimeForTesting").Invoke(stats, new object[] { Time.time - 5.1f });
                float hpBefore = Read(stats, "CurrentHP");

                var stateProp = controller.GetType().GetProperty("CurrentState");
                var playerStateType = RuntimeType("TheLastKnight.Player.PlayerState");

                // Set state to Jumping (not Idle or Walking)
                stateProp.SetValue(controller, Enum.Parse(playerStateType, "Jumping"));
                stats.GetType().GetMethod("RegenerateHP").Invoke(stats, new object[] { 1f });
                Assert.That(Read(stats, "CurrentHP"), Is.EqualTo(hpBefore).Within(0.001f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                difficulty.SetValue(null, originalMode);
            }
        }

        [TestCase(0, true, true)]
        [TestCase(1, true, false)]
        [TestCase(2, false, false)]
        public void HelperUI_DifficultySettings(int mode, bool expectHelpers, bool expectEnemyLevel)
        {
            var difficulty = RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("Current");
            var originalMode = difficulty.GetValue(null);
            try
            {
                difficulty.SetValue(null, Enum.ToObject(difficulty.PropertyType, mode));
                bool showHelpers = (bool)RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("ShowHelpers").GetValue(null);
                bool showEnemyLevel = (bool)RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("ShowEnemyLevel").GetValue(null);

                Assert.That(showHelpers, Is.EqualTo(expectHelpers));
                Assert.That(showEnemyLevel, Is.EqualTo(expectEnemyLevel));
            }
            finally
            {
                difficulty.SetValue(null, originalMode);
            }
        }
    }
}
