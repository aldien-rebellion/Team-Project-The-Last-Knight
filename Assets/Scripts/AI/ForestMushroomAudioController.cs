using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ForestMushroomAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _attackClip;
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

        public void SetClips(AudioClip attack, AudioClip hit)
        {
            _attackClip = attack;
            _hitClip = hit;
        }

        public void PlayBasicAttack() => PlayOneShot(_attackClip);

        public void PlayAttackWithStun() => PlayOneShot(_attackClip);

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
