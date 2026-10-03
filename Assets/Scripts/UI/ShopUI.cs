using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using TheLastKnight.Core;
using TheLastKnight.Environment;
using TheLastKnight.Inventory;
using TheLastKnight.Stats;
using TheLastKnight.Audio;

namespace TheLastKnight.UI
{
    public enum ShopItemType
    {
        InventoryItem, // Added to player inventory (potions, food, bombs, darts, etc.)
        StatPotion,    // Directly upgrades STR, VIT, DEX, or AGI attribute
        RuneReward     // Demon rune reward dropped into the world
    }

    [System.Serializable]
    public class ShopItemConfig
    {
        [Tooltip("Unique ID matching ItemRegistry id or 'RUNE', 'STR', 'VIT', 'DEX', 'AGI'")]
        public string id = "potion_heal";

        [Tooltip("Display name shown in shop cards")]
        public string displayName = "Healing Potion [Q]";

        [Tooltip("Category or subtitle (e.g. Consumable, Attribute Elixir)")]
        public string category = "Consumable Potion";

        [TextArea(2, 4)]
        [Tooltip("Item description shown in tooltips")]
        public string description = "Restores 50 HP immediately. Place in Quick Slot to drink on demand with [Q]. (Max 5 held)";

        [Tooltip("Purchase cost in Gold")]
        public int buyPrice = 50;

        [Tooltip("Initial stock limit. Set to -1 for infinite / unlimited stock.")]
        public int initialStock = -1;

        [Tooltip("Custom sell price override. Set to -1 to automatically calculate 75% of buyPrice.")]
        public int customSellPrice = -1;

        [Tooltip("Type of item behavior when purchased")]
        public ShopItemType itemType = ShopItemType.InventoryItem;

        [Tooltip("Specific stat to upgrade if itemType is StatPotion ('STR', 'VIT', 'DEX', 'AGI')")]
        public string statName = "STR";

        [Tooltip("Rune index (0-3) if itemType is RuneReward")]
        public int runeIndex = 3;

        [Tooltip("Custom icon override. If null, loads from ItemRegistry or Resources/CharacterStatus.")]
        public Sprite customIcon;

        // Runtime stock tracking
        [NonSerialized] public int currentStock = -1;
        [NonSerialized] public bool stockInitialized = false;

        public void EnsureStockInitialized()
        {
            if (!stockInitialized)
            {
                currentStock = initialStock;
                stockInitialized = true;
            }
        }
    }

    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        public event Action Closed;
        private GameObject _panel;
        public bool IsOpen => _panel != null;

        [Header("Shop Catalog (Configure / Add items in Inspector)")]
        [SerializeField] private List<ShopItemConfig> _shopCatalog = new List<ShopItemConfig>();
        public List<ShopItemConfig> Catalog => _shopCatalog;

        [Header("Sell Settings")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _sellPriceMultiplier = 0.75f; // 75% of buy price

        // Sprites loaded from Resources/Shop (Only allowed user textures)
        private Sprite _spWindowRack;
        private Sprite _spGoldCoin;

        // Procedural 9-slice rounded rectangle sprite cache for modern rounded corners
        private static Sprite _roundedRectSprite;

        public static Sprite GetRoundedRectSprite()
        {
            if (_roundedRectSprite != null) return _roundedRectSprite;

            int size = 64;
            int radius = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0;
                    if (x < radius) dx = radius - x - 0.5f;
                    else if (x >= size - radius) dx = x - (size - radius - 1) - 0.5f;

                    float dy = 0;
                    if (y < radius) dy = radius - y - 0.5f;
                    else if (y >= size - radius) dy = y - (size - radius - 1) - 0.5f;

                    if (dx > 0 && dy > 0)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > radius)
                        {
                            colors[y * size + x] = Color.clear;
                        }
                        else
                        {
                            float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                            colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                        }
                    }
                    else
                    {
                        colors[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            Vector4 border = new Vector4(radius, radius, radius, radius);
            _roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return _roundedRectSprite;
        }

        // UI references
        private TextMeshProUGUI _txtGold;
        private TextMeshProUGUI _txtToastMessage;
        private Coroutine _toastCoroutine;

        // Tooltip references
        private GameObject _tooltipGo;
        private RectTransform _tooltipRect;
        private TextMeshProUGUI _txtTooltipTitle;
        private TextMeshProUGUI _txtTooltipCategory;
        private TextMeshProUGUI _txtTooltipDesc;

        // Cursor Follower for Left-Click Drag/Drop & Stack Organizing
        private GameObject _cursorFollower;
        private Image _cursorIcon;
        private TextMeshProUGUI _cursorCount;

        // Sell Panel references
        private GameObject _sellPanelGo;
        private RectTransform _sellPanelRect;
        private Image _imgSellItemIcon;
        private TextMeshProUGUI _txtSellItemName;
        private TextMeshProUGUI _txtSellItemStock;
        private TextMeshProUGUI _txtSellUnitPrice;
        private TextMeshProUGUI _txtSellTotalPrice;
        private Button _btnSellOne;
        private Button _btnSellAll;
        private TextMeshProUGUI _txtSellOneLabel;
        private TextMeshProUGUI _txtSellAllLabel;
        private GameObject _sellNotForSaleGo;
        private SlotType _selectedSlotType;
        private int _selectedSlotIndex = -1;

        // Rack slot UI items
        private readonly List<ShopRackSlotUI> _gridSlotUIs = new List<ShopRackSlotUI>();
        private readonly List<ShopRackSlotUI> _quickSlotUIs = new List<ShopRackSlotUI>();

        public InventoryManager Inventory => InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>();
        public PlayerStats Player => GameManager.Instance?.Player ?? FindAnyObjectByType<PlayerStats>();

        // Shop goods item entries & row views
        private readonly List<ShopItemRowView> _shopRowViews = new List<ShopItemRowView>();

        private class ShopItemRowView
        {
            public ShopItemConfig config;
            public Image imgIcon;
            public TextMeshProUGUI txtName;
            public TextMeshProUGUI txtPrice;
            public TextMeshProUGUI txtStock;
            public Button btnBuy;
            public Image imgBuy;
            public TextMeshProUGUI txtBuyBtn;
            public CanvasGroup canvasGroup;
            public Transform rowTransform;
            public bool isSoldOut;
        }

        private void Reset()
        {
            PopulateDefaultCatalog();
        }

        [ContextMenu("Reset to Default Shop Catalog")]
        public void PopulateDefaultCatalog()
        {
            _shopCatalog = new List<ShopItemConfig>
            {
                new ShopItemConfig
                {
                    id = "HEAL",
                    displayName = "Healing Potion [Q]",
                    category = "Consumable Potion",
                    description = "Restores 50 HP immediately. Place in Quick Slot to drink on demand with [Q].",
                    buyPrice = 50,
                    initialStock = 10,
                    itemType = ShopItemType.InventoryItem
                },
                new ShopItemConfig
                {
                    id = "potion_stamina",
                    displayName = "Stamina Elixir",
                    category = "Consumable Potion",
                    description = "Instantly replenishes 100 Stamina points. Crucial during intense boss combat.",
                    buyPrice = 60,
                    initialStock = 5,
                    itemType = ShopItemType.InventoryItem
                },
                new ShopItemConfig
                {
                    id = "golden_seed",
                    displayName = "Golden Seed (+1 SP)",
                    category = "Sacred Relic",
                    description = "A sacred seed imbued with ancient holy light. Bestows +1 Attribute Stat Point (SP).",
                    buyPrice = 120,
                    initialStock = 3,
                    itemType = ShopItemType.InventoryItem
                },
                new ShopItemConfig
                {
                    id = "RUNE",
                    displayName = "Rune of the Trident",
                    category = "Demonic Keystone",
                    description = "Ancient demon relic imbued with oceanic depths. Key required to unlock the Demon Castle Gate.",
                    buyPrice = 150,
                    initialStock = 1,
                    itemType = ShopItemType.RuneReward,
                    runeIndex = 3
                },
                new ShopItemConfig
                {
                    id = "STR",
                    displayName = "Strength Potion (+1 STR)",
                    category = "Attribute Elixir",
                    description = "Permanently upgrades Strength by +1, increasing physical attack power and heavy blow impact.",
                    buyPrice = 100,
                    initialStock = 3,
                    itemType = ShopItemType.StatPotion,
                    statName = "STR"
                },
                new ShopItemConfig
                {
                    id = "VIT",
                    displayName = "Vitality Potion (+1 VIT)",
                    category = "Attribute Elixir",
                    description = "Permanently upgrades Vitality by +1, elevating maximum Health (HP) and defense resistances.",
                    buyPrice = 100,
                    initialStock = 3,
                    itemType = ShopItemType.StatPotion,
                    statName = "VIT"
                },
                new ShopItemConfig
                {
                    id = "DEX",
                    displayName = "Dexterity Potion (+1 DEX)",
                    category = "Attribute Elixir",
                    description = "Permanently upgrades Dexterity by +1, raising critical strike rate and offensive precision.",
                    buyPrice = 100,
                    initialStock = 3,
                    itemType = ShopItemType.StatPotion,
                    statName = "DEX"
                },
                new ShopItemConfig
                {
                    id = "AGI",
                    displayName = "Agility Potion (+1 AGI)",
                    category = "Attribute Elixir",
                    description = "Permanently upgrades Agility by +1, accelerating stamina recovery and evasion finesse.",
                    buyPrice = 100,
                    initialStock = 3,
                    itemType = ShopItemType.StatPotion,
                    statName = "AGI"
                },
                new ShopItemConfig
                {
                    id = "bread",
                    displayName = "Fresh Bread",
                    category = "Rations",
                    description = "Warm freshly baked tavern bread. Restores 35 HP on consumption.",
                    buyPrice = 25,
                    initialStock = -1,
                    itemType = ShopItemType.InventoryItem
                },
                new ShopItemConfig
                {
                    id = "smoke_bomb",
                    displayName = "Smoke Bomb",
                    category = "Tactical Tool",
                    description = "Detonates into a thick smoke cloud, obscuring enemy vision and aiding hasty retreats.",
                    buyPrice = 50,
                    initialStock = 5,
                    itemType = ShopItemType.InventoryItem
                }
            };
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            if (_shopCatalog == null || _shopCatalog.Count == 0)
            {
                PopulateDefaultCatalog();
            }
            foreach (var item in _shopCatalog)
            {
                item.EnsureStockInitialized();
            }
            LoadSprites();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= Refresh;
            }
        }

        private void LoadSprites()
        {
            if (_spWindowRack == null) _spWindowRack = Resources.Load<Sprite>("Shop/Window_Mockup_Shop");
            if (_spGoldCoin == null) _spGoldCoin = Resources.Load<Sprite>("Shop/Gold_Coin");

            // Fallback for gold coin if needed
            if (_spGoldCoin == null) _spGoldCoin = Resources.Load<Sprite>("CharacterStatus/Coin_Icon");
        }

        public void Open()
        {
            if (_panel != null) return;

            if (_shopCatalog == null || _shopCatalog.Count == 0)
            {
                PopulateDefaultCatalog();
            }
            foreach (var item in _shopCatalog)
            {
                item.EnsureStockInitialized();
            }

            LoadSprites();
            GameManager.Instance?.SetInputBlocked(true);
            if (Application.isPlaying) Time.timeScale = 0f;

            BuildUI();

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= Refresh;
                InventoryManager.Instance.OnInventoryChanged += Refresh;
            }

            Refresh();
        }

        private void BuildUI()
        {
            RuntimeUI.EnsureEventSystem();

            // 1. Root Canvas
            _panel = new GameObject("ShopUI_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;

            var scaler = _panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // 2. Dark Tinted Backdrop
            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdropGo.transform.SetParent(_panel.transform, false);
            var bdRt = backdropGo.GetComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero;
            bdRt.offsetMax = Vector2.zero;
            var bdImg = backdropGo.GetComponent<Image>();
            bdImg.color = new Color(0.04f, 0.03f, 0.05f, 0.82f);
            bdImg.raycastTarget = true;

            var bdTrigger = backdropGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var bdClick = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick };
            bdClick.callback.AddListener((d) =>
            {
                HideSellPanel();
                HideTooltip();
            });
            bdTrigger.triggers.Add(bdClick);

            // 3. Top-Left Currency Display (Gold Coin + Amount)
            BuildCurrencyDisplay(_panel.transform);

            // 4. Left Wooden Rack (QUICK ITEMS + 24 Inventory Slots)
            BuildWoodenRack(_panel.transform);

            // 5. Bottom-Center "Back" Button
            BuildBackButton(_panel.transform);

            // 6. Right Side Shop Goods Panel (Framed Scrollable List)
            BuildShopPanel(_panel.transform);

            // 7. Middle Sell Offer Modal / Popup (Above Back Button)
            BuildSellPanel(_panel.transform);

            // 8. Toast Message Banner
            BuildToastBanner(_panel.transform);

            // 9. Floating Tooltip Box
            BuildTooltipBox(_panel.transform);

            // 10. Cursor Follower (Highest UI Sorting)
            BuildCursorFollower(_panel.transform);

            _sellPanelGo.transform.SetAsLastSibling();
            _toastGoLastSibling();
            _tooltipGo.transform.SetAsLastSibling();
            if (_cursorFollower != null) _cursorFollower.transform.SetAsLastSibling();
        }

        private void BuildCursorFollower(Transform parent)
        {
            _cursorFollower = new GameObject("CursorFollower", typeof(RectTransform), typeof(CanvasGroup));
            _cursorFollower.transform.SetParent(parent, false);

            var cg = _cursorFollower.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var rt = _cursorFollower.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_cursorFollower.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            _cursorIcon = iconGo.GetComponent<Image>();
            _cursorIcon.raycastTarget = false;
            _cursorIcon.preserveAspect = true;

            var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            countGo.transform.SetParent(_cursorFollower.transform, false);
            var cRt = countGo.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
            cRt.offsetMin = Vector2.zero; cRt.offsetMax = new Vector2(-2f, 2f);

            _cursorCount = countGo.GetComponent<TextMeshProUGUI>();
            _cursorCount.fontSize = 12f;
            _cursorCount.fontStyle = FontStyles.Bold;
            _cursorCount.color = new Color(1f, 0.95f, 0.5f, 1f);
            _cursorCount.alignment = TextAlignmentOptions.BottomRight;
            _cursorCount.raycastTarget = false;

            var cShadow = countGo.GetComponent<Shadow>();
            cShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            cShadow.effectDistance = new Vector2(1.5f, -1.5f);

            _cursorFollower.SetActive(false);
        }

        private void UpdateCursorFollower()
        {
            var inv = Inventory;
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
                        _panel.GetComponent<RectTransform>(),
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

        private void _toastGoLastSibling()
        {
            if (_txtToastMessage != null) _txtToastMessage.transform.SetAsLastSibling();
        }

        private void BuildCurrencyDisplay(Transform parent)
        {
            var currGo = new GameObject("CurrencyDisplay", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            currGo.transform.SetParent(parent, false);
            var rt = currGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(45f, -32f);
            rt.sizeDelta = new Vector2(340f, 54f);

            var currBg = currGo.AddComponent<Image>();
            currBg.sprite = GetRoundedRectSprite();
            currBg.type = Image.Type.Sliced;
            currBg.color = new Color(0.10f, 0.14f, 0.20f, 0.75f);

            var currOutline = currGo.AddComponent<Outline>();
            currOutline.effectColor = new Color(0.30f, 0.45f, 0.65f, 0.50f);
            currOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var layout = currGo.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 16, 4, 4);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Coin Icon
            var coinGo = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            coinGo.transform.SetParent(currGo.transform, false);
            var coinRt = coinGo.GetComponent<RectTransform>();
            coinRt.sizeDelta = new Vector2(52f, 52f);
            var coinImg = coinGo.GetComponent<Image>();
            coinImg.sprite = _spGoldCoin;
            coinImg.preserveAspect = true;
            coinImg.raycastTarget = false;

            // Gold Text
            var textGo = new GameObject("Txt_Gold", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            textGo.transform.SetParent(currGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.sizeDelta = new Vector2(260f, 50f);
            _txtGold = textGo.GetComponent<TextMeshProUGUI>();
            _txtGold.fontSize = 32f;
            _txtGold.fontStyle = FontStyles.Bold;
            _txtGold.color = new Color(0.96f, 0.82f, 0.35f, 1f);
            _txtGold.alignment = TextAlignmentOptions.MidlineLeft;
            _txtGold.raycastTarget = false;

            var shadow = textGo.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private void BuildWoodenRack(Transform parent)
        {
            _gridSlotUIs.Clear();
            _quickSlotUIs.Clear();

            // Wooden rack dimensions (Aspect ratio matching 294x402)
            float rackH = 590f;
            float rackW = rackH * (294f / 402f); // ~431f

            var rackGo = new GameObject("Window_Mockup_Shop", typeof(RectTransform), typeof(Image));
            rackGo.transform.SetParent(parent, false);
            var rackRt = rackGo.GetComponent<RectTransform>();
            rackRt.anchorMin = new Vector2(0f, 0.5f);
            rackRt.anchorMax = new Vector2(0f, 0.5f);
            rackRt.pivot = new Vector2(0f, 0.5f);
            rackRt.anchoredPosition = new Vector2(35f, -12f);
            rackRt.sizeDelta = new Vector2(rackW, rackH);

            var rackImg = rackGo.GetComponent<Image>();
            rackImg.sprite = _spWindowRack;
            rackImg.preserveAspect = true;
            rackImg.raycastTarget = true;

            // Normalized slot coordinates inside the 294 x 402 texture:
            float[] colNormXs = { 57f / 294f, 101f / 294f, 145f / 294f, 189f / 294f };
            float quickNormY = 78f / 402f;
            float[] rowNormYs = {
                142f / 402f,
                184f / 402f,
                226f / 402f,
                268f / 402f,
                310f / 402f,
                352f / 402f
            };

            Vector2 slotSize = new Vector2(rackW * (38f / 294f), rackH * (38f / 402f)); // ~55x55

            Vector2 ToLocal(float nx, float ny)
            {
                return new Vector2((nx - 0.5f) * rackW, (0.5f - ny) * rackH);
            }

            // 1. Quick Items Row (4 slots)
            for (int c = 0; c < 4; c++)
            {
                int qIndex = c;
                var slotGo = CreateSlotObject(rackGo.transform, $"QuickSlot_{qIndex + 1}", ToLocal(colNormXs[c], quickNormY), slotSize);
                var slotUI = slotGo.GetComponent<ShopRackSlotUI>();
                slotUI.slotType = SlotType.QuickSlot;
                slotUI.slotIndex = qIndex;
                slotUI.shopUI = this;
                _quickSlotUIs.Add(slotUI);
            }

            // 2. Inventory Grid (4 columns x 6 rows = 24 slots)
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int invIndex = r * 4 + c;
                    var slotGo = CreateSlotObject(rackGo.transform, $"InvSlot_{invIndex + 1}", ToLocal(colNormXs[c], rowNormYs[r]), slotSize);
                    var slotUI = slotGo.GetComponent<ShopRackSlotUI>();
                    slotUI.slotType = SlotType.Inventory;
                    slotUI.slotIndex = invIndex;
                    slotUI.shopUI = this;
                    _gridSlotUIs.Add(slotUI);
                }
            }
        }

        private GameObject CreateSlotObject(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ShopRackSlotUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Slot base image (clickable raycast target)
            var baseImg = go.GetComponent<Image>();
            baseImg.color = new Color(0f, 0f, 0f, 0.05f);
            baseImg.raycastTarget = true;

            // Highlight overlay
            var hlGo = new GameObject("Highlight", typeof(RectTransform), typeof(Image), typeof(Outline));
            hlGo.transform.SetParent(go.transform, false);
            var hlRt = hlGo.GetComponent<RectTransform>();
            hlRt.anchorMin = Vector2.zero; hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = Vector2.zero; hlRt.offsetMax = Vector2.zero;
            var hlImg = hlGo.GetComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0f);
            hlImg.raycastTarget = false;
            var hlOutline = hlGo.GetComponent<Outline>();
            hlOutline.effectColor = new Color(1f, 0.85f, 0.35f, 0.8f);
            hlOutline.effectDistance = new Vector2(2f, 2f);
            hlOutline.enabled = false;

            // Item Icon
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(4f, 4f); iconRt.offsetMax = new Vector2(-4f, -4f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = new Color(1f, 1f, 1f, 0f);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Stack Count Text
            var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            countGo.transform.SetParent(go.transform, false);
            var countRt = countGo.GetComponent<RectTransform>();
            countRt.anchorMin = Vector2.zero; countRt.anchorMax = Vector2.one;
            countRt.offsetMin = new Vector2(2f, 2f); countRt.offsetMax = new Vector2(-4f, -2f);
            var countTxt = countGo.GetComponent<TextMeshProUGUI>();
            countTxt.fontSize = 12f;
            countTxt.fontStyle = FontStyles.Bold;
            countTxt.color = new Color(1f, 0.95f, 0.5f, 1f);
            countTxt.alignment = TextAlignmentOptions.BottomRight;
            countTxt.raycastTarget = false;
            var cShadow = countGo.GetComponent<Shadow>();
            cShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            cShadow.effectDistance = new Vector2(1.5f, -1.5f);

            hlGo.transform.SetAsLastSibling();

            var slotUI = go.GetComponent<ShopRackSlotUI>();
            slotUI.iconImage = iconImg;
            slotUI.countText = countTxt;
            slotUI.highlightImage = hlImg;
            slotUI.highlightOutline = hlOutline;

            return go;
        }

        private void BuildBackButton(Transform parent)
        {
            var btnGo = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 26f);
            rt.sizeDelta = new Vector2(170f, 50f);

            var img = btnGo.GetComponent<Image>();
            img.sprite = GetRoundedRectSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.16f, 0.22f, 0.32f, 0.92f);
            img.raycastTarget = true;

            var outline = btnGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.38f, 0.58f, 0.82f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.95f, 1f);
            btn.colors = colors;

            var txtGo = new GameObject("Txt_Back", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.text = "← BACK";
            txt.fontSize = 18f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0.92f, 0.96f, 1f, 1f);
            txt.raycastTarget = false;

            var shadow = txtGo.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySfx("click");
                Close();
            });
        }

        private void BuildShopPanel(Transform parent)
        {
            _shopRowViews.Clear();

            // Right Shop Container
            float panelW = 490f;
            float panelH = 590f;

            var panelGo = new GameObject("ShopGoodsPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelGo.transform.SetParent(parent, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0.5f);
            panelRt.anchorMax = new Vector2(1f, 0.5f);
            panelRt.pivot = new Vector2(1f, 0.5f);
            panelRt.anchoredPosition = new Vector2(-35f, -12f);
            panelRt.sizeDelta = new Vector2(panelW, panelH);

            var panelImg = panelGo.GetComponent<Image>();
            panelImg.sprite = GetRoundedRectSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.11f, 0.16f, 0.23f, 0.88f); // Sleek translucent blue-gray frame
            panelImg.raycastTarget = true;

            var outline = panelGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.58f, 0.82f, 0.70f); // Light cyan/slate glow
            outline.effectDistance = new Vector2(2f, -2f);

            // Header for Shop
            var headerGo = new GameObject("PanelHeader", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            headerGo.transform.SetParent(panelGo.transform, false);
            var hRt = headerGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = new Vector2(0f, -8f);
            hRt.sizeDelta = new Vector2(panelW - 24f, 32f);

            var hTxt = headerGo.GetComponent<TextMeshProUGUI>();
            hTxt.text = "MERCHANT WARES";
            hTxt.fontSize = 17f;
            hTxt.fontStyle = FontStyles.Bold;
            hTxt.alignment = TextAlignmentOptions.Center;
            hTxt.color = new Color(0.85f, 0.92f, 1f, 0.95f);
            hTxt.raycastTarget = false;

            var hShadow = headerGo.GetComponent<Shadow>();
            hShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            hShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Inner dark translucent blue-gray background for the list
            var listBgGo = new GameObject("ListBackground", typeof(RectTransform), typeof(Image));
            listBgGo.transform.SetParent(panelGo.transform, false);
            var listBgRt = listBgGo.GetComponent<RectTransform>();
            listBgRt.anchorMin = Vector2.zero;
            listBgRt.anchorMax = Vector2.one;
            listBgRt.offsetMin = new Vector2(10f, 10f);
            listBgRt.offsetMax = new Vector2(-10f, -42f); // Below header

            var listBgImg = listBgGo.GetComponent<Image>();
            listBgImg.sprite = GetRoundedRectSprite();
            listBgImg.type = Image.Type.Sliced;
            listBgImg.color = new Color(0.07f, 0.10f, 0.15f, 0.60f); // Recessed translucent area

            // Scroll View
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(listBgGo.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(4f, 6f);
            scrollRt.offsetMax = new Vector2(-4f, -6f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            // Content Container
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRt;

            // Instantiate rows for each shop item
            foreach (var item in _shopCatalog)
            {
                var rowView = CreateShopItemRow(contentGo.transform, item);
                _shopRowViews.Add(rowView);
            }
        }

        private ShopItemRowView CreateShopItemRow(Transform parent, ShopItemConfig config)
        {
            var rowGo = new GameObject($"ItemRow_{config.id}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement), typeof(Outline));
            rowGo.transform.SetParent(parent, false);

            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = 74f;
            le.minHeight = 74f;

            // Card background (translucent blue-gray with rounded corners)
            var cardImg = rowGo.GetComponent<Image>();
            cardImg.sprite = GetRoundedRectSprite();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.15f, 0.21f, 0.30f, 0.88f);
            cardImg.raycastTarget = true;

            var rowOutline = rowGo.GetComponent<Outline>();
            rowOutline.effectColor = new Color(0.28f, 0.42f, 0.58f, 0.50f);
            rowOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var cg = rowGo.GetComponent<CanvasGroup>();

            // 1. Icon Box on Left
            var iconBoxGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            iconBoxGo.transform.SetParent(rowGo.transform, false);
            var ibRt = iconBoxGo.GetComponent<RectTransform>();
            ibRt.anchorMin = new Vector2(0f, 0.5f);
            ibRt.anchorMax = new Vector2(0f, 0.5f);
            ibRt.pivot = new Vector2(0f, 0.5f);
            ibRt.anchoredPosition = new Vector2(10f, 0f);
            ibRt.sizeDelta = new Vector2(58f, 58f);

            var ibImg = iconBoxGo.GetComponent<Image>();
            ibImg.sprite = GetRoundedRectSprite();
            ibImg.type = Image.Type.Sliced;
            ibImg.color = new Color(0.09f, 0.12f, 0.17f, 0.95f);

            var ibOutline = iconBoxGo.GetComponent<Outline>();
            ibOutline.effectColor = new Color(0.22f, 0.32f, 0.45f, 0.6f);
            ibOutline.effectDistance = new Vector2(1f, -1f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(iconBoxGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(4f, 4f); iconRt.offsetMax = new Vector2(-4f, -4f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = ResolveItemIcon(config);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // 2. Middle Details (Name, Price & Stock)
            var detailsGo = new GameObject("Details", typeof(RectTransform));
            detailsGo.transform.SetParent(rowGo.transform, false);
            var dtRt = detailsGo.GetComponent<RectTransform>();
            dtRt.anchorMin = new Vector2(0f, 0f);
            dtRt.anchorMax = new Vector2(1f, 1f);
            dtRt.offsetMin = new Vector2(78f, 6f);
            dtRt.offsetMax = new Vector2(-120f, -6f);

            // Item Name
            var nameGo = new GameObject("Txt_Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(detailsGo.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(0f, 0f);
            nameRt.offsetMax = new Vector2(0f, 0f);
            var nameTxt = nameGo.GetComponent<TextMeshProUGUI>();
            nameTxt.text = config.displayName;
            nameTxt.fontSize = 16f;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color = new Color(0.92f, 0.96f, 1f, 1f); // Crisp ice-white
            nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            nameTxt.raycastTarget = false;

            // Price & Stock Row
            var priceRowGo = new GameObject("PriceRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            priceRowGo.transform.SetParent(detailsGo.transform, false);
            var prRt = priceRowGo.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0f, 0f);
            prRt.anchorMax = new Vector2(1f, 0.5f);
            prRt.offsetMin = new Vector2(0f, 0f);
            prRt.offsetMax = new Vector2(0f, 0f);

            var prLayout = priceRowGo.GetComponent<HorizontalLayoutGroup>();
            prLayout.spacing = 6f;
            prLayout.childAlignment = TextAnchor.MiddleLeft;
            prLayout.childControlWidth = false;
            prLayout.childControlHeight = false;

            var priceCoinGo = new GameObject("PriceCoin", typeof(RectTransform), typeof(Image));
            priceCoinGo.transform.SetParent(priceRowGo.transform, false);
            var pcRt = priceCoinGo.GetComponent<RectTransform>();
            pcRt.sizeDelta = new Vector2(22f, 22f);
            var pcImg = priceCoinGo.GetComponent<Image>();
            pcImg.sprite = _spGoldCoin;
            pcImg.preserveAspect = true;
            pcImg.raycastTarget = false;

            var priceTxtGo = new GameObject("Txt_Price", typeof(RectTransform), typeof(TextMeshProUGUI));
            priceTxtGo.transform.SetParent(priceRowGo.transform, false);
            var ptRt = priceTxtGo.GetComponent<RectTransform>();
            ptRt.sizeDelta = new Vector2(60f, 22f);
            var priceTxt = priceTxtGo.GetComponent<TextMeshProUGUI>();
            priceTxt.text = config.buyPrice.ToString();
            priceTxt.fontSize = 15f;
            priceTxt.fontStyle = FontStyles.Bold;
            priceTxt.color = new Color(1f, 0.86f, 0.40f, 1f); // Bright gold
            priceTxt.alignment = TextAlignmentOptions.MidlineLeft;
            priceTxt.raycastTarget = false;

            // Stock text
            var stockTxtGo = new GameObject("Txt_Stock", typeof(RectTransform), typeof(TextMeshProUGUI));
            stockTxtGo.transform.SetParent(priceRowGo.transform, false);
            var stRt = stockTxtGo.GetComponent<RectTransform>();
            stRt.sizeDelta = new Vector2(90f, 22f);
            var stockTxt = stockTxtGo.GetComponent<TextMeshProUGUI>();
            stockTxt.fontSize = 12f;
            stockTxt.fontStyle = FontStyles.Italic;
            stockTxt.color = new Color(0.65f, 0.78f, 0.88f, 0.85f); // Ice slate gray
            stockTxt.alignment = TextAlignmentOptions.MidlineLeft;
            stockTxt.raycastTarget = false;

            // 3. Info "(i)" Button
            var infoGo = new GameObject("Btn_Info", typeof(RectTransform), typeof(Image), typeof(Button));
            infoGo.transform.SetParent(rowGo.transform, false);
            var infoRt = infoGo.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(1f, 0.5f);
            infoRt.anchorMax = new Vector2(1f, 0.5f);
            infoRt.pivot = new Vector2(1f, 0.5f);
            infoRt.anchoredPosition = new Vector2(-88f, 0f);
            infoRt.sizeDelta = new Vector2(24f, 24f);

            var infoImg = infoGo.GetComponent<Image>();
            infoImg.sprite = GetRoundedRectSprite();
            infoImg.type = Image.Type.Sliced;
            infoImg.color = new Color(0.24f, 0.35f, 0.48f, 0.95f);

            var infoTxtGo = new GameObject("Txt_I", typeof(RectTransform), typeof(TextMeshProUGUI));
            infoTxtGo.transform.SetParent(infoGo.transform, false);
            var itRt = infoTxtGo.GetComponent<RectTransform>();
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;
            var itTxt = infoTxtGo.GetComponent<TextMeshProUGUI>();
            itTxt.text = "i";
            itTxt.fontSize = 15f;
            itTxt.fontStyle = FontStyles.Bold;
            itTxt.alignment = TextAlignmentOptions.Center;
            itTxt.color = Color.white;
            itTxt.raycastTarget = false;

            AddHoverHandlers(infoGo,
                () => ShowTooltip(config.displayName, config.category, config.description),
                () => HideTooltip()
            );

            // 4. "Buy" Button on Right (Procedural rounded, NO SPRITE)
            var buyGo = new GameObject("Btn_Buy", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            buyGo.transform.SetParent(rowGo.transform, false);
            var buyRt = buyGo.GetComponent<RectTransform>();
            buyRt.anchorMin = new Vector2(1f, 0.5f);
            buyRt.anchorMax = new Vector2(1f, 0.5f);
            buyRt.pivot = new Vector2(1f, 0.5f);
            buyRt.anchoredPosition = new Vector2(-10f, 0f);
            buyRt.sizeDelta = new Vector2(72f, 42f);

            var buyImg = buyGo.GetComponent<Image>();
            buyImg.sprite = GetRoundedRectSprite();
            buyImg.type = Image.Type.Sliced;
            buyImg.color = new Color(0.18f, 0.48f, 0.82f, 0.95f); // Glowing cyan-blue
            buyImg.raycastTarget = true;

            var buyOutline = buyGo.GetComponent<Outline>();
            buyOutline.effectColor = new Color(0.40f, 0.72f, 1.0f, 0.70f);
            buyOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var btnBuy = buyGo.GetComponent<Button>();
            btnBuy.targetGraphic = buyImg;

            var bColors = btnBuy.colors;
            bColors.normalColor = Color.white;
            bColors.highlightedColor = new Color(1.20f, 1.20f, 1.20f, 1f);
            bColors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            bColors.disabledColor = new Color(0.35f, 0.40f, 0.45f, 0.60f);
            btnBuy.colors = bColors;

            var buyTxtGo = new GameObject("Txt_Buy", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            buyTxtGo.transform.SetParent(buyGo.transform, false);
            var bTxtRt = buyTxtGo.GetComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero; bTxtRt.anchorMax = Vector2.one;
            bTxtRt.offsetMin = Vector2.zero; bTxtRt.offsetMax = Vector2.zero;
            var bTxt = buyTxtGo.GetComponent<TextMeshProUGUI>();
            bTxt.text = "BUY";
            bTxt.fontSize = 15f;
            bTxt.fontStyle = FontStyles.Bold;
            bTxt.alignment = TextAlignmentOptions.Center;
            bTxt.color = Color.white;
            bTxt.raycastTarget = false;
            var bShadow = buyTxtGo.GetComponent<Shadow>();
            bShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            bShadow.effectDistance = new Vector2(1.5f, -1.5f);

            btnBuy.onClick.AddListener(() =>
            {
                BuyItem(config);
            });

            return new ShopItemRowView
            {
                config = config,
                imgIcon = iconImg,
                txtName = nameTxt,
                txtPrice = priceTxt,
                txtStock = stockTxt,
                btnBuy = btnBuy,
                imgBuy = buyImg,
                txtBuyBtn = bTxt,
                canvasGroup = cg,
                rowTransform = rowGo.transform,
                isSoldOut = false
            };
        }

        private Sprite ResolveItemIcon(ShopItemConfig config)
        {
            if (config.customIcon != null) return config.customIcon;

            // Attempt from ItemRegistry definition (prioritizes canonical icons)
            string registryId = config.id;
            if (string.Equals(registryId, "HEAL", StringComparison.OrdinalIgnoreCase)) registryId = "potion_heal";
            else if (string.Equals(registryId, "RUNE", StringComparison.OrdinalIgnoreCase)) registryId = "rune_trident";

            var itemData = ItemRegistry.CreateItem(registryId);
            if (itemData != null && itemData.Icon != null) return itemData.Icon;

            // Fallback from Resources
            if (config.id == "HEAL" || config.id == "potion_heal")
                return Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean") ?? Resources.Load<Sprite>("CharacterStatus/Item_RedPotion");
            if (config.id == "potion_stamina")
                return Resources.Load<Sprite>("CharacterStatus/Item_GreenPotion");
            if (config.id == "golden_seed")
                return Resources.Load<Sprite>("CharacterStatus/Items/Item_GoldenSeed") ?? Resources.Load<Sprite>("CharacterStatus/Item_GoldCoin");
            if (config.id == "RUNE" || config.id == "rune_trident")
                return Resources.Load<Sprite>("CharacterStatus/Item_RuneTrident");
            if (config.id == "STR")
                return Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean") ?? Resources.Load<Sprite>("CharacterStatus/Item_RedPotion");
            if (config.id == "VIT")
                return Resources.Load<Sprite>("CharacterStatus/Item_GreenPotion");
            if (config.id == "DEX")
                return Resources.Load<Sprite>("CharacterStatus/Item_BluePotion");
            if (config.id == "AGI")
                return Resources.Load<Sprite>("CharacterStatus/Item_BluePotion");
            if (config.id == "bread")
                return Resources.Load<Sprite>("CharacterStatus/Item_Bread");
            if (config.id == "smoke_bomb")
                return Resources.Load<Sprite>("CharacterStatus/Item_Scroll");

            return _spGoldCoin;
        }

        #region Sell System UI & Logic

        private void BuildSellPanel(Transform parent)
        {
            _sellPanelGo = new GameObject("SellOfferModal", typeof(RectTransform), typeof(Image), typeof(Outline));
            _sellPanelGo.transform.SetParent(parent, false);
            _sellPanelRect = _sellPanelGo.GetComponent<RectTransform>();
            _sellPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _sellPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _sellPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _sellPanelRect.anchoredPosition = new Vector2(-40f, 60f); // Center area above Back button
            _sellPanelRect.sizeDelta = new Vector2(285f, 290f);

            var bg = _sellPanelGo.GetComponent<Image>();
            bg.sprite = GetRoundedRectSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.11f, 0.16f, 0.23f, 0.95f);
            bg.raycastTarget = true;

            var outline = _sellPanelGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.58f, 0.82f, 0.80f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Title Header
            var titleGo = new GameObject("Txt_SellTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_sellPanelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(240f, 26f);
            var titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.text = "SELL TO MERCHANT";
            titleTxt.fontSize = 15f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.85f, 0.92f, 1f, 1f);
            titleTxt.alignment = TextAlignmentOptions.Center;

            // Close button [x]
            var closeGo = new GameObject("Btn_CloseSell", typeof(RectTransform), typeof(Button), typeof(TextMeshProUGUI));
            closeGo.transform.SetParent(_sellPanelGo.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeRt.sizeDelta = new Vector2(24f, 24f);
            var closeTxt = closeGo.GetComponent<TextMeshProUGUI>();
            closeTxt.text = "X";
            closeTxt.fontSize = 15f;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = new Color(0.75f, 0.82f, 0.90f, 1f);
            closeTxt.alignment = TextAlignmentOptions.Center;
            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySfx("click");
                HideSellPanel();
            });

            // Big Item Icon Box
            var iconBoxGo = new GameObject("SellIconBox", typeof(RectTransform), typeof(Image), typeof(Outline));
            iconBoxGo.transform.SetParent(_sellPanelGo.transform, false);
            var ibRt = iconBoxGo.GetComponent<RectTransform>();
            ibRt.anchorMin = new Vector2(0.5f, 1f); ibRt.anchorMax = new Vector2(0.5f, 1f);
            ibRt.pivot = new Vector2(0.5f, 1f);
            ibRt.anchoredPosition = new Vector2(0f, -42f);
            ibRt.sizeDelta = new Vector2(62f, 62f);
            var ibImg = iconBoxGo.GetComponent<Image>();
            ibImg.sprite = GetRoundedRectSprite();
            ibImg.type = Image.Type.Sliced;
            ibImg.color = new Color(0.08f, 0.12f, 0.17f, 0.95f);

            var ibOutline = iconBoxGo.GetComponent<Outline>();
            ibOutline.effectColor = new Color(0.25f, 0.38f, 0.52f, 0.6f);
            ibOutline.effectDistance = new Vector2(1f, -1f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(iconBoxGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(4f, 4f); iconRt.offsetMax = new Vector2(-4f, -4f);
            _imgSellItemIcon = iconGo.GetComponent<Image>();
            _imgSellItemIcon.preserveAspect = true;

            // Stack text
            var stockGo = new GameObject("Txt_Stack", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            stockGo.transform.SetParent(iconBoxGo.transform, false);
            var sRt = stockGo.GetComponent<RectTransform>();
            sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
            sRt.offsetMin = new Vector2(2f, 2f); sRt.offsetMax = new Vector2(-4f, -2f);
            _txtSellItemStock = stockGo.GetComponent<TextMeshProUGUI>();
            _txtSellItemStock.fontSize = 12f;
            _txtSellItemStock.fontStyle = FontStyles.Bold;
            _txtSellItemStock.color = new Color(1f, 0.95f, 0.5f, 1f);
            _txtSellItemStock.alignment = TextAlignmentOptions.BottomRight;

            // Item Name
            var nameGo = new GameObject("Txt_ItemName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(_sellPanelGo.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f); nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -112f);
            nameRt.sizeDelta = new Vector2(260f, 24f);
            _txtSellItemName = nameGo.GetComponent<TextMeshProUGUI>();
            _txtSellItemName.fontSize = 15f;
            _txtSellItemName.fontStyle = FontStyles.Bold;
            _txtSellItemName.color = Color.white;
            _txtSellItemName.alignment = TextAlignmentOptions.Center;

            // Unit Price Line (Sell price 75% of buy)
            var unitGo = new GameObject("Txt_UnitPrice", typeof(RectTransform), typeof(TextMeshProUGUI));
            unitGo.transform.SetParent(_sellPanelGo.transform, false);
            var unitRt = unitGo.GetComponent<RectTransform>();
            unitRt.anchorMin = new Vector2(0f, 1f); unitRt.anchorMax = new Vector2(1f, 1f);
            unitRt.pivot = new Vector2(0.5f, 1f);
            unitRt.anchoredPosition = new Vector2(0f, -138f);
            unitRt.sizeDelta = new Vector2(260f, 22f);
            _txtSellUnitPrice = unitGo.GetComponent<TextMeshProUGUI>();
            _txtSellUnitPrice.fontSize = 14f;
            _txtSellUnitPrice.fontStyle = FontStyles.Normal;
            _txtSellUnitPrice.color = new Color(1f, 0.88f, 0.45f, 1f);
            _txtSellUnitPrice.alignment = TextAlignmentOptions.Center;

            // Total Value Line
            var totalGo = new GameObject("Txt_TotalPrice", typeof(RectTransform), typeof(TextMeshProUGUI));
            totalGo.transform.SetParent(_sellPanelGo.transform, false);
            var totRt = totalGo.GetComponent<RectTransform>();
            totRt.anchorMin = new Vector2(0f, 1f); totRt.anchorMax = new Vector2(1f, 1f);
            totRt.pivot = new Vector2(0.5f, 1f);
            totRt.anchoredPosition = new Vector2(0f, -160f);
            totRt.sizeDelta = new Vector2(260f, 22f);
            _txtSellTotalPrice = totalGo.GetComponent<TextMeshProUGUI>();
            _txtSellTotalPrice.fontSize = 13f;
            _txtSellTotalPrice.fontStyle = FontStyles.Italic;
            _txtSellTotalPrice.color = new Color(0.85f, 0.80f, 0.70f, 1f);
            _txtSellTotalPrice.alignment = TextAlignmentOptions.Center;

            // Not for sale message banner
            _sellNotForSaleGo = new GameObject("NotForSaleBanner", typeof(RectTransform), typeof(TextMeshProUGUI));
            _sellNotForSaleGo.transform.SetParent(_sellPanelGo.transform, false);
            var nfsRt = _sellNotForSaleGo.GetComponent<RectTransform>();
            nfsRt.anchorMin = new Vector2(0f, 0f); nfsRt.anchorMax = new Vector2(1f, 0f);
            nfsRt.pivot = new Vector2(0.5f, 0f);
            nfsRt.anchoredPosition = new Vector2(0f, 24f);
            nfsRt.sizeDelta = new Vector2(260f, 50f);
            var nfsTxt = _sellNotForSaleGo.GetComponent<TextMeshProUGUI>();
            nfsTxt.text = "Merchant is not interested in buying this item.";
            nfsTxt.fontSize = 13f;
            nfsTxt.fontStyle = FontStyles.Italic;
            nfsTxt.color = new Color(0.95f, 0.45f, 0.45f, 1f);
            nfsTxt.alignment = TextAlignmentOptions.Center;
            nfsTxt.textWrappingMode = TextWrappingModes.Normal;

            // "Sell 1" Button
            var s1Go = new GameObject("Btn_SellOne", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            s1Go.transform.SetParent(_sellPanelGo.transform, false);
            var s1Rt = s1Go.GetComponent<RectTransform>();
            s1Rt.anchorMin = new Vector2(0.5f, 0f); s1Rt.anchorMax = new Vector2(0.5f, 0f);
            s1Rt.pivot = new Vector2(0.5f, 0f);
            s1Rt.anchoredPosition = new Vector2(-60f, 16f);
            s1Rt.sizeDelta = new Vector2(110f, 38f);
            var s1Img = s1Go.GetComponent<Image>();
            s1Img.sprite = GetRoundedRectSprite();
            s1Img.type = Image.Type.Sliced;
            s1Img.color = new Color(0.88f, 0.42f, 0.12f, 1f);
            s1Img.raycastTarget = true;
            var s1Outline = s1Go.GetComponent<Outline>();
            s1Outline.effectColor = new Color(0.55f, 0.20f, 0.05f, 0.9f);
            s1Outline.effectDistance = new Vector2(1.5f, -1.5f);

            _btnSellOne = s1Go.GetComponent<Button>();
            _btnSellOne.targetGraphic = s1Img;
            var s1Colors = _btnSellOne.colors;
            s1Colors.normalColor = Color.white;
            s1Colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            s1Colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            _btnSellOne.colors = s1Colors;

            var s1TxtGo = new GameObject("Txt_Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            s1TxtGo.transform.SetParent(s1Go.transform, false);
            var s1tRt = s1TxtGo.GetComponent<RectTransform>();
            s1tRt.anchorMin = Vector2.zero; s1tRt.anchorMax = Vector2.one;
            s1tRt.offsetMin = Vector2.zero; s1tRt.offsetMax = Vector2.zero;
            _txtSellOneLabel = s1TxtGo.GetComponent<TextMeshProUGUI>();
            _txtSellOneLabel.text = "Sell 1";
            _txtSellOneLabel.fontSize = 14f;
            _txtSellOneLabel.fontStyle = FontStyles.Bold;
            _txtSellOneLabel.color = Color.white;
            _txtSellOneLabel.alignment = TextAlignmentOptions.Center;
            var s1Shadow = s1TxtGo.GetComponent<Shadow>();
            s1Shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            s1Shadow.effectDistance = new Vector2(1.5f, -1.5f);

            _btnSellOne.onClick.AddListener(() =>
            {
                ExecuteSell(_selectedSlotType, _selectedSlotIndex, false);
            });

            // "Sell All" Button
            var saGo = new GameObject("Btn_SellAll", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            saGo.transform.SetParent(_sellPanelGo.transform, false);
            var saRt = saGo.GetComponent<RectTransform>();
            saRt.anchorMin = new Vector2(0.5f, 0f); saRt.anchorMax = new Vector2(0.5f, 0f);
            saRt.pivot = new Vector2(0.5f, 0f);
            saRt.anchoredPosition = new Vector2(60f, 16f);
            saRt.sizeDelta = new Vector2(110f, 38f);
            var saImg = saGo.GetComponent<Image>();
            saImg.sprite = GetRoundedRectSprite();
            saImg.type = Image.Type.Sliced;
            saImg.color = new Color(0.20f, 0.58f, 0.30f, 1f);
            saImg.raycastTarget = true;
            var saOutline = saGo.GetComponent<Outline>();
            saOutline.effectColor = new Color(0.10f, 0.32f, 0.12f, 0.9f);
            saOutline.effectDistance = new Vector2(1.5f, -1.5f);

            _btnSellAll = saGo.GetComponent<Button>();
            _btnSellAll.targetGraphic = saImg;
            var saColors = _btnSellAll.colors;
            saColors.normalColor = Color.white;
            saColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            saColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            _btnSellAll.colors = saColors;

            var saTxtGo = new GameObject("Txt_Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            saTxtGo.transform.SetParent(saGo.transform, false);
            var satRt = saTxtGo.GetComponent<RectTransform>();
            satRt.anchorMin = Vector2.zero; satRt.anchorMax = Vector2.one;
            satRt.offsetMin = Vector2.zero; satRt.offsetMax = Vector2.zero;
            _txtSellAllLabel = saTxtGo.GetComponent<TextMeshProUGUI>();
            _txtSellAllLabel.text = "Sell All";
            _txtSellAllLabel.fontSize = 14f;
            _txtSellAllLabel.fontStyle = FontStyles.Bold;
            _txtSellAllLabel.color = Color.white;
            _txtSellAllLabel.alignment = TextAlignmentOptions.Center;
            var saShadow = saTxtGo.GetComponent<Shadow>();
            saShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            saShadow.effectDistance = new Vector2(1.5f, -1.5f);

            _btnSellAll.onClick.AddListener(() =>
            {
                ExecuteSell(_selectedSlotType, _selectedSlotIndex, true);
            });

            _sellPanelGo.SetActive(false);
        }

        public void OnRackSlotClicked(SlotType slotType, int slotIndex)
        {
            var inv = Inventory;
            if (inv == null) return;

            var item = inv.GetSlot(slotType, slotIndex);
            if (item == null || item.count <= 0)
            {
                HideSellPanel();
                return;
            }

            // Play select click
            AudioManager.Instance?.PlaySfx("click");

            _selectedSlotType = slotType;
            _selectedSlotIndex = slotIndex;

            // Highlight selected slot on rack
            ClearSlotSelections();
            var targetList = slotType == SlotType.Inventory ? _gridSlotUIs : _quickSlotUIs;
            if (slotIndex >= 0 && slotIndex < targetList.Count)
            {
                targetList[slotIndex].SetSelectedForSell(true);
            }

            UpdateSellPanelContent(slotType, slotIndex, item);
            _sellPanelGo.SetActive(true);
        }

        private void ClearSlotSelections()
        {
            foreach (var slot in _gridSlotUIs) slot.SetSelectedForSell(false);
            foreach (var slot in _quickSlotUIs) slot.SetSelectedForSell(false);
        }

        private void UpdateSellPanelContent(SlotType slotType, int slotIndex, InventoryItemData item)
        {
            _imgSellItemIcon.sprite = item.Icon;
            _txtSellItemStock.text = item.count > 1 ? item.count.ToString() : "";
            _txtSellItemName.text = item.name;

            int unitSellPrice = GetSellPrice(item.id);
            bool canSell = unitSellPrice > 0;

            if (canSell)
            {
                _sellNotForSaleGo.SetActive(false);
                _btnSellOne.gameObject.SetActive(true);
                _btnSellAll.gameObject.SetActive(item.count > 1);

                _txtSellUnitPrice.text = $"Sell Price: {unitSellPrice} Gold (75%)";

                if (item.count > 1)
                {
                    _txtSellTotalPrice.gameObject.SetActive(true);
                    _txtSellTotalPrice.text = $"Total Value: {unitSellPrice * item.count} Gold ({item.count}x)";
                    _txtSellOneLabel.text = "Sell 1";
                    _txtSellAllLabel.text = $"Sell All ({item.count})";
                    _btnSellOne.GetComponent<RectTransform>().anchoredPosition = new Vector2(-60f, 16f);
                }
                else
                {
                    _txtSellTotalPrice.gameObject.SetActive(false);
                    _txtSellOneLabel.text = "Sell";
                    // Center the single sell button
                    _btnSellOne.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 16f);
                }
            }
            else
            {
                _txtSellUnitPrice.text = "Not for sale";
                _txtSellTotalPrice.gameObject.SetActive(false);
                _sellNotForSaleGo.SetActive(true);
                _btnSellOne.gameObject.SetActive(false);
                _btnSellAll.gameObject.SetActive(false);
            }
        }

        public void HideSellPanel()
        {
            if (_sellPanelGo != null) _sellPanelGo.SetActive(false);
            _selectedSlotIndex = -1;
            ClearSlotSelections();
        }

        public int GetSellPrice(string itemId)
        {
            var config = FindCatalogItem(itemId);
            if (config == null) return -1; // Merchant does not sell/buy this item

            if (config.customSellPrice > 0) return config.customSellPrice;

            // 75% of buy price
            return Mathf.Max(1, Mathf.RoundToInt(config.buyPrice * _sellPriceMultiplier));
        }

        private ShopItemConfig FindCatalogItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            foreach (var item in _shopCatalog)
            {
                if (string.Equals(item.id, itemId, StringComparison.OrdinalIgnoreCase)) return item;
                // Special alias mapping
                if (string.Equals(itemId, "potion_heal", StringComparison.OrdinalIgnoreCase) && string.Equals(item.id, "HEAL", StringComparison.OrdinalIgnoreCase)) return item;
                if (string.Equals(itemId, "HEAL", StringComparison.OrdinalIgnoreCase) && string.Equals(item.id, "potion_heal", StringComparison.OrdinalIgnoreCase)) return item;
                if (string.Equals(itemId, "rune_trident", StringComparison.OrdinalIgnoreCase) && string.Equals(item.id, "RUNE", StringComparison.OrdinalIgnoreCase)) return item;
                if (string.Equals(itemId, "RUNE", StringComparison.OrdinalIgnoreCase) && string.Equals(item.id, "rune_trident", StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        public void ExecuteSell(SlotType slotType, int slotIndex, bool sellAll)
        {
            var inv = Inventory;
            var player = Player;
            if (inv == null || player == null) return;

            var item = inv.GetSlot(slotType, slotIndex);
            if (item == null || item.count <= 0)
            {
                HideSellPanel();
                return;
            }

            int unitPrice = GetSellPrice(item.id);
            if (unitPrice <= 0)
            {
                ShowToast("Cannot sell this item.", new Color(1f, 0.4f, 0.4f));
                return;
            }

            int amountToSell = sellAll ? item.count : 1;
            int totalGold = unitPrice * amountToSell;

            player.AddGold(totalGold);
            AudioManager.Instance?.PlaySfx("click");

            if (sellAll || item.count <= amountToSell)
            {
                inv.SetSlot(slotType, slotIndex, null);
            }
            else
            {
                item.count -= amountToSell;
                inv.SetSlot(slotType, slotIndex, item);
            }

            // Sync healing potions counter if heal potion was sold
            if (item.id == "potion_heal" || item.id == "HEAL")
            {
                player.SyncHealingPotions(inv.CountItem("potion_heal"));
            }

            GameManager.Instance?.Capture();
            ShowToast($"Sold {amountToSell}x {item.name} for {totalGold} Gold!", new Color(0.35f, 1f, 0.45f));

            Refresh();

            // Refresh sell panel or hide if empty
            var updatedItem = inv.GetSlot(slotType, slotIndex);
            if (updatedItem != null && updatedItem.count > 0)
            {
                UpdateSellPanelContent(slotType, slotIndex, updatedItem);
            }
            else
            {
                HideSellPanel();
            }
        }

        #endregion

        #region Toast & Tooltips

        private void BuildToastBanner(Transform parent)
        {
            var toastGo = new GameObject("ToastBanner", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Shadow));
            toastGo.transform.SetParent(parent, false);
            var rt = toastGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -32f);
            rt.sizeDelta = new Vector2(480f, 44f);

            _txtToastMessage = toastGo.GetComponent<TextMeshProUGUI>();
            _txtToastMessage.fontSize = 20f;
            _txtToastMessage.fontStyle = FontStyles.Bold;
            _txtToastMessage.alignment = TextAlignmentOptions.Center;
            _txtToastMessage.color = new Color(1f, 1f, 1f, 0f);
            _txtToastMessage.raycastTarget = false;

            var shadow = toastGo.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void BuildTooltipBox(Transform parent)
        {
            _tooltipGo = new GameObject("ShopTooltip", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _tooltipGo.transform.SetParent(parent, false);
            _tooltipRect = _tooltipGo.GetComponent<RectTransform>();
            _tooltipRect.pivot = new Vector2(0f, 1f);
            _tooltipRect.sizeDelta = new Vector2(260f, 120f);

            var bg = _tooltipGo.GetComponent<Image>();
            bg.sprite = GetRoundedRectSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.10f, 0.15f, 0.22f, 0.96f);
            bg.raycastTarget = false;

            var outline = _tooltipGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.58f, 0.82f, 0.80f);
            outline.effectDistance = new Vector2(2f, -2f);

            var vGroup = _tooltipGo.GetComponent<VerticalLayoutGroup>();
            vGroup.padding = new RectOffset(12, 12, 10, 10);
            vGroup.spacing = 4f;
            vGroup.childControlWidth = true;
            vGroup.childForceExpandWidth = true;
            vGroup.childControlHeight = false;
            vGroup.childForceExpandHeight = false;

            var fitter = _tooltipGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleGo = new GameObject("Txt_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_tooltipGo.transform, false);
            _txtTooltipTitle = titleGo.GetComponent<TextMeshProUGUI>();
            _txtTooltipTitle.fontSize = 16f;
            _txtTooltipTitle.fontStyle = FontStyles.Bold;
            _txtTooltipTitle.color = new Color(0.85f, 0.92f, 1f, 1f);
            _txtTooltipTitle.raycastTarget = false;

            // Category
            var catGo = new GameObject("Txt_Category", typeof(RectTransform), typeof(TextMeshProUGUI));
            catGo.transform.SetParent(_tooltipGo.transform, false);
            _txtTooltipCategory = catGo.GetComponent<TextMeshProUGUI>();
            _txtTooltipCategory.fontSize = 12f;
            _txtTooltipCategory.fontStyle = FontStyles.Italic;
            _txtTooltipCategory.color = new Color(0.75f, 0.70f, 0.65f, 1f);
            _txtTooltipCategory.raycastTarget = false;

            // Description
            var descGo = new GameObject("Txt_Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(_tooltipGo.transform, false);
            _txtTooltipDesc = descGo.GetComponent<TextMeshProUGUI>();
            _txtTooltipDesc.fontSize = 13f;
            _txtTooltipDesc.color = new Color(0.92f, 0.90f, 0.85f, 1f);
            _txtTooltipDesc.raycastTarget = false;
            _txtTooltipDesc.textWrappingMode = TextWrappingModes.Normal;

            _tooltipGo.SetActive(false);
        }

        public void ShowTooltip(string title, string category, string desc)
        {
            if (_tooltipGo == null) return;
            _txtTooltipTitle.text = title;
            _txtTooltipCategory.text = category;
            _txtTooltipDesc.text = desc;
            _tooltipGo.SetActive(true);
            UpdateTooltipPosition();
        }

        public void HideTooltip()
        {
            if (_tooltipGo != null) _tooltipGo.SetActive(false);
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipGo == null || !_tooltipGo.activeSelf) return;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Vector2 offset = new Vector2(18f, -18f);

            float screenW = Screen.width;
            float screenH = Screen.height;

            if (mousePos.x + 280f > screenW) offset.x = -280f;
            if (mousePos.y - 140f < 0) offset.y = 140f;

            _tooltipRect.position = mousePos + offset;
        }

        private void AddHoverHandlers(GameObject target, Action onEnter, Action onExit)
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

        #region Purchase Logic

        public bool HasInventorySpace(string itemId)
        {
            var inv = Inventory;
            if (inv == null) return false;
            for (int i = 0; i < InventoryManager.QuickSlotCount; i++)
            {
                var it = inv.GetSlot(SlotType.QuickSlot, i);
                if (it == null || it.count <= 0) return true;
                if (string.Equals(it.id, itemId, StringComparison.OrdinalIgnoreCase) && it.count < it.maxStack) return true;
            }
            for (int i = 0; i < InventoryManager.InventorySlotCount; i++)
            {
                var it = inv.GetSlot(SlotType.Inventory, i);
                if (it == null || it.count <= 0) return true;
                if (string.Equals(it.id, itemId, StringComparison.OrdinalIgnoreCase) && it.count < it.maxStack) return true;
            }
            return false;
        }

        public void BuyItem(ShopItemConfig config)
        {
            if (config == null) return;

            config.EnsureStockInitialized();

            // Check stock limit
            if (config.initialStock >= 0 && config.currentStock <= 0)
            {
                ShowToast("Item is Sold Out!", new Color(1f, 0.4f, 0.4f));
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            var player = Player;
            if (player == null) return;

            if (player.Gold < config.buyPrice)
            {
                ShowToast("Not enough Gold!", new Color(1f, 0.35f, 0.35f));
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            // Item-type specific validation
            if (config.itemType == ShopItemType.InventoryItem)
            {
                string targetItemId = (config.id == "HEAL") ? "potion_heal" : config.id;
                if (!HasInventorySpace(targetItemId))
                {
                    ShowToast("Inventory is full!", new Color(1f, 0.75f, 0.35f));
                    AudioManager.Instance?.PlaySfx("click");
                    return;
                }
            }
            else if (config.itemType == ShopItemType.RuneReward)
            {
                if (DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasReward(config.runeIndex))
                {
                    ShowToast("Already acquired this Demonic Rune!", new Color(1f, 0.75f, 0.35f));
                    AudioManager.Instance?.PlaySfx("click");
                    return;
                }
            }

            // Deduct Gold
            if (!player.TrySpendGold(config.buyPrice))
            {
                ShowToast("Not enough Gold!", new Color(1f, 0.35f, 0.35f));
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            // Execute purchase effect
            if (config.itemType == ShopItemType.InventoryItem)
            {
                string targetItemId = (config.id == "HEAL") ? "potion_heal" : config.id;
                var createdItem = ItemRegistry.CreateItem(targetItemId);
                if (createdItem != null && Inventory != null)
                {
                    Inventory.AddItem(createdItem);
                    if (targetItemId == "potion_heal")
                    {
                        player.SyncHealingPotions(Inventory.CountItem("potion_heal"));
                    }
                }
            }
            else if (config.itemType == ShopItemType.StatPotion)
            {
                player.AddStatPotion(config.statName);
            }
            else if (config.itemType == ShopItemType.RuneReward)
            {
                DemonRuneManager.Instance?.DropRune(config.runeIndex, player.transform.position);
            }

            // Decrement stock if limited
            if (config.initialStock >= 0)
            {
                config.currentStock = Mathf.Max(0, config.currentStock - 1);
            }

            AudioManager.Instance?.PlaySfx("click");
            GameManager.Instance?.Capture();
            ShowToast($"Purchased {config.displayName}!", new Color(0.35f, 1f, 0.45f));
            Refresh();
        }

        // Backward compatibility method
        public bool TryPurchase(string item)
        {
            var match = FindCatalogItem(item);
            if (match != null)
            {
                BuyItem(match);
                return true;
            }
            return false;
        }

        #endregion

        public void Refresh()
        {
            var player = Player;

            // 1. Update Currency Text
            if (_txtGold != null)
            {
                int gold = player != null ? player.Gold : 0;
                _txtGold.text = gold.ToString("N0");
            }

            // 2. Update Left Wooden Rack Slots
            var inv = Inventory;
            if (inv != null)
            {
                for (int i = 0; i < _gridSlotUIs.Count; i++)
                {
                    var it = inv.GetSlot(SlotType.Inventory, i);
                    _gridSlotUIs[i].UpdateDisplay(it);
                }
                for (int i = 0; i < _quickSlotUIs.Count; i++)
                {
                    var it = inv.GetSlot(SlotType.QuickSlot, i);
                    _quickSlotUIs[i].UpdateDisplay(it);
                }
            }

            // 3. Update Shop Rows
            foreach (var row in _shopRowViews)
            {
                var cfg = row.config;
                cfg.EnsureStockInitialized();

                // Stock & Sold-out calculation
                bool isSoldOut = cfg.initialStock >= 0 && cfg.currentStock <= 0;
                if (cfg.itemType == ShopItemType.RuneReward && DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasReward(cfg.runeIndex))
                {
                    isSoldOut = true;
                }
                row.isSoldOut = isSoldOut;

                if (isSoldOut)
                {
                    row.txtStock.text = "<color=#E85D5D>Sold Out</color>";
                }
                else if (cfg.initialStock >= 0)
                {
                    row.txtStock.text = $"Stock: {cfg.currentStock}";
                }
                else
                {
                    row.txtStock.text = "";
                }

                // Can buy validation
                bool canAfford = player != null && player.Gold >= cfg.buyPrice;
                bool isBlocked = isSoldOut;

                bool interactable = canAfford && !isBlocked;
                row.btnBuy.interactable = interactable;
                row.canvasGroup.alpha = interactable ? 1.0f : 0.70f;
                row.txtBuyBtn.text = isSoldOut ? "SOLD" : "BUY";
                row.imgBuy.color = isSoldOut 
                    ? new Color(0.20f, 0.25f, 0.32f, 0.60f) 
                    : (interactable ? new Color(0.18f, 0.48f, 0.82f, 0.95f) : new Color(0.18f, 0.30f, 0.48f, 0.65f));
            }

            // 4. Push Sold-Out Items to the bottom of the list
            foreach (var row in _shopRowViews)
            {
                if (row.isSoldOut && row.rowTransform != null)
                {
                    row.rowTransform.SetAsLastSibling();
                }
            }
        }

        public void ShowToast(string message, Color color)
        {
            if (_txtToastMessage == null) return;
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ToastRoutine(message, color));
        }

        private IEnumerator ToastRoutine(string message, Color color)
        {
            _txtToastMessage.text = message;
            _txtToastMessage.color = color;

            yield return new WaitForSecondsRealtime(2.2f);

            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(1f, 0f, elapsed / 0.4f);
                _txtToastMessage.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            _txtToastMessage.text = "";
            _toastCoroutine = null;
        }

        private void Update()
        {
            if (_panel != null)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    if (_sellPanelGo != null && _sellPanelGo.activeSelf)
                    {
                        HideSellPanel();
                    }
                    else
                    {
                        Close();
                    }
                    return;
                }
                UpdateTooltipPosition();
                UpdateCursorFollower();
            }
        }

        public void Close()
        {
            bool wasOpen = _panel != null;
            HideTooltip();
            HideSellPanel();

            var inv = Inventory;
            if (inv != null && inv.CursorHeldItem != null)
            {
                inv.Close(); // Safely return held item to inventory slots
            }

            if (_panel != null)
            {
                if (Application.isPlaying) Destroy(_panel);
                else DestroyImmediate(_panel);
            }
            _panel = null;

            if (Application.isPlaying) Time.timeScale = 1f;
            GameManager.Instance?.SetInputBlocked(false);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= Refresh;
            }

            if (wasOpen) Closed?.Invoke();
        }
    }

    public class ShopRackSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SlotType slotType;
        public int slotIndex;
        public Image iconImage;
        public TextMeshProUGUI countText;
        public Image highlightImage;
        public Outline highlightOutline;
        public ShopUI shopUI;
        public bool isSelectedForSell;

        private bool _dragging;

        public void SetHighlight(bool active)
        {
            if (isSelectedForSell) return;
            if (highlightImage != null)
                highlightImage.color = active ? new Color(1f, 0.95f, 0.65f, 0.35f) : new Color(1f, 1f, 1f, 0f);
            if (highlightOutline != null)
            {
                highlightOutline.effectColor = new Color(1f, 0.85f, 0.35f, 0.8f);
                highlightOutline.enabled = active;
            }
        }

        public void SetSelectedForSell(bool selected)
        {
            isSelectedForSell = selected;
            if (highlightImage != null)
                highlightImage.color = selected ? new Color(1f, 0.88f, 0.35f, 0.22f) : new Color(1f, 1f, 1f, 0f);
            if (highlightOutline != null)
            {
                highlightOutline.effectColor = selected ? new Color(1f, 0.90f, 0.25f, 1f) : new Color(1f, 0.85f, 0.35f, 0.8f);
                highlightOutline.effectDistance = selected ? new Vector2(2.5f, 2.5f) : new Vector2(2f, 2f);
                highlightOutline.enabled = selected;
            }
        }

        public void UpdateDisplay(InventoryItemData item)
        {
            if (item != null && item.count > 0 && item.Icon != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = item.Icon;
                    iconImage.color = Color.white;
                }
                if (countText != null)
                {
                    countText.text = item.count > 1 ? item.count.ToString() : "";
                }
            }
            else
            {
                if (iconImage != null)
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0f);
                }
                if (countText != null)
                {
                    countText.text = "";
                }
                SetSelectedForSell(false);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            var inv = shopUI != null ? shopUI.Inventory : InventoryManager.Instance;
            if (inv == null || inv.CursorHeldItem != null || inv.GetSlot(slotType, slotIndex) == null) return;

            _dragging = true;
            eventData.eligibleForClick = false;
            inv.HandleLeftClick(slotType, slotIndex, false, shopUI != null ? shopUI.Player : FindAnyObjectByType<PlayerStats>());
            shopUI?.HideTooltip();
            shopUI?.HideSellPanel();
            shopUI?.Refresh();
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            eventData.eligibleForClick = false;
            var inv = shopUI != null ? shopUI.Inventory : InventoryManager.Instance;
            if (inv == null || inv.CursorHeldItem == null) return;

            var hit = eventData.pointerCurrentRaycast.gameObject;
            var target = hit != null ? hit.GetComponentInParent<ShopRackSlotUI>() : null;
            if (target != null)
            {
                inv.HandleLeftClick(target.slotType, target.slotIndex, false, shopUI != null ? shopUI.Player : FindAnyObjectByType<PlayerStats>());
                AudioManager.Instance?.PlaySfx("click");
            }
            shopUI?.Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                // Right Click -> Open Sell Offer Modal
                shopUI?.OnRackSlotClicked(slotType, slotIndex);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                // Left Click -> Pick / Place / Swap / Shift-QuickMove
                var inv = shopUI != null ? shopUI.Inventory : InventoryManager.Instance;
                if (inv != null)
                {
                    bool isShift = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
                    var player = shopUI != null ? shopUI.Player : FindAnyObjectByType<PlayerStats>();
                    inv.HandleLeftClick(slotType, slotIndex, isShift, player);
                    AudioManager.Instance?.PlaySfx("click");
                    shopUI?.HideSellPanel();
                    shopUI?.Refresh();
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlight(true);
            var inv = shopUI != null ? shopUI.Inventory : (InventoryManager.Instance ?? FindAnyObjectByType<InventoryManager>());
            if (inv != null)
            {
                var item = inv.GetSlot(slotType, slotIndex);
                if (item != null)
                {
                    string desc = item.description;
                    if (item.count > 1)
                    {
                        desc += $"\n<color=#FFD56B>Current Stack: {item.count} / {item.maxStack}</color>";
                    }
                    int sellPrice = shopUI != null ? shopUI.GetSellPrice(item.id) : -1;
                    if (sellPrice > 0)
                    {
                        desc += $"\n<color=#6DE383>Sell Value: {sellPrice} Gold each (75%)</color>";
                    }
                    else
                    {
                        desc += "\n<color=#A0AAB5>Merchant does not buy this item</color>";
                    }
                    desc += "\n<color=#98E498>[L-Click: Move / Organize]</color>  <color=#85C1E9>[R-Click: Sell to Shop]</color>";
                    shopUI?.ShowTooltip(item.name, item.typeName, desc);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
            shopUI?.HideTooltip();
        }
    }
}
