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
        [SerializeField] private AudioClip _impactAudioClip;
        [SerializeField, Range(0f, 1f)] private float _impactAudioVolume = 1f;

        private Collider2D _hitCollider;
        private SpriteRenderer _visualRenderer;
        private float _groundY;
        private float _spawnTime;
        private bool _impactAudioPlayed;
        
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
            _spawnTime = Time.time;

            _hitCollider.isTrigger = true;
            _hitCollider.enabled = false;
            AlignSpriteBottomToGround();

            StartCoroutine(SpellRoutine());
            if (_impactAudioClip == null)
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

            if (_secondDamageDelay >= 0f)
            {
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

            if (_impactAudioClip != null)
            {
                PlayImpactAudio();
                float lifetimeRemaining = Mathf.Max(0f, _totalLifetime - (Time.time - _spawnTime));
                Destroy(gameObject, Mathf.Max(lifetimeRemaining, _impactAudioClip.length));
            }
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
                PlayImpactAudio();

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

        private void PlayImpactAudio()
        {
            if (_impactAudioPlayed || _impactAudioClip == null) return;

            _impactAudioPlayed = true;
            var audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(audioSource);
            audioSource.PlayOneShot(_impactAudioClip, _impactAudioVolume);
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
