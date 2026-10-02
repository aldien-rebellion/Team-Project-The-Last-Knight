using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MoonstoneKeeperAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _swordSwingClip;
        [SerializeField] private AudioClip _hitClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 24f;
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

        public void SetClips(AudioClip swordSwing, AudioClip hit)
        {
            _swordSwingClip = swordSwing;
            _hitClip = hit;
        }

        public void PlaySwordSwing()
        {
            PlayOneShot(_swordSwingClip);
        }

        private void HandleDamaged(DamageData damageData)
        {
            if (damageData.attacker == null
                || damageData.attacker.GetComponent<TheLastKnight.Player.PlayerController>() == null)
                return;

            PlayOneShot(_hitClip, 0.9f);
        }

        private void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }
    }
}
