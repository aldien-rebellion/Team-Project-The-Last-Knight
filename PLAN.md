# THE LAST KNIGHT - AGENT EXECUTION PLAN
**Project:** The Last Knight | **Target Build:** Windows x64 Standalone (.exe) | **Deadline:** 2026-09-30 18:00 +07:00  
**Engine:** Unity 6 / URP 2D | **Human Spec:** `PLAN_HUMAN.md`

---

## 0. AGENT EXECUTION RULES
1. **Branch Rule:** `feature/<name>`, `art/<name>`, `bugfix/<name>`.
2. **Workflow:** Branch from `main` -> Implement -> Verify (Console 0 errors + DoD) -> Commit & Push -> Merge `main`.
3. **Persistence Architecture:** Single `GameManager` (`DontDestroyOnLoad`) holds Player state (HP, STM, Level, EXP, Gold, Potions, Runes[4], SavePoint). Do not store persistent data in isolated scenes.
4. **Input Conventions (New Input System):**
   - Move: A/D | Jump: Space | Dash: Right Click / Shift | Attack/Parry: Left Click
   - Skills: E (Carnage Burst), R (Buff), T (Excalibur)
   - Quick Potion: Q | Interact/Save: F
5. **HUMAN TEAMMATE ASSIGNMENTS (DO NOT EXECUTE BY AI AGENT):**
   - `[TASK M4.1A]` (Audio Sourcing & `CREDITS.md`) is assigned to **Human Teammate 1**. AI Agent MUST NOT execute.
   - `[TASK M5.2]` (`MainMenu.unity` Scene & UI) is assigned to **Human Teammate 2**. AI Agent MUST NOT execute.
   - **NOTE:** AI Agent STILL executes `[TASK M4.1B]` (`AudioManager.cs` coding, BGM/SFX audio playback integration across Player, Enemy, and UI).

---

## 1. WORLD & RUNE MATRIX
```
[Church] -------------------> [CityCenter] <---> [OutdoorMarket] <---> [SuburbToForest] <---> [DemonCastleEntrance] ---> [DemonCastle]
(Boss: MoonstoneKeeper)       (Stage 1 Start)     (Stage 2)             (Stage 3)              (Stage 4 - VN Scene)       (Stage 5 - Final)
- Key dropped by boss         - Medusa Statue     - The Shadow Market   - Mob: Fox             - Arthur facing away       - Boss: Custom Drawn
- 2F Chest: Rune 1 (Pentagram)  Save -> Rune 2    - Shop: Rune 4 (150g) - On-Death: Rune 3     - No input controls        - Victory Cutscene
                                (Demon Hand)      - Boss: DemonBoss     (Evil Eye)             - Drag 4 Runes into gate
```

---

## 2. BLINDSPOT MITIGATION MATRIX
| ID | Blindspot | Mitigation Specification |
|---|---|---|
| **B1** | Scene Data Loss | `GameManager.cs` (`DontDestroyOnLoad`) serializes runtime state between scene unloads. |
| **B2** | Physics/Layer Desync | Layer Matrix: `Player` hits `Enemy`, `PlayerAttack` triggers `IDamageable`, ignores `Player`. Set in `Physics2DSettings.asset`. |
| **B3** | Frame-rate Parry Latency | Normalized progress `(Time.time - startTime) / duration` with `±0.1s` tolerance window. Independent of framerate. |
| **B4** | Boss Arena Escape | Trigger boundary locks arena doorways with red barrier gameobjects until `Boss.OnDeath` fires. |
| **B5** | Save File Corruption | Atomic JSON write to `Application.persistentDataPath/save.json` with `.tmp` swap. |
| **B6** | Audio Academic Licensing | Only CC0/Public Domain/Royalty-free clips. All source URLs logged to `Assets/Audio/CREDITS.md`. |

---

## 3. SPRINT MILESTONES & TASKS

### [MILESTONE 0] Baseline Git Cleanup
- **Deadline:** 2026-09-19 23:59
- **Branch:** `bugfix/cleanup-layer-and-prefabs`
- **Scope:** Stage modified files from `fixbug/layer` (`Assets/Prefabs/`, `Scenes/Maps/`, `Physics2DSettings.asset`), commit, push, and merge into `main`.
- **DoD:** `git status` clean on `main`. All scenes load without missing script/prefab warnings.

---

### [MILESTONE 1] Core Combat, Parry & Stamina
- **Deadline:** 2026-09-21 23:59

#### [TASK M1.1] Player Attack Hitbox & Damage Flow
- **Branch:** `feature/player-attack-hitbox`
- **Target Files:** `Assets/Scripts/Player/PlayerController.cs`, `Assets/Scripts/Combat/EnemyStats.cs`, `IDamageable.cs`
- **Spec:**
  - Attach attack collider / 2D overlap check during Arthur's `Attacking` state.
  - Calculate damage = `PlayerStats.AttackPower` with DEX-based crit roll (`PlayerStats.CriticalChance`).
  - Call `IDamageable.TakeDamage(DamageData)` on hit target. Instantiate floating text prefab.
- **DoD:** Left click damages enemies. Enemy HP decreases. Enemy dies and plays death anim when `HP <= 0`.

#### [TASK M1.2] Timed Circle Parry System
- **Branch:** `feature/parry-system`
- **Target Files:** `Assets/Scripts/Combat/ParryReceiver.cs`, `EnemyController.cs`, `PlayerController.cs`
- **Spec:**
  - Enemy wind-up spawns shrinking yellow circle indicator (`duration = 0.6s`).
  - Perfect window = last `0.2s` when circle aligns with target ring.
  - Left click attack during window triggers: enemy attack cancelled, enemy `Stagger` state (1.5s), Arthur invulnerable during parry impact.
  - Attacks against staggered enemies deal 100% Critical Damage.
  - If `GameDifficulty == Hard`: hide circle indicator UI.
- **DoD:** Timing attack cancels enemy attack, plays metallic clash SFX, enemy staggers 1.5s.

#### [TASK M1.3] Action Stamina & Medusa Statue Buff
- **Branch:** `feature/stamina-system`
- **Target Files:** `Assets/Scripts/Stats/PlayerStats.cs`, `Assets/Scripts/Player/PlayerController.cs`, `Assets/Scripts/UI/HUDController.cs`
- **Spec:**
  - `MaxStamina = 100`. Action costs: Sprint = 15/s, Attack = 15, Dash = 20, Skill E = 25, Skill T = 50.
  - Passive regen = 20 STM/s when idle/walking.
  - Medusa Statue Aura (`Collider2D.isTrigger` radius = 4m): doubles regen to 40 STM/s.
  - In `Hard` mode: regen rate halved.
  - HUDController: bind green Stamina fill bar below HP bar.
- **DoD:** Cannot dash or use Skill E/T when STM insufficient. HUD bar scales accurately. Standing near Medusa rapidly refills STM.

---

### [MILESTONE 2] Potions, Shop & Economy
- **Deadline:** 2026-09-23 23:59

#### [TASK M2.1] Monster Loot Drops (Gold & EXP)
- **Branch:** `feature/monster-loot-economy`
- **Target Files:** `Assets/Scripts/Combat/EnemyStats.cs`, `PlayerStats.cs`
- **Spec:**
  - On enemy death: drop floating pickup or directly credit `+EXP` and `+Gold` (e.g. Slime: +15 EXP, +8 Gold; Bosses: scaled).
  - Add `Gold` property to `PlayerStats` with UI HUD binding.
- **DoD:** Killing enemy increments Player Gold and EXP. Visual popup shows `+X Gold / +Y EXP`.

#### [TASK M2.2] Quick-Slot Potion Drink (Key Q)
- **Branch:** `feature/quickslot-potion`
- **Target Files:** `Assets/Scripts/Player/PlayerController.cs`, `PlayerStats.cs`, `HUDController.cs`
- **Spec:**
  - `HealingPotions` count (start: 3, max: 5).
  - Key Q triggers `PlayerState.Drinking` (2.5s duration using existing sprites `player-drink1..8`).
  - Heals 50 HP on completion. Interruptible if hit.
- **DoD:** Pressing Q plays drink anim, increments HP by 50, decrements potion count by 1.

#### [TASK M2.3] The Shadow Market Shop & Rune 4
- **Branch:** `feature/shadow-market-shop`
- **Target Files:** `Assets/Scripts/Environment/ShadowMarketNPC.cs`, `ShopUI.cs`, `PlayerStats.cs`
- **Spec:**
  - NPC in `OutdoorMarket`. Press F in range opens Shop UI modal.
  - Catalog:
    - `Rune of the Trident (Rune 4)`: 150 Gold (One-time purchase; adds to `DemonRuneManager`).
    - `Medium Healing Potion`: 50 Gold (+1 potion stack).
    - `Stat Potions (STR, VIT, DEX, AGI)`: 100 Gold each (+1 stat point allocated immediately).
- **DoD:** Player can open shop, purchase items with Gold, purchase Rune 4, and close UI cleanly.

---

### [MILESTONE 3] Boss Placements, 4 Runes & VN Castle Entrance
- **Deadline:** 2026-09-25 23:59

#### [TASK M3.1] Map Bosses & Enemy Setup
- **Branch:** `feature/map-bosses-placement`
- **Target Files:** `Assets/Scenes/Maps/`, `Assets/Prefabs/Enemies/`
- **Spec:**
  - `Church`: Spawn `MoonstoneKeeper.prefab` (drops `ChurchKey`). Add 2F chest interactable.
  - `CityCenter`: Basic mobs (`BlueSlime`, `Skeleton`) + Medusa Statue.
  - `OutdoorMarket`: Spawn `DemonBoss.prefab` + Trader NPC.
  - `SuburbToForest`: Spawn `Fox.prefab` (on death drops Rune 3) + `FireWorm`.
  - `DemonCastle`: Arena with 1 Custom Drawn Boss (`DemonBoss/บอสสัตว์ประหลาด`).
- **DoD:** All 5 maps contain their assigned enemies and bosses with functional AI.

#### [TASK M3.2] 4 Demon Runes Acquisition Flow
- **Branch:** `feature/demon-runes-progression`
- **Target Files:** `Assets/Scripts/Environment/DemonRuneManager.cs`, `RunePickup.cs`
- **Spec:**
  - Rune 1 (Pentagram): Church 2F chest -> requires `ChurchKey` from MoonstoneKeeper.
  - Rune 2 (Demon Hand): CityCenter Medusa Statue -> given upon first F interact/rest.
  - Rune 3 (Evil Eye): SuburbToForest Fox mob -> auto-dropped on death.
  - Rune 4 (Trident): OutdoorMarket Shadow Market -> purchased for 150 Gold.
- **DoD:** Collecting each source sets corresponding index in `DemonRuneManager._collectedRunes[0..3]`.

#### [TASK M3.3] Demon Castle Entrance VN Interactive Scene
- **Branch:** `feature/demon-castle-gate-vn`
- **Target Files:** `Assets/Scenes/Maps/DemonCastleEntrance.unity`, `DemonCastleGateVN.cs`
- **Spec:**
  - VN Scene composition:
    - Arthur sprite placed bottom-center **facing away from camera** (`player-turn.png` / back view).
    - Player movement script disabled (`_inputHandler` disabled).
    - Dialogue Box explaining the 4 rune seal.
    - Choice Button 1: "Return to Suburb to Forest" -> loads `SuburbToForest.unity`.
    - Choice Button 2 / Rune Tray: Opens rune bag; player can drag & drop or click runes into 4 pillar sockets.
    - When all 4 runes socketed: screen shake, pillars illuminate, gate opens, loads `DemonCastle.unity`.
- **DoD:** Arthur is motionless facing the gate. Can return to forest. Can insert 4 runes to open gate.

---

### [MILESTONE 4] Audio, Medusa Save & Story Cutscenes
- **Deadline:** 2026-09-27 23:59

#### [TASK M4.1A] Audio Sourcing & Licensing Credits
<!-- [ASSIGNED TO HUMAN TEAMMATE 1 - AI AGENT DO NOT EXECUTE] -->
- **Assignee:** Human Teammate 1
- **Branch:** `art/audio-assets-and-credits`
- **Target Folder:** `Assets/Audio/SFX/`, `Assets/Audio/BGM/`, `Assets/Audio/CREDITS.md`
- **Spec:**
  - Search and download CC0 / Royalty-Free SFX & 5 BGMs from Freesound, Itch.io, Kenney.
  - Categories:
    - Player SFX: slash, hit, jump, land, dash, hurt, death, drink, skill burst, excalibur beam.
    - Enemy SFX: slime jump/hit, monster roar, death.
    - UI SFX: click, parry chime, rune pickup, game over.
    - 5 BGMs: Town, Forest, Church, Castle, Boss.
  - Create `Assets/Audio/CREDITS.md` recording filename, creator, download URL, and license type.
- **DoD:** All audio files placed in `Assets/Audio/` with valid `CREDITS.md`.

#### [TASK M4.1B] AudioManager Script & SFX Sound Integration
<!-- [AI AGENT TASK - ACTIVE] -->
- **Assignee:** AI Agent
- **Branch:** `feature/audio-manager-integration`
- **Target Files:** `Assets/Scripts/Audio/AudioManager.cs`, `PlayerController.cs`, `EnemyStats.cs`, `HUDController.cs`
- **Spec:**
  - Create `AudioManager.cs` Singleton (`DontDestroyOnLoad`) managing Master, BGM, and SFX AudioSource channels.
  - Implement BGM cross-fade on scene transitions.
  - Wire up SFX triggers in code: Player (sword attack, jump, dash, drink, skill), Enemy (hurt, death), UI (parry clash, clicks).
- **DoD:** `AudioManager.cs` operational with zero console errors. Sound triggers play audio when clips exist.

#### [TASK M4.2] Medusa Statue Save System & Respawn
- **Branch:** `feature/medusa-save-respawn`
- **Target Files:** `Assets/Scripts/Environment/MedusaSavePoint.cs`, `SaveSystem.cs`, `PlayerStats.cs`
- **Spec:**
  - Press F at Medusa Statue: full heals HP/STM, saves JSON file (scene, coords, stats, level, runes, gold, potions).
  - Player death: freeze input, show "YOU DIED" screen.
    - `Respawn`: reloads scene at last Medusa Statue with saved data.
    - `Exit`: returns to Main Menu.
- **DoD:** Game state saves on F interact; death displays YOU DIED; respawning restores state accurately.

#### [TASK M4.3] Story Cutscenes & Narrative Dialogue Box
- **Branch:** `feature/story-dialogue-system`
- **Target Files:** `Assets/Scripts/UI/StoryDialogueUI.cs`
- **Spec:**
  - Intro Cutscene (CityCenter on new game): narrative text about fallen Moa kingdom and Arthur's redemption. Skippable with Space/Click.
  - Victory Cutscene (Demon Castle after final boss death): Arthur looks over rebuilt kingdom, roll credits.
- **DoD:** Starting new game plays intro dialog; defeating final boss triggers ending cutscene.

---

### [MILESTONE 5] Difficulty Modes & Main Menu
- **Deadline:** 2026-09-29 23:59

#### [TASK M5.1] Difficulty Multiplier System
- **Branch:** `feature/difficulty-system`
- **Target Files:** `Assets/Scripts/Core/GameDifficultyManager.cs`, `EnemyStats.cs`, `PlayerStats.cs`
- **Spec:**
  - `Easy`: Enemy Dmg 100%, Player Dmg 100%, Enemy Level visible, Floating HP visible, Parry circle visible.
  - `Normal`: Enemy Dmg 130%, Player Dmg 70%, Floating HP visible, Parry circle visible, Enemy Level hidden.
  - `Hard`: Enemy Dmg 160%, Player Dmg 40%, STM/HP regen halved, all helper UI hidden (no HP bars, no parry circle).
- **DoD:** Switching difficulty alters damage calculation and toggles helper UI according to spec.

#### [TASK M5.2] Main Menu Scene
<!-- [ASSIGNED TO HUMAN TEAMMATE 2 - AI AGENT DO NOT EXECUTE] -->
- **Assignee:** Human Teammate 2
- **Branch:** `feature/main-menu-scene`
- **Target Files:** `Assets/Scenes/MainMenu.unity`, `MainMenuController.cs`
- **Spec:**
  - UI Layout: Title, Play (opens Easy/Normal/Hard modal), Continue (load save), Settings (Volume sliders), Exit.
- **DoD:** Main Menu launches cleanly, starts new game with selected difficulty, loads existing save.

---

### [MILESTONE 6] QA Polish & Windows Standalone Build
- **Deadline:** 2026-09-30 18:00 (FINAL)

#### [TASK M6.1] Full Playthrough QA & Bugfixing
- **Branch:** `bugfix/final-qa-balancing`
- **Spec:** Test end-to-end traversal: Menu -> City -> Church (Key + Rune 1) -> City (Rune 2) -> Market (Boss + Rune 4) -> Forest (Fox + Rune 3) -> Entrance VN (Drag 4 Runes) -> Castle (Boss) -> Victory. Fix camera bounds, collision traps, audio mixing.
- **DoD:** Zero red console exceptions in full playthrough.

#### [TASK M6.2] Windows Standalone Build (.exe)
- **Branch:** `feature/standalone-build`
- **Spec:** Configure Build Settings scene hierarchy: `MainMenu` (0) -> `CityCenter` (1) -> `OutdoorMarket` (2) -> `SuburbToForest` (3) -> `DemonCastleEntrance` (4) -> `DemonCastle` (5) -> `Church` (6). Build x64 executable to `Builds/TheLastKnight_v1.0/`.
- **DoD:** `TheLastKnight.exe` executes standalone without crashes.

---

## 4. MASTER SCHEDULE MATRIX
| ID | Milestone / Task | Branch | Deadline | Acceptance Criteria |
|---|---|---|---|---|
| **M0** | Git Baseline Cleanup | `bugfix/cleanup-layer-and-prefabs` | 09-19 23:59 | Git clean on main, scenes load without errors |
| **M1.1** | Player Attack Hitbox | `feature/player-attack-hitbox` | 09-20 18:00 | Left click damages enemies; enemy death anim |
| **M1.2** | Parry Timing Circle | `feature/parry-system` | 09-21 12:00 | Timed attack staggers enemy 1.5s; 100% crit window |
| **M1.3** | Action Stamina & Medusa Buff | `feature/stamina-system` | 09-21 23:59 | STM limits actions; 2x regen at Medusa |
| **M2.1** | Loot Drops (Gold/EXP) | `feature/monster-loot-economy` | 09-22 14:00 | Enemies drop Gold & EXP; HUD updates |
| **M2.2** | Quick Potion Drink (Q) | `feature/quickslot-potion` | 09-22 23:59 | Q drinks potion (2.5s), restores 50 HP |
| **M2.3** | Shadow Market Shop & Rune 4 | `feature/shadow-market-shop` | 09-23 23:59 | Buy potions, stat buffs, and Rune 4 (150g) |
| **M3.1** | 5 Maps Boss Setup | `feature/map-bosses-placement` | 09-24 18:00 | Bosses & Fox placed in designated scenes |
| **M3.2** | 4 Demon Runes Flow | `feature/demon-runes-progression` | 09-25 12:00 | Church 2F chest, Medusa, Fox, Shop runes work |
| **M3.3** | Demon Castle Entrance VN | `feature/demon-castle-gate-vn` | 09-25 23:59 | Arthur back to camera, return button, drag 4 runes |
| **M4.1A** | Audio Sourcing & Credits `[HUMAN 1]` | `art/audio-assets-and-credits` | 09-26 18:00 | CC0 audio files in Assets/Audio/ + CREDITS.md |
| **M4.1B** | AudioManager Script `[AI AGENT]` | `feature/audio-manager-integration` | 09-26 23:59 | AudioManager.cs operational, wiring SFX triggers |
| **M4.2** | Medusa Save & Respawn | `feature/medusa-save-respawn` | 09-27 12:00 | F saves/heals; YOU DIED respawn functions |
| **M4.3** | Story Dialogue Cutscenes | `feature/story-dialogue-system` | 09-27 23:59 | Intro prologue + victory ending cutscenes |
| **M5.1** | Easy / Normal / Hard | `feature/difficulty-system` | 09-28 18:00 | Scaled dmg/regen and UI visibility toggles |
| **M5.2** | Main Menu Scene `[HUMAN 2]` | `feature/main-menu-scene` | 09-29 18:00 | Play (difficulty), continue, settings, exit |
| **M6.1** | QA Playthrough Polish | `bugfix/final-qa-balancing` | 09-30 12:00 | 0 console exceptions across full game run |
| **M6.2** | Standalone .exe Build | `feature/standalone-build` | **09-30 18:00** | **Functional Windows standalone .exe delivered** |
