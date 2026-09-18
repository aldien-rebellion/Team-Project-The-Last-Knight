using UnityEngine;

namespace TheLastKnight.Combat
{
    public enum DamageType
    {
        Physical,
        Fire,
        Poison,
        DarkMagic,
        Lightning
    }

    [System.Serializable]
    public struct DamageData
    {
        public float amount;
        public DamageType damageType;
        public GameObject attacker;
        public Vector2 knockbackForce;
        public Vector2 hitPoint;

        public DamageData(float amount, GameObject attacker = null, DamageType damageType = DamageType.Physical, Vector2 knockbackForce = default, Vector2 hitPoint = default)
        {
            this.amount = amount;
            this.attacker = attacker;
            this.damageType = damageType;
            this.knockbackForce = knockbackForce;
            this.hitPoint = hitPoint;
        }
    }
}
