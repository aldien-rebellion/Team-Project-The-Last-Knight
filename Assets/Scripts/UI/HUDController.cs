using UnityEngine;
using UnityEngine.UIElements;
using TheLastKnight.Stats;
using TheLastKnight.Input;

namespace TheLastKnight.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        
        private UIDocument _uiDocument;
        private VisualElement _healthFill;
        private VisualElement _staminaFill;
        private Label _healthText;
        private Label _levelValue;
        private Label _potionValue;
        private Label _quickItemKey;
        private VisualElement _potionIcon;
        private VisualElement _quickItemSlot;
        
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
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            
            // Find fill elements (Top-Left)
            _healthFill = root.Q<VisualElement>("HealthFill");
            _staminaFill = root.Q<VisualElement>("StaminaFill");
            _healthText = root.Q<Label>("HealthText");
            _levelValue = root.Q<Label>("LevelValue");

            // Find quick item elements (Bottom-Right)
            _potionValue = root.Q<Label>("PotionValue");
            _quickItemKey = root.Q<Label>("QuickItemKey");
            _potionIcon = root.Q<VisualElement>("PotionIcon");
            _quickItemSlot = root.Q<VisualElement>("QuickItemHUD");

            if (_potionIcon != null)
            {
                var sprite = Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean");
                if (sprite != null)
                {
                    if (_potionIcon is Image uiImg)
                    {
                        uiImg.sprite = sprite;
                        uiImg.scaleMode = ScaleMode.ScaleToFit;
                    }
                    else
                    {
                        _potionIcon.style.backgroundImage = new StyleBackground(sprite);
                    }
                }
            }

            // Set data source for automatic binding
            root.dataSource = _playerStats;
        }

        private void LateUpdate()
        {
            if (_playerStats == null) _playerStats = FindAnyObjectByType<PlayerStats>();
            if (_playerStats == null) return;

            // 1. Top-Left: Health & Stamina & Level
            if (_healthText != null) _healthText.text = _playerStats.HPText;
            if (_levelValue != null) _levelValue.text = _playerStats.Level.ToString();
            if (_healthFill != null) _healthFill.style.width = Length.Percent(_playerStats.HealthPercentage * 100f);
            if (_staminaFill != null) _staminaFill.style.width = Length.Percent(_playerStats.StaminaPercentage * 100f);

            // 2. Bottom-Right: Usable Q Item (Potion)
            if (_potionValue != null)
            {
                _potionValue.text = $"{_playerStats.HealingPotions}/5";
            }

            if (_quickItemKey != null)
            {
                string key = KeyRebindManager.GetCurrentBindingDisplay("UseDrink", 0);
                _quickItemKey.text = (!string.IsNullOrEmpty(key) && key != "Unknown" && key != "N/A") ? key : "Q";
            }

            if (_potionIcon != null)
            {
                _potionIcon.style.opacity = _playerStats.HealingPotions > 0 ? 1f : 0.4f;
            }
        }
    }
}
