using UnityEngine;

namespace TheLastKnight.Stats
{
    [CreateAssetMenu(fileName = "NewCharacterStats", menuName = "The Last Knight/Character Stats Template")]
    public class CharacterStatsSO : ScriptableObject
    {
        [Header("--- STR: Physical Attack Power ---")]
        [Tooltip("Base physical attack power before STR scaling.")]
        public float baseAttack = 0f;

        [Tooltip("Attack power gained per point of STR (ATK = baseAttack + STR * attackPerSTR).")]
        public float attackPerSTR = 1.5f;

        [Header("--- DEF: Defense (Scales with Level) ---")]
        [Tooltip("Base defense value before level scaling.")]
        public float baseDEF = 0f;

        [Tooltip("Defense gained per player level (DEF = baseDEF + Level * defPerLevel, e.g. 1 * Level).")]
        public float defPerLevel = 1.0f;

        [Header("--- VIT: Health & Stamina ---")]
        [Tooltip("Max HP gained per point of VIT (MaxHP = VIT * hpPerVIT).")]
        public float hpPerVIT = 10f;

        [Tooltip("Base Max Stamina before VIT bonus.")]
        public float baseStamina = 100f;

        [Tooltip("Max Stamina gained per point of VIT above baseVIT.")]
        public float staminaPerVIT = 1f;

        [Header("--- DEX: Critical Chance (Linear: 200 DEX = 100%) ---")]
        [Tooltip("Target DEX value where critical chance reaches targetCritAtCap (default: 200).")]
        public float dexLimitTarget = 200f;

        [Tooltip("Critical chance percentage achieved at dexLimitTarget (default: 100%).")]
        public float targetCritAtCap = 100f;

        [Tooltip("Linear crit chance gained per point of DEX (default: 0.5% per DEX).")]
        public float critChancePerDEX = 0.5f;

        [Header("--- AGI: Speed, Attack Speed & Double Jump ---")]
        [Tooltip("Base attack speed multiplier (default: 1.0x).")]
        public float baseAttackSpeed = 1.0f;

        [Tooltip("Attack speed multiplier gained per point of AGI above baseAGI.")]
        public float attackSpeedPerAGI = 0.004f;

        [Tooltip("ความเร็วในการวิ่ง (Sprint Speed) ที่เพิ่มขึ้นต่อ 1 AGI (ความเร็วเดินปกติจะคงที่ ไม่เปลี่ยนแปลงตาม AGI)")]
        public float speedPerAGI = 0.02f;

        [Tooltip("Dash velocity gained per point of AGI.")]
        public float dashSpeedPerAGI = 0.15f;

        [Tooltip("AGI required to unlock double jump capability (default: 250).")]
        public int doubleJumpAgiThreshold = 250;

        [Header("--- Starting Base Attributes ---")]
        public int baseSTR = 10;
        public int baseVIT = 10;
        public int baseDEX = 10;
        public int baseAGI = 10;

        /// <summary>
        /// Calculates Critical Strike Chance based on DEX using a linear formula:
        /// 200 DEX = 100% Critical Chance (0.5% crit per DEX, capped at 100%).
        /// </summary>
        public float CalculateCritChance(int dex)
        {
            if (dex <= 0) return 0f;
            if (dexLimitTarget <= 0f) return 0f;
            float crit = (dex / dexLimitTarget) * targetCritAtCap;
            return Mathf.Clamp(crit, 0f, 100f);
        }

        [Header("Level-up Formula")]
        [Tooltip("Number of stat upgrade points granted per level-up (default: 10).")]
        public int statPointsPerLevel = 10;
        public int baseExpNeeded = 100;
        public float expGrowthMultiplier = 1.25f;

        /// <summary>
        /// Calculates EXP threshold needed to advance from the current level to the next.
        /// </summary>
        public int GetExpNeededForLevel(int level)
        {
            if (level <= 1) return baseExpNeeded;
            return Mathf.RoundToInt(baseExpNeeded * Mathf.Pow(expGrowthMultiplier, level - 1));
        }
    }
}