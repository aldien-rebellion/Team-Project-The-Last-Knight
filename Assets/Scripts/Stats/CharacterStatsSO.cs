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

        [Header("--- DEX: Critical Chance (Asymptotic Limit) ---")]
        [Tooltip("Target DEX value where critical chance reaches targetCritAtCap (default: 250).")]
        public float dexLimitTarget = 250f;

        [Tooltip("Critical chance percentage achieved at dexLimitTarget (default: 99%).")]
        public float targetCritAtCap = 99f;

        [Tooltip("Fallback linear crit per DEX if asymptotic is disabled or fallback is queried.")]
        public float critChancePerDEX = 0.5f;

        [Header("--- AGI: Speed, Attack Speed & Double Jump ---")]
        [Tooltip("Base attack speed multiplier (default: 1.0x).")]
        public float baseAttackSpeed = 1.0f;

        [Tooltip("Attack speed multiplier gained per point of AGI above baseAGI.")]
        public float attackSpeedPerAGI = 0.004f;

        [Tooltip("ความเร็วเคลื่อนที่ที่เพิ่มขึ้นต่อ 1 AGI (คุมทั้งความเร็วเดินและวิ่งในค่าเดียว โดยความเร็ววิ่งจะสเกลตามความเร็วเดินโดยอัตโนมัติ)")]
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
        /// Calculates Critical Strike Chance based on DEX using an asymptotic limit formula:
        /// Lim(DEX -> inf) = 100%, and Crit(dexLimitTarget) = targetCritAtCap (default: 99% at 250 DEX).
        /// </summary>
        public float CalculateCritChance(int dex)
        {
            if (dex <= 0) return 0f;
            if (dexLimitTarget <= 0f) return 0f;
            float remainingRatio = Mathf.Clamp01(1f - (targetCritAtCap / 100f));
            float crit = 100f * (1f - Mathf.Pow(remainingRatio, (float)dex / dexLimitTarget));
            return Mathf.Clamp(crit, 0f, 100f);
        }

        [Header("Level-up EXP Formula")]
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