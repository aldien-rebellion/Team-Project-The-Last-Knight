using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyHitbox2D : MonoBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private float _damage = 10f;
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [SerializeField] private Vector2 _knockback = new Vector2(4f, 2f);
        [SerializeField] private float _hitCooldown = 0.8f;

        [Header("Activation")]
        [SerializeField] private bool _activeOnlyDuringAttack = false;
        [SerializeField] private bool _isActive = true;

        private Collider2D _collider;
        private readonly Dictionary<GameObject, float> _lastHitTimes = new Dictionary<GameObject, float>();

        public float Damage { get => _damage; set => _damage = value; }
        public bool IsActive { get => _isActive; set => SetActive(value); }

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _collider.isTrigger = true;

            if (_activeOnlyDuringAttack)
            {
                SetActive(false);
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (_collider != null)
            {
                _collider.enabled = active;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDealDamage(other.gameObject);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealDamage(other.gameObject);
        }

        private void TryDealDamage(GameObject target)
        {
            if (!_isActive) return;
            var owner = GetComponentInParent<TheLastKnight.AI.EnemyController>();
            if (owner != null && !owner.CanDealMeleeDamage) return;

            // Check if target is Player
            bool isPlayer = target.CompareTag("Player") || target.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase);
            var playerStats = target.GetComponentInParent<PlayerStats>();
            if (!isPlayer && playerStats == null)
            {
                return;
            }

            GameObject victim = playerStats != null ? playerStats.gameObject : target;

            // Check hit cooldown for this specific target
            if (_lastHitTimes.TryGetValue(victim, out float lastTime))
            {
                if (Time.time - lastTime < _hitCooldown)
                {
                    return;
                }
            }

            // Calculate directional knockback
            Vector2 direction = (victim.transform.position - transform.position).normalized;
            Vector2 appliedKnockback = new Vector2(Mathf.Sign(direction.x) * _knockback.x, _knockback.y);

            DamageData damageData = new DamageData(_damage, transform.root.gameObject, _damageType, appliedKnockback, victim.transform.position);

            // Apply damage to Player
            if (playerStats != null)
            {
                playerStats.TakeDamage(_damage);
            }
            else
            {
                var damageable = victim.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damageData);
                }
                else
                {
                    victim.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
                }
            }

            _lastHitTimes[victim] = Time.time;
        }
    }
}
