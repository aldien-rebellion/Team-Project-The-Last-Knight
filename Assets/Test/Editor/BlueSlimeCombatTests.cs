using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TheLastKnight.Tests
{
    public class BlueSlimeCombatTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<GameObject> _created = new List<GameObject>();
        private HashSet<Object> _existingPopups;

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name)).First(type => type != null);

        private static object Invoke(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, InstanceMembers).Invoke(target, args);

        private static T Read<T>(object target, string name) => (T)target.GetType()
            .GetField(name, InstanceMembers).GetValue(target);

        private static void Write(object target, string name, object value) => target.GetType()
            .GetField(name, InstanceMembers).SetValue(target, value);

        private static bool Property(Component target, string name) => (bool)target.GetType()
            .GetProperty(name).GetValue(target);

        [SetUp]
        public void SetUp()
        {
            _existingPopups = new HashSet<Object>(Object.FindObjectsByType(
                RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var actor in _created)
                if (actor != null) Object.DestroyImmediate(actor);
            _created.Clear();

            foreach (var popup in Object.FindObjectsByType(
                RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                if (!_existingPopups.Contains(popup))
                    Object.DestroyImmediate(((Component)popup).gameObject);
        }

        private Component CreateEnemy(string name = "BlueSlime")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab");
            Assert.That(prefab, Is.Not.Null);
            var actor = Object.Instantiate(prefab);
            _created.Add(actor);
            var controller = actor.GetComponent(RuntimeType("TheLastKnight.AI.EnemyController"));
            Invoke(controller, "Awake");
            Invoke(actor.GetComponent(RuntimeType("TheLastKnight.Combat.EnemyStats")), "Awake");
            return controller;
        }

        [Test]
        public void BlueSlime_UsesGuideSkillsAndNoBasicCooldown()
        {
            var controller = CreateEnemy();
            Assert.That(Read<float>(controller, "_meleeCooldown"), Is.Zero);
            var skills = Read<Array>(controller, "_skills").Cast<object>().ToArray();
            Assert.That(skills.Select(s => Read<string>(s, "skillName")),
                Is.EqualTo(new[] { "HeavySlam", "DoubleHop", "SlideTackle" }));
            Assert.That(skills.Select(s => Read<bool>(s, "isParryable")),
                Is.EqualTo(new[] { true, false, false }));
            Assert.That(skills.Select(s => Read<float>(s, "damageMultiplier")),
                Is.EqualTo(new[] { 1.6f, 1.3f, 1.2f }));
            Assert.That(skills.Select(s => Read<float>(s, "cooldown")),
                Is.EqualTo(new[] { 6f, 0f, 0f }));
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[0]));
            Write(skills[0], "nextReadyTime", Time.time + 6f);
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[1]));
            Invoke(controller, "PerformSkill", skills[1]);
            Invoke(controller, "StopAttack");
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[2]));
            Write(skills[0], "nextReadyTime", 0f);
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[0]));
            Invoke(controller, "PerformSkill", skills[0]);
            Invoke(controller, "StopAttack");
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[2]));
            Invoke(controller, "PerformSkill", skills[2]);
            Invoke(controller, "StopAttack");
            Assert.That(Invoke(controller, "GetReadySkill", 0.02f), Is.SameAs(skills[1]));
            Assert.That(Invoke(controller, "GetReadySkill", 3f), Is.Null);
            Assert.That(controller.GetComponent<Animator>().HasState(0, Animator.StringToHash("Run+Attack")), Is.True);
        }

        [Test]
        public void BlueSlime_AttacksWhenBodiesAreCloseButPivotsAreOutsideSkillRange()
        {
            var controller = CreateEnemy();
            var player = new GameObject("Close body target");
            _created.Add(player);
            player.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
            player.transform.position = controller.transform.position + new Vector3(3f, 0.65f, 0f);
            Write(controller, "_player", player);
            Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(controller.transform.position, player.transform.position), Is.GreaterThan(2f));
            Assert.That((float)Invoke(controller, "GetAttackDistance"), Is.GreaterThan(0.05f));
            var animator = controller.GetComponent<Animator>();
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            Invoke(controller, "Update");
            Assert.That(Read<bool>(controller, "_isActionLocked"), Is.False,
                "Slime must approach instead of attacking across an empty gap.");
            player.transform.position = controller.transform.position + new Vector3(2.35f, 0.65f, 0f);
            Physics2D.SyncTransforms();
            Assert.That((float)Invoke(controller, "GetAttackDistance"), Is.LessThanOrEqualTo(0.05f));
            Invoke(controller, "Update");
            Assert.That(Read<bool>(controller, "_isActionLocked"), Is.True);
            Assert.That(Property(controller.GetComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver")), "IsWindingUp"), Is.True);
            Invoke(controller, "StopAttack");
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        public void BlueSlime_OnlyHeavySlamOpensParryWindow(int index, bool parryable)
        {
            var controller = CreateEnemy();
            var parry = controller.GetComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver"));
            var skill = Read<Array>(controller, "_skills").GetValue(index);
            Invoke(parry, "BeginWindup");
            var routine = (IEnumerator)Invoke(controller, "ExecuteSkillRoutine", skill);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(Property(parry, "IsWindingUp"), Is.EqualTo(parryable));
            Invoke(controller, "StopAttack");
            (routine as IDisposable)?.Dispose();
        }

        [Test]
        public void BlueSlime_BasicAttackDoesNotOpenParryWindow()
        {
            var controller = CreateEnemy();
            Invoke(controller, "PerformMeleeAttack");
            var parry = controller.GetComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver"));
            Assert.That(Property(parry, "IsWindingUp"), Is.False);
        }

        [TestCase("CancelAttack")]
        [TestCase("OnDisable")]
        public void InterruptedSkill_ClosesDamageAndParryWindows(string method)
        {
            var controller = CreateEnemy();
            var parry = controller.GetComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver"));
            Invoke(parry, "BeginWindup");
            Write(controller, "_damageUntil", Time.time + 10f);
            Write(controller, "_currentAttackMultiplier", 1.6f);
            Write(controller, "_isActionLocked", true);
            Invoke(controller, method);
            Assert.That(Property(parry, "IsWindingUp"), Is.False);
            Assert.That(Read<float>(controller, "_damageUntil"), Is.Zero);
            Assert.That(Read<float>(controller, "_currentAttackMultiplier"), Is.EqualTo(1f));
            Assert.That(Read<bool>(controller, "_isActionLocked"), Is.False);
        }

        [Test]
        public void EnemyWithoutParryableSkills_BasicParryHasFourSecondCooldown()
        {
            var controller = CreateEnemy("ArchDemon");
            var parry = controller.GetComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver"));
            Invoke(controller, "PerformMeleeAttack");
            Assert.That(Property(parry, "IsWindingUp"), Is.True);
            Assert.That(Read<float>(controller, "_nextBasicParryTime"), Is.GreaterThanOrEqualTo(Time.time + 3.9f));
            Invoke(controller, "StopAttack");
            Invoke(controller, "PerformMeleeAttack");
            Assert.That(Property(parry, "IsWindingUp"), Is.False);
        }
    }
}
