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
        [Tooltip("When enabled, changing Level in Inspector automatically scales MaxHealth, AttackPower, Defense, based on project formula. Rewards always use Level and are rolled on every death.")]
        [SerializeField] private bool _useLevelScaling = false;

        [Header("Health & Defense")]
        [SerializeField] private float _maxHealth = 50f;
        [SerializeField] private float _defense = 0f;
        [SerializeField] private float _attackPower = 10f;
        [Tooltip("Increase damage received from players by their missing HP percentage (50% HP remaining = 50% more damage).")]
        [SerializeField] private bool _scalePlayerDamageWithMissingHealth;
        [SerializeField, HideInInspector] private int _goldReward = 8;
        [SerializeField, HideInInspector] private int _expReward = 15;
        public void SetRewards(int gold, int experience) { _goldReward = gold; _expReward = experience; }

        [Header("Status Effect")]
        [SerializeField] private StatusEffect _currentStatus = StatusEffect.None;
        private float _statusTimer = 0f;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        private bool _defenseSuppressed;
        private float? _temporaryDefense;
        public float Defense => _defenseSuppressed ? 0f : (_temporaryDefense ?? _defense);
        public void SetDefenseSuppressed(bool suppressed) => _defenseSuppressed = suppressed;
        public void SetTemporaryDefense(float defense) => _temporaryDefense = Mathf.Max(0f, defense);
        public void ClearTemporaryDefense() => _temporaryDefense = null;
        public float AttackPower => _attackPower;
        public bool IsDead { get; private set; } = false;
        public StatusEffect CurrentStatus => _currentStatus;

        public event Action<float, float> OnHealthChanged;
        public event Action<DamageData> OnDamaged;
        public event Action OnDeath;

        private void Awake()
        {
            CurrentHealth = _maxHealth;
            if (GetComponent<EnemyLootDrop>() == null)
            {
                gameObject.AddComponent<EnemyLootDrop>();
            }
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
            _maxHealth = _level * 50f;
            _attackPower = _level * 7f;
            _defense = _level * 1f;

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

            float actualDamage = Mathf.Max(1f, damageData.amount - Defense);

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

            // Only explicit player attacks receive this bonus; environmental damage does not.
            if (_scalePlayerDamageWithMissingHealth && damageData.attacker != null
                && player != null && player.MaxHP > 0f)
            {
                actualDamage *= 1f + (1f - Mathf.Clamp01(player.HealthPercentage));
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
            hasSpecificHitSound = hasSpecificHitSound
                || (attackingPlayer != null
                    && GetComponent<TheLastKnight.AI.UndeadExecutionerAudioController>() is { isActiveAndEnabled: true });
            hasSpecificHitSound = hasSpecificHitSound
                || (attackingPlayer != null
                    && GetComponent<TheLastKnight.AI.VolcanoxAudioController>() is { isActiveAndEnabled: true });
            if (attackingPlayer != null)
                TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("player_hit");
            else if (!hasSpecificHitSound)
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

        public void ApplyStatus(StatusEffect effect, float duration, bool cancelAttack = true)
        {
            if (IsDead) return;
            bool enteringStun = effect == StatusEffect.Stunned && _currentStatus != StatusEffect.Stunned;
            _currentStatus = effect;
            _statusTimer = duration;

            if (effect == StatusEffect.Stunned)
            {
                if (enteringStun)
                    GetComponent<TheLastKnight.AI.MechaStoneGolemAudioController>()?.PlayStunned();

                if (cancelAttack)
                {
                    GetComponent<TheLastKnight.AI.EnemyController>()?.CancelAttack();
                    GetComponent<SlimeController>()?.CancelAttack();
                }
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
            int level = Mathf.Clamp(_level, 1, (int.MaxValue - 1) / 80);
            _expReward = UnityEngine.Random.Range(level * 20, level * 80 + 1);
            _goldReward = UnityEngine.Random.Range(level * 5, level * 10 + 1);
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("enemy_death");
            CurrentHealth = 0f;
            RollRewards();
            var player = FindAnyObjectByType<TheLastKnight.Stats.PlayerStats>();
            if (player != null)
            {
                int finalExp = _expReward;
                player.AddGold(_goldReward);
                player.AddEXP(finalExp);
                FloatingCombatText.Show(transform.position, $"+{_goldReward} Gold / +{finalExp} EXP", Color.yellow);
            }
            OnDeath?.Invoke();
        }
    }
}
