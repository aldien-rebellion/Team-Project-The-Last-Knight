using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class DemonAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _basicAttackClip;
        [SerializeField] private AudioClip _hitClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
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

        public void PlayBasicAttack()
        {
            if (_basicAttackClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_basicAttackClip);
        }

        private void HandleDamaged(DamageData damageData)
        {
            if (_hitClip != null && _audioSource != null)
                _audioSource.PlayOneShot(_hitClip, 0.9f);
        }
    }
}
