using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SatyrAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _basicAttackClip;
        [SerializeField] private AudioClip _dropkickClip;
        [SerializeField] private AudioClip _slash2Clip;
        [SerializeField] private AudioClip _natureCastClip;
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

        public void SetClips(AudioClip basicAttack, AudioClip dropkick, AudioClip slash2,
            AudioClip natureCast, AudioClip hit)
        {
            _basicAttackClip = basicAttack;
            _dropkickClip = dropkick;
            _slash2Clip = slash2;
            _natureCastClip = natureCast;
            _hitClip = hit;
        }

        public void PlayBasicAttack() => PlayOneShot(_basicAttackClip);
        public void PlayDropkickSound() => PlayOneShot(_dropkickClip);

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_hitClip);
        }

        public void PlaySkill(string skillName)
        {
            switch (skillName)
            {
                case "Slash2":
                    PlayOneShot(_slash2Clip);
                    break;
                case "NatureCast":
                    PlayOneShot(_natureCastClip);
                    break;
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}
