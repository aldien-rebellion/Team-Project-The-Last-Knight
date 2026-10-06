using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkullwolfAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _biteClip;
        [SerializeField] private AudioClip _runClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField, Range(0f, 1f)] private float _biteVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _chargeVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _runVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.9f;

        private AudioSource _oneShotSource;
        private AudioSource _movementSource;
        private EnemyStats _stats;
        private EnemyController _enemyController;
        private Rigidbody2D _body;
        private bool _chargeSoundReady = true;

        private void Awake()
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length > 0) _oneShotSource = sources[0];
            if (sources.Length > 1) _movementSource = sources[1];

            ConfigureSource(_oneShotSource);
            ConfigureSource(_movementSource);
            if (_movementSource != null)
            {
                _movementSource.clip = _runClip;
                _movementSource.loop = true;
                _movementSource.volume = _runVolume;
            }

            _stats = GetComponent<EnemyStats>();
            _enemyController = GetComponent<EnemyController>();
            _body = GetComponent<Rigidbody2D>();
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(_oneShotSource);
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(_movementSource);
        }

        private void OnEnable()
        {
            if (_stats == null) _stats = GetComponent<EnemyStats>();
            if (_stats != null) _stats.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnDamaged -= HandleDamaged;
            StopMovementSound();
        }

        private void Update()
        {
            if (_stats == null || _stats.IsDead || _enemyController == null)
            {
                StopMovementSound();
                return;
            }

            EnemyAIState state = _enemyController.CurrentState;
            if (state == EnemyAIState.Chase && _chargeSoundReady)
            {
                PlayCharge();
                _chargeSoundReady = false;
            }
            else if (state == EnemyAIState.Idle || state == EnemyAIState.Patrol
                || state == EnemyAIState.ReturningToSpawn)
            {
                _chargeSoundReady = true;
            }

            bool isMoving = (state == EnemyAIState.Patrol || state == EnemyAIState.Chase
                || state == EnemyAIState.ReturningToSpawn)
                && _body != null && Mathf.Abs(_body.linearVelocity.x) > 0.05f
                && _stats.CurrentStatus != StatusEffect.Stunned;

            if (isMoving && _runClip != null && _movementSource != null)
            {
                if (!_movementSource.isPlaying)
                    _movementSource.Play();
            }
            else
            {
                StopMovementSound();
            }
        }

        public void SetClips(AudioClip bite, AudioClip run, AudioClip hit)
        {
            _biteClip = bite;
            _runClip = run;
            _hitClip = hit;
        }

        public void PlayBite() => PlayOneShot(_biteClip, _biteVolume);

        private void PlayCharge() => PlayOneShot(_biteClip, _chargeVolume);

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip, _hitVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && _oneShotSource != null)
                _oneShotSource.PlayOneShot(clip, volume);
        }

        private void StopMovementSound()
        {
            if (_movementSource != null && _movementSource.isPlaying)
                _movementSource.Stop();
        }

        private static void ConfigureSource(AudioSource source)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = 0.8f;
            source.minDistance = 2f;
            source.maxDistance = 20f;
        }
    }
}
