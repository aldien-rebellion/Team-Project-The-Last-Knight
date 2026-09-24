using System;
using UnityEngine;

namespace TheLastKnight.Combat
{
    public enum StatusEffect
    {
        None,
        Stunned,
        Burning,
        Frozen
    }

    [DisallowMultipleComponent]
    public class EnemyStats : MonoBehaviour, IDamageable
    {
        [Header("Health & Defense")]
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _defense = 0f;
        [SerializeField] private float _attackPower = 10f;
        [SerializeField, Min(1)] private int _level = 1;
        public int Level => _level;
        [SerializeField] private int _goldReward = 8;
        [SerializeField] private int _expReward = 15;
        public void SetRewards(int gold, int experience) { _goldReward = gold; _expReward = experience; }

        [Header("Status Effect")]
        [SerializeField] private StatusEffect _currentStatus = StatusEffect.None;
        private float _statusTimer = 0f;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public float Defense => _defense;
        public float AttackPower => _attackPower;
        public bool IsDead { get; private set; } = false;
        public StatusEffect CurrentStatus => _currentStatus;

        public event Action<float, float> OnHealthChanged;
        public event Action<DamageData> OnDamaged;
        public event Action OnDeath;

        private void Awake()
        {
            CurrentHealth = _maxHealth;
        }

        private void Update()
        {
            if (IsDead) return;

            if (_currentStatus != StatusEffect.None)
            {
                _statusTimer -= Time.deltaTime;
                if (_statusTimer <= 0f)
                {
                    ClearStatus();
                }
            }
        }

        public void TakeDamage(DamageData damageData)
        {
            if (IsDead) return;

            float actualDamage = Mathf.Max(1f, damageData.amount - _defense);
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("enemy_hurt");
            CurrentHealth = Mathf.Max(0f, CurrentHealth - actualDamage);

            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
            OnDamaged?.Invoke(damageData);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(new DamageData(amount));
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void ApplyStatus(StatusEffect effect, float duration)
        {
            if (IsDead) return;
            _currentStatus = effect;
            _statusTimer = duration;
        }

        public void ClearStatus()
        {
            _currentStatus = StatusEffect.None;
            _statusTimer = 0f;
        }

        public void SetStats(float maxHp, float defense, float attackPower)
        {
            _maxHealth = maxHp;
            _defense = defense;
            _attackPower = attackPower;
            CurrentHealth = _maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("enemy_death");
            CurrentHealth = 0f;
            var player = FindAnyObjectByType<TheLastKnight.Stats.PlayerStats>();
            if (player != null)
            {
                player.AddGold(_goldReward);
                player.AddEXP(_expReward);
                FloatingCombatText.Show(transform.position, $"+{_goldReward} Gold / +{_expReward} EXP", Color.yellow);
            }
            OnDeath?.Invoke();
        }
    }
}
