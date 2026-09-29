using UnityEngine;

namespace TheLastKnight.AI
{
    /// <summary>
    /// Plays the Small Dragon's six fire frames independently from its short
    /// body Attack clip. The fire sequence lasts two seconds while sampling
    /// at the configured 16 FPS update rate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmallDragonFireAttackEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite[] _frames = new Sprite[0];
        [SerializeField, Min(0.01f)] private float _duration = 2f;
        [SerializeField, Min(1f)] private float _frameRate = 16f;

        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
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
            _renderer.sprite = _frames[0];
            _renderer.enabled = true;
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }
    }
}
