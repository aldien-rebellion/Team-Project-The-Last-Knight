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
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 20f;
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
        public void PlayHowl() => PlayOneShot(_howlClip);
        public void PlayFireballLaunch() => PlayOneShot(_fireballLaunchClip, 0.55f);
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
