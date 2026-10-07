using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using TheLastKnight.Stats;
using TheLastKnight.Core;
using TheLastKnight.Environment;
using TheLastKnight.Audio;
using TheLastKnight.Inventory;

namespace TheLastKnight.UI
{
    [DefaultExecutionOrder(-500)]
    public class CharacterStatusUI : MonoBehaviour
    {
        public static CharacterStatusUI Instance { get; private set; }

        [Header("State")]
        private bool _isOpen;
        public bool IsOpen => _isOpen;

        // UI Canvas Components
        private GameObject _canvasObject;
        public GameObject CanvasObject => _canvasObject;
        private Canvas _canvas;
        public Canvas Canvas => _canvas;
        private CanvasScaler _scaler;
        private GraphicRaycaster _raycaster;
        private RectTransform _windowRect;
        private GameObject _itemUseMenu;

        private void HideItemUseMenu()
        {
            if (_itemUseMenu == null) return;
            _itemUseMenu.SetActive(false);
            Destroy(_itemUseMenu);
            _itemUseMenu = null;
        }

        public void ShowItemUseMenu(SlotType type, int index, Vector2 screenPosition)
        {
            HideItemUseMenu();
            HideTooltip();
            var inventory = InventoryManager.Instance;
            var item = inventory?.GetSlot(type, index);
            if (!_isOpen || _canvasObject == null || item == null || inventory.CursorHeldItem != null) return;

            _itemUseMenu = new GameObject("ItemUseMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            _itemUseMenu.transform.SetParent(_canvasObject.transform, false);
            var overlay = _itemUseMenu.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            _itemUseMenu.GetComponent<Image>().color = Color.clear;
            _itemUseMenu.GetComponent<Button>().onClick.AddListener(HideItemUseMenu);

            var buttonObject = new GameObject("Use", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(overlay, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(120f, 40f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screenPosition, null, out var position);
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp(position.x, overlay.rect.xMin, overlay.rect.xMax - rect.sizeDelta.x),
                Mathf.Clamp(position.y, overlay.rect.yMin + rect.sizeDelta.y, overlay.rect.yMax));
            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.10f, 0.07f, 0.98f);
            var button = buttonObject.GetComponent<Button>();
            button.interactable = inventory.CanUseSlot(type, index, GetPlayer());
            button.onClick.AddListener(() =>
            {
                HideItemUseMenu();
                // A stale menu must never consume a different item moved into the slot.
                if (inventory.GetSlot(type, index) != item) return;
                if (inventory.UseSlot(type, index, GetPlayer()))
                {
                    AudioManager.Instance?.PlaySfx("click");
                    Refresh(true);
                }
            });
            var label = CreateText(buttonObject.transform, "Label", "ใช้", 18f,
                TextAlignmentOptions.Center, new Color(1f, 0.88f, 0.60f), FontStyles.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        // Header Elements
        private TextMeshProUGUI _txtLevel;

        // Bars
        private Image _imgXpFill;
        private TextMeshProUGUI _txtXp;
        private Image _imgHpFill;
        private TextMeshProUGUI _txtHp;
        private Image _imgStmFill;
        private TextMeshProUGUI _txtStm;

        // Gold
        private TextMeshProUGUI _txtGold;

        // Attributes & Status Points
        private TextMeshProUGUI _txtStatusPoints;
        private TextMeshProUGUI _txtStrValue;
        private TextMeshProUGUI _txtAgiValue;
        private TextMeshProUGUI _txtVitValue;
        private TextMeshProUGUI _txtDexValue;

        private Button _btnStrPlus, _btnStrMax;
        private Button _btnAgiPlus, _btnAgiMax;
        private Button _btnVitPlus, _btnVitMax;
        private Button _btnDexPlus, _btnDexMax;

        // Side Status Allocation Panel (Dropdown/flyout to the right)
        private bool _isSidePanelOpen;
        public bool IsSidePanelOpen => _isSidePanelOpen;
        private GameObject _sidePanelGo;
        public GameObject SidePanelGo => _sidePanelGo;
        private RectTransform _sidePanelRect;
        public RectTransform SidePanelRect => _sidePanelRect;
        private RectTransform _sideToggleBtnRect;
        public RectTransform SideToggleBtnRect => _sideToggleBtnRect;
        private TextMeshProUGUI _txtSideToggleArrow;
        public TextMeshProUGUI TxtSideToggleArrow => _txtSideToggleArrow;
        private Image _imgSideToggleBg;
        private readonly Dictionary<string, TMP_InputField> _customStatInputs = new Dictionary<string, TMP_InputField>();
        public IReadOnlyDictionary<string, TMP_InputField> CustomStatInputs => _customStatInputs;
        private readonly List<Button> _sidePanelButtons = new List<Button>();
        public IReadOnlyList<Button> SidePanelButtons => _sidePanelButtons;

        // Tooltip Elements
        private GameObject _tooltipBox;
        private TextMeshProUGUI _txtTooltipTitle;
        private TextMeshProUGUI _txtTooltipSubtitle;
        private TextMeshProUGUI _txtTooltipDesc;
        private TextMeshProUGUI _txtTooltipHint;
        private string _currentHoveredStat;
        private string _currentHoveredPrefix;

        // Slots
        private readonly List<TheLastKnight.Inventory.InventorySlotUI> _gridSlotUIs = new List<TheLastKnight.Inventory.InventorySlotUI>();
        private readonly List<TheLastKnight.Inventory.InventorySlotUI> _quickSlotUIs = new List<TheLastKnight.Inventory.InventorySlotUI>();
        private readonly List<SkillSlotUI> _skillSlots = new List<SkillSlotUI>();

        // Left Buff Dock (Docked to wooden frame expanding leftwards)
        private RectTransform _leftBuffDock;
        public RectTransform LeftBuffDock => _leftBuffDock;
        private readonly List<BuffCardUI> _statusBuffCards = new List<BuffCardUI>();
        public IReadOnlyList<BuffCardUI> StatusBuffCards => _statusBuffCards;
        private readonly List<ActiveBuffInfo> _cachedActiveBuffs = new List<ActiveBuffInfo>();

        // Floating Cursor Follower
        private GameObject _cursorFollower;
        private Image _cursorIcon;
        private TextMeshProUGUI _cursorCount;

        private PlayerStats _cachedStats;
        private InventoryManager _boundInventory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Application.isPlaying && Instance == null)
            {
                var go = new GameObject("CharacterStatusUI_Manager");
                go.AddComponent<CharacterStatusUI>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            _boundInventory = InventoryManager.Instance;
            if (_boundInventory != null) _boundInventory.OnInventoryChanged += OnInventoryDataChanged;
            EnsureEventSystem();
            BuildUI();
            SetWindowVisible(false);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_boundInventory != null) _boundInventory.OnInventoryChanged -= OnInventoryDataChanged;
            SetHUDVisible(true);
            if (Instance == this) Instance = null;
        }

        private void OnInventoryDataChanged()
        {
            if (_isOpen)
            {
                Refresh(false);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isOpen)
            {
                Close();
            }
        }

        private void Start()
        {
            EnsureEventSystem();
            if (_canvasObject == null)
            {
                BuildUI();
                SetWindowVisible(false);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 1. Never toggle or remain open in MainMenu scene
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                if (_isOpen) Close();
                return;
            }

            // 2. Never toggle when typing in any InputField
            if (EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null)
                {
                    if (selected.GetComponent<InputField>() != null ||
                        selected.GetComponent<TMP_InputField>() != null)
                    {
                        return;
                    }
                }
            }

            // 3. Do not open if Pause Menu is open or input is blocked
            if (!_isOpen)
            {
                if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen) return;
                if (GameManager.Instance != null && GameManager.Instance.InputBlocked) return;
                var player = GetPlayer();
                if (player == null || (Application.isPlaying && player.IsDead)) return;
            }

            // Toggle with 'B' key
            if (TheLastKnight.Input.KeyRebindManager.WasPressedThisFrame("ToggleStatus"))
            {
                Toggle();
                return;
            }

            // Close with Escape if open
            if (_isOpen)
            {
                UpdateCursorFollower();
                UpdateTooltipPosition();

                // Press Q while holding an item to drop it into the world (Ctrl+Q drops whole stack, Q drops 1)
                if (TheLastKnight.Input.KeyRebindManager.WasPressedThisFrame("DropItem"))
                {
                    var inv = InventoryManager.Instance;
                    if (inv != null && inv.CursorHeldItem != null)
                    {
                        var p = GetPlayer();
                        Vector3 dropPos = p != null ? p.transform.position : Vector3.zero;
                        bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                        inv.DropCursorItemToWorld(!ctrlPressed, dropPos);
                        AudioManager.Instance?.PlaySfx("click");
                    }
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Close();
                    return;
                }

                Refresh(false);
            }
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase)) return;

            EnsureEventSystem();
            if (_canvasObject == null) BuildUI();

            var player = GetPlayer();
            if (player == null || (Application.isPlaying && player.IsDead)) return;

            _isOpen = true;
            if (Application.isPlaying)
            {
                GameManager.Instance?.SetInputBlocked(true);
                Time.timeScale = 0f;
            }

            SetHUDVisible(false);
            // Restore UI input after blocking gameplay and hiding the HUD.
            EnsureEventSystem();
            SetWindowVisible(true);
            Refresh(true);
        }

        public void Close()
        {
            if (!_isOpen) return;

            InventoryManager.Instance?.Close();

            _isOpen = false;
            SetWindowVisible(false);
            HideTooltip();
            SetHUDVisible(true);

            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
                GameManager.Instance?.SetInputBlocked(false);
            }
        }

        private HUDController _cachedHUD;

        private void SetHUDVisible(bool visible)
        {
            if (_cachedHUD == null)
            {
                _cachedHUD = FindAnyObjectByType<HUDController>(FindObjectsInactive.Include);
            }

            if (_cachedHUD != null)
            {
                var doc = _cachedHUD.GetComponent<UnityEngine.UIElements.UIDocument>();
                if (doc != null && doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.style.display = visible ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
                }
                // Keep the document alive so hiding the HUD does not tear down shared UI input.
            }
            SkillCooldownHUD.Instance?.SetVisible(visible);
            PlayerBuffHUD.Instance?.SetVisible(visible);
        }

        private void SetWindowVisible(bool visible)
        {
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(visible);
            }
            if (!visible)
            {
                HideItemUseMenu();
                HideTooltip();
            }
        }

        public PlayerStats GetPlayer()
        {
            if (_cachedStats == null || !_cachedStats.gameObject.activeInHierarchy)
            {
                if (GameManager.Instance != null && GameManager.Instance.Player != null)
                {
                    _cachedStats = GameManager.Instance.Player;
                }
                else
                {
                    _cachedStats = FindAnyObjectByType<PlayerStats>();
                }
            }
            return _cachedStats;
        }

        private void EnsureEventSystem()
        {
            var all = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            UnityEngine.EventSystems.EventSystem activeEs = null;

            if (all != null && all.Length > 0)
            {
                activeEs = all[0];
                for (int i = 1; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].gameObject != null)
                    {
                        all[i].enabled = false;
                        all[i].gameObject.SetActive(false);
                        if (Application.isPlaying) Destroy(all[i].gameObject);
                        else DestroyImmediate(all[i].gameObject);
                    }
                }
            }
            else
            {
                var go = new GameObject("EventSystem");
                activeEs = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            if (activeEs != null)
            {
                activeEs.enabled = true;
                activeEs.gameObject.SetActive(true);

                var legacy = activeEs.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (legacy != null)
                {
                    legacy.enabled = false;
                    if (Application.isPlaying) Destroy(legacy);
                    else DestroyImmediate(legacy);
                }

                var module = activeEs.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (module == null)
                {
                    module = activeEs.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
                module.enabled = true;

                UIInputLifetime.Configure(module);
            }
        }
        private Vector2 ToUI(float px, float py)
        {
            // Exact 1:1 pixel mapping on 805x466 native resolution
            return new Vector2(px - 402.5f, py - 233.0f);
        }

        #region UI Construction
        private void BuildUI()
        {
            if (_canvasObject != null) return;

            EnsureEventSystem();

            // Root Canvas
            _canvasObject = new GameObject("CharacterStatusCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;

            _scaler = _canvasObject.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1280, 720);
            _scaler.matchWidthOrHeight = 0.5f;

            _raycaster = _canvasObject.GetComponent<GraphicRaycaster>();
            _canvasObject.transform.SetParent(transform, false);

            // Semi-transparent Backdrop with Minecraft-style drop handler
            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(TheLastKnight.Inventory.BackdropClickHandler));
            backdropGo.transform.SetParent(_canvasObject.transform, false);
            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;

            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0.015f, 0.02f, 0.035f, 0.78f);

            // Main Window Container (Native 805x466 resolution)
            var winGo = new GameObject("Window", typeof(RectTransform), typeof(Image));
            winGo.transform.SetParent(_canvasObject.transform, false);
            _windowRect = winGo.GetComponent<RectTransform>();
            _windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            _windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _windowRect.pivot = new Vector2(0.5f, 0.5f);
            _windowRect.sizeDelta = new Vector2(805, 466);

            var winImg = winGo.GetComponent<Image>();
            var winSprite = Resources.Load<Sprite>("CharacterStatus/Window_Localized");
            if (winSprite != null)
            {
                winImg.sprite = winSprite;
                winImg.preserveAspect = true;
            }
            else
            {
                winImg.color = new Color(0.18f, 0.12f, 0.08f, 0.98f);
            }

            // Center Panel Overlays (Level, Bars, Gold, Skills, Quick Items)
            BuildCenterOverlays(_windowRect);

            // Right Panel Overlays (Status Points, STR/AGI/VIT/DEX, Inventory Grid)
            BuildRightOverlays(_windowRect);

            // Left Dock Overlays (Active Buffs attached to wooden frame expanding leftwards)
            BuildLeftBuffDock(_windowRect);

            // Side Status Allocation Drawer (Flyout to the right with [+1], [+10], [custom], [max])
            BuildSideStatusDrawer(_windowRect);

            // Close Button [X] at Top-Right (built after overlays to stay topmost)
            BuildCloseButton(_windowRect);
            BuildLocalizedLabels(_windowRect);

            // Floating Tooltip Box (follows cursor on Canvas)
            BuildTooltipBox(_canvasObject.transform);

            // Floating Cursor Item Follower (topmost on Canvas)
            BuildCursorFollower(_canvasObject.transform);
        }

        private void BuildCursorFollower(Transform parent)
        {
            _cursorFollower = new GameObject("CursorFollower", typeof(RectTransform), typeof(CanvasGroup));
            _cursorFollower.transform.SetParent(parent, false);
            _cursorFollower.transform.SetAsLastSibling();

            var cg = _cursorFollower.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var rt = _cursorFollower.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(38, 38);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_cursorFollower.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            _cursorIcon = iconGo.GetComponent<Image>();
            _cursorIcon.raycastTarget = false;
            _cursorIcon.preserveAspect = true;

            _cursorCount = CreateText(_cursorFollower.transform, "Count", "", 11, TextAlignmentOptions.BottomRight,
                new Color(1f, 0.95f, 0.5f), FontStyles.Bold);
            var cRt = _cursorCount.rectTransform;
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = new Vector2(-2, 2);

            _cursorFollower.SetActive(false);
        }

        private void UpdateCursorFollower()
        {
            var inv = TheLastKnight.Inventory.InventoryManager.Instance;
            if (inv != null && inv.CursorHeldItem != null && inv.CursorHeldItem.count > 0)
            {
                if (_cursorFollower != null)
                {
                    if (!_cursorFollower.activeSelf) _cursorFollower.SetActive(true);
                    if (_cursorIcon != null && _cursorIcon.sprite != inv.CursorHeldItem.Icon)
                    {
                        _cursorIcon.sprite = inv.CursorHeldItem.Icon;
                        _cursorIcon.color = Color.white;
                    }
                    if (_cursorCount != null)
                    {
                        TheLastKnight.UI.LocalizedText.Set(_cursorCount, inv.CursorHeldItem.count > 1 ? inv.CursorHeldItem.count.ToString() : "");
                    }

                    Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)UnityEngine.Input.mousePosition;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasObject.GetComponent<RectTransform>(),
                        mousePos,
                        null,
                        out Vector2 localPoint))
                    {
                        _cursorFollower.GetComponent<RectTransform>().anchoredPosition = localPoint;
                    }
                }
            }
            else
            {
                if (_cursorFollower != null && _cursorFollower.activeSelf)
                {
                    _cursorFollower.SetActive(false);
                }
            }
        }

        private void BuildCloseButton(RectTransform parent)
        {
            var btnGo = new GameObject("Btn_Close", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            btnGo.transform.SetAsLastSibling();
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchoredPosition = ToUI(786, 448);
            rt.sizeDelta = new Vector2(36, 36);

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.01f);
            img.raycastTarget = true;

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(Close);

            AddHoverHighlight(btnGo, img);
        }

        private void BuildLocalizedLabels(RectTransform parent)
        {
            // The background has no baked lettering; render localized text directly on the wood.
            LocalizedArtworkLabel(parent, "Arthur Reuven", 403, 392, 250, 32, 22);
            var pointsLabel = LocalizedArtworkLabel(parent, "Status Points", 666, 420, 173, 23, 14);
            pointsLabel.rectTransform.offsetMax = new Vector2(-48, 0);
            _txtStatusPoints.transform.parent.SetAsLastSibling();
            LocalizedArtworkLabel(parent, "Gold", 350, 249, 65, 21, 14);
            LocalizedArtworkLabel(parent, "Skill 1", 329, 221, 67, 21, 13);
            LocalizedArtworkLabel(parent, "Skill 2", 413, 221, 67, 21, 13);
            LocalizedArtworkLabel(parent, "Skill 3", 499, 221, 67, 21, 13);
            LocalizedArtworkLabel(parent, "Quick Items", 421, 112, 146, 22, 15);
            string[] attributes = { "STR", "AGI", "VIT", "DEX" };
            float[] positions = { 388, 359, 330, 302 };
            for (int i = 0; i < attributes.Length; i++)
            {
                LocalizedArtworkLabel(parent, attributes[i], 606, positions[i], 48, 24, 14);
                LocalizedArtworkLabel(parent, "MAX", 737, positions[i], 37, 17, 10);
            }
        }

        private TextMeshProUGUI LocalizedArtworkLabel(Transform parent, string source, float x, float y,
            float width, float height, float size)
        {
            var cover = new GameObject("Localized " + source, typeof(RectTransform));
            cover.transform.SetParent(parent, false);
            var rect = cover.GetComponent<RectTransform>();
            rect.anchoredPosition = ToUI(x, y);
            rect.sizeDelta = new Vector2(width, height);
            var label = CreateText(cover.transform, "Label", source, size, TextAlignmentOptions.Center,
                new Color(0.94f, 0.82f, 0.65f), FontStyles.Bold);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = size - 3;
            label.fontSizeMax = size;
            return label;
        }

        private void BuildCenterOverlays(RectTransform parent)
        {
            // 1. Dynamic Level Text ("LV.1")
            var lvlGo = new GameObject("Txt_Level", typeof(RectTransform));
            lvlGo.transform.SetParent(parent, false);
            var lvlRt = lvlGo.GetComponent<RectTransform>();
            lvlRt.anchoredPosition = ToUI(408, 363);
            lvlRt.sizeDelta = new Vector2(230, 24);

            _txtLevel = CreateText(lvlGo.transform, "Label", "LV.1", 19, TextAlignmentOptions.MidlineLeft,
                new Color(0.96f, 0.94f, 0.90f), FontStyles.Bold);
            var ltRt = _txtLevel.rectTransform;
            ltRt.anchorMin = Vector2.zero; ltRt.anchorMax = Vector2.one;
            ltRt.offsetMin = ltRt.offsetMax = Vector2.zero;
            _txtLevel.raycastTarget = true;
            AddHoverTrigger(_txtLevel.gameObject,
                () =>
                {
                    var player = GetPlayer();
                    if (player == null) return;
                    ShowTooltip($"Level {player.Level}", "Defense (DEF)",
                        $"DEF: {player.Defense:0.##}\nDefense increases with level and reduces incoming damage.");
                }, HideTooltip);

            // 2. Bars: XP, HP, STM
            CreateStatBar(parent, "XP_Bar", ToUI(408, 335), new Vector2(230, 14),
                new Color(0.25f, 0.45f, 0.85f), out _imgXpFill, out _txtXp, "XP: 0/100");

            CreateStatBar(parent, "HP_Bar", ToUI(408, 308), new Vector2(230, 14),
                new Color(0.85f, 0.18f, 0.15f), out _imgHpFill, out _txtHp, "HP: 100/150");

            CreateStatBar(parent, "STM_Bar", ToUI(408, 281), new Vector2(230, 14),
                new Color(0.85f, 0.55f, 0.15f), out _imgStmFill, out _txtStm, "STM: 30/100");

            // 3. Dynamic Gold Display
            var goldGo = new GameObject("Txt_Gold", typeof(RectTransform));
            goldGo.transform.SetParent(parent, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            goldRt.anchoredPosition = ToUI(456, 250);
            goldRt.sizeDelta = new Vector2(146, 20);

            _txtGold = CreateText(goldGo.transform, "Label", "0", 15, TextAlignmentOptions.MidlineLeft,
                Color.white, FontStyles.Bold);
            var gtRt = _txtGold.rectTransform;
            gtRt.anchorMin = Vector2.zero; gtRt.anchorMax = Vector2.one;
            gtRt.offsetMin = gtRt.offsetMax = Vector2.zero;

            // 4. Skills Section (3 Slots)
            string[] skillNames = { "Carnage Burst (E)", "Iron Will (R)", "Excalibur (T)" };
            string[] skillDescs = {
                "Arthur unleashes a whirlwind strike hitting all nearby enemies with massive area damage. Hotkey [E].",
                "Enter a berserk focus, boosting attack power and movement speed for 10 seconds. Hotkey [R].",
                "Channel the full radiance of the holy blade, unleashing a piercing holy beam across the battlefield. Hotkey [T]."
            };
            float[] skillXs = { 328f, 418f, 502f };
            int[] unlockLevels = { Player.PlayerController.Skill1UnlockLevel,
                Player.PlayerController.Skill2UnlockLevel, Player.PlayerController.Skill3UnlockLevel };

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var slotGo = new GameObject($"SkillSlot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotGo.transform.SetParent(parent, false);
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchoredPosition = ToUI(skillXs[i], 173);
                rt.sizeDelta = new Vector2(54, 54);

                var img = slotGo.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.01f); // Transparent over baked skill icon

                var btn = slotGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    ShowSkillTooltip(index);
                    AudioManager.Instance?.PlaySfx("click");
                });

                AddHoverHighlight(slotGo, img);
                AddHoverTrigger(slotGo,
                    () => ShowSkillTooltip(index),
                    HideTooltip);

                var lockOverlay = SkillCooldownHUD.CreateLockOverlay(slotGo.transform, unlockLevels[i]);
                lockOverlay.transform.localScale = Vector3.one * (54f / 48f);
                lockOverlay.SetActive(i > 0);
                _skillSlots.Add(new SkillSlotUI { button = btn, icon = img, name = skillNames[i],
                    description = skillDescs[i], lockOverlay = lockOverlay, unlockLevel = unlockLevels[i] });
            }

            // 5. Quick Items Section (5 Slots) - Priority Queue 1 to 5
            float[] quickXs = { 316.5f, 366.5f, 416.5f, 466.5f, 516.5f };

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var qGo = new GameObject($"QuickSlot_{i + 1}", typeof(RectTransform), typeof(Image));
                qGo.transform.SetParent(parent, false);
                var rt = qGo.GetComponent<RectTransform>();
                rt.anchoredPosition = ToUI(quickXs[i], 68);
                rt.sizeDelta = new Vector2(38, 38);

                var bgImg = qGo.GetComponent<Image>();
                bgImg.color = new Color(1f, 1f, 1f, 0.005f);
                bgImg.raycastTarget = true;

                // Highlight overlay on hover (topmost, golden border + white sheen)
                var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image), typeof(Outline));
                hlGo.transform.SetParent(qGo.transform, false);
                var hlRt = hlGo.GetComponent<RectTransform>();
                hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
                hlRt.offsetMin = Vector2.zero; hlRt.offsetMax = Vector2.zero;
                var hlImg = hlGo.GetComponent<Image>();
                hlImg.color = new Color(1f, 1f, 1f, 0f);
                hlImg.raycastTarget = false;

                var hlOutline = hlGo.GetComponent<Outline>();
                hlOutline.effectColor = new Color(1f, 0.88f, 0.35f, 0.95f);
                hlOutline.effectDistance = new Vector2(1.5f, -1.5f);
                hlOutline.enabled = false;

                // Priority number badge (1-5) at bottom-right
                var numTxt = CreateText(qGo.transform, "PriorityNumber", (i + 1).ToString(), 11, TextAlignmentOptions.BottomRight,
                    new Color(0.85f, 0.75f, 0.55f, 0.85f), FontStyles.Bold);
                var numRt = numTxt.rectTransform;
                numRt.anchorMin = Vector2.zero; numRt.anchorMax = Vector2.one;
                numRt.offsetMin = Vector2.zero;
                numRt.offsetMax = new Vector2(-2, 2);

                // Item icon (dynamic)
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(qGo.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(3, 3); iconRt.offsetMax = new Vector2(-3, -3);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                iconImg.color = new Color(1f, 1f, 1f, 0f);

                // Count text (e.g. 64)
                var countTxt = CreateText(qGo.transform, "Count", "", 10, TextAlignmentOptions.TopRight,
                    new Color(1f, 0.92f, 0.5f), FontStyles.Bold);
                var ctRt = countTxt.rectTransform;
                ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
                ctRt.offsetMin = Vector2.zero;
                ctRt.offsetMax = new Vector2(-2, -2);

                // Ensure highlight renders on top of icon and text
                hlGo.transform.SetAsLastSibling();

                var slotUI = qGo.AddComponent<TheLastKnight.Inventory.InventorySlotUI>();
                slotUI.slotType = TheLastKnight.Inventory.SlotType.QuickSlot;
                slotUI.slotIndex = index;
                slotUI.iconImage = iconImg;
                slotUI.countText = countTxt;
                slotUI.highlightImage = hlImg;
                slotUI.highlightOutline = hlOutline;

                _quickSlotUIs.Add(slotUI);
            }
        }

        private void CreateStatBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor,
            out Image fillImg, out TextMeshProUGUI labelTxt, string defaultText)
        {
            var barGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(parent, false);
            var rt = barGo.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = barGo.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.04f, 0.70f);

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(1, 1); fillRt.offsetMax = new Vector2(-1, -1);

            fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.color = fillColor;

            // Label
            labelTxt = CreateText(barGo.transform, "Label", defaultText, 13, TextAlignmentOptions.Center,
                Color.white, FontStyles.Bold);
            var lblRt = labelTxt.rectTransform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
        }

        private void BuildRightOverlays(RectTransform parent)
        {
            // Keep the points value in its own column to the right of the heading.
            var spGo = new GameObject("Txt_SP", typeof(RectTransform));
            spGo.transform.SetParent(parent, false);
            var spRt = spGo.GetComponent<RectTransform>();
            spRt.anchoredPosition = ToUI(715, 420);
            spRt.pivot = new Vector2(0f, 0.5f);
            spRt.sizeDelta = new Vector2(45, 22);

            _txtStatusPoints = CreateText(spGo.transform, "Label", "0", 17, TextAlignmentOptions.MidlineRight,
                Color.white, FontStyles.Bold);
            _txtStatusPoints.enableWordWrapping = false;
            _txtStatusPoints.overflowMode = TextOverflowModes.Truncate;
            _txtStatusPoints.enableAutoSizing = true;
            _txtStatusPoints.fontSizeMin = 10;
            _txtStatusPoints.fontSizeMax = 17;
            var sptRt = _txtStatusPoints.rectTransform;
            sptRt.anchorMin = Vector2.zero; sptRt.anchorMax = Vector2.one;
            sptRt.offsetMin = sptRt.offsetMax = Vector2.zero;

            // 2. Attributes Rows (STR, AGI, VIT, DEX)
            float[] statYs = { 388f, 359f, 331f, 303f };

            BuildStatRow(parent, "Row_STR", "STR", statYs[0], out _txtStrValue, out _btnStrPlus, out _btnStrMax,
                () => UpgradeStat("STR"), () => UpgradeStatMax("STR"));

            BuildStatRow(parent, "Row_AGI", "AGI", statYs[1], out _txtAgiValue, out _btnAgiPlus, out _btnAgiMax,
                () => UpgradeStat("AGI"), () => UpgradeStatMax("AGI"));

            BuildStatRow(parent, "Row_VIT", "VIT", statYs[2], out _txtVitValue, out _btnVitPlus, out _btnVitMax,
                () => UpgradeStat("VIT"), () => UpgradeStatMax("VIT"));

            BuildStatRow(parent, "Row_DEX", "DEX", statYs[3], out _txtDexValue, out _btnDexPlus, out _btnDexMax,
                () => UpgradeStat("DEX"), () => UpgradeStatMax("DEX"));

            // 3. Inventory Grid (4 Columns x 6 Rows = 24 Slots)
            BuildInventoryGrid(parent);
        }

        private void BuildStatRow(Transform parent, string name, string label, float py,
            out TextMeshProUGUI valueTxt, out Button btnPlus, out Button btnMax,
            UnityEngine.Events.UnityAction onPlus, UnityEngine.Events.UnityAction onMax)
        {
            // Hover covers the baked attribute label and its value, stopping before [+].
            var valGo = new GameObject($"Val_{name}", typeof(RectTransform), typeof(Image));
            valGo.transform.SetParent(parent, false);
            var valImg = valGo.GetComponent<Image>();
            valImg.color = new Color(1f, 1f, 1f, 0.001f);
            valImg.raycastTarget = true;
            var valRt = valGo.GetComponent<RectTransform>();
            valRt.anchoredPosition = ToUI(631, py);
            valRt.sizeDelta = new Vector2(98, 24);

            valueTxt = CreateText(valGo.transform, "Label", "10", 16, TextAlignmentOptions.MidlineRight,
                Color.white, FontStyles.Bold);
            valueTxt.enableWordWrapping = false;
            valueTxt.overflowMode = TextOverflowModes.Truncate;
            valueTxt.enableAutoSizing = true;
            valueTxt.fontSizeMin = 10;
            valueTxt.fontSizeMax = 16;
            var vtRt = valueTxt.rectTransform;
            vtRt.anchorMin = new Vector2(0f, 0f);
            vtRt.anchorMax = new Vector2(1f, 1f);
            vtRt.pivot = new Vector2(0f, 0.5f);
            vtRt.offsetMin = new Vector2(52f, 0f); // Label ends at x=630; value starts at x=634.
            vtRt.offsetMax = Vector2.zero; // Value ends at x=680, before the [+] button at x=685.
            AddHoverTrigger(valGo, () => ShowStatTooltip(label), () => HideTooltip());

            // [+] Button
            var plusGo = new GameObject($"BtnPlus_{name}", typeof(RectTransform), typeof(Image), typeof(Button));
            plusGo.transform.SetParent(parent, false);
            var plusRt = plusGo.GetComponent<RectTransform>();
            plusRt.anchoredPosition = ToUI(697, py);
            plusRt.sizeDelta = new Vector2(24, 24);
            var plusImg = plusGo.GetComponent<Image>();
            plusImg.color = new Color(1f, 1f, 1f, 0.01f);
            plusImg.raycastTarget = true;
            btnPlus = plusGo.GetComponent<Button>();
            btnPlus.targetGraphic = plusImg;
            btnPlus.onClick.AddListener(onPlus);
            AddHoverHighlight(plusGo, plusImg);

            // [max] Button
            var maxGo = new GameObject($"BtnMax_{name}", typeof(RectTransform), typeof(Image), typeof(Button));
            maxGo.transform.SetParent(parent, false);
            var maxRt = maxGo.GetComponent<RectTransform>();
            maxRt.anchoredPosition = ToUI(738, py);
            maxRt.sizeDelta = new Vector2(42, 24);
            var maxImg = maxGo.GetComponent<Image>();
            maxImg.color = new Color(1f, 1f, 1f, 0.01f);
            maxImg.raycastTarget = true;
            btnMax = maxGo.GetComponent<Button>();
            btnMax.targetGraphic = maxImg;
            btnMax.onClick.AddListener(onMax);
            AddHoverHighlight(maxGo, maxImg);
        }

        private void BuildInventoryGrid(Transform parent)
        {
            float[] colXs = { 600f, 645f, 690f, 735f };
            float[] rowYs = { 262.5f, 219.5f, 176.5f, 133.5f, 90.5f, 47.5f };
            Vector2 slotSize = new Vector2(38f, 39f);

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int index = r * 4 + c;
                    var slotGo = new GameObject($"InvSlot_{index + 1}", typeof(RectTransform), typeof(Image));
                    slotGo.transform.SetParent(parent, false);
                    var rt = slotGo.GetComponent<RectTransform>();
                    rt.anchoredPosition = ToUI(colXs[c], rowYs[r]);
                    rt.sizeDelta = slotSize;

                    // Invisible click/raycast target
                    var bgImg = slotGo.GetComponent<Image>();
                    bgImg.color = new Color(1f, 1f, 1f, 0.005f);
                    bgImg.raycastTarget = true;

                    // Child: Highlight image (golden highlight + outline overlay on hover)
                    var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image), typeof(Outline));
                    hlGo.transform.SetParent(slotGo.transform, false);
                    var hlRt = hlGo.GetComponent<RectTransform>();
                    hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
                    hlRt.offsetMin = Vector2.zero; hlRt.offsetMax = Vector2.zero;
                    var hlImg = hlGo.GetComponent<Image>();
                    hlImg.color = new Color(1f, 1f, 1f, 0f);
                    hlImg.raycastTarget = false;

                    var hlOutline = hlGo.GetComponent<Outline>();
                    hlOutline.effectColor = new Color(1f, 0.88f, 0.35f, 0.95f);
                    hlOutline.effectDistance = new Vector2(1.5f, -1.5f);
                    hlOutline.enabled = false;

                    // Child: Item Icon (shown only when item is present)
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(slotGo.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = new Vector2(3, 3); iconRt.offsetMax = new Vector2(-3, -3);
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;
                    iconImg.color = new Color(1f, 1f, 1f, 0f);

                    // Child: Stack Count text at bottom-right
                    var countTxt = CreateText(slotGo.transform, "Count", "", 10, TextAlignmentOptions.BottomRight,
                        new Color(1f, 0.95f, 0.5f), FontStyles.Bold);
                    var ctRt = countTxt.rectTransform;
                    ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
                    ctRt.offsetMin = Vector2.zero;
                    ctRt.offsetMax = new Vector2(-2, 2);

                    // Ensure highlight overlay renders on top of icon and text
                    hlGo.transform.SetAsLastSibling();

                    var slotUI = slotGo.AddComponent<TheLastKnight.Inventory.InventorySlotUI>();
                    slotUI.slotType = TheLastKnight.Inventory.SlotType.Inventory;
                    slotUI.slotIndex = index;
                    slotUI.iconImage = iconImg;
                    slotUI.countText = countTxt;
                    slotUI.highlightImage = hlImg;
                    slotUI.highlightOutline = hlOutline;

                    _gridSlotUIs.Add(slotUI);
                }
            }
        }

        private void BuildTooltipBox(Transform parent)
        {
            _tooltipBox = new GameObject("Tooltip_Box", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _tooltipBox.transform.SetParent(parent, false);
            _tooltipBox.transform.SetAsLastSibling();

            var cg = _tooltipBox.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var rt = _tooltipBox.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(240f, 0f);

            var img = _tooltipBox.GetComponent<Image>();
            img.color = new Color(0.06f, 0.045f, 0.035f, 0.96f);
            img.raycastTarget = false;

            var outline = _tooltipBox.GetComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.52f, 0.28f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var vlg = _tooltipBox.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 3;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = _tooltipBox.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _txtTooltipTitle = CreateText(_tooltipBox.transform, "Title", "", 13, TextAlignmentOptions.TopLeft,
                new Color(1f, 0.88f, 0.45f), FontStyles.Bold);

            _txtTooltipSubtitle = CreateText(_tooltipBox.transform, "Subtitle", "", 10.5f, TextAlignmentOptions.TopLeft,
                new Color(0.55f, 0.82f, 1f), FontStyles.Italic);

            // Separator Line
            var sepGo = new GameObject("Separator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sepGo.transform.SetParent(_tooltipBox.transform, false);
            var sepImg = sepGo.GetComponent<Image>();
            sepImg.color = new Color(0.5f, 0.4f, 0.25f, 0.5f);
            sepImg.raycastTarget = false;
            var sepLe = sepGo.GetComponent<LayoutElement>();
            sepLe.minHeight = 1f;
            sepLe.preferredHeight = 1f;

            _txtTooltipDesc = CreateText(_tooltipBox.transform, "Desc", "", 11, TextAlignmentOptions.TopLeft,
                new Color(0.92f, 0.92f, 0.92f), FontStyles.Normal);

            _txtTooltipHint = CreateText(_tooltipBox.transform, "Hint", "", 9.5f, TextAlignmentOptions.TopLeft,
                new Color(0.85f, 0.85f, 0.85f), FontStyles.Normal);

            _tooltipBox.SetActive(false);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
            TextAlignmentOptions align, Color color, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            TheLastKnight.UI.LocalizedText.Set(tmp, text);
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private void AddHoverHighlight(GameObject target, Image img, float normalAlpha = 0.01f, float hoverAlpha = 0.25f)
        {
            var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((d) => { img.color = new Color(1f, 0.85f, 0.4f, hoverAlpha); });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((d) => { img.color = new Color(1f, 1f, 1f, normalAlpha); });
            trigger.triggers.Add(exit);
        }

        private void AddHoverTrigger(GameObject target, Action onEnter, Action onExit)
        {
            var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((d) => onEnter?.Invoke());
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((d) => onExit?.Invoke());
            trigger.triggers.Add(exit);
        }
        #endregion

        #region Tooltip
        private void ShowSkillTooltip(int index)
        {
            var slot = _skillSlots[index];
            var player = GetPlayer();
            bool unlocked = player != null && player.Level >= slot.unlockLevel;
            string hint = $"Unlocks at Level {slot.unlockLevel}\n" + (unlocked ? "Unlocked" : "Locked");
            ShowTooltip(slot.name, "Arthur's Combat Skill", slot.description, hint);
        }

        public void ShowTooltip(string title, string subtitle, string description, string hint = null)
        {
            if (_tooltipBox == null) return;

            bool changed = false;
            string newTitle = title ?? "";
            if (_txtTooltipTitle != null && _txtTooltipTitle.text != newTitle)
            {
                TheLastKnight.UI.LocalizedText.Set(_txtTooltipTitle, newTitle);
                changed = true;
            }

            if (_txtTooltipSubtitle != null)
            {
                bool hasSub = !string.IsNullOrEmpty(subtitle);
                if (_txtTooltipSubtitle.gameObject.activeSelf != hasSub) { _txtTooltipSubtitle.gameObject.SetActive(hasSub); changed = true; }
                if (hasSub && _txtTooltipSubtitle.text != subtitle) { TheLastKnight.UI.LocalizedText.Set(_txtTooltipSubtitle, subtitle); changed = true; }
            }

            if (_txtTooltipDesc != null)
            {
                bool hasDesc = !string.IsNullOrEmpty(description);
                if (_txtTooltipDesc.gameObject.activeSelf != hasDesc) { _txtTooltipDesc.gameObject.SetActive(hasDesc); changed = true; }
                if (hasDesc && _txtTooltipDesc.text != description) { TheLastKnight.UI.LocalizedText.Set(_txtTooltipDesc, description); changed = true; }
            }

            if (_txtTooltipHint != null)
            {
                bool hasHint = !string.IsNullOrEmpty(hint);
                if (_txtTooltipHint.gameObject.activeSelf != hasHint) { _txtTooltipHint.gameObject.SetActive(hasHint); changed = true; }
                if (hasHint && _txtTooltipHint.text != hint) { TheLastKnight.UI.LocalizedText.Set(_txtTooltipHint, hint); changed = true; }
            }

            if (!_tooltipBox.activeSelf)
            {
                _tooltipBox.SetActive(true);
                changed = true;
            }
            _tooltipBox.transform.SetAsLastSibling();

            if (changed)
            {
                var rt = _tooltipBox.GetComponent<RectTransform>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            UpdateTooltipPosition();
        }

        public void HideTooltip()
        {
            _currentHoveredStat = null;
            _currentHoveredPrefix = null;
            if (_tooltipBox != null && _tooltipBox.activeSelf)
            {
                _tooltipBox.SetActive(false);
            }
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipBox == null || !_tooltipBox.activeSelf || _canvasObject == null) return;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)UnityEngine.Input.mousePosition;
            var canvasRt = _canvasObject.GetComponent<RectTransform>();
            if (canvasRt == null) return;

            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, mousePos, camera, out Vector2 localPoint))
            {
                var tooltipRt = _tooltipBox.GetComponent<RectTransform>();
                float tipW = tooltipRt.rect.width > 0 ? tooltipRt.rect.width : 240f;
                float tipH = tooltipRt.rect.height > 0 ? tooltipRt.rect.height : 100f;

                float canvasHalfW = canvasRt.rect.width * 0.5f;
                float canvasHalfH = canvasRt.rect.height * 0.5f;

                float posX = localPoint.x + 16f;
                float posY = localPoint.y - 12f;

                // If spilling past right edge, flip to left of cursor
                if (posX + tipW > canvasHalfW - 10f)
                {
                    posX = localPoint.x - tipW - 12f;
                }

                // If spilling past bottom edge, flip above cursor
                if (posY - tipH < -canvasHalfH + 10f)
                {
                    posY = localPoint.y + tipH + 12f;
                }

                posX = Mathf.Clamp(posX, canvasRt.rect.xMin + 10f, Mathf.Max(canvasRt.rect.xMin + 10f, canvasRt.rect.xMax - tipW - 10f));
                posY = Mathf.Clamp(posY, Mathf.Min(canvasRt.rect.yMax - 10f, canvasRt.rect.yMin + tipH + 10f), canvasRt.rect.yMax - 10f);
                tooltipRt.anchoredPosition = new Vector2(posX, posY);
            }
        }
        #endregion

        #region Actions & System Binding
        public void UpgradeStat(string statName)
        {
            UpgradeStat(statName, 1);
        }

        public void UpgradeStat(string statName, int count)
        {
            var player = GetPlayer();
            if (player == null) return;

            if (player.StatPoints <= 0)
            {
                ShowTooltip("No Status Points", "Notice", "You need available Status Points (SP) to upgrade attributes. Level up or consume Golden Seeds to gain points.");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            if ((statName == "DEX" || statName == "DEXTERITY") && player.DEX >= TheLastKnight.Stats.PlayerStats.MaxDexterity)
            {
                ShowTooltip("DEX Maxed Out", "Notice", $"Dexterity is already at the maximum limit ({TheLastKnight.Stats.PlayerStats.MaxDexterity})!");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            int allocated = player.UpgradeStatAmount(statName, count);
            if (allocated > 0)
            {
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
                ShowStatTooltip(statName, $"+{allocated} to {statName}! Remaining SP: {player.StatPoints}");
            }
        }

        public void UpgradeStatMax(string statName)
        {
            var player = GetPlayer();
            if (player == null) return;

            if (player.StatPoints <= 0)
            {
                ShowTooltip("No Status Points", "Notice", "You need available Status Points (SP) to upgrade attributes. Level up or consume Golden Seeds to gain points.");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            if ((statName == "DEX" || statName == "DEXTERITY") && player.DEX >= TheLastKnight.Stats.PlayerStats.MaxDexterity)
            {
                ShowTooltip("DEX Maxed Out", "Notice", $"Dexterity is already at the maximum limit ({TheLastKnight.Stats.PlayerStats.MaxDexterity})!");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            int allocated = player.UpgradeStatAmount(statName, player.StatPoints);
            if (allocated > 0)
            {
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
                ShowStatTooltip(statName, $"Allocated +{allocated} points to {statName}! Remaining SP: {player.StatPoints}");
            }
        }

        public void ApplyCustomStat(string statName)
        {
            if (_customStatInputs.TryGetValue(statName, out var inputField))
            {
                string text = inputField != null ? inputField.text.Trim() : "";
                if (int.TryParse(text, out int amount) && amount > 0)
                {
                    UpgradeStat(statName, amount);
                }
                else
                {
                    UpgradeStat(statName, 1);
                }
            }
        }

        public static string FormatStatNumber(float value)
        {
            return (Mathf.Abs(value - Mathf.Round(value)) < 0.05f)
                ? $"{Mathf.RoundToInt(value)}"
                : $"{value:F1}";
        }

        public static string FormatStatWithBonus(float baseValue, float totalValue)
        {
            return FormatStatWithBonus(baseValue, totalValue, null);
        }

        private static string FormatStatWithBonus(float baseValue, float totalValue, string numberFormat)
        {
            float diff = totalValue - baseValue;
            string baseStr = numberFormat == null ? FormatStatNumber(baseValue) : baseValue.ToString(numberFormat);
            float threshold = numberFormat == "F2" ? 0.005f : 0.05f;
            string bonusStr = numberFormat == null ? FormatStatNumber(diff) : diff.ToString(numberFormat);
            if (diff > threshold)
            {
                return $"{baseStr}(+{bonusStr})";
            }
            else if (diff < -threshold)
            {
                return $"{baseStr}({bonusStr})";
            }
            return baseStr;
        }

        private void ShowStatTooltip(string statName, string prefix = null)
        {
            _currentHoveredStat = statName;
            _currentHoveredPrefix = prefix;
            var player = GetPlayer();
            string desc = "";
            switch (statName.ToUpper())
            {
                case "STR":
                    float baseAtk = player != null ? player.BaseAttackPower : 15f;
                    float totalAtk = player != null ? player.AttackPower : 15f;
                    desc = $"ATK: {FormatStatWithBonus(baseAtk, totalAtk)}\nIncreases physical attack power.";
                    break;
                case "AGI":
                    var ctrl = player != null ? player.GetComponent<Player.PlayerController>() : null;
                    float baseMoveSpd = ctrl != null ? ctrl.BaseMoveSpeed : 8f;
                    float sprintSpd = ctrl != null ? ctrl.SprintSpeed : 13f;
                    float atkSpd = player != null ? player.AttackSpeedMultiplier : 1f;
                    float permanentAtkSpd = player != null ? player.BaseAttackSpeedMultiplier : 1f;
                    float moveSpd = ctrl != null ? ctrl.MoveSpeed : baseMoveSpd;
                    float permanentSprintSpd = player != null ? player.BaseSprintSpeed : sprintSpd;
                    bool canDoubleJump = player != null && player.CanDoubleJump;
                    string djStatus = canDoubleJump ? "<color=green>Unlocked</color>" : "Locked";
                    desc = $"Attack Speed: {FormatStatWithBonus(permanentAtkSpd, atkSpd, "F2")}x\nMove Speed: {FormatStatWithBonus(baseMoveSpd, moveSpd)} (Fixed)";
                    if (ctrl != null) desc += $"\nSprint Speed: {FormatStatWithBonus(permanentSprintSpd, sprintSpd)}\nDash Speed: {ctrl.DashSpeed:F1}";
                    desc += $"\nDouble Jump: {djStatus}";
                    break;
                case "VIT":
                    float maxHp = player != null ? player.MaxHP : 100f;
                    float permanentMaxHp = player != null ? player.BaseMaxHP : maxHp;
                    float maxStm = player != null ? player.MaxStamina : 100f;
                    desc = $"Max HP: {FormatStatWithBonus(permanentMaxHp, maxHp)}\nMax Stamina: {Mathf.CeilToInt(maxStm)}\nIncreases health and stamina capacity.";
                    break;
                case "DEX":
                    float crit = player != null ? player.CriticalChance : 5f;
                    int dexVal = player != null ? player.DEX : 10;
                    string maxNotice = dexVal >= TheLastKnight.Stats.PlayerStats.MaxDexterity ? " (MAX)" : "";
                    float dexCritDamage = player != null ? player.DexCriticalDamageMultiplier * 100f : 150f;
                    float parryCritDamage = player != null ? player.ParryCriticalDamageMultiplier * 100f : 200f;
                    desc = $"Critical Chance: {crit:F1}%{maxNotice}\n(Linear: 200 DEX = 100% Crit)\nDEX Crit Damage: {FormatStatWithBonus(150f, dexCritDamage)}%\nParry Crit Damage: {FormatStatWithBonus(200f, parryCritDamage)}%";
                    break;
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                desc = $"{prefix}\n\n{desc}";
            }

            ShowTooltip($"{statName.ToUpper()} Attribute", "Player Status", desc);
        }



        public void Refresh(bool fullSync = true)
        {
            var player = GetPlayer();

            int level = player != null ? player.Level : 1;
            foreach (var slot in _skillSlots)
                slot.lockOverlay.SetActive(level < slot.unlockLevel);
            float expPct = player != null ? player.EXPPercentage : 0f;
            int exp = player != null ? player.EXP : 0;
            int expNeeded = player != null ? player.EXPNeeded : 100;
            float hpPct = (player != null && player.MaxHP > 0) ? player.HealthPercentage : 0.67f;
            int curHp = (player != null && player.MaxHP > 0) ? Mathf.CeilToInt(player.CurrentHP) : 100;
            int maxHp = (player != null && player.MaxHP > 0) ? Mathf.CeilToInt(player.MaxHP) : 150;
            string maxHpDisplay = player != null && player.MaxHP > 0
                ? FormatStatWithBonus(player.BaseMaxHP, player.MaxHP)
                : maxHp.ToString();
            float stmPct = (player != null && player.MaxStamina > 0) ? player.StaminaPercentage : 0.3f;
            int curStm = (player != null && player.MaxStamina > 0) ? Mathf.CeilToInt(player.CurrentStamina) : 30;
            int maxStm = (player != null && player.MaxStamina > 0) ? Mathf.CeilToInt(player.MaxStamina) : 100;
            int gold = player != null ? player.Gold : 100000;
            int statPoints = player != null ? player.StatPoints : 0;
            int str = (player != null && player.STR > 0) ? player.STR : 12;
            int agi = (player != null && player.AGI > 0) ? player.AGI : 9;
            int vit = (player != null && player.VIT > 0) ? player.VIT : 10;
            int dex = (player != null && player.DEX > 0) ? player.DEX : 8;
            int potions = player != null ? player.HealingPotions : 3;

            // Header
            if (_txtLevel != null) TheLastKnight.UI.LocalizedText.Set(_txtLevel, $"LV.{level}");

            // Bars
            if (_imgXpFill != null) _imgXpFill.fillAmount = expPct;
            if (_txtXp != null) TheLastKnight.UI.LocalizedText.Set(_txtXp, $"XP: {exp}/{expNeeded}");

            if (_imgHpFill != null) _imgHpFill.fillAmount = hpPct;
            if (_txtHp != null) TheLastKnight.UI.LocalizedText.Set(_txtHp, $"HP: {curHp}/{maxHpDisplay}");

            if (_imgStmFill != null) _imgStmFill.fillAmount = stmPct;
            if (_txtStm != null) TheLastKnight.UI.LocalizedText.Set(_txtStm, $"STM: {curStm}/{maxStm}");

            // Gold
            if (_txtGold != null) TheLastKnight.UI.LocalizedText.Set(_txtGold, $"{gold:N0}");

            // Status Points
            if (_txtStatusPoints != null) TheLastKnight.UI.LocalizedText.Set(_txtStatusPoints, statPoints.ToString());

            // Attributes
            if (_txtStrValue != null) TheLastKnight.UI.LocalizedText.Set(_txtStrValue, str.ToString());
            if (_txtAgiValue != null) TheLastKnight.UI.LocalizedText.Set(_txtAgiValue, agi.ToString());
            if (_txtVitValue != null) TheLastKnight.UI.LocalizedText.Set(_txtVitValue, vit.ToString());
            if (_txtDexValue != null) TheLastKnight.UI.LocalizedText.Set(_txtDexValue, dex.ToString());

            // Keep upgrade buttons always interactable for responsive hover & click feedback
            if (_btnStrPlus != null) _btnStrPlus.interactable = true;
            if (_btnStrMax != null) _btnStrMax.interactable = true;
            if (_btnAgiPlus != null) _btnAgiPlus.interactable = true;
            if (_btnAgiMax != null) _btnAgiMax.interactable = true;
            if (_btnVitPlus != null) _btnVitPlus.interactable = true;
            if (_btnVitMax != null) _btnVitMax.interactable = true;
            if (_btnDexPlus != null) _btnDexPlus.interactable = true;
            if (_btnDexMax != null) _btnDexMax.interactable = true;

            // Refresh side panel buttons interactability
            for (int i = 0; i < _sidePanelButtons.Count; i++)
            {
                if (_sidePanelButtons[i] != null)
                {
                    _sidePanelButtons[i].interactable = true;
                }
            }

            // Refresh Inventory & Quick Slots from InventoryManager
            var inv = TheLastKnight.Inventory.InventoryManager.Instance;
            if (inv != null)
            {
                for (int i = 0; i < _gridSlotUIs.Count; i++)
                {
                    var item = inv.GetSlot(TheLastKnight.Inventory.SlotType.Inventory, i);
                    _gridSlotUIs[i].UpdateDisplay(item);
                }

                for (int i = 0; i < _quickSlotUIs.Count; i++)
                {
                    var item = inv.GetSlot(TheLastKnight.Inventory.SlotType.QuickSlot, i);
                    _quickSlotUIs[i].UpdateDisplay(item);
                }
            }

            // Refresh Active Buffs on the left side of the wooden frame
            RefreshBuffDock(player);

            if (_isOpen && _currentHoveredStat != null && _tooltipBox != null && _tooltipBox.activeSelf)
            {
                ShowStatTooltip(_currentHoveredStat, _currentHoveredPrefix);
            }
        }

        public void AddInventoryItem(TheLastKnight.Inventory.InventoryItemData newItem)
        {
            TheLastKnight.Inventory.InventoryManager.Instance?.AddItem(newItem);
            Refresh(false);
        }

        public void ClearInventory()
        {
            if (TheLastKnight.Inventory.InventoryManager.Instance != null)
            {
                for (int i = 0; i < TheLastKnight.Inventory.InventoryManager.InventorySlotCount; i++)
                {
                    TheLastKnight.Inventory.InventoryManager.Instance.SetSlot(TheLastKnight.Inventory.SlotType.Inventory, i, null);
                }
            }
            Refresh(false);
        }
        #endregion

        #region Side Status Allocation Drawer
        private void BuildSideStatusDrawer(RectTransform parent)
        {
            float drawerW = 210f;
            float drawerH = 160f;
            float drawerY = 104f;

            // 1. Side Panel Container (Docked to right edge of wooden frame)
            _sidePanelGo = new GameObject("SideStatusDrawer", typeof(RectTransform), typeof(Image));
            _sidePanelGo.transform.SetParent(parent, false);

            _sidePanelRect = _sidePanelGo.GetComponent<RectTransform>();
            _sidePanelRect.anchorMin = new Vector2(1f, 0.5f);
            _sidePanelRect.anchorMax = new Vector2(1f, 0.5f);
            _sidePanelRect.pivot = new Vector2(0f, 0.5f);
            _sidePanelRect.anchoredPosition = new Vector2(-4f, drawerY);
            _sidePanelRect.sizeDelta = new Vector2(drawerW, drawerH);
            _sidePanelRect.localScale = new Vector3(1.75f, 1.75f, 1f);

            // Rich Medieval Wood panel background
            var panelImg = _sidePanelGo.GetComponent<Image>();
            panelImg.color = new Color(0.12f, 0.08f, 0.05f, 0.98f);

            // Outer dark carved border
            var panelOutline = _sidePanelGo.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.32f, 0.20f, 0.11f, 0.95f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // Inner ornate golden rim
            var innerBorderGo = new GameObject("InnerGoldBorder", typeof(RectTransform), typeof(Image), typeof(Outline));
            innerBorderGo.transform.SetParent(_sidePanelGo.transform, false);
            var ibRt = innerBorderGo.GetComponent<RectTransform>();
            ibRt.anchorMin = Vector2.zero; ibRt.anchorMax = Vector2.one;
            ibRt.offsetMin = new Vector2(2, 2); ibRt.offsetMax = new Vector2(-2, -2);
            var ibImg = innerBorderGo.GetComponent<Image>();
            ibImg.color = new Color(0.16f, 0.10f, 0.06f, 0.6f);
            ibImg.raycastTarget = false;
            var ibOutline = innerBorderGo.GetComponent<Outline>();
            ibOutline.effectColor = new Color(0.68f, 0.52f, 0.24f, 0.8f);
            ibOutline.effectDistance = new Vector2(1f, -1f);

            // Header Banner
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerGo.transform.SetParent(_sidePanelGo.transform, false);
            var hRt = headerGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(0f, 24f);
            var hImg = headerGo.GetComponent<Image>();
            hImg.color = new Color(0.18f, 0.11f, 0.06f, 0.9f);
            hImg.raycastTarget = false;

            var txtHeader = CreateText(headerGo.transform, "TxtTitle", "« STAT ALLOCATION »", 10.5f,
                TextAlignmentOptions.Center, new Color(0.96f, 0.86f, 0.55f), FontStyles.Bold);
            var thRt = txtHeader.rectTransform;
            thRt.anchorMin = Vector2.zero; thRt.anchorMax = Vector2.one;
            thRt.offsetMin = thRt.offsetMax = Vector2.zero;

            // Separator line under header
            var sepGo = new GameObject("HeaderSep", typeof(RectTransform), typeof(Image));
            sepGo.transform.SetParent(_sidePanelGo.transform, false);
            var sepRt = sepGo.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 1f); sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot = new Vector2(0.5f, 1f);
            sepRt.anchoredPosition = new Vector2(0f, -24f);
            sepRt.sizeDelta = new Vector2(-12f, 1.5f);
            var sepImg = sepGo.GetComponent<Image>();
            sepImg.color = new Color(0.65f, 0.50f, 0.25f, 0.7f);
            sepImg.raycastTarget = false;

            // 2. The 4 Stat Rows (STR, AGI, VIT, DEX)
            // Exactly matching the main window's visual button positions
            string[] stats = { "STR", "AGI", "VIT", "DEX" };
            float[] rowYs = { 42.5f, 13.5f, -14.5f, -42.5f };

            _customStatInputs.Clear();
            _sidePanelButtons.Clear();
            for (int i = 0; i < stats.Length; i++)
            {
                string stat = stats[i];
                float y = rowYs[i];
                BuildSideStatRow(_sidePanelGo.transform, stat, y);
            }

            // 3. Side Toggle Tab Button (Attached to right edge)
            var toggleGo = new GameObject("Btn_SideToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleGo.transform.SetParent(parent, false);
            toggleGo.transform.SetAsLastSibling();

            _sideToggleBtnRect = toggleGo.GetComponent<RectTransform>();
            _sideToggleBtnRect.anchorMin = new Vector2(1f, 0.5f);
            _sideToggleBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _sideToggleBtnRect.pivot = new Vector2(0f, 0.5f);
            _sideToggleBtnRect.sizeDelta = new Vector2(24f, 54f);
            _sideToggleBtnRect.localScale = new Vector3(1.75f, 1.75f, 1f);

            _imgSideToggleBg = toggleGo.GetComponent<Image>();
            _imgSideToggleBg.color = new Color(0.18f, 0.11f, 0.07f, 0.98f);

            var toggleOutline = toggleGo.AddComponent<Outline>();
            toggleOutline.effectColor = new Color(0.72f, 0.54f, 0.24f, 0.95f);
            toggleOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var toggleBtn = toggleGo.GetComponent<Button>();
            toggleBtn.targetGraphic = _imgSideToggleBg;
            toggleBtn.onClick.AddListener(ToggleSidePanel);

            _txtSideToggleArrow = CreateText(toggleGo.transform, "Arrow", "»", 16f,
                TextAlignmentOptions.Center, new Color(0.98f, 0.88f, 0.45f), FontStyles.Bold);
            var taRt = _txtSideToggleArrow.rectTransform;
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = taRt.offsetMax = Vector2.zero;

            AddHoverTrigger(toggleGo,
                () => ShowTooltip(_isSidePanelOpen ? "Collapse Side Panel" : "Expand Status Controls",
                    "Status Points",
                    _isSidePanelOpen ? "Click to fold the status upgrade drawer." : "Click to expand multi-point (+1, +10, Custom, Max) allocation."),
                HideTooltip);

            AddMedievalButtonHover(toggleGo, _imgSideToggleBg, toggleOutline,
                new Color(0.72f, 0.54f, 0.24f, 0.95f), new Color(1f, 0.88f, 0.40f, 1f));

            // Default state: Closed (matching image 1)
            SetSidePanelOpen(false);
        }

        private void BuildSideStatRow(Transform parent, string statName, float localY)
        {
            var rowGo = new GameObject($"Row_{statName}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, localY);
            rowRt.sizeDelta = new Vector2(0f, 22f);

            float curX = 7f;

            // 1. Stat Badge
            var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(Outline));
            badgeGo.transform.SetParent(rowGo.transform, false);
            var bRt = badgeGo.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f); bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(curX, 0f);
            bRt.sizeDelta = new Vector2(28f, 20f);
            badgeGo.GetComponent<Image>().color = new Color(0.14f, 0.09f, 0.06f, 0.98f);
            var bOutline = badgeGo.GetComponent<Outline>();
            bOutline.effectColor = new Color(0.55f, 0.38f, 0.22f, 0.9f);
            bOutline.effectDistance = new Vector2(1f, -1f);
            var bTxt = CreateText(badgeGo.transform, "Label", statName, 10f, TextAlignmentOptions.Center,
                new Color(0.94f, 0.85f, 0.70f), FontStyles.Bold);
            bTxt.rectTransform.anchorMin = Vector2.zero; bTxt.rectTransform.anchorMax = Vector2.one;
            bTxt.rectTransform.offsetMin = bTxt.rectTransform.offsetMax = Vector2.zero;
            AddHoverTrigger(badgeGo, () => ShowStatTooltip(statName), HideTooltip);

            curX += 28f + 3f;

            // 2. [+1] Button
            CreateSideButton(rowGo.transform, "Btn_Plus1", "+1", new Vector2(curX, 0f), new Vector2(28f, 20f),
                new Color(0.92f, 0.84f, 0.70f), () => UpgradeStat(statName, 1),
                $"Add +1 to {statName}", $"Allocates 1 Status Point to {statName}.");
            curX += 28f + 3f;

            // 3. [+10] Button
            CreateSideButton(rowGo.transform, "Btn_Plus10", "+10", new Vector2(curX, 0f), new Vector2(32f, 20f),
                new Color(0.98f, 0.88f, 0.55f), () => UpgradeStat(statName, 10),
                $"Add +10 to {statName}", $"Allocates 10 Status Points (or remaining SP) to {statName}.");
            curX += 32f + 3f;

            // 4. Custom Input Field [_]
            var input = CreateInputField(rowGo.transform, $"Input_{statName}", new Vector2(curX, 0f), new Vector2(32f, 20f), "_");
            _customStatInputs[statName] = input;
            input.onSubmit.AddListener((val) => ApplyCustomStat(statName));
            AddHoverTrigger(input.gameObject,
                () => ShowTooltip($"Custom Points: {statName}", "Manual Allocation", "Type number of points to allocate, then press Enter or click [+]."),
                HideTooltip);
            curX += 32f + 2f;

            // 5. Companion [+] Apply Button
            CreateSideButton(rowGo.transform, "Btn_Apply", "+", new Vector2(curX, 0f), new Vector2(18f, 20f),
                new Color(1f, 0.92f, 0.60f), () => ApplyCustomStat(statName),
                $"Apply to {statName}", "Allocates the typed number of points to this attribute.");
            curX += 18f + 3f;

            // 6. [MAX] Button
            CreateSideButton(rowGo.transform, "Btn_Max", "MAX", new Vector2(curX, 0f), new Vector2(38f, 20f),
                new Color(1f, 0.85f, 0.30f), () => UpgradeStatMax(statName),
                $"Max Out {statName}", $"Allocates ALL available Status Points to {statName} attribute.");
        }

        private Button CreateSideButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            Color textColor, UnityEngine.Events.UnityAction onClick, string tipTitle, string tipDesc)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.20f, 0.13f, 0.08f, 0.95f);

            var outline = btnGo.GetComponent<Outline>();
            Color normalBorder = new Color(0.55f, 0.40f, 0.20f, 0.85f);
            Color hoverBorder = new Color(1f, 0.88f, 0.40f, 1f);
            outline.effectColor = normalBorder;
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var txt = CreateText(btnGo.transform, "Label", label, 10f, TextAlignmentOptions.Center, textColor, FontStyles.Bold);
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;

            AddHoverTrigger(btnGo, () => ShowTooltip(tipTitle, "Status Points", tipDesc), HideTooltip);
            AddMedievalButtonHover(btnGo, img, outline, normalBorder, hoverBorder);

            _sidePanelButtons.Add(btn);
            return btn;
        }

        private TMP_InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size, string placeholderText)
        {
            var rootGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(TMP_InputField));
            rootGo.transform.SetParent(parent, false);
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 0.5f); rootRt.anchorMax = new Vector2(0f, 0.5f);
            rootRt.pivot = new Vector2(0f, 0.5f);
            rootRt.anchoredPosition = pos;
            rootRt.sizeDelta = size;

            var bgImg = rootGo.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.05f, 0.03f, 0.98f);

            var outline = rootGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.35f, 0.20f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);

            var textAreaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textAreaGo.transform.SetParent(rootGo.transform, false);
            var textAreaRt = textAreaGo.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero; textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(2, 1); textAreaRt.offsetMax = new Vector2(-2, -1);

            var phTxt = CreateText(textAreaGo.transform, "Placeholder", placeholderText, 10f,
                TextAlignmentOptions.Center, new Color(0.55f, 0.45f, 0.35f, 0.75f), FontStyles.Italic);
            phTxt.rectTransform.anchorMin = Vector2.zero; phTxt.rectTransform.anchorMax = Vector2.one;
            phTxt.rectTransform.offsetMin = phTxt.rectTransform.offsetMax = Vector2.zero;

            var textTxt = CreateText(textAreaGo.transform, "Text", "", 10f,
                TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.60f), FontStyles.Bold);
            textTxt.rectTransform.anchorMin = Vector2.zero; textTxt.rectTransform.anchorMax = Vector2.one;
            textTxt.rectTransform.offsetMin = textTxt.rectTransform.offsetMax = Vector2.zero;

            var inputField = rootGo.GetComponent<TMP_InputField>();
            inputField.textViewport = textAreaRt;
            inputField.textComponent = textTxt;
            inputField.placeholder = phTxt;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.characterLimit = 4;
            inputField.targetGraphic = bgImg;

            return inputField;
        }

        private void AddMedievalButtonHover(GameObject target, Image img, Outline outline, Color normalBorder, Color hoverBorder)
        {
            var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            Color normalBg = img.color;
            Color hoverBg = new Color(Mathf.Min(1f, normalBg.r * 1.35f), Mathf.Min(1f, normalBg.g * 1.35f), Mathf.Min(1f, normalBg.b * 1.35f), normalBg.a);

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((d) =>
            {
                img.color = hoverBg;
                if (outline != null) outline.effectColor = hoverBorder;
            });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((d) =>
            {
                img.color = normalBg;
                if (outline != null) outline.effectColor = normalBorder;
            });
            trigger.triggers.Add(exit);
        }

        public void ToggleSidePanel()
        {
            SetSidePanelOpen(!_isSidePanelOpen);
        }

        public void SetSidePanelOpen(bool open)
        {
            _isSidePanelOpen = open;
            if (_sidePanelGo != null)
            {
                _sidePanelGo.SetActive(open);
            }

            if (_sideToggleBtnRect != null)
            {
                // When open, tab sits on outer right edge of the 1.75x drawer (210f * 1.75f - 4f = 363.5f)
                float posX = open ? (210f * 1.75f - 4f) : -4f;
                _sideToggleBtnRect.anchoredPosition = new Vector2(posX, 104f);
            }

            if (_txtSideToggleArrow != null)
            {
                TheLastKnight.UI.LocalizedText.Set(_txtSideToggleArrow, open ? "«" : "»");
            }

            if (_windowRect != null)
            {
                _windowRect.anchoredPosition = open ? new Vector2(-160f, 0f) : Vector2.zero;
            }

            AudioManager.Instance?.PlaySfx("click");
        }
        #endregion

        #region Left Buff Dock (Minecraft-style status effects docked to wooden frame, expanding leftwards)
        private void BuildLeftBuffDock(RectTransform windowRect)
        {
            var dockGo = new GameObject("BuffDock_Left", typeof(RectTransform), typeof(VerticalLayoutGroup));
            dockGo.transform.SetParent(windowRect, false);

            _leftBuffDock = dockGo.GetComponent<RectTransform>();
            // Docked to the left edge of the wooden window frame!
            _leftBuffDock.anchorMin = new Vector2(0f, 1f); // Top-left of wooden frame
            _leftBuffDock.anchorMax = new Vector2(0f, 1f);
            _leftBuffDock.pivot = new Vector2(1f, 1f); // Right-aligned to frame's left edge
            _leftBuffDock.anchoredPosition = new Vector2(-10f, -16f); // 10px to the left of the wooden frame
            _leftBuffDock.sizeDelta = new Vector2(150f, 400f);

            var vlg = dockGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperRight; // Cards grow outwards to the left!
        }

        private BuffCardUI CreateStatusBuffCard(int index)
        {
            var cardGo = new GameObject($"BuffCard_{index}", typeof(RectTransform), typeof(Image), typeof(Outline));
            cardGo.transform.SetParent(_leftBuffDock, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.pivot = new Vector2(1f, 0.5f); // Anchored on right, expands outwards to left
            cardRt.sizeDelta = new Vector2(146f, 38f);

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.07f, 0.08f, 0.12f, 0.92f);
            bgImg.raycastTarget = true; // Enables hover for tooltip

            var outline = cardGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.52f, 0.28f, 0.85f); // Warm gold border matching wooden frame
            outline.effectDistance = new Vector2(1.2f, 1.2f);

            // Icon Socket
            var socketGo = new GameObject("IconSocket", typeof(RectTransform), typeof(Image));
            socketGo.transform.SetParent(cardGo.transform, false);
            var socketRt = socketGo.GetComponent<RectTransform>();
            socketRt.anchorMin = new Vector2(0f, 0.5f);
            socketRt.anchorMax = new Vector2(0f, 0.5f);
            socketRt.pivot = new Vector2(0f, 0.5f);
            socketRt.anchoredPosition = new Vector2(4f, 0f);
            socketRt.sizeDelta = new Vector2(30f, 30f);

            var socketImg = socketGo.GetComponent<Image>();
            socketImg.color = new Color(0.03f, 0.04f, 0.06f, 0.95f);
            socketImg.raycastTarget = false;

            // Icon Image
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(socketGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(26f, 26f);
            iconRt.anchoredPosition = Vector2.zero;

            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;

            // Name Text
            var nameGo = new GameObject("Txt_Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.anchoredPosition = new Vector2(38f, -4f);
            nameRt.sizeDelta = new Vector2(-42f, 16f);

            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = 11f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.raycastTarget = false;

            // Duration / Time Text
            var timeGo = new GameObject("Txt_Time", typeof(RectTransform), typeof(TextMeshProUGUI));
            timeGo.transform.SetParent(cardGo.transform, false);
            var timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0f, 0f);
            timeRt.anchorMax = new Vector2(1f, 0f);
            timeRt.pivot = new Vector2(0f, 0f);
            timeRt.anchoredPosition = new Vector2(38f, 3f);
            timeRt.sizeDelta = new Vector2(-42f, 14f);

            var timeText = timeGo.GetComponent<TextMeshProUGUI>();
            timeText.fontSize = 10f;
            timeText.alignment = TextAlignmentOptions.BottomLeft;
            timeText.raycastTarget = false;

            var card = new BuffCardUI
            {
                Root = cardGo,
                BgImage = bgImg,
                Outline = outline,
                IconImage = iconImg,
                NameText = nameText,
                TimeText = timeText
            };

            AddHoverTrigger(cardGo,
                () =>
                {
                    var info = card.CurrentInfo;
                    if (!string.IsNullOrEmpty(info.name))
                    {
                        string timeHint = info.remainingSeconds >= 0f
                            ? $"Time Remaining: {info.formattedTime} ({info.remainingSeconds:0.0}s)"
                            : "Continuous Aura (Sacred Area)";
                        ShowTooltip(info.name, info.category, info.description, timeHint);
                    }
                },
                HideTooltip);

            return card;
        }

        private void RefreshBuffDock(PlayerStats player)
        {
            if (_leftBuffDock == null) return;

            _cachedActiveBuffs.Clear();
            if (player != null)
            {
                player.GetActiveBuffs(_cachedActiveBuffs);
            }

            int count = _cachedActiveBuffs.Count;
            while (_statusBuffCards.Count < count)
            {
                _statusBuffCards.Add(CreateStatusBuffCard(_statusBuffCards.Count));
            }

            for (int i = 0; i < _statusBuffCards.Count; i++)
            {
                var card = _statusBuffCards[i];
                if (i < count)
                {
                    card.Root.SetActive(true);
                    card.Bind(_cachedActiveBuffs[i]);
                }
                else
                {
                    card.Root.SetActive(false);
                }
            }
        }
        #endregion

        #region Helper Classes
        public class BuffCardUI
        {
            public GameObject Root;
            public Image BgImage;
            public Outline Outline;
            public Image IconImage;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI TimeText;
            public ActiveBuffInfo CurrentInfo;

            public void Bind(ActiveBuffInfo info)
            {
                CurrentInfo = info;

                if (NameText != null)
                {
                    TheLastKnight.UI.LocalizedText.Set(NameText, info.name);
                    NameText.color = info.themeColor;
                }

                if (IconImage != null)
                {
                    IconImage.sprite = info.icon;
                    IconImage.color = info.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }

                if (TimeText != null)
                {
                    TheLastKnight.UI.LocalizedText.Set(TimeText, info.formattedTime);

                    if (info.remainingSeconds >= 0f && info.remainingSeconds <= 5f)
                    {
                        bool flash = (Mathf.FloorToInt(Time.unscaledTime * 4f) % 2) == 0;
                        TimeText.color = flash ? new Color(1f, 0.25f, 0.25f) : new Color(1f, 0.85f, 0.35f);
                    }
                    else
                    {
                        TimeText.color = new Color(0.85f, 0.85f, 0.9f);
                    }
                }

                if (Outline != null)
                {
                    Color border = info.themeColor;
                    border.a = 0.65f;
                    Outline.effectColor = border;
                }
            }
        }

        private class SkillSlotUI
        {
            public Button button;
            public Image icon;
            public string name;
            public string description;
            public GameObject lockOverlay;
            public int unlockLevel;
        }
        #endregion
    }
}
