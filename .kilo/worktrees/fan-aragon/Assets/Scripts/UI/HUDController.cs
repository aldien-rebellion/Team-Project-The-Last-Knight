using UnityEngine;
using UnityEngine.UIElements;
using TheLastKnight.Stats;

namespace TheLastKnight.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        
        private UIDocument _uiDocument;
        private VisualElement _healthFill;
        private VisualElement _expFill;
        
        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            
            if (_playerStats == null)
            {
                _playerStats = FindAnyObjectByType<PlayerStats>();
            }
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            
            // Find fill elements
            _healthFill = root.Q<VisualElement>("HealthFill");
            _expFill = root.Q<VisualElement>("ExpFill");
            
            // Set data source for automatic binding (Labels with binding-path)
            root.dataSource = _playerStats;
        }

        private void LateUpdate()
        {
            if (_playerStats == null) return;
            
            // Manually update bar widths/scales as UIToolkit binding for styles is version-dependent
            if (_healthFill != null)
            {
                _healthFill.style.width = Length.Percent(_playerStats.HealthPercentage * 100f);
            }
            
            if (_expFill != null)
            {
                _expFill.style.width = Length.Percent(_playerStats.EXPPercentage * 100f);
            }
        }
    }
}
