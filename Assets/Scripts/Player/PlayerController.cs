using UnityEngine;
using TheLastKnight.Input;
using TheLastKnight.Physics;

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

        [Header("Skill Settings (Carnage Burst - Key E)")]
        [SerializeField, Tooltip("Carnage Burst skill duration.")]
        private float _skillDuration = 0.5f;
        [SerializeField, Tooltip("Cooldown between skill uses.")]
        private float _skillCooldown = 1.0f;

        [Header("Buff Settings (Key R)")]
        [SerializeField, Tooltip("Buff skill duration.")]
        private float _buffDuration = 0.75f;
        [SerializeField, Tooltip("Cooldown between Buff uses.")]
        private float _buffCooldown = 1.0f;

        [Header("Excalibur Settings (Key T)")]
        [SerializeField, Tooltip("Excalibur skill duration.")]
        private float _excaliburDuration = 3.8f;
        [SerializeField, Tooltip("Cooldown between Excalibur uses.")]
        private float _excaliburCooldown = 1.0f;

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

        // Components
        private KinematicCharacterController2D _kinematicController;
        private SpriteRenderer _spriteRenderer;

        // Current Active Scaled Stats (Step 7 Preparation)
        public float MoveSpeed { get; set; }
        public float SprintSpeed { get; set; }
        public float JumpForce { get; set; }
        public float DashSpeed { get; set; }
        public float DashDuration { get; set; }

        public float BaseMoveSpeed => _baseMoveSpeed;
        public float BaseSprintSpeed => _baseSprintSpeed;
        public float BaseJumpForce => _baseJumpForce;
        public float BaseDashSpeed => _baseDashSpeed;
        public float BaseDashDuration => _baseDashDuration;

        // State Machine
        public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        // Dash State Variables
        public bool IsInvincible { get; private set; } = false;
        private float _dashTimer = 0f;
        private float _dashCooldownTimer = 0f;
        private bool _hasDashedInAir = false;
        private Vector2 _dashDirection = Vector2.right;

        // Jump & Coyote Timers
        private float _jumpBufferCounter = -1f;
        private float _coyoteTimeCounter = -1f;

        // Movement variables
        private Vector2 _velocity;
        public bool IsFacingRight { get; private set; } = true;

        // Attack State Variables
        private float _attackTimer = 0f;
        private float _attackCooldownTimer = 0f;
        private bool _isAttacking = false;

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

        private void Awake()
        {
            _kinematicController = GetComponent<KinematicCharacterController2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();

            if (_inputHandler == null)
            {
                _inputHandler = GetComponent<PlayerInputHandler>();
                if (_inputHandler == null)
                {
                    _inputHandler = FindAnyObjectByType<PlayerInputHandler>();
                }
            }

            // Initialize active movement stats from base values
            MoveSpeed = _baseMoveSpeed;
            SprintSpeed = _baseSprintSpeed;
            JumpForce = _baseJumpForce;
            DashSpeed = _baseDashSpeed;
            DashDuration = _baseDashDuration;
        }

        private void Update()
        {
            // Update Dash Cooldown
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= Time.deltaTime;
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

            // Coyote time update
            if (_kinematicController.IsGrounded)
            {
                _coyoteTimeCounter = _coyoteTime;
                _hasDashedInAir = false; // Reset air dash
            }
            else
            {
                _coyoteTimeCounter -= Time.deltaTime;
            }

            // Jump buffer update
            if (_inputHandler != null && _inputHandler.JumpTriggered)
            {
                _jumpBufferCounter = _jumpBufferTime;
            }
            else
            {
                _jumpBufferCounter -= Time.deltaTime;
            }

            bool isBusy = CurrentState == PlayerState.Hurt || CurrentState == PlayerState.Attacking || CurrentState == PlayerState.UsingSkill || CurrentState == PlayerState.Buffing || CurrentState == PlayerState.Excalibur || CurrentState == PlayerState.Dashing || CurrentState == PlayerState.Drinking;

            // Check for Attack Trigger
            if (_inputHandler != null && _inputHandler.AttackTriggered && !isBusy && _attackCooldownTimer <= 0f)
            {
                StartAttack();
            }

            // Check for Skill Trigger (Carnage Burst - Key E)
            if (_inputHandler != null && _inputHandler.UseSkillTriggered && !isBusy && _skillCooldownTimer <= 0f)
            {
                StartSkill();
            }

            // Check for Buff Trigger (Key R)
            if (_inputHandler != null && _inputHandler.UseBuffTriggered && !isBusy && _buffCooldownTimer <= 0f)
            {
                StartBuff();
            }

            // Check for Excalibur Trigger (Key T)
            if (_inputHandler != null && _inputHandler.UseExcaliburTriggered && !isBusy && _excaliburCooldownTimer <= 0f)
            {
                StartExcalibur();
            }

            // Check for Drink Trigger (Key Q)
            if (_inputHandler != null && _inputHandler.UseDrinkTriggered && !isBusy && _drinkCooldownTimer <= 0f)
            {
                StartDrink();
            }

            // Check for Dash Trigger
            if (_inputHandler != null && _inputHandler.DashTriggered && _dashCooldownTimer <= 0f && CurrentState != PlayerState.Hurt)
            {
                bool canDash = _kinematicController.IsGrounded || !_hasDashedInAir;
                if (canDash)
                {
                    StartDash();
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
            CurrentState = PlayerState.Dashing;
            IsInvincible = true;
            _dashTimer = DashDuration;
            _dashCooldownTimer = _dashCooldown;

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

            // Move character
            _kinematicController.Move(_velocity, Time.deltaTime);

            if (_dashTimer <= 0f)
            {
                EndDash();
            }
        }

        private void EndDash()
        {
            IsInvincible = false;

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
            CurrentState = PlayerState.Attacking;
            _isAttacking = true;
            _attackTimer = _attackDuration;
            _attackCooldownTimer = _attackDuration + _attackCooldown;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Attack", 0, 0f);
            }

            // Stop horizontal movement during attack
            _velocity.x = 0f;
        }

        private void UpdateAttack()
        {
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

        private void StartSkill()
        {
            CurrentState = PlayerState.UsingSkill;
            _skillTimer = _skillDuration;
            _skillCooldownTimer = _skillDuration + _skillCooldown;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("CarnageBurst", 0, 0f);
            }

            // Stop horizontal movement during skill
            _velocity.x = 0f;
        }

        private void UpdateSkill()
        {
            _skillTimer -= Time.deltaTime;

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

        private void StartBuff()
        {
            CurrentState = PlayerState.Buffing;
            _buffTimer = _buffDuration;
            _buffCooldownTimer = _buffDuration + _buffCooldown;

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

        private void StartExcalibur()
        {
            CurrentState = PlayerState.Excalibur;
            _excaliburTimer = _excaliburDuration;
            _excaliburCooldownTimer = _excaliburDuration + _excaliburCooldown;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Excalibur", 0, 0f);
            }

            // Stop horizontal movement during Excalibur
            _velocity.x = 0f;
        }

        private void UpdateExcalibur()
        {
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

        private void StartDrink()
        {
            CurrentState = PlayerState.Drinking;
            _drinkTimer = _drinkDuration;
            _drinkCooldownTimer = _drinkDuration + _drinkCooldown;

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
        /// Triggers Arthur's hurt reaction animation.
        /// If damage is taken continuously, resets/holds frame 1 until damage stops.
        /// </summary>
        public void OnTakeDamage()
        {
            CurrentState = PlayerState.Hurt;
            _hurtTimer = _hurtFrame1Duration + _hurtFrame2Duration;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Hurt", 0, 0f);
            }
        }

        private void UpdateHurt()
        {
            _hurtTimer -= Time.deltaTime;

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

            // Jump mechanics (Coyote Time + Jump Buffering)
            bool jumpRequested = _jumpBufferCounter > 0f;
            bool canJump = _coyoteTimeCounter > 0f;

            if (jumpRequested && canJump)
            {
                _velocity.y = JumpForce;
                _jumpBufferCounter = -1f;
                _coyoteTimeCounter = -1f;
                CurrentState = PlayerState.Jumping;
            }

            // Ceiling collision check - instantly stop vertical rising momentum
            if (_kinematicController.HitCeiling && _velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            // Move the controller
            _kinematicController.Move(_velocity, Time.deltaTime);

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

            _animator.speed = 1.0f;
        }
    }
}
