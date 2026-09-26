using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
            EnsureEventSystem();
            BuildUI();
            SetWindowVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            EnsureEventSystem();
            if (_canvasObject == null) BuildUI();

            var player = GetPlayer();
            if (Application.isPlaying && player != null && player.IsDead) return;

            _isOpen = true;
            if (Application.isPlaying)
            {
                GameManager.Instance?.SetInputBlocked(true);
                Time.timeScale = 0f;
            }

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

            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
                GameManager.Instance?.SetInputBlocked(false);
            }
            AudioManager.Instance?.PlaySfx("click");
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
            var lvlGo = new GameObject("Txt_Level", typeof(RectTransform), typeof(Image));
            lvlGo.transform.SetParent(parent, false);
            var lvlRt = lvlGo.GetComponent<RectTransform>();
            lvlRt.anchoredPosition = ToUI(332, 360);
            lvlRt.sizeDelta = new Vector2(86, 24);
            var lvlBg = lvlGo.GetComponent<Image>();
            lvlBg.color = new Color(0.35f, 0.25f, 0.18f, 1f);

            _txtLevel = CreateText(lvlGo.transform, "Label", "LV.1", 19, TextAlignmentOptions.MidlineLeft,
                new Color(0.96f, 0.94f, 0.90f), FontStyles.Bold);
            var ltRt = _txtLevel.rectTransform;
            ltRt.anchorMin = Vector2.zero; ltRt.anchorMax = Vector2.one;
            ltRt.offsetMin = ltRt.offsetMax = Vector2.zero;

            // 2. Bars: XP, HP, STM
            CreateStatBar(parent, "XP_Bar", ToUI(406, 330), new Vector2(236, 18),
                new Color(0.25f, 0.32f, 0.42f), out _imgXpFill, out _txtXp, "XP: 0/100");

            CreateStatBar(parent, "HP_Bar", ToUI(406, 307), new Vector2(236, 18),
                new Color(0.72f, 0.15f, 0.12f), out _imgHpFill, out _txtHp, "HP: 100/150");

            CreateStatBar(parent, "STM_Bar", ToUI(406, 280), new Vector2(236, 18),
                new Color(0.76f, 0.48f, 0.12f), out _imgStmFill, out _txtStm, "STM: 30/100");

            // 3. Dynamic Gold Display
            var goldGo = new GameObject("Txt_Gold", typeof(RectTransform), typeof(Image));
            goldGo.transform.SetParent(parent, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            goldRt.anchoredPosition = ToUI(423, 248);
            goldRt.sizeDelta = new Vector2(225, 22);
            goldGo.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.06f, 1f);

            _txtGold = CreateText(goldGo.transform, "Label", "GOLD: 0", 15, TextAlignmentOptions.MidlineLeft,
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

            // 5. Quick Items Section (5 Slots)
            string[] quickTitles = { "Healing Potion [Q]", "Ancient Scroll of Moa", "Stamina Elixir", "Iron Bark Elixir", "Token of Moa" };
            string[] quickDescs = {
                "Restores 50 HP immediately. Hotkey [Q]. [Click to drink]",
                "An ancient enchanted parchment recounting the legends of the kingdom of Moa.",
                "A refreshing herbal brew that instantly restores 50 Stamina. [Click to drink]",
                "An alchemical defense tonic that hardens skin against demon strikes.",
                "A rare commemorative gold coin from the royal treasury of Moa."
            };
            float[] quickXs = { 315f, 365f, 415f, 465f, 515f };

            for (int i = 0; i < 5; i++)
            {
                int index = i;
                var qGo = new GameObject($"QuickSlot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                qGo.transform.SetParent(parent, false);
                var rt = qGo.GetComponent<RectTransform>();
                rt.anchoredPosition = ToUI(quickXs[i], 61);
                rt.sizeDelta = new Vector2(44, 44);

                var img = qGo.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.01f);

                // Slot 1 has potion count badge
                TextMeshProUGUI countTxt = null;
                if (i == 0)
                {
                    countTxt = CreateText(qGo.transform, "Count", "3/5", 10, TextAlignmentOptions.TopRight,
                        new Color(1f, 0.9f, 0.4f), FontStyles.Bold);
                    var ctRt = countTxt.rectTransform;
                    ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
                    ctRt.offsetMin = new Vector2(0, 0); ctRt.offsetMax = new Vector2(-2, -2);
                }

                var btn = qGo.GetComponent<Button>();
                btn.onClick.AddListener(() => OnQuickItemClicked(index));

                AddHoverHighlight(qGo, img);
                AddHoverTrigger(qGo,
                    () => ShowTooltip(quickTitles[index], "Quick Item Slot " + (index + 1), quickDescs[index]),
                    HideTooltip);

                _quickSlots.Add(new QuickSlotUI
                {
                    slotIndex = i + 1,
                    button = btn,
                    icon = img,
                    countText = countTxt,
                    title = quickTitles[i],
                    description = quickDescs[i]
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
            // 1. Status Points Number overlay
            var spGo = new GameObject("Txt_SP", typeof(RectTransform), typeof(Image));
            spGo.transform.SetParent(parent, false);
            var spRt = spGo.GetComponent<RectTransform>();
            spRt.anchoredPosition = ToUI(735, 417);
            spRt.sizeDelta = new Vector2(28, 20);
            spGo.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.98f);

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
            // Value Box
            var valGo = new GameObject($"Val_{name}", typeof(RectTransform), typeof(Image));
            valGo.transform.SetParent(parent, false);
            var valRt = valGo.GetComponent<RectTransform>();
            valRt.anchoredPosition = ToUI(660, py);
            valRt.sizeDelta = new Vector2(28, 20);
            valGo.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.98f);

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
            float[] colXs = { 602f, 645f, 688f, 731f };
            float[] rowYs = { 262f, 220f, 178f, 136f, 94f, 52f };

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
                    rt.sizeDelta = new Vector2(38, 38);

                    var img = slotGo.GetComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0.01f); // Transparent over baked slot frame

                    var btn = slotGo.GetComponent<Button>();
                    var slotData = new InventorySlotUI
                    {
                        index = index,
                        button = btn,
                        icon = img,
                        item = (index < items.Count) ? items[index] : null
                    };

                    btn.onClick.AddListener(() => OnInventorySlotClicked(slotData));

                    AddHoverHighlight(slotGo, img);
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

        private void AddHoverHighlight(GameObject target, Image img)
        {
            var trigger = target.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? target.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener((d) => { img.color = new Color(1f, 0.85f, 0.4f, 0.25f); });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener((d) => { img.color = new Color(1f, 1f, 1f, 0.01f); });
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

        private void OnQuickItemClicked(int index)
        {
            var player = GetPlayer();
            if (player == null) return;

            if (index == 0) // Slot 1: Healing potion
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
            else if (index == 2) // Slot 3: Stamina Elixir
            {
                if (player.CurrentStamina < player.MaxStamina)
                {
                    player.Rest();
                    AudioManager.Instance?.PlaySfx("click");
                    ShowTooltip("Stamina Restored", "Recovery", "Arthur feels revitalized!");
                    Refresh(true);
                }
            }
        }

        private void OnInventorySlotClicked(InventorySlotUI slot)
        {
            if (slot.item == null) return;
            var player = GetPlayer();
            if (player == null) return;

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
            if (_txtGold != null) _txtGold.text = $"GOLD: {gold:N0}";

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

            // Sync Quick items
            if (_quickSlots.Count > 0 && _quickSlots[0].countText != null)
            {
                _quickSlots[0].countText.text = $"{potions}/5";
            }
        }

        private List<InventoryItemData> GetInitialInventoryData()
        {
            var list = new List<InventoryItemData>();

            // 1. Arthur's Greatsword
            list.Add(new InventoryItemData
            {
                id = "sword_arthur",
                name = "Knight's Greatsword",
                typeName = "Equipment (Weapon)",
                description = "The stalwart blade forged for Arthur of Moa. Increases physical attack power.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Sword")
            });

            // 2. Healing Potion
            list.Add(new InventoryItemData
            {
                id = "heal_potion",
                name = "Medium Healing Potion",
                typeName = "Consumable",
                description = "A distilled alchemical draught. Restores 50 HP on drink. [Click to use]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_RedPotion"),
                isConsumable = true,
                onUse = (p) =>
                {
                    if (p.HealingPotions > 0 && p.CurrentHP < p.MaxHP) p.CompletePotionDrink();
                }
            });

            // 3. Knight Armor
            list.Add(new InventoryItemData
            {
                id = "armor_knight",
                name = "Knight's Plate Armor",
                typeName = "Equipment (Chest)",
                description = "Tempered steel breastplate inscribed with protective runes of the royal realm.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_DarkArmor")
            });

            // 4. Knight Greaves
            list.Add(new InventoryItemData
            {
                id = "boots_knight",
                name = "Knight's Greaves",
                typeName = "Equipment (Boots)",
                description = "Reinforced greaves granting stability and swift dash recovery.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Boots")
            });

            // 5. Silver Breastplate
            list.Add(new InventoryItemData
            {
                id = "armor_silver",
                name = "Silver Guard Armor",
                typeName = "Equipment (Chest)",
                description = "Polished parade armor worn by high ranking knights.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_SilverArmor")
            });

            // 6. Stamina Elixir
            list.Add(new InventoryItemData
            {
                id = "stamina_potion",
                name = "Stamina Elixir",
                typeName = "Consumable",
                description = "Herbal extract that instantly restores stamina. [Click to drink]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_GreenPotion"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.Rest();
                }
            });

            // 7. Golden Seed of Moa
            list.Add(new InventoryItemData
            {
                id = "golden_seed",
                name = "Golden Seed of Moa",
                typeName = "Consumable",
                description = "A glowing sacred seed gathered from the ancient forest boughs. [Click to consume for +5 Stat Points]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_GoldenSeed"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.AddStatPoints(5);
                    ShowTooltip("Golden Seed Consumed", "Stat Points Granted", "Granted +5 Status Points! Total available: " + p.StatPoints);
                }
            });

            // 8. Coin Pouch
            list.Add(new InventoryItemData
            {
                id = "coin_pouch",
                name = "Royal Coin Pouch",
                typeName = "Valuable",
                description = "A heavy pouch filled with gold coins recovered from fallen foes. [Click to open]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_GoldCoin"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.AddGold(100);
                }
            });

            // 9. Bronze Broadsword
            list.Add(new InventoryItemData
            {
                id = "sword_bronze",
                name = "Bronze Broadsword",
                typeName = "Equipment (Weapon)",
                description = "A heavy backup blade kept sharp in case of battle.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_BronzeSword")
            });

            // 10. Secondary Healing Potion
            list.Add(new InventoryItemData
            {
                id = "heal_potion_2",
                name = "Red Healing Potion",
                typeName = "Consumable",
                description = "Restores 50 HP. [Click to use]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_RedPotion2"),
                isConsumable = true,
                onUse = (p) =>
                {
                    if (p.CurrentHP < p.MaxHP) p.Heal(50f);
                }
            });

            // 11. Leather Tunic
            list.Add(new InventoryItemData
            {
                id = "armor_leather",
                name = "Scout's Leather Armor",
                typeName = "Equipment (Armor)",
                description = "Lightweight boiled leather offering superior maneuverability.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_LeatherArmor")
            });

            // 12. Steel Greaves
            list.Add(new InventoryItemData
            {
                id = "boots_heavy",
                name = "Heavy Marching Greaves",
                typeName = "Equipment (Boots)",
                description = "Heavy greaves worn during long sieges.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_HeavyBoots")
            });

            // 13. Silver Dart
            list.Add(new InventoryItemData
            {
                id = "dart_silver",
                name = "Silver Throwing Dart",
                typeName = "Consumable",
                description = "A blessed throwing dart deadly to unholy monstrosities.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Dart")
            });

            // 14. Demon Plate
            list.Add(new InventoryItemData
            {
                id = "armor_demon",
                name = "Dark Demon Armor",
                typeName = "Equipment (Armor)",
                description = "Forged from abyss ore, radiating cold demonic energy.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_DemonArmor")
            });

            // 15. Demon Rune of Trident (Slot 15)
            list.Add(new InventoryItemData
            {
                id = "rune_3",
                name = "Rune of the Trident",
                typeName = "Demon Rune (Quest)",
                description = "One of four ancient runes required to breach the Demon Castle Gates.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_PurpleShard")
            });

            // 16. Supply Pouch
            list.Add(new InventoryItemData
            {
                id = "supply_pouch",
                name = "Soldier's Rucksack",
                typeName = "Quest Item",
                description = "Contains traveler supplies and parchment map fragments.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Pouch")
            });

            // 17. Reinforced Plate
            list.Add(new InventoryItemData
            {
                id = "armor_reinforced",
                name = "Reinforced Vanguard Plate",
                typeName = "Equipment (Armor)",
                description = "Extra-thick breastplate capable of deflecting boss strikes.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_SilverArmor2")
            });

            // 18. Vitality Potion (+1 VIT)
            list.Add(new InventoryItemData
            {
                id = "potion_vit",
                name = "Vitality Potion (+1 VIT)",
                typeName = "Consumable",
                description = "A rare elixir that permanently increases Vitality by +1! [Click to drink]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_PotionFlask"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.AddStatPotion("VIT");
                }
            });

            // 19. Healing Herb
            list.Add(new InventoryItemData
            {
                id = "herb_heal",
                name = "Sunlit Blossom Herb",
                typeName = "Consumable",
                description = "Medicinal herb that restores 25 HP. [Click to use]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Herb"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.Heal(25f);
                }
            });

            // 20. Gold Coins
            list.Add(new InventoryItemData
            {
                id = "gold_coins",
                name = "Ancient Moa Gold Coin",
                typeName = "Currency",
                description = "Standard currency accepted by the Shadow Market traders.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_GoldCoin2")
            });

            // 21. Dexterity Potion (+1 DEX)
            list.Add(new InventoryItemData
            {
                id = "potion_dex",
                name = "Dexterity Potion (+1 DEX)",
                typeName = "Consumable",
                description = "A glowing yellow concoction that permanently increases Dexterity by +1! [Click to drink]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_YellowPotion"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.AddStatPotion("DEX");
                }
            });

            // 22. Smoke Bomb
            list.Add(new InventoryItemData
            {
                id = "smoke_bomb",
                name = "Shadow Smoke Flask",
                typeName = "Consumable",
                description = "Creates a dense obscuring smoke screen to elude pursuit.",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Bomb")
            });

            // 23. Strength Potion (+1 STR)
            list.Add(new InventoryItemData
            {
                id = "potion_str",
                name = "Strength Potion (+1 STR)",
                typeName = "Consumable",
                description = "A vibrant violet elixir that permanently increases Strength by +1! [Click to drink]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_VioletPotion"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.AddStatPotion("STR");
                }
            });

            // 24. Fresh Bread
            list.Add(new InventoryItemData
            {
                id = "bread",
                name = "Hearty Country Bread",
                typeName = "Consumable",
                description = "Freshly baked bread from the village. Restores 30 HP and 20 Stamina. [Click to eat]",
                icon = Resources.Load<Sprite>("CharacterStatus/Items/Item_Bread"),
                isConsumable = true,
                onUse = (p) =>
                {
                    p.Heal(30f);
                }
            });

            return list;
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
