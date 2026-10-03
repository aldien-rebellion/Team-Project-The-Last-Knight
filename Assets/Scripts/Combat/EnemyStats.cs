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
        [Header("Level & Scaling Configuration")]
        [SerializeField, Min(1)] private int _level = 1;
        public int Level => _level;
        [Tooltip("When enabled, changing Level in Inspector automatically scales MaxHealth, AttackPower, Defense, and EXP/Gold rewards based on project formula.")]
        [SerializeField] private bool _useLevelScaling = false;

        [Header("Health & Defense")]
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _defense = 0f;
        [SerializeField] private float _attackPower = 10f;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_useLevelScaling)
            {
                ApplyLevelScaling();
            }
        }
#endif

        [ContextMenu("Apply Level Scaling Formula")]
        public void ApplyLevelScaling()
        {
            _level = Mathf.Max(1, _level);
            _maxHealth = _level * 75f;
            _attackPower = _level * 10f;
            _defense = _level * 1f;

            int baseValue = _level * 100;
            int minReward = Mathf.RoundToInt(baseValue * 0.20f);
            int maxReward = Mathf.RoundToInt(baseValue * 0.30f);
            _expReward = UnityEngine.Random.Range(minReward, maxReward + 1);
            _goldReward = UnityEngine.Random.Range(minReward, maxReward + 1);

            CurrentHealth = _maxHealth;
        }

        public void SetLevel(int level, bool autoRecalculate = true)
        {
            _level = Mathf.Max(1, level);
            if (autoRecalculate && _useLevelScaling)
            {
                ApplyLevelScaling();
            }
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

            // Apply Level Difference damage reduction when Monster Lv > Player Lv + 5, 10, 15, 20
            var player = damageData.attacker != null 
                ? damageData.attacker.GetComponent<TheLastKnight.Stats.PlayerStats>() 
                : FindAnyObjectByType<TheLastKnight.Stats.PlayerStats>();

            if (player != null && _useLevelScaling)
            {
                int levelDiff = _level - player.Level;
                if (levelDiff >= 20) actualDamage *= 0.60f;      // -40%
                else if (levelDiff >= 15) actualDamage *= 0.70f; // -30%
                else if (levelDiff >= 10) actualDamage *= 0.80f; // -20%
                else if (levelDiff >= 5)  actualDamage *= 0.90f; // -10%

                actualDamage = Mathf.Max(1f, actualDamage);
            }

            var attackingPlayer = damageData.attacker != null
                ? damageData.attacker.GetComponent<TheLastKnight.Player.PlayerController>()
                : null;
            if (attackingPlayer != null)
            {
                var playerStats = damageData.attacker.GetComponent<TheLastKnight.Stats.PlayerStats>();
                float normalDamage = playerStats != null
                    ? playerStats.AttackPower * TheLastKnight.Core.GameDifficultyManager.PlayerDamage
                    : 0f;
                bool critical = damageData.amount > normalDamage + 0.01f;
                Vector3 hitPosition = damageData.hitPoint != Vector2.zero
                    ? (Vector3)damageData.hitPoint
                    : transform.position;
                FloatingCombatText.Show(hitPosition, Mathf.CeilToInt(actualDamage).ToString() + (critical ? "!" : ""),
                    critical ? Color.yellow : Color.white);
            }

            var dragonSfx = GetComponent<TheLastKnight.AI.DragonSfxController>();
            var jinnSfx = GetComponent<TheLastKnight.AI.JinnAudioController>();
            var moonstoneKeeperSfx = GetComponent<TheLastKnight.AI.MoonstoneKeeperAudioController>();
            var necromancerSfx = GetComponent<TheLastKnight.AI.NecromancerAudioController>();
            var reaperSfx = GetComponent<TheLastKnight.AI.ReaperAudioController>();
            bool hasSpecificHitSound = (dragonSfx != null && dragonSfx.isActiveAndEnabled)
                || (jinnSfx != null && jinnSfx.isActiveAndEnabled)
                || (attackingPlayer != null && moonstoneKeeperSfx != null && moonstoneKeeperSfx.isActiveAndEnabled)
                || (attackingPlayer != null && necromancerSfx != null && necromancerSfx.isActiveAndEnabled)
                || (attackingPlayer != null && reaperSfx != null && reaperSfx.isActiveAndEnabled)
                || (GetComponent<TheLastKnight.AI.MechaStoneGolemAudioController>() is { isActiveAndEnabled: true });
            hasSpecificHitSound = hasSpecificHitSound
                || (GetComponent<TheLastKnight.AI.SkeletonAudioController>() is { isActiveAndEnabled: true });
            hasSpecificHitSound = hasSpecificHitSound
                || (attackingPlayer != null
                    && GetComponent<TheLastKnight.AI.SkeletonKnightAudioController>() is { isActiveAndEnabled: true });
            hasSpecificHitSound = hasSpecificHitSound
                || (attackingPlayer != null
                    && GetComponent<TheLastKnight.AI.HoodedProtagonistAudioController>() is { isActiveAndEnabled: true });
            if (!hasSpecificHitSound)
                TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("enemy_hurt");
            CurrentHealth = Mathf.Max(0f, CurrentHealth - actualDamage);

            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
            OnDamaged?.Invoke(damageData);
            var enemyController = GetComponent<TheLastKnight.AI.EnemyController>();
            if (enemyController != null)
            {
                enemyController.NotifyDamaged(damageData);
            }

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

        public void Revive()
        {
            IsDead = false;
            CurrentHealth = _maxHealth;
            ClearStatus();
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void ApplyStatus(StatusEffect effect, float duration)
        {
            if (IsDead) return;
            bool enteringStun = effect == StatusEffect.Stunned && _currentStatus != StatusEffect.Stunned;
            _currentStatus = effect;
            _statusTimer = duration;

            if (effect == StatusEffect.Stunned)
            {
                if (enteringStun)
                    GetComponent<TheLastKnight.AI.MechaStoneGolemAudioController>()?.PlayStunned();

                GetComponent<TheLastKnight.AI.EnemyController>()?.CancelAttack();
                GetComponent<SlimeController>()?.CancelAttack();
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }
        }

        public void ClearStatus()
        {
            _currentStatus = StatusEffect.None;
            _statusTimer = 0f;
        }

        public void SetStats(float maxHp, float defense, float attackPower)
        {
            _useLevelScaling = false;
            _maxHealth = maxHp;
            _defense = defense;
            _attackPower = attackPower;
            CurrentHealth = _maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        public void RollRewards()
        {
            int baseValue = _level * 100;
            int minReward = Mathf.RoundToInt(baseValue * 0.20f);
            int maxReward = Mathf.RoundToInt(baseValue * 0.30f);
            _expReward = UnityEngine.Random.Range(minReward, maxReward + 1);
            _goldReward = UnityEngine.Random.Range(minReward, maxReward + 1);
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
                // Roll on each death so repeated enemy spawns do not always grant
                // the same serialized reward values.
                RollRewards();

                int finalExp = _expReward;
                // Level Difference EXP Penalty when Player Lv > Monster Lv + 5, 10
                if (_useLevelScaling)
                {
                    int playerAdvantage = player.Level - _level;
                    if (playerAdvantage >= 10)
                    {
                        finalExp = Mathf.Max(1, Mathf.RoundToInt(_expReward * 0.80f)); // ลด 20%
                    }
                    else if (playerAdvantage >= 5)
                    {
                        finalExp = Mathf.Max(1, Mathf.RoundToInt(_expReward * 0.90f)); // ลด 10%
                    }
                }

                player.AddGold(_goldReward);
                player.AddEXP(finalExp);
                FloatingCombatText.Show(transform.position, $"+{_goldReward} Gold / +{finalExp} EXP", Color.yellow);
            }
            OnDeath?.Invoke();
        }
    }
}
