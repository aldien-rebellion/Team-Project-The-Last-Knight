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

        private static void Invoke(Component target, string method, params object[] args)
        {
            var types = args != null && args.Length > 0 ? args.Select(a => a.GetType()).ToArray() : Type.EmptyTypes;
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null)
                 ?? target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m.Invoke(target, args != null && args.Length == 0 ? null : args);
        }

        private static object GetProp(object target, string prop) => target.GetType().GetProperty(prop).GetValue(target);
        private static void SetProp(object target, string prop, object val) => target.GetType().GetProperty(prop).SetValue(target, val);
        private static void SetField(object target, string field, object val) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(target, val);
        private static float PlayerDamageMultiplier => (float)RuntimeType("TheLastKnight.Core.GameDifficultyManager")
            .GetProperty("PlayerDamage", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        [TestCase(2, "CancelCurrentAction")]
        [TestCase(2, "OnTakeDamage")]
        [TestCase(2, "ApplyStun")]
        [TestCase(3, "CancelCurrentAction")]
        [TestCase(3, "CancelExcalibur")]
        [TestCase(3, "ApplyStun")]
        public void CancelledSkill_HalvesRemainingCooldown_OnlyOnce(int skill, string cancellation)
        {
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            try
            {
                player.transform.position = new Vector3(28000, 28000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, skill == 2 ? "StartBuff" : "StartExcalibur");
                string timer = skill == 2 ? "BuffCooldownTimer" : "ExcaliburCooldownTimer";
                string maximum = skill == 2 ? "BuffCooldown" : "ExcaliburCooldown";
                float originalMaximum = (float)GetProp(controller, maximum);
                SetProp(controller, timer, 12f); // Some cooldown has already elapsed.
                if (cancellation == "ApplyStun") Invoke(controller, cancellation, 1f);
                else Invoke(controller, cancellation);
                Assert.That((float)GetProp(controller, timer), Is.EqualTo(6f));
                Assert.That((float)GetProp(controller, maximum), Is.EqualTo(originalMaximum));
                Assert.That(GetProp(controller, "CurrentState").ToString(), Is.Not.EqualTo(skill == 2 ? "Buffing" : "Excalibur"));
                if (cancellation == "ApplyStun") Invoke(controller, cancellation, 1f);
                else Invoke(controller, cancellation);
                Assert.That((float)GetProp(controller, timer), Is.EqualTo(6f), "Repeating cancellation must not refund cooldown again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        public void CompletedSkill_KeepsRemainingCooldown(int skill)
        {
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            try
            {
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, skill == 2 ? "StartBuff" : "StartExcalibur");
                string timer = skill == 2 ? "BuffCooldownTimer" : "ExcaliburCooldownTimer";
                SetProp(controller, timer, 12f);
                Invoke(controller, skill == 2 ? "EndBuff" : "EndExcalibur");
                Invoke(controller, "CancelCurrentAction");
                Assert.That((float)GetProp(controller, timer), Is.EqualTo(12f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Skill3_Timing_AlignsAnimationDamageAndVFX_IncludingHitStop()
        {
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            try
            {
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                var vfx = player.GetComponent(RuntimeType("TheLastKnight.Player.ExcaliburVFXController"));
                var serializedVfx = new SerializedObject(vfx);
                float anticipation = serializedVfx.FindProperty("anticipationDuration").floatValue;
                float charge = serializedVfx.FindProperty("chargeDuration").floatValue;
                float beam = serializedVfx.FindProperty("beamDuration").floatValue;
                float hitStop = serializedVfx.FindProperty("hitStopDuration").floatValue;
                float hitStopScale = serializedVfx.FindProperty("hitStopTimeScale").floatValue;
                Assert.That((float)GetProp(controller, "ExcaliburDuration"), Is.EqualTo(1.5f));
                Assert.That(anticipation + charge + hitStop + beam, Is.EqualTo(1.5f).Within(0.00001f));
                Assert.That((float)GetProp(controller, "ExcaliburDamageDelay"), Is.EqualTo(anticipation + charge + hitStop * hitStopScale).Within(0.00001f));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, "StartExcalibur");
                Invoke(controller, "LateUpdate");
                var animator = player.GetComponent<Animator>();
                var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == "Excalibur");
                float extraHitStop = hitStop * (1f - hitStopScale);
                Assert.That(clip.length / animator.speed + extraHitStop, Is.EqualTo(1.5f).Within(0.00001f));
                Assert.That(clip.events.First(e => e.functionName == "PlayExcaliburSlashDownSound").time / animator.speed,
                    Is.EqualTo(anticipation + charge).Within(0.00001f));
                Invoke(controller, "CancelExcalibur");
                Assert.That(animator.speed, Is.EqualTo(1f), "Movement animations must regain their normal speed on cancellation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

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
                var enemyRb = enemy.AddComponent<Rigidbody2D>();
                enemyRb.gravityScale = 0;
                enemy.AddComponent<BoxCollider2D>();
                Invoke(enemyStats, "Awake");
                Physics2D.SyncTransforms();

                float initialEnemyX = enemy.transform.position.x;
                SetProp(controller, "SkillDamageMultiplier", 2.0f);
                Invoke(controller, "StartSkill");
                Invoke(controller, "ApplySkillHits");

                float hpAfterSkill = (float)GetProp(enemyStats, "CurrentHealth");
                float maxHP = (float)GetProp(enemyStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 2.0f * PlayerDamageMultiplier;

                Assert.That(hpAfterSkill, Is.LessThan(maxHP), "Skill 1 must deal damage to enemy");
                Assert.That(maxHP - hpAfterSkill, Is.EqualTo(expectedDamage).Within(1f));
                Assert.That(enemy.transform.position.x, Is.GreaterThan(initialEnemyX), "Skill 1 must push monster outward away from player");
                Assert.That(enemyRb.linearVelocity.x, Is.GreaterThan(0f), "Skill 1 must apply knockback velocity to monster");

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
        public void Skill1_CarnageBurst_HitsAndPushesEnemiesOutwardAlongCircle()
        {
            GameObject player = null, behindEnemy = null, frontEnemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(25000, 25000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "<CriticalChance>k__BackingField", 0f);

                // Player faces right (positive X direction)
                Assert.IsTrue((bool)GetProp(controller, "IsFacingRight"), "Player defaults to facing right");

                // Behind enemy is positioned behind player within skill radius (to the left, negative X direction)
                behindEnemy = new GameObject("CarnageBurst Behind Enemy");
                behindEnemy.transform.position = player.transform.position + Vector3.left * 1.2f;
                var behindStats = behindEnemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(behindStats, "_maxHealth", 500f);
                SetField(behindStats, "_defense", 0f);
                var behindRb = behindEnemy.AddComponent<Rigidbody2D>();
                behindRb.gravityScale = 0;
                behindEnemy.AddComponent<BoxCollider2D>();
                Invoke(behindStats, "Awake");

                // Front enemy is positioned in front of player within skill radius (to the right, positive X direction)
                frontEnemy = new GameObject("CarnageBurst Front Enemy");
                frontEnemy.transform.position = player.transform.position + Vector3.right * 1.2f;
                var frontStats = frontEnemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(frontStats, "_maxHealth", 500f);
                SetField(frontStats, "_defense", 0f);
                var frontRb = frontEnemy.AddComponent<Rigidbody2D>();
                frontRb.gravityScale = 0;
                frontEnemy.AddComponent<BoxCollider2D>();
                Invoke(frontStats, "Awake");

                Physics2D.SyncTransforms();

                float initialBehindX = behindEnemy.transform.position.x;
                float initialFrontX = frontEnemy.transform.position.x;
                SetProp(controller, "SkillDamageMultiplier", 2.0f);
                Invoke(controller, "StartSkill");
                Invoke(controller, "ApplySkillHits");

                float behindHp = (float)GetProp(behindStats, "CurrentHealth");
                float frontHp = (float)GetProp(frontStats, "CurrentHealth");
                float maxHP = (float)GetProp(behindStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 2.0f * PlayerDamageMultiplier;

                // Enemy behind player inside circle takes damage and is pushed backward to the left
                Assert.That(behindHp, Is.LessThan(maxHP), "Skill 1 deals damage to enemy in circle behind player");
                Assert.That(maxHP - behindHp, Is.EqualTo(expectedDamage).Within(1f));
                Assert.That(behindEnemy.transform.position.x, Is.LessThan(initialBehindX), "Skill 1 pushes monster behind outward to the left");
                Assert.That(behindRb.linearVelocity.x, Is.LessThan(0f), "Skill 1 applies outward knockback velocity to monster behind");

                // Enemy in front of player inside circle takes damage and is pushed forward to the right
                Assert.That(frontHp, Is.LessThan(maxHP), "Skill 1 deals damage to enemy in front of player");
                Assert.That(maxHP - frontHp, Is.EqualTo(expectedDamage).Within(1f));
                Assert.That(frontEnemy.transform.position.x, Is.GreaterThan(initialFrontX), "Skill 1 pushes monster forward to the right");
                Assert.That(frontRb.linearVelocity.x, Is.GreaterThan(0f), "Skill 1 applies forward knockback velocity to monster in front");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (behindEnemy != null) UnityEngine.Object.DestroyImmediate(behindEnemy);
                if (frontEnemy != null) UnityEngine.Object.DestroyImmediate(frontEnemy);
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
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, "StartExcalibur");
                Invoke(controller, "ApplyExcaliburHits");

                float hpAfterExcalibur = (float)GetProp(enemyStats, "CurrentHealth");
                float maxHP = (float)GetProp(enemyStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 5.0f * PlayerDamageMultiplier;

                Assert.That(hpAfterExcalibur, Is.LessThan(maxHP), "Skill 3 Excalibur must deal damage to enemy in beam path");
                Assert.That(maxHP - hpAfterExcalibur, Is.EqualTo(expectedDamage).Within(1f));
                Assert.That(GetProp(enemyStats, "CurrentStatus").ToString(), Is.EqualTo("Stunned"), "Enemy hit by Excalibur must be stunned");
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
        public void Skill3_Excalibur_HitsPointBlankTouchingEnemy()
        {
            GameObject player = null, enemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(24000, 24000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "<CriticalChance>k__BackingField", 0f);

                // Enemy is touching/overlapping player right at point-blank range (0.2 units away)
                enemy = new GameObject("Excalibur Point-Blank Enemy");
                enemy.transform.position = player.transform.position + new Vector3(0.2f, 0f, 0f);
                var enemyStats = enemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(enemyStats, "_maxHealth", 500f);
                SetField(enemyStats, "_defense", 0f);
                enemy.AddComponent<BoxCollider2D>();
                Invoke(enemyStats, "Awake");
                Physics2D.SyncTransforms();

                SetProp(controller, "ExcaliburDamageMultiplier", 4.0f);
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, "StartExcalibur");
                Invoke(controller, "ApplyExcaliburHits");

                float hpAfterExcalibur = (float)GetProp(enemyStats, "CurrentHealth");
                float maxHP = (float)GetProp(enemyStats, "MaxHealth");
                float atk = (float)GetProp(stats, "AttackPower");
                float expectedDamage = atk * 4.0f * PlayerDamageMultiplier;

                Assert.That(hpAfterExcalibur, Is.LessThan(maxHP), "Excalibur must hit point-blank / touching enemy directly in front of player");
                Assert.That(maxHP - hpAfterExcalibur, Is.EqualTo(expectedDamage).Within(1f));
                Assert.That(GetProp(enemyStats, "CurrentStatus").ToString(), Is.EqualTo("Stunned"), "Point-blank enemy hit by Excalibur must be stunned");
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
        public void Skill3_Excalibur_DoesNotHitEnemyBehindPlayer()
        {
            GameObject player = null, behindEnemy = null, frontEnemy = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(27000, 27000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");
                SetField(stats, "<CriticalChance>k__BackingField", 0f);

                // Behind enemy
                behindEnemy = new GameObject("Excalibur Behind Enemy");
                behindEnemy.transform.position = player.transform.position + Vector3.left * 1.5f;
                var behindStats = behindEnemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(behindStats, "_maxHealth", 500f);
                SetField(behindStats, "_defense", 0f);
                behindEnemy.AddComponent<BoxCollider2D>();
                Invoke(behindStats, "Awake");

                // Front enemy
                frontEnemy = new GameObject("Excalibur Front Enemy");
                frontEnemy.transform.position = player.transform.position + Vector3.right * 1.5f;
                var frontStats = frontEnemy.AddComponent(RuntimeType("TheLastKnight.Combat.EnemyStats"));
                SetField(frontStats, "_maxHealth", 500f);
                SetField(frontStats, "_defense", 0f);
                frontEnemy.AddComponent<BoxCollider2D>();
                Invoke(frontStats, "Awake");

                Physics2D.SyncTransforms();

                SetProp(controller, "ExcaliburDamageMultiplier", 4.0f);
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, "StartExcalibur");
                Invoke(controller, "ApplyExcaliburHits");

                float behindHp = (float)GetProp(behindStats, "CurrentHealth");
                float frontHp = (float)GetProp(frontStats, "CurrentHealth");
                float maxHP = (float)GetProp(behindStats, "MaxHealth");

                // Enemy behind player MUST NOT take damage and MUST NOT be stunned
                Assert.That(behindHp, Is.EqualTo(maxHP), "Excalibur must NOT deal damage to enemy behind player");
                Assert.That(GetProp(behindStats, "CurrentStatus").ToString(), Is.Not.EqualTo("Stunned"), "Excalibur must NOT stun enemy behind player");

                // Enemy in front of player MUST take damage and be stunned
                Assert.That(frontHp, Is.LessThan(maxHP), "Excalibur must deal damage to enemy in front of player");
                Assert.That(GetProp(frontStats, "CurrentStatus").ToString(), Is.EqualTo("Stunned"), "Excalibur must stun enemy in front of player");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (behindEnemy != null) UnityEngine.Object.DestroyImmediate(behindEnemy);
                if (frontEnemy != null) UnityEngine.Object.DestroyImmediate(frontEnemy);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void Skill3_Excalibur_NoInvincibility_TakesDamageWithoutInterruption_CancelledByStunWithVFXCleared()
        {
            GameObject player = null;
            try
            {
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
                player.transform.position = new Vector3(26000, 26000, 0);
                var controller = player.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController"));
                var stats = player.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                var vfx = player.GetComponent(RuntimeType("TheLastKnight.Player.ExcaliburVFXController")) 
                    ?? player.AddComponent(RuntimeType("TheLastKnight.Player.ExcaliburVFXController"));
                Invoke(controller, "Awake");
                Invoke(stats, "Awake");

                float initialHP = (float)GetProp(stats, "CurrentHP");

                // Start Excalibur
                SetField(stats, "_currentLevel", 20);
                Invoke(controller, "StartExcalibur");

                Assert.That(GetProp(controller, "CurrentState").ToString(), Is.EqualTo("Excalibur"));
                Assert.IsFalse((bool)GetProp(controller, "IsInvincible"), "Arthur must NOT be invincible during Excalibur channel");

                // Taking normal incoming damage during Excalibur channel is NOT avoided
                Invoke(stats, "TakeDamage", 25f);
                Assert.That((float)GetProp(stats, "CurrentHP"), Is.LessThan(initialHP), "Arthur must take damage while channeling Excalibur");

                // Normal OnTakeDamage must NOT knock Arthur out of Excalibur
                Invoke(controller, "OnTakeDamage");
                Assert.That(GetProp(controller, "CurrentState").ToString(), Is.EqualTo("Excalibur"), "Arthur must not be interrupted out of Excalibur by normal damage");

                // Create dummy VFX instance under player
                var dummyBeam = new GameObject("ExcaliburBeam(Clone)");
                dummyBeam.transform.SetParent(player.transform);
                Assert.That(player.transform.Find("ExcaliburBeam(Clone)"), Is.Not.Null);

                // Stun attack MUST cancel Excalibur and clear all VFX immediately
                Invoke(controller, "ApplyStun", 1.5f);
                Assert.That(GetProp(controller, "CurrentState").ToString(), Is.EqualTo("Hurt"), "Arthur must be interrupted out of Excalibur into Hurt/Stunned");
                Assert.IsFalse((bool)GetProp(vfx, "IsExecuting"), "All Excalibur VFX must be stopped/cleared immediately upon cancellation");
                Assert.That(player.transform.Find("ExcaliburBeam(Clone)"), Is.Null, "Excalibur beam and VFX clones must be immediately destroyed upon cancellation");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
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

                // Verify default or configurable cooldowns & stun duration
                SetProp(controller, "SkillCooldown", 2.5f);
                SetProp(controller, "BuffCooldown", 4.0f);
                SetProp(controller, "ExcaliburCooldown", 8.0f);
                SetProp(controller, "DashCooldown", 0.8f);
                SetProp(controller, "DashIFrameDuration", 0.15f);
                SetProp(controller, "ExcaliburStunDuration", 3.0f);

                Assert.That((float)GetProp(controller, "SkillCooldown"), Is.EqualTo(2.5f));
                Assert.That((float)GetProp(controller, "BuffCooldown"), Is.EqualTo(4.0f));
                Assert.That((float)GetProp(controller, "ExcaliburCooldown"), Is.EqualTo(8.0f));
                Assert.That((float)GetProp(controller, "DashCooldown"), Is.EqualTo(0.8f));
                Assert.That((float)GetProp(controller, "DashIFrameDuration"), Is.EqualTo(0.15f).Within(0.001f));
                Assert.That((float)GetProp(controller, "ExcaliburStunDuration"), Is.EqualTo(3.0f));
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
            }
        }
    }
}
