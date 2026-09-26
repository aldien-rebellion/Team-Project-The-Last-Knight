# Monster System Technical Specification & Agent Guide [AI / LLM Edition]

> **Target Audience:** Autonomous Coding Agents, Subagents, and AI Assistants.  
> **Human Documentation:** For verbose design walkthroughs and Unity GUI instructions, see [`MONSTER_PREFABS_GUIDE_HUMAN.md`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/MONSTER_PREFABS_GUIDE_HUMAN.md).  
> **Repository:** `aldien-rebellion/Team-Project-The-Last-Knight`  
> **Runtime Environment:** Unity 2022.3+ / 6000.x (2D Action RPG, C# .NET Standard 2.1)

---

## 1. System Architecture & File Registry

```
Assets/
├── Animations/Enemies/
│   ├── {EnemyName}/
│   │   ├── {EnemyName}Controller.controller     # State machine with parameters & ActionIndex
│   │   └── *.anim                               # Raw animation clips
│   └── Projectiles/
│       └── {ProjName}/                          # Projectile/Spell animation controllers
├── Prefabs/Enemies/
│   ├── {EnemyName}.prefab                       # Complete root enemy prefabs (31 prefabs)
│   └── Projectiles/
│       └── {ProjName}.prefab                    # Direct projectiles & ground spell areas
├── Scripts/
│   ├── AI/
│   │   ├── EnemyAIState.cs                      # AI State Enum (Idle, Patrol, Chase, Melee, Ranged, Hurt, Dead, ReturningToSpawn, Skill)
│   │   └── EnemyController.cs                  # Main AI brain, skill execution, leash, and basic attack parry tracker
│   └── Combat/
│       ├── EnemyStats.cs                        # Stats, Level Scaling formulas, Level Difference penalties, and Revive logic
│       ├── EnemySkill.cs                        # Serializable skill data contract
│       ├── EnemyHitbox2D.cs                     # Dynamic damage applicator (reads owner.CurrentAttackDamage)
│       ├── ParryReceiver.cs                     # Parry timing ring, windup state, perfect window detection
│       └── Projectiles/
│           ├── EnemyProjectile.cs               # Linear projectile movement & damage delivery
│           └── GroundSpellArea.cs               # Area-of-effect ground spell with delay and damage lifetime
├── Editor/
│   └── EnemyPrefabBuilder.cs                    # Automated prefab generator and parameter configurator
└── Test/Editor/
    └── CombatFlowTests.cs                       # NUnit EditMode tests (uses reflection due to AnimationTests.asmdef boundary)
```

---

## 2. Prefab GameObject Structure & Component Schema

Every monster prefab in `Assets/Prefabs/Enemies/{EnemyName}.prefab` strictly follows this 3-tier hierarchy:

```
[GameObject: Root: {EnemyName}]
 ├── SpriteRenderer              (Layer: Default, Sorting Layer: Default, Order: 2)
 ├── Animator                    (RuntimeAnimatorController: Assets/Animations/Enemies/{EnemyName}/...)
 ├── Rigidbody2D                 (BodyType: Dynamic, Collision: Continuous, FreezeRotation: Z, Gravity: 2.5 or 0 for flying)
 ├── CapsuleCollider2D           (Solid physics collider against ground and obstacles)
 ├── EnemyStats                  (Combat stats, Level scaling, Revive, HP tracking)
 ├── EnemyController             (AI State machine, skills, basic attack, leash, respawn)
 ├── ParryReceiver               (Dynamic LineRenderer timing rings, windup window, stagger tracker)
 │
 ├── [Child GameObject: HealthCanvas]
 │    ├── Canvas                 (RenderMode: WorldSpace, Scale: 0.01, 0.01, 1.0)
 │    ├── FloatingHealthBar      (Controller script)
 │    ├── [Child: Background]    (Image, Black, alpha 0.75)
 │    └── [Child: Fill]          (Image, Red, ImageType: Filled, Horizontal)
 │
 └── [Child GameObject: Hitbox]
      ├── BoxCollider2D          (isTrigger: true, Layer: Default/Combat)
      └── EnemyHitbox2D          (Damage trigger, queries owner.CurrentAttackDamage)
```

---

## 3. Mathematical Formulas & Stat Scaling Contracts

### 3.1 Level Scaling Formulas (Active when `_useLevelScaling == true`)
$$\text{MaxHP} = \text{Level} \times 100$$
$$\text{ATK} = \text{Level} \times 10$$
$$\text{DEF} = \text{Level} \times 1$$
$$\text{EXP} = \text{UniformRandom}(0.20, 0.30) \times (\text{Level} \times 100)$$
$$\text{Gold} = \text{UniformRandom}(0.20, 0.30) \times (\text{Level} \times 100)$$

* **Reference Lookup Values:**

| Level | Max HP | ATK | DEF | EXP Yield | Gold Yield |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **1** | 100 | 10 | 1 | 20 – 30 | 20 – 30 |
| **5** | 500 | 50 | 5 | 100 – 150 | 100 – 150 |
| **10** | 1,000 | 100 | 10 | 200 – 300 | 200 – 300 |
| **20** | 2,000 | 200 | 20 | 400 – 600 | 400 – 600 |
| **50** | 5,000 | 500 | 50 | 1,000 – 1,500 | 1,000 – 1,500 |
| **100** | 10,000 | 1,000 | 100 | 2,000 – 3,000 | 2,000 – 3,000 |

### 3.2 Level Difference Penalty System
Implemented in [`EnemyStats.CalculateIncomingDamage()`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Scripts/Combat/EnemyStats.cs#L88) and [`EnemyStats.Die()`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Scripts/Combat/EnemyStats.cs#L173):

```csharp
// 1. Monster Level Higher than Player (Damage Reduction on Monster)
int diff = _level - playerLevel;
float multiplier = 1.0f;
if (diff >= 20) multiplier = 0.60f;      // -40% damage penalty
else if (diff >= 15) multiplier = 0.70f; // -30% damage penalty
else if (diff >= 10) multiplier = 0.80f; // -20% damage penalty
else if (diff >= 5)  multiplier = 0.90f; // -10% damage penalty

// 2. Player Level Higher than Monster (EXP Penalty to prevent power-leveling)
int playerAdvantage = playerLevel - _level;
if (playerAdvantage >= 10) expAwarded *= 0.80f; // -20% EXP penalty
else if (playerAdvantage >= 5) expAwarded *= 0.90f; // -10% EXP penalty
```

---

## 4. Combat, Attack & Parry State Machine

### 4.1 Damage Dispatch Pipeline
```
[EnemyController] ──> _currentAttackMultiplier (1.0x for Basic, skill.damageMultiplier for Skills)
         │
         ├──> EnemyHitbox2D.TryDealDamage() ──> reads owner.CurrentAttackDamage (_stats.AttackPower * _currentAttackMultiplier)
         ├──> EnemyController.SpawnProjectile() ──> passes (_stats.AttackPower * _currentAttackMultiplier) to EnemyProjectile
         └──> GroundSpellArea.Initialize() ──> receives (_stats.AttackPower * _currentAttackMultiplier)
```

### 4.2 Parry Rules & State Transitions
1. **Monsters with Skills (`HasParryableSkill() == true`):**
   * Must define $\ge 1$ entry in `_skills` with `isParryable = true`.
   * When casting a parryable skill:
     * Calls `_parry.BeginWindup()`.
     * `ParryReceiver` renders yellow contracting rings (`Progress` from $1.2\text{m} \to 0.35\text{m}$).
     * Windup duration: $0.60\text{s}$ (`WindupDuration`).
     * Perfect window: $[0.30\text{s}, 0.70\text{s}]$ ($\text{WindupDuration} - \text{PerfectWindow} - \text{Tolerance}$ to $\text{WindupDuration} + \text{Tolerance}$).
     * If player hits attack in perfect window: `_parry.TryParry()` returns `true`, cancels attack (`CancelAttack()`), staggers enemy for $1.5\text{s}$, applies `StatusEffect.Stunned`, triggers audio `"parry"`, displays floating text `"PARRY"`, and grants player invulnerability + guaranteed critical damage.
   * Basic Attack for monsters with parryable skills: `canParryThisTime = false` (no parry ring).
2. **Monsters without Skills (`HasParryableSkill() == false`):**
   * Displays the Parry Timing Ring on **Basic Attack**.
   * **Enforces Cooldown:** `_basicParryCooldown >= 4.0f`.
   * Attack 1 ($t = 0$): `Time.time >= _nextBasicParryTime` $\implies$ Parry windup triggers, sets `_nextBasicParryTime = Time.time + 4.0f`.
   * Subsequent Attacks within 4.0s: `canParryThisTime = false` $\implies$ 0.15s anticipation, normal unparried strike without timing ring.

---

## 5. Leash (Patrol) & Respawn Subsystems

### 5.1 Leash & HP Reset Contract
* **Field:** `_isBoss` (bool).
* **Boss Monsters (`_isBoss == true`):** Leash logic is completely bypassed. Boss never resets HP or returns to spawn due to distance.
* **Non-Boss Monsters (`_isBoss == false`):**
  * Boundary: $\text{LeashDistance} = \text{DetectionRange} \times \text{LeashRangeMultiplier}$ (Default multiplier: $2.0$).
  * Trigger: `Vector2.Distance(player.position, spawnPosition) > LeashDistance` OR `Vector2.Distance(monster.position, spawnPosition) > LeashDistance`.
  * Action: State switches to `EnemyAIState.ReturningToSpawn`. Ignores player, moves towards `_spawnPosition`.
  * Arrival ($\le 0.4\text{m}$): Heals to 100% Max HP (`_stats.Heal(_stats.MaxHealth)`), displays green `"Full HP"` text, transitions back to `Patrol`.

### 5.2 Respawn Lifecycle Contract
* **Fields:** `_canRespawn` (bool, default `false`), `_respawnTime` (float, default $30.0\text{s}$).
* **Execution Sequence:**
  1. On death: Disables colliders, sets `_currentState = EnemyAIState.Dead`.
  2. Waits `_deathDestroyDelay` ($1.5\text{s}$) for death animation.
  3. Hides visual renderers and floating UI canvas (`SetVisibility(false)`).
  4. Waits `_respawnTime` seconds.
  5. Distance Guard: `while (Vector2.Distance(player.position, _spawnPosition) <= _detectionRange) yield return new WaitForSeconds(0.5f);`
  6. Resets transform, physics velocity, colliders, re-enables visuals, calls `_stats.Revive()`, plays `"Idle"`.

---

## 6. Complete 30-Monster Master Specification Registry

This machine-readable registry details all 30 monsters, their exact animation states, skill payloads, and parry configurations:

```json
{
  "monsters": [
    {
      "name": "ForestMushroom",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "AttackWithStun", "anim": "AttackWithStun", "actionIndex": 1, "multiplier": 1.5, "cooldown": 6.0, "minRange": 0.0, "maxRange": 1.6, "isParryable": true }
      ]
    },
    {
      "name": "Goblin",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.4 },
      "skills": [
        { "name": "GoblinBomb", "anim": "Attack3", "actionIndex": -1, "multiplier": 1.6, "cooldown": 5.0, "minRange": 2.5, "maxRange": 7.0, "isParryable": true, "projectile": "Goblin_Bomb" }
      ]
    },
    {
      "name": "Skeleton",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "SwordThrow", "anim": "Attack3", "actionIndex": -1, "multiplier": 1.4, "cooldown": 4.5, "minRange": 2.5, "maxRange": 6.5, "isParryable": true, "projectile": "Skeleton_Sword" },
        { "name": "ShieldGuard", "anim": "Shield", "actionIndex": -1, "multiplier": 0.5, "cooldown": 8.0, "minRange": 0.0, "maxRange": 2.0, "isParryable": false }
      ]
    },
    {
      "name": "FlyingEye",
      "isBoss": false,
      "isFlying": true,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.3 },
      "skills": [
        { "name": "EyeBeam", "anim": "Attack3", "actionIndex": -1, "multiplier": 1.5, "cooldown": 5.0, "minRange": 2.5, "maxRange": 7.5, "isParryable": true, "projectile": "FlyingEye_Projectile" }
      ]
    },
    {
      "name": "FantasyMushroom",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "SporeShot", "anim": "Attack3", "actionIndex": -1, "multiplier": 1.4, "cooldown": 5.0, "minRange": 2.5, "maxRange": 7.0, "isParryable": true, "projectile": "FantasyMushroom_Projectile" }
      ]
    },
    {
      "name": "UndeadExecutioner",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 2.0 },
      "skills": [
        { "name": "SpinningCleave", "anim": "Skill1", "actionIndex": 3, "multiplier": 2.0, "cooldown": 7.0, "minRange": 0.0, "maxRange": 2.5, "isParryable": true },
        { "name": "DarkSummon", "anim": "Summon", "actionIndex": 4, "multiplier": 1.5, "cooldown": 15.0, "minRange": 0.0, "maxRange": 4.0, "isParryable": false }
      ]
    },
    {
      "name": "MoonstoneKeeper",
      "isBoss": false,
      "basicAttack": { "animState": "Attack1", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "GroundSlam", "anim": "Attack2", "actionIndex": -1, "multiplier": 1.8, "cooldown": 6.0, "minRange": 0.0, "maxRange": 2.2, "isParryable": true },
        { "name": "DashThrust", "anim": "Dash", "actionIndex": -1, "multiplier": 1.3, "cooldown": 5.0, "minRange": 3.0, "maxRange": 6.5, "isParryable": false }
      ]
    },
    {
      "name": "MechaStoneGolem",
      "isBoss": false,
      "basicAttack": { "animState": "Melee", "multiplier": 1.0, "cooldown": 2.0 },
      "skills": [
        { "name": "RocketPunch", "anim": "Shoot", "actionIndex": -1, "multiplier": 1.5, "cooldown": 5.0, "minRange": 2.5, "maxRange": 8.0, "isParryable": true, "projectile": "Golem_ArmProjectile" },
        { "name": "LaserBeam", "anim": "LaserCast", "actionIndex": -1, "multiplier": 2.5, "cooldown": 10.0, "minRange": 2.5, "maxRange": 9.0, "isParryable": false, "projectile": "Golem_Laser" },
        { "name": "StoneShield", "anim": "ShieldCast", "actionIndex": -1, "multiplier": 0.5, "cooldown": 12.0, "minRange": 0.0, "maxRange": 3.0, "isParryable": false }
      ]
    },
    {
      "name": "BlueSlime",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.2 },
      "skills": [
        { "name": "HeavySlam", "anim": "Attack_3", "actionIndex": -1, "multiplier": 1.6, "cooldown": 6.0, "minRange": 0.0, "maxRange": 2.0, "isParryable": true },
        { "name": "DoubleHop", "anim": "Attack_2", "actionIndex": -1, "multiplier": 1.3, "cooldown": 3.5, "minRange": 0.0, "maxRange": 1.8, "isParryable": false },
        { "name": "SlideTackle", "anim": "Run+Attack", "actionIndex": -1, "multiplier": 1.2, "cooldown": 5.0, "minRange": 2.5, "maxRange": 5.5, "isParryable": false }
      ]
    },
    {
      "name": "Satyr",
      "isBoss": false,
      "basicAttack": { "animState": "Attack1", "multiplier": 1.0, "cooldown": 1.4 },
      "skills": [
        { "name": "Dropkick", "anim": "Skill1", "actionIndex": -1, "multiplier": 1.8, "cooldown": 7.0, "minRange": 0.0, "maxRange": 2.2, "isParryable": true },
        { "name": "Slash2", "anim": "Attack2", "actionIndex": -1, "multiplier": 1.4, "cooldown": 4.0, "minRange": 0.0, "maxRange": 1.8, "isParryable": false },
        { "name": "NatureCast", "anim": "Cast", "actionIndex": -1, "multiplier": 2.0, "cooldown": 9.0, "minRange": 2.5, "maxRange": 7.0, "isParryable": false }
      ]
    },
    {
      "name": "Necromancer",
      "isBoss": false,
      "basicAttack": { "animState": "Attack1", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "SoulBlast", "anim": "Attack3", "actionIndex": -1, "multiplier": 2.2, "cooldown": 9.0, "minRange": 1.5, "maxRange": 7.0, "isParryable": true },
        { "name": "DarkWave", "anim": "Attack2", "actionIndex": -1, "multiplier": 1.6, "cooldown": 5.0, "minRange": 2.0, "maxRange": 7.5, "isParryable": false }
      ]
    },
    {
      "name": "SkeletonKnight",
      "isBoss": false,
      "basicAttack": { "animState": "FwdSwing", "multiplier": 1.0, "cooldown": 1.6 },
      "skills": [
        { "name": "DownSwing", "anim": "DownSwing", "actionIndex": -1, "multiplier": 1.6, "cooldown": 5.0, "minRange": 0.0, "maxRange": 2.0, "isParryable": true },
        { "name": "SideSwing", "anim": "SideSwing", "actionIndex": -1, "multiplier": 1.3, "cooldown": 4.0, "minRange": 0.0, "maxRange": 1.8, "isParryable": false },
        { "name": "FullCombo", "anim": "FullCombo", "actionIndex": -1, "multiplier": 2.2, "cooldown": 9.0, "minRange": 0.0, "maxRange": 2.2, "isParryable": false }
      ]
    },
    {
      "name": "DemonBoss",
      "isBoss": true,
      "basicAttack": { "animState": "Attack_01", "multiplier": 1.0, "cooldown": 1.8 },
      "skills": [
        { "name": "DualClawCleave", "anim": "Attack_02", "actionIndex": -1, "multiplier": 1.8, "cooldown": 5.5, "minRange": 0.0, "maxRange": 3.2, "isParryable": true },
        { "name": "EarthquakeJump", "anim": "Jump", "actionIndex": -1, "multiplier": 1.6, "cooldown": 8.0, "minRange": 3.5, "maxRange": 8.0, "isParryable": false },
        { "name": "TerrifyingRoar", "anim": "Shout", "actionIndex": -1, "multiplier": 0.8, "cooldown": 12.0, "minRange": 0.0, "maxRange": 5.0, "isParryable": false }
      ]
    },
    {
      "name": "BringerOfDeath",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.8 },
      "skills": [
        { "name": "HellfirePillar", "anim": "Cast", "actionIndex": -1, "multiplier": 2.0, "cooldown": 8.0, "minRange": 1.5, "maxRange": 8.0, "isParryable": true, "groundSpell": "BringerOfDeath_Spell" }
      ]
    },
    {
      "name": "Fox",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.3 },
      "skills": [
        { "name": "WarpAmbush", "anim": "Disappear", "actionIndex": -1, "multiplier": 1.5, "cooldown": 6.0, "minRange": 2.5, "maxRange": 6.5, "isParryable": true }
      ]
    },
    {
      "name": "Dragon",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.6 },
      "skills": [
        { "name": "FireBall", "anim": "Attack", "actionIndex": -1, "multiplier": 1.5, "cooldown": 4.5, "minRange": 2.5, "maxRange": 8.0, "isParryable": false, "projectile": "Dragon_FireBall" }
      ]
    },
    {
      "name": "FireWorm",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.4 },
      "skills": [
        { "name": "FireBall", "anim": "Attack", "actionIndex": -1, "multiplier": 1.4, "cooldown": 4.0, "minRange": 2.5, "maxRange": 7.0, "isParryable": false, "projectile": "FireWorm_FireBall" }
      ]
    },
    {
      "name": "Jinn",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.5 },
      "skills": [
        { "name": "MagicCast", "anim": "Attack", "actionIndex": -1, "multiplier": 1.8, "cooldown": 6.0, "minRange": 1.5, "maxRange": 7.0, "isParryable": false, "groundSpell": "Jinn_Magic" }
      ]
    },
    {
      "name": "Small_dragon",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.3 },
      "skills": [
        { "name": "SmallFireBall", "anim": "Attack", "actionIndex": -1, "multiplier": 1.3, "cooldown": 4.0, "minRange": 2.5, "maxRange": 7.0, "isParryable": false, "projectile": "SmallDragon_FireBall" }
      ]
    },
    {
      "name": "ArchDemon",
      "isBoss": false,
      "basicAttack": { "animState": "BasicAtk", "multiplier": 1.0, "cooldown": 1.8, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Demon",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.5, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "DemonKin",
      "isBoss": false,
      "basicAttack": { "animState": "BasicAtk", "multiplier": 1.0, "cooldown": 1.4, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Lizard",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.4, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Minotaur_1",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.8, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Minotaur_2",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.8, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Minotaur_3",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.8, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Reaper",
      "isBoss": false,
      "basicAttack": { "animState": "HostileAttack", "multiplier": 1.0, "cooldown": 1.6, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "ShadowDemonDragon",
      "isBoss": true,
      "basicAttack": { "animState": "Attack_Left", "multiplier": 1.0, "cooldown": 2.0, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Skullwolf",
      "isBoss": false,
      "basicAttack": { "animState": "Attack", "multiplier": 1.0, "cooldown": 1.2, "basicParryCooldown": 4.0 },
      "skills": []
    },
    {
      "name": "Fairy",
      "isBoss": false,
      "isFlying": true,
      "basicAttack": { "animState": "Fly_1", "multiplier": 0.0, "cooldown": 5.0 },
      "skills": []
    },
    {
      "name": "Trader_1",
      "isBoss": false,
      "isNPC": true,
      "basicAttack": { "animState": "Idle", "multiplier": 0.0, "cooldown": 999.0 },
      "skills": []
    }
  ]
}
```

---

## 7. C# Public API Surface

### 7.1 `TheLastKnight.Combat.EnemyStats`
```csharp
public int Level => _level;
public void SetLevel(int level, bool autoRecalculate = true);
public void ApplyLevelScaling();
public void SetStats(float maxHp, float def, float atk);
public void Heal(float amount);
public void Revive();
public float CalculateIncomingDamage(DamageData damageData);
public event Action<DamageData> OnDamaged;
public event Action OnDeath;
```

### 7.2 `TheLastKnight.AI.EnemyController`
```csharp
public EnemyAIState CurrentState => _currentState;
public bool IsBoss => _isBoss;
public bool CanRespawn => _canRespawn;
public float RespawnTime => _respawnTime;
public bool CanDealMeleeDamage => !_stats.IsDead && !_parry.IsStaggered && Time.time < _damageUntil;
public float CurrentAttackDamage => _stats != null ? _stats.AttackPower * _currentAttackMultiplier : 10f;
public float CurrentAttackMultiplier => _currentAttackMultiplier;
public TheLastKnight.Combat.EnemySkill[] Skills => _skills;

public void SetSkills(TheLastKnight.Combat.EnemySkill[] skills);
public void SetBasicAttackConfiguration(string animState, float multiplier, bool canParry, float parryCooldown);
public void SetBoss(bool isBoss);
public void SetSpawnPosition(Vector3 position);
public void CancelAttack();
public bool HasParryableSkill();
public TheLastKnight.Combat.EnemySkill GetReadySkill(float distToPlayer);
```

### 7.3 `TheLastKnight.Combat.EnemySkill`
```csharp
public string skillName;
public string animationName;
public int actionIndex;
public float damageMultiplier;
public float cooldown;
public float minRange;
public float maxRange;
public bool isParryable;
public GameObject projectilePrefab;
public GameObject groundSpellPrefab;
public float nextReadyTime;

public bool IsReady(float distance, float currentTime);
```

---

## 8. Verification & Automation Recipes

### 8.1 Rebuilding All Prefabs via Code Execution
Execute this snippet in Unity via `execute_code` whenever animation states or default multipliers are updated:
```csharp
TheLastKnight.EditorTools.EnemyPrefabBuilder.BuildAll();
```

### 8.2 Running Automated EditMode Tests
Execute via `run_tests`:
```json
{
  "mode": "EditMode"
}
```
* **Expected Test Count:** 141 tests (140 passing, 1 pre-existing Camera boundary test).
* **Test Assembly Boundary Rule:** Tests in `Assets/Test/Editor/CombatFlowTests.cs` run inside `AnimationTests.asmdef`. This asmdef has no runtime assembly references. Always use reflection via `RuntimeType("TheLastKnight.Namespace.ClassName")` when authoring tests in this assembly.
