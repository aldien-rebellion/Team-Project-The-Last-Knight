using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkeletonAudioController : MonoBehaviour
    {
        [Header("Skeleton sound effects")]
        [SerializeField] private AudioClip _normalAttackClip;
        [SerializeField] private AudioClip _shieldHitClip;
        [SerializeField] private AudioClip _swordThrowClip;
        [SerializeField] private AudioClip _hitClip;

        private AudioSource _audioSource;
        private EnemyStats _stats;
        private Animator _animator;

        private void Awake()
        {
            EnsureReferences();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f;
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 20f;
            TheLastKnight.Audio.AudioManager.RegisterEffectsSource(_audioSource);
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (_stats != null)
                _stats.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_stats != null)
                _stats.OnDamaged -= HandleDamaged;
        }

        public void PlayNormalAttack() => PlayOneShot(_normalAttackClip);

        public void PlaySwordThrow() => PlayOneShot(_swordThrowClip);

        private void HandleDamaged(DamageData damageData)
        {
            if (_stats != null && _stats.IsDead)
                return;

            PlayOneShot(IsShieldGuardActive() ? _shieldHitClip : _hitClip);
        }

        private bool IsShieldGuardActive()
        {
            if (_animator == null)
                return false;

            var current = _animator.GetCurrentAnimatorStateInfo(0);
            if (current.IsName("Shield") || current.IsName("Base Layer.Shield"))
                return true;

            if (!_animator.IsInTransition(0))
                return false;

            var next = _animator.GetNextAnimatorStateInfo(0);
            return next.IsName("Shield") || next.IsName("Base Layer.Shield");
        }

        private void EnsureReferences()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();
            if (_stats == null)
                _stats = GetComponent<EnemyStats>();
            if (_animator == null)
                _animator = GetComponent<Animator>();
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }
}