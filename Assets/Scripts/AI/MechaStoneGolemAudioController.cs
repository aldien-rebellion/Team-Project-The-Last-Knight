using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MechaStoneGolemAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _meleeAttackClip;
        [SerializeField] private AudioClip _rocketPunchClip;
        [SerializeField] private AudioClip _laserOrbClip;
        [SerializeField] private AudioClip _shieldCastClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _stunClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
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

        public void SetClips(AudioClip meleeAttack, AudioClip rocketPunch, AudioClip laserOrb,
            AudioClip shieldCast, AudioClip hit, AudioClip stun)
        {
            _meleeAttackClip = meleeAttack;
            _rocketPunchClip = rocketPunch;
            _laserOrbClip = laserOrb;
            _shieldCastClip = shieldCast;
            _hitClip = hit;
            _stunClip = stun;
        }

        public void PlayMeleeAttack() => PlayOneShot(_meleeAttackClip);
        public void PlayRocketPunch() => PlayOneShot(_rocketPunchClip);
        public void PlayLaserOrb() => PlayOneShot(_laserOrbClip);
        public void PlayShieldCast() => PlayOneShot(_shieldCastClip);
        public void PlayStunned() => PlayOneShot(_stunClip);

        private void HandleDamaged(DamageData damageData)
        {
            AudioClip clip = _stats != null && _stats.CurrentStatus == StatusEffect.Stunned
                ? _stunClip
                : _hitClip;
            PlayOneShot(clip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
