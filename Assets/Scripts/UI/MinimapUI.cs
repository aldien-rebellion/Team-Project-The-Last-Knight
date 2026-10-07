using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using TheLastKnight.Core;

namespace TheLastKnight.UI
{
    // Runs before the status/pause menus so Escape closes the map first.
    [DefaultExecutionOrder(-600)]
    public sealed class MinimapUI : MonoBehaviour
    {
        public static MinimapUI Instance { get; private set; }
        public static int EscapeConsumedFrame { get; private set; } = -1;
        public bool IsOpen { get; private set; }
        public MinimapGraphic Map { get; private set; }
        public WorldMapGraphic WorldMap { get; private set; }
        public bool IsWorldView { get; private set; } = true;
        private GameObject _canvasObject, _window, _hint;
        private RectTransform _windowRect;
        private GameObject _worldRoot;
        private Text _worldTabText, _localTabText, _worldLocation, _worldLegend;
        private Image _worldTabImage, _localTabImage;
        private readonly Text[] _regionLabels = new Text[WorldMapGraphic.Regions.Length];
        private Text _title, _area, _subtitle, _controls, _hintText;
        private readonly Text[] _legend = new Text[5];
        private float _nextDiscovery;
        internal static readonly Color Gold = new Color(0.77f, 0.61f, 0.34f);
        internal static readonly Color Ink = new Color(0.035f, 0.047f, 0.057f, 0.97f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            EscapeConsumedFrame = -1;
            BuildUI();
            SceneManager.sceneLoaded += SceneLoaded;
            LocalizationManager.OnLanguageChanged += LanguageChanged;
            RefreshText();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            LocalizationManager.OnLanguageChanged -= LanguageChanged;
            if (_canvasObject != null) Destroy(_canvasObject);
            if (Instance == this) Instance = null;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Close();
            Map.ClearMap();
            _nextDiscovery = 0;
            RefreshText();
            _hint.SetActive(false);
        }

        private void LanguageChanged(GameLanguage language) => RefreshText();
        private bool CanShow()
        {
            var manager = GameManager.Instance;
            return !string.Equals(SceneManager.GetActiveScene().name, "MainMenu", StringComparison.OrdinalIgnoreCase)
                && manager != null && manager.Player != null && !manager.Player.IsDead
                && !manager.InputBlocked && Time.timeScale > 0f
                && !(PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsOpen)
                && !(CharacterStatusUI.Instance != null && CharacterStatusUI.Instance.IsOpen);
        }

        private static bool IsTyping()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponent<InputField>() != null || selected.GetComponent<TMP_InputField>() != null);
        }

        private void Update()
        {
            bool canShow = CanShow();
            _hint.SetActive(canShow && !IsOpen);
            if (!canShow) { Close(); return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && !IsTyping())
            {
                if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    EscapeConsumedFrame = Time.frameCount;
                    Close();
                    return;
                }
                if (keyboard.mKey.wasPressedThisFrame) Toggle();
                if (IsOpen && keyboard.tabKey.wasPressedThisFrame) SwitchView();
            }
            if (!IsOpen) return;
            if (IsWorldView) { WorldMap.SetVerticesDirty(); return; }
            // Rediscover spawns occasionally; moving markers use their live transforms.
            if (Time.unscaledTime >= _nextDiscovery)
            {
                Map.RebuildMap(GameManager.Instance.Player.transform);
                _nextDiscovery = Time.unscaledTime + 2f;
            }
            Map.SetVerticesDirty();
        }

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            if (!CanShow() || IsTyping()) return;
            Map.RebuildMap(GameManager.Instance.Player.transform);
            _nextDiscovery = Time.unscaledTime + 2f;
            RefreshText();
            IsOpen = true;
            _window.SetActive(true);
            _hint.SetActive(false);
            ApplyView();
        }

        public void SwitchView()
        {
            if (!IsOpen || !CanShow() || IsTyping()) return;
            IsWorldView = !IsWorldView;
            ApplyView();
            RefreshText();
        }

        private void ApplyView()
        {
            _windowRect.anchorMin = IsWorldView ? new Vector2(.065f,.12f) : new Vector2(.49f,.29f);
            _windowRect.anchorMax = IsWorldView ? new Vector2(.935f,.94f) : new Vector2(.98f,.985f);
            _worldRoot.SetActive(IsWorldView);
            Map.gameObject.SetActive(!IsWorldView);
            _subtitle.gameObject.SetActive(!IsWorldView);
            for (int i = 0; i < _legend.Length; i++) _legend[i].transform.parent.Find("Legend" + i)?.gameObject.SetActive(!IsWorldView);
            foreach (var label in _legend) label.gameObject.SetActive(!IsWorldView);
            _worldLegend.gameObject.SetActive(IsWorldView);
            _worldTabImage.color = IsWorldView ? new Color(.34f,.29f,.18f) : new Color(.13f,.14f,.12f);
            _localTabImage.color = !IsWorldView ? new Color(.34f,.29f,.18f) : new Color(.13f,.14f,.12f);
            if (!IsWorldView && GameManager.Instance != null && GameManager.Instance.Player != null)
                Map.RebuildMap(GameManager.Instance.Player.transform);
        }

        public void Close()
        {
            IsOpen = false;
            if (_window != null) _window.SetActive(false);
        }

        private void BuildUI()
        {
            _canvasObject = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var hint = Box(_canvasObject.transform, "MapKeyHint", new Vector2(0.79f, 0.925f), new Vector2(0.98f, 0.985f), Ink);
            _hint = hint.gameObject;
            Border(hint, Gold);
            _hintText = Label(hint, "", 17, Gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

            var window = Box(_canvasObject.transform, "AtlasWindow", new Vector2(0.49f, 0.29f), new Vector2(0.98f, 0.985f), Ink);
            _windowRect = window;
            _window = window.gameObject;
            Border(window, Gold);
            Box(window, "Header", new Vector2(0, 0.80f), Vector2.one, new Color(0.12f, 0.105f, 0.083f, 0.96f));
            Label(window, "THE LAST KNIGHT", 12, Gold, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.94f), new Vector2(0.75f, 0.985f));
            _title = Label(window, "", 26, new Color(0.96f, 0.86f, 0.64f), TextAnchor.MiddleLeft, new Vector2(0.04f, 0.855f), new Vector2(0.48f, 0.947f));
            _area = Label(window, "", 15, new Color(0.62f, 0.71f, 0.69f), TextAnchor.MiddleLeft, new Vector2(0.04f, 0.806f), new Vector2(0.88f, 0.86f));
            _worldTabText = TabButton(window, "WorldViewTab", new Vector2(.53f,.868f), new Vector2(.72f,.955f), true, out _worldTabImage);
            _localTabText = TabButton(window, "LocalViewTab", new Vector2(.735f,.868f), new Vector2(.925f,.955f), false, out _localTabImage);
            var close = Box(window, "CloseMap", new Vector2(.945f,.895f), new Vector2(.98f,.955f), new Color(.25f,.15f,.13f));
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            Label(close, "X", 15, Gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            var viewport = Box(window, "MapViewport", new Vector2(0.035f, 0.19f), new Vector2(0.965f, 0.78f), new Color(0.026f, 0.040f, 0.046f));
            Border(viewport, new Color(0.27f, 0.31f, 0.28f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var map = new GameObject("SideViewMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(MinimapGraphic));
            map.transform.SetParent(viewport, false);
            var rect = map.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Map = map.GetComponent<MinimapGraphic>();
            Map.raycastTarget = false;
            _subtitle = Label(viewport, "", 12, new Color(0.52f, 0.62f, 0.60f), TextAnchor.MiddleLeft, new Vector2(0.035f, 0.86f), new Vector2(0.84f, 0.97f));
            var world = new GameObject("WorldAtlas", typeof(RectTransform), typeof(CanvasRenderer), typeof(WorldMapGraphic));
            world.transform.SetParent(viewport, false);
            var worldRect = world.GetComponent<RectTransform>();
            worldRect.anchorMin = Vector2.zero; worldRect.anchorMax = Vector2.one;
            worldRect.offsetMin = worldRect.offsetMax = Vector2.zero;
            _worldRoot = world;
            WorldMap = world.GetComponent<WorldMapGraphic>();
            WorldMap.raycastTarget = false;
            for (int i = 0; i < _regionLabels.Length; i++)
            {
                var p = WorldMapGraphic.Regions[i].Position;
                _regionLabels[i] = Label(world.transform, "", 16, new Color(.94f,.87f,.69f), TextAnchor.MiddleCenter,
                    p + new Vector2(-.11f,-.15f), p + new Vector2(.11f,-.078f));
                _regionLabels[i].gameObject.AddComponent<Shadow>().effectColor = new Color(0,0,0,.9f);
            }
            _worldLocation = Label(world.transform, "", 13, Gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            Label(world.transform, "N", 13, Gold, TextAnchor.MiddleCenter, new Vector2(.93f,.26f), new Vector2(.98f,.31f));
            _worldLegend = Label(window, "", 15, new Color(.78f,.80f,.71f), TextAnchor.MiddleCenter, new Vector2(.04f,.112f), new Vector2(.96f,.165f));
            for (int i = 0; i < _legend.Length; i++)
            {
                float x = 0.04f + i * 0.187f;
                var swatch = Box(window, "Legend" + i, new Vector2(x, 0.128f), new Vector2(x + 0.013f, 0.147f), MinimapGraphic.MarkerColor(i));
                _legend[i] = Label(window, "", 13, new Color(0.78f, 0.79f, 0.73f), TextAnchor.MiddleLeft, new Vector2(x + 0.020f, 0.112f), new Vector2(x + 0.15f, 0.165f));
            }
            _controls = Label(window, "", 13, Gold, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.025f), new Vector2(0.96f, 0.09f));
            _window.SetActive(false);
            _hint.SetActive(false);
            ApplyView();
        }

        private Text TabButton(Transform parent, string name, Vector2 min, Vector2 max, bool world, out Image image)
        {
            var rect = Box(parent, name, min, max, Ink);
            image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (IsWorldView != world) SwitchView(); });
            return Label(rect, "", 16, Gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        }

        private void RefreshText()
        {
            bool thai = LocalizationManager.Current == GameLanguage.Thai;
            _title.text = IsWorldView ? (thai ? "แผนที่โลก" : "WORLD ATLAS") : (thai ? "แผนที่พื้นที่" : "AREA MAP");
            _area.text = IsWorldView ? (thai ? "อาณาจักรทั้งหมด • 6 พื้นที่ • มุมมองจากด้านบน" : "THE WHOLE KINGDOM • 6 REGIONS • TOP-DOWN") : LocalizationManager.Translate(SceneManager.GetActiveScene().name);
            _subtitle.text = thai ? "ตำแหน่งทางเดินและห้อง • มุมมองด้านข้าง" : "SIDE VIEW • ROUTES & ROOMS";
            _controls.text = thai ? "Tab สลับมุมมอง   •   M / Esc ปิดแผนที่" : "Tab Switch view   •   M / Esc Close map";
            _worldTabText.text = thai ? "แผนที่โลก" : "WORLD";
            _localTabText.text = thai ? "ด้านข้าง" : "SIDE VIEW";
            _worldLegend.text = thai ? "◆ พื้นที่ปัจจุบัน     •     ทางสีทอง: เส้นทางเชื่อม     •     ทางสีแดง: ประตูผนึก" : "◆ Current region     •     Gold: connected route     •     Red: sealed gate";
            for (int i = 0; i < _regionLabels.Length; i++) _regionLabels[i].text = LocalizationManager.Translate(WorldMapGraphic.Regions[i].Scene);
            int current = WorldMap.CurrentRegionIndex;
            _worldLocation.gameObject.SetActive(current >= 0);
            if (current >= 0)
            {
                var p = WorldMapGraphic.Regions[current].Position;
                _worldLocation.text = thai ? "คุณอยู่ที่นี่" : "YOU ARE HERE";
                _worldLocation.rectTransform.anchorMin = p + new Vector2(-.09f,.11f);
                _worldLocation.rectTransform.anchorMax = p + new Vector2(.09f,.17f);
            }
            _hintText.text = thai ? "M   แผนที่" : "M   MAP";
            string[] names = thai ? new[] { "คุณ", "ศัตรู", "ประตู", "จุดเซฟ", "ร้านค้า" }
                                  : new[] { "You", "Enemy", "Gate", "Save", "Shop" };
            for (int i = 0; i < names.Length; i++) _legend[i].text = names[i];
        }

        private static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static void Border(RectTransform parent, Color color)
        {
            var outline = parent.gameObject.AddComponent<Outline>();
            outline.effectColor = color; outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static Text Label(Transform parent, string text, int size, Color color, TextAnchor alignment, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(ThaiTextMarks));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = RuntimeUI.Font; label.text = text; label.fontSize = size;
            label.color = color; label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = size;
            label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }
    }
}
