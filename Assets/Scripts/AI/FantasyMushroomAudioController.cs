using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class FantasyMushroomAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _basicAttackClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _sporeShotClip;

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

        public void SetClips(AudioClip basicAttack, AudioClip hit, AudioClip sporeShot)
        {
            _basicAttackClip = basicAttack;
            _hitClip = hit;
            _sporeShotClip = sporeShot;
        }

        public void PlayBasicAttack() => PlayOneShot(_basicAttackClip);

        public void PlaySporeShot() => PlayOneShot(_sporeShotClip);

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
