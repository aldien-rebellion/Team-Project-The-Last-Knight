using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkeletonKnightAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip _sideSwingClip;
        [SerializeField] private AudioClip _forwardSwingClip;
        [SerializeField] private AudioClip _downSwingClip;
        [SerializeField] private AudioClip _fullComboClip;
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

        public void SetClips(AudioClip sideSwing, AudioClip forwardSwing, AudioClip downSwing,
            AudioClip fullCombo, AudioClip hit)
        {
            _sideSwingClip = sideSwing;
            _forwardSwingClip = forwardSwing;
            _downSwingClip = downSwing;
            _fullComboClip = fullCombo;
            _hitClip = hit;
        }

        public void PlayBasicAttack(string animationState)
        {
            AudioClip clip = animationState switch
            {
                "SideSwing" => _sideSwingClip,
                "FwdSwing" => _forwardSwingClip,
                "DownSwing" => _downSwingClip,
                _ => null
            };
            PlayOneShot(clip);
        }

        public void PlayFullCombo() => PlayOneShot(_fullComboClip);

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
