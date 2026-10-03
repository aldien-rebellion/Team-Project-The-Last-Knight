using UnityEngine;
using TheLastKnight.Player;
using TheLastKnight.Combat;
using Unity.Properties;

namespace TheLastKnight.Stats
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerStats : MonoBehaviour, IDamageable
    {
        [Header("Attribute Template Configuration")]
        [SerializeField] private CharacterStatsSO _statsTemplate;

        [Header("Current Runtime Progression")]
        [SerializeField] private int _currentLevel = 1;
        [SerializeField] private int _currentEXP = 0;
        [SerializeField] private int _availableStatPoints = 5;

        [Header("Current Attribute Allocations")]
        [SerializeField] private int _strength;
        [SerializeField] private int _vitality;
        [SerializeField] private int _dexterity;
        [SerializeField] private int _agility;

        [Header("Runtime Status")]
        [SerializeField] private float _currentHP;
        [SerializeField] private float _currentStamina = 100f;
        [CreateProperty]
        public float MaxStamina { get; private set; } = 100f;
        public float CurrentStamina => _currentStamina;
        public float StaminaPercentage => MaxStamina > 0f ? _currentStamina / MaxStamina : 0f;
        public bool IsDead => _currentHP <= 0;
        [SerializeField] private int _gold;
        [SerializeField] private int _healingPotions = 3;
        public int Gold => _gold;
        public int HealingPotions => _healingPotions;
        public const int MaxHealingPotions = 5;
        public void AddGold(int amount) => _gold += Mathf.Max(0, amount);
        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || _gold < amount) return false;
            _gold -= amount;
            return true;
        }
        public bool AddPotion()
        {
            var inventory = TheLastKnight.Inventory.InventoryManager.Instance;
            return inventory != null && inventory.AddItem(TheLastKnight.Inventory.ItemRegistry.CreateItem("potion_heal")) == 0;
        }
        public bool CompletePotionDrink()
        {
            var inventory = TheLastKnight.Inventory.InventoryManager.Instance;
            int index = TheLastKnight.Core.QuickItemManager.Instance?.GetActiveSlotIndex() ?? -1;
            var item = inventory?.GetSlot(TheLastKnight.Inventory.SlotType.QuickSlot, index);
            return item != null && item.id == "potion_heal" && inventory.UseQuickSlot(index, this);
        }
        public void SyncHealingPotions(int amount)
        {
            _healingPotions = Mathf.Max(0, amount);
        }

        public void RestoreStamina(float amount)
        {
            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + Mathf.Max(0, amount));
        }

        public void AddStatPoints(int amount) => _availableStatPoints += Mathf.Max(0, amount);
        public bool AddStatPotion(string stat)
        {
            if (stat != "STR" && stat != "VIT" && stat != "DEX" && stat != "AGI") return false;
            _availableStatPoints++;
            return UpgradeStat(stat);
        }
        public void AddAGI(int amount)
        {
            _agility += Mathf.Max(0, amount);
            RecalculateStats();
        }
        public void SetAGI(int value)
        {
            _agility = Mathf.Max(0, value);
            RecalculateStats();
        }
        private readonly System.Collections.Generic.HashSet<Object> _regenAuras = new System.Collections.Generic.HashSet<Object>();
        private float _lastDamageTime = -100f;
        public const float HPRegenDelay = 5f;
        public const float BaseHPRegenRate = 0.02f; // 2% of MaxHP per second
        public const float MaxAuraHPPercent = 0.70f; // Regen Aura (Medusa) caps HP regen at 70% of MaxHP
        public float LastDamageTime => _lastDamageTime;
        public void SetLastDamageTimeForTesting(float time) => _lastDamageTime = time;

        private float _lastStaminaSpendTime = -100f;
        public const float StaminaRegenDelay = 1.0f; // 1 second delay after spending stamina before regen starts
        public const float BaseStaminaRegenPercent = 0.10f; // 10% of MaxStamina per second
        public const float AuraStaminaRegenPercent = 0.20f; // 20% of MaxStamina per second
        public float LastStaminaSpendTime => _lastStaminaSpendTime;
        public void SetLastStaminaSpendTimeForTesting(float time) => _lastStaminaSpendTime = time;

        public void SetRegenAura(Object source, bool active)
        {
            if (active) _regenAuras.Add(source); else _regenAuras.Remove(source);
        }
        public bool HasRegenAura => _regenAuras.Count > 0;

        public static event System.Action OnInsufficientStaminaGlobal;
        public event System.Action OnInsufficientStamina;
        private float _lastInsufficientStaminaTime = -100f;
        public const float InsufficientStaminaCooldown = 0.25f;

        public void NotifyInsufficientStamina()
        {
            if (Time.time - _lastInsufficientStaminaTime < InsufficientStaminaCooldown) return;
            _lastInsufficientStaminaTime = Time.time;
            OnInsufficientStamina?.Invoke();
            OnInsufficientStaminaGlobal?.Invoke();
        }

        public void ResetInsufficientStaminaCooldownForTesting() => _lastInsufficientStaminaTime = -100f;

        public bool TrySpendStamina(float amount)
        {
            if (amount < 0 || _currentStamina < amount || IsDead)
            {
                if (amount > 0 && _currentStamina < amount && !IsDead)
                {
                    NotifyInsufficientStamina();
                }
                return false;
            }
            _currentStamina -= amount;
            if (amount > 0)
            {
                _lastStaminaSpendTime = Time.time;
            }
            return true;
        }

        private void Update()
        {
            if (IsDead || _playerController == null) return;
            _regenAuras.RemoveWhere(source => source == null);
            if (_playerController.CurrentState == PlayerState.Idle || _playerController.CurrentState == PlayerState.Walking)
            {
                RegenerateStamina(Time.deltaTime);
                RegenerateHP(Time.deltaTime);
            }
        }

        public void RegenerateStamina(float deltaTime)
        {
            if (IsDead || _playerController == null) return;
            if (_playerController.CurrentState != PlayerState.Idle && _playerController.CurrentState != PlayerState.Walking) return;
            if (Time.time - _lastStaminaSpendTime < StaminaRegenDelay || _currentStamina >= MaxStamina) return;

            float regenPercent = _regenAuras.Count > 0 ? AuraStaminaRegenPercent : BaseStaminaRegenPercent;
            float regenRate = MaxStamina * regenPercent * TheLastKnight.Core.GameDifficultyManager.Regeneration;
            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + regenRate * deltaTime);
        }

        public void RegenerateHP(float deltaTime)
        {
            if (IsDead || _playerController == null) return;
            if (_playerController.CurrentState != PlayerState.Idle && _playerController.CurrentState != PlayerState.Walking) return;
            if (_regenAuras.Count == 0) return;
            float maxRegenHP = MaxHP * MaxAuraHPPercent;
            if (Time.time - _lastDamageTime < HPRegenDelay || _currentHP >= maxRegenHP) return;

            float regenRate = BaseHPRegenRate * TheLastKnight.Core.GameDifficultyManager.Regeneration;
            _currentHP = Mathf.Min(maxRegenHP, _currentHP + MaxHP * regenRate * deltaTime);
        }

        public void Heal(float amount) => _currentHP = Mathf.Min(MaxHP, _currentHP + Mathf.Max(0, amount));
        public void Rest() { _currentHP = MaxHP; _currentStamina = MaxStamina; _lastDamageTime = -100f; _lastStaminaSpendTime = -100f; }

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
        public float AttackPower
        {
            get
            {
                float bonus = 0f;
                if (Time.time < _mightExpiresAt) bonus += 0.25f;
                if (Time.time < _skill2BuffExpiresAt) bonus += 0.22f;
                return _baseAttackPower * (1f + bonus);
            }
            private set => _baseAttackPower = value;
        }
        [CreateProperty]
        public float BaseAttackPower => _baseAttackPower;
        private float _baseAttackPower;
        private float _mightExpiresAt;
        public const float MightBuffMultiplier = 0.25f; // +25% ATK
        public const float MightBuffDuration = 30f; // 30 seconds
        public void ApplyMightBuff() => _mightExpiresAt = Time.time + MightBuffDuration;
        public void RemoveMightBuff() => _mightExpiresAt = 0f;
        public bool HasMightBuff => Time.time < _mightExpiresAt;
        public float MightBuffRemaining => Mathf.Max(0f, _mightExpiresAt - Time.time);
        public void SetMightBuffExpiresAtForTesting(float time) => _mightExpiresAt = time;

        private float _skill2BuffExpiresAt;
        public const float Skill2BuffMultiplier = 0.22f; // +22% ATK
        public const float Skill2BuffDuration = 15f; // 15 seconds
        public void ApplySkill2Buff() => _skill2BuffExpiresAt = Time.time + Skill2BuffDuration;
        public void RemoveSkill2Buff() => _skill2BuffExpiresAt = 0f;
        public bool HasSkill2Buff => Time.time < _skill2BuffExpiresAt;
        public float Skill2BuffRemaining => Mathf.Max(0f, _skill2BuffExpiresAt - Time.time);
        public void SetSkill2BuffExpiresAtForTesting(float time) => _skill2BuffExpiresAt = time;
        public void SetRegenAuraForTesting(bool active) => SetRegenAura(this, active);

        public void GetActiveBuffs(System.Collections.Generic.List<ActiveBuffInfo> list)
        {
            if (list == null) return;
            list.Clear();

            // 1. Skill 2: Iron Will (Berserk Buff)
            if (HasSkill2Buff)
            {
                float rem = Skill2BuffRemaining;
                list.Add(new ActiveBuffInfo
                {
                    id = "buff_iron_will",
                    name = "Iron Will",
                    category = "Combat Skill Buff",
                    description = "Boosts Attack Power by +22% with unwavering warrior spirit.",
                    remainingSeconds = rem,
                    totalDuration = Skill2BuffDuration,
                    formattedTime = FormatMinecraftTime(rem),
                    icon = Resources.Load<Sprite>("BuffIcons/buff_iron_will") ?? Resources.Load<Sprite>("CharacterStatus/Skill_BerserkBuff"),
                    isDebuff = false,
                    themeColor = new Color(1f, 0.48f, 0.15f) // Fiery Orange
                });
            }

            // 2. Potion of Might
            if (HasMightBuff)
            {
                float rem = MightBuffRemaining;
                Sprite icon = Resources.Load<Sprite>("BuffIcons/buff_might");
                if (icon == null)
                {
                    var def = Resources.Load<TheLastKnight.Inventory.ItemDefinition>("Items/Definitions/Consumables/potion_might");
                    if (def != null && def.icon != null) icon = def.icon;
                }
                if (icon == null) icon = Resources.Load<Sprite>("CharacterStatus/Item_BluePotion");

                list.Add(new ActiveBuffInfo
                {
                    id = "buff_might",
                    name = "Potion of Might",
                    category = "Elixir Enhancement",
                    description = "Infuses weapons with brute force, boosting Attack Power by +25%.",
                    remainingSeconds = rem,
                    totalDuration = MightBuffDuration,
                    formattedTime = FormatMinecraftTime(rem),
                    icon = icon,
                    isDebuff = false,
                    themeColor = new Color(0.72f, 0.35f, 0.95f) // Violet / Purple
                });
            }

            // 3. Medusa Sacred Fountain Aura
            if (HasRegenAura)
            {
                list.Add(new ActiveBuffInfo
                {
                    id = "aura_medusa",
                    name = "Fountain Grace",
                    category = "Sacred Aura",
                    description = "Sacred blessing restoring Stamina (+20%/s) and HP up to 70% Max HP.",
                    remainingSeconds = -1f,
                    totalDuration = -1f,
                    formattedTime = "**:**",
                    icon = Resources.Load<Sprite>("BuffIcons/buff_medusa") ?? Resources.Load<Sprite>("CharacterStatus/Item_RuneTrident"),
                    isDebuff = false,
                    themeColor = new Color(0.25f, 0.85f, 1f) // Holy Cyan
                });
            }

            // 4. Stunned Debuff (when player is in Hurt/Stunned state)
            if (_playerController != null && _playerController.CurrentState == Player.PlayerState.Hurt)
            {
                list.Add(new ActiveBuffInfo
                {
                    id = "debuff_stun",
                    name = "Stunned",
                    category = "Status Debuff",
                    description = "Stunned and incapacitated. Cannot move, attack, or cast skills.",
                    remainingSeconds = 1f,
                    totalDuration = 1f,
                    formattedTime = "0:01",
                    icon = Resources.Load<Sprite>("BuffIcons/debuff_stun") ?? Resources.Load<Sprite>("CharacterStatus/Slot_Frame"),
                    isDebuff = true,
                    themeColor = new Color(1f, 0.25f, 0.25f) // Red
                });
            }
        }

        public static string FormatMinecraftTime(float seconds)
        {
            if (seconds < 0f) return "**:**";
            int totalSec = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int m = totalSec / 60;
            int s = totalSec % 60;
            return $"{m}:{s:D2}";
        }
        [CreateProperty]
        public float CriticalChance { get; private set; }
        [CreateProperty]
        public float Defense { get; private set; }
        [CreateProperty]
        public float AttackSpeedMultiplier { get; private set; } = 1.0f;
        [CreateProperty]
        public bool CanDoubleJump => _agility >= (_statsTemplate != null ? _statsTemplate.doubleJumpAgiThreshold : DoubleJumpAgiThreshold);
        public const int DoubleJumpAgiThreshold = 250;
        [CreateProperty]
        public float CurrentHP => _currentHP;
        [CreateProperty]
        public float HealthPercentage => MaxHP > 0 ? _currentHP / MaxHP : 0;
        [CreateProperty]
        public string HPText => $"{Mathf.CeilToInt(_currentHP)}/{Mathf.CeilToInt(MaxHP)}";

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
            // Calculate derived parameters
            float previousMaxHP = MaxHP;
            float previousMaxStamina = MaxStamina;

            float hpPerVit = _statsTemplate != null ? _statsTemplate.hpPerVIT : 10f;
            float baseStam = _statsTemplate != null ? _statsTemplate.baseStamina : 100f;
            float stamPerVit = _statsTemplate != null ? _statsTemplate.staminaPerVIT : 1f;
            int baseVit = _statsTemplate != null ? _statsTemplate.baseVIT : 10;
            float baseAtk = _statsTemplate != null ? _statsTemplate.baseAttack : 0f;
            float atkPerStr = _statsTemplate != null ? _statsTemplate.attackPerSTR : 1.5f;
            float baseAtkSpd = _statsTemplate != null ? _statsTemplate.baseAttackSpeed : 1.0f;
            float atkSpdPerAgi = _statsTemplate != null ? _statsTemplate.attackSpeedPerAGI : 0.004f;
            int baseAgi = _statsTemplate != null ? _statsTemplate.baseAGI : 10;
            float spdPerAgi = _statsTemplate != null ? _statsTemplate.speedPerAGI : 0.12f;
            float dashSpdPerAgi = _statsTemplate != null ? _statsTemplate.dashSpeedPerAGI : 0.15f;

            // STR -> ATK
            AttackPower = baseAtk + _strength * atkPerStr;

            // VIT -> Max HP and Max Stamina
            MaxHP = _vitality * hpPerVit;
            MaxStamina = baseStam + Mathf.Max(0, _vitality - baseVit) * stamPerVit;

            // DEX -> Asymptotic Critical Chance approaching 100% (99% at 250 DEX)
            if (_statsTemplate != null)
            {
                CriticalChance = _statsTemplate.CalculateCritChance(_dexterity);
            }
            else
            {
                float remainingRatio = 0.01f;
                float crit = 100f * (1f - Mathf.Pow(remainingRatio, (float)_dexterity / 250f));
                CriticalChance = Mathf.Clamp(crit, 0f, 100f);
            }

            // AGI -> Attack Speed, Movement Speed, Double Jump
            AttackSpeedMultiplier = baseAtkSpd + Mathf.Max(0, _agility - baseAgi) * atkSpdPerAgi;

            // DEF -> Level-based Defense (DEF = baseDEF + Level * defPerLevel)
            float baseDef = _statsTemplate != null ? _statsTemplate.baseDEF : 0f;
            float defPLv = _statsTemplate != null ? _statsTemplate.defPerLevel : 1f;
            Defense = baseDef + _currentLevel * defPLv;

            // Adjust health and stamina when caps grow
            if (refillHealth)
            {
                _currentHP = MaxHP;
                _currentStamina = MaxStamina;
            }
            else
            {
                float hpDifference = MaxHP - previousMaxHP;
                if (hpDifference > 0)
                {
                    _currentHP += hpDifference;
                }
                _currentHP = Mathf.Clamp(_currentHP, 0, MaxHP);

                float staminaDifference = MaxStamina - previousMaxStamina;
                if (staminaDifference > 0)
                {
                    _currentStamina += staminaDifference;
                }
                _currentStamina = Mathf.Clamp(_currentStamina, 0, MaxStamina);
            }

            // Sync stats to Arthur's PlayerController movement & combat logic
            if (_playerController != null)
            {
                // Dynamic scaling of speed based on AGI (unified: single speedPerAGI value controls both walk & sprint)
                _playerController.MoveSpeed = _playerController.BaseMoveSpeed + (_agility - baseAgi) * spdPerAgi;
                float sprintRatio = _playerController.BaseMoveSpeed > 0f ? (_playerController.BaseSprintSpeed / _playerController.BaseMoveSpeed) : 1.625f;
                _playerController.SprintSpeed = _playerController.MoveSpeed * sprintRatio;
                _playerController.DashSpeed = _playerController.BaseDashSpeed + (_agility - baseAgi) * dashSpdPerAgi;
                _playerController.AttackSpeedMultiplier = AttackSpeedMultiplier;
                _playerController.CanDoubleJump = CanDoubleJump;
            }

            Debug.Log($"[PlayerStats] Recalculated Derived Parameters. MaxHP: {MaxHP}, MaxStamina: {MaxStamina}, ATK: {AttackPower}, DEF: {Defense}, Crit: {CriticalChance:F2}%, AtkSpd: {AttackSpeedMultiplier:F2}x, Speed: {(_playerController != null ? _playerController.MoveSpeed : 0)}, DoubleJump: {CanDoubleJump}");
        }

        /// <summary>
        /// Adds Experience Points, triggering Level Up if the threshold is reached.
        /// </summary>
        public void AddEXP(int amount)
        {
            _currentEXP += Mathf.Max(0, amount);
            Debug.Log($"[PlayerStats] Gained +{amount} EXP. Total: {_currentEXP}/{EXPNeeded}");

            while (_currentEXP >= EXPNeeded)
            {
                LevelUp();
            }
        }

        public const int DefaultStatPointsPerLevel = 10;

        private void LevelUp()
        {
            _currentEXP -= EXPNeeded;
            _currentLevel++;
            int pointsGained = (_statsTemplate != null && _statsTemplate.statPointsPerLevel > 0)
                ? _statsTemplate.statPointsPerLevel
                : DefaultStatPointsPerLevel;
            _availableStatPoints += pointsGained; // Grant 10 stat upgrade points per level

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
            if (_adminInvincible) return;
            if (_playerController != null && _playerController.IsInvincible)
            {
                Debug.Log("[PlayerStats] Damage avoided! Arthur is invincible!");
                return;
            }

            if (IsDead) return;
            damage = Mathf.Max(0f, damage) * TheLastKnight.Core.GameDifficultyManager.EnemyDamage;
            // Apply Defense: reduce damage by DEF, minimum 1 damage
            if (damage > 0f)
            {
                damage = Mathf.Max(1f, damage - Defense);
            }
            if (damage > 0f)
            {
                _lastDamageTime = Time.time;
            }
            _currentHP -= damage;
            _currentHP = Mathf.Max(_currentHP, 0);
            Debug.Log($"[PlayerStats] Arthur took {damage} damage! HP: {_currentHP}/{MaxHP}");

            if (_playerController != null)
            {
                _playerController.OnTakeDamage();
            }

            if (_currentHP <= 0)
            {
                Die();
            }
        }

        private bool _adminInvincible;
        public bool AdminInvincible { get => _adminInvincible; set => _adminInvincible = value; }

        public void TakeDamage(DamageData damageData)
        {
            TakeDamage(damageData.amount);
        }

        public void TakeDamage(float damage, bool isStun, float stunDuration = 1.0f)
        {
            TakeDamage(damage);
            if (isStun && !IsDead)
            {
                ApplyStun(stunDuration);
            }
        }

        public void ApplyStun(float duration = 1.0f)
        {
            if (_playerController != null)
            {
                _playerController.ApplyStun(duration);
            }
        }

        public void ApplyStatus(StatusEffect effect, float duration)
        {
            if (_playerController != null)
            {
                _playerController.ApplyStatus(effect, duration);
            }
        }

        private void Die()
        {
            if (_playerController != null && _playerController.CurrentState == PlayerState.Excalibur)
            {
                _playerController.CancelExcalibur();
            }
            TheLastKnight.Core.GameManager.Instance?.PlayerDied();
        }

        public void Capture(TheLastKnight.Core.PlayerSaveData state)
        {
            state.initialized = true;
            state.hp = _currentHP; state.stamina = _currentStamina;
            state.level = _currentLevel; state.exp = _currentEXP; state.statPoints = _availableStatPoints;
            state.strength = _strength; state.vitality = _vitality; state.dexterity = _dexterity; state.agility = _agility;
            state.gold = _gold; state.potions = _healingPotions;
        }

        public void Restore(TheLastKnight.Core.PlayerSaveData state)
        {
            _currentLevel = state.level; _currentEXP = state.exp; _availableStatPoints = state.statPoints;
            _strength = state.strength; _vitality = state.vitality; _dexterity = state.dexterity; _agility = state.agility;
            _gold = state.gold; _healingPotions = state.potions;
            RecalculateStats();
            _currentHP = Mathf.Clamp(state.hp, 0, MaxHP);
            _currentStamina = Mathf.Clamp(state.stamina, 0, MaxStamina);
            _lastDamageTime = -100f;

        }
    }
}
