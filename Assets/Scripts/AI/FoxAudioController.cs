using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FoxAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _biteClip;
        [SerializeField] private AudioClip _howlClip;
        [SerializeField] private AudioClip _fireballLaunchClip;
        [SerializeField] private AudioClip _fireballImpactClip;
        [SerializeField] private AudioClip _vanishClip;
        [SerializeField] private AudioClip _hitClip;

        private AudioSource _audioSource;
        private AudioSource _howlSource;
        private float _nextFireballLaunchSoundTime;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 20f;

            // Keep the howl audible over the rain and independent of the 2D camera's Z offset.
            _howlSource = gameObject.AddComponent<AudioSource>();
            _howlSource.playOnAwake = false;
            _howlSource.spatialBlend = 0f;
            _howlSource.priority = 32;
            _howlSource.outputAudioMixerGroup = _audioSource.outputAudioMixerGroup;
            _howlSource.volume = _audioSource.volume;
            _stats = GetComponent<EnemyStats>();
        }

        private void OnEnable()
        {
            if (_stats == null) _stats = GetComponent<EnemyStats>();
            if (_stats != null) _stats.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.OnDamaged -= HandleDamaged;
            if (_howlSource != null) _howlSource.Stop();
        }

        public void SetClips(AudioClip bite, AudioClip howl, AudioClip fireballLaunch,
            AudioClip fireballImpact, AudioClip vanish, AudioClip hit)
        {
            _biteClip = bite;
            _howlClip = howl;
            _fireballLaunchClip = fireballLaunch;
            _fireballImpactClip = fireballImpact;
            _vanishClip = vanish;
            _hitClip = hit;
        }

        public void PlayBite() => PlayOneShot(_biteClip);
        public void PlayHowl()
        {
            if (_howlClip == null || _howlSource == null) return;
            _howlSource.clip = _howlClip;
            _howlSource.Play();
        }

        public void PlayFireballLaunch()
        {
            // Each launch clip lasts about three seconds; playing every drop exhausts real voices.
            if (Time.time < _nextFireballLaunchSoundTime) return;
            _nextFireballLaunchSoundTime = Time.time + 0.3f;
            PlayOneShot(_fireballLaunchClip, 0.55f);
        }
        public void PlayFireballImpact() => PlayOneShot(_fireballImpactClip, 0.7f);
        public void PlayVanish() => PlayOneShot(_vanishClip);

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip);
        }

        private void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
