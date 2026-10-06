using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class DemonBossDamageTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        [TestCase(100f, 100f, true, true, 20f)]
        [TestCase(70f, 100f, true, true, 26f)]
        [TestCase(50f, 100f, true, true, 30f)]
        [TestCase(1f, 100f, true, true, 39.8f)]
        [TestCase(120f, 100f, true, true, 20f)]
        [TestCase(0f, 0f, true, true, 20f)]
        [TestCase(50f, 100f, false, true, 20f)]
        [TestCase(50f, 100f, true, false, 20f)]
        public void DamageFlow_ScalesOnlyEnabledPlayerAttacks(float hp, float maxHP, bool enabled,
            bool playerAttack, float expectedDamage)
        {
            var player = new GameObject("Damage test player");
            player.SetActive(false);
            var enemy = new GameObject("Damage test target");
            var source = new GameObject("Non-player source");
            var popupType = RuntimeType("TheLastKnight.Combat.FloatingCombatText");
            var existingPopups = UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None);
            try
            {
                var playerType = RuntimeType("TheLastKnight.Stats.PlayerStats");
                var stats = player.AddComponent(playerType);
                playerType.GetField("_currentHP", Members).SetValue(stats, hp);
                playerType.GetField("<MaxHP>k__BackingField", Members).SetValue(stats, maxHP);
                var enemyType = RuntimeType("TheLastKnight.Combat.EnemyStats");
                var target = enemy.AddComponent(enemyType);
                enemyType.GetMethod("SetStats").Invoke(target, new object[] { 800f, 10f, 50f });
                enemyType.GetField("_scalePlayerDamageWithMissingHealth", Members).SetValue(target, enabled);
                var damageType = RuntimeType("TheLastKnight.Combat.DamageData");
                var damage = Activator.CreateInstance(damageType);
                damageType.GetField("amount").SetValue(damage, 30f);
                damageType.GetField("attacker").SetValue(damage, playerAttack ? player : source);
                enemyType.GetMethod("TakeDamage", new[] { damageType }).Invoke(target, new[] { damage });
                Assert.That(800f - (float)enemyType.GetProperty("CurrentHealth").GetValue(target),
                    Is.EqualTo(expectedDamage).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(source);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None))
                    if (!existingPopups.Contains(popup)) UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void DemonBossPrefab_EnablesMissingHealthDamageBonus()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/DemonBoss.prefab");
            var stats = prefab.GetComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
            Assert.That(stats.GetType().GetField("_scalePlayerDamageWithMissingHealth", Members).GetValue(stats), Is.True);
        }
    }
}
