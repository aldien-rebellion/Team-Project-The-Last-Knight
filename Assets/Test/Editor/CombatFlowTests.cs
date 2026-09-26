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

        [Test]
        public void EnemySkill_IsReady_EvaluatesDistanceAndCooldown()
        {
            var skillType = RuntimeType("TheLastKnight.Combat.EnemySkill");
            var skill = Activator.CreateInstance(skillType);
            skillType.GetField("skillName").SetValue(skill, "Heavy Smash");
            skillType.GetField("minRange").SetValue(skill, 0f);
            skillType.GetField("maxRange").SetValue(skill, 2f);
            skillType.GetField("cooldown").SetValue(skill, 5f);
            skillType.GetField("nextReadyTime").SetValue(skill, 10f);

            var isReadyMethod = skillType.GetMethod("IsReady");
            bool notReadyEarly = (bool)isReadyMethod.Invoke(skill, new object[] { 1.5f, 8f });
            bool notReadyFar = (bool)isReadyMethod.Invoke(skill, new object[] { 3.0f, 12f });
            bool ready = (bool)isReadyMethod.Invoke(skill, new object[] { 1.5f, 12f });

            Assert.IsFalse(notReadyEarly, "Skill should not be ready before cooldown expires");
            Assert.IsFalse(notReadyFar, "Skill should not be ready if target is out of max range");
            Assert.IsTrue(ready, "Skill should be ready when within range and cooldown passed");
        }

        [Test]
        public void EnemyController_ParryMechanics_DistinguishSkillsAndBasicAttack()
        {
            var go = new GameObject("TestEnemyController");
            try
            {
                go.AddComponent<Rigidbody2D>();
                go.AddComponent<Animator>();
                go.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                var ctrl = go.AddComponent(RuntimeType("TheLastKnight.AI.EnemyController"));

                var hasParryableSkillMethod = ctrl.GetType().GetMethod("HasParryableSkill");
                Assert.IsFalse((bool)hasParryableSkillMethod.Invoke(ctrl, null));

                var skillType = RuntimeType("TheLastKnight.Combat.EnemySkill");
                var skillArray = Array.CreateInstance(skillType, 2);
                var s1 = Activator.CreateInstance(skillType);
                skillType.GetField("skillName").SetValue(s1, "Spin");
                skillType.GetField("isParryable").SetValue(s1, false);

                var s2 = Activator.CreateInstance(skillType);
                skillType.GetField("skillName").SetValue(s2, "BigSmash");
                skillType.GetField("isParryable").SetValue(s2, true);
                skillType.GetField("damageMultiplier").SetValue(s2, 2.0f);

                skillArray.SetValue(s1, 0);
                skillArray.SetValue(s2, 1);

                ctrl.GetType().GetMethod("SetSkills").Invoke(ctrl, new object[] { skillArray });

                Assert.IsTrue((bool)hasParryableSkillMethod.Invoke(ctrl, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
