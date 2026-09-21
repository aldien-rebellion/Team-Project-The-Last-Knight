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
