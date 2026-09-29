using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.Combat
{
    public class FloatingHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyStats _targetStats;
        [SerializeField] private Image _healthBarFill;
        [SerializeField] private Canvas _canvas;

        [Header("Settings")]
        [SerializeField] private bool _autoHideOnDeath = true;
        [SerializeField] private bool _maintainWorldScale = true;
        [SerializeField] private SpriteRenderer _followSprite;
        [SerializeField] private bool _useVisibleSpriteBounds;
        [SerializeField] private float _headGap = 0.08f;
        [Tooltip("Horizontal head alignment offset in the followed sprite's local space.")]
        [SerializeField] private float _headHorizontalOffset;

        [Header("Animated Head Following")]
        [SerializeField] private bool _followSpriteBounds = false;
        [SerializeField] private float _headOffset = 0.12f;

        private Vector3 _originalScale;
        private float _fillLeftInset;
        private float _fillRightInset;
        private Text _levelText;
        private Text _healthPercentText;
        private SpriteRenderer _targetSpriteRenderer;

        private void Awake()
        {
            // The health bar always belongs to the enemy that owns this canvas.
            // Resolve it from the hierarchy so prefab-local references cannot point
            // at a stale component after a prefab is copied or rebuilt.
            var parentStats = GetComponentInParent<EnemyStats>();
            if (parentStats != null)
            {
                _targetStats = parentStats;
            }

            if (_canvas == null)
            {
                _canvas = GetComponent<Canvas>();
            }

            if (_healthBarFill == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var image in images)
                {
                    if (image.gameObject.name == "Fill")
                    {
                        _healthBarFill = image;
                        break;
                    }
                }
            }

            CaptureFillInsets();

            if (_followSpriteBounds)
            {
                _targetSpriteRenderer = GetComponentInParent<SpriteRenderer>();
            }

            _originalScale = transform.localScale;
            if (_canvas != null)
            {
                var label = new GameObject("Enemy Level", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(_canvas.transform, false);
                _levelText = label.GetComponent<Text>();
                _levelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // Keep the level label readable on small enemy bars without
                // allowing it to overflow when the label has more digits.
                _levelText.fontSize = 14;
                _levelText.resizeTextForBestFit = true;
                _levelText.resizeTextMinSize = 8;
                _levelText.resizeTextMaxSize = 14;
                _levelText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _levelText.verticalOverflow = VerticalWrapMode.Overflow;
                _levelText.alignment = TextAnchor.MiddleCenter;
                _levelText.raycastTarget = false;
                var rect = _levelText.rectTransform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.offsetMin = new Vector2(2, 0);
                rect.offsetMax = new Vector2(-2, 20);
                rect.anchoredPosition = new Vector2(0, 8);

                var percentObj = new GameObject("Health Percent", typeof(RectTransform), typeof(Text));
                percentObj.transform.SetParent(_canvas.transform, false);
                _healthPercentText = percentObj.GetComponent<Text>();
                _healthPercentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // The percentage is rendered inside the bar. Best-fit plus a
                // small inset keeps values such as 100% inside narrow bars.
                _healthPercentText.fontSize = 10;
                _healthPercentText.resizeTextForBestFit = true;
                _healthPercentText.resizeTextMinSize = 6;
                _healthPercentText.resizeTextMaxSize = 10;
                _healthPercentText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _healthPercentText.verticalOverflow = VerticalWrapMode.Overflow;
                _healthPercentText.alignment = TextAnchor.MiddleCenter;
                _healthPercentText.color = Color.white;
                _healthPercentText.raycastTarget = false;
                var rectP = _healthPercentText.rectTransform;
                rectP.anchorMin = Vector2.zero;
                rectP.anchorMax = Vector2.one;
                rectP.offsetMin = new Vector2(2, 0);
                rectP.offsetMax = new Vector2(-2, 0);
                rectP.anchoredPosition = Vector2.zero;
            }
        }

        private void OnEnable()
        {
            ResolveTargetStats();
            if (_targetStats != null)
            {
                _targetStats.OnHealthChanged += HandleHealthChanged;
                _targetStats.OnDeath += HandleDeath;
                HandleHealthChanged(_targetStats.CurrentHealth, _targetStats.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (_targetStats != null)
            {
                _targetStats.OnHealthChanged -= HandleHealthChanged;
                _targetStats.OnDeath -= HandleDeath;
            }
        }

        private void LateUpdate()
        {
            // A prefab can be enabled before its parent EnemyStats has finished
            // initializing. Keep resolving until the valid owner is available.
            if (_targetStats == null)
            {
                ResolveTargetStats();
                if (_targetStats != null)
                {
                    _targetStats.OnHealthChanged -= HandleHealthChanged;
                    _targetStats.OnHealthChanged += HandleHealthChanged;
                    _targetStats.OnDeath -= HandleDeath;
                    _targetStats.OnDeath += HandleDeath;
                }
            }

            // Keep the visual in sync even if damage was applied before this
            // component subscribed to the health event (or a prefab reference was missing).
            if (_targetStats != null)
            {
                HandleHealthChanged(_targetStats.CurrentHealth, _targetStats.MaxHealth);
            }

            if (!_followSpriteBounds && _followSprite != null && transform is RectTransform barRect)
            {
                Bounds bounds = _useVisibleSpriteBounds ? SpriteVisualBounds.GetWorldBounds(_followSprite) : _followSprite.bounds;
                float halfHeight = barRect.rect.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
                var parryAlignment = GetComponentInParent<ParryReceiver>();
                float centerX = parryAlignment != null && parryAlignment.CenterSprite == _followSprite
                    ? parryAlignment.GetVisualCenter().x
                    : bounds.center.x + GetHeadHorizontalOffsetWorld();
                transform.position = new Vector3(centerX, bounds.max.y + _headGap + halfHeight, transform.position.z);
            }
            if (_canvas != null) _canvas.enabled = TheLastKnight.Core.GameDifficultyManager.ShowHelpers;
            if (_levelText != null)
            {
                _levelText.enabled = TheLastKnight.Core.GameDifficultyManager.ShowEnemyLevel;
                _levelText.text = _targetStats != null ? "Lv. " + _targetStats.Level : "";
            }
            // Counteract parent flipping so health bar always stays upright and correctly oriented
            if (_maintainWorldScale && transform.parent != null)
            {
                float parentSignX = Mathf.Sign(transform.parent.lossyScale.x);
                Vector3 currentScale = transform.localScale;
                if (Mathf.Sign(currentScale.x) != parentSignX)
                {
                    currentScale.x = Mathf.Abs(_originalScale.x) * (parentSignX < 0 ? -1 : 1);
                    transform.localScale = currentScale;
                }
            }

            // Follow the current animated sprite bounds so the bar and level label
            // move with the monster's head when its pose changes (for example, a crouch).
            if (_followSpriteBounds && _targetSpriteRenderer != null && _targetSpriteRenderer.sprite != null)
            {
                Sprite currentSprite = _targetSpriteRenderer.sprite;
                Vector3 localHeadPoint;

                // Magma Punch frames 2 and 3 include tall fire/ground effects in
                // the sprite bounds. Use authored head points for those frames
                // instead of following the top of the effect.
                if (currentSprite.name.StartsWith("Volcanox_Magma Punch2"))
                {
                    localHeadPoint = GetSpriteLocalPoint(currentSprite, 245f, 300f);
                }
                else if (currentSprite.name.StartsWith("Volcanox_Magma Punch3"))
                {
                    localHeadPoint = GetSpriteLocalPoint(currentSprite, 320f, 315f);
                }
                else if (currentSprite.name.StartsWith("Volcanox_Infernal Slam2"))
                {
                    localHeadPoint = GetSpriteLocalPoint(currentSprite, 260f, 317f);
                }
                else
                {
                    Bounds spriteBounds = _useVisibleSpriteBounds
                        ? SpriteVisualBounds.GetWorldBounds(_targetSpriteRenderer)
                        : _targetSpriteRenderer.bounds;
                    float barHalfHeight = 0f;
                    if (_useVisibleSpriteBounds && transform is RectTransform visibleBoundsRect)
                        barHalfHeight = visibleBoundsRect.rect.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
                    Vector3 position = transform.position;
                    var parryAlignment = GetComponentInParent<ParryReceiver>();
                    position.x = parryAlignment != null && parryAlignment.CenterSprite == _followSprite
                        ? parryAlignment.GetVisualCenter().x
                        : spriteBounds.center.x + GetHeadHorizontalOffsetWorld();
                    position.y = spriteBounds.max.y + _headOffset + barHalfHeight;
                    transform.position = position;
                    return;
                }

                if (_targetSpriteRenderer.flipX) localHeadPoint.x = -localHeadPoint.x;
                if (_targetSpriteRenderer.flipY) localHeadPoint.y = -localHeadPoint.y;
                Vector3 headPosition = _targetSpriteRenderer.transform.TransformPoint(localHeadPoint);
                headPosition += Vector3.up * _headOffset;
                headPosition += GetHeadHorizontalOffsetVectorWorld();
                transform.position = new Vector3(headPosition.x, headPosition.y, transform.position.z);
            }
        }

        private float GetHeadHorizontalOffsetWorld()
        {
            return GetHeadHorizontalOffsetVectorWorld().x;
        }

        private Vector3 GetHeadHorizontalOffsetVectorWorld()
        {
            SpriteRenderer offsetReference = _targetSpriteRenderer != null ? _targetSpriteRenderer : _followSprite;
            if (offsetReference == null)
            {
                return Vector3.right * _headHorizontalOffset;
            }

            // SpriteRenderer.flipX mirrors the artwork around its pivot without
            // changing the Transform scale, so mirror local-space head offsets too.
            float spriteDirection = offsetReference.flipX ? -1f : 1f;
            return offsetReference.transform.TransformVector(Vector3.right * (_headHorizontalOffset * spriteDirection));
        }

        private void ResolveTargetStats()
        {
            var parentStats = GetComponentInParent<EnemyStats>();
            if (parentStats != null)
            {
                _targetStats = parentStats;
            }
        }

        private static Vector3 GetSpriteLocalPoint(Sprite sprite, float pixelX, float pixelY)
        {
            float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
            return new Vector3(
                (pixelX - sprite.pivot.x) / pixelsPerUnit,
                (pixelY - sprite.pivot.y) / pixelsPerUnit,
                0f);
        }

        public void Setup(EnemyStats stats, Image fillImage)
        {
            _targetStats = stats;
            _healthBarFill = fillImage;
            CaptureFillInsets();

            if (_targetStats != null)
            {
                _targetStats.OnHealthChanged += HandleHealthChanged;
                _targetStats.OnDeath += HandleDeath;
                HandleHealthChanged(_targetStats.CurrentHealth, _targetStats.MaxHealth);
            }
        }

        private void CaptureFillInsets()
        {
            if (_healthBarFill == null) return;
            var fillRect = _healthBarFill.rectTransform;
            _fillLeftInset = fillRect.offsetMin.x;
            _fillRightInset = -fillRect.offsetMax.x;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (max <= 0f) return;

            float pct = Mathf.Clamp01(current / max);
            if (_healthBarFill != null)
            {
                // These prefab Images have no sprite, so Filled ignores fillAmount.
                // Resize the RectTransform instead, keeping the authored insets.
                _healthBarFill.type = Image.Type.Simple;
                var fillRect = _healthBarFill.rectTransform;
                fillRect.anchorMax = new Vector2(pct, fillRect.anchorMax.y);
                var offsetMax = fillRect.offsetMax;
                offsetMax.x = _fillLeftInset * (1f - pct) - _fillRightInset * pct;
                fillRect.offsetMax = offsetMax;
            }

            if (_healthPercentText != null)
            {
                _healthPercentText.text = Mathf.RoundToInt(pct * 100f) + "%";
            }
        }

        private void HandleDeath()
        {
            if (_autoHideOnDeath && gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
