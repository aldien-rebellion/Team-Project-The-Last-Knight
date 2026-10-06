using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    public sealed class HoodedProtagonistAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _basicAttackClip;
        [SerializeField] private AudioClip _playerHitClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
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

        public void PlayBasicAttack() => PlayOneShot(_basicAttackClip);

        private void HandleDamaged(DamageData damageData)
        {
            bool hitByPlayer = damageData.attacker != null
                && damageData.attacker.GetComponentInParent<TheLastKnight.Player.PlayerController>() != null;
            if (hitByPlayer) PlayOneShot(_playerHitClip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
