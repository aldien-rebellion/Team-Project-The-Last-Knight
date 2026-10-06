using UnityEngine;
using UnityEngine.UIElements;
using TheLastKnight.Stats;
using TheLastKnight.Input;
using TheLastKnight.Core;

namespace TheLastKnight.UI
{
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerStats _playerStats;
        
        private UIDocument _uiDocument;
        private VisualElement _healthFill;
        private VisualElement _staminaFill;
        private VisualElement _staminaBar;
        private Label _healthText;
        private Label _levelValue;
        private Label _potionValue;
        private Label _quickItemKey;
        private VisualElement _potionIcon;
        private VisualElement _quickItemSlot;

        private Coroutine _staminaShakeCoroutine;
        private const float ShakeDuration = 0.35f;
        private const float ShakeMagnitude = 4f;

        public bool IsStaminaWarningActive => _staminaShakeCoroutine != null;
        public VisualElement StaminaBarElement => _staminaBar;
        
        private void Awake()
        {
            Instance = this;
            _uiDocument = GetComponent<UIDocument>();
            
            if (_playerStats == null)
            {
                _playerStats = FindAnyObjectByType<PlayerStats>();
            }

            if (SkillCooldownHUD.Instance == null)
            {
                var existing = FindAnyObjectByType<SkillCooldownHUD>();
                if (existing == null)
                {
                    var go = new GameObject("SkillCooldownHUD_Manager");
                    go.AddComponent<SkillCooldownHUD>();
                }
            }

            if (PlayerBuffHUD.Instance == null)
            {
                var existingBuff = FindAnyObjectByType<PlayerBuffHUD>();
                if (existingBuff == null)
                {
                    var go = new GameObject("PlayerBuffHUD_Manager");
                    go.AddComponent<PlayerBuffHUD>();
                }
            }
        }

        private void OnEnable()
        {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            root.style.unityFont = LocalizedText.Font;
            LocalizationManager.OnLanguageChanged += RefreshLanguage;
            RefreshLanguage(LocalizationManager.Current);
            
            // Find fill elements (Top-Left)
            _healthFill = root.Q<VisualElement>("HealthFill");
            _staminaFill = root.Q<VisualElement>("StaminaFill");
            _staminaBar = root.Q<VisualElement>("StaminaBar");
            _healthText = root.Q<Label>("HealthText");
            _levelValue = root.Q<Label>("LevelValue");

            ResetStaminaBarEffect();
            PlayerStats.OnInsufficientStaminaGlobal -= HandleInsufficientStamina;
            PlayerStats.OnInsufficientStaminaGlobal += HandleInsufficientStamina;

            // Find quick item elements (Bottom-Right)
            _potionValue = root.Q<Label>("PotionValue");
            _quickItemKey = root.Q<Label>("QuickItemKey");
            _potionIcon = root.Q<VisualElement>("PotionIcon");
            _quickItemSlot = root.Q<VisualElement>("QuickItemHUD");
            if (_quickItemSlot != null)
            {
                _quickItemSlot.pickingMode = PickingMode.Position;
                _quickItemSlot.RegisterCallback<ClickEvent>(OnQuickItemClicked);
            }

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

            // 2. Bottom-Right: Usable Q Item (Slot 1 of QuickItemManager)
            var qm = TheLastKnight.Core.QuickItemManager.Instance;
            var activeItem = qm != null ? qm.GetActiveItem() : null;

            if (activeItem != null && activeItem.count > 0)
            {
                if (_potionIcon != null)
                {
                    _potionIcon.style.opacity = 1f;
                    if (_potionIcon is Image uiImg && activeItem.icon != null)
                    {
                        if (uiImg.sprite != activeItem.icon)
                        {
                            uiImg.sprite = activeItem.icon;
                        }
                    }
                }

                if (_potionValue != null)
                {
                    _potionValue.text = activeItem.maxCount > 1 ? $"{activeItem.count}/{activeItem.maxCount}" : $"{activeItem.count}";
                }

                if (_quickItemSlot != null)
                {
                    _quickItemSlot.style.opacity = 1f;
                }
            }
            else
            {
                if (_potionIcon != null)
                {
                    _potionIcon.style.opacity = 0.2f;
                }
                if (_potionValue != null)
                {
                    _potionValue.text = "0";
                }
                if (_quickItemSlot != null)
                {
                    _quickItemSlot.style.opacity = 0.5f;
                }
            }

            if (_quickItemKey != null)
            {
                string key = KeyRebindManager.GetCurrentBindingDisplay("UseDrink", 0);
                _quickItemKey.text = (!string.IsNullOrEmpty(key) && key != "Unknown" && key != "N/A") ? key : "Q";
            }
        }

        private void RefreshLanguage(GameLanguage language)
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            var level = root.Q<Label>("LevelTag");
            var health = root.Q<Label>("HealthTag");
            var stamina = root.Q<Label>("StaminaTag");
            if (level != null) level.text = LocalizationManager.Translate("LVL");
            if (health != null) health.text = LocalizationManager.Translate("HP");
            if (stamina != null) stamina.text = LocalizationManager.Translate("STA");
        }

        private void OnDisable()
        {
            _quickItemSlot?.UnregisterCallback<ClickEvent>(OnQuickItemClicked);
            LocalizationManager.OnLanguageChanged -= RefreshLanguage;
            PlayerStats.OnInsufficientStaminaGlobal -= HandleInsufficientStamina;
            if (_staminaShakeCoroutine != null)
            {
                StopCoroutine(_staminaShakeCoroutine);
                _staminaShakeCoroutine = null;
            }
            ResetStaminaBarEffect();
            if (Instance == this) Instance = null;
        }

        private void OnQuickItemClicked(ClickEvent evt)
        {
            if (evt.button != 0) return;
            var player = GameManager.Instance?.Player;
            player?.GetComponent<PlayerInputHandler>()?.RequestUIAction("UseDrink");
        }

        public bool PointerOverQuickItem()
        {
            if (_quickItemSlot == null || _quickItemSlot.panel == null || !isActiveAndEnabled ||
                _quickItemSlot.resolvedStyle.display == DisplayStyle.None || UnityEngine.InputSystem.Mouse.current == null) return false;
            Vector2 mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Vector2 point = RuntimePanelUtils.ScreenToPanel(_quickItemSlot.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            return _quickItemSlot.worldBound.Contains(point);
        }

        private void HandleInsufficientStamina()
        {
            TriggerStaminaWarning();
        }

        public void TriggerStaminaWarning()
        {
            if (!gameObject.activeInHierarchy || _staminaBar == null) return;
            if (_staminaShakeCoroutine != null)
            {
                StopCoroutine(_staminaShakeCoroutine);
            }
            _staminaShakeCoroutine = StartCoroutine(StaminaShakeRoutine());
        }

        private System.Collections.IEnumerator StaminaShakeRoutine()
        {
            if (_staminaBar == null) yield break;

            // Apply red border
            _staminaBar.AddToClassList("stamina-bar-warning");
            _staminaBar.style.borderTopColor = new Color(1f, 0.15f, 0.15f, 1f);
            _staminaBar.style.borderBottomColor = new Color(1f, 0.15f, 0.15f, 1f);
            _staminaBar.style.borderLeftColor = new Color(1f, 0.15f, 0.15f, 1f);
            _staminaBar.style.borderRightColor = new Color(1f, 0.15f, 0.15f, 1f);
            _staminaBar.style.borderTopWidth = 2f;
            _staminaBar.style.borderBottomWidth = 2f;
            _staminaBar.style.borderLeftWidth = 2f;
            _staminaBar.style.borderRightWidth = 2f;

            float elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / ShakeDuration);
                float decay = 1f - progress;
                // High-frequency shake oscillation
                float offsetX = Mathf.Sin(elapsed * 55f) * ShakeMagnitude * decay;
                _staminaBar.transform.position = new Vector3(offsetX, 0f, 0f);
                yield return null;
            }

            ResetStaminaBarEffect();
            _staminaShakeCoroutine = null;
        }

        public void ResetStaminaBarEffect()
        {
            if (_staminaBar == null) return;
            _staminaBar.RemoveFromClassList("stamina-bar-warning");
            _staminaBar.style.borderTopColor = StyleKeyword.Null;
            _staminaBar.style.borderBottomColor = StyleKeyword.Null;
            _staminaBar.style.borderLeftColor = StyleKeyword.Null;
            _staminaBar.style.borderRightColor = StyleKeyword.Null;
            _staminaBar.style.borderTopWidth = StyleKeyword.Null;
            _staminaBar.style.borderBottomWidth = StyleKeyword.Null;
            _staminaBar.style.borderLeftWidth = StyleKeyword.Null;
            _staminaBar.style.borderRightWidth = StyleKeyword.Null;
            _staminaBar.transform.position = Vector3.zero;
        }
    }
}
