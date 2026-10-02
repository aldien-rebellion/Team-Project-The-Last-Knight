using UnityEngine;
using TheLastKnight.Input;
using TheLastKnight.Physics;
using TheLastKnight.Combat;
using TheLastKnight.Stats;
using System.Collections.Generic;

namespace TheLastKnight.Player
{
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Jumping,
        Falling,
        Dashing,
        Attacking,
        UsingSkill,
        Buffing,
        Excalibur,
        Drinking,
        Hurt
    }

    [RequireComponent(typeof(KinematicCharacterController2D), typeof(SpriteRenderer), typeof(Animator))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Required References")]
        [SerializeField, Tooltip("Input handler reference. Will find in parent/scene if not set.")]
        private PlayerInputHandler _inputHandler;

        [Header("Animation Settings")]
        [SerializeField, Tooltip("Animator component reference. Auto-assigned if null.")]
        private Animator _animator;
        [SerializeField, Tooltip("Attack animation duration.")]
        private float _attackDuration = 0.25f;
        [SerializeField, Tooltip("Cooldown between attacks.")]
        private float _attackCooldown = 0.35f;

        [Header("Skill 1 Settings (Carnage Burst - Key E)")]
        [SerializeField, Tooltip("Carnage Burst skill duration.")]
        private float _skillDuration = 0.5f;
        [SerializeField, Tooltip("Cooldown between Skill 1 uses.")]
        private float _skillCooldown = 1.0f;
        [SerializeField, Tooltip("Damage multiplier for Skill 1 (Carnage Burst) multiplied by Player ATK.")]
        private float _skillDamageMultiplier = 2.0f;
        [SerializeField, Tooltip("Radius of the circular forward area for Skill 1 (Carnage Burst).")]
        private float _skillAttackRadius = 2.2f;
        [SerializeField, Tooltip("Hitbox size for Skill 1 (Carnage Burst).")]
        private Vector2 _skillAttackSize = new Vector2(2.8f, 2.2f);
        [SerializeField, Tooltip("Hitbox offset for Skill 1 (Carnage Burst). Centered on player.")]
        private Vector2 _skillAttackOffset = new Vector2(0f, 0.5f);
        [SerializeField, Tooltip("Knockback velocity impulse applied to enemies hit by Skill 1.")]
        private Vector2 _skillKnockbackForce = new Vector2(6f, 2.5f);
        [SerializeField, Tooltip("Displacement distance pushing enemies outward along the skill circle.")]
        private float _skillPushDistance = 1.2f;

        [Header("Skill 2 Settings (Buff - Key R)")]
        [SerializeField, Tooltip("Buff skill duration.")]
        private float _buffDuration = 0.6f;
        [SerializeField, Tooltip("Cooldown between Skill 2 (Buff) uses.")]
        private float _buffCooldown = 30.0f;

        [Header("Skill 3 Settings (Excalibur - Key T)")]
        [SerializeField, Tooltip("Excalibur skill duration.")]
        private float _excaliburDuration = 3.8f;
        [SerializeField, Tooltip("Cooldown between Skill 3 (Excalibur) uses.")]
        private float _excaliburCooldown = 5.0f;
        [SerializeField, Tooltip("Damage multiplier for Skill 3 (Excalibur) multiplied by Player ATK.")]
        private float _excaliburDamageMultiplier = 5.0f;
        [SerializeField, Tooltip("Hitbox size for Excalibur beam (length x height), stretched back to touch player.")]
        private Vector2 _excaliburHitSize = new Vector2(24f, 4.2f);
        [SerializeField, Tooltip("Hitbox offset for Excalibur beam from player center.")]
        private Vector2 _excaliburHitOffset = new Vector2(12f, 1.47f);
        [SerializeField, Tooltip("Time delay from skill start before damage is dealt (corresponds to beam release).")]
        private float _excaliburDamageDelay = 3.3f;
        [SerializeField, Tooltip("Stun duration in seconds applied to enemies hit by Skill 3 (Excalibur).")]
        private float _excaliburStunDuration = 2.0f;
        [SerializeField, Tooltip("VFX controller for Excalibur. Auto-assigned if null.")]
        private ExcaliburVFXController _excaliburVFXController;

        [Header("Drink Settings (Key Q)")]
        [SerializeField, Tooltip("Drink skill duration.")]
        private float _drinkDuration = 2.5f;
        [SerializeField, Tooltip("Cooldown between drink uses.")]
        private float _drinkCooldown = 1.0f;

        [Header("Hurt Settings")]
        [SerializeField, Tooltip("Frame 1 duration before advancing to frame 2 when damage stops.")]
        private float _hurtFrame1Duration = 0.25f;
        [SerializeField, Tooltip("Frame 2 duration.")]
        private float _hurtFrame2Duration = 0.17f;

        [Header("Movement Settings")]
        [SerializeField, Tooltip("Base movement speed.")]
        private float _baseMoveSpeed = 8f;
        [SerializeField, Tooltip("Base sprint movement speed.")]
        private float _baseSprintSpeed = 13f;
        [SerializeField, Tooltip("Horizontal acceleration rate.")]
        private float _acceleration = 50f;
        [SerializeField, Tooltip("Horizontal deceleration rate.")]
        private float _deceleration = 50f;

        [Header("Jump Settings")]
        [SerializeField, Tooltip("Base jump force.")]
        private float _baseJumpForce = 16f;
        [SerializeField, Tooltip("Base gravity applied to character.")]
        private float _gravity = 40f;
        [SerializeField, Tooltip("Gravity multiplier when falling.")]
        private float _fallMultiplier = 1.5f;
        [SerializeField, Tooltip("Gravity multiplier when rising and jump button is released (Variable Jump).")]
        private float _jumpCutMultiplier = 2.5f;
        [SerializeField, Tooltip("Maximum downward fall velocity.")]
        private float _maxFallSpeed = 20f;
        [SerializeField, Tooltip("Time window to register a jump before hitting the ground.")]
        private float _jumpBufferTime = 0.15f;
        [SerializeField, Tooltip("Time window to allow a jump after leaving a ledge.")]
        private float _coyoteTime = 0.15f;

        [Header("Dash Settings")]
        [SerializeField, Tooltip("Base dash speed.")]
        private float _baseDashSpeed = 20f;
        [SerializeField, Tooltip("Base dash duration.")]
        private float _baseDashDuration = 0.2f;
        [SerializeField, Tooltip("Cooldown between dashes.")]
        private float _dashCooldown = 0.5f;
        [SerializeField, Tooltip("Invincibility duration during dash (i-Frames in seconds).")]
        private float _dashIFrameDuration = 0.15f;

        // Components
        private KinematicCharacterController2D _kinematicController;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _playerCollider;
        private readonly List<Collider2D> _ignoredEnemyColliders = new List<Collider2D>();

        // Current Active Scaled Stats (Step 7 Preparation)
        public float MoveSpeed { get; set; }
        public float SprintSpeed { get; set; }
        public float JumpForce { get; set; }
        public float DashSpeed { get; set; }
        public float DashDuration { get; set; }
        public float AttackSpeedMultiplier { get; set; } = 1.0f;
        public bool AdminNoCooldown { get; set; }
        public bool AdminStatusImmunity { get; set; }
        public bool CanDoubleJump { get; set; } = false;
        private bool _hasDoubleJumped = false;
        public bool HasDoubleJumped => _hasDoubleJumped;

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float BaseSprintSpeed => _baseSprintSpeed;
        public float BaseJumpForce => _baseJumpForce;
        public float BaseDashSpeed => _baseDashSpeed;
        public float BaseDashDuration => _baseDashDuration;

        // Cooldown and multiplier properties (accessible for tests/UI)
        public float DashCooldown { get => _dashCooldown; set => _dashCooldown = value; }
        public float DashIFrameDuration { get => _dashIFrameDuration; set => _dashIFrameDuration = value; }

        public float SkillCooldown { get => _skillCooldown; set => _skillCooldown = value; }
        public float SkillDamageMultiplier { get => _skillDamageMultiplier; set => _skillDamageMultiplier = value; }
        public float SkillAttackRadius { get => _skillAttackRadius; set => _skillAttackRadius = value; }
        public Vector2 SkillAttackSize { get => _skillAttackSize; set => _skillAttackSize = value; }
        public Vector2 SkillAttackOffset { get => _skillAttackOffset; set => _skillAttackOffset = value; }
        public Vector2 SkillKnockbackForce { get => _skillKnockbackForce; set => _skillKnockbackForce = value; }
        public float SkillPushDistance { get => _skillPushDistance; set => _skillPushDistance = value; }

        public float BuffCooldown { get => _buffCooldown; set => _buffCooldown = value; }

        public float ExcaliburCooldown { get => _excaliburCooldown; set => _excaliburCooldown = value; }
        public float ExcaliburDamageMultiplier { get => _excaliburDamageMultiplier; set => _excaliburDamageMultiplier = value; }
        public Vector2 ExcaliburHitSize { get => _excaliburHitSize; set => _excaliburHitSize = value; }
        public Vector2 ExcaliburHitOffset { get => _excaliburHitOffset; set => _excaliburHitOffset = value; }
        public float ExcaliburDamageDelay { get => _excaliburDamageDelay; set => _excaliburDamageDelay = value; }
        public float ExcaliburStunDuration { get => _excaliburStunDuration; set => _excaliburStunDuration = value; }

        // State Machine
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        // Dash State Variables
        private bool _dashInvincible;
        private float _dashIFrameTimer = 0f;
        private float _parryInvincibleUntil;
        public bool IsInvincible { get => _dashIFrameTimer > 0f || _dashInvincible || Time.time < _parryInvincibleUntil; private set => _dashInvincible = value; }
        private float _dashTimer = 0f;
        private float _dashCooldownTimer = 0f;
        private bool _hasDashedInAir = false;
        private Vector2 _dashDirection = Vector2.right;

        // Jump & Coyote Timers
        private float _jumpBufferCounter = -1f;
        private float _coyoteTimeCounter = -1f;
        [SerializeField, Tooltip("Time in seconds to disable one-way platforms when dropping down.")]
        private float _platformDropDuration = 0.15f;
        private float _platformDropTimer = 0f;
        private Collider2D _droppedPlatform;

        // Movement variables
        private Vector2 _velocity;
        public Vector2 Velocity => _velocity;
        public void ResetVelocity() => _velocity = Vector2.zero;
        public bool IsFacingRight { get; private set; } = true;

        // Attack State Variables
        private float _attackTimer = 0f;
        private float _attackCooldownTimer = 0f;
        private bool _isAttacking = false;
        [SerializeField] private Vector2 _attackSize = new Vector2(2.2f, 2f);
        [SerializeField] private Vector2 _attackOffset = new Vector2(1.1f, 0f);
        [SerializeField] private LayerMask _attackLayers = ~0;
        private readonly HashSet<IDamageable> _attackTargets = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _skillTargets = new HashSet<IDamageable>();
        private readonly HashSet<IDamageable> _excaliburTargets = new HashSet<IDamageable>();

        // Skill State Variables
        private float _skillTimer = 0f;
        private float _skillCooldownTimer = 0f;

        // Buff State Variables
        private float _buffTimer = 0f;
        private float _buffCooldownTimer = 0f;

        // Excalibur State Variables
        private float _excaliburTimer = 0f;
        private float _excaliburCooldownTimer = 0f;

        // Drink State Variables
        private float _drinkTimer = 0f;
        private float _drinkCooldownTimer = 0f;

        // Hurt State Variables
        private float _hurtTimer = 0f;

        // Action Cancellation Tracking
        private float _actionDurationTimer = 0f;
        private float _actionInitialMoveX = 0f;

        private void Awake()
        {
            _kinematicController = GetComponent<KinematicCharacterController2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _playerCollider = GetComponent<Collider2D>();

            if (_inputHandler == null)
            {
                _inputHandler = GetComponent<PlayerInputHandler>();
                if (_inputHandler == null)
                {
                    _inputHandler = FindAnyObjectByType<PlayerInputHandler>();
                }
            }

            if (_excaliburVFXController == null)
            {
                _excaliburVFXController = GetComponent<ExcaliburVFXController>();
            }

            // Initialize active movement stats from base values
            MoveSpeed = _baseMoveSpeed;
            SprintSpeed = _baseSprintSpeed;
            JumpForce = _baseJumpForce;
            DashSpeed = _baseDashSpeed;
            DashDuration = _baseDashDuration;
            AttackSpeedMultiplier = 1.0f;
        }

        private void Update()
        {
            var stats = GetComponent<PlayerStats>();
            if (stats != null && stats.IsDead) return;

            // Update Dash i-Frame Timer
            if (_dashIFrameTimer > 0f)
            {
                _dashIFrameTimer -= Time.deltaTime;
                if (_dashIFrameTimer <= 0f)
                {
                    _dashInvincible = false;
                }
            }

            // Cleanup ignored enemy colliders when separated
            CleanupIgnoredColliders();

            // Update Dash Cooldown
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
            }

            // Update One-Way Platform Pass-Through (Down+A or Down+D) & Drop Timer (Down+Space)
            bool isDownMoveHeld = _inputHandler != null && _inputHandler.MoveInput.y < -0.5f && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f;
            if (_platformDropTimer > 0f)
            {
                _platformDropTimer -= Time.deltaTime;
                if (_platformDropTimer <= 0f)
                {
                    _droppedPlatform = null;
                    if (_kinematicController != null)
                    {
                        _kinematicController.IgnoredOneWayPlatform = null;
                    }
                }
            }

            if (_kinematicController != null)
            {
                _kinematicController.IgnoreOneWayPlatforms = isDownMoveHeld;
            }

            // Update Attack Cooldown
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            // Update Skill Cooldown
            if (_skillCooldownTimer > 0f)
            {
                _skillCooldownTimer -= Time.deltaTime;
            }

            // Update Buff Cooldown
            if (_buffCooldownTimer > 0f)
            {
                _buffCooldownTimer -= Time.deltaTime;
            }

            // Update Excalibur Cooldown
            if (_excaliburCooldownTimer > 0f)
            {
                _excaliburCooldownTimer -= Time.deltaTime;
            }

            if (_drinkCooldownTimer > 0f)
            {
                _drinkCooldownTimer -= Time.deltaTime;
            }

            if (AdminNoCooldown)
            {
                _attackCooldownTimer = 0f;
                _skillCooldownTimer = 0f;
                _buffCooldownTimer = 0f;
                _excaliburCooldownTimer = 0f;
                _drinkCooldownTimer = 0f;
                _dashCooldownTimer = 0f;
            }

            // Coyote time update
            if (_kinematicController.IsGrounded)
            {
                _coyoteTimeCounter = _coyoteTime;
                _hasDashedInAir = false; // Reset air dash
                _hasDoubleJumped = false; // Reset double jump
            }
            else
            {
                _coyoteTimeCounter -= Time.deltaTime;
            }

            // Jump buffer update and Jump Cancel
            if (_inputHandler != null && _inputHandler.JumpTriggered)
            {
                if (CurrentState != PlayerState.Attacking)
                {
                    if (IsCancellableState(CurrentState))
                    {
                        CancelCurrentAction();
                    }
                }
                _jumpBufferCounter = _jumpBufferTime;
            }
            else
            {
                _jumpBufferCounter -= Time.deltaTime;
            }

            // Check for Attack Trigger (Cannot be performed during Attack; cancels other actions)
            if (_inputHandler != null && _inputHandler.AttackTriggered && _attackCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking)
                {
                    if (stats == null || stats.CurrentStamina >= 15f)
                    {
                        if (IsCancellableState(CurrentState))
                        {
                            CancelCurrentAction();
                        }
                        StartAttack();
                    }
                    else
                    {
                        stats?.NotifyInsufficientStamina();
                    }
                }
            }
            // Check for Skill Trigger (Carnage Burst - Key E)
            else if (_inputHandler != null && _inputHandler.UseSkillTriggered && _skillCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking && CurrentState != PlayerState.UsingSkill)
                {
                    if (stats == null || stats.CurrentStamina >= 25f)
                    {
                        if (IsCancellableState(CurrentState))
                        {
                            CancelCurrentAction();
                        }
                        StartSkill();
                    }
                    else
                    {
                        stats?.NotifyInsufficientStamina();
                    }
                }
            }
            // Check for Buff Trigger (Key R)
            else if (_inputHandler != null && _inputHandler.UseBuffTriggered && _buffCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking && CurrentState != PlayerState.Buffing)
                {
                    if (IsCancellableState(CurrentState))
                    {
                        CancelCurrentAction();
                    }
                    StartBuff();
                }
            }
            // Check for Excalibur Trigger (Key T)
            else if (_inputHandler != null && _inputHandler.UseExcaliburTriggered && _excaliburCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking && CurrentState != PlayerState.Excalibur)
                {
                    if (stats == null || stats.CurrentStamina >= 50f)
                    {
                        if (IsCancellableState(CurrentState))
                        {
                            CancelCurrentAction();
                        }
                        StartExcalibur();
                    }
                    else
                    {
                        stats?.NotifyInsufficientStamina();
                    }
                }
            }
            // Check for Drink Trigger (Key Q)
            else if (_inputHandler != null && _inputHandler.UseDrinkTriggered && _drinkCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking && CurrentState != PlayerState.Drinking)
                {
                    var qm = TheLastKnight.Core.QuickItemManager.Instance;
                    var inventory = TheLastKnight.Inventory.InventoryManager.Instance;
                    int index = qm != null ? qm.GetActiveSlotIndex() : -1;
                    if (inventory != null && inventory.CanUseQuickSlot(index, stats))
                    {
                        if (IsCancellableState(CurrentState))
                        {
                            CancelCurrentAction();
                        }
                        StartDrink();
                    }
                }
            }
            // Check for Dash Trigger
            else if (_inputHandler != null && _inputHandler.DashTriggered && _dashCooldownTimer <= 0f)
            {
                if (CurrentState != PlayerState.Attacking && CurrentState != PlayerState.Dashing)
                {
                    bool canDash = _kinematicController.IsGrounded || !_hasDashedInAir;
                    if (canDash)
                    {
                        if (stats == null || stats.CurrentStamina >= 20f)
                        {
                            if (IsCancellableState(CurrentState))
                            {
                                CancelCurrentAction();
                            }
                            StartDash();
                        }
                        else
                        {
                            stats?.NotifyInsufficientStamina();
                        }
                    }
                }
            }

            // Handle State Logic
            if (CurrentState == PlayerState.Dashing)
            {
                UpdateDash();
            }
            else if (CurrentState == PlayerState.Attacking)
            {
                UpdateAttack();
            }
            else if (CurrentState == PlayerState.UsingSkill)
            {
                UpdateSkill();
            }
            else if (CurrentState == PlayerState.Buffing)
            {
                UpdateBuff();
            }
            else if (CurrentState == PlayerState.Excalibur)
            {
                UpdateExcalibur();
            }
            else if (CurrentState == PlayerState.Drinking)
            {
                UpdateDrink();
            }
            else if (CurrentState == PlayerState.Hurt)
            {
                UpdateHurt();
            }
            else
            {
                UpdateNormalMovement();
            }
        }

        private void StartDash()
        {
            if (!GetComponent<PlayerStats>().TrySpendStamina(20f)) return;
            CurrentState = PlayerState.Dashing;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("dash");
            IsInvincible = true;
            _dashIFrameTimer = _dashIFrameDuration;
            _dashTimer = DashDuration;
            _dashCooldownTimer = _dashCooldown;

            if (_kinematicController != null)
            {
                _kinematicController.IgnoreEnemies = true;
            }
            IgnoreEnemyCollisions(true);

            if (!_kinematicController.IsGrounded)
            {
                _hasDashedInAir = true;
            }

            // Dash horizontal direction is based on movement input if there is any, otherwise facing direction
            float dashSign = IsFacingRight ? 1f : -1f;
            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.01f)
            {
                dashSign = Mathf.Sign(_inputHandler.MoveInput.x);
            }

            _dashDirection = new Vector2(dashSign, 0f);
            _velocity = _dashDirection * DashSpeed;
        }

        private void UpdateDash()
        {
            _dashTimer -= Time.deltaTime;

            // Velocity during dash is flat horizontal speed, gravity is suspended
            _velocity = _dashDirection * DashSpeed;

            // Collect and ignore collision with any enemy colliders passed along the way
            CollectNearbyEnemyColliders();

            // Move character
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_dashTimer <= 0f)
            {
                EndDash();
            }
        }

        private void EndDash()
        {
            _dashInvincible = false;
            _dashIFrameTimer = 0f;
            if (_kinematicController != null)
            {
                _kinematicController.IgnoreEnemies = false;
            }
            IgnoreEnemyCollisions(false);

            // Transition out of dash cleanly
            if (_kinematicController.IsGrounded)
            {
                float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;
                bool isSprinting = _inputHandler != null && _inputHandler.SprintHeld;
                float currentSpeed = isSprinting ? SprintSpeed : MoveSpeed;
                _velocity = new Vector2(moveInputX * currentSpeed, 0f);
                CurrentState = Mathf.Abs(moveInputX) > 0.01f ? (isSprinting ? PlayerState.Running : PlayerState.Walking) : PlayerState.Idle;
            }
            else
            {
                float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;
                _velocity = new Vector2(moveInputX * MoveSpeed, 0f); // smooth exit transition
                CurrentState = PlayerState.Falling;
            }
        }

        private void StartAttack()
        {
            if (!GetComponent<PlayerStats>().TrySpendStamina(15f)) return;
            _attackTargets.Clear();
            foreach (var hit in Physics2D.OverlapCircleAll(transform.position, 2.5f))
            {
                var parry = hit.GetComponentInParent<ParryReceiver>();
                if (parry != null && parry.TryParry()) _parryInvincibleUntil = Time.time + 0.3f;
            }
            CurrentState = PlayerState.Attacking;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("slash");
            _isAttacking = true;
            float atkSpd = AttackSpeedMultiplier > 0.1f ? AttackSpeedMultiplier : 1.0f;
            _attackTimer = _attackDuration / atkSpd;
            _attackCooldownTimer = (_attackDuration + _attackCooldown) / atkSpd;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Attack", 0, 0f);
            }

            // Stop horizontal movement during attack
            _velocity.x = 0f;
        }

        private void UpdateAttack()
        {
            ApplyAttackHits();
            _attackTimer -= Time.deltaTime;

            // Apply gravity during attack if in air
            if (!_kinematicController.IsGrounded)
            {
                float activeGravity = _gravity;
                if (_velocity.y > 0f)
                {
                    if (_inputHandler != null && !_inputHandler.JumpHeld)
                    {
                        activeGravity *= _jumpCutMultiplier;
                    }
                }
                else
                {
                    activeGravity *= _fallMultiplier;
                }
                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            // Keep horizontal velocity at zero during attack
            _velocity.x = 0f;

            // Move character
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_attackTimer <= 0f)
            {
                EndAttack();
            }
        }

        private void EndAttack()
        {
            _isAttacking = false;

            // Transition to appropriate state after attack
            if (_kinematicController.IsGrounded)
            {
                float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;
                bool isSprinting = _inputHandler != null && _inputHandler.SprintHeld;
                float currentSpeed = isSprinting ? SprintSpeed : MoveSpeed;
                _velocity = new Vector2(moveInputX * currentSpeed, 0f);
                CurrentState = Mathf.Abs(moveInputX) > 0.01f ? (isSprinting ? PlayerState.Running : PlayerState.Walking) : PlayerState.Idle;
            }
            else
            {
                CurrentState = PlayerState.Falling;
            }
        }

        private void ApplyAttackHits()
        {
            var stats = GetComponent<PlayerStats>();
            if (stats == null) return;
            float facing = IsFacingRight ? 1f : -1f;
            Vector2 center = (Vector2)transform.position + new Vector2(_attackOffset.x * facing, _attackOffset.y);
            foreach (var collider in Physics2D.OverlapBoxAll(center, _attackSize, 0f, _attackLayers))
            {
                // Only ignore this player's own colliders. Other actors can share a scene/container root.
                if (collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
                // Enemy hitboxes are often child trigger colliders. Resolve the root
                // EnemyStats first so a player hit always reaches the enemy health
                // component and raises its health-bar event.
                IDamageable target = collider.GetComponentInParent<TheLastKnight.Combat.EnemyStats>();
                if (target == null)
                {
                    target = collider.GetComponentInParent<IDamageable>();
                }
                if (target == null || !_attackTargets.Add(target)) continue;
                var parry = collider.GetComponentInParent<ParryReceiver>();
                bool critical = (parry != null && parry.IsStaggered) || Random.value * 100f < Mathf.Clamp(stats.CriticalChance, 0f, 100f);
                float damage = stats.AttackPower * (critical ? 2f : 1f) * TheLastKnight.Core.GameDifficultyManager.PlayerDamage;
                Vector2 point = collider.ClosestPoint(center);
                target.TakeDamage(new DamageData(damage, gameObject, hitPoint: point));
            }
        }

        private void StartSkill()
        {
            if (!GetComponent<PlayerStats>().TrySpendStamina(25f)) return;
            _skillTargets.Clear();
            CurrentState = PlayerState.UsingSkill;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("skill");
            _skillTimer = _skillDuration;
            _skillCooldownTimer = _skillDuration + _skillCooldown;
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("CarnageBurst", 0, 0f);
            }

            // Stop horizontal movement during skill
            _velocity.x = 0f;
        }

        private void UpdateSkill()
        {
            ApplySkillHits();
            _skillTimer -= Time.deltaTime;
            _actionDurationTimer += Time.deltaTime;

            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f)
            {
                bool newMovementInput = Mathf.Abs(_actionInitialMoveX) < 0.1f ||
                                        Mathf.Sign(_inputHandler.MoveInput.x) != Mathf.Sign(_actionInitialMoveX) ||
                                        _actionDurationTimer >= 0.12f;
                if (newMovementInput)
                {
                    CancelCurrentAction();
                    return;
                }
            }

            if (!_kinematicController.IsGrounded)
            {
                float activeGravity = _gravity;
                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            _velocity.x = 0f;
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_skillTimer <= 0f)
            {
                EndSkill();
            }
        }

        private void EndSkill()
        {
            _skillTargets.Clear();
            TransitionToMovementState();
        }

        public void ApplySkillHits()
        {
            var stats = GetComponent<PlayerStats>();
            if (stats == null) return;
            float facing = IsFacingRight ? 1f : -1f;
            Vector2 center = (Vector2)transform.position + new Vector2(_skillAttackOffset.x * facing, _skillAttackOffset.y);
            foreach (var collider in Physics2D.OverlapCircleAll(center, _skillAttackRadius, _attackLayers))
            {
                if (collider.transform == transform || collider.transform.IsChildOf(transform)) continue;

                IDamageable target = collider.GetComponentInParent<TheLastKnight.Combat.EnemyStats>();
                if (target == null)
                {
                    target = collider.GetComponentInParent<IDamageable>();
                }
                if (target == null || !_skillTargets.Add(target)) continue;
                var parry = collider.GetComponentInParent<ParryReceiver>();
                bool critical = (parry != null && parry.IsStaggered) || Random.value * 100f < Mathf.Clamp(stats.CriticalChance, 0f, 100f);
                float damage = stats.AttackPower * _skillDamageMultiplier * (critical ? 2f : 1f) * TheLastKnight.Core.GameDifficultyManager.PlayerDamage;
                Vector2 point = collider.ClosestPoint(center);

                // Calculate push / knockback direction away from the player along the skill circle
                Vector2 diff = (Vector2)collider.transform.position - (Vector2)transform.position;
                float signX = Mathf.Abs(diff.x) > 0.05f ? Mathf.Sign(diff.x) : facing;
                Vector2 knockback = new Vector2(signX * _skillKnockbackForce.x, _skillKnockbackForce.y);

                target.TakeDamage(new DamageData(damage, gameObject, DamageType.Physical, knockback, point));
                FloatingCombatText.Show(point, Mathf.CeilToInt(damage).ToString() + (critical ? "!" : ""), critical ? Color.yellow : new Color(1f, 0.45f, 0.2f));

                // Push monster outward away from player along the skill circle
                var enemyRb = collider.GetComponentInParent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    enemyRb.linearVelocity = knockback;
                    if (_skillPushDistance > 0f)
                    {
                        int groundMask = LayerMask.GetMask("Ground");
                        float pushDist = _skillPushDistance;
                        if (groundMask != 0)
                        {
                            RaycastHit2D wallCheck = Physics2D.Raycast(enemyRb.position, Vector2.right * signX, _skillPushDistance, groundMask);
                            if (wallCheck.collider != null && wallCheck.collider != collider)
                            {
                                pushDist = Mathf.Max(0f, wallCheck.distance - 0.2f);
                            }
                        }
                        enemyRb.position += new Vector2(signX * pushDist, 0f);
                        enemyRb.transform.position = enemyRb.position;
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether the specified state can be cancelled by player action.
        /// Attack is strictly NOT cancellable. Hurt is cancellable.
        /// </summary>
        public bool IsCancellableState(PlayerState state)
        {
            return state == PlayerState.UsingSkill ||
                   state == PlayerState.Buffing ||
                   state == PlayerState.Excalibur ||
                   state == PlayerState.Drinking ||
                   state == PlayerState.Dashing ||
                   state == PlayerState.Hurt;
        }

        public bool CanCancelCurrentAnimation => IsCancellableState(CurrentState);

        private void TransitionToMovementState()
        {
            if (_kinematicController.IsGrounded)
            {
                float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;
                bool isSprinting = _inputHandler != null && _inputHandler.SprintHeld;
                float currentSpeed = isSprinting ? SprintSpeed : MoveSpeed;
                _velocity = new Vector2(moveInputX * currentSpeed, 0f);
                CurrentState = Mathf.Abs(moveInputX) > 0.01f ? (isSprinting ? PlayerState.Running : PlayerState.Walking) : PlayerState.Idle;
            }
            else
            {
                CurrentState = PlayerState.Falling;
            }
        }

        /// <summary>
        /// Cancels the current cancellable action/animation cleanly.
        /// Attack cannot be cancelled.
        /// </summary>
        public void CancelCurrentAction()
        {
            if (CurrentState == PlayerState.Excalibur)
            {
                CancelExcalibur();
            }
            else if (CurrentState == PlayerState.UsingSkill)
            {
                _skillTimer = 0f;
                _skillTargets.Clear();
                TransitionToMovementState();
            }
            else if (CurrentState == PlayerState.Buffing)
            {
                _buffTimer = 0f;
                var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
                if (stats != null)
                {
                    stats.RemoveSkill2Buff();
                }
                TransitionToMovementState();
            }
            else if (CurrentState == PlayerState.Drinking)
            {
                _drinkTimer = 0f;
                _drinkingSlot = -1;
                _drinkingItem = null;
                TransitionToMovementState();
            }
            else if (CurrentState == PlayerState.Dashing)
            {
                EndDash();
            }
            else if (CurrentState == PlayerState.Hurt)
            {
                _hurtTimer = 0f;
                TransitionToMovementState();
            }

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (CurrentState == PlayerState.Idle)
                {
                    _animator.Play("Idle", 0, 0f);
                }
            }
        }

        private void StartBuff()
        {
            CurrentState = PlayerState.Buffing;
            _buffTimer = _buffDuration;
            _buffCooldownTimer = _buffCooldown;
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("skill");

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Buff", 0, 0f);
            }

            // Stop horizontal movement during Buff
            _velocity.x = 0f;
        }

        private void UpdateBuff()
        {
            _buffTimer -= Time.deltaTime;
            _actionDurationTimer += Time.deltaTime;

            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f)
            {
                bool newMovementInput = Mathf.Abs(_actionInitialMoveX) < 0.1f ||
                                        Mathf.Sign(_inputHandler.MoveInput.x) != Mathf.Sign(_actionInitialMoveX) ||
                                        _actionDurationTimer >= 0.12f;
                if (newMovementInput)
                {
                    CancelCurrentAction();
                    return;
                }
            }

            if (!_kinematicController.IsGrounded)
            {
                float activeGravity = _gravity;
                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            _velocity.x = 0f;
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_buffTimer <= 0f)
            {
                EndBuff();
            }
        }

        private void EndBuff()
        {
            var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
            if (stats != null)
            {
                stats.ApplySkill2Buff();
            }
            TransitionToMovementState();
        }

        private void StartExcalibur()
        {
            if (!GetComponent<PlayerStats>().TrySpendStamina(50f)) return;
            _excaliburTargets.Clear();
            CurrentState = PlayerState.Excalibur;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("excalibur");
            _excaliburTimer = _excaliburDuration;
            _excaliburCooldownTimer = _excaliburDuration + _excaliburCooldown;
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Excalibur", 0, 0f);
            }

            if (_excaliburVFXController != null)
            {
                _excaliburVFXController.ExecuteUltimateAttack();
            }

            // Stop horizontal movement during Excalibur
            _velocity.x = 0f;
        }

        private void UpdateExcalibur()
        {
            _actionDurationTimer += Time.deltaTime;

            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f)
            {
                bool newMovementInput = Mathf.Abs(_actionInitialMoveX) < 0.1f ||
                                        Mathf.Sign(_inputHandler.MoveInput.x) != Mathf.Sign(_actionInitialMoveX) ||
                                        _actionDurationTimer >= 0.12f;
                if (newMovementInput)
                {
                    CancelCurrentAction();
                    return;
                }
            }

            // Apply beam damage during the beam release window
            if (_excaliburTimer <= (_excaliburDuration - _excaliburDamageDelay))
            {
                ApplyExcaliburHits();
            }

            _excaliburTimer -= Time.deltaTime;

            if (!_kinematicController.IsGrounded)
            {
                float activeGravity = _gravity;
                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            _velocity.x = 0f;
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_excaliburTimer <= 0f)
            {
                EndExcalibur();
            }
        }

        private void EndExcalibur()
        {
            _excaliburTargets.Clear();
            TransitionToMovementState();
        }

        private int _drinkingSlot = -1;
        private TheLastKnight.Inventory.InventoryItemData _drinkingItem;

        /// <summary>
        /// Cancels Excalibur immediately, removing all VFX, active beams, particles, and charging effects.
        /// </summary>
        public void CancelExcalibur()
        {
            if (CurrentState != PlayerState.Excalibur) return;

            _excaliburTargets.Clear();
            _excaliburTimer = 0f;

            if (_excaliburVFXController != null)
            {
                _excaliburVFXController.CancelUltimateAttack();
            }

            foreach (var vfx in GetComponentsInChildren<ExcaliburVFXController>(true))
            {
                if (vfx != null) vfx.CancelUltimateAttack();
            }

            TransitionToMovementState();
        }

        public void ApplyExcaliburHits()
        {
            var stats = GetComponent<PlayerStats>();
            if (stats == null) return;
            float facing = IsFacingRight ? 1f : -1f;
            Vector2 center = (Vector2)transform.position + new Vector2(_excaliburHitOffset.x * facing, _excaliburHitOffset.y);

            foreach (var collider in Physics2D.OverlapBoxAll(center, _excaliburHitSize, 0f, _attackLayers))
            {
                if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform)) continue;
                IDamageable target = collider.GetComponentInParent<TheLastKnight.Combat.EnemyStats>();
                if (target == null)
                {
                    target = collider.GetComponentInParent<IDamageable>();
                }
                if (target == null || !_excaliburTargets.Add(target)) continue;
                var parry = collider.GetComponentInParent<ParryReceiver>();
                bool critical = (parry != null && parry.IsStaggered) || Random.value * 100f < Mathf.Clamp(stats.CriticalChance, 0f, 100f);
                float damage = stats.AttackPower * _excaliburDamageMultiplier * (critical ? 2f : 1f) * TheLastKnight.Core.GameDifficultyManager.PlayerDamage;
                Vector2 point = collider.ClosestPoint(center);
                target.TakeDamage(new DamageData(damage, gameObject, hitPoint: point));
                FloatingCombatText.Show(point, Mathf.CeilToInt(damage).ToString() + (critical ? "!" : ""), Color.yellow);

                // Apply Stun for _excaliburStunDuration (configurable in Inspector, default 2s)
                if (_excaliburStunDuration > 0f)
                {
                    var enemyStats = collider.GetComponentInParent<EnemyStats>();
                    if (enemyStats != null)
                    {
                        enemyStats.ApplyStatus(StatusEffect.Stunned, _excaliburStunDuration);
                        FloatingCombatText.Show(point + Vector2.up * 0.7f, "STUNNED!", Color.cyan);
                    }
                }
            }
        }

        private void StartDrink()
        {
            var stats = GetComponent<PlayerStats>();
            var qm = TheLastKnight.Core.QuickItemManager.Instance;
            var inventory = TheLastKnight.Inventory.InventoryManager.Instance;
            int index = qm != null ? qm.GetActiveSlotIndex() : -1;
            if (inventory == null || !inventory.CanUseQuickSlot(index, stats)) return;
            _drinkingSlot = index;
            _drinkingItem = inventory.GetSlot(TheLastKnight.Inventory.SlotType.QuickSlot, index);
            CurrentState = PlayerState.Drinking;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("drink");
            _drinkTimer = _drinkDuration;
            _drinkCooldownTimer = _drinkDuration + _drinkCooldown;
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Drink", 0, 0f);
            }

            // Stop horizontal movement during drink
            _velocity.x = 0f;
        }

        private void UpdateDrink()
        {
            _drinkTimer -= Time.deltaTime;
            _actionDurationTimer += Time.deltaTime;

            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f)
            {
                bool newMovementInput = Mathf.Abs(_actionInitialMoveX) < 0.1f ||
                                        Mathf.Sign(_inputHandler.MoveInput.x) != Mathf.Sign(_actionInitialMoveX) ||
                                        _actionDurationTimer >= 0.12f;
                if (newMovementInput)
                {
                    CancelCurrentAction();
                    return;
                }
            }

            if (!_kinematicController.IsGrounded)
            {
                float activeGravity = _gravity;
                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            _velocity.x = 0f;
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_drinkTimer <= 0f)
            {
                EndDrink();
            }
        }

        private void EndDrink()
        {
            var stats = GetComponent<PlayerStats>();
            var inventory = TheLastKnight.Inventory.InventoryManager.Instance;
            // Consume the stack selected at the start, not a replacement moved during the animation.
            if (inventory != null && _drinkingItem != null &&
                ReferenceEquals(inventory.GetSlot(TheLastKnight.Inventory.SlotType.QuickSlot, _drinkingSlot), _drinkingItem))
                inventory.UseQuickSlot(_drinkingSlot, stats);
            _drinkingSlot = -1;
            _drinkingItem = null;
            TransitionToMovementState();
        }

        /// <summary>
        /// Triggers Arthur's hurt reaction animation.
        /// If damage is taken continuously, resets/holds frame 1 until damage stops.
        /// </summary>
        public void OnTakeDamage()
        {
            // Do not interrupt ultimate skill (Excalibur), normal skill (Carnage Burst), or dash with hurt reaction
            if (CurrentState == PlayerState.Excalibur || CurrentState == PlayerState.UsingSkill || CurrentState == PlayerState.Dashing)
            {
                return;
            }

            if (CurrentState == PlayerState.Buffing)
            {
                _buffTimer = 0f;
                var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
                if (stats != null)
                {
                    stats.RemoveSkill2Buff();
                }
            }
            else if (CurrentState == PlayerState.Drinking)
            {
                _drinkTimer = 0f;
                _drinkingSlot = -1;
                _drinkingItem = null;
            }

            CurrentState = PlayerState.Hurt;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("hurt");
            _hurtTimer = _hurtFrame1Duration + _hurtFrame2Duration;
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Hurt", 0, 0f);
            }
        }

        /// <summary>
        /// Applies a stun effect to Arthur. Cancels Excalibur or other channeled skills immediately,
        /// removes all Excalibur VFX, and puts Arthur into the Hurt/Stunned state for the specified duration.
        /// </summary>
        public void ApplyStun(float duration = 1.0f)
        {
            if (AdminStatusImmunity) return;
            if (CurrentState == PlayerState.Excalibur)
            {
                CancelExcalibur();
            }
            else if (CurrentState == PlayerState.UsingSkill)
            {
                _skillTimer = 0f;
                _skillTargets.Clear();
            }
            else if (CurrentState == PlayerState.Buffing)
            {
                _buffTimer = 0f;
                var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
                if (stats != null)
                {
                    stats.RemoveSkill2Buff();
                }
            }
            else if (CurrentState == PlayerState.Drinking)
            {
                _drinkTimer = 0f;
                _drinkingSlot = -1;
                _drinkingItem = null;
            }

            CurrentState = PlayerState.Hurt;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("hurt");
            _hurtTimer = Mathf.Max(duration, _hurtFrame1Duration + _hurtFrame2Duration);
            _actionDurationTimer = 0f;
            _actionInitialMoveX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Hurt", 0, 0f);
            }
        }

        public void ApplyStatus(TheLastKnight.Combat.StatusEffect effect, float duration)
        {
            if (AdminStatusImmunity) return;
            if (effect == TheLastKnight.Combat.StatusEffect.Stunned)
            {
                ApplyStun(duration);
            }
        }

        private void UpdateHurt()
        {
            _hurtTimer -= Time.deltaTime;
            _actionDurationTimer += Time.deltaTime;

            if (_inputHandler != null && Mathf.Abs(_inputHandler.MoveInput.x) > 0.1f)
            {
                bool newMovementInput = Mathf.Abs(_actionInitialMoveX) < 0.1f ||
                                        Mathf.Sign(_inputHandler.MoveInput.x) != Mathf.Sign(_actionInitialMoveX) ||
                                        _actionDurationTimer >= 0.08f;
                if (newMovementInput)
                {
                    CancelCurrentAction();
                    return;
                }
            }

            if (!_kinematicController.IsGrounded)
            {
                _velocity.y -= _gravity * _fallMultiplier * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }
            else
            {
                _velocity.y = 0f;
            }

            _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, _deceleration * Time.deltaTime);
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_hurtTimer <= 0f)
            {
                EndHurt();
            }
        }

        private void EndHurt()
        {
            _hurtTimer = 0f;
            if (_kinematicController.IsGrounded)
            {
                float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;
                bool isSprinting = _inputHandler != null && _inputHandler.SprintHeld;
                float currentSpeed = isSprinting ? SprintSpeed : MoveSpeed;
                _velocity = new Vector2(moveInputX * currentSpeed, 0f);
                CurrentState = Mathf.Abs(moveInputX) > 0.01f ? (isSprinting ? PlayerState.Running : PlayerState.Walking) : PlayerState.Idle;
            }
            else
            {
                CurrentState = PlayerState.Falling;
            }
        }

        private void UpdateNormalMovement()
        {
            float moveInputX = _inputHandler != null ? _inputHandler.MoveInput.x : 0f;

            // Flip/Facing Logic
            if (moveInputX > 0.01f)
            {
                IsFacingRight = true;
                if (_spriteRenderer != null) _spriteRenderer.flipX = false;
            }
            else if (moveInputX < -0.01f)
            {
                IsFacingRight = false;
                if (_spriteRenderer != null) _spriteRenderer.flipX = true;
            }

            // Horizontal Movement with acceleration/deceleration
            bool isSprinting = _inputHandler != null && _inputHandler.SprintHeld;
            float currentMoveSpeed = isSprinting ? SprintSpeed : MoveSpeed;
            if (isSprinting && Mathf.Abs(moveInputX) > 0.01f && !GetComponent<PlayerStats>().TrySpendStamina(15f * Time.deltaTime))
            {
                isSprinting = false;
                currentMoveSpeed = MoveSpeed;
            }
            float targetXSpeed = moveInputX * currentMoveSpeed;
            float accelRate = Mathf.Abs(targetXSpeed) > 0.01f ? _acceleration : _deceleration;
            _velocity.x = Mathf.MoveTowards(_velocity.x, targetXSpeed, accelRate * Time.deltaTime);

            // Vertical Movement & Gravity
            if (_kinematicController.IsGrounded)
            {
                _velocity.y = 0f;
            }
            else
            {
                // Gravity scale based on jump release (Variable Jump) or falling
                float activeGravity = _gravity;
                if (_velocity.y > 0f)
                {
                    if (_inputHandler != null && !_inputHandler.JumpHeld)
                    {
                        // Player released jump button early -> rise slower (fall faster)
                        activeGravity *= _jumpCutMultiplier;
                    }
                }
                else
                {
                    // Snappy falling gravity multiplier
                    activeGravity *= _fallMultiplier;
                }

                _velocity.y -= activeGravity * Time.deltaTime;
                _velocity.y = Mathf.Max(_velocity.y, -_maxFallSpeed);
            }

            // Jump mechanics (Coyote Time + Jump Buffering + Double Jump + Platform Drop Down)
            bool jumpRequested = _jumpBufferCounter > 0f;
            bool canJump = _coyoteTimeCounter > 0f;
            bool isDownPressed = _inputHandler != null && _inputHandler.MoveInput.y < -0.5f;

            if (jumpRequested)
            {
                Collider2D standingPlat = GetStandingOneWayPlatform();
                if (isDownPressed && _kinematicController.IsGrounded && standingPlat != null)
                {
                    _platformDropTimer = _platformDropDuration;
                    _droppedPlatform = standingPlat;
                    if (_kinematicController != null)
                    {
                        _kinematicController.IgnoredOneWayPlatform = standingPlat;
                    }
                    _velocity.y = -2.5f;
                    _jumpBufferCounter = -1f;
                    _coyoteTimeCounter = -1f;
                    CurrentState = PlayerState.Falling;
                }
                else if (canJump)
                {
                    var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
                    if (stats == null || stats.TrySpendStamina(15f))
                    {
                        TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("jump");
                        _velocity.y = JumpForce;
                        _coyoteTimeCounter = -1f;
                        _hasDoubleJumped = false;
                        CurrentState = PlayerState.Jumping;
                    }
                    _jumpBufferCounter = -1f;
                }
                else if (CanDoubleJump && !_hasDoubleJumped)
                {
                    var stats = GetComponent<TheLastKnight.Stats.PlayerStats>();
                    if (stats == null || stats.TrySpendStamina(15f))
                    {
                        TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("jump");
                        _velocity.y = JumpForce;
                        _hasDoubleJumped = true;
                        CurrentState = PlayerState.Jumping;
                        if (_animator != null && _animator.runtimeAnimatorController != null)
                        {
                            _animator.Play("Jump", 0, 0f);
                        }
                    }
                    _jumpBufferCounter = -1f;
                }
            }

            // Ceiling collision check - instantly stop vertical rising momentum
            if (_kinematicController.HitCeiling && _velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            // Move the controller
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_kinematicController.HitCeiling && _velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            if (_kinematicController.IsGrounded && _velocity.y < 0f)
            {
                _velocity.y = 0f;
            }

            // Update States based on grounded status and movement
            if (_kinematicController.IsGrounded)
            {
                if (Mathf.Abs(_velocity.x) > 0.01f)
                {
                    CurrentState = isSprinting ? PlayerState.Running : PlayerState.Walking;
                }
                else
                {
                    CurrentState = PlayerState.Idle;
                }
            }
            else
            {
                if (_velocity.y > 0f)
                {
                    CurrentState = PlayerState.Jumping;
                }
                else
                {
                    CurrentState = PlayerState.Falling;
                }
            }
        }

        private void LateUpdate()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            bool isIdle = CurrentState == PlayerState.Idle;
            bool isWalking = CurrentState == PlayerState.Walking;
            bool isRunningState = CurrentState == PlayerState.Running;
            bool isMoving = isWalking || isRunningState;
            bool isJumping = CurrentState == PlayerState.Jumping;
            bool isDashing = CurrentState == PlayerState.Dashing;
            bool isAttacking = CurrentState == PlayerState.Attacking;
            bool isUsingSkill = CurrentState == PlayerState.UsingSkill;
            bool isBuffing = CurrentState == PlayerState.Buffing;
            bool isExcalibur = CurrentState == PlayerState.Excalibur;
            bool isDrinking = CurrentState == PlayerState.Drinking;
            bool isHurt = CurrentState == PlayerState.Hurt;

            _animator.SetBool("IsIdle", isIdle);
            _animator.SetBool("IsRunning", isMoving);
            _animator.SetBool("IsJumping", isJumping);
            _animator.SetBool("IsDashing", isDashing);
            _animator.SetBool("IsAttacking", isAttacking);
            _animator.SetBool("UseCarnageBurst", isUsingSkill);
            _animator.SetBool("UseBuff", isBuffing);
            _animator.SetBool("UseExcalibur", isExcalibur);
            _animator.SetBool("UseDrink", isDrinking);
            _animator.SetBool("IsHurt", isHurt);

            // Play explicit animation clips for Walk vs Run state
            if (isRunningState)
            {
                if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Run"))
                {
                    _animator.Play("Run");
                }
            }
            else if (isWalking)
            {
                if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") &&
                    !_animator.IsInTransition(0))
                {
                    _animator.Play("Walk");
                }
            }

            if (isAttacking)
            {
                float atkSpd = AttackSpeedMultiplier > 0.1f ? AttackSpeedMultiplier : 1.0f;
                _animator.speed = atkSpd;
            }
            else
            {
                _animator.speed = 1.0f;
            }

            // Visual feedback for dash i-frames
            if (_spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                if (IsInvincible && CurrentState == PlayerState.Dashing)
                {
                    c.a = 0.65f;
                }
                else
                {
                    c.a = 1.0f;
                }
                _spriteRenderer.color = c;
            }
        }

        private void CollectNearbyEnemyColliders()
        {
            if (_playerCollider == null) _playerCollider = GetComponent<Collider2D>();
            if (_playerCollider == null) return;

            var colliders = Physics2D.OverlapCircleAll(transform.position, 2.5f);
            foreach (var col in colliders)
            {
                if (col != null && col != _playerCollider && !col.isTrigger && KinematicCharacterController2D.IsEnemyCollider(col))
                {
                    if (!_ignoredEnemyColliders.Contains(col))
                    {
                        Physics2D.IgnoreCollision(_playerCollider, col, true);
                        _ignoredEnemyColliders.Add(col);
                    }
                }
            }
        }

        private void IgnoreEnemyCollisions(bool ignore)
        {
            if (_playerCollider == null) _playerCollider = GetComponent<Collider2D>();
            if (_playerCollider == null) return;

            int playerLayer = gameObject.layer;
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignore);
            }

            if (ignore)
            {
                _ignoredEnemyColliders.Clear();
                var colliders = Physics2D.OverlapCircleAll(transform.position, DashSpeed * DashDuration + 5f);
                foreach (var col in colliders)
                {
                    if (col != null && col != _playerCollider && !col.isTrigger && KinematicCharacterController2D.IsEnemyCollider(col))
                    {
                        Physics2D.IgnoreCollision(_playerCollider, col, true);
                        _ignoredEnemyColliders.Add(col);
                    }
                }
            }
            else
            {
                CleanupIgnoredColliders();
            }
        }

        private void CleanupIgnoredColliders()
        {
            if (_playerCollider == null) _playerCollider = GetComponent<Collider2D>();
            if (_playerCollider == null || _ignoredEnemyColliders.Count == 0) return;

            for (int i = _ignoredEnemyColliders.Count - 1; i >= 0; i--)
            {
                var col = _ignoredEnemyColliders[i];
                if (col == null)
                {
                    _ignoredEnemyColliders.RemoveAt(i);
                    continue;
                }

                // If still overlapping with enemy while not dashing, wait until bounds separate
                if (CurrentState != PlayerState.Dashing && _playerCollider.bounds.Intersects(col.bounds))
                {
                    continue;
                }

                if (CurrentState != PlayerState.Dashing)
                {
                    Physics2D.IgnoreCollision(_playerCollider, col, false);
                    _ignoredEnemyColliders.RemoveAt(i);
                }
            }
        }

        private void OnDisable()
        {
            _dashInvincible = false;
            _dashIFrameTimer = 0f;
            if (_kinematicController != null)
            {
                _kinematicController.IgnoreEnemies = false;
            }
            if (_playerCollider != null)
            {
                int playerLayer = gameObject.layer;
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer != -1)
                {
                    Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
                }
                foreach (var col in _ignoredEnemyColliders)
                {
                    if (col != null) Physics2D.IgnoreCollision(_playerCollider, col, false);
                }
                _ignoredEnemyColliders.Clear();
            }
        }

        private Collider2D GetStandingOneWayPlatform()
        {
            if (_playerCollider == null) _playerCollider = GetComponent<Collider2D>();
            if (_playerCollider == null) return null;
            Vector2 boxCenter = new Vector2(_playerCollider.bounds.center.x, _playerCollider.bounds.min.y);
            Vector2 boxSize = new Vector2(_playerCollider.bounds.size.x * 0.9f, 0.1f);
            RaycastHit2D[] hits = Physics2D.BoxCastAll(boxCenter, boxSize, 0f, Vector2.down, 0.25f);
            foreach (var h in hits)
            {
                if (h.collider != null && h.collider != _playerCollider && KinematicCharacterController2D.IsOneWayPlatform(h.collider))
                {
                    return h.collider;
                }
            }
            return null;
        }

        private bool IsStandingOnOneWayPlatform()
        {
            return GetStandingOneWayPlatform() != null;
        }

        private void OnDrawGizmosSelected()
        {
            float facing = IsFacingRight ? 1f : -1f;

            // Attack Hitbox (Red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Vector2 attackCenter = (Vector2)transform.position + new Vector2(_attackOffset.x * facing, _attackOffset.y);
            Gizmos.DrawWireCube(attackCenter, _attackSize);

            // Skill 1 Carnage Burst Hitbox (Orange Circle)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Vector2 skillCenter = (Vector2)transform.position + new Vector2(_skillAttackOffset.x * facing, _skillAttackOffset.y);
            Gizmos.DrawWireSphere(skillCenter, _skillAttackRadius);

            // Skill 3 Excalibur Hitbox (Gold / Yellow)
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.4f);
            Vector2 excaliburCenter = (Vector2)transform.position + new Vector2(_excaliburHitOffset.x * facing, _excaliburHitOffset.y);
            Gizmos.DrawWireCube(excaliburCenter, _excaliburHitSize);
        }
    }
}
