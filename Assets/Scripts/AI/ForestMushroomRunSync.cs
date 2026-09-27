using UnityEngine;

namespace TheLastKnight.AI
{
    [RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
    public sealed class ForestMushroomRunSync : MonoBehaviour
    {
        // The run sheet completes a footfall every four frames at 10 fps.
        // At this travel speed the foot moves roughly one stride per footfall.
        private const float UnitsPerSecondAtNormalSpeed = 1.25f;

        private Animator _animator;
        private Rigidbody2D _body;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void LateUpdate()
        {
            bool running = _animator.GetCurrentAnimatorStateInfo(0).IsName("Run")
                && !_animator.IsInTransition(0);
            _animator.speed = running && Mathf.Abs(_body.linearVelocity.x) > 0.05f
                ? Mathf.Clamp(Mathf.Abs(_body.linearVelocity.x) / UnitsPerSecondAtNormalSpeed, 0.8f, 3f)
                : 1f;
        }

        private void OnDisable()
        {
            if (_animator != null) _animator.speed = 1f;
        }
    }
}
