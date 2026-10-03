using System.Collections;
using UnityEngine;
using TheLastKnight.Combat;
using TheLastKnight.Combat.Projectiles;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(EnemyStats))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _chaseSpeed = 3.8f;
        [Tooltip("Patrol distance walking shortly around the spawn point.")]
        [SerializeField] private float _patrolDistance = 3f;
        [SerializeField] private bool _isFlying = false;
        [Tooltip("Vertical offset from the target collider center while chasing as a flying enemy.")]
        [SerializeField] private float _flyingChaseHeightOffset;
        [Tooltip("Vertical adjustment applied once to this flying enemy's spawn and patrol height.")]
        [SerializeField] private float _flyingIdleHeightOffset;
        [SerializeField] private bool _avoidLedges = true;
        [SerializeField] private bool _initialFacingRight = true;
        [Tooltip("Flip only the sprite instead of mirroring the enemy root and its attached UI/physics.")]
        [SerializeField] private bool _flipSpriteInsteadOfTransformScale;
        [Tooltip("Ignore tiny horizontal target changes to prevent rapid left/right facing flicker.")]
        [SerializeField, Min(0f)] private float _facingFlipDeadZone;
        [Tooltip("For enemies that need it, stop safely instead of snapping to the spawn point when the return path is blocked.")]
        [SerializeField] private bool _avoidTeleportOnReturn;

        [Header("Boss & Leash Settings")]
        [Tooltip("If true, this monster is considered a Boss and will not leash/return to spawn or reset HP when player runs far away.")]
        [SerializeField] private bool _isBoss = false;
        public bool IsBoss => _isBoss;
        public float DetectionRange => _detectionRange;
        [Tooltip("Multiplier of detection range used as the leash boundary from spawn point (default: 2.0x).")]
        [SerializeField] private float _leashRangeMultiplier = 2.0f;

        [Header("Obstacle & Ground Detection")]
        [SerializeField] private LayerMask _groundLayer = ~0;
        [SerializeField] private float _groundCheckDistance = 1.2f;
        [SerializeField] private float _wallCheckDistance = 0.6f;
        [SerializeField] private float _ledgeForwardOffset = 0.5f;

        [Header("Combat Ranges")]
        [SerializeField, Min(0f)] private float _detectionRange = 7f;
        [SerializeField] private float _meleeRange = 1.4f;
        [SerializeField] private float _meleeCooldown = 1.5f;
        [Tooltip("Measure attack range between the nearest edges of the enemy and player solid colliders.")]
        [SerializeField] private bool _useColliderEdgeAttackDistance;
        [Tooltip("Require contact skills to start at melee distance even when their configured maximum range is larger.")]
        [SerializeField] private bool _requireCloseRangeForContactSkills;
        [Tooltip("Play named attack states directly instead of sharing an Attack trigger and stale ActionIndex.")]
        [SerializeField] private bool _playAttackStatesDirectly;
        [Tooltip("Play authored Idle, Walk, Run, Jump, Land and Hit states from movement (Moonstone Keeper).")]
        [SerializeField] private bool _useMovementAnimationStates;
        [Tooltip("Use passive patrol, weapon draw, and hostile movement animations (Reaper).")]
        [SerializeField] private bool _usePassiveStanceAnimations;
        [Tooltip("Measure attack and skill ranges from the nearest enabled non-trigger collider edges.")]
        [SerializeField] private bool _useColliderEdgeAttackRanges;
        [Tooltip("When enabled, this enemy waits for configured skills instead of using the basic melee attack.")]
        [SerializeField] private bool _disableBasicAttack;

        [Header("Ranged Combat")]
        [SerializeField] private bool _hasRangedAttack = false;
        [SerializeField] private float _rangedRange = 8f;
        [SerializeField] private float _rangedCooldown = 3.0f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Vector2 _projectileSpawnOffset = new Vector2(0.6f, 0.2f);
        [Tooltip("Use the authored local mouth offset instead of the animated sprite edge for projectiles.")]
        [SerializeField] private bool _useProjectileSpawnOffset;
        [SerializeField] private GameObject _groundSpellPrefab;

        [Header("Basic Attack & Parry Settings")]
        [SerializeField] private string _basicAttackAnimState = "Attack";
        [Tooltip("Optional repeating animation sequence for basic melee attacks.")]
        [SerializeField] private string[] _basicAttackAnimStates = new string[0];
        [Tooltip("Delay from the attack animation start to the visible damage frame.")]
        [SerializeField, Min(0f)] private float _basicAttackDamageDelay;
        [SerializeField, Min(0f)] private float _basicAttackActionDuration;
        [Tooltip("Per-animation delay from attack animation start to the damage frame; indices match Basic Attack Anim States.")]
        [SerializeField] private float[] _basicAttackDamageStartDelays = new float[0];
        private int _nextBasicAttackAnimIndex;
        [Tooltip("Damage multiplier for basic attack (always 1.0x ATK).")]
        [SerializeField] private float _basicAttackMultiplier = 1.0f;
        [Tooltip("If true and this monster has no parryable skills, basic attack triggers the Parry timing ring with a cooldown.")]
        [SerializeField] private bool _basicAttackCanParry = true;
        [Tooltip("Allow the basic Attack to open a Parry ring even when this monster also has a parryable skill.")]
        [SerializeField] private bool _allowBasicParryWithSkills;
        [Tooltip("Minimum cooldown in seconds between Parry rings on basic attack (minimum 4.0s).")]
        [SerializeField] private float _basicParryCooldown = 4.0f;
        [Tooltip("When greater than zero, show the basic attack parry ring once every N attacks, without a time cooldown.")]
        [SerializeField, Min(0)] private int _basicParryEveryNAttacks;
        [Tooltip("On every Nth basic melee opportunity, use the named guard skill instead of attacking. Zero disables this cadence.")]
        [SerializeField, Min(0)] private int _basicGuardEveryNAttacks;
        [SerializeField] private string _basicGuardSkillName;

        [Header("Skills Configuration")]
        [Tooltip("Special attacks and skills configured from the monster's Animation Controller.")]
        [SerializeField] private TheLastKnight.Combat.EnemySkill[] _skills = new TheLastKnight.Combat.EnemySkill[0];
        public TheLastKnight.Combat.EnemySkill[] Skills => _skills;
        [SerializeField] private bool _cycleNonParryableSkills;
        [SerializeField] private bool _waitForAttackAnimationToFinish;
        [SerializeField] private bool _waitForAttackProjectileToFinish;
        [Tooltip("Cancel the current attack and play TakeHit when this enemy takes damage.")]
        [SerializeField] private bool _interruptAttackOnDamage;
        [Tooltip("Run the configured cycle without anticipation gaps and finish actions before reacting to damage.")]
        [SerializeField] private bool _continuousActions;
        [Tooltip("Show a skill's Parry ring on every Nth use of a parryable skill.")]
        [SerializeField, Min(1)] private int _parryEveryNSkillUses = 1;
        private int _nextCyclicSkill;
        private int _parryableSkillUseCount;

        [Header("Death & Respawn Settings")]
        [SerializeField] private float _deathDestroyDelay = 1.5f;
        [Tooltip("If true, this monster will respawn after being defeated.")]
        [SerializeField] private bool _canRespawn = false;
        public bool CanRespawn => _canRespawn;
        [Tooltip("Maximum number of respawns. Use -1 for unlimited respawns.")]
        [SerializeField] private int _maxRespawns = -1;
        [SerializeField] private int _respawnsUsed;
        [Tooltip("Time in seconds before the monster attempts to respawn after death.")]
        [SerializeField] private float _respawnTime = 30f;
        public float RespawnTime => _respawnTime;
        [Tooltip("If false, respawn even when the player is still near the spawn point.")]
        [SerializeField] private bool _requirePlayerAwayToRespawn = true;
        [Tooltip("Respawn at the position where this enemy died instead of its original spawn point.")]
        [SerializeField] private bool _respawnAtDeathPosition;

        [Header("Attack Rendering")]
        [SerializeField] private bool _bringToFrontWhileAttacking;
        [SerializeField] private int _attackSortingOrder = 1;
        [SerializeField] private string _attackSortingLayerName;

// Components
        private SpriteRenderer _spriteRenderer;
        private int _defaultSortingOrder;
        private string _defaultSortingLayerName;
private Rigidbody2D _rb;
        private Animator _animator;
        private EnemyStats _stats;
        private Collider2D[] _colliders;
        private Collider2D _attackHitbox;
        private GameObject _player;

        // Spawn / Respawn Tracking
        private Vector3 _spawnPosition;
        private Vector3 _deathPosition;
        private Quaternion _spawnRotation;

        // State Machine
        private EnemyAIState _currentState = EnemyAIState.Idle;
        private bool _isFacingRight;
        private float _startX;
        private bool _movingRight = true;
        private float _nextMeleeTime = 0f;
        private float _nextRangedTime = 0f;
        private float _nextBasicParryTime = 0f;
        private int _basicAttackCount;
        private int _basicMeleeOpportunityCount;
        private float _currentAttackMultiplier = 1.0f;
        private bool _isActionLocked = false;
        private bool _holdingFireballRain;
        private bool _weaponDrawn;
        private float _patrolPauseUntil;
        private ParryReceiver _parry;
        private SmallDragonFireAttackEffect _smallDragonFireEffect;
        private ArchDemonAudioController _archDemonAudio;
        private JinnAudioController _jinnAudio;
        private DemonAudioController _demonAudio;
        private FantasyMushroomAudioController _fantasyMushroomAudio;
        private ForestMushroomAudioController _forestMushroomAudio;
        private FireWormAudioController _fireWormAudio;
        private FlyingEyeAudioController _flyingEyeAudio;
        private SkullwolfAudioController _skullwolfAudio;
        private FoxAudioController _foxAudio;
        private GoblinAudioController _goblinAudio;
        private SkeletonAudioController _skeletonAudio;
        private SkeletonKnightAudioController _skeletonKnightAudio;
        private LizardAudioController _lizardAudio;
        private MinotaurAudioController _minotaurAudio;
        private MoonstoneKeeperAudioController _moonstoneKeeperAudio;
        private ReaperAudioController _reaperAudio;
        private DragonSfxController _dragonSfx;
        private MechaStoneGolemAudioController _mechaStoneGolemAudio;
        private NecromancerAudioController _necromancerAudio;
        private SatyrAudioController _satyrAudio;
        private UndeadExecutionerAudioController _undeadExecutionerAudio;
        private float _damageUntil;
        private bool _projectileSpawned;
        private bool _wasAirborne;
        private bool _hurtAnimationPending;
        private float _hurtAnimationPendingUntil;
        private GameObject _activeSkillProjectile;
        private bool _skillSpriteHidden;
        public bool CanDealMeleeDamage => !_stats.IsDead && !_parry.IsStaggered && Time.time < _damageUntil;
        public float CurrentAttackDamage => _stats != null ? _stats.AttackPower * _currentAttackMultiplier : 10f;
        public float CurrentAttackMultiplier => _currentAttackMultiplier;

        public EnemyAIState CurrentState => _currentState;
        public bool IsFacingRight => _isFacingRight;
        public bool IsFlying => _isFlying;

        private readonly System.Collections.Generic.HashSet<string> _availableAnimParams = new System.Collections.Generic.HashSet<string>();

        private void Awake()
        {
                        _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _defaultSortingOrder = _spriteRenderer.sortingOrder;
                _defaultSortingLayerName = _spriteRenderer.sortingLayerName;
            }
_rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<EnemyStats>();
            _parry = GetComponent<ParryReceiver>();
            if (_parry == null) _parry = gameObject.AddComponent<ParryReceiver>();
            if (_useColliderEdgeAttackRanges)
                _parry.SetSpriteCenter(GetComponent<SpriteRenderer>(), true);
            _smallDragonFireEffect = GetComponentInChildren<SmallDragonFireAttackEffect>(true);
            _archDemonAudio = GetComponent<ArchDemonAudioController>();
            _jinnAudio = GetComponent<JinnAudioController>();
            _demonAudio = GetComponent<DemonAudioController>();
            _fantasyMushroomAudio = GetComponent<FantasyMushroomAudioController>();
            _forestMushroomAudio = GetComponent<ForestMushroomAudioController>();
            _fireWormAudio = GetComponent<FireWormAudioController>();
            _flyingEyeAudio = GetComponent<FlyingEyeAudioController>();
            _skullwolfAudio = GetComponent<SkullwolfAudioController>();
            _foxAudio = GetComponent<FoxAudioController>();
            _goblinAudio = GetComponent<GoblinAudioController>();
            _skeletonAudio = GetComponent<SkeletonAudioController>();
            _skeletonKnightAudio = GetComponent<SkeletonKnightAudioController>();
            _lizardAudio = GetComponent<LizardAudioController>();
            _minotaurAudio = GetComponent<MinotaurAudioController>();
            _moonstoneKeeperAudio = GetComponent<MoonstoneKeeperAudioController>();
            _reaperAudio = GetComponent<ReaperAudioController>();
            _dragonSfx = GetComponent<DragonSfxController>();
            _mechaStoneGolemAudio = GetComponent<MechaStoneGolemAudioController>();
            _necromancerAudio = GetComponent<NecromancerAudioController>();
            _satyrAudio = GetComponent<SatyrAudioController>();
            _undeadExecutionerAudio = GetComponent<UndeadExecutionerAudioController>();
            _colliders = GetComponentsInChildren<Collider2D>();
            var attackHitbox = GetComponentInChildren<EnemyHitbox2D>(true);
            _attackHitbox = attackHitbox != null ? attackHitbox.GetComponent<Collider2D>() : null;

            _isFacingRight = _initialFacingRight;
            if (_flipSpriteInsteadOfTransformScale)
            {
                Vector3 rootScale = transform.localScale;
                rootScale.x = Mathf.Abs(rootScale.x);
                transform.localScale = rootScale;

                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                    spriteRenderer.flipX = !_initialFacingRight;
            }

            _startX = transform.position.x;
            _spawnPosition = transform.position;
            if (_isFlying && !Mathf.Approximately(_flyingIdleHeightOffset, 0f))
            {
                transform.position += Vector3.up * _flyingIdleHeightOffset;
                _spawnPosition = transform.position;
            }
            _spawnRotation = transform.rotation;
            if (_usePassiveStanceAnimations)
                _patrolPauseUntil = Time.time + 0.75f;
            foreach (var skill in _skills)
                if (skill != null) skill.nextReadyTime = Time.time + skill.initialDelay;

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    _availableAnimParams.Add(p.name);
                }
                if (_playAttackStatesDirectly && _availableAnimParams.Contains("ActionIndex"))
                    _animator.SetInteger("ActionIndex", -1);

            }

            if (_isFlying)
            {
                _rb.gravityScale = 0f;
            }
        }

        private void SetAnimBool(string paramName, bool val)
        {
            if (_animator != null && _availableAnimParams.Contains(paramName))
            {
                _animator.SetBool(paramName, val);
            }
        }

        private void LateUpdate()
        {
            if (!_useMovementAnimationStates || _animator == null || _stats == null || _stats.IsDead)
                return;

            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_isActionLocked || _currentState == EnemyAIState.Skill ||
                _currentState == EnemyAIState.MeleeAttack || _currentState == EnemyAIState.RangedAttack)
                return;

            // Animator.Play takes effect on the next Animator evaluation. Do not
            // reassert locomotion in the same frame that damage requested Hurt.
            if (_hurtAnimationPending)
            {
                if (state.IsName("Hurt") || state.IsName("TakeHit") || state.IsName("Hit"))
                    _hurtAnimationPending = false;
                else if (Time.time < _hurtAnimationPendingUntil)
                    return;
                else
                    _hurtAnimationPending = false;
            }

            // Keep authored one-shot actions in control. Movement-state playback
            // is reasserted after these clips return to locomotion.
            if (state.IsName("Attack") || state.IsName("Hurt") || state.IsName("TakeHit") || state.IsName("Death"))
                return;

            if (state.IsName("Hit") && state.normalizedTime < 1f)
                return;

            // Vertical movement can come from a jump, fall, or knockback. Show the
            // authored jump sequence only while the monster is actually airborne.
            bool airborne = Mathf.Abs(_rb.linearVelocity.y) > 0.12f;
            if (airborne)
            {
                if (!_wasAirborne) PlayMovementState("JumpStart");
                else if (state.IsName("JumpStart") && state.normalizedTime >= 1f)
                    PlayMovementState("JumpLoop");
                _wasAirborne = true;
                return;
            }

            if (_wasAirborne)
            {
                _wasAirborne = false;
                PlayMovementState("Land");
                return;
            }
            if (state.IsName("Land") && state.normalizedTime < 1f)
                return;

            string movementState = _currentState == EnemyAIState.Chase
                ? (_animator.HasState(0, Animator.StringToHash("Base Layer.Run")) ? "Run" : "Walk")
                : (_currentState == EnemyAIState.Patrol || _currentState == EnemyAIState.ReturningToSpawn) ? "Walk"
                : "Idle";
            PlayMovementState(movementState);
        }

        private void PlayMovementState(string stateName)
        {
            if (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
                TryPlayAnimatorState(stateName);
        }

        private void SetAnimTrigger(string paramName)
        {
            if (_animator != null && _availableAnimParams.Contains(paramName))
            {
                _animator.SetTrigger(paramName);
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void OnEnable()
        {
            if (_stats == null) _stats = GetComponent<EnemyStats>();
            if (_stats != null)
            {
                _stats.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnDeath -= HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_stats != null)
            {
                _stats.OnDeath -= HandleDeath;
            }
            StopAttack();
        }

        private void FindPlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _player = p;
                return;
            }

            p = GameObject.Find("Player");
            if (p != null)
            {
                _player = p;
                return;
            }

            var playerScript = Object.FindFirstObjectByType<TheLastKnight.Player.PlayerController>();
            if (playerScript != null)
            {
                _player = playerScript.gameObject;
            }
        }

        private void Update()
        {
            UpdateAttackSortingOrder();
            if (_stats.IsDead) return;

            if ((_parry != null && _parry.IsStaggered) || (_stats != null && _stats.CurrentStatus == StatusEffect.Stunned))
            {
                _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
                return;
            }

            if (_waitForAttackAnimationToFinish && _isActionLocked)
            {
                _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
                return;
            }

            // Keep resolving the player while active. Scene transitions and
            // runtime player replacement can leave a stale/null reference;
            // waiting until damage arrives makes the enemy appear asleep.
            if (_player == null || !_player.activeInHierarchy)
            {
                FindPlayer();
                if (_player == null)
                {
                    Patrol();
                    return;
                }
            }

            // Check if current animation state locks movement (e.g. hurt or attacking)
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            bool isBasicAttackAnimation = !string.IsNullOrEmpty(_basicAttackAnimState)
                && stateInfo.IsName(_basicAttackAnimState);
            bool isSkillAnimationPlaying = false;
            if (_waitForAttackAnimationToFinish && stateInfo.normalizedTime < 1f && _skills != null)
            {
                foreach (var skill in _skills)
                {
                    if (skill != null && !string.IsNullOrEmpty(skill.animationName)
                        && stateInfo.IsName(skill.animationName))
                    {
                        isSkillAnimationPlaying = true;
                        break;
                    }
                }
            }
            if (stateInfo.IsName("Hurt") || stateInfo.IsName("TakeHit") ||
                (_useMovementAnimationStates && stateInfo.IsName("Hit") && stateInfo.normalizedTime < 1f) ||
                stateInfo.IsName("Attack") || stateInfo.IsName("Attack3") || stateInfo.IsName("Cast") ||
                isBasicAttackAnimation || isSkillAnimationPlaying || _isActionLocked)
            {
                float vx = stateInfo.IsName("Hurt") ? Mathf.MoveTowards(_rb.linearVelocity.x, 0f, 15f * Time.deltaTime) : 0f;
                _rb.linearVelocity = new Vector2(vx, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
                return;
            }

            // Keep the return AI from moving the enemy while an attack animation is still playing.
            if (_currentState == EnemyAIState.ReturningToSpawn)
            {
                ReturnToSpawn();
                return;
            }

            // Leash Distance Check (สำหรับมอนสเตอร์ที่ไม่ใช่บอส)
            // ถ้า player เดินออกไปไกลเกิน 2 เท่าของระยะการมองเห็น (_detectionRange * 2) จากจุดเกิด จะหยุดไล่ตามกลับไปที่เดิมและฟื้นฟู HP
            if (!_isBoss && _player != null)
            {
                float playerDistFromSpawn = Vector2.Distance(_player.transform.position, _spawnPosition);
                float maxLeashDist = _detectionRange * _leashRangeMultiplier;

                if (playerDistFromSpawn > maxLeashDist || Vector2.Distance(transform.position, _spawnPosition) > maxLeashDist)
                {
                    StartReturningToSpawn();
                    return;
                }
            }

            float distToPlayer = Vector2.Distance(transform.position, _player.transform.position);
            bool isTouching = IsTouchingPlayer();
            float colliderEdgeDistance = isTouching
                ? 0f
                : ((_useColliderEdgeAttackRanges || _useColliderEdgeAttackDistance
                    || _cycleNonParryableSkills || _basicParryEveryNAttacks > 0)
                    ? GetAttackDistance()
                    : distToPlayer);
            float attackDistance = colliderEdgeDistance;
            float meleeDistance = colliderEdgeDistance;
            bool withinDetectionRange = isTouching || (_useColliderEdgeAttackRanges
                ? colliderEdgeDistance <= _detectionRange
                : distToPlayer <= _detectionRange);

            if (_usePassiveStanceAnimations && withinDetectionRange && !_weaponDrawn)
            {
                StartCoroutine(ChangeWeaponStance(true));
                return;
            }

            TheLastKnight.Combat.EnemySkill readySkill = withinDetectionRange
                ? GetReadySkill(attackDistance) : null;
            if (readySkill != null)
            {
                bool isContactSkill = readySkill.projectilePrefab == null && readySkill.groundSpellPrefab == null
                    && !readySkill.summonFireballRain;
                if ((!_requireCloseRangeForContactSkills || !isContactSkill || readySkill.approachPlayerWhileHidden
                    || readySkill.teleportToPlayerAfterPreparation
                    || attackDistance <= _meleeRange || isTouching)
                    && (!_disableBasicAttack || CanReachPlayerWithSkill(readySkill) || isTouching))
                {
                    FaceTarget(_player.transform.position);
                    PerformSkill(readySkill);
                }
                else
                {
                    if (isTouching)
                    {
                        _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                        SetAnimBool("IsMoving", false);
                        SetAnimBool("IsChasing", false);
                        if (_useMovementAnimationStates) PlayMovementState("Idle");
                    }
                    else
                    {
                        ChasePlayer();
                    }
                }
            }
            else if (!_disableBasicAttack && (meleeDistance <= _meleeRange || isTouching)
                && withinDetectionRange && Time.time >= _nextMeleeTime)
            {
                FaceTarget(_player.transform.position);
                PerformMeleeAttack();
            }
            else if (_disableBasicAttack && meleeDistance <= _meleeRange)
            {
                _currentState = EnemyAIState.Idle;
                _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
            }
            else if (_hasRangedAttack && distToPlayer <= _rangedRange && distToPlayer > _meleeRange && Time.time >= _nextRangedTime)
            {
                FaceTarget(_player.transform.position);
                PerformRangedAttack();
            }
            else if (withinDetectionRange && _playAttackStatesDirectly && readySkill == null
                && _skills != null && _skills.Length > 0 && !_disableBasicAttack
                && Time.time >= _nextMeleeTime)
            {
                // Keep this boss applying pressure with its basic attack while
                // its special moves are cooling down.
                FaceTarget(_player.transform.position);
                PerformMeleeAttack();
            }
            else if (withinDetectionRange)
            {
                ChasePlayer();
            }
            else
            {
                if (_usePassiveStanceAnimations && _weaponDrawn)
                {
                    StartCoroutine(ChangeWeaponStance(false));
                    return;
                }
                // Player is outside detection range. If monster is far from spawn point, return to spawn
                float distFromSpawn = Vector2.Distance(transform.position, _spawnPosition);
                if (!_isBoss && distFromSpawn > _patrolDistance + 0.8f)
                {
                    StartReturningToSpawn();
                }
                else
                {
                    Patrol();
                }
            }
        }

        private bool IsTouchingPlayer()
        {
            if (_player == null || _colliders == null) return false;
            var targets = _player.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < _colliders.Length; i++)
            {
                var body = _colliders[i];
                if (body == null || !body.enabled || body.isTrigger) continue;
                for (int j = 0; j < targets.Length; j++)
                {
                    var target = targets[j];
                    if (target == null || !target.enabled || target.isTrigger) continue;
                    if (body.IsTouching(target)) return true;
                    var separation = body.Distance(target);
                    if (separation.isValid && (separation.isOverlapped || separation.distance <= 0.02f))
                        return true;
                }
            }
            return false;
        }

        private float GetAttackDistance()
        {
            float distance = float.PositiveInfinity;
            // Large sprites have offset pivots: use the solid bodies, not their origins.
            var targets = _player.GetComponentsInChildren<Collider2D>();
            foreach (var body in _colliders)
            {
                if (body == null || !body.enabled || body.isTrigger) continue;
                foreach (var target in targets)
                {
                    if (!target.enabled || target.isTrigger) continue;
                    var separation = body.Distance(target);
                    if (separation.isValid)
                        distance = Mathf.Min(distance, Mathf.Max(0f, separation.distance));
                }
            }
            return float.IsPositiveInfinity(distance)
                ? Vector2.Distance(transform.position, _player.transform.position) : distance;
        }

        private void PlayStanceAnimation(string stateName)
        {
            string fullStateName = "Base Layer." + stateName;
            int stateHash = Animator.StringToHash(fullStateName);
            if (_animator == null || !_animator.HasState(0, stateHash)) return;
            if (!_animator.GetCurrentAnimatorStateInfo(0).IsName(fullStateName))
                _animator.Play(stateHash, 0, 0f);
        }

        private IEnumerator ChangeWeaponStance(bool drawWeapon)
        {
            _isActionLocked = true;
            _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
            SetAnimBool("IsMoving", false);
            _weaponDrawn = drawWeapon;
            string animationName = drawWeapon ? "WieldWeapon" : "HolsterWeapon";
            PlayStanceAnimation(animationName);
            if (drawWeapon) _reaperAudio?.PlayWeaponDraw();
            yield return new WaitForSeconds(GetAnimationDuration(animationName, 0.5f));
            if (!_stats.IsDead)
                PlayStanceAnimation(drawWeapon ? "HostileIdle" : "PassiveIdle");
            _isActionLocked = false;
        }

        private void Patrol()
        {
            _currentState = EnemyAIState.Patrol;
            SetAnimBool("IsChasing", false);
            if (_usePassiveStanceAnimations && Time.time < _patrolPauseUntil)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                PlayStanceAnimation("PassiveIdle");
                return;
            }
            float currentX = transform.position.x;
            float moveDir = _movingRight ? 1f : -1f;
            float centerOriginX = _isBoss ? _startX : _spawnPosition.x;

            // Check ledge and wall before moving
            if (!_isFlying && _avoidLedges)
            {
                Vector2 ledgeCheckOrigin = new Vector2(transform.position.x + moveDir * _ledgeForwardOffset, transform.position.y);
                RaycastHit2D groundHit = Physics2D.Raycast(ledgeCheckOrigin, Vector2.down, _groundCheckDistance, _groundLayer);
                if (groundHit.collider == null)
                {
                    _movingRight = !_movingRight;
                    if (_usePassiveStanceAnimations) _patrolPauseUntil = Time.time + 0.75f;
                    FaceDirection(_movingRight);
                    SetHorizontalVelocity(0f);
                    SetAnimBool("IsMoving", false);
                    return;
                }
            }

            // Wall check
            RaycastHit2D wallHit = Physics2D.Raycast(transform.position, Vector2.right * moveDir, _wallCheckDistance, _groundLayer);
            if (wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject)
            {
                _movingRight = !_movingRight;
                if (_usePassiveStanceAnimations) _patrolPauseUntil = Time.time + 0.75f;
                FaceDirection(_movingRight);
                SetHorizontalVelocity(0f);
                SetAnimBool("IsMoving", false);
                return;
            }

            SetAnimBool("IsMoving", true);
            if (_usePassiveStanceAnimations) PlayStanceAnimation("PassiveRunning");

            // Patrol bounds around spawn point
            if (_movingRight)
            {
                SetHorizontalVelocity(_patrolSpeed);
                FaceDirection(true);
                if (currentX > centerOriginX + _patrolDistance)
                {
                    _movingRight = false;
                    if (_usePassiveStanceAnimations) _patrolPauseUntil = Time.time + 0.75f;
                }
            }
            else
            {
                SetHorizontalVelocity(-_patrolSpeed);
                FaceDirection(false);
                if (currentX < centerOriginX - _patrolDistance)
                {
                    _movingRight = true;
                    if (_usePassiveStanceAnimations) _patrolPauseUntil = Time.time + 0.75f;
                }
            }
        }

        private void ChasePlayer()
        {
            _currentState = EnemyAIState.Chase;
            SetAnimBool("IsChasing", true);
            SetAnimBool("IsMoving", true);
            if (_usePassiveStanceAnimations) PlayStanceAnimation("HostileRunning");

            Vector2 targetPosition = _player.transform.position;
            if (_isFlying && _useColliderEdgeAttackRanges)
            {
                var playerColliders = _player.GetComponentsInChildren<Collider2D>();
                foreach (var playerCollider in playerColliders)
                {
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;
                    targetPosition = playerCollider.bounds.center + Vector3.up * _flyingChaseHeightOffset;
                    break;
                }
            }

            Vector2 toPlayer = targetPosition - (Vector2)transform.position;
            if (!_isFlying && _facingFlipDeadZone > 0f && Mathf.Abs(toPlayer.x) <= _facingFlipDeadZone)
            {
                SetHorizontalVelocity(0f);
                SetAnimBool("IsMoving", Mathf.Abs(_rb.linearVelocity.x) > 0.05f);
                return;
            }

            float dirX = Mathf.Sign(toPlayer.x);

            // Flying enemies can move in 2D
            if (_isFlying)
            {
                Vector2 targetVelocity = toPlayer.normalized * _chaseSpeed;
                _rb.linearVelocity = targetVelocity;
            }
            else
            {
                // Ground enemy ledge check while chasing
                if (_avoidLedges)
                {
                    Vector2 ledgeCheckOrigin = new Vector2(transform.position.x + dirX * _ledgeForwardOffset, transform.position.y);
                    RaycastHit2D groundHit = Physics2D.Raycast(ledgeCheckOrigin, Vector2.down, _groundCheckDistance, _groundLayer);
                    if (groundHit.collider == null)
                    {
                        // Stop at ledge edge rather than committing suicide
                        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                        SetAnimBool("IsMoving", false);
                        return;
                    }
                }

                SetHorizontalVelocity(dirX * _chaseSpeed);
            }

            if (Mathf.Abs(targetPosition.x - transform.position.x) > _facingFlipDeadZone)
                FaceDirection(dirX > 0);
            if (_isBoss)
            {
                _startX = transform.position.x;
            }
        }

        private void PerformMeleeAttack()
        {
            if (_basicGuardEveryNAttacks > 0 && !string.IsNullOrEmpty(_basicGuardSkillName))
            {
                _basicMeleeOpportunityCount++;
                if (_basicMeleeOpportunityCount % _basicGuardEveryNAttacks == 0)
                {
                    foreach (var skill in _skills)
                    {
                        if (skill == null || skill.skillName != _basicGuardSkillName) continue;
                        _nextMeleeTime = Time.time + _meleeCooldown;
                        PerformSkill(skill);
                        return;
                    }
                }
            }

            _currentState = EnemyAIState.MeleeAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            if (_useMovementAnimationStates) PlayMovementState("Idle");
            _nextMeleeTime = Time.time + _meleeCooldown;
            _currentAttackMultiplier = _basicAttackMultiplier;

            // A new basic swing is a new hit window. Clear the per-target
            // cooldown so repeated swings can damage the same player.
            foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
            {
                hitbox.BeginAttack();
            }

            if (_basicAttackAnimStates != null && _basicAttackAnimStates.Length > 0)
            {
                _basicAttackAnimState = _basicAttackAnimStates[_nextBasicAttackAnimIndex % _basicAttackAnimStates.Length];
                _nextBasicAttackAnimIndex = (_nextBasicAttackAnimIndex + 1) % _basicAttackAnimStates.Length;
            }

            // Optional attack-count cadence takes precedence over the legacy timed parry cooldown.
            bool canParryThisTime = ShouldBasicAttackParry();

            StartCoroutine(WindupAttack(false, canParryThisTime));
        }

        private void PerformRangedAttack()
        {
            _currentState = EnemyAIState.RangedAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            if (_useMovementAnimationStates) PlayMovementState("Idle");
            _nextRangedTime = Time.time + _rangedCooldown;
            _currentAttackMultiplier = _basicAttackMultiplier;

            bool canParryThisTime = ShouldBasicAttackParry();

            StartCoroutine(WindupAttack(true, canParryThisTime));
        }

        private bool ShouldBasicAttackParry()
        {
            if (!_basicAttackCanParry || (!_allowBasicParryWithSkills && _basicParryEveryNAttacks <= 0 && HasParryableSkill()))
                return false;

            _basicAttackCount++;
            if (_basicParryEveryNAttacks > 0)
                return _basicAttackCount % _basicParryEveryNAttacks == 0;

            if (Time.time < _nextBasicParryTime)
                return false;

            _nextBasicParryTime = Time.time + Mathf.Max(4.0f, _basicParryCooldown);
            return true;
        }

        private IEnumerator WindupAttack(bool ranged, bool canParry)
        {
            var dragonAudio = GetComponent<DragonAudioController>();
            dragonAudio?.PrepareAttackAnimation();
            _isActionLocked = true;
            _projectileSpawned = false;
            _damageUntil = 0f;
            _parry?.FinishWindup();

            if (canParry && _parry != null)
            {
                _parry.BeginWindup();
                yield return new WaitForSeconds(ParryReceiver.WindupDuration + ParryReceiver.TimingTolerance);
                _parry.FinishWindup();
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    _isActionLocked = false;
                    yield break;
                }
            }
            else
            {
                // Unparried attack: short anticipation delay for normal enemies; bosses attack immediately
                if (!_isBoss)
                {
                    yield return new WaitForSeconds(0.15f);
                    if (_stats.IsDead || _parry.IsStaggered)
                    {
                        _isActionLocked = false;
                        yield break;
                    }
                }
            }

            if (dragonAudio != null && !ranged)
                dragonAudio.PlayAttackAnimation();
            else
            {
                PlayAnimationAction(_basicAttackAnimState);
                if (!ranged) _moonstoneKeeperAudio?.PlaySwordSwing();
            }
            if (!ranged)
            {
                _skeletonKnightAudio?.PlayBasicAttack(_basicAttackAnimState);
                _satyrAudio?.PlayBasicAttack();
                _reaperAudio?.PlaySwordSlash();
                _jinnAudio?.PlayCloseAttack();
                _foxAudio?.PlayBite();
                _dragonSfx?.PlayBite();
                _archDemonAudio?.PlayBasicAttack();
                _demonAudio?.PlayBasicAttack();
                _necromancerAudio?.PlayAttack();
                _minotaurAudio?.PlayBasicAttack();
            }
            _smallDragonFireEffect?.Play();
            float damageStartDelay = ranged ? 0f : Mathf.Max(
                _basicAttackDamageDelay,
                GetBasicAttackDamageStartDelay(_basicAttackAnimState));
            // Keep damage aligned with the visible impact frame (including dragon-specific timing).
            if (dragonAudio != null && !ranged)
                damageStartDelay = Mathf.Max(damageStartDelay, 2f / 12f);
            if (damageStartDelay > 0f)
            {
                yield return new WaitForSeconds(damageStartDelay);
                yield return null;
            }
            if (_stats.IsDead || _parry.IsStaggered)
            {
                _isActionLocked = false;
                yield break;
            }
            if (!ranged)
            {
                _goblinAudio?.PlayBasicAttack();
                _lizardAudio?.PlayBasicAttack();
                _fantasyMushroomAudio?.PlayBasicAttack();
                _forestMushroomAudio?.PlayBasicAttack();
                _flyingEyeAudio?.PlayBite();
                _skullwolfAudio?.PlayBite();
            }
            // A new basic swing is a new hit window. Reset each hitbox's per-target
            // cooldown so SideSwing, FwdSwing, and DownSwing can all damage in sequence.
            foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
            {
                hitbox.BeginAttack();
            }
            float damageWindow = ranged ? 0.35f : 0.18f;
            _damageUntil = Time.time + damageWindow;
            foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
            {
                hitbox.DealDamageToOverlaps();
            }
            if (ranged) SpawnProjectile();
            yield return new WaitForSeconds(damageWindow);
            _damageUntil = 0f;
            float basicActionDuration = _basicAttackActionDuration > 0f
                ? _basicAttackActionDuration : GetAnimationDuration(_basicAttackAnimState, 0.35f);
            float remainingAnimationTime = basicActionDuration
                - damageWindow - damageStartDelay;
            if (remainingAnimationTime > 0f)
            {
                yield return new WaitForSeconds(remainingAnimationTime);
            }
            while (_waitForAttackAnimationToFinish && _animator != null
                && _animator.GetCurrentAnimatorStateInfo(0).IsName(_basicAttackAnimState)
                && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
            if (_waitForAttackAnimationToFinish) yield return null;
            _isActionLocked = false;
        }

        private float GetBasicAttackDamageStartDelay(string animationName)
        {
            if (_basicAttackAnimStates == null || _basicAttackDamageStartDelays == null)
                return 0f;

            int index = System.Array.IndexOf(_basicAttackAnimStates, animationName);
            return index >= 0 && index < _basicAttackDamageStartDelays.Length
                ? Mathf.Max(0f, _basicAttackDamageStartDelays[index])
                : 0f;
        }

        private void PerformSkill(TheLastKnight.Combat.EnemySkill skill)
        {
            _currentState = EnemyAIState.Skill;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            if (_useMovementAnimationStates) PlayMovementState("Idle");

            if (_cycleNonParryableSkills && !skill.isParryable)
                _nextCyclicSkill = (System.Array.IndexOf(_skills, skill) + 1) % _skills.Length;
            _currentAttackMultiplier = skill.damageMultiplier;

            bool canParry = false;
            if (skill.isParryable)
            {
                _parryableSkillUseCount++;
                canParry = _parryableSkillUseCount % Mathf.Max(1, _parryEveryNSkillUses) == 0;
            }
            StartCoroutine(ExecuteSkillRoutine(skill, canParry));
        }

        private IEnumerator ExecuteSkillRoutine(TheLastKnight.Combat.EnemySkill skill, bool canParry)
        {
            _isActionLocked = true;
            _projectileSpawned = false;
            _damageUntil = 0f;
            _parry?.FinishWindup();

            // ท่าที่สามารถ Parry ได้ จะแสดงวงกลม Timing Ring
            if (canParry && _parry != null)
            {
                _parry.BeginWindup();
                yield return new WaitForSeconds(ParryReceiver.WindupDuration + ParryReceiver.TimingTolerance);
                _parry.FinishWindup();
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    _isActionLocked = false;
                    yield break;
                }
            }
            else if (!_continuousActions && !_isBoss && !skill.skipAnticipation)
            {
                yield return new WaitForSeconds(0.15f);
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    _isActionLocked = false;
                    yield break;
                }
            }

            bool waitForAnimation = (_cycleNonParryableSkills || _waitForAttackAnimationToFinish) && _animator != null
                && (_animator.HasState(0, Animator.StringToHash(skill.animationName))
                    || _animator.HasState(0, Animator.StringToHash("Base Layer." + skill.animationName)));

            if (!string.IsNullOrEmpty(skill.preparationAnimationName))
            {
                if (skill.skillName == "VanishingStrike")
                    _foxAudio?.PlayVanish();
                SetSkillHealthBarsHidden(skill.hideHealthBarDuringSkill);
                PlayAnimationAction(skill.preparationAnimationName);
                float preparationDuration = skill.preparationDurationOverride > 0f
                    ? skill.preparationDurationOverride
                    : GetAnimationDuration(skill.preparationAnimationName, 0.5f);
                if (skill.preparationDamageDelay >= 0f && skill.preparationDamageDelay < preparationDuration)
                {
                    yield return new WaitForSeconds(skill.preparationDamageDelay);
                    if (_stats.IsDead || _parry.IsStaggered)
                    {
                        SetSkillHealthBarsHidden(false);
                        _isActionLocked = false;
                        yield break;
                    }
                    foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                    {
                        hitbox.BeginAttack();
                    }
                    _damageUntil = Time.time + 0.18f;
                    foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                        hitbox.DealDamageToOverlaps();
                    yield return new WaitForSeconds(preparationDuration - skill.preparationDamageDelay);
                    _damageUntil = 0f;
                }
                else
                {
                    yield return new WaitForSeconds(preparationDuration);
                }
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    SetSkillHealthBarsHidden(false);
                    _isActionLocked = false;
                    yield break;
                }
            }

            if (skill.teleportToPlayerAfterPreparation)
            {
                SetSkillSpriteHidden(true);
                bool reachedPlayer = TeleportBesidePlayer();
                SetSkillSpriteHidden(false);
                if (!reachedPlayer)
                {
                    SetSkillHealthBarsHidden(false);
                    _currentAttackMultiplier = _basicAttackMultiplier;
                    _isActionLocked = false;
                    yield break;
                }
                FaceTarget(_player.transform.position);
            }

            if (skill.approachPlayerWhileHidden && _player != null)
            {
                SetSkillSpriteHidden(true);
                float stopAt = Time.time + Mathf.Max(0f, skill.hiddenApproachTimeout);
                while (Time.time < stopAt && _player != null && !_stats.IsDead
                    && !_parry.IsStaggered && GetAttackDistance() > _meleeRange)
                {
                    ChasePlayer();
                    yield return new WaitForFixedUpdate();
                }
                _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
                SetSkillSpriteHidden(false);
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    SetSkillHealthBarsHidden(false);
                    _isActionLocked = false;
                    yield break;
                }
                _currentState = EnemyAIState.Skill;
                FaceTarget(_player.transform.position);
            }

            if (!string.IsNullOrEmpty(skill.reappearanceAnimationName))
            {
                PlayAnimationAction(skill.reappearanceAnimationName);
                yield return new WaitForSeconds(skill.reappearanceDurationOverride > 0f
                    ? skill.reappearanceDurationOverride
                    : GetAnimationDuration(skill.reappearanceAnimationName, 0.5f));
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    SetSkillHealthBarsHidden(false);
                    _isActionLocked = false;
                    yield break;
                }
                if (skill.approachPlayerWhileHidden)
                {
                    float catchUpUntil = Time.time + Mathf.Min(1.5f, Mathf.Max(0f, skill.hiddenApproachTimeout));
                    if (_player != null && GetAttackDistance() > _meleeRange)
                        PlayMovementState("Run");
                    while (Time.time < catchUpUntil && _player != null && GetAttackDistance() > _meleeRange)
                    {
                        ChasePlayer();
                        yield return new WaitForFixedUpdate();
                    }
                    _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                    SetAnimBool("IsMoving", false);
                    SetAnimBool("IsChasing", false);
                    if (_player == null || GetAttackDistance() > _meleeRange)
                    {
                        SetSkillHealthBarsHidden(false);
                        _currentAttackMultiplier = _basicAttackMultiplier;
                        _isActionLocked = false;
                        yield break;
                    }
                    _currentState = EnemyAIState.Skill;
                    FaceTarget(_player.transform.position);
                }
            }

            if (skill.guardDuration > 0f)
            {
                if (skill.summonFireballRain)
                    _foxAudio?.PlayHowl();
                if (waitForAnimation && _cycleNonParryableSkills)
                    _animator.Play(skill.animationName, 0, 0f);
                else
                    PlayAnimationAction(skill.animationName, skill.actionIndex);
                if (skill.skillName == "StoneShield")
                    _mechaStoneGolemAudio?.PlayShieldCast();
                if (skill.summonFireballRain)
                {
                    _holdingFireballRain = true;
                    Coroutine rainRoutine = null;
                    var rain = GetComponent<TheLastKnight.Combat.FoxFireballRain>();
                    if (!_stats.IsDead && (_parry == null || !_parry.IsStaggered) && rain != null)
                        rainRoutine = StartCoroutine(rain.CastRoutine(_player,
                            _stats.AttackPower * skill.damageMultiplier));
                    yield return new WaitForSeconds(skill.guardDuration);
                    if (rainRoutine != null) yield return rainRoutine;
                    _holdingFireballRain = false;
                }
                else
                {
                    yield return new WaitForSeconds(skill.guardDuration);
                }
                if (skill.summonFireballRain)
                {
                    _animator.Play("Idle", 0, 0f);
                    _currentState = EnemyAIState.Idle;
                }
                else if (waitForAnimation) _animator.Play("Idle", 0, 0f);
                if (_waitForAttackAnimationToFinish) yield return null;
                _currentAttackMultiplier = _basicAttackMultiplier;
                skill.nextReadyTime = Time.time + skill.cooldown;
                SetSkillHealthBarsHidden(false);
                _isActionLocked = false;
                yield break;
            }

            if (waitForAnimation && _cycleNonParryableSkills)
                _animator.Play(skill.animationName, 0, 0f);
            else
                PlayAnimationAction(skill.animationName, skill.actionIndex);
            _satyrAudio?.PlaySkill(skill.skillName);
            _necromancerAudio?.PlayAttack();
            _moonstoneKeeperAudio?.PlaySwordSwing();
            if (skill.skillName == "FullCombo" || skill.animationName == "FullCombo")
                _skeletonKnightAudio?.PlayFullCombo();
            if (skill.skillName == "ParryBite")
                _foxAudio?.PlayBite();
            if (skill.skillName == "FireBreath" || skill.skillName == "SmallFireBall")
                _dragonSfx?.PlayFireBreath();
            _smallDragonFireEffect?.Play();
            if (skill.animationName == "Skill1")
                _undeadExecutionerAudio?.PlaySpinningCleave();
            if (skill.animationName == "Summon")
            {
                GetComponent<TheLastKnight.Combat.UndeadExecutionerSummonEffect>()?.Play(_player);
                _undeadExecutionerAudio?.PlayDarkSummon();
            }
            _damageUntil = 0f;

            float damageStartDelay = Mathf.Max(0f, skill.damageStartDelay);
            float elapsedAnimationTime = damageStartDelay;
            if (damageStartDelay > 0f)
            {
                yield return new WaitForSeconds(damageStartDelay);
                // Let the Animator apply the sprite for this frame before testing
                // its bounds; resuming exactly on a keyframe boundary can read
                // the previous sprite for one update.
                yield return null;
            }
            if (_stats.IsDead || _parry.IsStaggered)
            {
                SetSkillHealthBarsHidden(false);
                _isActionLocked = false;
                yield break;
            }

            if (skill.skillName == "AttackWithStun" || skill.animationName == "AttackWithStun")
                _forestMushroomAudio?.PlayAttackWithStun();

            // Every attack window gets a fresh per-attack hit gate. Otherwise
            // the hitbox's 0.8s victim cooldown can suppress the next skill.
            foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                hitbox.BeginAttack();
            if (skill.animationName == "Attack" && skill.projectilePrefab == null
                && skill.groundSpellPrefab == null && skill.guardDuration <= 0f)
                _skeletonAudio?.PlayNormalAttack();

            // A thrown weapon delivers its own damage; do not also hit with the body.
            if (skill.dealDamageAsSingleHit)
            {
                _damageUntil = 0f;
                ApplySingleSkillHit(skill.requireSpriteBoundsOverlap);

                if (skill.secondDamageHitTime >= damageStartDelay)
                {
                    float delayToSecondHit = skill.secondDamageHitTime - elapsedAnimationTime;
                    if (delayToSecondHit > 0f)
                    {
                        yield return new WaitForSeconds(delayToSecondHit);
                        yield return null;
                        elapsedAnimationTime += delayToSecondHit;
                    }

                    if (!_stats.IsDead && !_parry.IsStaggered)
                    {
                        ApplySingleSkillHit(skill.requireSpriteBoundsOverlap);
                    }
                }
            }
            else
            {
                _damageUntil = _continuousActions && skill.projectilePrefab != null
                    ? 0f
                    : Time.time + Mathf.Max(0f, skill.damageDuration);
                foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                {
                    hitbox.DealDamageToOverlaps();
                }
            }

            if (skill.additionalDamageHitTimes != null)
            {
                foreach (float hitTime in skill.additionalDamageHitTimes)
                {
                    if (hitTime <= elapsedAnimationTime) continue;

                    float delayToHit = hitTime - elapsedAnimationTime;
                    if (delayToHit > 0f)
                    {
                        yield return new WaitForSeconds(delayToHit);
                        yield return null;
                        elapsedAnimationTime = hitTime;
                    }

                    if (_stats.IsDead || _parry.IsStaggered)
                    {
                        _damageUntil = 0f;
                        SetSkillHealthBarsHidden(false);
                        _isActionLocked = false;
                        yield break;
                    }

                    if (skill.dealDamageAsSingleHit)
                    {
                        ApplySingleSkillHit(skill.requireSpriteBoundsOverlap);
                    }
                    else
                    {
                        // Each combo strike gets its own damage window and fresh hit gate.
                        foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                            hitbox.BeginAttack();
                        _damageUntil = Time.time + Mathf.Max(0.08f, skill.damageDuration);
                        foreach (var hitbox in GetComponentsInChildren<EnemyHitbox2D>())
                            hitbox.DealDamageToOverlaps();
                    }
                }
            }

            if (skill.groundSpellPrefab != null && _player != null)
            {
                Vector3 spellPos = new Vector3(_player.transform.position.x, _player.transform.position.y, 0f);
                _jinnAudio?.PlayWindAttack();
                _archDemonAudio?.PlaySkillSound();
                var spellObj = Instantiate(skill.groundSpellPrefab, spellPos, Quaternion.identity);
                var spellArea = spellObj.GetComponent<GroundSpellArea>();
                if (spellArea != null && _stats != null)
                {
                    spellArea.Initialize(_stats.AttackPower * skill.damageMultiplier, gameObject);
                }
                else
                {
                    PlaySkillVisualEffect(spellObj);
                }
            }
            else if (skill.projectilePrefab != null)
            {
                if (skill.skillName == "RocketPunch")
                    _mechaStoneGolemAudio?.PlayRocketPunch();
                else if (skill.skillName == "LaserBeam")
                    _mechaStoneGolemAudio?.PlayLaserOrb();
                _jinnAudio?.PlayWindAttack();
                SpawnCustomProjectile(skill.projectilePrefab, skill.damageMultiplier);
            }

            float actionDuration = skill.actionDurationOverride > 0f
                ? skill.actionDurationOverride : GetAnimationDuration(skill.animationName, elapsedAnimationTime);
            float remainingAnimationTime = actionDuration - elapsedAnimationTime;
            if (remainingAnimationTime > 0f)
            {
                yield return new WaitForSeconds(remainingAnimationTime);
            }
            _damageUntil = 0f;
            while (waitForAnimation && _animator.GetCurrentAnimatorStateInfo(0).IsName(skill.animationName)
                && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                yield return null;
            if (_waitForAttackProjectileToFinish && skill.projectilePrefab != null)
            {
                while (_activeSkillProjectile != null && !_stats.IsDead && !_parry.IsStaggered)
                    yield return null;
                _activeSkillProjectile = null;
            }
            if (_continuousActions && waitForAnimation) _animator.Play("Idle", 0, 0f);
            if (_waitForAttackAnimationToFinish) yield return null;
            _currentAttackMultiplier = _basicAttackMultiplier;
            skill.nextReadyTime = Time.time + skill.cooldown;
            SetSkillHealthBarsHidden(false);
            _isActionLocked = false;
        }

        private void SetSkillHealthBarsHidden(bool hidden)
        {
            foreach (var bar in GetComponentsInChildren<FloatingHealthBar>(true))
                bar.SetTemporarilyHidden(hidden);
        }

        private void SetSkillSpriteHidden(bool hidden)
        {
            if (_spriteRenderer == null) return;
            if (hidden)
            {
                _spriteRenderer.enabled = false;
                _skillSpriteHidden = true;
            }
            else if (_skillSpriteHidden)
            {
                _spriteRenderer.enabled = true;
                _skillSpriteHidden = false;
            }
        }

        private bool TeleportBesidePlayer()
        {
            if (_player == null || _rb == null || _colliders == null) return false;
            Collider2D ownBody = null;
            foreach (var collider in _colliders)
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    ownBody = collider;
                    break;
                }
            if (ownBody == null) return false;

            foreach (var target in _player.GetComponentsInChildren<Collider2D>())
            {
                if (target == null || !target.enabled || target.isTrigger) continue;
                // Choose either side on every ambush so the fox can strike from
                // in front of or behind the player, regardless of its old position.
                bool approachFromLeft = Random.value < 0.5f;
                float edgeGap = Mathf.Min(0.03f, _meleeRange * 0.5f);
                float x = approachFromLeft
                    ? target.bounds.min.x - edgeGap - (ownBody.bounds.max.x - transform.position.x)
                    : target.bounds.max.x + edgeGap + (transform.position.x - ownBody.bounds.min.x);
                float y = target.bounds.min.y - (ownBody.bounds.min.y - transform.position.y);
                _rb.linearVelocity = Vector2.zero;
                transform.position = new Vector3(x, y, transform.position.z);
                _rb.position = new Vector2(x, y);
                Physics2D.SyncTransforms();
                return GetAttackDistance() <= _meleeRange;
            }
            return false;
        }

        private static void PlaySkillVisualEffect(GameObject effect)
        {
            if (effect == null) return;

            var particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            float lifetime = 0f;
            foreach (var particle in particles)
            {
                if (particle == null) continue;
                var main = particle.main;
                particle.Play(true);
                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }

            // Non-spell VFX prefabs are often authored with Play On Awake disabled.
            // Start them explicitly and remove the instance after its particles finish.
            Destroy(effect, Mathf.Max(1f, lifetime + 0.25f));
        }

        private void ApplySingleSkillHit(bool requireSpriteBoundsOverlap)
        {
            if (_player == null || _stats == null) return;

            var playerStats = _player.GetComponent<TheLastKnight.Stats.PlayerStats>();
            if (playerStats == null)
            {
                playerStats = _player.GetComponentInChildren<TheLastKnight.Stats.PlayerStats>();
            }
            if (playerStats == null) return;

            if (requireSpriteBoundsOverlap)
            {
                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null || spriteRenderer.sprite == null) return;

                Bounds visibleFrameBounds = spriteRenderer.bounds;
                var playerColliders = _player.GetComponentsInChildren<Collider2D>();
                bool overlapsFrame = false;
                for (int i = 0; i < playerColliders.Length; i++)
                {
                    var playerCollider = playerColliders[i];
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;
                    if (visibleFrameBounds.Intersects(playerCollider.bounds))
                    {
                        overlapsFrame = true;
                        break;
                    }
                }

                if (!overlapsFrame) return;
            }

            playerStats.TakeDamage(CurrentAttackDamage);
        }

        private float GetAnimationDuration(string animationName, float fallbackDuration)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return fallbackDuration;
            }

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && (clip.name == animationName ||
                    clip.name.EndsWith("_" + animationName, System.StringComparison.Ordinal)))
                {
                    float stateSpeed = 1f;
                    var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                    if (stateInfo.IsName(animationName) || stateInfo.IsName("Base Layer." + animationName))
                        stateSpeed = Mathf.Max(0.01f, Mathf.Abs(stateInfo.speed));
                    return clip.length / Mathf.Max(0.01f, _animator.speed * stateSpeed);
                }
            }

            return fallbackDuration;
        }

        private void PlayAnimationAction(string animName, int actionIndex = -1)
        {
            if (_animator == null) return;

            if (_playAttackStatesDirectly && !string.IsNullOrEmpty(animName))
            {
                if (TryPlayAnimatorState(animName)) return;
            }

            if (actionIndex >= 0 && _availableAnimParams.Contains("ActionIndex"))
            {
                _animator.SetInteger("ActionIndex", actionIndex);
                SetAnimTrigger("Attack");
                return;
            }

            if (!string.IsNullOrEmpty(animName))
            {
                if (_availableAnimParams.Contains(animName))
                {
                    _animator.SetTrigger(animName);
                    return;
                }

                if (TryPlayAnimatorState(animName)) return;
            }

            SetAnimTrigger("Attack");
        }

        private bool TryPlayAnimatorState(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return false;

            int fullPathHash = Animator.StringToHash("Base Layer." + stateName);
            if (_animator.HasState(0, fullPathHash))
            {
                PrepareDirectAnimatorState();
                _animator.Play(fullPathHash, 0, 0f);
                return true;
            }

            int shortNameHash = Animator.StringToHash(stateName);
            if (_animator.HasState(0, shortNameHash))
            {
                PrepareDirectAnimatorState();
                _animator.Play(shortNameHash, 0, 0f);
                return true;
            }

            return false;
        }

        private void PrepareDirectAnimatorState()
        {
            // Controllers with Any State ActionIndex transitions can immediately
            // override a state played directly if ActionIndex remains at its default (0).
            if (_availableAnimParams.Contains("ActionIndex"))
                _animator.SetInteger("ActionIndex", -1);

            if (_availableAnimParams.Contains("Attack"))
                _animator.ResetTrigger("Attack");
        }

        public TheLastKnight.Combat.EnemySkill GetReadySkill(float distToPlayer)
        {
            if (_skills == null || _skills.Length == 0) return null;

            if (_cycleNonParryableSkills)
            {
                foreach (var prioritySkill in _skills)
                    if (prioritySkill != null && prioritySkill.skillName != _basicGuardSkillName
                        && prioritySkill.isParryable && prioritySkill.IsReady(distToPlayer, Time.time)
                        && (!prioritySkill.requireLineOfSight || HasLineOfSightToPlayer()))
                        return prioritySkill;

                for (int offset = 0; offset < _skills.Length; offset++)
                {
                    var candidate = _skills[(_nextCyclicSkill + offset) % _skills.Length];
                    if (candidate != null && candidate.skillName != _basicGuardSkillName
                        && !candidate.isParryable && candidate.IsReady(distToPlayer, Time.time)
                        && (!candidate.requireLineOfSight || HasLineOfSightToPlayer()))
                        return candidate;
                }
                return null;
            }

            for (int i = 0; i < _skills.Length; i++)
            {
                var skill = _skills[i];
                if (skill != null && skill.skillName != _basicGuardSkillName
                    && skill.IsReady(distToPlayer, Time.time)
                    && (!skill.requireLineOfSight || HasLineOfSightToPlayer()))
                {
                    return skill;
                }
            }
            return null;
        }

        private bool HasLineOfSightToPlayer()
        {
            if (_player == null) return false;

            var playerColliders = _player.GetComponentsInChildren<Collider2D>();
            foreach (var enemyCollider in _colliders)
            {
                if (enemyCollider == null || !enemyCollider.enabled || enemyCollider.isTrigger) continue;
                foreach (var playerCollider in playerColliders)
                {
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;

                    Vector2 start = enemyCollider.ClosestPoint(playerCollider.bounds.center);
                    Vector2 end = playerCollider.ClosestPoint(start);
                    var hits = Physics2D.LinecastAll(start, end);
                    bool blocked = false;
                    foreach (var hit in hits)
                    {
                        var hitCollider = hit.collider;
                        if (hitCollider == null || hitCollider.isTrigger) continue;
                        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;
                        if (hitCollider.transform == _player.transform || hitCollider.transform.IsChildOf(_player.transform))
                        {
                            blocked = false;
                            break;
                        }

                        blocked = true;
                        break;
                    }

                    if (!blocked) return true;
                }
            }

            return false;
        }

        private bool CanReachPlayerWithSkill(TheLastKnight.Combat.EnemySkill skill)
        {
            if (skill == null || _player == null) return false;
            if (skill.projectilePrefab != null || skill.groundSpellPrefab != null) return true;
            return CanReachPlayerWithContactHitbox();
        }

        private bool CanReachPlayerWithContactHitbox()
        {
            if (_player == null || _attackHitbox == null || !_attackHitbox.enabled) return false;

            var playerColliders = _player.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < playerColliders.Length; i++)
            {
                var playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;
                if (_attackHitbox.bounds.Intersects(playerCollider.bounds)) return true;
            }

            return false;
        }

        public bool HasParryableSkill()
        {
            if (_skills == null || _skills.Length == 0) return false;
            for (int i = 0; i < _skills.Length; i++)
            {
                if (_skills[i] != null && _skills[i].isParryable) return true;
            }
            return false;
        }

        public void SetSkills(TheLastKnight.Combat.EnemySkill[] skills)
        {
            _skills = skills;
            _nextCyclicSkill = 0;
            _parryableSkillUseCount = 0;
            _basicMeleeOpportunityCount = 0;
        }

        public void SetBasicAttackConfiguration(string animState, float multiplier, bool canParry, float parryCooldown)
        {
            _basicAttackAnimState = animState;
            _basicAttackMultiplier = multiplier;
            _basicAttackCanParry = canParry;
            _basicParryCooldown = parryCooldown;
        }

        public void SetBoss(bool isBoss)
        {
            _isBoss = isBoss;
        }

        /// <summary>Area bosses are permanently cleared and must not use the enemy respawn loop.</summary>
        public void DisableRespawn()
        {
            _isBoss = true;
            _canRespawn = false;
        }

        public void CancelAttack()
        {
            StopAttack();
            _currentState = EnemyAIState.Hurt;
            if (_continuousActions && _animator != null && _animator.HasState(0, Animator.StringToHash("TakeHit")))
            {
                _animator.ResetTrigger("Attack");
                _animator.ResetTrigger("Hurt");
                _animator.Play("TakeHit", 0, 0f);
            }
            else SetAnimTrigger("Hurt");
        }

        private void StopAttack()
        {
            StopAllCoroutines();
            _holdingFireballRain = false;
            SetSkillSpriteHidden(false);
            SetSkillHealthBarsHidden(false);
            _smallDragonFireEffect?.Stop();
            _parry?.FinishWindup();
            _damageUntil = 0f;
            _projectileSpawned = true;
            _currentAttackMultiplier = _basicAttackMultiplier;
            _isActionLocked = false;
            if (_activeSkillProjectile != null)
            {
                _activeSkillProjectile.SetActive(false);
                Destroy(_activeSkillProjectile);
                _activeSkillProjectile = null;
            }
        }

        private IEnumerator DelayedProjectileRoutine(float delay)
        {
            _isActionLocked = true;
            yield return new WaitForSeconds(delay);
            SpawnProjectile();
            _isActionLocked = false;
        }

        /// <summary>
        /// Can be triggered by code fallback or directly by Animation Event in Attack clip.
        /// </summary>
        public void SpawnProjectile()
        {
            if (_stats.IsDead || _parry.IsStaggered || _parry.IsWindingUp || _projectileSpawned) return;
            _projectileSpawned = true;

            // Ground spell check (e.g. BringerOfDeath Spell, Jinn Magic)
            if (_groundSpellPrefab != null && _player != null)
            {
                Vector3 spellPos = GetGroundSpellSpawnPosition(_player);
                var spellObj = Instantiate(_groundSpellPrefab, spellPos, Quaternion.identity);
                var spellArea = spellObj.GetComponent<GroundSpellArea>();
                if (spellArea != null && _stats != null)
                {
                    spellArea.Initialize(_stats.AttackPower * _currentAttackMultiplier, gameObject);
                }
                return;
            }

            if (_projectilePrefab == null) return;

            float dirX = _isFacingRight ? 1f : -1f;
            Vector3 spawnPos = GetProjectileSpawnPosition(dirX);

            GameObject proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);

            // Orient projectile towards facing direction
            Vector3 scale = proj.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dirX;
            proj.transform.localScale = scale;

            var projectileScript = proj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                Vector2 fireDir = _isFacingRight ? Vector2.right : Vector2.left;
                float power = _stats != null ? _stats.AttackPower * _currentAttackMultiplier : 10f;
                projectileScript.Initialize(fireDir, power, gameObject);
            }
        }

        private void SpawnCustomProjectile(GameObject prefab, float damageMultiplier)
        {
            if (_stats.IsDead || _parry.IsStaggered || prefab == null) return;

            float dirX = _isFacingRight ? 1f : -1f;
            Vector3 spawnPos = GetProjectileSpawnPosition(dirX);

            GameObject proj = Instantiate(prefab, spawnPos, Quaternion.identity);
            _skeletonAudio?.PlaySwordThrow();
            _goblinAudio?.PlayBombThrow();
            if (_continuousActions || _waitForAttackProjectileToFinish) _activeSkillProjectile = proj;
            Vector3 scale = proj.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dirX;
            proj.transform.localScale = scale;

            var projectileScript = proj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                Vector2 fireDir = _isFacingRight ? Vector2.right : Vector2.left;
                float power = _stats != null ? _stats.AttackPower * damageMultiplier : 10f * damageMultiplier;
                Vector2? landingPoint = null;
                if (_player != null)
                {
                    Collider2D playerBody = _player.GetComponent<Collider2D>();
                    if (playerBody != null)
                    {
                        Bounds playerBounds = playerBody.bounds;
                        float frontX = dirX > 0f ? playerBounds.min.x - 0.35f : playerBounds.max.x + 0.35f;
                        float groundY = playerBounds.min.y;
                        foreach (var hit in Physics2D.RaycastAll(
                            new Vector2(frontX, playerBounds.min.y + 1f), Vector2.down, 6f))
                        {
                            if (hit.collider == null || hit.collider.isTrigger
                                || hit.collider.transform.IsChildOf(_player.transform)
                                || hit.collider.transform.IsChildOf(transform)
                                || hit.point.y > playerBounds.min.y + 0.25f)
                                continue;
                            groundY = hit.point.y;
                            break;
                        }
                        landingPoint = new Vector2(frontX, groundY + 0.35f);
                    }
                }
                projectileScript.Initialize(fireDir, power, gameObject, landingPoint);
                _fantasyMushroomAudio?.PlaySporeShot();
                _fireWormAudio?.PlayFireballLaunch();
                _flyingEyeAudio?.PlayEyeBeam();
            }
        }

        private Vector3 GetProjectileSpawnPosition(float directionX)
        {
            if (_useProjectileSpawnOffset)
                return transform.position + new Vector3(_projectileSpawnOffset.x * directionX,
                    _projectileSpawnOffset.y, 0f);

            if (_useColliderEdgeAttackRanges)
            {
                var sprite = GetComponent<SpriteRenderer>();
                if (sprite != null && sprite.sprite != null)
                {
                    Bounds visibleBounds = SpriteVisualBounds.GetWorldBounds(sprite);
                    float mouthSide = directionX >= 0f ? visibleBounds.max.x : visibleBounds.min.x;
                    return new Vector3(mouthSide + directionX * 0.05f, visibleBounds.center.y, transform.position.z);
                }
            }

            return transform.position + new Vector3(_projectileSpawnOffset.x * directionX, _projectileSpawnOffset.y, 0f);
        }

        private void FaceTarget(Vector3 targetPos)
        {
            float deltaX = targetPos.x - transform.position.x;
            if (Mathf.Abs(deltaX) > _facingFlipDeadZone)
                FaceDirection(deltaX > 0f);
        }

        private void SetHorizontalVelocity(float targetVelocity)
        {
            Vector2 velocity = _rb.linearVelocity;
            if (_facingFlipDeadZone > 0f)
            {
                float turnAcceleration = Mathf.Max(1f, _chaseSpeed * 8f);
                velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity, turnAcceleration * Time.deltaTime);
            }
            else
            {
                velocity.x = targetVelocity;
            }

            _rb.linearVelocity = velocity;
        }

        private void FaceDirection(bool faceRight)
        {
            _isFacingRight = faceRight;
            if (_flipSpriteInsteadOfTransformScale)
            {
                Vector3 rootScale = transform.localScale;
                if (rootScale.x < 0f)
                {
                    rootScale.x = Mathf.Abs(rootScale.x);
                    transform.localScale = rootScale;
                }

                var spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = faceRight != _initialFacingRight;
                    return;
                }
            }

            Vector3 scale = transform.localScale;
            float targetSign = GetComponent<DragonAudioController>() != null
                ? 1f : _initialFacingRight ? (faceRight ? 1f : -1f) : (faceRight ? -1f : 1f);
            scale.x = Mathf.Abs(scale.x) * targetSign;
            transform.localScale = scale;
        }

        public void NotifyDamaged(DamageData data)
        {
            if (_stats.IsDead) return;
            if (_holdingFireballRain) return;

            // A cyclic action finishes before the next action or hurt pose can start.
            bool shouldInterruptAttack = _interruptAttackOnDamage || _useMovementAnimationStates;
            if ((_continuousActions || _waitForAttackAnimationToFinish) && _isActionLocked
                && !shouldInterruptAttack) return;

            // Cancel active attacks before the hurt animation, including authored
            // movement-state enemies whose attack animation is not action-locked.
            if (shouldInterruptAttack)
                StopAttack();

            _currentState = EnemyAIState.Hurt;
            if (_useMovementAnimationStates)
            {
                _hurtAnimationPending = true;
                _hurtAnimationPendingUntil = Time.time + 0.5f;
            }
            // Force the reaction state so damage received during an attack cannot
            // leave the Animator waiting on an interrupted trigger transition.
            if (_interruptAttackOnDamage && TryPlayAnimatorState("TakeHit"))
            {
                if (_availableAnimParams.Contains("Hurt")) _animator.ResetTrigger("Hurt");
            }
            else if (_useMovementAnimationStates && TryPlayAnimatorState("Hit"))
            {
                // The Hit clip has its own frames; no trigger transition is needed.
            }
            else if (_animator != null && _animator.HasState(0, Animator.StringToHash("TakeHit")))
            {
                if (_availableAnimParams.Contains("Hurt")) _animator.ResetTrigger("Hurt");
                if (_availableAnimParams.Contains("Attack")) _animator.ResetTrigger("Attack");
                _animator.Play("TakeHit", 0, 0f);
            }
            else if (_animator != null && _animator.HasState(0, Animator.StringToHash("Base Layer.Hurt")))
            {
                if (_availableAnimParams.Contains("ActionIndex"))
                    _animator.SetInteger("ActionIndex", -1);
                if (_availableAnimParams.Contains("Hurt")) _animator.ResetTrigger("Hurt");
                if (_availableAnimParams.Contains("Attack")) _animator.ResetTrigger("Attack");
                _animator.Play("Base Layer.Hurt", 0, 0f);
            }
            else
            {
                SetAnimTrigger("Hurt");
            }

            // Apply slight knockback
            if (data.knockbackForce != Vector2.zero)
            {
                if (_isBoss)
                {
                    // Bosses resist upward knockback so they stay firmly grounded
                    _rb.linearVelocity = new Vector2(data.knockbackForce.x * 0.25f, Mathf.Min(0f, _rb.linearVelocity.y));
                }
                else
                {
                    _rb.linearVelocity = data.knockbackForce;
                }
            }
        }

        private void HandleDeath()
        {
            _deathPosition = transform.position;
            bool willRespawn = _canRespawn && (_maxRespawns < 0 || _respawnsUsed < _maxRespawns);
            if (willRespawn)
                _respawnsUsed++;

            StopAttack();
            _currentState = EnemyAIState.Dead;
            SetAnimBool("IsDead", true);
            int deathStateHash = Animator.StringToHash("Base Layer.Death");
            if (_animator != null && _animator.HasState(0, deathStateHash))
            {
                if (_availableAnimParams.Contains("ActionIndex"))
                    _animator.SetInteger("ActionIndex", -1);
                if (_availableAnimParams.Contains("Attack"))
                    _animator.ResetTrigger("Attack");
                if (_availableAnimParams.Contains("Hurt"))
                    _animator.ResetTrigger("Hurt");
                _animator.Play(deathStateHash, 0, 0f);
            }
            _rb.linearVelocity = Vector2.zero;
            if (_isFlying)
            {
                // Let flying monsters fall after death. Keep their solid body
                // collider enabled so it can land on the floor, but turn off
                // trigger hitboxes so the corpse cannot keep attacking.
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.gravityScale = 2.5f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                if (!willRespawn) StartCoroutine(FitFlyingDeathColliderToSprite());
            }
            else
            {
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }

            // Ground enemies keep their previous death behavior. Flying enemies
            // retain only their solid collider to land, disabling all attack triggers.
            foreach (var col in _colliders)
            {
                if (col == null) continue;
                if (_isFlying && !col.isTrigger) continue;
                col.enabled = false;
            }

            if (willRespawn)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                Destroy(gameObject, _deathDestroyDelay);
            }
        }

        private IEnumerator FitFlyingDeathColliderToSprite()
        {
            // Let the Animator switch to the first death frame, then fit the
            // landing collider to the visible sprite instead of its oversized
            // transparent texture rectangle. This keeps the corpse above ground.
            yield return null;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            var bodyCollider = GetComponent<CapsuleCollider2D>();
            if (spriteRenderer == null || spriteRenderer.sprite == null || bodyCollider == null) yield break;

            Bounds visibleBounds = TheLastKnight.Combat.SpriteVisualBounds.GetWorldBounds(spriteRenderer);
            Vector3 localCenter = transform.InverseTransformPoint(visibleBounds.center);
            Vector3 scale = transform.lossyScale;
            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            bodyCollider.offset = new Vector2(localCenter.x, localCenter.y);
            bodyCollider.size = new Vector2(
                visibleBounds.size.x / scaleX + 0.08f / scaleX,
                visibleBounds.size.y / scaleY + 0.08f / scaleY);
        }

        private IEnumerator RespawnRoutine()
        {
            // 1. Wait for death animation to finish playing
            yield return new WaitForSeconds(_deathDestroyDelay);

            // 2. Hide visuals and floating UI
            SetVisibility(false);

            // 3. Wait for configured respawn timer
            yield return new WaitForSeconds(Mathf.Max(0.1f, _respawnTime));

            // 4. Distance check: Do not respawn if player is within detection range of spawn position!
            // มอนสเตอร์จะไม่เกิดถ้า player อยู่ใกล้จุดเกิดในระยะเท่ากับระยะการมองเห็น (_detectionRange)
            while (_requirePlayerAwayToRespawn)
            {
                if (_player == null)
                {
                    FindPlayer();
                }

                if (_player != null)
                {
                    float distToSpawn = Vector2.Distance(_player.transform.position, _spawnPosition);
                    if (distToSpawn > _detectionRange)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            }

            // 5. Restore at the death location when this monster uses a revival life.
            Vector3 respawnPosition = _respawnAtDeathPosition ? _deathPosition : _spawnPosition;
            _archDemonAudio?.PlayResurrection();
            transform.position = respawnPosition;
            transform.rotation = _spawnRotation;
            _startX = respawnPosition.x;
            _isFacingRight = _initialFacingRight;
            FaceDirection(_isFacingRight);

            // 6. Restore physics
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearVelocity = Vector2.zero;
            _rb.gravityScale = _isFlying ? 0f : 2.5f;

            // 7. Re-enable colliders
            foreach (var col in _colliders)
            {
                if (col != null) col.enabled = true;
            }

            // 8. Revive stats
            if (_stats != null)
            {
                _stats.Revive();
            }

            // 9. Reset animation
            SetAnimBool("IsDead", false);
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            bool playedRespawnEffect = false;
            int hurtStateHash = Animator.StringToHash("Base Layer.Hurt");
            if (_respawnAtDeathPosition && _animator != null && _animator.HasState(0, hurtStateHash))
            {
                _animator.Play(hurtStateHash, 0, 0f);
                playedRespawnEffect = true;
            }
            if (!playedRespawnEffect && _animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Idle", 0, 0f);
            }

            // 10. Re-enable visuals and floating UI
            // FloatingHealthBar hides its own GameObject on death, so restoring
            // child renderers and canvases alone cannot make that UI visible again.
            var healthBars = GetComponentsInChildren<TheLastKnight.Combat.FloatingHealthBar>(true);
            foreach (var healthBar in healthBars)
            {
                if (healthBar != null && !healthBar.gameObject.activeSelf)
                    healthBar.gameObject.SetActive(true);
            }
            SetVisibility(true);

            _damageUntil = 0f;
            _isActionLocked = false;
            _nextCyclicSkill = 0;
            _parryableSkillUseCount = 0;
            _basicMeleeOpportunityCount = 0;
            foreach (var skill in _skills)
                if (skill != null) skill.nextReadyTime = Time.time + skill.initialDelay;
            _currentState = EnemyAIState.Idle;
        }

        private void SetVisibility(bool visible)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.enabled = visible;
            }

            var canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                c.enabled = visible;
            }
        }

        private void StartReturningToSpawn()
        {
            _currentState = EnemyAIState.ReturningToSpawn;
            _isActionLocked = false;
            _damageUntil = 0f;
            SetAnimBool("IsChasing", false);
            SetAnimBool("IsMoving", true);
        }

        private void ReturnToSpawn()
        {
            float distToSpawnX = Mathf.Abs(transform.position.x - _spawnPosition.x);
            float totalDist = Vector2.Distance(transform.position, _spawnPosition);

            // Reached original spawn position
            float arrivalDistance = _avoidTeleportOnReturn ? Mathf.Max(0.05f, _chaseSpeed * Time.fixedDeltaTime) : 0.4f;
            if ((_isFlying && totalDist <= arrivalDistance) || (!_isFlying && distToSpawnX <= arrivalDistance))
            {
                _rb.linearVelocity = Vector2.zero;
                if (!_avoidTeleportOnReturn)
                    transform.position = new Vector3(_spawnPosition.x, _isFlying ? _spawnPosition.y : transform.position.y, transform.position.z);
                _startX = _spawnPosition.x;
                _currentState = EnemyAIState.Patrol;
                SetAnimBool("IsMoving", false);

                // ฟื้นฟู HP เต็มหลอดเมื่อกลับถึงจุดเกิด
                if (_stats != null && _stats.CurrentHealth < _stats.MaxHealth)
                {
                    _stats.Heal(_stats.MaxHealth);
                    FloatingCombatText.Show(transform.position, "Full HP", Color.green);
                }
                return;
            }

            // Move back towards spawn point
            Vector2 toSpawn = _spawnPosition - transform.position;
            float dirX = Mathf.Sign(toSpawn.x);

            if (_isFlying)
            {
                _rb.linearVelocity = toSpawn.normalized * _chaseSpeed;
                FaceDirection(dirX > 0);
            }
            else
            {
                // Ground enemy ledge check while returning
                if (_avoidLedges)
                {
                    Vector2 ledgeCheckOrigin = new Vector2(transform.position.x + dirX * _ledgeForwardOffset, transform.position.y);
                    RaycastHit2D groundHit = Physics2D.Raycast(ledgeCheckOrigin, Vector2.down, _groundCheckDistance, _groundLayer);
                    if (groundHit.collider == null)
                    {
                        if (_avoidTeleportOnReturn)
                        {
                            // Stop at the safe edge instead of teleporting through the blocked route.
                            _rb.linearVelocity = Vector2.zero;
                            _spawnPosition = transform.position;
                            _startX = transform.position.x;
                            _currentState = EnemyAIState.Patrol;
                            SetAnimBool("IsMoving", false);
                        }
                        else
                        {
                            transform.position = _spawnPosition;
                            if (_stats != null && _stats.CurrentHealth < _stats.MaxHealth)
                            {
                                _stats.Heal(_stats.MaxHealth);
                                FloatingCombatText.Show(transform.position, "Full HP", Color.green);
                            }
                            _currentState = EnemyAIState.Patrol;
                        }
                        return;
                    }
                }

                SetHorizontalVelocity(dirX * _chaseSpeed);
                FaceDirection(dirX > 0);
            }

            SetAnimBool("IsMoving", true);
        }

        public void SetSpawnPosition(Vector3 position)
        {
            _spawnPosition = position;
            _startX = position.x;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? _spawnPosition : transform.position;

            // Detection Range (Yellow Wire Sphere)
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawWireSphere(center, _detectionRange);

            // Leash Range (2x Detection Range) for non-bosses (Red Wire Sphere)
            if (!_isBoss)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.25f);
                Gizmos.DrawWireSphere(center, _detectionRange * _leashRangeMultiplier);
            }

            // Short Patrol Range around spawn point (Green Line)
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.45f);
            Gizmos.DrawLine(center + Vector3.left * _patrolDistance, center + Vector3.right * _patrolDistance);

            // Respawn indicator
            if (_canRespawn)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                Gizmos.DrawWireCube(center, Vector3.one * 0.8f);
            }
        }
#endif

        public void SetCombatConfiguration(float hp, float attack, float patrolSpd, float chaseSpd, bool hasRanged, GameObject projPrefab = null, GameObject spellPrefab = null)
        {
            if (_stats != null)
            {
                _stats.SetStats(hp, 0f, attack);
            }
            _patrolSpeed = patrolSpd;
            _chaseSpeed = chaseSpd;
            _hasRangedAttack = hasRanged;
            _projectilePrefab = projPrefab;
            _groundSpellPrefab = spellPrefab;
        }
    

private Vector3 GetGroundSpellSpawnPosition(GameObject target)
        {
            if (target == null) return transform.position;

            // The target transform is often at the character's center. Ground
            // telegraphs must start at the bottom edge of its solid collider.
            Collider2D groundCollider = target.GetComponent<Collider2D>();
            if (groundCollider == null || !groundCollider.enabled || groundCollider.isTrigger)
            {
                var colliders = target.GetComponentsInChildren<Collider2D>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
                    {
                        groundCollider = colliders[i];
                        break;
                    }
                }
            }

            float groundY = groundCollider != null ? groundCollider.bounds.min.y : target.transform.position.y;
            return new Vector3(target.transform.position.x, groundY, transform.position.z);
        }


        private void UpdateAttackSortingOrder()
        {
            if (!_bringToFrontWhileAttacking || _spriteRenderer == null || _animator == null) return;
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            bool isSlashing = stateInfo.IsName("Attack") || stateInfo.IsName("Attack3") || stateInfo.IsName("Attack_01") || stateInfo.IsName("Attack_02") ||
                stateInfo.IsName("Jump") || stateInfo.IsName("Shout") ||
                (!string.IsNullOrEmpty(_basicAttackAnimState) && stateInfo.IsName(_basicAttackAnimState));
            if (!isSlashing && _basicAttackAnimStates != null)
            {
                for (int i = 0; i < _basicAttackAnimStates.Length; i++)
                {
                    if (!string.IsNullOrEmpty(_basicAttackAnimStates[i]) && stateInfo.IsName(_basicAttackAnimStates[i]))
                    {
                        isSlashing = true;
                        break;
                    }
                }
            }
            int targetOrder = isSlashing ? _attackSortingOrder : _defaultSortingOrder;
            string targetLayer = isSlashing && !string.IsNullOrEmpty(_attackSortingLayerName)
                ? _attackSortingLayerName
                : _defaultSortingLayerName;
            if (_spriteRenderer.sortingLayerName != targetLayer)
                _spriteRenderer.sortingLayerName = targetLayer;
            if (_spriteRenderer.sortingOrder != targetOrder)
                _spriteRenderer.sortingOrder = targetOrder;
        }
}
}
