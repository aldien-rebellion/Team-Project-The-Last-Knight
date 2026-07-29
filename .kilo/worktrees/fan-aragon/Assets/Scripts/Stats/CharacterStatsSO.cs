using UnityEngine;

namespace TheLastKnight.Stats
{
    [CreateAssetMenu(fileName = "NewCharacterStats", menuName = "The Last Knight/Character Stats Template")]
    public class CharacterStatsSO : ScriptableObject
    {
        [Header("Starting Base Attributes")]
        public int baseSTR = 10;
        public int baseVIT = 10;
        public int baseDEX = 10;
        public int baseAGI = 10;

        [Header("Progression Growth Coefficients")]
        [Tooltip("How much Max HP increases per level of VIT.")]
        public float hpPerVIT = 10f;
        
        [Tooltip("How much attack power increases per level of STR.")]
        public float attackPerSTR = 1.5f;

        [Tooltip("How much critical hit percentage increases per level of DEX.")]
        public float critChancePerDEX = 0.5f;

        [Tooltip("How much movement speed increases per level of AGI.")]
        public float speedPerAGI = 0.12f;

        [Tooltip("How much dash velocity increases per level of AGI.")]
        public float dashSpeedPerAGI = 0.15f;

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