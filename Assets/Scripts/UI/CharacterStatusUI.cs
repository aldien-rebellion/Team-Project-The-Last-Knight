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

        // Slots
        private readonly List<InventorySlotUI> _inventorySlots = new List<InventorySlotUI>();
        private readonly List<QuickSlotUI> _quickSlots = new List<QuickSlotUI>();
        private readonly List<SkillSlotUI> _skillSlots = new List<SkillSlotUI>();

        private PlayerStats _cachedStats;

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
            EnsureEventSystem();
            BuildUI();
            SetWindowVisible(false);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SetHUDVisible(true);
            if (Instance == this) Instance = null;
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

            // Close with Escape if open
            if (_isOpen)
            {
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
            AudioManager.Instance?.PlaySfx("click");
            SetWindowVisible(true);
            Refresh(true);
        }

        public void Close()
        {
            if (!_isOpen) return;

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
                _cachedHUD.gameObject.SetActive(visible);
            }
        }

        private void SetWindowVisible(bool visible)
        {
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(visible);
            }
        }

        private PlayerStats GetPlayer()
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

                if (UnityEngine.InputSystem.InputSystem.actions != null)
                {
                    module.actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
                }
                else
                {
                    module.AssignDefaultActions();
                }
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

            // Semi-transparent Backdrop
            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(_canvasObject.transform, false);
            var backdropRect = backdropGo.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;

            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0.015f, 0.02f, 0.035f, 0.78f);
            var backdropBtn = backdropGo.GetComponent<Button>();
            backdropBtn.onClick.AddListener(Close);

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

            // Tooltip Box (Floating overlay at bottom center)
            BuildTooltipBox(_windowRect);
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
            lvlRt.anchoredPosition = ToUI(295, 362);
            lvlRt.sizeDelta = new Vector2(86, 24);

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

            // 3. Dynamic Gold Display
            var goldGo = new GameObject("Txt_Gold", typeof(RectTransform));
            goldGo.transform.SetParent(parent, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            goldRt.anchoredPosition = ToUI(435, 244);
            goldRt.sizeDelta = new Vector2(160, 20);

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
            float[] skillXs = { 325f, 415f, 505f };

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var slotGo = new GameObject($"SkillSlot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotGo.transform.SetParent(parent, false);
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchoredPosition = ToUI(skillXs[i], 158);
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
                var qGo = new GameObject($"QuickSlot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                qGo.transform.SetParent(parent, false);
                var rt = qGo.GetComponent<RectTransform>();
                rt.anchoredPosition = ToUI(quickXs[i], 68);
                rt.sizeDelta = new Vector2(38, 38);

                var bgImg = qGo.GetComponent<Image>();
                bgImg.color = new Color(1f, 1f, 1f, 0.005f);

                // Highlight overlay on hover
                var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
                hlGo.transform.SetParent(qGo.transform, false);
                var hlRt = hlGo.GetComponent<RectTransform>();
                hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
                hlRt.offsetMin = hlRt.offsetMax = Vector2.zero;
                var hlImg = hlGo.GetComponent<Image>();
                hlImg.color = new Color(1f, 0.88f, 0.4f, 0f);
                hlImg.raycastTarget = false;

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

                // Count text (e.g. 3/5)
                var countTxt = CreateText(qGo.transform, "Count", "", 10, TextAlignmentOptions.TopRight,
                    new Color(1f, 0.92f, 0.5f), FontStyles.Bold);
                var ctRt = countTxt.rectTransform;
                ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
                ctRt.offsetMin = Vector2.zero;
                ctRt.offsetMax = new Vector2(-2, -2);

                var btn = qGo.GetComponent<Button>();
                btn.targetGraphic = bgImg;
                btn.onClick.AddListener(() => OnQuickItemClicked(index));

                AddHoverHighlight(qGo, hlImg, 0f, 0.25f);
                AddHoverTrigger(qGo,
                    () => ShowQuickSlotTooltip(index),
                    HideTooltip);

                _quickSlots.Add(new QuickSlotUI
                {
                    slotIndex = i + 1,
                    button = btn,
                    icon = iconImg,
                    countText = countTxt,
                    numberText = numTxt
                });
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
            var valGo = new GameObject($"Val_{name}", typeof(RectTransform));
            valGo.transform.SetParent(parent, false);
            var valRt = valGo.GetComponent<RectTransform>();
            valRt.anchoredPosition = ToUI(660, py);
            valRt.sizeDelta = new Vector2(32, 20);

            valueTxt = CreateText(valGo.transform, "Label", "10", 16, TextAlignmentOptions.Center,
                Color.white, FontStyles.Bold);
            var vtRt = valueTxt.rectTransform;
            vtRt.anchorMin = Vector2.zero; vtRt.anchorMax = Vector2.one;
            vtRt.offsetMin = vtRt.offsetMax = Vector2.zero;

            // [+] Button
            var plusGo = new GameObject($"BtnPlus_{name}", typeof(RectTransform), typeof(Image), typeof(Button));
            plusGo.transform.SetParent(parent, false);
            var plusRt = plusGo.GetComponent<RectTransform>();
            plusRt.anchoredPosition = ToUI(708, py);
            plusRt.sizeDelta = new Vector2(28, 24);
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
            maxRt.anchoredPosition = ToUI(752, py);
            maxRt.sizeDelta = new Vector2(46, 24);
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

            var items = GetInitialInventoryData();

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int index = r * 4 + c;
                    var slotGo = new GameObject($"InvSlot_{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                    slotGo.transform.SetParent(parent, false);
                    var rt = slotGo.GetComponent<RectTransform>();
                    rt.anchoredPosition = ToUI(colXs[c], rowYs[r]);
                    rt.sizeDelta = slotSize;

                    // Invisible click/raycast target
                    var bgImg = slotGo.GetComponent<Image>();
                    bgImg.color = new Color(1f, 1f, 1f, 0.005f);

                    // Child: Highlight image (golden highlight perfectly aligned to slot borders)
                    var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
                    hlGo.transform.SetParent(slotGo.transform, false);
                    var hlRt = hlGo.GetComponent<RectTransform>();
                    hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
                    hlRt.offsetMin = Vector2.zero; hlRt.offsetMax = Vector2.zero;
                    var hlImg = hlGo.GetComponent<Image>();
                    hlImg.color = new Color(1f, 0.88f, 0.4f, 0f);
                    hlImg.raycastTarget = false;

                    // Child: Item Icon (shown only when item is present)
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(slotGo.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
                    iconRt.offsetMin = new Vector2(3, 3); iconRt.offsetMax = new Vector2(-3, -3);
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;

                    var item = (index < items.Count) ? items[index] : null;
                    if (item != null && item.icon != null)
                    {
                        iconImg.sprite = item.icon;
                        iconImg.color = Color.white;
                    }
                    else
                    {
                        iconImg.sprite = null;
                        iconImg.color = new Color(1f, 1f, 1f, 0f);
                    }

                    var btn = slotGo.GetComponent<Button>();
                    btn.targetGraphic = bgImg;

                    var slotData = new InventorySlotUI
                    {
                        index = index,
                        button = btn,
                        icon = iconImg,
                        item = item
                    };

                    btn.onClick.AddListener(() => OnInventorySlotClicked(slotData));

                    AddHoverHighlight(slotGo, hlImg, 0f, 0.25f);
                    AddHoverTrigger(slotGo,
                        () =>
                        {
                            if (slotData.item != null)
                            {
                                ShowTooltip(slotData.item.name, slotData.item.typeName, slotData.item.description);
                            }
                        },
                        HideTooltip);

                    _inventorySlots.Add(slotData);
                }
            }
        }

        private void BuildTooltipBox(RectTransform parent)
        {
            _tooltipBox = new GameObject("Tooltip_Box", typeof(RectTransform), typeof(Image));
            _tooltipBox.transform.SetParent(parent, false);
            var rt = _tooltipBox.GetComponent<RectTransform>();
            rt.anchoredPosition = ToUI(435, 18);
            rt.sizeDelta = new Vector2(340, 48);

            var img = _tooltipBox.GetComponent<Image>();
            img.color = new Color(0.08f, 0.05f, 0.03f, 0.95f);

            _txtTooltipTitle = CreateText(_tooltipBox.transform, "Title", "", 13, TextAlignmentOptions.MidlineLeft,
                new Color(1f, 0.85f, 0.45f), FontStyles.Bold);
            var tRt = _txtTooltipTitle.rectTransform;
            tRt.anchorMin = new Vector2(0.03f, 0.5f); tRt.anchorMax = new Vector2(0.6f, 1f);
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;

            _txtTooltipSubtitle = CreateText(_tooltipBox.transform, "Subtitle", "", 11, TextAlignmentOptions.MidlineRight,
                new Color(0.55f, 0.80f, 1f), FontStyles.Italic);
            var sRt = _txtTooltipSubtitle.rectTransform;
            sRt.anchorMin = new Vector2(0.6f, 0.5f); sRt.anchorMax = new Vector2(0.97f, 1f);
            sRt.offsetMin = sRt.offsetMax = Vector2.zero;

            _txtTooltipDesc = CreateText(_tooltipBox.transform, "Desc", "", 11, TextAlignmentOptions.MidlineLeft,
                new Color(0.92f, 0.92f, 0.92f), FontStyles.Normal);
            var dRt = _txtTooltipDesc.rectTransform;
            dRt.anchorMin = new Vector2(0.03f, 0f); dRt.anchorMax = new Vector2(0.97f, 0.55f);
            dRt.offsetMin = dRt.offsetMax = Vector2.zero;

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
        public void ShowTooltip(string title, string subtitle, string description)
        {
            if (_tooltipBox == null) return;
            _tooltipBox.SetActive(true);
            if (_txtTooltipTitle != null) _txtTooltipTitle.text = title;
            if (_txtTooltipSubtitle != null) _txtTooltipSubtitle.text = subtitle;
            if (_txtTooltipDesc != null) _txtTooltipDesc.text = description;
        }

        public void HideTooltip()
        {
            if (_tooltipBox != null)
            {
                _tooltipBox.SetActive(false);
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
                ShowTooltip($"{statName} Upgraded", "Attribute Increased", $"+1 to {statName}! Remaining SP: {player.StatPoints}");
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
                ShowTooltip($"{statName} Maximized", "Attributes Allocated", $"Allocated +{count} points to {statName}! Remaining SP: {player.StatPoints}");
            }
        }

        private void ShowQuickSlotTooltip(int index)
        {
            var qm = TheLastKnight.Core.QuickItemManager.Instance;
            var item = qm != null ? qm.GetSlot(index) : null;

            if (item != null && item.count > 0)
            {
                string subtitle = index == 0 ? $"{item.typeName} [Ready for Q]" : $"{item.typeName} [Priority {index + 1}]";
                ShowTooltip(item.name, subtitle, item.description);
            }
            else
            {
                ShowTooltip($"Quick Slot {index + 1} (Empty)", $"Priority {index + 1}",
                    index == 0
                        ? "Currently active quick slot [Q]. No item ready. When items are acquired, they will be placed here."
                        : "Empty queue slot. When items in earlier slots are exhausted, items in later slots advance forward automatically.");
            }
        }

        private void OnQuickItemClicked(int index)
        {
            var player = GetPlayer();
            if (player == null) return;

            var qm = TheLastKnight.Core.QuickItemManager.Instance;
            var item = qm != null ? qm.GetSlot(index) : null;

            if (item == null || item.count <= 0)
            {
                ShowTooltip($"Quick Slot {index + 1} Empty", $"Priority {index + 1}", "This slot is currently empty.");
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            if (item.id == "potion_heal")
            {
                if (player.HealingPotions > 0 && player.CurrentHP < player.MaxHP)
                {
                    player.CompletePotionDrink();
                    AudioManager.Instance?.PlaySfx("click");
                    GameManager.Instance?.Capture();
                    Refresh(true);
                    ShowTooltip("Healing Potion Consumed", "Recovery", "Restored 50 HP. Potions remaining: " + player.HealingPotions);
                }
                else if (player.HealingPotions <= 0)
                {
                    ShowTooltip("Healing Potion Empty", "Warning", "No healing potions left!");
                }
                else
                {
                    ShowTooltip("Full Health", "Notice", "Arthur is already at maximum health.");
                }
            }
            else
            {
                qm.UseSlot(index, player);
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
            }
        }

        private void OnInventorySlotClicked(InventorySlotUI slot)
        {
            if (slot.item == null) return;
            var player = GetPlayer();
            if (player == null) return;

            if (slot.item.isConsumable)
            {
                var qm = TheLastKnight.Core.QuickItemManager.Instance;
                if (qm != null)
                {
                    int emptyIdx = -1;
                    for (int i = 0; i < TheLastKnight.Core.QuickItemManager.MaxSlots; i++)
                    {
                        if (qm.GetSlot(i) == null) { emptyIdx = i; break; }
                    }

                    if (emptyIdx != -1)
                    {
                        qm.SetSlot(emptyIdx, new TheLastKnight.Core.QuickItemSlotData
                        {
                            id = slot.item.id,
                            name = slot.item.name,
                            typeName = slot.item.typeName,
                            description = slot.item.description,
                            icon = slot.item.icon,
                            count = slot.item.count,
                            maxCount = 99,
                            onUse = slot.item.onUse
                        });
                        string itemName = slot.item.name;
                        slot.item = null;
                        slot.icon.sprite = null;
                        slot.icon.color = new Color(1f, 1f, 1f, 0f);
                        AudioManager.Instance?.PlaySfx("click");
                        Refresh(true);
                        ShowTooltip("Assigned to Quick Slot", $"Priority {emptyIdx + 1}", $"{itemName} placed in Quick Slot {emptyIdx + 1}.");
                        return;
                    }
                }
            }

            if (slot.item.onUse != null)
            {
                slot.item.onUse.Invoke(player);
                AudioManager.Instance?.PlaySfx("click");
                GameManager.Instance?.Capture();
                Refresh(true);
            }
            else
            {
                ShowTooltip(slot.item.name, slot.item.typeName, slot.item.description);
                AudioManager.Instance?.PlaySfx("click");
            }
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

            // Sync Quick items (1-5 Priority Queue)
            var qm = TheLastKnight.Core.QuickItemManager.Instance;
            if (qm != null && player != null)
            {
                qm.SyncItemCount("potion_heal", player.HealingPotions);
            }

            for (int i = 0; i < _quickSlots.Count; i++)
            {
                var slotUI = _quickSlots[i];
                var item = qm != null ? qm.GetSlot(i) : null;

                if (item != null && item.count > 0)
                {
                    if (slotUI.icon != null)
                    {
                        slotUI.icon.sprite = item.icon;
                        slotUI.icon.color = Color.white;
                    }
                    if (slotUI.countText != null)
                    {
                        slotUI.countText.text = item.maxCount > 1 ? $"{item.count}/{item.maxCount}" : $"{item.count}";
                    }
                }
                else
                {
                    if (slotUI.icon != null)
                    {
                        slotUI.icon.sprite = null;
                        slotUI.icon.color = new Color(1f, 1f, 1f, 0f);
                    }
                    if (slotUI.countText != null)
                    {
                        slotUI.countText.text = "";
                    }
                }
            }
        }

        private List<InventoryItemData> GetInitialInventoryData()
        {
            // Arthur starts with an empty inventory.
            // Items can be added dynamically via AddInventoryItem().
            return new List<InventoryItemData>();
        }

        public void AddInventoryItem(InventoryItemData newItem)
        {
            if (newItem == null) return;
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                if (_inventorySlots[i].item == null)
                {
                    _inventorySlots[i].item = newItem;
                    if (_inventorySlots[i].icon != null)
                    {
                        _inventorySlots[i].icon.sprite = newItem.icon;
                        _inventorySlots[i].icon.color = Color.white;
                    }
                    break;
                }
            }
        }

        public void ClearInventory()
        {
            for (int i = 0; i < _inventorySlots.Count; i++)
            {
                _inventorySlots[i].item = null;
                if (_inventorySlots[i].icon != null)
                {
                    _inventorySlots[i].icon.sprite = null;
                    _inventorySlots[i].icon.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }
        #endregion

        #region Helper Classes
        public class InventoryItemData
        {
            public string id;
            public string name;
            public string typeName;
            public string description;
            public Sprite icon;
            public int count = 1;
            public bool isConsumable;
            public Action<PlayerStats> onUse;
        }

        private class InventorySlotUI
        {
            public int index;
            public Button button;
            public Image icon;
            public InventoryItemData item;
        }

        private class QuickSlotUI
        {
            public int slotIndex;
            public Button button;
            public Image icon;
            public TextMeshProUGUI countText;
            public TextMeshProUGUI numberText;
            public string title;
            public string description;
        }

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
