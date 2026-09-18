using System.Collections;
using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public class DragonAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _idleClip;
        [SerializeField] private AudioClip _footstepClip;
        [SerializeField] private AudioClip _attackClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _deathClip;
        [SerializeField] private AudioClip _deathFallClip;

        [Header("Settings")]
        [SerializeField] private float _idleSoundInterval = 8f;

        private AudioSource _audioSource;
        private EnemyStats _stats;
        private float _nextIdleTime = 0f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f; // 2D/3D balanced spatial sound
            _stats = GetComponent<EnemyStats>();
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.OnDamaged += HandleDamaged;
                _stats.OnDeath += HandleDeath;
            }

            _nextIdleTime = Time.time + Random.Range(3f, _idleSoundInterval);
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnDamaged -= HandleDamaged;
                _stats.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_stats != null && _stats.IsDead) return;

            if (Time.time >= _nextIdleTime)
            {
                PlayIdleSound();
                _nextIdleTime = Time.time + _idleSoundInterval + Random.Range(-2f, 2f);
            }
        }

        public void PlayFootstep()
        {
            PlayOneShot(_footstepClip, 0.7f);
        }

        public void PlayAttackSound()
        {
            PlayOneShot(_attackClip, 1.0f);
        }

        public void PlayIdleSound()
        {
            PlayOneShot(_idleClip, 0.6f);
        }

        public void PlayDeathFall()
        {
            PlayOneShot(_deathFallClip, 1.0f);
        }

        private void HandleDamaged(DamageData data)
        {
            PlayOneShot(_hitClip, 0.9f);
        }

        private void HandleDeath()
        {
            PlayOneShot(_deathClip, 1.0f);
            StartCoroutine(DelayedDeathFall(0.7f));
        }

        private IEnumerator DelayedDeathFall(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayDeathFall();
        }

        private void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }

        public void SetClips(AudioClip idle, AudioClip footstep, AudioClip attack, AudioClip hit, AudioClip death, AudioClip deathFall)
        {
            _idleClip = idle;
            _footstepClip = footstep;
            _attackClip = attack;
            _hitClip = hit;
            _deathClip = death;
            _deathFallClip = deathFall;
        }
    }
}
