using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class BlueSlimeAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _walkClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField, Min(0.05f)] private float _footstepInterval = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.9f;

        private AudioSource _audioSource;
        private EnemyStats _stats;
        private EnemyController _ai;
        private Rigidbody2D _body;
        private float _nextFootstepTime;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            _stats = GetComponent<EnemyStats>();
            _ai = GetComponent<EnemyController>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            if (_stats != null)
                _stats.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_stats != null)
                _stats.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            if (_stats == null || _stats.IsDead || _ai == null || _body == null)
                return;

            bool movingState = _ai.CurrentState == EnemyAIState.Patrol
                || _ai.CurrentState == EnemyAIState.Chase
                || _ai.CurrentState == EnemyAIState.ReturningToSpawn;
            bool moving = movingState && Mathf.Abs(_body.linearVelocity.x) > 0.05f;

            if (!moving)
            {
                _nextFootstepTime = Time.time;
                return;
            }

            if (Time.time >= _nextFootstepTime)
            {
                if (_walkClip != null)
                    _audioSource.PlayOneShot(_walkClip, _walkVolume);
                _nextFootstepTime = Time.time + _footstepInterval;
            }
        }

        private void HandleDamaged(DamageData damageData)
        {
            if (_hitClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_hitClip, _hitVolume);
        }
    }
}
