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
            float initialSprintSpeed = (float)GetProp(_controller, "SprintSpeed");
            float initialAtkSpeed = (float)GetProp(_stats, "AttackSpeedMultiplier");

            SetField(_stats, "_agility", 50);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });

            float newMoveSpeed = (float)GetProp(_controller, "MoveSpeed");
            float newSprintSpeed = (float)GetProp(_controller, "SprintSpeed");
            float newAtkSpeed = (float)GetProp(_stats, "AttackSpeedMultiplier");
            float ctrlAtkSpeed = (float)GetProp(_controller, "AttackSpeedMultiplier");

            // Walk speed remains fixed (AGI does not affect walk speed)
            Assert.That(newMoveSpeed, Is.EqualTo(initialMoveSpeed));
            // Sprint speed and attack speed increase with AGI
            Assert.That(newSprintSpeed, Is.GreaterThan(initialSprintSpeed));
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
        public void DEX_LinearCritChance_Reaches100PercentAt200_AndCannotExceed200()
        {
            var soType = RuntimeType("TheLastKnight.Stats.CharacterStatsSO");
            var template = AssetDatabase.LoadAssetAtPath("Assets/Settings/PlayerStatsTemplate.asset", soType);
            Assert.IsNotNull(template, "PlayerStatsTemplate must exist");

            var calcMethod = soType.GetMethod("CalculateCritChance");

            // 0 DEX -> 0%
            float crit0 = (float)calcMethod.Invoke(template, new object[] { 0 });
            Assert.That(crit0, Is.EqualTo(0f));

            // Base DEX (10) -> 5%
            float crit10 = (float)calcMethod.Invoke(template, new object[] { 10 });
            Assert.That(crit10, Is.EqualTo(5.0f).Within(0.01f));

            // Mid DEX (50, 100) -> 25%, 50%
            float crit50 = (float)calcMethod.Invoke(template, new object[] { 50 });
            float crit100 = (float)calcMethod.Invoke(template, new object[] { 100 });
            Assert.That(crit50, Is.EqualTo(25.0f).Within(0.01f));
            Assert.That(crit100, Is.EqualTo(50.0f).Within(0.01f));

            // 200 DEX -> 100%
            float crit200 = (float)calcMethod.Invoke(template, new object[] { 200 });
            Assert.That(crit200, Is.EqualTo(100.0f).Within(0.01f), "At 200 DEX, critical rate must be 100%");

            // Beyond 200 DEX -> Capped at 100%
            float crit250 = (float)calcMethod.Invoke(template, new object[] { 250 });
            Assert.That(crit250, Is.EqualTo(100.0f).Within(0.01f));

            // Verify Max DEX cap on PlayerStats: cannot upgrade beyond 200
            SetField(_stats, "_dexterity", 200);
            var addPoints = _stats.GetType().GetMethod("AddStatPoints");
            addPoints.Invoke(_stats, new object[] { 5 });
            var upgradeMethod = _stats.GetType().GetMethod("UpgradeStat");
            bool upgradeResult = (bool)upgradeMethod.Invoke(_stats, new object[] { "DEX" });
            Assert.That(upgradeResult, Is.False, "Upgrading DEX at 200 cap should fail");
            Assert.That((int)GetProp(_stats, "DEX"), Is.EqualTo(200), "DEX cannot exceed 200");
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
            _stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(_stats, new object[] { 20f });
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
            _stats.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(_stats, new object[] { 5f });
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

            // Configure speed setting: speedPerAGI = 0.02
            SetField(customTemplate, "speedPerAGI", 0.02f);
            SetField(_stats, "_statsTemplate", customTemplate);

            // 1. At AGI = 10 (base): MoveSpeed = 8.0, SprintSpeed = 13.0
            SetField(_stats, "_agility", 10);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpd10 = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpd10 = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpd10, Is.EqualTo(8.0f).Within(0.01f));
            Assert.That(sprintSpd10, Is.EqualTo(13.0f).Within(0.01f));

            // 2. At AGI = 250: MoveSpeed remains 8.0 (fixed), SprintSpeed = 13.0 + 240 * (0.02 * 1.625) = 20.8
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpd250 = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpd250 = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpd250, Is.EqualTo(8.0f).Within(0.01f), "Walk speed must remain fixed at 8.0 despite high AGI");
            Assert.That(sprintSpd250, Is.EqualTo(20.8f).Within(0.01f));
            Assert.That(sprintSpd250, Is.GreaterThan(moveSpd250), "Sprint speed must always be greater than walk speed");

            // 3. Lowering speedPerAGI to 0.01 lowers SprintSpeed proportionally while walk speed remains fixed
            SetField(customTemplate, "speedPerAGI", 0.01f);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { true });

            float moveSpdLowered = (float)GetProp(_controller, "MoveSpeed");
            float sprintSpdLowered = (float)GetProp(_controller, "SprintSpeed");
            Assert.That(moveSpdLowered, Is.EqualTo(8.0f).Within(0.01f), "Walk speed must remain 8.0");
            Assert.That(sprintSpdLowered, Is.EqualTo(16.9f).Within(0.01f));
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

        [Test]
        public void Jump_Consumes15Stamina_AndFailsWhenInsufficientStamina()
        {
            // Set stamina to 10 (< 15)
            SetField(_stats, "_currentStamina", 10f);

            // Grounded jump setup
            SetField(_controller, "_jumpBufferCounter", 0.15f);
            SetField(_controller, "_coyoteTimeCounter", 0.15f);
            SetField(_controller, "_velocity", Vector2.zero);

            Invoke(_controller, "UpdateNormalMovement");

            // Jump failed due to lack of stamina: velocity.y is not JumpForce and state is not Jumping
            Vector2 velAfter = (Vector2)GetField(_controller, "_velocity");
            float jumpForce = (float)GetProp(_controller, "JumpForce");
            Assert.That(velAfter.y, Is.LessThan(jumpForce), "Jump must fail when stamina < 15");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.Not.EqualTo("Jumping"));
            Assert.That((float)GetProp(_stats, "CurrentStamina"), Is.EqualTo(10f).Within(0.01f));

            // Set stamina to 20 (>= 15)
            SetField(_stats, "_currentStamina", 20f);
            SetField(_controller, "_jumpBufferCounter", 0.15f);
            SetField(_controller, "_coyoteTimeCounter", 0.15f);

            Invoke(_controller, "UpdateNormalMovement");

            velAfter = (Vector2)GetField(_controller, "_velocity");
            jumpForce = (float)GetProp(_controller, "JumpForce");
            Assert.That(velAfter.y, Is.EqualTo(jumpForce).Within(0.1f), "Jump should succeed when stamina >= 15");
            Assert.That((float)GetProp(_stats, "CurrentStamina"), Is.EqualTo(5f).Within(0.01f), "Jump must consume 15 stamina");
        }

        [Test]
        public void DoubleJump_Consumes15Stamina_AndFailsWhenInsufficientStamina()
        {
            // Unlock double jump
            SetField(_stats, "_agility", 250);
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });

            // Set stamina to 10 (< 15)
            SetField(_stats, "_currentStamina", 10f);

            // In air setup
            SetField(_controller, "_velocity", new Vector2(0, -10f));
            SetField(_controller, "_jumpBufferCounter", 0.15f);
            SetField(_controller, "_coyoteTimeCounter", -1f);
            SetField(_controller, "_hasDoubleJumped", false);

            Invoke(_controller, "UpdateNormalMovement");

            Assert.IsFalse((bool)GetProp(_controller, "HasDoubleJumped"), "Double jump must not occur when stamina < 15");
            Assert.That((float)GetProp(_stats, "CurrentStamina"), Is.EqualTo(10f).Within(0.01f));

            // Set stamina to 15 (>= 15)
            SetField(_stats, "_currentStamina", 15f);
            SetField(_controller, "_jumpBufferCounter", 0.15f);

            Invoke(_controller, "UpdateNormalMovement");

            Assert.IsTrue((bool)GetProp(_controller, "HasDoubleJumped"), "Double jump must succeed when stamina >= 15");
            Assert.That((float)GetProp(_stats, "CurrentStamina"), Is.EqualTo(0f).Within(0.01f), "Double jump must consume 15 stamina");
        }

        [Test]
        public void StaminaRegen_RatesMatchRequirements_Normal10Percent_Aura20Percent()
        {
            var regenMethod = _stats.GetType().GetMethod("RegenerateStamina");
            var setLastSpendTime = _stats.GetType().GetMethod("SetLastStaminaSpendTimeForTesting");
            var setRegenAura = _stats.GetType().GetMethod("SetRegenAura");

            // Max stamina is 100
            float maxStamina = (float)GetProp(_stats, "MaxStamina");
            Assert.That(maxStamina, Is.EqualTo(100f).Within(0.01f));

            // Set stamina to 0
            SetField(_stats, "_currentStamina", 0f);
            setLastSpendTime.Invoke(_stats, new object[] { Time.time - 2.0f });

            // 1. Normal regen (no aura): 10% per second -> 10 stamina in 1 sec
            setRegenAura.Invoke(_stats, new object[] { _stats, false });
            regenMethod.Invoke(_stats, new object[] { 1.0f });
            float stmNormal = (float)GetProp(_stats, "CurrentStamina");
            Assert.That(stmNormal, Is.EqualTo(10f).Within(0.01f), "Normal stamina regen should be 10% of MaxStamina per second");

            // 2. Regen Aura: 20% per second -> 20 stamina in 1 sec
            setRegenAura.Invoke(_stats, new object[] { _stats, true });
            regenMethod.Invoke(_stats, new object[] { 1.0f });
            float stmAura = (float)GetProp(_stats, "CurrentStamina");
            Assert.That(stmAura, Is.EqualTo(30f).Within(0.01f), "Regen Aura stamina regen should be 20% of MaxStamina per second");
        }

        [Test]
        public void HPRegen_MedusaAura_CappedAt70PercentMaxHP_AndRequiresAura()
        {
            var regenHPMethod = _stats.GetType().GetMethod("RegenerateHP");
            var setLastDamageTime = _stats.GetType().GetMethod("SetLastDamageTimeForTesting");
            var setRegenAura = _stats.GetType().GetMethod("SetRegenAura");

            float maxHP = (float)GetProp(_stats, "MaxHP");
            Assert.That(maxHP, Is.EqualTo(100f).Within(0.01f));

            // Set HP to 50%
            SetField(_stats, "_currentHP", 50f);
            setLastDamageTime.Invoke(_stats, new object[] { Time.time - 10.0f });

            // 1. Without Regen Aura: HP must not regenerate
            setRegenAura.Invoke(_stats, new object[] { _stats, false });
            regenHPMethod.Invoke(_stats, new object[] { 1.0f });
            Assert.That((float)GetProp(_stats, "CurrentHP"), Is.EqualTo(50f).Within(0.01f), "HP must not regenerate outside Medusa Aura");

            // 2. With Regen Aura: HP regenerates
            setRegenAura.Invoke(_stats, new object[] { _stats, true });
            regenHPMethod.Invoke(_stats, new object[] { 1.0f });
            Assert.That((float)GetProp(_stats, "CurrentHP"), Is.GreaterThan(50f), "HP should regenerate in Medusa Aura");

            // 3. Clamping: HP cannot regenerate past 70% MaxHP (70 HP)
            regenHPMethod.Invoke(_stats, new object[] { 1000f });
            Assert.That((float)GetProp(_stats, "CurrentHP"), Is.EqualTo(70f).Within(0.01f), "HP regen in Medusa Aura must cap at 70% of MaxHP");
        }

        [Test]
        public void Skill2Buff_IncreasesAttackPowerBy22Percent_For15Seconds()
        {
            float baseAtk = (float)GetProp(_stats, "AttackPower");
            Assert.That(baseAtk, Is.GreaterThan(0f));

            // Apply Skill 2 buff
            _stats.GetType().GetMethod("ApplySkill2Buff").Invoke(_stats, null);
            float buffedAtk = (float)GetProp(_stats, "AttackPower");
            float expectedBuffedAtk = baseAtk * 1.22f;
            Assert.That(buffedAtk, Is.EqualTo(expectedBuffedAtk).Within(0.01f), "Skill 2 must buff ATK by 22%");

            // Check duration (15 seconds)
            var fiBuffExpires = _stats.GetType().GetField("_skill2BuffExpiresAt", BindingFlags.Instance | BindingFlags.NonPublic);
            float expiresAt = (float)fiBuffExpires.GetValue(_stats);
            Assert.That(expiresAt - Time.time, Is.EqualTo(15f).Within(0.1f), "Skill 2 buff duration must be 15 seconds");

            // Fast forward past 15 seconds -> buff should expire
            _stats.GetType().GetMethod("SetSkill2BuffExpiresAtForTesting").Invoke(_stats, new object[] { Time.time - 1f });
            float expiredAtk = (float)GetProp(_stats, "AttackPower");
            Assert.That(expiredAtk, Is.EqualTo(baseAtk).Within(0.01f), "ATK should return to base after buff expires");
        }

        [Test]
        public void Skill2_StartBuff_AppliesBuff_AndSets30SecondCooldown()
        {
            float baseAtk = (float)GetProp(_stats, "AttackPower");

            // Cast Buff skill
            Invoke(_controller, "StartBuff");

            // Verify state
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Buffing"));

            // Verify 30s cooldown
            float cdTimer = (float)GetField(_controller, "_buffCooldownTimer");
            Assert.That(cdTimer, Is.EqualTo(30f).Within(0.1f), "StartBuff must set a 30-second cooldown");

            // Buff channel completes
            Invoke(_controller, "EndBuff");

            // Verify buff applied
            float buffedAtk = (float)GetProp(_stats, "AttackPower");
            Assert.That(buffedAtk, Is.EqualTo(baseAtk * 1.22f).Within(0.01f), "Completing Buff must apply +22% ATK buff");
        }

        [Test]
        public void TemporaryBuff_FormattedInParentheses_ShowsPermanentAndBonus()
        {
            var uiType = RuntimeType("TheLastKnight.UI.CharacterStatusUI");
            var formatMethod = uiType.GetMethod("FormatStatWithBonus", BindingFlags.Public | BindingFlags.Static);

            // 1. Base 15, Total 18.3 (with 22% buff) -> 15(+3.3)
            string result1 = (string)formatMethod.Invoke(null, new object[] { 15f, 18.3f });
            Assert.That(result1, Is.EqualTo("15(+3.3)"), "Buffed stat must display 15(+3.3) where 15 is permanent and 3.3 is bonus");

            // 2. Base 15, Total 15 (no buff) -> 15
            string result2 = (string)formatMethod.Invoke(null, new object[] { 15f, 15f });
            Assert.That(result2, Is.EqualTo("15"), "Unbuffed stat must display permanent value without parentheses");

            // 3. Base 16.5, Total 20.13 (STR 11 + 22% buff) -> 16.5(+3.6)
            string result3 = (string)formatMethod.Invoke(null, new object[] { 16.5f, 20.13f });
            Assert.That(result3, Is.EqualTo("16.5(+3.6)"));

            // 4. Verify BaseAttackPower on PlayerStats
            float baseAtk = (float)GetProp(_stats, "BaseAttackPower");
            float currentAtk = (float)GetProp(_stats, "AttackPower");
            Assert.That(baseAtk, Is.EqualTo(15f).Within(0.01f));
            Assert.That(currentAtk, Is.EqualTo(15f).Within(0.01f));

            // Apply Skill 2 buff
            _stats.GetType().GetMethod("ApplySkill2Buff").Invoke(_stats, null);
            float buffedAtk = (float)GetProp(_stats, "AttackPower");
            float permanentAtk = (float)GetProp(_stats, "BaseAttackPower");
            Assert.That(permanentAtk, Is.EqualTo(15f).Within(0.01f));
            Assert.That(buffedAtk, Is.EqualTo(18.3f).Within(0.01f));

            string formattedFromStats = (string)formatMethod.Invoke(null, new object[] { permanentAtk, buffedAtk });
            Assert.That(formattedFromStats, Is.EqualTo("15(+3.3)"));
        }

        [Test]
        public void LevelUp_Grants10StatPointsPerLevel()
        {
            int initialPoints = (int)GetProp(_stats, "StatPoints");
            int initialLevel = (int)GetProp(_stats, "Level");
            int expNeeded = (int)GetProp(_stats, "EXPNeeded");

            // Add enough EXP to trigger level up
            var addExpMethod = _stats.GetType().GetMethod("AddEXP");
            addExpMethod.Invoke(_stats, new object[] { expNeeded });

            int newLevel = (int)GetProp(_stats, "Level");
            int newPoints = (int)GetProp(_stats, "StatPoints");

            Assert.That(newLevel, Is.EqualTo(initialLevel + 1), "Level must increase by 1");
            Assert.That(newPoints, Is.EqualTo(initialPoints + 10), "Leveling up must grant exactly 10 stat points");
        }

        [Test]
        public void AnimationCancel_CanCancelSkills_IntoDashJumpAndAttack()
        {
            // 1. Start Buff (Skill 2)
            Invoke(_controller, "StartBuff");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Buffing"));
            Assert.IsTrue((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Buffing must be cancellable");

            // Cancel Buff into Dash
            SetField(_controller, "_dashCooldownTimer", 0f);
            SetField(_stats, "_currentStamina", 50f);
            Invoke(_controller, "CancelCurrentAction");
            Invoke(_controller, "StartDash");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Dashing"));

            // 2. Start Skill (Skill 1)
            SetField(_stats, "_currentStamina", 50f);
            Invoke(_controller, "StartSkill");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("UsingSkill"));
            Assert.IsTrue((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Skill must be cancellable");

            // Cancel Skill into Attack
            Invoke(_controller, "CancelCurrentAction");
            Invoke(_controller, "StartAttack");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Attacking"));
        }

        [Test]
        public void AnimationCancel_AttackAnimation_CannotBeCancelled()
        {
            // Start Attack
            SetField(_stats, "_currentStamina", 50f);
            Invoke(_controller, "StartAttack");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Attacking"));

            // Verify CanCancelCurrentAnimation is FALSE for Attack
            Assert.IsFalse((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Attack animation MUST NOT be cancellable");

            var isCancellableMethod = _controller.GetType().GetMethod("IsCancellableState");
            var playerStateType = RuntimeType("TheLastKnight.Player.PlayerState");
            object attackingState = Enum.Parse(playerStateType, "Attacking");
            bool isAttackingCancellable = (bool)isCancellableMethod.Invoke(_controller, new object[] { attackingState });
            Assert.IsFalse(isAttackingCancellable, "Attacking state must not be cancellable");

            // Calling CancelCurrentAction while attacking does nothing to the attacking state
            Invoke(_controller, "CancelCurrentAction");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Attacking"), "Attack must remain active and cannot be cancelled");
        }

        [Test]
        public void AnimationCancel_HurtAnimation_CanBeCancelled_IntoDashAndAttack()
        {
            // Enter Hurt state
            Invoke(_controller, "OnTakeDamage");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Hurt"));
            Assert.IsTrue((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Hurt state must be cancellable");

            var isCancellableMethod = _controller.GetType().GetMethod("IsCancellableState");
            var playerStateType = RuntimeType("TheLastKnight.Player.PlayerState");
            object hurtState = Enum.Parse(playerStateType, "Hurt");
            bool isHurtCancellable = (bool)isCancellableMethod.Invoke(_controller, new object[] { hurtState });
            Assert.IsTrue(isHurtCancellable, "Hurt state must be cancellable via IsCancellableState");

            // Cancel Hurt into Dash
            SetField(_controller, "_dashCooldownTimer", 0f);
            SetField(_stats, "_currentStamina", 50f);
            Invoke(_controller, "CancelCurrentAction");
            Invoke(_controller, "StartDash");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Dashing"));

            // End dash before re-entering Hurt
            Invoke(_controller, "EndDash");

            // Re-enter Hurt
            Invoke(_controller, "OnTakeDamage");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Hurt"));

            // Cancel Hurt into Attack
            SetField(_stats, "_currentStamina", 50f);
            Invoke(_controller, "CancelCurrentAction");
            Invoke(_controller, "StartAttack");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Attacking"));
        }

        [Test]
        public void AnimationCancel_BuffCancelled_DoesNotApplyAttackBonus()
        {
            float baseAtk = (float)GetProp(_stats, "BaseAttackPower");
            Assert.IsFalse((bool)GetProp(_stats, "HasSkill2Buff"));

            // Start Buff
            Invoke(_controller, "StartBuff");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Buffing"));
            // While buffing before completion, buff is not yet applied
            Assert.IsFalse((bool)GetProp(_stats, "HasSkill2Buff"), "Buff should not apply immediately upon cast");

            // Cancel Buff early (e.g. dash or attack)
            Invoke(_controller, "CancelCurrentAction");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.Not.EqualTo("Buffing"));
            Assert.IsFalse((bool)GetProp(_stats, "HasSkill2Buff"), "Buff must NOT be applied when cancelled early");
            Assert.That((float)GetProp(_stats, "AttackPower"), Is.EqualTo(baseAtk).Within(0.001f));

            // Now test natural completion: StartBuff and call EndBuff
            Invoke(_controller, "StartBuff");
            Invoke(_controller, "EndBuff");
            Assert.IsTrue((bool)GetProp(_stats, "HasSkill2Buff"), "Buff must be applied upon successful completion");
            Assert.That((float)GetProp(_stats, "AttackPower"), Is.EqualTo(baseAtk * 1.22f).Within(0.01f));
        }

        [Test]
        public void AnimationCancel_ExcaliburCancelled_ClearsTargetsAndStopsAction()
        {
            SetField(_stats, "_currentStamina", 100f);
            Invoke(_controller, "StartExcalibur");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Excalibur"));
            Assert.IsTrue((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Excalibur must be cancellable");

            // Cancel Excalibur early
            Invoke(_controller, "CancelCurrentAction");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.Not.EqualTo("Excalibur"));
            float excaliburTimer = (float)GetField(_controller, "_excaliburTimer");
            Assert.That(excaliburTimer, Is.EqualTo(0f), "Excalibur timer must be reset to 0 upon cancel");
        }

        [Test]
        public void AnimationCancel_DrinkCancelled_DoesNotConsumeItem()
        {
            // Set state to Drinking
            var playerStateType = RuntimeType("TheLastKnight.Player.PlayerState");
            object drinkingState = Enum.Parse(playerStateType, "Drinking");
            _controller.GetType().GetProperty("CurrentState")?.SetValue(_controller, drinkingState);
            SetField(_controller, "_drinkingSlot", 0);
            SetField(_controller, "_drinkTimer", 1.0f);

            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.EqualTo("Drinking"));
            Assert.IsTrue((bool)GetProp(_controller, "CanCancelCurrentAnimation"), "Drinking must be cancellable");

            // Cancel Drink
            Invoke(_controller, "CancelCurrentAction");
            Assert.That(GetProp(_controller, "CurrentState")?.ToString(), Is.Not.EqualTo("Drinking"));
            Assert.That((int)GetField(_controller, "_drinkingSlot"), Is.EqualTo(-1), "Drinking slot must be reset without consuming");
            Assert.That((float)GetField(_controller, "_drinkTimer"), Is.EqualTo(0f), "Drinking timer must be reset");
        }

        [Test]
        public void InsufficientStamina_FiresEvent_WhenActionCannotBePerformed()
        {
            // Set stamina to 0
            SetField(_stats, "_currentStamina", 0f);
            var resetCooldownMethod = _stats.GetType().GetMethod("ResetInsufficientStaminaCooldownForTesting");
            resetCooldownMethod?.Invoke(_stats, null);

            bool instanceEventFired = false;
            bool globalEventFired = false;

            var statsType = _stats.GetType();
            var instanceEvent = statsType.GetEvent("OnInsufficientStamina");
            var globalEvent = statsType.GetEvent("OnInsufficientStaminaGlobal");

            Action onInstance = () => instanceEventFired = true;
            Action onGlobal = () => globalEventFired = true;

            instanceEvent.AddEventHandler(_stats, onInstance);
            globalEvent.AddEventHandler(null, onGlobal);

            try
            {
                // Attempt to spend 15 stamina when at 0
                var trySpendMethod = statsType.GetMethod("TrySpendStamina", new[] { typeof(float) });
                bool success = (bool)trySpendMethod.Invoke(_stats, new object[] { 15f });

                Assert.IsFalse(success, "TrySpendStamina must fail when stamina is 0");
                Assert.IsTrue(instanceEventFired, "OnInsufficientStamina must be fired when an action fails due to stamina");
                Assert.IsTrue(globalEventFired, "OnInsufficientStaminaGlobal must be fired when an action fails due to stamina");
            }
            finally
            {
                instanceEvent.RemoveEventHandler(_stats, onInstance);
                globalEvent.RemoveEventHandler(null, onGlobal);
            }
        }

        [Test]
        public void InsufficientStamina_HUD_AppliesRedBorderAndShakeOnWarning()
        {
            var hudType = RuntimeType("TheLastKnight.UI.HUDController");
            var hudGo = new GameObject("TestHUD");
            var hud = hudGo.AddComponent(hudType);
            var staminaBar = new UnityEngine.UIElements.VisualElement();
            staminaBar.name = "StaminaBar";

            var staminaBarField = hudType.GetField("_staminaBar", BindingFlags.NonPublic | BindingFlags.Instance);
            staminaBarField.SetValue(hud, staminaBar);

            // Trigger warning
            hudType.GetMethod("TriggerStaminaWarning").Invoke(hud, null);

            Assert.IsTrue(staminaBar.ClassListContains("stamina-bar-warning"), "StaminaBar must have stamina-bar-warning class when warning triggered");
            Assert.That(staminaBar.style.borderTopWidth.value, Is.EqualTo(2f), "StaminaBar border width must increase to 2px");
            Assert.That(staminaBar.style.borderTopColor.value.r, Is.GreaterThan(0.8f), "StaminaBar border color must be red");

            // Reset effect
            hudType.GetMethod("ResetStaminaBarEffect").Invoke(hud, null);

            Assert.IsFalse(staminaBar.ClassListContains("stamina-bar-warning"), "StaminaBar must remove stamina-bar-warning class upon reset");
            Assert.That(staminaBar.transform.position, Is.EqualTo(Vector3.zero), "StaminaBar position must return to 0");

            GameObject.DestroyImmediate(hudGo);
        }

        [Test]
        public void Combat_DexCritDamage_Is150Percent()
        {
            SetField(_stats, "_dexterity", 200); // 100% crit chance
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });
            float atk = (float)GetProp(_stats, "AttackPower");

            var enemy = new GameObject("TestEnemy", typeof(BoxCollider2D));
            enemy.transform.position = _player.transform.position + Vector3.right * 0.5f;
            var target = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
            Invoke(target, "Awake");
            Physics2D.SyncTransforms();

            try
            {
                float beforeHp = (float)GetProp(target, "CurrentHealth");
                Invoke((Component)_controller, "StartAttack");
                Invoke((Component)_controller, "ApplyAttackHits");
                float damageDealt = beforeHp - (float)GetProp(target, "CurrentHealth");

                Assert.That(damageDealt, Is.EqualTo(atk * 1.5f).Within(0.01f), "DEX critical hit must deal 150% damage");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void Combat_ParryCritDamage_Is200Percent()
        {
            SetField(_stats, "_dexterity", 0); // 0% dex crit chance
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });
            float atk = (float)GetProp(_stats, "AttackPower");

            var enemy = new GameObject("TestEnemyParry", typeof(BoxCollider2D));
            enemy.transform.position = _player.transform.position + Vector3.right * 0.5f;
            var target = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
            var parry = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.ParryReceiver"));
            Invoke(target, "Awake");
            SetField(parry, "_isStaggered", true);
            Physics2D.SyncTransforms();

            try
            {
                float beforeHp = (float)GetProp(target, "CurrentHealth");
                Invoke((Component)_controller, "StartAttack");
                Invoke((Component)_controller, "ApplyAttackHits");
                float damageDealt = beforeHp - (float)GetProp(target, "CurrentHealth");

                Assert.That(damageDealt, Is.EqualTo(atk * 2.0f).Within(0.01f), "Parry critical hit must deal 200% damage");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void Combat_SkillCanCriticalHit_With150PercentDamage()
        {
            SetField(_stats, "_dexterity", 200); // 100% crit chance
            _stats.GetType().GetMethod("RecalculateStats", new[] { typeof(bool) })?.Invoke(_stats, new object[] { false });
            float atk = (float)GetProp(_stats, "AttackPower");
            float skillMult = (float)GetField(_controller, "_skillDamageMultiplier");

            var enemy = new GameObject("TestEnemySkill", typeof(BoxCollider2D));
            enemy.transform.position = _player.transform.position + Vector3.right * 0.5f;
            var target = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
            Invoke(target, "Awake");
            Physics2D.SyncTransforms();

            try
            {
                float beforeHp = (float)GetProp(target, "CurrentHealth");
                Invoke((Component)_controller, "StartSkill");
                Invoke((Component)_controller, "ApplySkillHits");
                float damageDealt = beforeHp - (float)GetProp(target, "CurrentHealth");

                Assert.That(damageDealt, Is.EqualTo(atk * skillMult * 1.5f).Within(0.01f), "Skill critical hit must deal 150% of base skill damage");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }
    }
}



