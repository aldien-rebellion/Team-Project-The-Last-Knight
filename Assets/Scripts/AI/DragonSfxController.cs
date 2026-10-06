using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class DragonSfxController : MonoBehaviour
    {
        [Header("Dragon sound effects")]
        [SerializeField] private AudioClip _biteClip;
        [SerializeField] private AudioClip _fireBreathClip;
        [SerializeField] private AudioClip _playerHitClip;

        [Header("Volume")]
        [SerializeField, Range(0f, 1f)] private float _biteVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _fireBreathVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _playerHitVolume = 0.9f;

        [SerializeField] private AudioSource _attackAudioSource;
        [SerializeField, Min(0f)] private float _attackStartOffset;
        [SerializeField, Min(0f)] private float _attackEndOffset = 3f;
        [SerializeField, Min(0f)] private float _attackFadeOutDuration = 0.3f;
        [SerializeField, Range(0.5f, 2f)] private float _attackPitch = 1f;
        private AudioSource _audioSource;
        private EnemyStats _stats;
        private float _attackBaseVolume;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
            if (_attackAudioSource != null)
            {
                _attackAudioSource.playOnAwake = false;
                _attackAudioSource.loop = false;
                _attackAudioSource.spatialBlend = 0.8f;
            }
            _stats = GetComponent<EnemyStats>();
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(_attackAudioSource);
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

        private void Update()
        {
            if (_attackAudioSource == null || !_attackAudioSource.isPlaying || _attackAudioSource.clip == null)
                return;

            var clip = _attackAudioSource.clip;
            float startOffset = Mathf.Clamp(_attackStartOffset, 0f, clip.length);
            float endOffset = Mathf.Clamp(_attackEndOffset, startOffset, clip.length);
            float currentOffset = (float)_attackAudioSource.timeSamples / clip.frequency;
            float fadeDuration = Mathf.Clamp(_attackFadeOutDuration, 0f, endOffset - startOffset);
            float fadeStartOffset = endOffset - fadeDuration;

            if (fadeDuration > 0f && currentOffset >= fadeStartOffset)
            {
                float fade = Mathf.Clamp01((endOffset - currentOffset) / fadeDuration);
                TheLastKnight.Audio.AudioManager.SetEffectsSourceVolume(_attackAudioSource, _attackBaseVolume * fade);
            }

            if (currentOffset >= endOffset)
            {
                _attackAudioSource.Stop();
                _attackAudioSource.clip = null;
            }
        }

        public void PlayBite()
        {
            PlayAttackClip(_biteClip, _biteVolume);
        }

        public void PlayFireBreath()
        {
            PlayAttackClip(_fireBreathClip, _fireBreathVolume);
        }

        private void HandleDamaged(DamageData damageData)
        {
            PlayOneShot(_playerHitClip, _playerHitVolume);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip, volume);
        }

        private void PlayAttackClip(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (_attackAudioSource == null)
            {
                PlayOneShot(clip, volume);
                return;
            }

            _attackAudioSource.Stop();
            _attackAudioSource.clip = clip;
            _attackBaseVolume = volume;
            TheLastKnight.Audio.AudioManager.SetEffectsSourceVolume(_attackAudioSource, _attackBaseVolume);
            _attackAudioSource.pitch = _attackPitch;
            _attackAudioSource.loop = false;
            float startOffset = Mathf.Clamp(_attackStartOffset, 0f, clip.length);
            _attackAudioSource.timeSamples = Mathf.Clamp(
                Mathf.RoundToInt(startOffset * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            _attackAudioSource.Play();
        }
    }
}
