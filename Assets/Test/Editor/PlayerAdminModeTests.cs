using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class PlayerAdminModeTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject _player;
        private Component _admin;
        private Component _stats;
        private Component _controller;

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Members).Invoke(target, null);
        private void SetInvincible(bool active) => _admin.GetType().GetMethod("SetInfiniteHealth").Invoke(_admin, new object[] { active });
        private bool Keys(bool g, bool gp, bool o, bool op, bool d, bool dp) =>
            (bool)_admin.GetType().GetMethod("AdvanceToggleSequence", Members).Invoke(_admin, new object[] { g, gp, o, op, d, dp });

        [SetUp]
        public void SetUp()
        {
            _player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            _player.transform.position = new Vector3(30000f, 30000f, 0f);
            _controller = _player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
            _stats = _player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
            Invoke(_controller, "Awake");
            Invoke(_stats, "Awake");
            _admin = _player.AddComponent(RuntimeType("TheLastKnight.Player.PlayerAdminMode"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
        }

        [TestCase("float")]
        [TestCase("data")]
        [TestCase("stun")]
        public void Invincible_ImmediatelyBlocksLethalDamageThroughEveryOverload_WhenPanelClosed(string overload)
        {
            SetInvincible(true);
            float hp = (float)Property(_stats, "CurrentHP");
            Assert.That(hp, Is.GreaterThan(0f));
            Assert.That(Property(_admin, "IsOpen"), Is.False);
            Assert.That(Property(_controller, "IsInvincible"), Is.True);
            object state = Property(_controller, "CurrentState");
            if (overload == "float")
                _stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(_stats, new object[] { hp * 100f });
            else if (overload == "stun")
                _stats.GetType().GetMethod("TakeDamage", new[] { typeof(float), typeof(bool), typeof(float) })
                    .Invoke(_stats, new object[] { hp * 100f, true, 5f });
            else
            {
                var damageType = RuntimeType("TheLastKnight.Combat.DamageData");
                var data = Activator.CreateInstance(damageType);
                damageType.GetField("amount").SetValue(data, hp * 100f);
                _stats.GetType().GetMethod("TakeDamage", new[] { damageType }).Invoke(_stats, new[] { data });
            }
            Assert.That(Property(_stats, "CurrentHP"), Is.EqualTo(hp));
            Assert.That(Property(_stats, "IsDead"), Is.False);
            Assert.That(Property(_controller, "CurrentState"), Is.EqualTo(state));
        }

        [Test]
        public void Invincible_BlocksDirectHurtReaction_AndTurningOffRestoresDamage()
        {
            SetInvincible(true);
            object state = Property(_controller, "CurrentState");
            Invoke(_controller, "OnTakeDamage");
            Assert.That(Property(_controller, "CurrentState"), Is.EqualTo(state));
            SetInvincible(false);
            Assert.That(Property(_controller, "IsInvincible"), Is.False);
            float hp = (float)Property(_stats, "CurrentHP");
            _stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(_stats, new object[] { hp * 0.5f });
            Assert.That((float)Property(_stats, "CurrentHP"), Is.LessThan(hp));
        }

        [Test]
        public void AdminPermissions_AreIndependent_AndDisableClearsThem()
        {
            SetInvincible(true);
            Assert.That(Property(_controller, "AdminNoCooldown"), Is.False);
            Assert.That(Property(_controller, "AdminStatusImmunity"), Is.False);
            Invoke(_admin, "OnDisable");
            Assert.That(Property(_stats, "AdminInvincible"), Is.False);
            Invoke(_admin, "OnEnable");
            Assert.That(Property(_stats, "AdminInvincible"), Is.True);
        }

        [Test]
        public void HoldGThenOThenD_TogglesOnce_AndRequiresFullReleaseToRepeat()
        {
            Assert.That(Keys(true, true, false, false, false, false), Is.False);
            Assert.That(Keys(true, false, true, true, false, false), Is.False);
            Assert.That(Keys(true, false, true, false, true, true), Is.True);
            Assert.That(Keys(true, false, true, false, true, false), Is.False);
            Assert.That(Keys(true, false, true, false, false, false), Is.False);
            Assert.That(Keys(true, false, true, false, true, true), Is.False);
            Keys(false, false, false, false, false, false);
            Keys(true, true, false, false, false, false);
            Keys(true, false, true, true, false, false);
            Assert.That(Keys(true, false, true, false, true, true), Is.True);
        }

        [TestCase("OGD")]
        [TestCase("GDO")]
        [TestCase("DOG")]
        [TestCase("ODG")]
        [TestCase("DGO")]
        public void WrongKeyOrder_DoesNotToggle(string order)
        {
            bool g = false, o = false, d = false;
            foreach (char key in order)
            {
                g |= key == 'G'; o |= key == 'O'; d |= key == 'D';
                Assert.That(Keys(g, key == 'G', o, key == 'O', d, key == 'D'), Is.False);
            }
        }

        [Test]
        public void SimultaneousChord_AndReleasedPrefix_DoNotToggle()
        {
            Assert.That(Keys(true, true, true, true, true, true), Is.False);
            Keys(false, false, false, false, false, false);
            Keys(true, true, false, false, false, false);
            Keys(false, false, true, true, false, false);
            Assert.That(Keys(false, false, true, false, true, true), Is.False);
            Keys(false, false, false, false, false, false);
            Keys(true, true, false, false, false, false);
            Keys(true, false, true, true, false, false);
            Assert.That(Keys(true, false, false, false, true, true), Is.False);
        }

        [TestCase(1, 1)]
        [TestCase(9, 1)]
        [TestCase(19, 1)]
        [TestCase(1, 10)]
        [TestCase(1, 100)]
        [TestCase(170, 100)]
        public void AddLevels_GrantsRequestedLevelsAndNormalPoints_RestoresHP_PreservesEXP(int level, int amount)
        {
            Field(_stats, "_currentLevel", level);
            Field(_stats, "_currentEXP", 25);
            Field(_stats, "_currentHP", 1f);
            int points = (int)Property(_stats, "StatPoints");
            var template = _stats.GetType().GetField("_statsTemplate", Members).GetValue(_stats);
            int reward = (int)template.GetType().GetField("statPointsPerLevel").GetValue(template);
            if (amount == 1) Invoke(_stats, "AddLevel");
            else _stats.GetType().GetMethod("AddLevels").Invoke(_stats, new object[] { amount });
            Assert.That(Property(_stats, "Level"), Is.EqualTo(level + amount));
            Assert.That(Property(_stats, "EXP"), Is.EqualTo(25));
            Assert.That(Property(_stats, "StatPoints"), Is.EqualTo(points + reward * amount));
            Assert.That(Property(_stats, "CurrentHP"), Is.EqualTo(Property(_stats, "MaxHP")));
            Assert.That(Property(_controller, "IsSkill2Unlocked"), Is.EqualTo(level + amount >= 10));
            Assert.That(Property(_controller, "IsSkill3Unlocked"), Is.EqualTo(level + amount >= 20));
            Assert.That((int)Property(_stats, "EXPNeeded"), Is.GreaterThan(0));
            _stats.GetType().GetMethod("AddEXP").Invoke(_stats, new object[] { 1 });
            Assert.That(Property(_stats, "EXP"), Is.EqualTo(26));
            Assert.That(Property(_stats, "Level"), Is.EqualTo(level + amount));
        }

        [Test]
        public void InputSettings_ExcludeAdminBindings()
        {
            var actions = (IEnumerable)RuntimeType("TheLastKnight.Input.KeyRebindManager")
                .GetMethod("GetRebindableActions").Invoke(null, null);
            Assert.That(actions.Cast<object>().Select(a => (string)Property(a, "ActionName")),
                Has.None.StartsWith("AdminMode"));
        }
    }
}
