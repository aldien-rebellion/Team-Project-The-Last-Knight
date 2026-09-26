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
        [SerializeField] private float _headGap = 0.08f;

        private Vector3 _originalScale;
        private Text _levelText;
        private Text _healthPercentText;

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

                var percentObj = new GameObject("Health Percent", typeof(RectTransform), typeof(Text));
                percentObj.transform.SetParent(_canvas.transform, false);
                _healthPercentText = percentObj.GetComponent<Text>();
                _healthPercentText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _healthPercentText.fontSize = 12;
                _healthPercentText.alignment = TextAnchor.MiddleCenter;
                _healthPercentText.color = Color.white;
                _healthPercentText.raycastTarget = false;
                var rectP = _healthPercentText.rectTransform;
                rectP.anchorMin = Vector2.zero;
                rectP.anchorMax = Vector2.one;
                rectP.sizeDelta = Vector2.zero;
                rectP.anchoredPosition = Vector2.zero;
            }
        }

        private void OnEnable()
        {
            if (_targetStats != null)
            {
                _targetStats.OnHealthChanged += HandleHealthChanged;
                _targetStats.OnDeath += HandleDeath;
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
            if (_followSprite != null && transform is RectTransform barRect)
            {
                Bounds bounds = _followSprite.bounds;
                float halfHeight = barRect.rect.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
                transform.position = new Vector3(bounds.center.x, bounds.max.y + _headGap + halfHeight, transform.position.z);
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
            if (max > 0f)
            {
                float pct = Mathf.Clamp01(current / max);
                if (_healthBarFill != null)
                {
                    _healthBarFill.fillAmount = pct;
                    var fillRt = _healthBarFill.rectTransform;
                    if (fillRt != null)
                    {
                        Vector2 maxAnchor = fillRt.anchorMax;
                        maxAnchor.x = pct;
                        fillRt.anchorMax = maxAnchor;
                    }
                }
                if (_healthPercentText != null)
                {
                    _healthPercentText.text = Mathf.RoundToInt(pct * 100f) + "%";
                }
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
