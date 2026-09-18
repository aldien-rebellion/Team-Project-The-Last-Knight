using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Combat.Projectiles
{
    [RequireComponent(typeof(Collider2D))]
    public class GroundSpellArea : MonoBehaviour
    {
        [Header("Spell Timing & Damage")]
        [SerializeField] private float _damage = 25f;
        [SerializeField] private float _delayBeforeDamage = 0.4f;
        [SerializeField] private float _damageDuration = 0.4f;
        [SerializeField] private float _totalLifetime = 1.3f;
        [SerializeField] private DamageType _damageType = DamageType.DarkMagic;

        private Collider2D _hitCollider;
        private bool _canDealDamage = false;
        private readonly HashSet<GameObject> _hitEntities = new HashSet<GameObject>();

        private void Awake()
        {
            _hitCollider = GetComponent<Collider2D>();
            _hitCollider.isTrigger = true;
            _hitCollider.enabled = false;

            StartCoroutine(SpellRoutine());
            Destroy(gameObject, _totalLifetime);
        }

        private IEnumerator SpellRoutine()
        {
            yield return new WaitForSeconds(_delayBeforeDamage);
            _canDealDamage = true;
            _hitCollider.enabled = true;

            yield return new WaitForSeconds(_damageDuration);
            _canDealDamage = false;
            _hitCollider.enabled = false;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!_canDealDamage) return;

            bool isPlayer = other.CompareTag("Player") || other.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase);
            var playerStats = other.GetComponentInParent<PlayerStats>();

            if (isPlayer || playerStats != null)
            {
                GameObject victim = playerStats != null ? playerStats.gameObject : other.gameObject;
                if (_hitEntities.Contains(victim)) return;

                _hitEntities.Add(victim);

                if (playerStats != null)
                {
                    playerStats.TakeDamage(_damage);
                }
                else
                {
                    var damageable = victim.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(new DamageData(_damage, gameObject, _damageType, new Vector2(0f, 4f), transform.position));
                    }
                    else
                    {
                        victim.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }
    }
}
