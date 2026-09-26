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

        [Header("Animated Head Following")]
        [SerializeField] private bool _followSpriteBounds = false;
        [SerializeField] private float _headOffset = 0.12f;

        private Vector3 _originalScale;
        private Text _levelText;
        private SpriteRenderer _targetSpriteRenderer;

        private void Awake()
        {
            if (_targetStats == null)
            {
                _targetStats = GetComponentInParent<EnemyStats>();
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
                _levelText.fontSize = 18;
                _levelText.alignment = TextAnchor.MiddleCenter;
                _levelText.raycastTarget = false;
                var rect = _levelText.rectTransform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.sizeDelta = new Vector2(0, 24);
                rect.anchoredPosition = new Vector2(0, 14);
            }
        }

        private void OnEnable()
        {
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
            // Keep the visual in sync even if damage was applied before this
            // component subscribed to the health event (or a prefab reference was missing).
            if (_targetStats != null)
            {
                HandleHealthChanged(_targetStats.CurrentHealth, _targetStats.MaxHealth);
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
                    Bounds spriteBounds = _targetSpriteRenderer.bounds;
                    Vector3 position = transform.position;
                    position.x = spriteBounds.center.x;
                    position.y = spriteBounds.max.y + _headOffset;
                    transform.position = position;
                    return;
                }

                if (_targetSpriteRenderer.flipX) localHeadPoint.x = -localHeadPoint.x;
                if (_targetSpriteRenderer.flipY) localHeadPoint.y = -localHeadPoint.y;
                Vector3 headPosition = _targetSpriteRenderer.transform.TransformPoint(localHeadPoint);
                headPosition += Vector3.up * _headOffset;
                transform.position = new Vector3(headPosition.x, headPosition.y, transform.position.z);
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

            if (_targetStats != null)
            {
                _targetStats.OnHealthChanged += HandleHealthChanged;
                _targetStats.OnDeath += HandleDeath;
                HandleHealthChanged(_targetStats.CurrentHealth, _targetStats.MaxHealth);
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_healthBarFill != null && max > 0f)
            {
                _healthBarFill.fillAmount = Mathf.Clamp01(current / max);
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
