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
        
        [Tooltip("Optional second hit window start time measured from spawn. Negative disables it.")]
        [SerializeField] private float _secondDamageDelay = -1f;
        [SerializeField] private float _secondDamageDuration = 0.18f;
[SerializeField] private float _damageDuration = 0.4f;
        [SerializeField] private float _totalLifetime = 1.3f;
        [SerializeField] private bool _alignSpriteBottomToSpawn;
        [SerializeField] private DamageType _damageType = DamageType.DarkMagic;

        private Collider2D _hitCollider;
        private SpriteRenderer _visualRenderer;
        private float _groundY;
        
        private bool _canDealDamage = false;
        private readonly HashSet<GameObject> _hitEntities = new HashSet<GameObject>();

        public void Initialize(float damage, GameObject owner = null)
        {
            _damage = damage;
        }

        private void Awake()
        {
            _hitCollider = GetComponent<Collider2D>();
            _visualRenderer = GetComponent<SpriteRenderer>();
            _groundY = transform.position.y;

            _hitCollider.isTrigger = true;
            _hitCollider.enabled = false;
            AlignSpriteBottomToGround();

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

            if (_secondDamageDelay < 0f)
                yield break;

            float waitUntilSecondHit = _secondDamageDelay - (_delayBeforeDamage + _damageDuration);
            if (waitUntilSecondHit > 0f)
                yield return new WaitForSeconds(waitUntilSecondHit);

            _hitEntities.Clear();
            _canDealDamage = true;
            _hitCollider.enabled = true;
            yield return new WaitForSeconds(_secondDamageDuration);
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
    

        private void LateUpdate()
        {
            AlignSpriteBottomToGround();
        }

        private void AlignSpriteBottomToGround()
        {
            if (!_alignSpriteBottomToSpawn || _visualRenderer == null || _visualRenderer.sprite == null)
                return;

            // Spell frames have different heights and centered pivots. Follow
            // each frame so the visible bottom stays at the original ground line.
            float deltaY = _groundY - _visualRenderer.bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.001f)
                return;

            transform.position += Vector3.up * deltaY;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
            _hitCollider.offset -= Vector2.up * (deltaY / scaleY);
        }
}
}
