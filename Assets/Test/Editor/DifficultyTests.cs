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

        [TestCase(0, 4f, 1.5f)]
        [TestCase(1, 9f, 1f)]
        [TestCase(2, 15f, 0.4f)]
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
                stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(stats, new object[] { 10f });
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
                float expected = Read(stats, "AttackPower") * outgoingMultiplier * (critical ? 1.5f : 1f);
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
                stats.GetType().GetMethod("SetRegenAura").Invoke(stats, new object[] { stats, true });
                // Keep HP below the aura cap even when Easy halves incoming damage.
                float incomingMultiplier = (float)RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("EnemyDamage").GetValue(null);
                stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(stats, new object[] { 50f / incomingMultiplier });
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

                // Large deltaTime clamps to 70% MaxHP in Regen Aura
                stats.GetType().GetMethod("RegenerateHP").Invoke(stats, new object[] { 1000f });
                Assert.That(Read(stats, "CurrentHP"), Is.EqualTo(maxHP * 0.70f).Within(0.001f));
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

                stats.GetType().GetMethod("SetRegenAura").Invoke(stats, new object[] { stats, true });
                float incomingMultiplier = (float)RuntimeType("TheLastKnight.Core.GameDifficultyManager").GetProperty("EnemyDamage").GetValue(null);
                stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(stats, new object[] { 50f / incomingMultiplier });
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

        [TestCase(0, false, 2f / 3f)]
        [TestCase(1, false, 1f)]
        [TestCase(2, false, 4f / 3f)]
        [TestCase(0, true, 1f / 3f)]
        [TestCase(1, true, 0.5f)]
        [TestCase(2, true, 2f / 3f)]
        public void StaminaActions_UseDifficultyAndEnduranceCosts(int mode, bool endurance, float expectedMultiplier)
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
                stats.GetType().GetField("_currentLevel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stats, 20);
                if (endurance) Invoke(stats, "ApplyEnduranceBuff");

                var stamina = stats.GetType().GetField("_currentStamina", BindingFlags.Instance | BindingFlags.NonPublic);
                var state = controller.GetType().GetProperty("CurrentState");
                var idle = Enum.Parse(state.PropertyType, "Idle");
                var getCost = stats.GetType().GetMethod("GetStaminaCost");
                string[] actions = { "StartAttack", "StartDash", "StartSkill", "StartExcalibur" };
                float[] baseCosts = { 11.25f, 15f, 18.75f, 37.5f };
                for (int action = 0; action < actions.Length; action++)
                {
                    float expectedCost = baseCosts[action] * expectedMultiplier;
                    Assert.That((float)getCost.Invoke(stats, new object[] { baseCosts[action] }), Is.EqualTo(expectedCost).Within(0.001f));
                    state.SetValue(controller, idle);
                    stamina.SetValue(stats, expectedCost - 0.1f);
                    Invoke(controller, actions[action]);
                    Assert.That(state.GetValue(controller), Is.EqualTo(idle), actions[action] + " must reject insufficient stamina");
                    Assert.That(Read(stats, "CurrentStamina"), Is.EqualTo(expectedCost - 0.1f).Within(0.001f));

                    stamina.SetValue(stats, expectedCost);
                    Invoke(controller, actions[action]);
                    Assert.That(state.GetValue(controller), Is.Not.EqualTo(idle), actions[action] + " must accept exact stamina cost");
                    Assert.That(Read(stats, "CurrentStamina"), Is.EqualTo(0f).Within(0.001f));
                    if (actions[action] == "StartDash") Invoke(controller, "EndDash");
                }

                // Jump, double jump and sprint use the same spending path, including fractional frame costs.
                var spend = stats.GetType().GetMethod("TrySpendStamina");
                foreach (float baseCost in new[] { 11.25f, 11.25f * 0.1f })
                {
                    float expectedCost = baseCost * expectedMultiplier;
                    stamina.SetValue(stats, expectedCost);
                    Assert.That((bool)spend.Invoke(stats, new object[] { baseCost }), Is.True);
                    Assert.That(Read(stats, "CurrentStamina"), Is.EqualTo(0f).Within(0.001f));
                }
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
