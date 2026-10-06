using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FlyingEyeAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _biteClip;
        [SerializeField] private AudioClip _eyeBeamClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField, Range(0f, 1f)] private float _biteVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _eyeBeamVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.9f;

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
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(_audioSource);
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

        public void SetClips(AudioClip bite, AudioClip eyeBeam, AudioClip hit)
        {
            _biteClip = bite;
            _eyeBeamClip = eyeBeam;
            _hitClip = hit;
        }

        public void PlayBite() => PlayOneShot(_biteClip, _biteVolume);

        public void PlayEyeBeam() => PlayOneShot(_eyeBeamClip, _eyeBeamVolume);

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip, _hitVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
