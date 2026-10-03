using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    public sealed class VolcanoxAudioController : MonoBehaviour
    {
        [Header("Skill sounds")]
        [SerializeField] private AudioClip _fireballReleaseClip;
        [SerializeField] private AudioClip _windHitClip;
        [SerializeField] private AudioClip _infernalSlamPunchClip;
        [SerializeField] private AudioClip _rockBreakClip;

        [Header("Damage sound")]
        [SerializeField] private AudioClip _playerAttackHitClip;

        private AudioSource _skillAudioSource;
        private AudioSource _magmaAudioSource;
        private AudioSource _hitAudioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            var sources = GetComponents<AudioSource>();
            _skillAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            _hitAudioSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            _magmaAudioSource = sources.Length > 2 ? sources[2] : gameObject.AddComponent<AudioSource>();
            Configure(_skillAudioSource);
            Configure(_hitAudioSource);
            Configure(_magmaAudioSource);
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
            StopSkillSound();
            if (_magmaAudioSource != null) _magmaAudioSource.Stop();
        }

        public void PlayFireballReleaseSound() => PlaySkillSound(_fireballReleaseClip);
        public void PlayWindHitSound() => PlaySkillSound(_windHitClip);
        public void PlayPunchSound() => PlaySkillSound(_infernalSlamPunchClip);
        public void PlayMagmaPunchSound() => PlayMagmaOneShot(_infernalSlamPunchClip);
        public void PlayMagmaRockBreakSound() => PlayMagmaOneShot(_rockBreakClip);

        public void StopSkillSound()
        {
            if (_skillAudioSource == null) return;
            _skillAudioSource.Stop();
            _skillAudioSource.clip = null;
        }

        private void HandleDamaged(DamageData damageData)
        {
            if (_playerAttackHitClip == null || _hitAudioSource == null || damageData.attacker == null)
                return;

            if (damageData.attacker.GetComponentInParent<TheLastKnight.Player.PlayerController>() != null)
                _hitAudioSource.PlayOneShot(_playerAttackHitClip);
        }

        private void PlaySkillSound(AudioClip clip)
        {
            if (clip == null || _skillAudioSource == null) return;

            _skillAudioSource.Stop();
            _skillAudioSource.clip = clip;
            _skillAudioSource.loop = false;
            _skillAudioSource.Play();
        }

        private void PlayMagmaOneShot(AudioClip clip)
        {
            if (clip != null && _magmaAudioSource != null)
                _magmaAudioSource.PlayOneShot(clip);
        }

        private static void Configure(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0.8f;
            source.minDistance = 2f;
            source.maxDistance = 24f;
        }
    }
}
