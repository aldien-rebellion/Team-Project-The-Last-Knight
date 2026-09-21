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

        private Vector3 _originalScale;
        private Text _levelText;

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
