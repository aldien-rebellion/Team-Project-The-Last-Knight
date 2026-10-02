using UnityEngine;
using TheLastKnight.AI;

namespace TheLastKnight.Combat
{
    [RequireComponent(typeof(EnemyStats))]
    public class ParryReceiver : MonoBehaviour
    {
        public const float WindupDuration = 0.6f;
        public const float PerfectWindow = 0.2f;
        public const float TimingTolerance = 0.1f;
        private float _start = float.NegativeInfinity;
        private float _staggerUntil;
        private bool _windingUp;
        private LineRenderer _ring;
        private LineRenderer _target;
        [SerializeField] private SpriteRenderer _centerSprite;
        [SerializeField] private bool _useVisibleSpriteBounds;
        [SerializeField] private Vector2 _centerLocalOffset;
        [SerializeField] private Texture2D[] _stunEffectFrames;
        [SerializeField, Min(0.1f)] private float _stunEffectDuration = 2f;
        [SerializeField, Range(0f, 0.5f)] private float _stunEffectHeadXFraction;
        [SerializeField, Range(0f, 1f)] private float _stunEffectHeadYFraction;
        [SerializeField] private float _stunEffectHeadYOffset;
        [SerializeField, Min(0.01f)] private float _stunEffectWorldScale = 0.5f;
        private Sprite[] _stunSprites;
        private SpriteRenderer _stunRenderer;
        private float _stunEffectStart;
        private float _stunEffectUntil;
        public bool IsStaggered => Time.time < _staggerUntil;
        public bool IsWindingUp => _windingUp;
        public float Progress => Mathf.Clamp01((Time.time - _start) / WindupDuration);
        public SpriteRenderer CenterSprite => _centerSprite;

        public Vector3 GetVisualCenter()
        {
            Vector3 center = _centerSprite != null ? _centerSprite.bounds.center : transform.position + Vector3.up * 1.5f;
            if (_centerSprite != null && _useVisibleSpriteBounds)
                center = SpriteVisualBounds.GetWorldBounds(_centerSprite).center;
            if (_centerSprite != null)
            {
                Vector2 localOffset = _centerLocalOffset;
                // FaceDirection can mirror only the SpriteRenderer, leaving the
                // Transform scale positive. Mirror authored local offsets with it.
                if (_centerSprite.flipX) localOffset.x = -localOffset.x;
                if (_centerSprite.flipY) localOffset.y = -localOffset.y;
                center += _centerSprite.transform.TransformVector(localOffset);
            }
            return center;
        }

        public void SetSpriteCenter(SpriteRenderer sprite, bool useVisibleBounds)
        {
            _centerSprite = sprite;
            _useVisibleSpriteBounds = useVisibleBounds;
        }
        public static bool InPerfectWindow(float elapsed) => elapsed >= WindupDuration - PerfectWindow - TimingTolerance
            && elapsed <= WindupDuration + TimingTolerance;

        public void BeginWindup()
        {
            _start = Time.time;
            _windingUp = true;
        }

        public void FinishWindup() => _windingUp = false;

        public bool TryParry()
        {
            if (!_windingUp || !InPerfectWindow(Time.time - _start) || GetComponent<EnemyStats>().IsDead) return false;
            _windingUp = false;
            float stunDuration = _stunEffectFrames != null && _stunEffectFrames.Length > 0
                ? _stunEffectDuration : 1.5f;
            _staggerUntil = Time.time + stunDuration;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("parry");
            GetComponent<EnemyController>()?.CancelAttack();
            GetComponent<SlimeController>()?.CancelAttack();
            GetComponent<EnemyStats>().ApplyStatus(StatusEffect.Stunned, stunDuration);
            StartStunEffect();
            FloatingCombatText.Show(transform.position, "PARRY", Color.yellow);
            return true;
        }

        private void LateUpdate()
        {
            UpdateStunEffect();
            bool visible = _windingUp && !GetComponent<EnemyStats>().IsDead && TheLastKnight.Core.GameDifficultyManager.ShowHelpers;
            if (_ring == null && visible)
            {
                _ring = CreateRing("Parry timing");
                _target = CreateRing("Parry target");
            }
            if (_ring == null) return;
            _ring.enabled = _target.enabled = visible;
            DrawRing(_ring, Mathf.Lerp(1.2f, 0.35f, Progress));
            DrawRing(_target, 0.35f);
        }

        private void StartStunEffect()
        {
            if (_stunEffectFrames == null || _stunEffectFrames.Length == 0) return;
            if (_stunSprites == null)
            {
                _stunSprites = new Sprite[_stunEffectFrames.Length];
                for (int i = 0; i < _stunEffectFrames.Length; i++)
                {
                    var texture = _stunEffectFrames[i];
                    if (texture != null)
                        _stunSprites[i] = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                }
            }
            if (_stunRenderer == null)
            {
                var effect = new GameObject("Parry stun effect");
                effect.transform.SetParent(transform, false);
                _stunRenderer = effect.AddComponent<SpriteRenderer>();
                _stunRenderer.sortingLayerName = "InGame_UI";
                _stunRenderer.sortingOrder = 102;
            }
            _stunEffectStart = Time.time;
            _stunEffectUntil = Time.time + _stunEffectDuration;
            UpdateStunEffect();
        }

        private void UpdateStunEffect()
        {
            if (_stunRenderer == null) return;
            bool active = Time.time < _stunEffectUntil && !GetComponent<EnemyStats>().IsDead;
            _stunRenderer.enabled = active;
            if (!active || _stunSprites == null || _stunSprites.Length == 0) return;
            int frame = (int)((Time.time - _stunEffectStart) * 10f) % _stunSprites.Length;
            _stunRenderer.sprite = _stunSprites[frame];
            if (_stunRenderer.sprite == null) return;

            Transform effectTransform = _stunRenderer.transform;
            Vector3 parentScale = transform.lossyScale;
            effectTransform.localScale = new Vector3(_stunEffectWorldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                _stunEffectWorldScale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
            Bounds bodyBounds = _centerSprite != null
                ? (_useVisibleSpriteBounds ? SpriteVisualBounds.GetWorldBounds(_centerSprite) : _centerSprite.bounds)
                : new Bounds(transform.position, Vector3.one);
            bool facingLeft = (transform.lossyScale.x < 0f) != (_centerSprite != null && _centerSprite.flipX);
            float headX = bodyBounds.center.x + (facingLeft ? -1f : 1f)
                * bodyBounds.size.x * _stunEffectHeadXFraction;
            float headY = bodyBounds.max.y - bodyBounds.size.y * _stunEffectHeadYFraction
                + _stunEffectHeadYOffset;
            effectTransform.position = new Vector3(headX, headY, transform.position.z);
        }

        private LineRenderer CreateRing(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var ring = child.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.04f;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.startColor = ring.endColor = Color.yellow;
            ring.sortingOrder = 101;
            ring.sortingLayerName = "InGame_UI";
            return ring;
        }

        private void DrawRing(LineRenderer ring, float radius)
        {
            Vector3 center = GetVisualCenter();
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void OnDestroy()
        {
            if (_ring != null) Destroy(_ring.sharedMaterial);
            if (_target != null) Destroy(_target.sharedMaterial);
            if (_stunSprites != null)
                foreach (var sprite in _stunSprites)
                    if (sprite != null) Destroy(sprite);
        }
    }
}
