using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class JinnAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _closeAttackClip;
        [SerializeField] private AudioClip _windAttackClip;
        [SerializeField] private AudioClip _hitClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
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

        public void SetClips(AudioClip closeAttack, AudioClip windAttack, AudioClip hit)
        {
            _closeAttackClip = closeAttack;
            _windAttackClip = windAttack;
            _hitClip = hit;
        }

        public void PlayCloseAttack() => PlayOneShot(_closeAttackClip);

        public void PlayWindAttack() => PlayOneShot(_windAttackClip);

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
