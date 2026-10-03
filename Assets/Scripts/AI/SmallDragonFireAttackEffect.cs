using UnityEngine;

namespace TheLastKnight.AI
{
    /// <summary>
    /// Plays the Small Dragon's six fire frames independently from its short
    /// body Attack clip. The effect stays at its firing position until the
    /// sequence ends, even if the dragon starts moving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmallDragonFireAttackEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private Sprite[] _frames = new Sprite[0];
        [SerializeField, Min(0.01f)] private float _duration = 2f;
        [SerializeField, Min(1f)] private float _frameRate = 16f;

        private float _elapsed;
        private bool _playing;
        private Transform _ownerTransform;
        private Vector3 _ownerLocalPosition;
        private Quaternion _ownerLocalRotation;
        private Vector3 _ownerLocalScale;

        private void Awake()
        {
            _ownerTransform = transform.parent;
            _ownerLocalPosition = transform.localPosition;
            _ownerLocalRotation = transform.localRotation;
            _ownerLocalScale = transform.localScale;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
                _audioSource.loop = true;
            }
            Stop();
        }

        private void Update()
        {
            if (!_playing || _frames == null || _frames.Length == 0) return;

            _elapsed += Time.deltaTime;
            float sampledElapsed = Mathf.Floor(_elapsed * _frameRate) / _frameRate;
            float frameDuration = _duration / _frames.Length;
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(sampledElapsed / frameDuration), 0, _frames.Length - 1);
            if (_renderer != null)
            {
                _renderer.sprite = _frames[frameIndex];
                _renderer.enabled = true;
            }

            if (_elapsed >= _duration)
            {
                Stop();
            }
        }

        public void Play()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _frames == null || _frames.Length == 0) return;

            _elapsed = 0f;
            _playing = true;
            if (_ownerTransform != null && transform.parent == _ownerTransform)
            {
                // Preserve the mouth position and world scale, then let the
                // effect finish independently of the dragon's movement.
                transform.SetParent(null, true);
            }
            _renderer.sprite = _frames[0];
            _renderer.enabled = true;
            if (_audioSource != null && _audioSource.clip != null)
            {
                _audioSource.Stop();
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            if (_ownerTransform != null && transform.parent != _ownerTransform)
            {
                transform.SetParent(_ownerTransform, false);
                transform.localPosition = _ownerLocalPosition;
                transform.localRotation = _ownerLocalRotation;
                transform.localScale = _ownerLocalScale;
            }
        }
    }
}
