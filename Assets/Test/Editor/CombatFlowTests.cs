using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class CombatFlowTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static void Invoke(Component target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);

        [Test]
        public void SwordSwing_DamagesEnemyOnceDespiteMultipleColliders_AndCanKill()
        {
            GameObject player = null, enemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(10000, 10000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                enemy = new GameObject("Combat Test Enemy");
                enemy.transform.position = player.transform.position + Vector3.right;
                var enemyStats = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                enemy.AddComponent<BoxCollider2D>();
                enemy.AddComponent<CircleCollider2D>();
                Invoke(enemyStats, "Awake");
                Physics2D.SyncTransforms();
                Invoke(controller, "StartAttack");
                Invoke(controller, "ApplyAttackHits");
                var health = enemyStats.GetType().GetProperty("CurrentHealth");
                float afterHit = (float)health.GetValue(enemyStats);
                Assert.That(afterHit, Is.LessThan(50f));
                Invoke(controller, "ApplyAttackHits");
                Assert.That((float)health.GetValue(enemyStats), Is.EqualTo(afterHit));
                for (int i = 0; i < 10; i++)
                {
                    Invoke(controller, "StartAttack");
                    Invoke(controller, "ApplyAttackHits");
                }
                Assert.That((bool)enemyStats.GetType().GetProperty("IsDead").GetValue(enemyStats), Is.True);
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }
    }
}
