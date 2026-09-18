namespace TheLastKnight.Combat
{
    public interface IDamageable
    {
        void TakeDamage(DamageData damageData);
        void TakeDamage(float amount);
    }
}
