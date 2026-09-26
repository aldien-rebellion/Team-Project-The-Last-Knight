using System;
using UnityEngine;

namespace TheLastKnight.Combat
{
    /// <summary>
    /// Configuration data for a monster's special skill or unique attack.
    /// Can be configured per-monster in the Unity Inspector.
    /// </summary>
    [Serializable]
    public class EnemySkill
    {
        [Tooltip("Skill identifier or display name.")]
        public string skillName = "Special Attack";

        [Tooltip("Animator trigger or state name (e.g. Attack3, Skill1, Summon, Dash).")]
        public string animationName = "Attack3";

        [Tooltip("Optional ActionIndex if used in Animator Controller (-1 = ignore).")]
        public int actionIndex = -1;

        [Tooltip("Damage multiplier based on EnemyStats.AttackPower (1.0 = 100% ATK, 1.5 = 150% ATK, 2.0 = 200% ATK).")]
        [Range(0.5f, 5.0f)]
        public float damageMultiplier = 1.5f;

        [Tooltip("Cooldown in seconds before this skill can be executed again.")]
        public float cooldown = 6.0f;

        [Tooltip("Minimum distance from player required to cast this skill (meters).")]
        public float minRange = 0f;

        [Tooltip("Maximum distance from player required to cast this skill (meters).")]
        public float maxRange = 2.0f;

        [Tooltip("If true, this skill displays the Parry timing ring and can be parried by the player.")]
        public bool isParryable = true;

        [Tooltip("Delay before the first use after spawn, in seconds.")]
        public float initialDelay;
        [Tooltip("Hold this non-damaging pose for this many seconds (zero = normal attack).")]
        public float guardDuration;
        [Tooltip("Seconds after the animation starts when this skill begins dealing damage.")]
        [Min(0f)]
        public float damageStartDelay = 0.4f;

        [Tooltip("How long this skill can deal damage after its damage window begins.")]
        [Min(0f)]
        public float damageDuration = 0.4f;

        [Tooltip("Apply one hit at the damage start time instead of using the continuous melee hitbox window.")]
        public bool dealDamageAsSingleHit = false;

        [Tooltip("Optional second single-hit time in seconds after the animation starts. Use -1 to disable.")]
        public float secondDamageHitTime = -1f;

        [Tooltip("Require the player's collider to overlap the current sprite bounds before a single hit is applied.")]
        public bool requireSpriteBoundsOverlap = false;

        [Tooltip("Optional projectile prefab for ranged skills (e.g. Goblin_Bomb, FlyingEye_Projectile).")]
        public GameObject projectilePrefab;

        [Tooltip("Optional ground spell prefab for area skills (e.g. BringerOfDeath_Spell, Jinn_Magic).")]
        public GameObject groundSpellPrefab;

        [NonSerialized]
        public float nextReadyTime = 0f;

        /// <summary>
        /// Checks whether this skill is ready to be cast given distance and current time.
        /// </summary>
        public bool IsReady(float distance, float currentTime)
        {
            return currentTime >= nextReadyTime && distance >= minRange && distance <= maxRange;
        }
    }
}
