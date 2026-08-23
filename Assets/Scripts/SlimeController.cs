using UnityEngine;
using UnityEngine.UI;

public class SlimeController : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    
    [Header("UI")]
    public Image healthBarFill;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 3.5f;
    public float patrolDistance = 5f;
    
    [Header("Combat")]
    public float detectionRange = 6f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1.5f;
    public float damage = 10f;
    
    private bool movingRight = true;
    private float startX;
    private float nextAttackTime = 0f;
    
    private GameObject player;
    private Animator animator;
    private bool isDead = false;
    private float deathTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        startX = transform.position.x;
        animator = GetComponent<Animator>();
        player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
        
        UpdateHealthBar();
    }

    void Update()
    {
        if (isDead) 
        {
            // Optional: Make the body blink or fade out here if needed
            deathTimer -= Time.deltaTime;
            if (deathTimer <= 0)
            {
                Destroy(gameObject);
            }
            return;
        }
        
        if (player == null) return;
        
        // Wait for attack or hurt animation to finish basically
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Attack") || stateInfo.IsName("Hurt")) {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.transform.position);

        if (distToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else if (distToPlayer <= detectionRange)
        {
            ChasePlayer();
        }
        else
        {
            Patrol();
        }
    }

    void Patrol()
    {
        animator.SetBool("IsChasing", false);
        animator.SetBool("IsMoving", true);
        
        float currentX = transform.position.x;
        float speed = patrolSpeed * Time.deltaTime;

        if (movingRight)
        {
            transform.Translate(Vector2.right * speed);
            if (transform.localScale.x < 0) Flip(); // Face right
            if (currentX > startX + patrolDistance)
            {
                movingRight = false;
                Flip();
            }
        }
        else
        {
            transform.Translate(Vector2.left * speed);
            if (transform.localScale.x > 0) Flip(); // Face left
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
        
        if (dir > 0)
        {
            transform.Translate(Vector2.right * chaseSpeed * Time.deltaTime);
            if (transform.localScale.x < 0) Flip();
            movingRight = true;
        }
        else
        {
            transform.Translate(Vector2.left * chaseSpeed * Time.deltaTime);
            if (transform.localScale.x > 0) Flip();
            movingRight = false;
        }
        
        startX = transform.position.x; 
    }

    void AttackPlayer()
    {
        if (Time.time >= nextAttackTime)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
            animator.SetTrigger("Attack");
            
            var stats = player.GetComponent("PlayerStats");
            if (stats != null)
            {
                stats.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
            else 
            {
                player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
            
            nextAttackTime = Time.time + attackCooldown;
        }
        else 
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsChasing", false);
        }
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
        
        currentHealth -= amount;
        
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
            healthBarFill.fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        }
    }
    
    private void Die()
    {
        isDead = true;
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
        
        // Set timer to destroy the object after the death animation (0.5 seconds)
        deathTimer = 0.5f;
    }
}
