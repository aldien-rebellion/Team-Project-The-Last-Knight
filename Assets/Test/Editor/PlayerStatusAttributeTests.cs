using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class PlayerStatusAttributeTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        private static void Invoke(Component target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, null);

        private static object GetProp(object target, string prop) => target.GetType().GetProperty(prop).GetValue(target);
        private static void SetProp(object target, string prop, object val) => target.GetType().GetProperty(prop).SetValue(target, val);
        private static void SetField(object target, string field, object val) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(target, val);
        private static object GetField(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(target);

        private GameObject _player;
        private Component _controller;
        private Component _stats;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            _player = UnityEngine.Object.Instantiate(prefab);
            _player.transform.position = new Vector3(30000, 30000, 0);
            _controller = _player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
            _stats = _player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));

            var kcc = _player.GetComponent(RuntimeType("TheLastKnight.Physics.KinematicCharacterController2D"));
            Invoke(kcc, "Awake");
            Invoke(_controller, "Awake");
            Invoke(_stats, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null)
            {
                UnityEngine.Object.DestroyImmediate(_player);
            }
        }

        [Test]
        public void STR_IncreasesAttackPower()
        {
            float initialAtk = (float)GetProp(_stats, "AttackPower");
            Assert.That(initialAtk, Is.GreaterThan(0f));

            var addPoints = _stats.GetType().GetMethod("AddStatPoints");
            addPoints.Invoke(_stats, new object[] { 5 });

            var upgradeMethod = _stats.GetType().GetMethod("UpgradeStat");
            for (int i = 0; i < 5; i++)
            {
                upgradeMethod.Invoke(_stats, new object[] { "STR" });
            }

            float newAtk = (float)GetProp(_stats, "AttackPower");
            int str = (int)GetProp(_stats, "STR");

            Assert.That(newAtk, Is.GreaterThan(initialAtk));
            Assert.That(str, Is.EqualTo(15));
        }

        [Test]
        public void AGI_IncreasesMovementSpeed_AndAttackSpeed()
        {
            float initialMoveSpeed = (float)GetProp(_controller, "MoveSpeed");
            float initialAtkSpeed = (float)GetProp(_stats, "AttackSpeedMultiplier");

            SetField(_stats, "_agility", 50);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });

            float newMoveSpeed = (float)GetProp(_controller, "MoveSpeed");
            float newAtkSpeed = (float)GetProp(_stats, "AttackSpeedMultiplier");
            float ctrlAtkSpeed = (float)GetProp(_controller, "AttackSpeedMultiplier");

            Assert.That(newMoveSpeed, Is.GreaterThan(initialMoveSpeed));
            Assert.That(newAtkSpeed, Is.GreaterThan(initialAtkSpeed));
            Assert.That(ctrlAtkSpeed, Is.EqualTo(newAtkSpeed));
        }

        [Test]
        public void AGI_AtOrAbove250_UnlocksDoubleJump()
        {
            int agi = (int)GetProp(_stats, "AGI");
            Assert.That(agi, Is.LessThan(250));
            Assert.IsFalse((bool)GetProp(_stats, "CanDoubleJump"), "Double jump should be locked when AGI < 250");
            Assert.IsFalse((bool)GetProp(_controller, "CanDoubleJump"), "Controller double jump should be locked when AGI < 250");

            // Set AGI directly to 250
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });

            int finalAgi = (int)GetProp(_stats, "AGI");
            Assert.That(finalAgi, Is.EqualTo(250));
            Assert.IsTrue((bool)GetProp(_stats, "CanDoubleJump"), "Double jump should be unlocked when AGI >= 250");
            Assert.IsTrue((bool)GetProp(_controller, "CanDoubleJump"), "Controller double jump should be active when AGI >= 250");
        }

        [Test]
        public void VIT_IncreasesMaxHP_AndMaxStamina()
        {
            float initialMaxHP = (float)GetProp(_stats, "MaxHP");
            float initialMaxStamina = (float)GetProp(_stats, "MaxStamina");

            SetField(_stats, "_vitality", 30);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });

            float newMaxHP = (float)GetProp(_stats, "MaxHP");
            float newMaxStamina = (float)GetProp(_stats, "MaxStamina");
            float curStamina = (float)GetProp(_stats, "CurrentStamina");
            float stmPct = (float)GetProp(_stats, "StaminaPercentage");

            Assert.That(newMaxHP, Is.GreaterThan(initialMaxHP), "Max HP must increase with VIT");
            Assert.That(newMaxStamina, Is.GreaterThan(initialMaxStamina), "Max Stamina must increase with VIT");
            Assert.That(curStamina, Is.GreaterThan(0f));
            Assert.That(stmPct, Is.GreaterThan(0f));
        }

        [Test]
        public void DEX_AsymptoticCritChance_Approaches100Percent_AndReaches99At250()
        {
            var soType = RuntimeType("TheLastKnight.Stats.CharacterStatsSO");
            var template = AssetDatabase.LoadAssetAtPath("Assets/Settings/PlayerStatsTemplate.asset", soType);
            Assert.IsNotNull(template, "PlayerStatsTemplate must exist");

            var calcMethod = soType.GetMethod("CalculateCritChance");

            // 0 DEX -> 0%
            float crit0 = (float)calcMethod.Invoke(template, new object[] { 0 });
            Assert.That(crit0, Is.EqualTo(0f));

            // Base DEX (10)
            float crit10 = (float)calcMethod.Invoke(template, new object[] { 10 });
            Assert.That(crit10, Is.GreaterThan(0f).And.LessThan(25f));

            // Mid DEX (50, 100, 200)
            float crit50 = (float)calcMethod.Invoke(template, new object[] { 50 });
            float crit100 = (float)calcMethod.Invoke(template, new object[] { 100 });
            float crit200 = (float)calcMethod.Invoke(template, new object[] { 200 });

            Assert.That(crit50, Is.GreaterThan(crit10));
            Assert.That(crit100, Is.GreaterThan(crit50));
            Assert.That(crit200, Is.GreaterThan(crit100));

            // Cap at 250 DEX -> 99.0%
            float crit250 = (float)calcMethod.Invoke(template, new object[] { 250 });
            Assert.That(crit250, Is.EqualTo(99.0f).Within(0.05f), "At 250 DEX, critical rate must be 99%");

            // Beyond 250 DEX -> Approaching 100% asymptotically without exceeding 100%
            float crit500 = (float)calcMethod.Invoke(template, new object[] { 500 });
            float crit1000 = (float)calcMethod.Invoke(template, new object[] { 1000 });

            Assert.That(crit500, Is.GreaterThan(crit250));
            Assert.That(crit500, Is.LessThanOrEqualTo(100f));
            Assert.That(crit1000, Is.GreaterThanOrEqualTo(crit500));
            Assert.That(crit1000, Is.LessThanOrEqualTo(100f));
        }

        [Test]
        public void DoubleJump_InAir_AppliesJumpForceWhenUnlocked()
        {
            // Set AGI directly to 250
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });
            Assert.IsTrue((bool)GetProp(_controller, "CanDoubleJump"));

            // Simulate falling in air
            SetField(_controller, "_velocity", new Vector2(0, -10f));
            SetField(_controller, "_jumpBufferCounter", 0.15f);
            SetField(_controller, "_coyoteTimeCounter", -1f);

            // Call UpdateNormalMovement
            Invoke(_controller, "UpdateNormalMovement");

            // Velocity Y should now be JumpForce
            Vector2 velAfter = (Vector2)GetField(_controller, "_velocity");
            float jumpForce = (float)GetProp(_controller, "JumpForce");
            Assert.That(velAfter.y, Is.EqualTo(jumpForce).Within(0.1f));
            Assert.IsTrue((bool)GetProp(_controller, "HasDoubleJumped"), "HasDoubleJumped must be true after performing double jump in air");
        }

        [Test]
        public void DEF_ScalesWithLevel_AndReducesDamage()
        {
            // At level 1 with defPerLevel=1 and baseDEF=0: Defense = 0 + 1*1 = 1
            float def1 = (float)GetProp(_stats, "Defense");
            Assert.That(def1, Is.EqualTo(1f).Within(0.01f), "DEF at level 1 should be 1");

            // Level up to 5 and verify DEF scales
            SetField(_stats, "_currentLevel", 5);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float def5 = (float)GetProp(_stats, "Defense");
            Assert.That(def5, Is.EqualTo(5f).Within(0.01f), "DEF at level 5 should be 5");

            // Verify damage reduction: 20 raw damage → after DEF(5) → 15 actual damage taken
            float hpBefore = (float)GetProp(_stats, "CurrentHP");
            _stats.GetType().GetMethod("TakeDamage").Invoke(_stats, new object[] { 20f });
            float hpAfter = (float)GetProp(_stats, "CurrentHP");
            float damageTaken = hpBefore - hpAfter;

            // 20 * 1.0 (normal difficulty) - 5 DEF = 15
            Assert.That(damageTaken, Is.EqualTo(15f).Within(0.01f), "Damage should be reduced by DEF");
        }

        [Test]
        public void DEF_MinimumDamageIs1()
        {
            // Set level very high so DEF > incoming damage
            SetField(_stats, "_currentLevel", 100);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float def = (float)GetProp(_stats, "Defense");
            Assert.That(def, Is.EqualTo(100f).Within(0.01f));

            // 5 raw damage → after DEF(100) → should be clamped to 1
            float hpBefore = (float)GetProp(_stats, "CurrentHP");
            _stats.GetType().GetMethod("TakeDamage").Invoke(_stats, new object[] { 5f });
            float hpAfter = (float)GetProp(_stats, "CurrentHP");
            float damageTaken = hpBefore - hpAfter;

            Assert.That(damageTaken, Is.EqualTo(1f).Within(0.01f), "Minimum damage should always be 1");
        }

        [Test]
        public void Multipliers_CustomTemplateValues_DirectlyControlDerivedStats()
        {
            var soType = RuntimeType("TheLastKnight.Stats.CharacterStatsSO");
            var customTemplate = ScriptableObject.CreateInstance(soType);

            // Set custom multipliers
            SetField(customTemplate, "attackPerSTR", 5.0f);
            SetField(customTemplate, "defPerLevel", 3.0f);
            SetField(customTemplate, "baseDEF", 10.0f);
            SetField(customTemplate, "hpPerVIT", 25.0f);
            SetField(customTemplate, "staminaPerVIT", 5.0f);
            SetField(customTemplate, "speedPerAGI", 0.5f);
            SetField(customTemplate, "attackSpeedPerAGI", 0.01f);
            SetField(customTemplate, "doubleJumpAgiThreshold", 100);

            // Assign custom template to stats
            SetField(_stats, "_statsTemplate", customTemplate);
            SetField(_stats, "_strength", 10);
            SetField(_stats, "_vitality", 10);
            SetField(_stats, "_agility", 20);
            SetField(_stats, "_currentLevel", 4);

            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            // ATK: 10 * 5.0 = 50.0
            Assert.That((float)GetProp(_stats, "AttackPower"), Is.EqualTo(50.0f).Within(0.01f));
            // DEF: 10.0 + 4 * 3.0 = 22.0
            Assert.That((float)GetProp(_stats, "Defense"), Is.EqualTo(22.0f).Within(0.01f));
            // MaxHP: 10 * 25.0 = 250
            Assert.That((float)GetProp(_stats, "MaxHP"), Is.EqualTo(250.0f).Within(0.01f));
            // Double jump unlocks at 100 threshold (we have 20, so false)
            Assert.IsFalse((bool)GetProp(_stats, "CanDoubleJump"));
            // Set AGI to 100 -> unlocks
            SetField(_stats, "_agility", 100);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });
            Assert.IsTrue((bool)GetProp(_stats, "CanDoubleJump"));
        }

        [Test]
        public void HighAGI_AnimationSpeed_SynchronizesWithAttackDurationAndFrequency()
        {
            var anim = _player.GetComponent<Animator>();
            var startAtk = _controller.GetType().GetMethod("StartAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            var endAtk = _controller.GetType().GetMethod("EndAttack", BindingFlags.Instance | BindingFlags.NonPublic);
            var lateUpdate = _controller.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

            // 1. Base AGI (10)
            SetField(_stats, "_agility", 10);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            startAtk.Invoke(_controller, null);
            float baseDuration = (float)GetField(_controller, "_attackTimer");
            float baseCooldown = (float)GetField(_controller, "_attackCooldownTimer");
            lateUpdate.Invoke(_controller, null);
            float baseAnimSpeed = anim.speed;
            endAtk.Invoke(_controller, null);
            lateUpdate.Invoke(_controller, null);
            float baseAnimSpeedAfter = anim.speed;

            Assert.That(baseDuration, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(baseCooldown, Is.EqualTo(0.60f).Within(0.001f));
            Assert.That(baseAnimSpeed, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(baseAnimSpeedAfter, Is.EqualTo(1.0f).Within(0.001f));

            // 2. High AGI (250) -> AttackSpeedMultiplier = 1.0 + 240 * 0.004 = 1.96x
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float highMultiplier = (float)GetProp(_stats, "AttackSpeedMultiplier");
            Assert.That(highMultiplier, Is.EqualTo(1.96f).Within(0.01f));

            startAtk.Invoke(_controller, null);
            float highDuration = (float)GetField(_controller, "_attackTimer");
            float highCooldown = (float)GetField(_controller, "_attackCooldownTimer");
            lateUpdate.Invoke(_controller, null);
            float highAnimSpeed = anim.speed;
            endAtk.Invoke(_controller, null);
            lateUpdate.Invoke(_controller, null);
            float highAnimSpeedAfter = anim.speed;

            // Duration is shortened by 1 / highMultiplier
            Assert.That(highDuration, Is.EqualTo(0.25f / highMultiplier).Within(0.005f));
            Assert.That(highCooldown, Is.EqualTo(0.60f / highMultiplier).Within(0.005f));
            // Animator speed is accelerated by highMultiplier during attack
            Assert.That(highAnimSpeed, Is.EqualTo(highMultiplier).Within(0.01f));
            // Animator speed resets to 1.0x after attack
            Assert.That(highAnimSpeedAfter, Is.EqualTo(1.0f).Within(0.01f));

            // The animation time reduction matches attack duration reduction precisely:
            // Duration ratio: 0.25 / (0.25 / 1.96) = 1.96 = Anim speed ratio
            Assert.That(baseDuration / highDuration, Is.EqualTo(highAnimSpeed / baseAnimSpeed).Within(0.02f));
        }

        [Test]
        public void MoveSpeed_And_SprintSpeed_BehaveCorrectlyWithUnifiedSpeedSetting()
        {
            var soType = RuntimeType("TheLastKnight.Stats.CharacterStatsSO");
            var customTemplate = ScriptableObject.CreateInstance(soType);

            // Configure single speed setting: speedPerAGI = 0.02
            SetField(customTemplate, "speedPerAGI", 0.02f);
            SetField(_stats, "_statsTemplate", customTemplate);

            // 1. At AGI = 10 (base): MoveSpeed = 8.0, SprintSpeed = 8.0 * (13 / 8) = 13.0
            SetField(_stats, "_agility", 10);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpd10 = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpd10 = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpd10, Is.EqualTo(8.0f).Within(0.01f));
            Assert.That(sprintSpd10, Is.EqualTo(13.0f).Within(0.01f));

            // 2. At AGI = 250: MoveSpeed = 8.0 + 240 * 0.02 = 12.8, SprintSpeed = 12.8 * (13 / 8) = 20.8
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpd250 = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpd250 = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpd250, Is.EqualTo(12.8f).Within(0.01f));
            Assert.That(sprintSpd250, Is.EqualTo(20.8f).Within(0.01f));
            Assert.That(sprintSpd250, Is.GreaterThan(moveSpd250), "Sprint speed must always be greater than walk speed");

            // 3. Lowering single speedPerAGI to 0.01 lowers BOTH walk and sprint proportionally
            SetField(customTemplate, "speedPerAGI", 0.01f);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpdLowered = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpdLowered = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpdLowered, Is.EqualTo(10.4f).Within(0.01f));
            Assert.That(sprintSpdLowered, Is.EqualTo(16.9f).Within(0.01f));
            Assert.That(moveSpdLowered, Is.LessThan(moveSpd250));
            Assert.That(sprintSpdLowered, Is.LessThan(sprintSpd250));
        }

        [Test]
        public void StaminaRegen_DelaysAfterSpendingStamina()
        {
            var spendMethod = _stats.GetType().GetMethod("TrySpendStamina");
            var regenMethod = _stats.GetType().GetMethod("RegenerateStamina");
            var setLastSpendTime = _stats.GetType().GetMethod("SetLastStaminaSpendTimeForTesting");

            // Spend stamina
            spendMethod.Invoke(_stats, new object[] { 40f });
            float stmAfterSpend = (float)GetProp(_stats, "CurrentStamina");
            Assert.That(stmAfterSpend, Is.EqualTo(60f).Within(0.01f));

            // While within delay (e.g. 0.2s elapsed), regen should not happen
            setLastSpendTime.Invoke(_stats, new object[] { Time.time - 0.2f });
            regenMethod.Invoke(_stats, new object[] { 0.5f });
            float stmDuringDelay = (float)GetProp(_stats, "CurrentStamina");
            Assert.That(stmDuringDelay, Is.EqualTo(60f).Within(0.01f), "Stamina must not regenerate during delay period");

            // After delay has passed (e.g. 2.0s elapsed > StaminaRegenDelay), regen should proceed
            setLastSpendTime.Invoke(_stats, new object[] { Time.time - 2.0f });
            regenMethod.Invoke(_stats, new object[] { 0.5f });
            float stmAfterDelay = (float)GetProp(_stats, "CurrentStamina");
            Assert.That(stmAfterDelay, Is.GreaterThan(60f), "Stamina must regenerate after delay period");
        }
    }
}



