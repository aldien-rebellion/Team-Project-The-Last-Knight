using UnityEngine;
using TheLastKnight.Player;
using Unity.Properties;

namespace TheLastKnight.Stats
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Attribute Template Configuration")]
        [SerializeField] private CharacterStatsSO _statsTemplate;

        [Header("Current Runtime Progression")]
        [SerializeField] private int _currentLevel = 1;
        [SerializeField] private int _currentEXP = 0;
        [SerializeField] private int _availableStatPoints = 0;

        [Header("Current Attribute Allocations")]
        [SerializeField] private int _strength;
        [SerializeField] private int _vitality;
        [SerializeField] private int _dexterity;
        [SerializeField] private int _agility;

        [Header("Runtime Status")]
        [SerializeField] private float _currentHP;

        // Derived calculations (cached for other systems to query)
        [CreateProperty]
        public int Level => _currentLevel;
        [CreateProperty]
        public int EXP => _currentEXP;
        [CreateProperty]
        public int EXPNeeded => _statsTemplate != null ? _statsTemplate.GetExpNeededForLevel(_currentLevel) : 100;
        [CreateProperty]
        public float EXPPercentage => EXPNeeded > 0 ? (float)_currentEXP / EXPNeeded : 0;
        [CreateProperty]
        public int StatPoints => _availableStatPoints;

        [CreateProperty]
        public int STR => _strength;
        [CreateProperty]
        public int VIT => _vitality;
        [CreateProperty]
        public int DEX => _dexterity;
        [CreateProperty]
        public int AGI => _agility;

        [CreateProperty]
        public float MaxHP { get; private set; }
        [CreateProperty]
        public float AttackPower { get; private set; }
        [CreateProperty]
        public float CriticalChance { get; private set; }
        [CreateProperty]
        public float CurrentHP => _currentHP;
        [CreateProperty]
        public float HealthPercentage => MaxHP > 0 ? _currentHP / MaxHP : 0;
        [CreateProperty]
        public string HPText => $"{Mathf.CeilToInt(_currentHP)} / {Mathf.CeilToInt(MaxHP)}";

        private PlayerController _playerController;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            InitializeStats();
        }

        private void InitializeStats()
        {
            if (_statsTemplate == null)
            {
                Debug.LogError($"[PlayerStats] Template configuration missing on {gameObject.name}!");
                return;
            }

            // Populate attributes from ScriptableObject base template
            _strength = _statsTemplate.baseSTR;
            _vitality = _statsTemplate.baseVIT;
            _dexterity = _statsTemplate.baseDEX;
            _agility = _statsTemplate.baseAGI;

            RecalculateStats(true); // Full recalculation and set full health
        }

        /// <summary>
        /// Recalculates derived values and synchronizes variables directly with PlayerController.
        /// </summary>
        public void RecalculateStats(bool refillHealth = false)
        {
            if (_statsTemplate == null) return;

            // Calculate derived parameters
            float previousMaxHP = MaxHP;
            MaxHP = _vitality * _statsTemplate.hpPerVIT;
            AttackPower = _strength * _statsTemplate.attackPerSTR;
            CriticalChance = _dexterity * _statsTemplate.critChancePerDEX;

            // Adjust health when Max HP grows
            if (refillHealth)
            {
                _currentHP = MaxHP;
            }
            else
            {
                float hpDifference = MaxHP - previousMaxHP;
                if (hpDifference > 0)
                {
                    _currentHP += hpDifference; // increase current health proportionally
                }
                _currentHP = Mathf.Clamp(_currentHP, 0, MaxHP);
            }

            // Sync stats to Arthur's PlayerController movement logic
            if (_playerController != null)
            {
                // Dynamic scaling of speed based on AGI
                _playerController.MoveSpeed = _playerController.BaseMoveSpeed + (_agility - _statsTemplate.baseAGI) * _statsTemplate.speedPerAGI;
                _playerController.DashSpeed = _playerController.BaseDashSpeed + (_agility - _statsTemplate.baseAGI) * _statsTemplate.dashSpeedPerAGI;
            }

            Debug.Log($"[PlayerStats] Recalculated Derived Parameters. MaxHP: {MaxHP}, AttackPower: {AttackPower}, Speed: {_playerController.MoveSpeed}");
        }

        /// <summary>
        /// Adds Experience Points, triggering Level Up if the threshold is reached.
        /// </summary>
        public void AddEXP(int amount)
        {
            _currentEXP += amount;
            Debug.Log($"[PlayerStats] Gained +{amount} EXP. Total: {_currentEXP}/{EXPNeeded}");

            while (_currentEXP >= EXPNeeded)
            {
                LevelUp();
            }
        }

        private void LevelUp()
        {
            _currentEXP -= EXPNeeded;
            _currentLevel++;
            _availableStatPoints += 5; // Grant 5 stat upgrade points per level

            RecalculateStats();
            
            // Fully restore HP on Level Up
            _currentHP = MaxHP;

            Debug.Log($"<color=yellow>[PlayerStats] Level Up! New Level: {_currentLevel}, Available Stat Points: {_availableStatPoints}</color>");
        }

        /// <summary>
        /// Upgrades an attribute using available Stat Points.
        /// </summary>
        public bool UpgradeStat(string statName)
        {
            if (_availableStatPoints <= 0)
            {
                Debug.LogWarning("[PlayerStats] Attempted to upgrade stat, but no Stat Points are available!");
                return false;
            }

            bool upgraded = false;
            switch (statName.ToUpper())
            {
                case "STR":
                case "STRENGTH":
                    _strength++;
                    upgraded = true;
                    break;
                case "VIT":
                case "VITALITY":
                    _vitality++;
                    upgraded = true;
                    break;
                case "DEX":
                case "DEXTERITY":
                    _dexterity++;
                    upgraded = true;
                    break;
                case "AGI":
                case "AGILITY":
                    _agility++;
                    upgraded = true;
                    break;
                default:
                    Debug.LogError($"[PlayerStats] Unknown stat upgrade requested: {statName}");
                    break;
            }

            if (upgraded)
            {
                _availableStatPoints--;
                RecalculateStats();
                Debug.Log($"[PlayerStats] Stat Modified: {statName.ToUpper()} upgraded. Remaining points: {_availableStatPoints}");
            }

            return upgraded;
        }

        /// <summary>
        /// Applies incoming damage to Arthur.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_playerController != null && _playerController.IsInvincible)
            {
                Debug.Log("[PlayerStats] Damage avoided! Arthur is invincible while dashing!");
                return;
            }

            _currentHP -= damage;
            _currentHP = Mathf.Max(_currentHP, 0);
            Debug.Log($"[PlayerStats] Arthur took {damage} damage! HP: {_currentHP}/{MaxHP}");

            if (_currentHP <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.LogError("[PlayerStats] Arthur has perished!");
            // TODO: Trigger save system reload, death screen, or custom respawn event
        }
    }
}