using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class ReaperAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _swordSlashClip;
        [SerializeField] private AudioClip _weaponDrawClip;
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
            _audioSource.maxDistance = 24f;
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

        public void SetClips(AudioClip swordSlash, AudioClip weaponDraw, AudioClip playerHit)
        {
            _swordSlashClip = swordSlash;
            _weaponDrawClip = weaponDraw;
            _playerHitClip = playerHit;
        }

        public void PlaySwordSlash() => PlayOneShot(_swordSlashClip);

        public void PlayWeaponDraw() => PlayOneShot(_weaponDrawClip);

        private void HandleDamaged(DamageData damageData)
        {
            if (damageData.attacker == null
                || damageData.attacker.GetComponent<TheLastKnight.Player.PlayerController>() == null)
                return;

            PlayOneShot(_playerHitClip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
