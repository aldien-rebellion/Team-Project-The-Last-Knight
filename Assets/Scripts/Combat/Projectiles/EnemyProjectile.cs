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
        [SerializeField, Min(0f)] private float _maxTravelDistance;
        [SerializeField] private bool _destroyOnGround = true;
        [SerializeField] private Vector2 _knockback = new Vector2(3f, 2f);

        private Vector2 _direction = Vector2.right;
        private GameObject _attacker;
        private Rigidbody2D _rb;
        private Animator _animator;
        private Collider2D _collider;
        private bool _hasImpacted = false;
        private Vector2 _startPosition;
        private bool _initialized;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;

            Invoke(nameof(TriggerImpact), _lifetime);
        }

        public void Initialize(Vector2 direction, float damage, GameObject attacker = null)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                // Restart the moving animation when the projectile is reused.
                if (_animator.HasState(0, Animator.StringToHash("Projectile")))
                    _animator.Play("Projectile", 0, 0f);
                else if (_animator.HasState(0, Animator.StringToHash("Move")))
                    _animator.Play("Move", 0, 0f);
            }

            _direction = direction.normalized;
            _startPosition = transform.position;
            _initialized = true;
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

            // Rigidbody2D velocity also moves kinematic bodies. Do not move them twice.
            if (_rb == null)
            {
                transform.position += (Vector3)(_direction * _speed * Time.deltaTime);
            }

            if (_initialized && _maxTravelDistance > 0f
                && Vector2.Distance(_startPosition, transform.position) >= _maxTravelDistance)
            {
                transform.position = _startPosition + _direction * _maxTravelDistance;
                TriggerImpact();
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
            if (_hasImpacted) return;
            _hasImpacted = true;
            if (_collider != null) _collider.enabled = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            // Play impact / explosion animation if available
            if (_animator != null && _animator.HasState(0, Animator.StringToHash("Explosion")))
            {
                _animator.Play("Explosion", 0, 0f);
                float explosionDuration = 0.5f;
                foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && (clip.name == "Explosion"
                        || clip.name.EndsWith("_Explosion", System.StringComparison.Ordinal)))
                    {
                        explosionDuration = clip.length;
                        break;
                    }
                }
                Destroy(gameObject, explosionDuration);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
