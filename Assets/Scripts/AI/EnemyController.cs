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
        [SerializeField] private bool _avoidLedges = true;
        [SerializeField] private bool _initialFacingRight = true;

        [Header("Boss & Leash Settings")]
        [Tooltip("If true, this monster is considered a Boss and will not leash/return to spawn or reset HP when player runs far away.")]
        [SerializeField] private bool _isBoss = false;
        public bool IsBoss => _isBoss;
        [Tooltip("Multiplier of detection range used as the leash boundary from spawn point (default: 2.0x).")]
        [SerializeField] private float _leashRangeMultiplier = 2.0f;

        [Header("Obstacle & Ground Detection")]
        [SerializeField] private LayerMask _groundLayer = ~0;
        [SerializeField] private float _groundCheckDistance = 1.2f;
        [SerializeField] private float _wallCheckDistance = 0.6f;
        [SerializeField] private float _ledgeForwardOffset = 0.5f;

        [Header("Combat Ranges")]
        [SerializeField] private float _detectionRange = 7f;
        [SerializeField] private float _meleeRange = 1.4f;
        [SerializeField] private float _meleeCooldown = 1.5f;
        [Tooltip("When enabled, this enemy waits for configured skills instead of using the basic melee attack.")]
        [SerializeField] private bool _disableBasicAttack;

        [Header("Ranged Combat")]
        [SerializeField] private bool _hasRangedAttack = false;
        [SerializeField] private float _rangedRange = 8f;
        [SerializeField] private float _rangedCooldown = 3.0f;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private Vector2 _projectileSpawnOffset = new Vector2(0.6f, 0.2f);
        [SerializeField] private GameObject _groundSpellPrefab;

        [Header("Basic Attack & Parry Settings")]
        [SerializeField] private string _basicAttackAnimState = "Attack";
        [Tooltip("Damage multiplier for basic attack (always 1.0x ATK).")]
        [SerializeField] private float _basicAttackMultiplier = 1.0f;
        [Tooltip("If true and this monster has no parryable skills, basic attack triggers the Parry timing ring with a cooldown.")]
        [SerializeField] private bool _basicAttackCanParry = true;
        [Tooltip("Minimum cooldown in seconds between Parry rings on basic attack (minimum 4.0s).")]
        [SerializeField] private float _basicParryCooldown = 4.0f;

        [Header("Skills Configuration")]
        [Tooltip("Special attacks and skills configured from the monster's Animation Controller.")]
        [SerializeField] private TheLastKnight.Combat.EnemySkill[] _skills = new TheLastKnight.Combat.EnemySkill[0];
        public TheLastKnight.Combat.EnemySkill[] Skills => _skills;

        [Header("Death & Respawn Settings")]
        [SerializeField] private float _deathDestroyDelay = 1.5f;
        [Tooltip("If true, this monster will respawn after being defeated.")]
        [SerializeField] private bool _canRespawn = false;
        public bool CanRespawn => _canRespawn;
        [Tooltip("Time in seconds before the monster attempts to respawn after death.")]
        [SerializeField] private float _respawnTime = 30f;
        public float RespawnTime => _respawnTime;

        // Components
        private Rigidbody2D _rb;
        private Animator _animator;
        private EnemyStats _stats;
        private Collider2D[] _colliders;
        private Collider2D _attackHitbox;
        private GameObject _player;

        // Spawn / Respawn Tracking
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        // State Machine
        private EnemyAIState _currentState = EnemyAIState.Idle;
        private bool _isFacingRight;
        private float _startX;
        private bool _movingRight = true;
        private float _nextMeleeTime = 0f;
        private float _nextRangedTime = 0f;
        private float _nextBasicParryTime = 0f;
        private float _currentAttackMultiplier = 1.0f;
        private bool _isActionLocked = false;
        private ParryReceiver _parry;
        private float _damageUntil;
        private bool _projectileSpawned;
        public bool CanDealMeleeDamage => !_stats.IsDead && !_parry.IsStaggered && Time.time < _damageUntil;
        public float CurrentAttackDamage => _stats != null ? _stats.AttackPower * _currentAttackMultiplier : 10f;
        public float CurrentAttackMultiplier => _currentAttackMultiplier;

        public EnemyAIState CurrentState => _currentState;
        public bool IsFlying => _isFlying;

        private readonly System.Collections.Generic.HashSet<string> _availableAnimParams = new System.Collections.Generic.HashSet<string>();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _stats = GetComponent<EnemyStats>();
            _parry = GetComponent<ParryReceiver>();
            if (_parry == null) _parry = gameObject.AddComponent<ParryReceiver>();
            _colliders = GetComponentsInChildren<Collider2D>();
            var attackHitbox = GetComponentInChildren<EnemyHitbox2D>(true);
            _attackHitbox = attackHitbox != null ? attackHitbox.GetComponent<Collider2D>() : null;

            _isFacingRight = _initialFacingRight;
            _startX = transform.position.x;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

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

            if (_parry.IsStaggered)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            if (_player == null)
            {
                FindPlayer();
                if (_player == null)
                {
                    Patrol();
                    return;
                }
            }

            // If currently returning to spawn position, execute return logic
            if (_currentState == EnemyAIState.ReturningToSpawn)
            {
                ReturnToSpawn();
                return;
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

            TheLastKnight.Combat.EnemySkill readySkill = GetReadySkill(distToPlayer);
            if (readySkill != null)
            {
                if (!_disableBasicAttack || CanReachPlayerWithSkill(readySkill))
                {
                    FaceTarget(_player.transform.position);
                    PerformSkill(readySkill);
                }
                else
                {
                    // Keep closing the gap until the actual attack collider can touch
                    // the player; configured max range alone can make a skill whiff.
                    ChasePlayer();
                }
            }
            else if (!_disableBasicAttack && distToPlayer <= _meleeRange && Time.time >= _nextMeleeTime)
            {
                FaceTarget(_player.transform.position);
                PerformMeleeAttack();
            }
            else if (_disableBasicAttack && distToPlayer <= _meleeRange)
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
            else if (distToPlayer <= _detectionRange)
            {
                ChasePlayer();
            }
            else
            {
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

        private void Patrol()
        {
            _currentState = EnemyAIState.Patrol;
            SetAnimBool("IsChasing", false);
            SetAnimBool("IsMoving", true);

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

            // Patrol bounds around spawn point
            if (_movingRight)
            {
                _rb.linearVelocity = new Vector2(_patrolSpeed, _isFlying ? 0f : _rb.linearVelocity.y);
                FaceDirection(true);
                if (currentX > centerOriginX + _patrolDistance)
                {
                    _movingRight = false;
                }
            }
            else
            {
                _rb.linearVelocity = new Vector2(-_patrolSpeed, _isFlying ? 0f : _rb.linearVelocity.y);
                FaceDirection(false);
                if (currentX < centerOriginX - _patrolDistance)
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
            if (_isBoss)
            {
                _startX = transform.position.x;
            }
        }

        private void PerformMeleeAttack()
        {
            _currentState = EnemyAIState.MeleeAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            _nextMeleeTime = Time.time + _meleeCooldown;
            _currentAttackMultiplier = _basicAttackMultiplier;

            // มอนสเตอร์ที่ไม่มีสกิล วง parry จะเกิดขึ้นกับการโจมตีปกติ (Basic Attack) แต่มีคูลดาวน์อย่างน้อย 4 วินาที
            bool canParryThisTime = false;
            if (!HasParryableSkill() && _basicAttackCanParry)
            {
                if (Time.time >= _nextBasicParryTime)
                {
                    canParryThisTime = true;
                    _nextBasicParryTime = Time.time + Mathf.Max(4.0f, _basicParryCooldown);
                }
            }

            StartCoroutine(WindupAttack(false, canParryThisTime));
        }

        private void PerformRangedAttack()
        {
            _currentState = EnemyAIState.RangedAttack;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);
            _nextRangedTime = Time.time + _rangedCooldown;
            _currentAttackMultiplier = _basicAttackMultiplier;

            bool canParryThisTime = false;
            if (!HasParryableSkill() && _basicAttackCanParry)
            {
                if (Time.time >= _nextBasicParryTime)
                {
                    canParryThisTime = true;
                    _nextBasicParryTime = Time.time + Mathf.Max(4.0f, _basicParryCooldown);
                }
            }

            StartCoroutine(WindupAttack(true, canParryThisTime));
        }

        private IEnumerator WindupAttack(bool ranged, bool canParry)
        {
            _isActionLocked = true;
            _projectileSpawned = false;

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
                // Unparried attack: short anticipation delay
                yield return new WaitForSeconds(0.15f);
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    _isActionLocked = false;
                    yield break;
                }
            }

            PlayAnimationAction(_basicAttackAnimState);
            _damageUntil = Time.time + 0.35f;
            if (ranged) SpawnProjectile();
            yield return new WaitForSeconds(0.35f);
            float remainingAnimationTime = GetAnimationDuration(_basicAttackAnimState, 0.35f) - 0.35f;
            if (remainingAnimationTime > 0f)
            {
                yield return new WaitForSeconds(remainingAnimationTime);
            }
            _isActionLocked = false;
        }

        private void PerformSkill(TheLastKnight.Combat.EnemySkill skill)
        {
            _currentState = EnemyAIState.Skill;
            _rb.linearVelocity = Vector2.zero;
            SetAnimBool("IsMoving", false);
            SetAnimBool("IsChasing", false);

            skill.nextReadyTime = Time.time + skill.cooldown;
            _currentAttackMultiplier = skill.damageMultiplier;

            StartCoroutine(ExecuteSkillRoutine(skill));
        }

        private IEnumerator ExecuteSkillRoutine(TheLastKnight.Combat.EnemySkill skill)
        {
            _isActionLocked = true;
            _projectileSpawned = false;

            // ท่าที่สามารถ Parry ได้ จะแสดงวงกลม Timing Ring
            if (skill.isParryable && _parry != null)
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
                yield return new WaitForSeconds(0.15f);
                if (_stats.IsDead || _parry.IsStaggered)
                {
                    _isActionLocked = false;
                    yield break;
                }
            }

            PlayAnimationAction(skill.animationName, skill.actionIndex);
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
                _isActionLocked = false;
                yield break;
            }

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
                _damageUntil = Time.time + Mathf.Max(0f, skill.damageDuration);
            }

            if (skill.groundSpellPrefab != null && _player != null)
            {
                Vector3 spellPos = new Vector3(_player.transform.position.x, _player.transform.position.y, 0f);
                var spellObj = Instantiate(skill.groundSpellPrefab, spellPos, Quaternion.identity);
                var spellArea = spellObj.GetComponent<GroundSpellArea>();
                if (spellArea != null && _stats != null)
                {
                    spellArea.Initialize(_stats.AttackPower * skill.damageMultiplier, gameObject);
                }
            }
            else if (skill.projectilePrefab != null)
            {
                SpawnCustomProjectile(skill.projectilePrefab, skill.damageMultiplier);
            }

            float remainingAnimationTime = GetAnimationDuration(skill.animationName, elapsedAnimationTime) - elapsedAnimationTime;
            if (remainingAnimationTime > 0f)
            {
                yield return new WaitForSeconds(remainingAnimationTime);
            }
            _currentAttackMultiplier = _basicAttackMultiplier;
            _isActionLocked = false;
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
                if (clip != null && clip.name == animationName)
                {
                    return clip.length / Mathf.Max(0.01f, _animator.speed);
                }
            }

            return fallbackDuration;
        }

        private void PlayAnimationAction(string animName, int actionIndex = -1)
        {
            if (_animator == null) return;

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

                int stateHash = Animator.StringToHash(animName);
                if (_animator.HasState(0, stateHash))
                {
                    _animator.Play(stateHash, 0, 0f);
                    return;
                }
            }

            SetAnimTrigger("Attack");
        }

        public TheLastKnight.Combat.EnemySkill GetReadySkill(float distToPlayer)
        {
            if (_skills == null || _skills.Length == 0) return null;

            for (int i = 0; i < _skills.Length; i++)
            {
                var skill = _skills[i];
                if (skill != null && skill.IsReady(distToPlayer, Time.time))
                {
                    return skill;
                }
            }
            return null;
        }

        private bool CanReachPlayerWithSkill(TheLastKnight.Combat.EnemySkill skill)
        {
            if (skill == null || _player == null) return false;
            if (skill.projectilePrefab != null || skill.groundSpellPrefab != null) return true;
            if (_attackHitbox == null) return false;

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

        public void CancelAttack()
        {
            StopAllCoroutines();
            _damageUntil = 0f;
            _currentAttackMultiplier = _basicAttackMultiplier;
            _isActionLocked = false;
            _currentState = EnemyAIState.Hurt;
            SetAnimTrigger("Hurt");
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
                Vector3 spellPos = new Vector3(_player.transform.position.x, _player.transform.position.y, 0f);
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
                float power = _stats != null ? _stats.AttackPower * _currentAttackMultiplier : 10f;
                projectileScript.Initialize(fireDir, power, gameObject);
            }
        }

        private void SpawnCustomProjectile(GameObject prefab, float damageMultiplier)
        {
            if (_stats.IsDead || _parry.IsStaggered || prefab == null) return;

            float dirX = _isFacingRight ? 1f : -1f;
            Vector3 spawnPos = transform.position + new Vector3(_projectileSpawnOffset.x * dirX, _projectileSpawnOffset.y, 0f);

            GameObject proj = Instantiate(prefab, spawnPos, Quaternion.identity);
            Vector3 scale = proj.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * dirX;
            proj.transform.localScale = scale;

            var projectileScript = proj.GetComponent<EnemyProjectile>();
            if (projectileScript != null)
            {
                Vector2 fireDir = _isFacingRight ? Vector2.right : Vector2.left;
                if (_player != null && _isFlying)
                {
                    fireDir = ((Vector2)_player.transform.position - (Vector2)spawnPos).normalized;
                }
                float power = _stats != null ? _stats.AttackPower * damageMultiplier : 10f * damageMultiplier;
                projectileScript.Initialize(fireDir, power, gameObject);
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

            if (_canRespawn)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                Destroy(gameObject, _deathDestroyDelay);
            }
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
            while (true)
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

            // 5. Reset position and orientation
            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            _startX = _spawnPosition.x;
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
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.Play("Idle", 0, 0f);
            }

            // 10. Re-enable visuals and floating UI
            SetVisibility(true);

            _damageUntil = 0f;
            _isActionLocked = false;
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
            if ((_isFlying && totalDist <= 0.4f) || (!_isFlying && distToSpawnX <= 0.4f))
            {
                _rb.linearVelocity = Vector2.zero;
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
                        // Ledge blocks path, snap to spawn point
                        transform.position = _spawnPosition;
                        if (_stats != null && _stats.CurrentHealth < _stats.MaxHealth)
                        {
                            _stats.Heal(_stats.MaxHealth);
                            FloatingCombatText.Show(transform.position, "Full HP", Color.green);
                        }
                        _currentState = EnemyAIState.Patrol;
                        return;
                    }
                }

                _rb.linearVelocity = new Vector2(dirX * _chaseSpeed, _rb.linearVelocity.y);
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
    }
}
