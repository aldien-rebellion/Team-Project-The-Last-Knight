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
        public const int MaxDexterity = 200;
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
            if (inventory == null) return false;
            // A repeated animation callback must not start another immediate use.
            var controller = _playerController != null ? _playerController : GetComponent<PlayerController>();
            if (controller != null && controller.CurrentState == PlayerState.Drinking && !inventory.QuickItemUse.IsPending) return false;
            return inventory.QuickItemUse.CompleteHealingPotion(this);
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
        public void AddVIT(int amount)
        {
            _vitality += Mathf.Max(0, amount);
            RecalculateStats();
        }
        public void AddSTR(int amount)
        {
            _strength += Mathf.Max(0, amount);
            RecalculateStats();
        }
        public void AddDEX(int amount)
        {
            _dexterity += Mathf.Max(0, amount);
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
        public const float StaminaRegenDelay = 0.25f; // 0.25 second delay after spending stamina before regen starts
        public const float BaseStaminaRegenPercent = 0.10f; // 10% of MaxStamina per second
        public const float AuraStaminaRegenPercent = 0.20f; // 20% of MaxStamina per second
        public float LastStaminaSpendTime => _lastStaminaSpendTime;
        public void SetLastStaminaSpendTimeForTesting(float time) => _lastStaminaSpendTime = time;

        public void SetRegenAura(Object source, bool active)
        {
            if (active) _regenAuras.Add(source); else _regenAuras.Remove(source);
        }
        public bool HasRegenAura
        {
            get
            {
                foreach (var source in _regenAuras)
                    if (source != null && (!(source is TheLastKnight.Environment.MedusaAura aura) || aura.CanHeal)) return true;
                return false;
            }
        }

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

        public float GetStaminaCost(float baseCost)
        {
            float cost = baseCost * TheLastKnight.Core.GameDifficultyManager.StaminaConsumption;
            return cost > 0f ? cost * _buffs.StaminaCostMultiplier : cost;
        }

        public bool TrySpendStamina(float amount)
        {
            amount = GetStaminaCost(amount);
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

        private readonly TemporaryBuffs _buffs = new TemporaryBuffs(() => Time.time);
        private readonly BuffDisplay _buffDisplay = new BuffDisplay();

        private void Update()
        {
            if (IsDead || _playerController == null) return;
            _regenAuras.RemoveWhere(source => source == null);

            // Potion of Regeneration: heals +5% Max HP per second, blocked if Undying
            if (HasRegenBuff && !HasUndyingBuff && _currentHP < MaxHP)
            {
                _currentHP = Mathf.Min(MaxHP, _currentHP + MaxHP * 0.05f * Time.deltaTime);
            }

            if (_buffs.Tick()) RecalculateStats();

            if (_playerController.CurrentState == PlayerState.Idle || _playerController.CurrentState == PlayerState.Walking)
            {
                RegenerateStamina(Time.deltaTime);
                if (!HasUndyingBuff)
                {
                    RegenerateHP(Time.deltaTime);
                }
            }
        }

        public void RegenerateStamina(float deltaTime)
        {
            if (IsDead || _playerController == null) return;
            if (_playerController.CurrentState != PlayerState.Idle && _playerController.CurrentState != PlayerState.Walking) return;
            if (Time.time - _lastStaminaSpendTime < StaminaRegenDelay || _currentStamina >= MaxStamina) return;

            float regenPercent = HasRegenAura ? AuraStaminaRegenPercent : BaseStaminaRegenPercent;
            float regenRate = MaxStamina * regenPercent * TheLastKnight.Core.GameDifficultyManager.Regeneration;
            _currentStamina = Mathf.Min(MaxStamina, _currentStamina + regenRate * deltaTime);
        }

        public void RegenerateHP(float deltaTime)
        {
            if (IsDead || _playerController == null || HasUndyingBuff) return;
            if (_playerController.CurrentState != PlayerState.Idle && _playerController.CurrentState != PlayerState.Walking) return;
            if (!HasRegenAura) return;
            float maxRegenHP = MaxHP * MaxAuraHPPercent;
            if (Time.time - _lastDamageTime < HPRegenDelay || _currentHP >= maxRegenHP) return;

            float regenRate = BaseHPRegenRate * TheLastKnight.Core.GameDifficultyManager.Regeneration;
            _currentHP = Mathf.Min(maxRegenHP, _currentHP + MaxHP * regenRate * deltaTime);
        }

        public void Heal(float amount)
        {
            if (HasUndyingBuff) return;
            _currentHP = Mathf.Min(MaxHP, _currentHP + Mathf.Max(0, amount));
        }
        public void Rest()
        {
            _currentHP = Mathf.Max(_currentHP, MaxHP * MaxAuraHPPercent);
            _currentStamina = MaxStamina;
            _lastDamageTime = -100f;
            _lastStaminaSpendTime = -100f;
        }

        // Derived calculations (cached for other systems to query)
        [CreateProperty]
        public int Level => _currentLevel;
        [CreateProperty]
        public int EXP => _currentEXP;
        [CreateProperty]
        public int EXPNeeded => _statsTemplate != null ? _statsTemplate.GetExpNeededForLevel(_currentLevel) : 100;

        public int EarthSpellbookEXP => _statsTemplate != null ? _statsTemplate.GetExpNeededForLevel(10) : 100;

        public int AdvancedSpellbookEXP => _statsTemplate != null ? _statsTemplate.GetExpNeededForLevel(20) : 100;

        public int IceSpellbookEXP => _statsTemplate != null ? _statsTemplate.GetExpNeededForLevel(30) : 100;
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
            get => _baseAttackPower * (1f + _buffs.AttackBonus);
            private set => _baseAttackPower = value;
        }
        [CreateProperty]
        public float BaseAttackPower => _baseAttackPower;
        private float _baseAttackPower;
        public const float MightBuffMultiplier = TemporaryBuffs.MightAttackBonus; // +25% ATK
        public const float MightBuffDuration = TemporaryBuffs.PotionDuration; // 1 minute 30 seconds
        public void ApplyMightBuff() => _buffs.Apply(TemporaryBuff.Might);
        public void RemoveMightBuff() => _buffs.Remove(TemporaryBuff.Might);
        public bool HasMightBuff => _buffs.IsActive(TemporaryBuff.Might);
        public float MightBuffRemaining => _buffs.Remaining(TemporaryBuff.Might);

        // Potion of Swiftness (Potion 1-2): +25% Attack Speed & Movement Speed for 90s
        public const float SwiftnessBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplySwiftnessBuff() { _buffs.Apply(TemporaryBuff.Swiftness); RecalculateStats(); }
        public void RemoveSwiftnessBuff() { _buffs.Remove(TemporaryBuff.Swiftness); RecalculateStats(); }
        public bool HasSwiftnessBuff => _buffs.IsActive(TemporaryBuff.Swiftness);
        public float SwiftnessBuffRemaining => _buffs.Remaining(TemporaryBuff.Swiftness);

        // Potion of Endurance (Potion 1-3): -50% all Stamina consumption for 90s
        public const float EnduranceBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplyEnduranceBuff() => _buffs.Apply(TemporaryBuff.Endurance);
        public void RemoveEnduranceBuff() => _buffs.Remove(TemporaryBuff.Endurance);
        public bool HasEnduranceBuff => _buffs.IsActive(TemporaryBuff.Endurance);
        public float EnduranceBuffRemaining => _buffs.Remaining(TemporaryBuff.Endurance);

        // Potion of Purity (Potion 1-4): Immune to stun and status ailments for 90s
        public const float PurityBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplyPurityBuff()
        {
            _buffs.Apply(TemporaryBuff.Purity);
            if (_playerController != null && _playerController.CurrentState == Player.PlayerState.Hurt)
            {
                _playerController.EndHurt();
            }
        }
        public void RemovePurityBuff() => _buffs.Remove(TemporaryBuff.Purity);
        public bool HasPurityBuff => _buffs.IsActive(TemporaryBuff.Purity);
        public float PurityBuffRemaining => _buffs.Remaining(TemporaryBuff.Purity);

        // Potion of Regeneration (Potion 1-5): +5% Max HP/sec for 90s
        public const float RegenBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplyRegenBuff() => _buffs.Apply(TemporaryBuff.Regeneration);
        public void RemoveRegenBuff() => _buffs.Remove(TemporaryBuff.Regeneration);
        public bool HasRegenBuff => _buffs.IsActive(TemporaryBuff.Regeneration);
        public float RegenBuffRemaining => _buffs.Remaining(TemporaryBuff.Regeneration);

        // Potion of Fortitude (Potion 1-7): +25% Max HP & DEF for 90s
        public const float FortitudeBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplyFortitudeBuff()
        {
            bool hadBuff = HasFortitudeBuff;
            _buffs.Apply(TemporaryBuff.Fortitude);
            if (!hadBuff)
            {
                float prevMaxHP = MaxHP;
                RecalculateStats();
                _currentHP += Mathf.Max(0f, MaxHP - prevMaxHP);
            }
        }
        public void RemoveFortitudeBuff() { _buffs.Remove(TemporaryBuff.Fortitude); RecalculateStats(); }
        public bool HasFortitudeBuff => _buffs.IsActive(TemporaryBuff.Fortitude);
        public float FortitudeBuffRemaining => _buffs.Remaining(TemporaryBuff.Fortitude);

        // Potion of the Undying (Potion 1-8): HP becomes 1, no heal, invincible for 90s
        public const float UndyingBuffDuration = TemporaryBuffs.PotionDuration;
        public void ApplyUndyingBuff()
        {
            _buffs.Apply(TemporaryBuff.Undying);
            _currentHP = 1f;
        }
        public void RemoveUndyingBuff() => _buffs.Remove(TemporaryBuff.Undying);
        public bool HasUndyingBuff => _buffs.IsActive(TemporaryBuff.Undying);
        public float UndyingBuffRemaining => _buffs.Remaining(TemporaryBuff.Undying);

        public const float CritDamageBuffDuration = TemporaryBuffs.PotionDuration;
        public const string CritDamageBuffDescription = "Sets DEX critical damage (attacks and skills) to 300% and Parry critical damage to 500% for 90 seconds. Using again refreshes the duration.";
        public void ApplyCritDamageBuff() => _buffs.Apply(TemporaryBuff.CriticalDamage);
        public void RemoveCritDamageBuff() => _buffs.Remove(TemporaryBuff.CriticalDamage);
        public bool HasCritDamageBuff => _buffs.IsActive(TemporaryBuff.CriticalDamage);
        public float CritDamageBuffRemaining => _buffs.Remaining(TemporaryBuff.CriticalDamage);
        public float DexCriticalDamageMultiplier => _buffs.DexCriticalMultiplier;
        public float ParryCriticalDamageMultiplier => _buffs.ParryCriticalMultiplier;

        public float GetCriticalDamageMultiplier(bool isParryStagger, bool isDexCrit)
        {
            return isParryStagger ? ParryCriticalDamageMultiplier : (isDexCrit ? DexCriticalDamageMultiplier : 1f);
        }

        public const float Skill2BuffMultiplier = TemporaryBuffs.IronWillAttackBonus; // +22% ATK
        public const float Skill2BuffDuration = TemporaryBuffs.IronWillDuration; // 15 seconds
        public void ApplySkill2Buff() => _buffs.Apply(TemporaryBuff.IronWill);
        public void RemoveSkill2Buff() => _buffs.Remove(TemporaryBuff.IronWill);
        public bool HasSkill2Buff => _buffs.IsActive(TemporaryBuff.IronWill);
        public float Skill2BuffRemaining => _buffs.Remaining(TemporaryBuff.IronWill);
        public void SetRegenAuraForTesting(bool active) => SetRegenAura(this, active);

        public void GetActiveBuffs(System.Collections.Generic.List<ActiveBuffInfo> list) =>
            _buffDisplay.Read(_buffs, HasRegenAura,
                _playerController != null && _playerController.CurrentState == PlayerState.Hurt, list);

        public static string FormatMinecraftTime(float seconds) => BuffDisplay.FormatTime(seconds);
        [CreateProperty]
        public float CriticalChance { get; private set; }
        [CreateProperty]
        public float Defense { get; private set; }
        [CreateProperty]
        public float AttackSpeedMultiplier { get; private set; } = 1.0f;
        // Permanent derived values include attribute upgrades, before temporary buffs.
        public float BaseMaxHP { get; private set; }
        public float BaseAttackSpeedMultiplier { get; private set; } = 1.0f;
        public float BaseSprintSpeed { get; private set; }
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
            float dashSpdPerAgi = _statsTemplate != null ? _statsTemplate.dashSpeedPerAGI : 0.05f;

            // STR -> ATK
            AttackPower = baseAtk + _strength * atkPerStr;

            // VIT -> Max HP and Max Stamina
            BaseMaxHP = _vitality * hpPerVit;
            float calculatedMaxHP = BaseMaxHP;
            calculatedMaxHP *= _buffs.FortitudeMultiplier;
            MaxHP = calculatedMaxHP;
            MaxStamina = baseStam + Mathf.Max(0, _vitality - baseVit) * stamPerVit;

            _dexterity = Mathf.Max(0, _dexterity);

            // DEX -> Linear Critical Chance: 200 DEX = 100% crit chance
            if (_statsTemplate != null)
            {
                CriticalChance = _statsTemplate.CalculateCritChance(_dexterity);
            }
            else
            {
                float crit = (_dexterity / 200f) * 100f;
                CriticalChance = Mathf.Clamp(crit, 0f, 100f);
            }

            // AGI -> Attack Speed, Movement Speed, Double Jump
            BaseAttackSpeedMultiplier = baseAtkSpd + Mathf.Max(0, _agility - baseAgi) * atkSpdPerAgi;
            float baseAtkSpdMultiplier = BaseAttackSpeedMultiplier;
            baseAtkSpdMultiplier *= _buffs.SpeedMultiplier;
            AttackSpeedMultiplier = baseAtkSpdMultiplier;

            // DEF -> Level-based Defense (DEF = baseDEF + Level * defPerLevel)
            float baseDef = _statsTemplate != null ? _statsTemplate.baseDEF : 0f;
            float defPLv = _statsTemplate != null ? _statsTemplate.defPerLevel : 1f;
            float calcDef = baseDef + _currentLevel * defPLv;
            calcDef *= _buffs.FortitudeMultiplier;
            Defense = calcDef;

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
                float speedMod = _buffs.SpeedMultiplier;
                _playerController.MoveSpeed = _playerController.BaseMoveSpeed * speedMod;
                float sprintRatio = _playerController.BaseMoveSpeed > 0f ? (_playerController.BaseSprintSpeed / _playerController.BaseMoveSpeed) : 1.625f;
                BaseSprintSpeed = _playerController.BaseSprintSpeed + (_agility - baseAgi) * (spdPerAgi * sprintRatio);
                _playerController.SprintSpeed = BaseSprintSpeed * speedMod;
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
            _currentEXP = (int)System.Math.Min(int.MaxValue, (long)_currentEXP + Mathf.Max(0, amount));
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
            AddLevel();
        }

        /// <summary>Grants one level with normal rewards, preserving accumulated EXP.</summary>
        public void AddLevel() => AddLevels(1);

        /// <summary>Grants levels with normal rewards, preserving accumulated EXP.</summary>
        public void AddLevels(int amount)
        {
            int levelsGained = Mathf.Min(Mathf.Max(0, amount), int.MaxValue - _currentLevel);
            if (levelsGained == 0) return;
            _currentLevel += levelsGained;
            int pointsGained = (_statsTemplate != null && _statsTemplate.statPointsPerLevel > 0)
                ? _statsTemplate.statPointsPerLevel
                : DefaultStatPointsPerLevel;
            _availableStatPoints = (int)System.Math.Min(int.MaxValue, (long)_availableStatPoints + (long)pointsGained * levelsGained);

            RecalculateStats();
            
            // Fully restore HP on Level Up
            _currentHP = MaxHP;

            Debug.Log($"<color=yellow>[PlayerStats] Level Up! New Level: {_currentLevel}, Available Stat Points: {_availableStatPoints}</color>");
        }

        /// <summary>
        /// Upgrades an attribute by a specified amount using available Stat Points.
        /// Returns the actual number of points allocated.
        /// </summary>
        public int UpgradeStatAmount(string statName, int count)
        {
            if (count <= 0 || _availableStatPoints <= 0)
            {
                Debug.LogWarning("[PlayerStats] Attempted to upgrade stat, but count <= 0 or no Stat Points available!");
                return 0;
            }

            int toAllocate = Mathf.Min(count, _availableStatPoints);
            int allocated = 0;

            switch (statName.ToUpper())
            {
                case "STR":
                case "STRENGTH":
                    _strength += toAllocate;
                    allocated = toAllocate;
                    break;
                case "VIT":
                case "VITALITY":
                    _vitality += toAllocate;
                    allocated = toAllocate;
                    break;
                case "DEX":
                case "DEXTERITY":
                    if (_dexterity >= MaxDexterity)
                    {
                        Debug.LogWarning($"[PlayerStats] Dexterity is already at maximum cap ({MaxDexterity})!");
                        return 0;
                    }
                    int canAdd = Mathf.Clamp(toAllocate, 0, Mathf.Max(0, MaxDexterity - _dexterity));
                    _dexterity += canAdd;
                    allocated = canAdd;
                    break;
                case "AGI":
                case "AGILITY":
                    _agility += toAllocate;
                    allocated = toAllocate;
                    break;
                default:
                    Debug.LogError($"[PlayerStats] Unknown stat upgrade requested: {statName}");
                    break;
            }

            if (allocated > 0)
            {
                _availableStatPoints -= allocated;
                RecalculateStats();
                Debug.Log($"[PlayerStats] Stat Modified: {statName.ToUpper()} upgraded +{allocated}. Remaining points: {_availableStatPoints}");
            }

            return allocated;
        }

        /// <summary>
        /// Upgrades an attribute using available Stat Points.
        /// </summary>
        public bool UpgradeStat(string statName)
        {
            return UpgradeStatAmount(statName, 1) > 0;
        }

        /// <summary>
        /// Applies incoming damage to Arthur.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_adminInvincible || HasUndyingBuff) return;
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
            if (AdminInvincible) return;
            TakeDamage(damage);
            if (isStun && !IsDead && !HasPurityBuff)
            {
                ApplyStun(stunDuration);
            }
        }

        public void ApplyStun(float duration = 1.0f)
        {
            if (HasPurityBuff) return;
            if (_playerController != null)
            {
                _playerController.ApplyStun(duration);
            }
        }

        public void ApplyStatus(StatusEffect effect, float duration)
        {
            if (HasPurityBuff) return;
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
            _strength = state.strength; _vitality = state.vitality; _dexterity = Mathf.Max(0, state.dexterity); _agility = state.agility;
            _gold = state.gold; _healingPotions = state.potions;
            RecalculateStats();
            _currentHP = Mathf.Clamp(state.hp, 0, MaxHP);
            _currentStamina = Mathf.Clamp(state.stamina, 0, MaxStamina);
            _lastDamageTime = -100f;

        }
    }
}
