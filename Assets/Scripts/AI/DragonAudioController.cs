using System.Collections;
using UnityEngine;
using TheLastKnight.Combat;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(AudioSource))]
    public class DragonAudioController : MonoBehaviour
    {
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _idleClip;
        [SerializeField] private AudioClip _footstepClip;
        [SerializeField] private AudioClip _attackClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private AudioClip _playerAttackHitClip;
        [SerializeField] private AudioClip _deathClip;
        [SerializeField] private AudioClip _deathFallClip;

        [Header("Settings")]
        [SerializeField] private float _idleSoundInterval = 8f;

        private AudioSource _audioSource;
        private EnemyStats _stats;
        private EnemyController _ai;
        private Animator _animator;
        private Rigidbody2D _body;
        private string _playedState;
        private float _nextFootstepTime;
        private bool _attackAnimationActive;
        private float _nextIdleTime = 0f;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.8f; // 2D/3D balanced spatial sound
            _stats = GetComponent<EnemyStats>();
            _ai = GetComponent<EnemyController>();
            _animator = GetComponent<Animator>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.OnDamaged += HandleDamaged;
                _stats.OnDeath += HandleDeath;
            }

            _nextIdleTime = Time.time + Random.Range(3f, _idleSoundInterval);
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.OnDamaged -= HandleDamaged;
                _stats.OnDeath -= HandleDeath;
            }
        }

        private void Update()
        {
            if (_stats != null && _stats.IsDead) return;

            if (_ai != null && _ai.CurrentState == EnemyAIState.Idle && Time.time >= _nextIdleTime)
            {
                PlayIdleSound();
                _nextIdleTime = Time.time + _idleSoundInterval + Random.Range(-2f, 2f);
            }
        }

        private void LateUpdate()
        {
            if (_ai == null || _animator == null) return;

            // This asset has separate left and right sheets. Mirroring the whole
            // transform would reverse the selected sheet and make it walk backwards.
            var scale = transform.localScale;
            if (scale.x < 0f)
            {
                scale.x = -scale.x;
                transform.localScale = scale;
            }

            var state = _ai.CurrentState;
            bool moving = state == EnemyAIState.Patrol || state == EnemyAIState.Chase || state == EnemyAIState.ReturningToSpawn;
            bool faceRight = moving && _body != null && Mathf.Abs(_body.linearVelocity.x) > 0.05f
                ? _body.linearVelocity.x > 0f : _ai.IsFacingRight;
            string side = faceRight ? "Right" : "Left";
            string desired = null;
            if (state == EnemyAIState.Dead) desired = "Death_" + side;
            else if (state == EnemyAIState.Hurt) desired = "Hit_" + side;
            else if (state == EnemyAIState.Patrol || state == EnemyAIState.Chase || state == EnemyAIState.ReturningToSpawn)
                desired = _body != null && Mathf.Abs(_body.linearVelocity.x) > 0.05f ? "Walk_" + side : "Idle_" + side;
            else if (state == EnemyAIState.Idle) desired = "Idle_" + side;
            else if (state == EnemyAIState.MeleeAttack)
            {
                desired = _attackAnimationActive ? "Attack_" + side : "Idle_" + side;
            }

            bool wrongAnimatorState = desired != null && !_animator.GetCurrentAnimatorStateInfo(0).IsName(desired);
            if (desired != null && (desired != _playedState || wrongAnimatorState)
                && _animator.HasState(0, Animator.StringToHash(desired)))
            {
                _animator.Play(desired, 0, 0f);
                _playedState = desired;
            }

            if (state == EnemyAIState.Chase && desired != null && desired.StartsWith("Walk_")
                && Time.time >= _nextFootstepTime)
            {
                PlayFootstep();
                _nextFootstepTime = Time.time + 0.45f;
            }
        }

        public void PlayFootstep()
        {
            PlayOneShot(_footstepClip, 0.7f);
        }

        public void PlayAttackSound()
        {
            PlayOneShot(_attackClip, 1.0f);
        }

        public void PrepareAttackAnimation()
        {
            _attackAnimationActive = false;
        }

        public void PlayAttackAnimation()
        {
            if (_animator == null || _ai == null) return;
            _attackAnimationActive = true;
            string state = _ai.IsFacingRight ? "Attack_Right" : "Attack_Left";
            _animator.Play(state, 0, 0f);
            _playedState = state;
        }

        public void PlayIdleSound()
        {
            PlayOneShot(_idleClip, 0.6f);
        }

        public void PlayDeathFall()
        {
            PlayOneShot(_deathFallClip, 1.0f);
        }

        private void HandleDamaged(DamageData data)
        {
            PlayOneShot(_hitClip, 0.9f);
            PlayOneShot(_playerAttackHitClip, 0.9f);
        }

        private void HandleDeath()
        {
            PlayOneShot(_deathClip, 1.0f);
            StartCoroutine(DelayedDeathFall(0.7f));
        }

        private IEnumerator DelayedDeathFall(float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayDeathFall();
        }

        private void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip, volume);
            }
        }

        public void SetClips(AudioClip idle, AudioClip footstep, AudioClip attack, AudioClip hit,
            AudioClip playerAttackHit, AudioClip death, AudioClip deathFall)
        {
            _idleClip = idle;
            _footstepClip = footstep;
            _attackClip = attack;
            _hitClip = hit;
            _playerAttackHitClip = playerAttackHit;
            _deathClip = death;
            _deathFallClip = deathFall;
        }
    }
}
