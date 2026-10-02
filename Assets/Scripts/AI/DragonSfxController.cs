using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class DragonSfxController : MonoBehaviour
    {
        [Header("Dragon sound effects")]
        [SerializeField] private AudioClip _biteClip;
        [SerializeField] private AudioClip _fireBreathClip;
        [SerializeField] private AudioClip _playerHitClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _biteVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _fireBreathVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _playerHitVolume = 0.9f;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
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

        public void PlayBite()
        {
            PlayOneShot(_biteClip, _biteVolume);
        }

        public void PlayFireBreath()
        {
            PlayOneShot(_fireBreathClip, _fireBreathVolume);
        }

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_playerHitClip, _playerHitVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
