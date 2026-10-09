using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class MedusaBossCheckpointTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject _bossObject, _arenaObject, _statueObject, _playerObject;
        private Component _boss, _arena, _statue, _aura, _player;
        private static Type TypeFor(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static void Call(object target, string name) => target.GetType().GetMethod(name, Members).Invoke(target, null);

        [SetUp]
        public void SetUp()
        {
            _bossObject = new GameObject("Checkpoint boss test");
            _boss = _bossObject.AddComponent(TypeFor("TheLastKnight.Combat.EnemyStats"));
            _boss.GetType().GetMethod("SetStats").Invoke(_boss, new object[] { 100f, 0f, 10f });
            _arenaObject = new GameObject("Checkpoint arena test");
            _arena = _arenaObject.AddComponent(TypeFor("TheLastKnight.Environment.BossArena"));
            Field(_arena, "boss", _boss);
            _statueObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Medusa Save Point.prefab"));
            _statue = _statueObject.GetComponent(TypeFor("TheLastKnight.Environment.MedusaSavePoint"));
            _aura = _statueObject.GetComponent(TypeFor("TheLastKnight.Environment.MedusaAura"));
            Field(_statue, "bossCombatArena", _arena);
            Call(_aura, "Awake");
            _playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            _player = _playerObject.GetComponent(TypeFor("TheLastKnight.Stats.PlayerStats"));
            Call(_playerObject.GetComponent(TypeFor("TheLastKnight.Player.PlayerController")), "Awake");
            Call(_player, "Awake");
            Field(_player, "_currentHP", 50f);
            _player.GetType().GetMethod("SetLastDamageTimeForTesting").Invoke(_player, new object[] { Time.time - 10f });
            _player.GetType().GetMethod("SetRegenAura").Invoke(_player, new object[] { _aura, true });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _playerObject, _statueObject, _arenaObject, _bossObject })
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void NewCheckpoint_HealsBeforeFight_BlocksImmediatelyAfterArenaEntry_ResumesAfterBossDeath()
        {
            Assert.That(Property(_statue, "IsBossCombatBlocked"), Is.False);
            _player.GetType().GetMethod("RegenerateHP").Invoke(_player, new object[] { 1f });
            Assert.That((float)Property(_player, "CurrentHP"), Is.GreaterThan(50f));
            float hp = (float)Property(_player, "CurrentHP");
            Field(_arena, "_entered", true);
            Assert.That(Property(_statue, "IsBossCombatBlocked"), Is.True);
            Assert.That(Property(_aura, "CanHeal"), Is.False);
            Assert.That(Property(_player, "HasRegenAura"), Is.False, "The active aura must stop before the next trigger callback.");
            _player.GetType().GetMethod("RegenerateHP").Invoke(_player, new object[] { 100f });
            Assert.That(Property(_player, "CurrentHP"), Is.EqualTo(hp));
            Field(_boss, "<IsDead>k__BackingField", true);
            Assert.That(Property(_statue, "IsBossCombatBlocked"), Is.False);
            Assert.That(Property(_aura, "CanHeal"), Is.True);
            _player.GetType().GetMethod("RegenerateHP").Invoke(_player, new object[] { 1f });
            Assert.That((float)Property(_player, "CurrentHP"), Is.GreaterThan(hp));
        }

        [Test]
        public void InjuredBossOutsideArena_StillBlocksHealing_WithoutChangingExistingStatues()
        {
            Field(_boss, "<CurrentHealth>k__BackingField", 90f);
            Assert.That(Property(_arena, "IsCombatActive"), Is.True);
            Assert.That(Property(_aura, "CanHeal"), Is.False);
            Field(_statue, "bossCombatArena", null);
            Assert.That(Property(_statue, "IsBossCombatBlocked"), Is.False);
            Assert.That(Property(_aura, "CanHeal"), Is.True);
            Assert.That(Property(_player, "HasRegenAura"), Is.True);
        }

        [TestCase("Chase")]
        [TestCase("MeleeAttack")]
        [TestCase("RangedAttack")]
        [TestCase("Skill")]
        public void FullHealthBoss_EngagedBeforeArenaEntry_BlocksHealing(string state)
        {
            var controller = _bossObject.AddComponent(TypeFor("TheLastKnight.AI.EnemyController"));
            Field(controller, "_currentState", Enum.Parse(TypeFor("TheLastKnight.AI.EnemyAIState"), state));
            Assert.That(Property(_arena, "IsCombatActive"), Is.True);
            Assert.That(Property(_aura, "CanHeal"), Is.False);
        }

        [Test]
        public void DisabledBoss_DoesNotBlockCheckpoint()
        {
            Field(_arena, "_entered", true);
            _bossObject.SetActive(false);
            Assert.That(Property(_statue, "IsBossCombatBlocked"), Is.False);
        }

        [TestCase("CityCenter")]
        [TestCase("OutdoorMarket")]
        [TestCase("SuburbToForest")]
        public void AuthoredCheckpoint_IsAdditional_OutsideBossDetection_AndLinkedToItsOwnArena(string name)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Maps/" + name + ".unity");
            try
            {
                var points = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(TypeFor("TheLastKnight.Environment.MedusaSavePoint"), true)).ToArray();
                Assert.That(points.Length, Is.EqualTo(2));
                var added = points.Single(p => p.name == "Medusa Pre-Boss Save Point");
                var original = points.Single(p => p != added);
                var linked = (Component)added.GetType().GetField("bossCombatArena").GetValue(added);
                Assert.That(linked, Is.Not.Null);
                Assert.That(linked.gameObject.scene, Is.EqualTo(scene));
                Assert.That(original.GetType().GetField("bossCombatArena").GetValue(original), Is.Null);
                Assert.That(added.GetType().GetField("grantsCityRune").GetValue(added), Is.False);
                if (name == "CityCenter") Assert.That(original.GetType().GetField("grantsCityRune").GetValue(original), Is.True);
                var boss = (Component)linked.GetType().GetField("boss").GetValue(linked);
                float distance = Vector2.Distance(added.transform.position, boss.transform.position);
                float sight = (float)Property(boss.GetComponent(TypeFor("TheLastKnight.AI.EnemyController")), "DetectionRange");
                Assert.That(distance, Is.GreaterThanOrEqualTo(35f));
                Assert.That(distance - added.GetComponent<CircleCollider2D>().radius, Is.GreaterThan(sight));
                Assert.That(added.transform.position.x, Is.LessThan(boss.transform.position.x));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
