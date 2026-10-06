using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using TheLastKnight.Stats;

namespace TheLastKnight.UI
{
    /// <summary>
    /// In-game HUD overlay displayed on the far-left edge of the screen.
    /// Shows active buffs/status effects in Minecraft style with icons, names,
    /// and countdown timers, positioned cleanly between the top HUD (HP/Stamina/Lv)
    /// and the bottom-left skill cooldown HUD without any overlap.
    /// Hovering over any buff displays a small tooltip window with detailed info.
    /// </summary>
    [DefaultExecutionOrder(-390)]
    public class PlayerBuffHUD : MonoBehaviour
    {
        public static PlayerBuffHUD Instance { get; private set; }

        private bool _isVisible = true;
        public bool IsVisible => _isVisible;

        private GameObject _canvasObject;
        public GameObject CanvasObject => _canvasObject;
        private Canvas _canvas;
        public Canvas Canvas => _canvas;
        private CanvasScaler _scaler;
        private GraphicRaycaster _raycaster;

        private RectTransform _containerRect;
        public RectTransform ContainerRect => _containerRect;

        // Tooltip
        private GameObject _tooltipBox;
        private TextMeshProUGUI _txtTipTitle;
        private TextMeshProUGUI _txtTipSubtitle;
        private TextMeshProUGUI _txtTipDesc;
        private TextMeshProUGUI _txtTipTime;

        private readonly List<BuffCardUI> _cardPool = new List<BuffCardUI>();
        public IReadOnlyList<BuffCardUI> Cards => _cardPool;

        private readonly List<ActiveBuffInfo> _activeBuffs = new List<ActiveBuffInfo>();
        private PlayerStats _cachedStats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Application.isPlaying && Instance == null)
            {
                var go = new GameObject("PlayerBuffHUD_Manager");
                go.AddComponent<PlayerBuffHUD>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            BuildUI();
            UpdateVisibilityForCurrentScene();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateVisibilityForCurrentScene();
            _cachedStats = null;
            HideTooltip();
        }

        private void UpdateVisibilityForCurrentScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool isMainMenu = string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase);
            SetVisible(!isMainMenu);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(visible);
            }
            if (!visible)
            {
                HideTooltip();
            }
        }

        public void BindStats(PlayerStats stats)
        {
            _cachedStats = stats;
        }

        private void BuildUI()
        {
            _canvasObject = new GameObject("PlayerBuffHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);

            _canvas = _canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 22; // Above world, below modals

            _scaler = _canvasObject.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;

            _raycaster = _canvasObject.GetComponent<GraphicRaycaster>();

            // Container along the far-left edge of the screen, below the top HUD (Y=-140) and above the skills HUD
            var containerGo = new GameObject("Buff_Container", typeof(RectTransform), typeof(VerticalLayoutGroup));
            containerGo.transform.SetParent(_canvasObject.transform, false);

            _containerRect = containerGo.GetComponent<RectTransform>();
            _containerRect.anchorMin = new Vector2(0f, 1f);
            _containerRect.anchorMax = new Vector2(0f, 1f);
            _containerRect.pivot = new Vector2(0f, 1f);
            _containerRect.anchoredPosition = new Vector2(16f, -140f); // 16px from left edge, below HP/Stamina HUD
            _containerRect.sizeDelta = new Vector2(44f, 500f);

            var vlg = containerGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.UpperLeft;

            BuildTooltipBox(_canvasObject.transform);
        }

        private void BuildTooltipBox(Transform parent)
        {
            _tooltipBox = new GameObject("HUD_Buff_Tooltip", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _tooltipBox.transform.SetParent(parent, false);

            var rt = _tooltipBox.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(240f, 0f);

            var img = _tooltipBox.GetComponent<Image>();
            img.color = new Color(0.06f, 0.07f, 0.1f, 0.96f);
            img.raycastTarget = false;

            var outline = _tooltipBox.GetComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.72f, 0.45f, 0.9f);
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

            _txtTipTitle = CreateText(_tooltipBox.transform, "Title", "", 13, TextAlignmentOptions.TopLeft,
                new Color(1f, 0.88f, 0.45f), FontStyles.Bold);

            _txtTipSubtitle = CreateText(_tooltipBox.transform, "Subtitle", "", 10.5f, TextAlignmentOptions.TopLeft,
                new Color(0.55f, 0.82f, 1f), FontStyles.Italic);

            var sepGo = new GameObject("Separator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            sepGo.transform.SetParent(_tooltipBox.transform, false);
            var sepImg = sepGo.GetComponent<Image>();
            sepImg.color = new Color(0.5f, 0.4f, 0.25f, 0.5f);
            sepImg.raycastTarget = false;
            var sepLe = sepGo.GetComponent<LayoutElement>();
            sepLe.minHeight = 1f;
            sepLe.preferredHeight = 1f;

            _txtTipDesc = CreateText(_tooltipBox.transform, "Desc", "", 11, TextAlignmentOptions.TopLeft,
                new Color(0.92f, 0.92f, 0.92f), FontStyles.Normal);

            _txtTipTime = CreateText(_tooltipBox.transform, "Time", "", 10f, TextAlignmentOptions.TopLeft,
                new Color(1f, 0.9f, 0.55f), FontStyles.Bold);

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

        private BuffCardUI CreateCard(int index)
        {
            var cardGo = new GameObject($"BuffCard_{index}", typeof(RectTransform), typeof(Image), typeof(Outline));
            cardGo.transform.SetParent(_containerRect, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(40f, 40f);

            var bgImg = cardGo.GetComponent<Image>();
            bgImg.color = new Color(0.07f, 0.08f, 0.12f, 0.90f); // Dark Minecraft card style
            bgImg.raycastTarget = true; // For hover trigger

            var outline = cardGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.42f, 0.55f, 0.7f);
            outline.effectDistance = new Vector2(1.2f, 1.2f);

            // Icon Image (fills the slot cleanly with padding)
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(cardGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(2f, 2f);
            iconRt.offsetMax = new Vector2(-2f, -2f);

            var iconImg = iconGo.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Inactive Name Text (requirement: do not display name text on play HUD)
            var nameGo = new GameObject("Txt_Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(cardGo.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.sizeDelta = Vector2.zero;
            var nameText = nameGo.GetComponent<TextMeshProUGUI>();
            TheLastKnight.UI.LocalizedText.Set(nameText, "");
            nameText.raycastTarget = false;
            nameGo.SetActive(false);

            // Duration / Timer Text overlay at bottom of icon
            var timeGo = new GameObject("Txt_Time", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Outline));
            timeGo.transform.SetParent(cardGo.transform, false);
            var timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0f, 0f);
            timeRt.anchorMax = new Vector2(1f, 0f);
            timeRt.pivot = new Vector2(0.5f, 0f);
            timeRt.anchoredPosition = new Vector2(0f, 1f);
            timeRt.sizeDelta = new Vector2(40f, 13f);

            var timeText = timeGo.GetComponent<TextMeshProUGUI>();
            timeText.fontSize = 9.5f;
            timeText.fontStyle = FontStyles.Bold;
            timeText.alignment = TextAlignmentOptions.Bottom;
            timeText.raycastTarget = false;

            var timeOutline = timeGo.GetComponent<Outline>();
            timeOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            timeOutline.effectDistance = new Vector2(0.8f, -0.8f);

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
                () => ShowTooltipForCard(card),
                HideTooltip);

            return card;
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

        private BuffCardUI _currentHoveredCard;

        private void ShowTooltipForCard(BuffCardUI card)
        {
            _currentHoveredCard = card;
            if (card == null || string.IsNullOrEmpty(card.CurrentInfo.name))
            {
                HideTooltip();
                return;
            }

            var info = card.CurrentInfo;
            if (_txtTipTitle != null) TheLastKnight.UI.LocalizedText.Set(_txtTipTitle, info.name);
            if (_txtTipSubtitle != null) TheLastKnight.UI.LocalizedText.Set(_txtTipSubtitle, info.category);
            if (_txtTipDesc != null) TheLastKnight.UI.LocalizedText.Set(_txtTipDesc, info.description);
            if (_txtTipTime != null)
            {
                TheLastKnight.UI.LocalizedText.Set(_txtTipTime, info.remainingSeconds >= 0f
                    ? $"Time Remaining: {info.formattedTime} ({info.remainingSeconds:0.0}s)"
                    : "Continuous Blessing (Sacred Area)");
            }

            if (_tooltipBox != null)
            {
                _tooltipBox.SetActive(true);
                _tooltipBox.transform.SetAsLastSibling();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipBox.GetComponent<RectTransform>());
            }
            UpdateTooltipPosition();
        }

        public void HideTooltip()
        {
            _currentHoveredCard = null;
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
                var tipRt = _tooltipBox.GetComponent<RectTransform>();
                float tipW = tipRt.rect.width > 0 ? tipRt.rect.width : 240f;
                float tipH = tipRt.rect.height > 0 ? tipRt.rect.height : 100f;

                float halfW = canvasRt.rect.width * 0.5f;
                float halfH = canvasRt.rect.height * 0.5f;

                float posX = localPoint.x + 18f;
                float posY = localPoint.y - 10f;

                if (posX + tipW > halfW) posX = localPoint.x - tipW - 10f;
                if (posY - tipH < -halfH) posY = localPoint.y + tipH + 10f;

                tipRt.anchoredPosition = new Vector2(posX, posY);
            }
        }

        private void LateUpdate()
        {
            if (!_isVisible) return;

            if (_cachedStats == null)
            {
                _cachedStats = FindAnyObjectByType<PlayerStats>();
            }

            _activeBuffs.Clear();
            if (_cachedStats != null)
            {
                _cachedStats.GetActiveBuffs(_activeBuffs);
            }

            // Sync card pool
            int count = _activeBuffs.Count;
            while (_cardPool.Count < count)
            {
                _cardPool.Add(CreateCard(_cardPool.Count));
            }

            for (int i = 0; i < _cardPool.Count; i++)
            {
                var card = _cardPool[i];
                if (i < count)
                {
                    card.Root.SetActive(true);
                    card.Bind(_activeBuffs[i]);
                }
                else
                {
                    card.Root.SetActive(false);
                }
            }

            // Update tooltip position if open
            if (_tooltipBox != null && _tooltipBox.activeSelf)
            {
                if (_currentHoveredCard != null && _currentHoveredCard.Root.activeSelf)
                {
                    // Update remaining time text dynamically
                    if (_txtTipTime != null)
                    {
                        var info = _currentHoveredCard.CurrentInfo;
                        TheLastKnight.UI.LocalizedText.Set(_txtTipTime, info.remainingSeconds >= 0f
                            ? $"Time Remaining: {info.formattedTime} ({info.remainingSeconds:0.0}s)"
                            : "Continuous Blessing (Sacred Area)");
                    }
                    UpdateTooltipPosition();
                }
                else
                {
                    HideTooltip();
                }
            }
        }

        [Serializable]
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
                    TheLastKnight.UI.LocalizedText.Set(NameText, ""); // Requirement: do not display name text on play HUD
                    if (NameText.gameObject.activeSelf)
                        NameText.gameObject.SetActive(false);
                }

                if (IconImage != null)
                {
                    IconImage.sprite = info.icon;
                    IconImage.color = info.icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
                }

                if (TimeText != null)
                {
                    TheLastKnight.UI.LocalizedText.Set(TimeText, info.formattedTime);

                    // Minecraft warning: pulses when < 5 seconds remaining
                    if (info.remainingSeconds >= 0f && info.remainingSeconds <= 5f)
                    {
                        bool flash = (Mathf.FloorToInt(Time.time * 4f) % 2) == 0;
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
    }
}
