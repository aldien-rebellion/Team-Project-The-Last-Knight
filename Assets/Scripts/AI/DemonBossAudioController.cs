using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class DemonBossAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _attackClip;
        [SerializeField] private AudioClip _roarClip;
        [SerializeField] private AudioClip _footstepClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private float _footstepInterval = 0.55f;

        private AudioSource _audioSource;
        private EnemyStats _stats;
        private EnemyController _ai;
        private Rigidbody2D _body;
        private float _nextFootstepTime;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;

            _stats = GetComponent<EnemyStats>();
            _ai = GetComponent<EnemyController>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void LateUpdate()
        {
            if (_ai == null || _body == null || (_stats != null && _stats.IsDead))
            {
                _nextFootstepTime = 0f;
                return;
            }

            EnemyAIState state = _ai.CurrentState;
            bool movingState = state == EnemyAIState.Patrol || state == EnemyAIState.Chase
                || state == EnemyAIState.ReturningToSpawn;
            if (!movingState || Mathf.Abs(_body.linearVelocity.x) <= 0.05f)
            {
                _nextFootstepTime = 0f;
                return;
            }

            if (Time.time >= _nextFootstepTime)
            {
                PlayFootstep();
                _nextFootstepTime = Time.time + _footstepInterval;
            }
        }

        private void OnEnable()
        {
            if (_stats == null) _stats = GetComponent<EnemyStats>();
            if (_stats != null) _stats.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnDamaged -= HandleDamaged;
        }

        public void PlayAttackSound()
        {
            PlayOneShot(_attackClip, 1f);
        }

        public void PlayRoarSound()
        {
            PlayOneShot(_roarClip, 1f);
        }

        public void PlayFootstep()
        {
            PlayOneShot(_footstepClip, 5f);
        }

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip, 0.9f);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
