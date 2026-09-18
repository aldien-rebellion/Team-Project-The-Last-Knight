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
        [SerializeField] private float _patrolDistance = 5f;
        [SerializeField] private bool _isFlying = false;
        [SerializeField] private bool _avoidLedges = true;
        [SerializeField] private bool _initialFacingRight = true;

        [Header("Obstacle & Ground Detection")]
        [SerializeField] private LayerMask _groundLayer = ~0;
        [SerializeField] private float _groundCheckDistance = 1.2f;
        [SerializeField] private float _wallCheckDistance = 0.6f;
        [SerializeField] private float _ledgeForwardOffset = 0.5f;

        [Header("Combat Ranges")]
        [SerializeField] private float _detectionRange = 7f;
        [SerializeField] private float _meleeRange = 1.4f;
        [SerializeField] private float _meleeCooldown = 1.5f;

        [Header("Ranged Combat")]
        [SerializeField] private bool _hasRangedAttack = false;
        [SerializeField] private float _rangedRange = 8f;
        [SerializeField] private float _rangedCooldown = 3.0f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Vector2 _projectileSpawnOffset = new Vector2(0.6f, 0.2f);
        [SerializeField] private GameObject _groundSpellPrefab;

        [Header("Death Settings")]
        [SerializeField] private float _deathDestroyDelay = 1.5f;

        // Components
        private Rigidbody2D _rb;
        private Animator _animator;
        private EnemyStats _stats;
        private Collider2D[] _colliders;
        private GameObject _player;

        // State Machine
        private EnemyAIState _currentState = EnemyAIState.Idle;
        private bool _isFacingRight;
        private float _startX;
        private bool _movingRight = true;
        private float _nextMeleeTime = 0f;
        private float _nextRangedTime = 0f;
        private bool _isActionLocked = false;

        public EnemyAIState CurrentState => _currentState;
        public bool IsFlying => _isFlying;

        private readonly System.Collections.Generic.HashSet<string> _availableAnimParams = new System.Collections.Generic.HashSet<string>();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<EnemyStats>();
            _colliders = GetComponentsInChildren<Collider2D>();

            _isFacingRight = _initialFacingRight;
            _startX = transform.position.x;

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    _availableAnimParams.Add(p.name);
                }
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

            if (_stats != null)
            {
                _stats.OnDamaged += HandleDamaged;
                _stats.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnDamaged -= HandleDamaged;
                _stats.OnDeath -= HandleDeath;
            }
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
            if (_stats.IsDead) return;

            if (_player == null)
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
            if (stateInfo.IsName("Hurt") || stateInfo.IsName("Attack") || stateInfo.IsName("Attack3") || stateInfo.IsName("Cast") || _isActionLocked)
            {
                _rb.linearVelocity = new Vector2(0f, _isFlying ? 0f : _rb.linearVelocity.y);
                SetAnimBool("IsMoving", false);
                SetAnimBool("IsChasing", false);
                return;
            }

            float distToPlayer = Vector2.Distance(transform.position, _player.transform.position);

            if (distToPlayer <= _meleeRange && Time.time >= _nextMeleeTime)
            {
                FaceTarget(_player.transform.position);
                PerformMeleeAttack();
            }
            else if (_hasRangedAttack && distToPlayer <= _rangedRange && distToPlayer > _meleeRange && Time.time >= _nextRangedTime)
            {
                FaceTarget(_player.transform.position);
                PerformRangedAttack();
            }
            else if (distToPlayer <= _detectionRange)
            {
                ChasePlayer();
            }
            else
            {
                Patrol();
            }
        }

        private void Patrol()
        {
            _currentState = EnemyAIState.Patrol;
            SetAnimBool("IsChasing", false);
            SetAnimBool("IsMoving", true);

            float currentX = transform.position.x;
            float moveDir = _movingRight ? 1f : -1f;

            // Check ledge and wall before moving
            if (!_isFlying && _avoidLedges)
            {
                Vector2 ledgeCheckOrigin = new Vector2(transform.position.x + moveDir * _ledgeForwardOffset, transform.position.y);
                RaycastHit2D groundHit = Physics2D.Raycast(ledgeCheckOrigin, Vector2.down, _groundCheckDistance, _groundLayer);
                if (groundHit.collider == null)
                {
                    _movingRight = !_movingRight;
                    FaceDirection(_movingRight);
                    return;
                }
            }

            // Wall check
            RaycastHit2D wallHit = Physics2D.Raycast(transform.position, Vector2.right * moveDir, _wallCheckDistance, _groundLayer);
            if (wallHit.collider != null && !wallHit.collider.isTrigger && wallHit.collider.gameObject != gameObject)
            {
                _movingRight = !_movingRight;
                FaceDirection(_movingRight);
                return;
            }

            // Patrol bounds
            if (_movingRight)
            {
                _rb.linearVelocity = new Vector2(_patrolSpeed, _isFlying ? 0f : _rb.linearVelocity.y);
                FaceDirection(true);
                if (currentX > _startX + _patrolDistance)
                {
                    _movingRight = false;
                }
            }
            else
            {
                _rb.linearVelocity = new Vector2(-_patrolSpeed, _isFlying ? 0f : _rb.linearVelocity.y);
                FaceDirection(false);
                if (currentX < _startX - _patrolDistance)
                {
                    _movingRight = true;
                }
            }
        }

        private void ChasePlayer()
        {
            _currentState = EnemyAIState.Chase;
            SetAnimBool("IsChasing", true);
            SetAnimBool("IsMoving", true);

            Vector2 toPlayer = _player.transform.position - transform.position;
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

                _rb.linearVelocity = new Vector2(dirX * _chaseSpeed, _rb.linearVelocity.y);
            }

            FaceDirection(dirX > 0);
            _startX = transform.position.x; // Shift patrol center with chase
        }

        private void PerformMeleeAttack()
        {
            _currentState = EnemyAIState.MeleeAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            SetAnimTrigger("Attack");

            _nextMeleeTime = Time.time + _meleeCooldown;
        }

        private void PerformRangedAttack()
        {
            _currentState = EnemyAIState.RangedAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);

            // Trigger animation
            SetAnimTrigger("Attack");

            _nextRangedTime = Time.time + _rangedCooldown;

            // Spawn projectile with a slight anticipation delay
            StartCoroutine(DelayedProjectileRoutine(0.35f));
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
            if (_stats.IsDead) return;

            // Ground spell check (e.g. BringerOfDeath Spell, Jinn Magic)
            if (_groundSpellPrefab != null && _player != null)
            {
                Vector3 spellPos = new Vector3(_player.transform.position.x, _player.transform.position.y, 0f);
                Instantiate(_groundSpellPrefab, spellPos, Quaternion.identity);
                return;
            }

            if (_projectilePrefab == null) return;

            float dirX = _isFacingRight ? 1f : -1f;
            Vector3 spawnPos = transform.position + new Vector3(_projectileSpawnOffset.x * dirX, _projectileSpawnOffset.y, 0f);

            GameObject proj = Instantiate(_projectilePrefab, spawnPos, Quaternion.identity);

            // Orient projectile towards facing direction
            Vector3 scale = proj.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dirX;
            proj.transform.localScale = scale;

            var projectileScript = proj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                Vector2 fireDir = _isFacingRight ? Vector2.right : Vector2.left;
                if (_player != null && _isFlying)
                {
                    // Target player directly if flying
                    fireDir = ((Vector2)_player.transform.position - (Vector2)spawnPos).normalized;
                }
                projectileScript.Initialize(fireDir, _stats.AttackPower, gameObject);
            }
        }

        private void FaceTarget(Vector3 targetPos)
        {
            FaceDirection(targetPos.x > transform.position.x);
        }

        private void FaceDirection(bool faceRight)
        {
            _isFacingRight = faceRight;
            Vector3 scale = transform.localScale;
            float targetSign = _initialFacingRight ? (faceRight ? 1f : -1f) : (faceRight ? -1f : 1f);
            scale.x = Mathf.Abs(scale.x) * targetSign;
            transform.localScale = scale;
        }

        private void HandleDamaged(DamageData data)
        {
            if (_stats.IsDead) return;

            _currentState = EnemyAIState.Hurt;
            SetAnimTrigger("Hurt");

            // Apply slight knockback
            if (data.knockbackForce != Vector2.zero)
            {
                _rb.linearVelocity = data.knockbackForce;
            }
        }

        private void HandleDeath()
        {
            _currentState = EnemyAIState.Dead;
            SetAnimBool("IsDead", true);
            _rb.linearVelocity = Vector2.zero;
            _rb.bodyType = RigidbodyType2D.Kinematic;

            // Disable all colliders so it doesn't block player or absorb attacks
            foreach (var col in _colliders)
            {
                if (col != null) col.enabled = false;
            }

            Destroy(gameObject, _deathDestroyDelay);
        }

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
    }
}
