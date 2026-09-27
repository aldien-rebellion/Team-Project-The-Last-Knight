using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class PlayerSkillAndDashTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        private static void Invoke(Component target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);

        private static object GetProp(object target, string prop) => target.GetType().GetProperty(prop).GetValue(target);
        private static void SetProp(object target, string prop, object val) => target.GetType().GetProperty(prop).SetValue(target, val);
        private static void SetField(object target, string field, object val) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(target, val);
        private static float PlayerDamageMultiplier => (float)RuntimeType("TheLastKnight.Core.GameDifficultyManager")
            .GetProperty("PlayerDamage", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        [Test]
        public void Dash_EnablesInvincibility_AndAvoidsDamage()
        {
            GameObject player = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(20000, 20000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");

                float initialHP = (float)GetProp(stats, "CurrentHP");
                Assert.That(initialHP, Is.GreaterThan(0f));

                // Normal state: TakeDamage damages Arthur
                stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(stats, new object[] { 10f });
                float damagedHP = (float)GetProp(stats, "CurrentHP");
                Assert.That(damagedHP, Is.LessThan(initialHP));

                // Start Dash: Should become invincible
                Invoke(controller, "StartDash");
                bool isInvincible = (bool)GetProp(controller, "IsInvincible");
                Assert.IsTrue(isInvincible, "Player must be invincible during dash");

                // Taking damage during dash must be completely avoided
                stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(stats, new object[] { 30f });
                Assert.That((float)GetProp(stats, "CurrentHP"), Is.EqualTo(damagedHP), "HP should not decrease while invincible during dash");

                // End Dash
                Invoke(controller, "EndDash");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void Dash_PhasesThroughMonster_IgnoresEnemyColliders()
        {
            GameObject player = null, enemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(21000, 21000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var kcc = player.GetComponent(RuntimeType("TheLastKnight.Physics.KinematicCharacterController2D"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");

                enemy = new GameObject("TestEnemyForDash");
                enemy.transform.position = player.transform.position + Vector3.right;
                var enemyStats = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                var enemyCol = enemy.AddComponent<BoxCollider2D>();
                enemyCol.size = new Vector2(1f, 2f);
                Physics2D.SyncTransforms();

                // Before dash, enemy collider is detected as enemy
                var isEnemyMethod = kcc.GetType().GetMethod("IsEnemyCollider", BindingFlags.Public | BindingFlags.Static);
                bool isEnemy = (bool)isEnemyMethod.Invoke(null, new object[] { enemyCol });
                Assert.IsTrue(isEnemy, "Enemy collider should be identified as enemy");
                Assert.IsFalse((bool)GetProp(kcc, "IgnoreEnemies"), "IgnoreEnemies should default to false");

                // Start Dash: IgnoreEnemies set to true
                Invoke(controller, "StartDash");
                Assert.IsTrue((bool)GetProp(kcc, "IgnoreEnemies"), "IgnoreEnemies must be true during dash");

                // End Dash: IgnoreEnemies restored to false
                Invoke(controller, "EndDash");
                Assert.IsFalse((bool)GetProp(kcc, "IgnoreEnemies"), "IgnoreEnemies must be false after dash ends");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void Skill1_CarnageBurst_DealsConfigurableMultiplierDamage()
        {
            GameObject player = null, enemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(22000, 22000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "<CriticalChance>k__BackingField", 0f);

                enemy = new GameObject("CarnageBurst Test Enemy");
                enemy.transform.position = player.transform.position + Vector3.right;
                var enemyStats = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(enemyStats, "_maxHealth", 500f);
                SetField(enemyStats, "_defense", 0f);
                enemy.AddComponent<BoxCollider2D>();
                Invoke(enemyStats, "Awake");
                Physics2D.SyncTransforms();

                SetProp(controller, "SkillDamageMultiplier", 2.0f);
                Invoke(controller, "StartSkill");
                Invoke(controller, "ApplySkillHits");

                float hpAfterSkill = (float)GetProp(enemyStats, "CurrentHealth");
                float maxHP = (float)GetProp(enemyStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 2.0f * PlayerDamageMultiplier;

                Assert.That(hpAfterSkill, Is.LessThan(maxHP), "Skill 1 must deal damage to enemy");
                Assert.That(maxHP - hpAfterSkill, Is.EqualTo(expectedDamage).Within(1f));

                // Target hit set should prevent duplicate damage on same skill execution
                Invoke(controller, "ApplySkillHits");
                Assert.That((float)GetProp(enemyStats, "CurrentHealth"), Is.EqualTo(hpAfterSkill), "Skill 1 should not damage same enemy twice in one cast");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void Skill3_Excalibur_DealsConfigurableMultiplierDamage()
        {
            GameObject player = null, enemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(23000, 23000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "<CriticalChance>k__BackingField", 0f);

                enemy = new GameObject("Excalibur Test Enemy");
                enemy.transform.position = player.transform.position + new Vector3(5f, 0f, 0f);
                var enemyStats = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(enemyStats, "_maxHealth", 500f);
                SetField(enemyStats, "_defense", 0f);
                enemy.AddComponent<BoxCollider2D>();
                Invoke(enemyStats, "Awake");
                Physics2D.SyncTransforms();

                SetProp(controller, "ExcaliburDamageMultiplier", 5.0f);
                Invoke(controller, "StartExcalibur");
                Invoke(controller, "ApplyExcaliburHits");

                float hpAfterExcalibur = (float)GetProp(enemyStats, "CurrentHealth");
                float maxHP = (float)GetProp(enemyStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 5.0f * PlayerDamageMultiplier;

                Assert.That(hpAfterExcalibur, Is.LessThan(maxHP), "Skill 3 Excalibur must deal damage to enemy in beam path");
                Assert.That(maxHP - hpAfterExcalibur, Is.EqualTo(expectedDamage).Within(1f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void Skills_Cooldowns_CanBeAdjustedInInspector()
        {
            GameObject player = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));

                Assert.That((float)GetProp(controller, "DashIFrameDuration"), Is.EqualTo(0.15f).Within(0.001f));

                // Verify default or configurable cooldowns
                SetProp(controller, "SkillCooldown", 2.5f);
                SetProp(controller, "BuffCooldown", 4.0f);
                SetProp(controller, "ExcaliburCooldown", 8.0f);
                SetProp(controller, "DashCooldown", 0.8f);
                SetProp(controller, "DashIFrameDuration", 0.15f);

                Assert.That((float)GetProp(controller, "SkillCooldown"), Is.EqualTo(2.5f));
                Assert.That((float)GetProp(controller, "BuffCooldown"), Is.EqualTo(4.0f));
                Assert.That((float)GetProp(controller, "ExcaliburCooldown"), Is.EqualTo(8.0f));
                Assert.That((float)GetProp(controller, "DashCooldown"), Is.EqualTo(0.8f));
                Assert.That((float)GetProp(controller, "DashIFrameDuration"), Is.EqualTo(0.15f).Within(0.001f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
            }
        }
    }
}
