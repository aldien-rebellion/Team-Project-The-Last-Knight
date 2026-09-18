using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Combat.Projectiles
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        [Header("Projectile Properties")]
        [SerializeField] private float _speed = 8f;
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private bool _destroyOnGround = true;
        [SerializeField] private Vector2 _knockback = new Vector2(3f, 2f);

        private Vector2 _direction = Vector2.right;
        private GameObject _attacker;
        private Rigidbody2D _rb;
        private Animator _animator;
        private Collider2D _collider;
        private bool _hasImpacted = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;

            Destroy(gameObject, _lifetime);
        }

        public void Initialize(Vector2 direction, float damage, GameObject attacker = null)
        {
            _direction = direction.normalized;
            if (damage > 0) _damage = damage;
            _attacker = attacker;

            if (_rb != null)
            {
                _rb.linearVelocity = _direction * _speed;
            }

            // Rotate towards direction if moving diagonally or vertically
            if (Mathf.Abs(_direction.y) > 0.05f)
            {
                float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }
        }

        private void Update()
        {
            if (_hasImpacted) return;

            // Move if Rigidbody2D is not handling it
            if (_rb == null || _rb.bodyType == RigidbodyType2D.Kinematic)
            {
                transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasImpacted) return;

            // Ignore shooter
            if (_attacker != null && (other.gameObject == _attacker || other.transform.IsChildOf(_attacker.transform)))
            {
                return;
            }

            // Ignore other enemy triggers / enemies
            if (other.isTrigger && !other.CompareTag("Player"))
            {
                return;
            }

            // Check if hit Player
            bool isPlayer = other.CompareTag("Player") || other.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase);
            var playerStats = other.GetComponentInParent<PlayerStats>();

            if (isPlayer || playerStats != null)
            {
                GameObject victim = playerStats != null ? playerStats.gameObject : other.gameObject;
                Vector2 appliedKnockback = new Vector2(Mathf.Sign(_direction.x) * _knockback.x, _knockback.y);

                if (playerStats != null)
                {
                    playerStats.TakeDamage(_damage);
                }
                else
                {
                    var damageable = victim.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(new DamageData(_damage, _attacker, DamageType.Physical, appliedKnockback, transform.position));
                    }
                    else
                    {
                        victim.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
                    }
                }

                TriggerImpact();
                return;
            }

            // Check if hit solid wall or ground
            if (_destroyOnGround && !other.isTrigger)
            {
                TriggerImpact();
            }
        }

        private void TriggerImpact()
        {
            _hasImpacted = true;
            if (_collider != null) _collider.enabled = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            // Play impact / explosion animation if available
            if (_animator != null && _animator.HasState(0, Animator.StringToHash("Explosion")))
            {
                _animator.Play("Explosion");
                Destroy(gameObject, 0.5f);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
