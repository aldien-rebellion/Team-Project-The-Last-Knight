# Project Overview
- **Game Title**: The Last Knight
- **High-Level Concept**: Arthur Reuven, a disgraced former Commander of the Border Knights, fights to redeem himself through a dark, interconnected 2D world. Players explore, level up custom stats, unlock skills, and fight bosses in classic Metroidvania fashion.
- **Players**: Single-player
- **Inspiration / Reference Games**: Hollow Knight (kinematic movement feel, combat weight), Castlevania: Symphony of the Night (stat-based RPG progression, level-up system), Celeste (for pixel-perfect kinematic precision)
- **Tone / Art Direction**: Dark fantasy, melancholic, 2D high-contrast side-scroller
- **Target Platform**: PC (Standalone Windows)
- **Screen Orientation / Resolution**: Landscape 1920x1080 (16:9 widescreen)
- **Render Pipeline**: URP 2D (Universal Render Pipeline with 2D Renderer)

# Game Mechanics
## Core Gameplay Loop
The player explores an interconnected world, encounters various enemy types (normal, elite, and bosses), defeats them to acquire EXP, and levels up. Upon leveling up, players distribute stat points (STR, VIT, DEX, AGI) which dynamically scale Arthur's combat parameters. Progression unlocks 6 high-impact skills used to defeat tougher challenges and access previously unreachable areas.

## Controls and Input Methods
The game uses Unity's **New Input System** (configured with the project-wide asset at `Assets/Settings/InputSystem_Actions.inputactions`).
Input actions are mapped to Keyboard/Mouse and Gamepads:
- **Horizontal Movement**: A/D or Left Stick (maps to Action: `Move`)
- **Jump**: Space or South Button (maps to Action: `Jump`)
- **Dash / Dodge**: Shift or West Button (maps to Action: `Dash`)
- **Normal Attack**: Left Click or East Button (maps to Action: `Attack`)
- **Use Skill**: E or Right Shoulder (maps to Action: `UseSkill`)
- **Skill Swap**: Mouse Wheel or D-Pad Up/Down (maps to Action: `CycleSkill`)

## Kinematic Custom Movement System
Arthur's physics will be managed by a **Kinematic Custom Controller** rather than dynamic Rigidbody2D forces.
- **Why**: Dynamic Rigidbody2D controllers often suffer from "sticky walls", floaty jump heights, variable slide friction on slopes, and lack of perfect control. A Kinematic Custom Controller uses manual swept-collisions (`BoxCast2D` / `Raycast2D`) to provide instant responsiveness, variable jump cuts, reliable slope-climbing, and pixel-perfect positioning, which are essential for Metroidvania platforming.
- **Collision Resolution**: Moves the character along axes (X first, then Y). If a collision is detected via BoxCast, the movement is truncated, and the character slides along the hit surface.
- **Slope-Climbing**: Automatically detects slopes up to 45 degrees, sliding the character up or down smoothly while maintaining contact with the ground.

## Progression & Stats (ScriptableObject-Driven)
Character growth is configured via ScriptableObjects for high editor-tweakability, but runtime state is stored in a clean component-based model.
- **CharacterStatsSO**: Persistent template defining starting stats (STR, VIT, DEX, AGI), stat scaling coefficients, and level-up EXP curves.
- **PlayerStats**: Component attached to Arthur that manages runtime dynamic values (Level, current EXP, current HP, available stat points) to avoid modifying disk assets at runtime.
  - **STR (Strength)**: Increases Normal Attack & Skill Damage.
  - **VIT (Vitality)**: Increases Max HP.
  - **DEX (Dexterity)**: Scales Skill damage multiplier and Critical Hit chance.
  - **AGI (Agility)**: Increases Movement Speed, Attack speed, and Dash distance.

---

# UI Design
## HUD Layout (In-Game Screen)
- **Top-Left**:
  - HP Bar (Red, dynamically scales with Max HP from VIT)
  - EXP Bar (Purple, with Level indicator)
  - Active Skill Icon (Slots 1-6, representing currently equipped skill)
- **Bottom-Left / Popup**:
  - Available Stat Points notifier (glowing text when > 0)

## Menus (Pause/Character Menu)
- **Stats Tab**:
  - Left side: Arthur's Portrait and current Level
  - Middle: Attributes (STR, VIT, DEX, AGI) with numeric values and `+` buttons to upgrade if Stat Points > 0
  - Right side: Derived stats (Attack Power, Critical Chance, Max HP, Movement Speed)

---

# Key Asset & Context

We will establish a modular directory structure under `Assets/Scripts/`:
```
Assets/
├── Plans/
│   └── the-last-knight-plan.md
├── Scripts/
│   ├── Input/
│   │   └── PlayerInputHandler.cs
│   ├── Physics/
│   │   └── KinematicCharacterController2D.cs
│   ├── Player/
│   │   ├── PlayerController.cs
│   │   └── PlayerState.cs (Enum or FSM states)
│   ├── Stats/
│   │   ├── CharacterStatsSO.cs
│   │   └── PlayerStats.cs
│   └── Camera/
│       └── CameraFollow2D.cs
```

### Key Signatures and Code Snippets

#### 1. Kinematic Character Controller (`KinematicCharacterController2D.cs`)
```csharp
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class KinematicCharacterController2D : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] [Range(0.01f, 0.1f)] private float skinWidth = 0.02f;
    [SerializeField] private float maxSlopeAngle = 45f;

    private BoxCollider2D boxCollider;
    private Rigidbody2D rb;

    public bool IsGrounded { get; private set; }
    public bool HitWall { get; private set; }
    public bool HitCeiling { get; private set; }

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        // Ensure Rigidbody2D is kinematic and configured correctly
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true;
    }

    public void Move(Vector2 velocity, float deltaTime)
    {
        Vector2 deltaMove = velocity * deltaTime;
        IsGrounded = false;
        HitWall = false;
        HitCeiling = false;

        // 1. Resolve Y-axis Movement (Vertical first or Horizontal depending on system, standard is Y then X or vice versa)
        ResolveVerticalMovement(ref deltaMove);

        // 2. Resolve X-axis Movement & Slopes
        ResolveHorizontalMovement(ref deltaMove);

        // Apply remaining movement
        rb.position += deltaMove;
    }

    private void ResolveHorizontalMovement(ref Vector2 deltaMove) { /* Swept BoxCast and slope handling */ }
    private void ResolveVerticalMovement(ref Vector2 deltaMove) { /* Swept BoxCast and ground/ceiling handling */ }
}
```

#### 2. Player Input Handler (`PlayerInputHandler.cs`)
```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool DashTriggered { get; private set; }
    public bool AttackTriggered { get; private set; }
    public bool UseSkillTriggered { get; private set; }
    public float CycleSkillInput { get; private set; }

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;
    private InputAction attackAction;
    private InputAction useSkillAction;
    private InputAction cycleSkillAction;

    private void Start()
    {
        // Fetch project-wide actions
        var actions = InputSystem.actions;
        if (actions != null)
        {
            moveAction = actions.FindAction("Move");
            jumpAction = actions.FindAction("Jump");
            dashAction = actions.FindAction("Sprint"); // Or mapped Dash
            attackAction = actions.FindAction("Attack");
            useSkillAction = actions.FindAction("Crouch"); // Fallback or new
            cycleSkillAction = actions.FindAction("Next");
        }
    }

    private void Update()
    {
        if (moveAction != null) MoveInput = moveAction.ReadValue<Vector2>();
        if (jumpAction != null)
        {
            JumpTriggered = jumpAction.WasPressedThisFrame();
            JumpHeld = jumpAction.IsPressed();
        }
        if (dashAction != null) DashTriggered = dashAction.WasPressedThisFrame();
        if (attackAction != null) AttackTriggered = attackAction.WasPressedThisFrame();
        if (useSkillAction != null) UseSkillTriggered = useSkillAction.WasPressedThisFrame();
        if (cycleSkillAction != null) CycleSkillInput = cycleSkillAction.ReadValue<float>();
    }
}
```

#### 3. Character Stats Template (`CharacterStatsSO.cs`)
```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterStats", menuName = "The Last Knight/Character Stats")]
public class CharacterStatsSO : ScriptableObject
{
    [Header("Base Stat Configuration")]
    public int baseSTR = 10;
    public int baseVIT = 10;
    public int baseDEX = 10;
    public int baseAGI = 10;

    [Header("Growth Coefficients")]
    public float hpPerVIT = 12f;
    public float attackPerSTR = 1.5f;
    public float critPerDEX = 0.5f; // percentage
    public float speedPerAGI = 0.1f;
    public float dashDistancePerAGI = 0.05f;

    [Header("EXP Curve Parameters")]
    public int baseExpNeeded = 100;
    public float expGrowthMultiplier = 1.2f;

    public int GetExpNeededForLevel(int level)
    {
        return Mathf.RoundToInt(baseExpNeeded * Mathf.Pow(expGrowthMultiplier, level - 1));
    }
}
```

---

# Implementation Steps

## Phase 1: Input Setup and Kinematic Foundation
### Step 1: Input Mappings Configuration
- **Description**: Add missing actions ("Dash", "UseSkill") to the project-wide `InputSystem_Actions.inputactions` asset using the Input System Editor or via the Unity Input C# API. Set up standard Keyboard/Mouse and Gamepad bindings.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Implement Input Handler
- **Description**: Create `PlayerInputHandler.cs` under `Assets/Scripts/Input/`. It retrieves references from `InputSystem.actions` in `Start` and polls values in `Update` for frame-perfect safety.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Implement Kinematic Collision Solver
- **Description**: Create `KinematicCharacterController2D.cs` under `Assets/Scripts/Physics/`. Build sweeping collision logic utilizing `BoxCollider2D.Cast` along vertical and horizontal axes to resolve collisions and manage slopes. Set up custom gravity, grounding checks, and ceiling checks.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

---

## Phase 2: Arthur's Movement and State Logic
### Step 4: Implement Player Controller State Machine
- **Description**: Create `PlayerController.cs` under `Assets/Scripts/Player/`. Implement state variables: `Idle`, `Walking`, `Jumping`, `Falling`, `Dashing`. It reads movement parameters from `PlayerInputHandler` and feeds horizontal velocity and jumping vectors into the `KinematicCharacterController2D`.
- **Assigned role**: developer
- **Dependencies**: Step 2, Step 3
- **Parallelizable**: No

### Step 5: Implement Variable Jump & Gravity Scaling
- **Description**: Inside `PlayerController.cs`, refine gravity and jumping:
  - If the player holds Jump, apply default downward gravity.
  - If the player releases Jump early while rising, scale gravity higher (or cut Y-velocity) to achieve variable jump height.
  - Implement jump buffering (jump inputs registered slightly before grounding are preserved).
  - Implement Coyote Time (player can jump for a few frames after leaving a ledge).
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

### Step 6: Implement Dash / Dodge Mechanic
- **Description**: Add dash mechanics into `PlayerController.cs`. Arthur dashes forward in the direction he is facing. During the dash:
  - Gravity is completely suspended.
  - Maintain high linear speed for `dashDuration` (derived from AGI).
  - Apply invincibility state (no-damage frame trigger).
  - Enforce dash cooldown and limit to 1 dash until grounded again.
- **Assigned role**: developer
- **Dependencies**: Step 5
- **Parallelizable**: No

---

## Phase 3: Progression, Stats, and Camera Tracking
### Step 7: Create ScriptableObject Stats System
- **Description**: Create `CharacterStatsSO.cs` and `PlayerStats.cs` in `Assets/Scripts/Stats/`.
  - `CharacterStatsSO` manages structural stat coefficients.
  - `PlayerStats` handles runtime data (current HP, current Level, current EXP, stat attributes, and points). It scales movement speed, health, attack power, and dash distance dynamically, notifying the controller of speed/dash value changes.
- **Assigned role**: developer
- **Dependencies**: Step 6
- **Parallelizable**: Yes

### Step 8: Metroidvania Camera System
- **Description**: Create `CameraFollow2D.cs` under `Assets/Scripts/Camera/`. It tracks Arthur smoothly using `Vector3.SmoothDamp`.
  - Integrate a configurable "deadzone" (the player can move slightly without triggering the camera).
  - Add camera constraints using a 2D bounding box (e.g. `BoxCollider2D` representing room boundaries) so the camera cannot pan outside map edges.
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: Yes

---

## Phase 4: Integration & Scene Setup
### Step 9: Sample Scene Setup
- **Description**: Configure the `SampleScene.unity` file.
  - Draw placeholder testing structures (ground platforms, steep slopes up to 45 degrees, narrow corridors, wall jump surfaces) using simple 2D sprites or Tilemaps.
  - Set up a composite ground layer with the Layer Mask configured on `KinematicCharacterController2D`.
  - Create the Player Prefab containing Arthur's Game Object, `BoxCollider2D`, kinematic `Rigidbody2D`, `PlayerInputHandler`, `KinematicCharacterController2D`, `PlayerController`, and `PlayerStats`.
  - Link the camera script to Arthur and define a test boundary.
- **Assigned role**: developer
- **Dependencies**: Step 1 to Step 8
- **Parallelizable**: No

---

# Verification & Testing

## Manual Test Cases
1. **Ground Collision**: Move Arthur left and right on flat ground. Ensure there is no vertical jitter and that he is reported as `IsGrounded == true` continuously.
2. **Slope Climbing**: Walk Arthur up 15-degree, 30-degree, and 45-degree slopes. Ensure movement is smooth and there are no bounces. Walk into walls steeper than 45 degrees to verify that Arthur halts as expected.
3. **Variable Jump Height**: Tap Space bar and verify Arthur performs a small jump. Hold Space bar and verify Arthur jumps significantly higher.
4. **Coyote Time & Jump Buffering**: Run Arthur off a ledge and press jump a split second after falling to confirm coyote time functions. Press jump a split second before landing to verify Arthur jumps immediately upon touching the ground.
5. **Dash Mechanics**: Press Dash on the ground and in the air. Verify that gravity is suspended during the dash, that Arthur cannot double-dash in mid-air, and that the cooldown timer is respected.
6. **Camera Boundary Boundaries**: Move Arthur to the far edge of the map. Verify that the camera smoothly stops panning once it hits the room boundaries.
7. **Stat Allocation Verification**: Trigger a mock level-up event via a debug key (e.g. press 'L' to gain 100 EXP), verify that Stat Points increase. Increase VIT, verify that Arthur's Max HP dynamically expands in real-time. Increase AGI, verify that Arthur's walk speed and dash distance increase.

## Auto Testing (Script-driven Console Validation)
- We will include a debug console logging suite inside `PlayerStats` to log:
  - `"Level Up! New Level: {level}, Available Points: {points}"`
  - `"Stat Modified: {statName} set to {value}. Recalculating derived parameters."`
