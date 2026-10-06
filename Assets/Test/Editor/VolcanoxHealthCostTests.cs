using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class VolcanoxHealthCostTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static float Health(Component stats) => (float)stats.GetType().GetProperty("CurrentHealth").GetValue(stats);

        [TestCase(5000f, 4900f)]
        [TestCase(1600f, 1500f)]
        [TestCase(1550f, 1500f)]
        [TestCase(1500f, 1500f)]
        [TestCase(1000f, 1000f)]
        public void AttackCost_UsesMaxHealthAndStopsAtThirtyPercent(float startingHealth, float expected)
        {
            var go = new GameObject("Health cost test");
            try
            {
                var type = RuntimeType("TheLastKnight.Combat.EnemyStats");
                var stats = go.AddComponent(type);
                type.GetMethod("SetStats").Invoke(stats, new object[] { 5000f, 100f, 700f });
                type.GetField("<CurrentHealth>k__BackingField", Members).SetValue(stats, startingHealth);
                type.GetField("_attackHealthCostPercent", Members).SetValue(stats, 0.02f);
                type.GetMethod("ConsumeAttackHealth").Invoke(stats, null);
                Assert.That(Health(stats), Is.EqualTo(expected).Within(0.001f));
                if (expected <= 1500f)
                {
                    type.GetMethod("ConsumeAttackHealth").Invoke(stats, null);
                    Assert.That(Health(stats), Is.EqualTo(expected).Within(0.001f));
                }
                Assert.That(type.GetProperty("IsDead").GetValue(stats), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EachVolcanoxSkill_ConsumesHealthOnceAtAttackStart(int index)
        {
            var go = new GameObject("Skill cost test");
            go.SetActive(false);
            try
            {
                var controllerType = RuntimeType("TheLastKnight.AI.EnemyController");
                var statsType = RuntimeType("TheLastKnight.Combat.EnemyStats");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Volcanox.prefab");
                var prefabStats = prefab.GetComponent(statsType);
                var controller = go.AddComponent(controllerType);
                var stats = go.GetComponent(statsType);
                statsType.GetMethod("SetStats").Invoke(stats, new object[] { 5000f, 100f, 700f });
                foreach (var name in new[] { "_attackHealthCostPercent", "_attackHealthCostFloorPercent" })
                    statsType.GetField(name, Members).SetValue(stats, statsType.GetField(name, Members).GetValue(prefabStats));
                controllerType.GetField("_stats", Members).SetValue(controller, stats);
                controllerType.GetField("_isBoss", Members).SetValue(controller, true);
                var skills = (Array)controllerType.GetProperty("Skills").GetValue(prefab.GetComponent(controllerType));
                var routine = (IEnumerator)controllerType.GetMethod("ExecuteSkillRoutine", Members)
                    .Invoke(controller, new[] { skills.GetValue(index), (object)false });
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(Health(stats), Is.EqualTo(4900f).Within(0.001f));
                // The next yield is animation synchronization, not another attack.
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(Health(stats), Is.EqualTo(4900f).Within(0.001f));
                (routine as IDisposable)?.Dispose();
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void OrdinaryEnemy_AttackHasNoHealthCost()
        {
            var go = new GameObject("Ordinary enemy");
            try
            {
                var type = RuntimeType("TheLastKnight.Combat.EnemyStats");
                var stats = go.AddComponent(type);
                type.GetMethod("SetStats").Invoke(stats, new object[] { 100f, 0f, 10f });
                type.GetMethod("ConsumeAttackHealth").Invoke(stats, null);
                Assert.That(Health(stats), Is.EqualTo(100f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
