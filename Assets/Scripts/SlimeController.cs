using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Combat;
using TheLastKnight.Stats;
using TheLastKnight.Player;

public class SlimeController : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    
    [Header("UI")]
    public Image healthBarFill;
    public Text healthPercentText;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public float patrolDistance = 5f;
    
    [Header("Combat")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.0f;
    public float damage = 10f;

    [Header("Jump")]
    public float jumpForce = 8f;
    public float jumpMoveSpeed = 5f;
    public float jumpAttackRange = 5f;    // ระยะที่จะใช้ jump attack แทน chase
    public float jumpCooldown = 3f;       // cooldown ระหว่าง jump
    public float jumpAttackDamageMultiplier = 1.5f;

    private bool movingRight = true;
    private float startX;
    private float nextAttackTime = 0f;
    private int attackSequenceIndex = 0;
    private float nextJumpTime = 0f;
    private bool isJumping = false;
    private bool hasDealtJumpDamage = false;

    private GameObject player;
    private Animator animator;
    private Rigidbody2D rb;
    private bool isDead = false;
    private float deathTimer = 0f;

    private ParryReceiver parryReceiver;
    private EnemyStats enemyStats;
    private bool isAttacking = false;
    private Coroutine attackCoroutine;

    void Start()
    {
        currentHealth = maxHealth;
        startX = transform.position.x;
        animator = GetComponent<Animator>();
        player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        else
        {
            rb.freezeRotation = true;
        }

        enemyStats = GetComponent<EnemyStats>();
        if (enemyStats == null)
        {
            enemyStats = gameObject.AddComponent<EnemyStats>();
            enemyStats.SetStats(maxHealth, 0f, damage);
        }

        parryReceiver = GetComponent<ParryReceiver>();
        if (parryReceiver == null)
        {
            parryReceiver = gameObject.AddComponent<ParryReceiver>();
        }

        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var cap = gameObject.AddComponent<CapsuleCollider2D>();
            cap.size = new Vector2(0.45f, 0.6f);
            cap.offset = new Vector2(0f, 0.3f);
        }
        
        UpdateHealthBar();
    }

    void Update()
    {
        if (isDead) 
        {
            deathTimer -= Time.deltaTime;
            if (deathTimer <= 0)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (enemyStats != null)
        {
            currentHealth = enemyStats.CurrentHealth;
            if (enemyStats.IsDead && !isDead)
            {
                Die();
                return;
            }
        }
        
        if (player == null) return;

        if (parryReceiver != null && parryReceiver.IsStaggered)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
            return;
        }

        if (isAttacking) return;

        // Handle jump state - move toward player mid-air
        if (isJumping)
        {
            HandleJumpMovement();
            return;
        }
        
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Attack") || stateInfo.IsName("Hurt")) {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.transform.position);

        if (distToPlayer <= detectionRange && Time.time >= nextAttackTime)
        {
            FacePlayer();
            // If player is far, try jump attack
            if (distToPlayer > attackRange && distToPlayer <= jumpAttackRange && Time.time >= nextJumpTime)
            {
                DoJumpAttack();
            }
            else
            {
                AttackPlayer();
            }
        }
        else if (distToPlayer <= detectionRange)
        {
            if (distToPlayer > attackRange)
            {
                // Jump over obstacles if player is mid-far range and jump is ready
                if (distToPlayer > attackRange * 2f && Time.time >= nextJumpTime)
                {
                    FacePlayer();
                    DoJumpMove();
                }
                else
                {
                    ChasePlayer();
                }
            }
            else
            {
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsChasing", false);
            }
        }
        else
        {
            Patrol();
        }
    }

    // ---- Jump Methods ----

    void DoJumpAttack()
    {
        if (isJumping) return;
        isJumping = true;
        hasDealtJumpDamage = false;
        nextJumpTime = Time.time + jumpCooldown;
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsChasing", false);
        animator.SetTrigger("Jump");
        // Apply upward + horizontal impulse toward player
        float dirX = Mathf.Sign(player.transform.position.x - transform.position.x);
        if (rb != null) rb.linearVelocity = new Vector2(dirX * jumpMoveSpeed, jumpForce);
    }

    void DoJumpMove()
    {
        if (isJumping) return;
        isJumping = true;
        hasDealtJumpDamage = false;
        nextJumpTime = Time.time + jumpCooldown;
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsChasing", false);
        animator.SetTrigger("Jump");
        float dirX = Mathf.Sign(player.transform.position.x - transform.position.x);
        if (rb != null) rb.linearVelocity = new Vector2(dirX * jumpMoveSpeed, jumpForce);
    }

    void HandleJumpMovement()
    {
        if (rb == null) { isJumping = false; return; }

        // Maintain horizontal drift toward player while airborne
        float dirX = player != null ? Mathf.Sign(player.transform.position.x - transform.position.x) : 0f;
        rb.linearVelocity = new Vector2(dirX * jumpMoveSpeed, rb.linearVelocity.y);

        // Detect landing: moving downward and near the ground (small vertical speed)
        bool isLanding = rb.linearVelocity.y <= 0.1f && rb.linearVelocity.y >= -15f;
        if (isLanding && Mathf.Abs(rb.linearVelocity.y) < 1f)
        {
            // We've landed
            isJumping = false;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Deal jump damage if player is nearby on landing
            if (!hasDealtJumpDamage && player != null &&
                Vector2.Distance(transform.position, player.transform.position) <= attackRange + 0.8f)
            {
                hasDealtJumpDamage = true;
                var pController = player.GetComponent<PlayerController>();
                if (pController == null || !pController.IsInvincible)
                {
                    var stats = player.GetComponent<PlayerStats>();
                    if (stats != null)
                        stats.TakeDamage(damage * jumpAttackDamageMultiplier);
                    else
                        player.SendMessage("TakeDamage", damage * jumpAttackDamageMultiplier,
                            SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    void Patrol()
    {
        animator.SetBool("IsChasing", false);
        animator.SetBool("IsMoving", true);
        
        float currentX = transform.position.x;

        if (movingRight)
        {
            if (rb != null) rb.linearVelocity = new Vector2(patrolSpeed, rb.linearVelocity.y);
            else transform.Translate(Vector2.right * patrolSpeed * Time.deltaTime);
            if (transform.localScale.x < 0) Flip();
            if (currentX > startX + patrolDistance)
            {
                movingRight = false;
                Flip();
            }
        }
        else
        {
            if (rb != null) rb.linearVelocity = new Vector2(-patrolSpeed, rb.linearVelocity.y);
            else transform.Translate(Vector2.left * patrolSpeed * Time.deltaTime);
            if (transform.localScale.x > 0) Flip();
            if (currentX < startX - patrolDistance)
            {
                movingRight = true;
                Flip();
            }
        }
    }

    void ChasePlayer()
    {
        animator.SetBool("IsChasing", true);
        animator.SetBool("IsMoving", true);

        float dir = player.transform.position.x - transform.position.x;
        float velX = Mathf.Sign(dir) * chaseSpeed;

        if (rb != null)
            rb.linearVelocity = new Vector2(velX, rb.linearVelocity.y);
        else
            transform.Translate(new Vector2(velX, 0f) * Time.deltaTime);

        if (dir > 0)
        {
            if (transform.localScale.x < 0) Flip();
            movingRight = true;
        }
        else
        {
            if (transform.localScale.x > 0) Flip();
            movingRight = false;
        }
        
        startX = transform.position.x; 
    }

    void AttackPlayer()
    {
        if (Time.time >= nextAttackTime && !isAttacking)
        {
            nextAttackTime = Time.time + attackCooldown;
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            attackCoroutine = StartCoroutine(PerformAttackRoutine());
        }
        else if (!isAttacking)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    private IEnumerator PerformAttackRoutine()
    {
        float attackStartedAt = Time.time;
        isAttacking = true;
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsChasing", false);

        int currentCombo = attackSequenceIndex;
        attackSequenceIndex = (attackSequenceIndex + 1) % 3;

        string animTrigger = currentCombo == 0 ? "Attack_1" : (currentCombo == 1 ? "Attack_2" : "Attack_3");
        animator.Play(animTrigger, 0, 0f);

        bool isParryable = (currentCombo == 1); // Only Attack_2 is parryable!

        // Always close the previous window before choosing this attack's window.
        parryReceiver?.FinishWindup();
        if (isParryable && parryReceiver != null)
        {
            parryReceiver.BeginWindup();
        }

        float hitTime = attackStartedAt + ParryReceiver.WindupDuration + ParryReceiver.TimingTolerance;
        while (Time.time < hitTime) yield return null;

        if (isParryable && parryReceiver != null)
        {
            parryReceiver.FinishWindup();
        }

        if (isDead || (enemyStats != null && enemyStats.IsDead) ||
            (parryReceiver != null && parryReceiver.IsStaggered))
        {
            isAttacking = false;
            attackCoroutine = null;
            yield break;
        }

        // Deal damage to player if player is in range and not invulnerable
        if (player != null && Vector2.Distance(transform.position, player.transform.position) <= attackRange + 0.5f)
        {
            var pController = player.GetComponent<PlayerController>();
            if (pController == null || !pController.IsInvincible)
            {
                var stats = player.GetComponent<PlayerStats>();
                if (stats != null)
                {
                    stats.TakeDamage(damage);
                }
                else
                {
                    player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // Use the same start time so frame delays do not extend the one-second cadence.
        float recoveryEnd = hitTime + 0.2f;
        while (Time.time < recoveryEnd) yield return null;
        isAttacking = false;
        attackCoroutine = null;
    }

    public void CancelAttack()
    {
        StopAttack();
        if (!isDead && animator != null) animator.SetTrigger("Hurt");
    }

    private void StopAttack()
    {
        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }
        parryReceiver?.FinishWindup();
        isAttacking = false;
        isJumping = false;
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void OnDisable()
    {
        StopAttack();
    }

    private void FacePlayer()
    {
        float direction = player.transform.position.x - transform.position.x;
        if ((direction > 0f && transform.localScale.x < 0f) ||
            (direction < 0f && transform.localScale.x > 0f)) Flip();
    }

    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
    
    public void TakeDamage(float amount)
    {
        if (isDead) return;
        
        if (enemyStats != null)
        {
            enemyStats.TakeDamage(amount);
            currentHealth = enemyStats.CurrentHealth;
        }
        else
        {
            currentHealth -= amount;
        }
        
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
        else 
        {
            animator.SetTrigger("Hurt");
        }
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarFill != null)
        {
            float pct = Mathf.Clamp01(currentHealth / maxHealth);
            healthBarFill.fillAmount = pct;

            var fillRt = healthBarFill.rectTransform;
            if (fillRt != null)
            {
                Vector2 maxAnchor = fillRt.anchorMax;
                maxAnchor.x = pct;
                fillRt.anchorMax = maxAnchor;
            }

            if (healthPercentText == null && healthBarFill.transform.parent != null)
            {
                healthPercentText = healthBarFill.transform.parent.GetComponentInChildren<Text>();
                if (healthPercentText == null)
                {
                    GameObject textGo = new GameObject("PercentText", typeof(RectTransform), typeof(Text));
                    textGo.transform.SetParent(healthBarFill.transform.parent, false);
                    healthPercentText = textGo.GetComponent<Text>();
                    healthPercentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    healthPercentText.fontSize = 10;
                    healthPercentText.alignment = TextAnchor.MiddleCenter;
                    healthPercentText.color = Color.white;
                    healthPercentText.raycastTarget = false;
                    RectTransform rt = healthPercentText.rectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                }
            }

            if (healthPercentText != null)
            {
                healthPercentText.text = Mathf.RoundToInt(pct * 100f) + "%";
            }
        }
    }
    
    private void Die()
    {
        isDead = true;
        StopAttack();
        currentHealth = 0;
        UpdateHealthBar();
        
        animator.SetBool("IsDead", true);
        
        // Disable collider so it doesn't block player or take more hits
        var colls = GetComponents<Collider2D>();
        foreach(var c in colls) c.enabled = false;
        
        // Hide health bar immediately
        if (healthBarFill != null && healthBarFill.transform.parent != null) {
            healthBarFill.transform.parent.gameObject.SetActive(false);
        }
        
        deathTimer = 0.5f;
    }
}
