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

        // Header Elements
        private TextMeshProUGUI _txtLevel;

        // Bars
        private Image _imgXpFill;
        private TextMeshProUGUI _txtXp;
        private Image _imgHpFill;
        private TextMeshProUGUI _txtHp;
        private Image _imgStmFill;
        private TextMeshProUGUI _txtStm;

        // Derived Combat
        private TextMeshProUGUI _txtDef;

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

        // Tooltip Elements
        private GameObject _tooltipBox;
        private TextMeshProUGUI _txtTooltipTitle;
        private TextMeshProUGUI _txtTooltipSubtitle;
        private TextMeshProUGUI _txtTooltipDesc;
        private TextMeshProUGUI _txtTooltipHint;

        // Slots
        private readonly List<TheLastKnight.Inventory.InventorySlotUI> _gridSlotUIs = new List<TheLastKnight.Inventory.InventorySlotUI>();
        private readonly List<TheLastKnight.Inventory.InventorySlotUI> _quickSlotUIs = new List<TheLastKnight.Inventory.InventorySlotUI>();
        private readonly List<SkillSlotUI> _skillSlots = new List<SkillSlotUI>();

        // Floating Cursor Follower
        private GameObject _cursorFollower;
        private Image _cursorIcon;
        private TextMeshProUGUI _cursorCount;

        private PlayerStats _cachedStats;
        private InventoryManager _boundInventory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
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
            if (transform.parent == null)
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
            if (keyboard.bKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            // Quick Testing Hotkeys (Available in Editor / Development)
            if (keyboard.f1Key != null && keyboard.f1Key.wasPressedThisFrame)
            {
                var p = GetPlayer();
                if (p != null)
                {
                    p.AddAGI(50);
                    if (_isOpen) Refresh(true);
                    Debug.Log($"<color=cyan>[Test Hotkey F1]</color> +50 AGI! Current AGI: {p.AGI}, DoubleJump: {p.CanDoubleJump}, AtkSpd: {p.AttackSpeedMultiplier:F2}x");
                }
            }
            else if (keyboard.f2Key != null && keyboard.f2Key.wasPressedThisFrame)
            {
                var p = GetPlayer();
                if (p != null)
                {
                    p.SetAGI(250);
                    if (_isOpen) Refresh(true);
                    Debug.Log($"<color=green>[Test Hotkey F2]</color> AGI set to 250! DoubleJump Unlocked: {p.CanDoubleJump}, AtkSpd: {p.AttackSpeedMultiplier:F2}x");
                }
            }
            else if (keyboard.f3Key != null && keyboard.f3Key.wasPressedThisFrame)
            {
                var p = GetPlayer();
                if (p != null)
                {
                    p.AddStatPoints(100);
                    if (_isOpen) Refresh(true);
                    Debug.Log($"<color=yellow>[Test Hotkey F3]</color> +100 SP added! Remaining SP: {p.StatPoints}");
                }
            }

            // Close with Escape if open
            if (_isOpen)
            {
                UpdateCursorFollower();
                UpdateTooltipPosition();

                // Press Q while holding an item to drop it into the world (Ctrl+Q drops whole stack, Q drops 1)
                if (keyboard.qKey.wasPressedThisFrame)
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
            AudioManager.Instance?.PlaySfx("click");
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
            AudioManager.Instance?.PlaySfx("click");
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
        }

        private void SetWindowVisible(bool visible)
        {
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(visible);
            }
            if (!visible)
            {
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
            var winSprite = Resources.Load<Sprite>("CharacterStatus/Window_Mockup");
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

            // Close Button [X] at Top-Right (built after overlays to stay topmost)
            BuildCloseButton(_windowRect);

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
                        _cursorCount.text = inv.CursorHeldItem.count > 1 ? inv.CursorHeldItem.count.ToString() : "";
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

            // 2. Bars: XP, HP, STM
            CreateStatBar(parent, "XP_Bar", ToUI(408, 335), new Vector2(230, 14),
                new Color(0.25f, 0.45f, 0.85f), out _imgXpFill, out _txtXp, "XP: 0/100");

            CreateStatBar(parent, "HP_Bar", ToUI(408, 308), new Vector2(230, 14),
                new Color(0.85f, 0.18f, 0.15f), out _imgHpFill, out _txtHp, "HP: 100/150");

            CreateStatBar(parent, "STM_Bar", ToUI(408, 281), new Vector2(230, 14),
                new Color(0.85f, 0.55f, 0.15f), out _imgStmFill, out _txtStm, "STM: 30/100");

            // DEF display (between stamina and gold)
            var defGo = new GameObject("Txt_DEF", typeof(RectTransform));
            defGo.transform.SetParent(parent, false);
            var defRt = defGo.GetComponent<RectTransform>();
            defRt.anchoredPosition = ToUI(408, 264);
            defRt.sizeDelta = new Vector2(230, 14);

            _txtDef = CreateText(defGo.transform, "Label", "DEF: 1", 11, TextAlignmentOptions.MidlineLeft,
                new Color(0.75f, 0.75f, 0.85f), FontStyles.Normal);
            var defTxtRt = _txtDef.rectTransform;
            defTxtRt.anchorMin = Vector2.zero; defTxtRt.anchorMax = Vector2.one;
            defTxtRt.offsetMin = defTxtRt.offsetMax = Vector2.zero;

            AddHoverTrigger(defGo,
                () => ShowTooltip("Defense (DEF)", "Level-Based",
                    $"Reduces incoming damage by a flat amount.\nDEF scales with Level (baseDEF + Level × defPerLevel).\nMinimum 1 damage is always taken."),
                () => HideTooltip());

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
                    ShowTooltip(skillNames[index], "Arthur's Combat Skill", skillDescs[index]);
                    AudioManager.Instance?.PlaySfx("click");
                });

                AddHoverHighlight(slotGo, img);
                AddHoverTrigger(slotGo,
                    () => ShowTooltip(skillNames[index], "Arthur's Combat Skill", skillDescs[index]),
                    HideTooltip);

                _skillSlots.Add(new SkillSlotUI { button = btn, icon = img, name = skillNames[i], description = skillDescs[i] });
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
            // 1. Status Points Number overlay (Clean text directly on wood)
            var spGo = new GameObject("Txt_SP", typeof(RectTransform));
            spGo.transform.SetParent(parent, false);
            var spRt = spGo.GetComponent<RectTransform>();
            spRt.anchoredPosition = ToUI(745, 420);
            spRt.sizeDelta = new Vector2(32, 20);

            _txtStatusPoints = CreateText(spGo.transform, "Label", "0", 17, TextAlignmentOptions.Center,
                Color.white, FontStyles.Bold);
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
            // Value Box (Clean text directly on wood)
            var valGo = new GameObject($"Val_{name}", typeof(RectTransform), typeof(Image));
            valGo.transform.SetParent(parent, false);
            var valImg = valGo.GetComponent<Image>();
            valImg.color = new Color(1f, 1f, 1f, 0.001f);
            valImg.raycastTarget = true;
            var valRt = valGo.GetComponent<RectTransform>();
            valRt.anchoredPosition = ToUI(655, py);
            valRt.sizeDelta = new Vector2(46, 22);

            valueTxt = CreateText(valGo.transform, "Label", "10", 16, TextAlignmentOptions.Center,
                Color.white, FontStyles.Bold);
            var vtRt = valueTxt.rectTransform;
            vtRt.anchorMin = Vector2.zero; vtRt.anchorMax = Vector2.one;
            vtRt.offsetMin = vtRt.offsetMax = Vector2.zero;
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
            tmp.text = text;
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
        public void ShowTooltip(string title, string subtitle, string description, string hint = null)
        {
            if (_tooltipBox == null) return;

            if (_txtTooltipTitle != null) _txtTooltipTitle.text = title ?? "";

            if (_txtTooltipSubtitle != null)
            {
                bool hasSub = !string.IsNullOrEmpty(subtitle);
                _txtTooltipSubtitle.gameObject.SetActive(hasSub);
                if (hasSub) _txtTooltipSubtitle.text = subtitle;
            }

            if (_txtTooltipDesc != null)
            {
                bool hasDesc = !string.IsNullOrEmpty(description);
                _txtTooltipDesc.gameObject.SetActive(hasDesc);
                if (hasDesc) _txtTooltipDesc.text = description;
            }

            if (_txtTooltipHint != null)
            {
                bool hasHint = !string.IsNullOrEmpty(hint);
                _txtTooltipHint.gameObject.SetActive(hasHint);
                if (hasHint) _txtTooltipHint.text = hint;
            }

            _tooltipBox.SetActive(true);
            _tooltipBox.transform.SetAsLastSibling();

            var rt = _tooltipBox.GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            UpdateTooltipPosition();
        }

        public void HideTooltip()
        {
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

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, mousePos, null, out Vector2 localPoint))
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

                tooltipRt.anchoredPosition = new Vector2(posX, posY);
            }
        }
        #endregion

        #region Actions & System Binding
        public void UpgradeStat(string statName)
        {
            var player = GetPlayer();
            if (player == null) return;

            if (player.StatPoints <= 0)
            {
                ShowTooltip("No Status Points", "Notice", "You need available Status Points (SP) to upgrade attributes. Level up or consume Golden Seeds to gain points.");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            if (player.UpgradeStat(statName))
            {
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
                ShowStatTooltip(statName, $"+1 to {statName}! Remaining SP: {player.StatPoints}");
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

            int count = 0;
            while (player.StatPoints > 0)
            {
                if (player.UpgradeStat(statName)) count++;
                else break;
            }

            if (count > 0)
            {
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
                ShowStatTooltip(statName, $"Allocated +{count} points to {statName}! Remaining SP: {player.StatPoints}");
            }
        }

        private void ShowStatTooltip(string statName, string prefix = null)
        {
            var player = GetPlayer();
            string desc = "";
            switch (statName.ToUpper())
            {
                case "STR":
                    float atk = player != null ? player.AttackPower : 15f;
                    desc = $"Increases Physical Attack Power (ATK).\nCurrent ATK: {atk:F1}";
                    break;
                case "AGI":
                    int agi = player != null ? player.AGI : 10;
                    var ctrl = player != null ? player.GetComponent<Player.PlayerController>() : null;
                    float moveSpd = ctrl != null ? ctrl.MoveSpeed : 8f;
                    float atkSpd = player != null ? player.AttackSpeedMultiplier : 1f;
                    bool canDoubleJump = player != null && player.CanDoubleJump;
                    string djStatus = canDoubleJump ? "<color=green>Unlocked</color>" : $"Unlocks at 250 AGI ({agi}/250)";
                    desc = $"Increases Attack Speed and Movement Speed.\nAttack Speed: {atkSpd:F2}x | Move Speed: {moveSpd:F1}\nDouble Jump: {djStatus}";
                    break;
                case "VIT":
                    float maxHp = player != null ? player.MaxHP : 100f;
                    float maxStm = player != null ? player.MaxStamina : 100f;
                    desc = $"Increases Maximum HP and Maximum Stamina.\nMax HP: {Mathf.CeilToInt(maxHp)} | Max Stamina: {Mathf.CeilToInt(maxStm)}";
                    break;
                case "DEX":
                    int dex = player != null ? player.DEX : 10;
                    float crit = player != null ? player.CriticalChance : 16.8f;
                    desc = $"Increases Critical Chance approaching 100% limit (99% at 250 DEX).\nCurrent Critical Rate: {crit:F2}%";
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
            float expPct = player != null ? player.EXPPercentage : 0f;
            int exp = player != null ? player.EXP : 0;
            int expNeeded = player != null ? player.EXPNeeded : 100;
            float hpPct = (player != null && player.MaxHP > 0) ? player.HealthPercentage : 0.67f;
            int curHp = (player != null && player.MaxHP > 0) ? Mathf.CeilToInt(player.CurrentHP) : 100;
            int maxHp = (player != null && player.MaxHP > 0) ? Mathf.CeilToInt(player.MaxHP) : 150;
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
            if (_txtLevel != null) _txtLevel.text = $"LV.{level}";

            // Bars
            if (_imgXpFill != null) _imgXpFill.fillAmount = expPct;
            if (_txtXp != null) _txtXp.text = $"XP: {exp}/{expNeeded}";

            if (_imgHpFill != null) _imgHpFill.fillAmount = hpPct;
            if (_txtHp != null) _txtHp.text = $"HP: {curHp}/{maxHp}";

            if (_imgStmFill != null) _imgStmFill.fillAmount = stmPct;
            if (_txtStm != null) _txtStm.text = $"STM: {curStm}/{maxStm}";

            // DEF (level-based)
            float def = player != null ? player.Defense : 1f;
            if (_txtDef != null) _txtDef.text = $"DEF: {Mathf.CeilToInt(def)}";

            // Gold
            if (_txtGold != null) _txtGold.text = $"{gold:N0}";

            // Status Points
            if (_txtStatusPoints != null) _txtStatusPoints.text = statPoints.ToString();

            // Attributes
            if (_txtStrValue != null) _txtStrValue.text = str.ToString();
            if (_txtAgiValue != null) _txtAgiValue.text = agi.ToString();
            if (_txtVitValue != null) _txtVitValue.text = vit.ToString();
            if (_txtDexValue != null) _txtDexValue.text = dex.ToString();

            // Keep upgrade buttons always interactable for responsive hover & click feedback
            if (_btnStrPlus != null) _btnStrPlus.interactable = true;
            if (_btnStrMax != null) _btnStrMax.interactable = true;
            if (_btnAgiPlus != null) _btnAgiPlus.interactable = true;
            if (_btnAgiMax != null) _btnAgiMax.interactable = true;
            if (_btnVitPlus != null) _btnVitPlus.interactable = true;
            if (_btnVitMax != null) _btnVitMax.interactable = true;
            if (_btnDexPlus != null) _btnDexPlus.interactable = true;
            if (_btnDexMax != null) _btnDexMax.interactable = true;

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

        #region Helper Classes
        private class SkillSlotUI
        {
            public Button button;
            public Image icon;
            public string name;
            public string description;
        }
        #endregion
    }
}
