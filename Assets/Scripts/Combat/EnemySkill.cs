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
