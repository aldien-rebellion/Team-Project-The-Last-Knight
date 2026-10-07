using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using TheLastKnight.Core;
using UIDocument = UnityEngine.UIElements.UIDocument;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;

namespace TheLastKnight.UI
{
    public class StoryDialogueUI : MonoBehaviour
    {
        private GameObject _panel;
        private string[] _lines;
        private string[] _sourceLines;
        private int[] _lineOrigins;
        private int _index;
        private Text _body;
        private Text _speaker;
        private Text _continueHint;
        private Image _storyBackground;
        private GameObject _speakerPlate;
        private GameObject _menuPanel;
        private GameObject _dialoguePanel;
        private GameObject _sceneTitlePanel;
        private Action _complete;
        private int _openedFrame;
        private bool _ending, _rolling, _cutscenePlaying, _isDialogueHidden;
        private Text _hideButtonText;
        private float _elapsed;
        private float _sceneTitleElapsed;
        private RectTransform _credits, _creditsViewport;
        private RectTransform[] _creditsBanners;
        private float _creditsBannerStart, _creditsBannerPeriod;
        private const float CreditsScrollSpeed = 32f;
        private const float CreditsBannerGap = 160f;
        private static Font _creditsBannerFont;
        private readonly List<string> _history = new List<string>();
        private GameObject _logPanel;
        private HUDController _hud;
        private int _currentStoryScene;
        private const float DialogueHeight = 0.255f;
        private static readonly string[] ThaiFontNames = { "Leelawadee UI", "Tahoma", "Arial" };
        private static Font _storyFont;
        private static Sprite _roundedPanelSprite;
        public void Show(string title, string[] lines, Action complete = null)
        {
            if (_panel != null) Destroy(_panel);
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != null && canvas.name.StartsWith("ฉากที่", StringComparison.Ordinal))
                    Destroy(canvas.gameObject);
            }
            _ending = _rolling = _cutscenePlaying = _isDialogueHidden = false; _elapsed = 0f;
            _sourceLines = lines;
            _lines = PrepareLines(lines); _index = 0; _complete = complete; _openedFrame = Time.frameCount; _history.Clear();
            GameManager.Instance.SetInputBlocked(true); Time.timeScale = 0f;
            SetHudVisible(false);
            BuildVisualNovelLayout(title);
            SetLine(0);
        }

        private void BuildVisualNovelLayout(string title)
        {
            RuntimeUI.EnsureEventSystem();
            _panel = new GameObject(title, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = CreatePanel(_panel.transform, "Backdrop", new Color(0f, 0f, 0f, 0.08f), Vector2.zero, Vector2.one);
            backdrop.GetComponent<Image>().raycastTarget = true;
            var backdropButton = backdrop.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(() =>
            {
                if (_isDialogueHidden)
                {
                    ToggleHideDialogue();
                }
            });

            var backgroundObject = new GameObject("Story Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(backdrop.transform, false);
            backgroundObject.transform.SetAsFirstSibling();
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            _storyBackground = backgroundObject.GetComponent<Image>();
            _storyBackground.raycastTarget = false;
            // A dialogue without a story-scene image should leave gameplay visible.
            // A null-sprite Image otherwise renders as a solid white full-screen panel.
            _storyBackground.enabled = false;

            _menuPanel = CreatePanel(backdrop.transform, "Story Menu", new Color(0.02f, 0.025f, 0.035f, 0.88f), new Vector2(0.68f, 0.925f), new Vector2(0.985f, 0.985f), true);
            CreateTopMenuButton(_menuPanel.transform, "SKIP", 0.04f, 0.32f, Finish);
            CreateTopMenuButton(_menuPanel.transform, "LOG", 0.36f, 0.64f, ToggleLog);
            _hideButtonText = CreateTopMenuButton(_menuPanel.transform, "HIDE", 0.68f, 0.96f, ToggleHideDialogue);

            _dialoguePanel = CreatePanel(backdrop.transform, "Dialogue Box", new Color(0.015f, 0.015f, 0.02f, 0.88f), new Vector2(0.015f, 0.045f), new Vector2(0.985f, 0.045f + DialogueHeight), true);
            var dialogueRect = _dialoguePanel.GetComponent<RectTransform>();
            dialogueRect.offsetMax = new Vector2(0f, 0f);
            var continueButton = _dialoguePanel.AddComponent<Button>();
            continueButton.targetGraphic = _dialoguePanel.GetComponent<Image>();
            continueButton.onClick.AddListener(Advance);

            _speakerPlate = CreatePanel(_dialoguePanel.transform, "Speaker", new Color(0.035f, 0.035f, 0.05f, 0.96f), new Vector2(0f, 1f), new Vector2(0.30f, 1.22f), true);
            _speaker = CreateText(_speakerPlate.transform, "", 21, new Color(0.94f, 0.88f, 0.7f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            _body = CreateText(_dialoguePanel.transform, "", 24, new Color(0.95f, 0.95f, 0.95f), TextAnchor.UpperLeft, new Vector2(0.045f, 0.18f), new Vector2(0.955f, 0.80f));
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Truncate;
            _body.resizeTextForBestFit = true;
            _body.resizeTextMinSize = 16;
            _body.resizeTextMaxSize = 24;
            _continueHint = CreateText(_dialoguePanel.transform, "Click / Space to continue", 14, new Color(0.75f, 0.75f, 0.78f), TextAnchor.MiddleRight, new Vector2(0.65f, 0.04f), new Vector2(0.95f, 0.18f));
        }

        private string[] PrepareLines(string[] source)
        {
            var prepared = new List<string>();
            var origins = new List<int>();
            for (int sourceIndex = 0; sourceIndex < source.Length; sourceIndex++)
            {
                string sourceLine = source[sourceIndex];
                if (sourceLine.StartsWith("ฉากที่", StringComparison.Ordinal))
                {
                    prepared.Add(sourceLine);
                    origins.Add(sourceIndex);
                    continue;
                }

                int divider = sourceLine.IndexOf("\n\n", StringComparison.Ordinal);
                string speaker = divider >= 0 ? sourceLine.Substring(0, divider) : "บทบรรยาย";
                string content = LocalizationManager.Translate(divider >= 0 ? sourceLine.Substring(divider + 2) : sourceLine);
                content = Regex.Replace(content, @"\n(?:[ \t]*\n)+", " ");
                content = Regex.Replace(content, @"\s+", " ").Trim();
                content = content.Replace("—", "-").Replace("…", "...").Replace("|", ",").Replace("◆", "");

                foreach (string page in SplitTextPages(content, 120))
                {
                    prepared.Add(speaker + "\n\n" + page);
                    origins.Add(sourceIndex);
                }
            }
            _lineOrigins = origins.ToArray();
            return prepared.ToArray();
        }

        private static IEnumerable<string> SplitTextPages(string text, int maxElements)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var page = new StringBuilder();
            foreach (string word in words)
            {
                string addition = page.Length == 0 ? word : " " + word;
                if (new StringInfo(page.ToString()).LengthInTextElements + new StringInfo(addition).LengthInTextElements <= maxElements)
                {
                    page.Append(addition);
                    continue;
                }

                if (page.Length > 0)
                {
                    yield return page.ToString();
                    page.Length = 0;
                }

                var elementEnumerator = StringInfo.GetTextElementEnumerator(word);
                while (elementEnumerator.MoveNext())
                {
                    string element = elementEnumerator.GetTextElement();
                    if (new StringInfo(page.ToString()).LengthInTextElements >= maxElements)
                    {
                        yield return page.ToString();
                        page.Length = 0;
                    }
                    page.Append(element);
                }
            }

            if (page.Length > 0) yield return page.ToString();
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, bool rounded = false)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = panel.GetComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = RoundedPanelSprite;
                image.type = Image.Type.Sliced;
            }
            return panel;
        }

        private static Sprite RoundedPanelSprite
        {
            get
            {
                if (_roundedPanelSprite != null) return _roundedPanelSprite;

                const int size = 32;
                const float radius = 6f;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cornerX = x < radius ? radius : x > size - 1 - radius ? size - 1 - radius : x;
                    float cornerY = y < radius ? radius : y > size - 1 - radius ? size - 1 - radius : y;
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cornerX, cornerY));
                    texture.SetPixel(x, y, distance > radius ? Color.clear : Color.white);
                }
                texture.Apply();
                _roundedPanelSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                _roundedPanelSprite.hideFlags = HideFlags.DontSave;
                return _roundedPanelSprite;
            }
        }

        private static Text CreateText(Transform parent, string value, int size, Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, bool localize = true)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            var rect = label.rectTransform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.font = StoryFont;
            if (localize) TheLastKnight.UI.LocalizedText.Set(label, value);
            else label.text = value;
            label.fontSize = size; label.color = color; label.alignment = alignment; label.raycastTarget = false;
            return label;
        }

        private static Font StoryFont
        {
            get
            {
                if (_storyFont == null)
                    _storyFont = Font.CreateDynamicFontFromOSFont(ThaiFontNames, 24);
                return _storyFont != null ? _storyFont : RuntimeUI.Font;
            }
        }

        private Text CreateTopMenuButton(Transform parent, string label, float left, float right, UnityAction action)
        {
            var button = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(left, 0.12f); rect.anchorMax = new Vector2(right, 0.88f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = button.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            var uiButton = button.GetComponent<Button>();
            var text = CreateText(button.transform, label, 14, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            uiButton.targetGraphic = text;
            var colors = uiButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.58f, 1f);
            colors.pressedColor = new Color(0.9f, 0.8f, 0.55f, 1f);
            colors.selectedColor = colors.highlightedColor;
            uiButton.colors = colors;
            uiButton.onClick.AddListener(action);
            return text;
        }

        private void SetLine(int index)
        {
            string line = _lines[index];
            if (line.StartsWith("ฉากที่", StringComparison.Ordinal))
            {
                if (_history.Count > 0) _history.Add(string.Empty);
                _history.Add("<color=#E8D8A8><b>" + line + "</b></color>");
                var sceneMatch = Regex.Match(line, @"^ฉากที่\s*(\d+)");
                if (sceneMatch.Success && int.TryParse(sceneMatch.Groups[1].Value, out int nextScene))
                {
                    if (_currentStoryScene == 6 && nextScene == 7)
                    {
                        var captured = CaptureSceneView();
                        if (captured != null && _storyBackground != null)
                        {
                            _storyBackground.sprite = captured;
                            _storyBackground.enabled = true;
                            _storyBackground.color = Color.white;
                        }
                    }
                    _currentStoryScene = nextScene;
                    UpdateStorySceneMusic();
                    UpdateStorySceneBackground();
                }
                ShowSceneTitle(line);
                return;
            }

            int divider = line.IndexOf("\n\n", StringComparison.Ordinal);
            string speaker = divider >= 0 ? line.Substring(0, divider) : "บทบรรยาย";
            string dialogue = divider >= 0 ? line.Substring(divider + 2) : line;
            dialogue = Regex.Replace(dialogue, @"\n(?:[ \t]*\n)+", "\n").Trim();
            dialogue = dialogue.Replace("—", "-").Replace("…", "...").Replace("|", ",").Replace("◆", "");
            bool narration = speaker.StartsWith("บทบรรยาย", StringComparison.Ordinal);
            if (narration)
                speaker = "Arthur Reuven";
            else if (speaker.StartsWith("ตัวเอก", StringComparison.Ordinal))
                speaker = "Arthur Reuven";
            else
                speaker = Regex.Replace(speaker.Replace("—", " ").Replace("◆", ""), @"\s+", " ").Trim();
            TheLastKnight.UI.LocalizedText.Set(_speaker, speaker);
            _speakerPlate.SetActive(!narration);
            TheLastKnight.UI.LocalizedText.Set(_body, dialogue);
            _history.Add(speaker + "\n" + dialogue);
        }

        private void UpdateStorySceneMusic()
        {
            if (_currentStoryScene == 1 || _currentStoryScene == 6)
                TheLastKnight.Audio.AudioManager.Instance?.PlayMusic("StarfallDreams");
            else if (_currentStoryScene == 7)
                TheLastKnight.Audio.AudioManager.Instance?.PlayMusic("HollowVale");
        }

        private void ShowSceneTitle(string line)
        {
            int split = line.IndexOf('—');
            string chapter = split >= 0 ? line.Substring(0, split).Trim() : line.Trim();
            string sceneName = split >= 0 ? line.Substring(split + 1).Trim() : string.Empty;
            _menuPanel.SetActive(false);
            _dialoguePanel.SetActive(false);
            _sceneTitlePanel = CreatePanel(_panel.transform.Find("Backdrop"), "Scene Title", new Color(0f, 0f, 0f, 0.42f), Vector2.zero, Vector2.one);
            var card = CreatePanel(_sceneTitlePanel.transform, "Scene Title Card", new Color(0.015f, 0.015f, 0.02f, 0.86f), new Vector2(0.16f, 0.39f), new Vector2(0.84f, 0.61f), true);
            string text = string.IsNullOrEmpty(sceneName) ? chapter : chapter + "\n" + sceneName;
            var title = CreateText(card.transform, text, 38, new Color(0.96f, 0.91f, 0.78f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 26;
            title.resizeTextMaxSize = 38;
            _sceneTitleElapsed = 0f;
        }

        private void OnEnable() => LocalizationManager.OnLanguageChanged += RefreshLanguage;

        private void OnDisable() => LocalizationManager.OnLanguageChanged -= RefreshLanguage;

        private void RefreshLanguage(GameLanguage language)
        {
            if (_panel == null || _sourceLines == null || _rolling || _cutscenePlaying) return;
            int scene = _currentStoryScene;
            int sourceIndex = _lineOrigins[_index];
            _lines = PrepareLines(_sourceLines);
            _index = Mathf.Max(0, Array.IndexOf(_lineOrigins, sourceIndex));
            _history.Clear();
            if (_sceneTitlePanel != null) HideSceneTitle();
            if (_logPanel != null) { Destroy(_logPanel); _logPanel = null; }
            SetLine(_index);
            _currentStoryScene = scene;
        }

        private void HideSceneTitle()
        {
            if (_sceneTitlePanel == null) return;
            Destroy(_sceneTitlePanel);
            _sceneTitlePanel = null;
            _menuPanel.SetActive(true);
            _dialoguePanel.SetActive(!_isDialogueHidden);
        }

        private void ToggleLog()
        {
            if (_logPanel != null) { Destroy(_logPanel); _logPanel = null; return; }
            if (_isDialogueHidden) ToggleHideDialogue();
            var backdrop = _panel.transform.Find("Backdrop");
            _logPanel = CreatePanel(backdrop, "Story Log", new Color(0.01f, 0.01f, 0.02f, 0.96f), new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), true);
            CreateText(_logPanel.transform, "LOG", 28, new Color(0.94f, 0.88f, 0.7f), TextAnchor.UpperCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            var viewport = CreatePanel(_logPanel.transform, "Log Viewport", new Color(0f, 0f, 0f, 0f), new Vector2(0.055f, 0.13f), new Vector2(0.91f, 0.85f), true);
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            var content = new GameObject("Log Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(8f, 0f);
            contentRect.offsetMax = new Vector2(-8f, 0f);
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 8, 8);
            contentLayout.spacing = 12f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var log = CreateText(content.transform, string.Join("\n", _history), 18, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one);
            log.horizontalOverflow = HorizontalWrapMode.Wrap;
            log.verticalOverflow = VerticalWrapMode.Overflow;
            log.supportRichText = true;

            var scrollbarObject = new GameObject("Log Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(_logPanel.transform, false);
            var scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(0.93f, 0.14f);
            scrollbarRect.anchorMax = new Vector2(0.95f, 0.84f);
            scrollbarRect.offsetMin = scrollbarRect.offsetMax = Vector2.zero;
            scrollbarObject.GetComponent<Image>().color = new Color(0.25f, 0.23f, 0.2f, 0.75f);
            var handle = CreatePanel(scrollbarObject.transform, "Handle", new Color(0.85f, 0.78f, 0.62f, 0.9f), Vector2.zero, Vector2.one, true);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();

            var scrollRect = _logPanel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalNormalizedPosition = 1f;
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(_logPanel.transform, false);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.75f, 0.02f); closeRect.anchorMax = new Vector2(0.93f, 0.10f); closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
            close.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.18f, 1f);
            close.GetComponent<Button>().onClick.AddListener(ToggleLog);
            CreateText(close.transform, "Close", 16, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        }

        private void ToggleHideDialogue()
        {
            if (_cutscenePlaying || _panel == null || _dialoguePanel == null) return;
            if (_sceneTitlePanel != null) return;
            if (_logPanel != null) { Destroy(_logPanel); _logPanel = null; }

            _isDialogueHidden = !_isDialogueHidden;
            _dialoguePanel.SetActive(!_isDialogueHidden);
            if (_hideButtonText != null)
            {
                TheLastKnight.UI.LocalizedText.Set(_hideButtonText, _isDialogueHidden ? "SHOW" : "HIDE");
            }
        }

        private void SetHudVisible(bool visible)
        {
            if (_hud == null) _hud = FindAnyObjectByType<HUDController>(FindObjectsInactive.Include);
            var document = _hud != null ? _hud.GetComponent<UIDocument>() : null;
            if (document != null && document.rootVisualElement != null)
                document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private Material _blurMaterial;
        private Coroutine _backgroundTransition;

        private void UpdateStorySceneBackground()
        {
            string resourceName;
            switch (_currentStoryScene)
            {
                case 1: resourceName = "Scene 1 — The First Day Under Boa’s Banner"; break;
                case 2: resourceName = "Scene 2 — The Oath at the Border"; break;
                case 3: resourceName = "Scene 3 — The Rift from Beyond"; break;
                case 4: resourceName = "main menu background"; break;
                case 5: resourceName = "Scene 5 — The Survivor"; break;
                case 6: resourceName = null; break; // Keep the current game view visible during the final battle.
                case 7: resourceName = "RebuiltMoa"; break;
                default: resourceName = null; break;
            }

            var nextSprite = string.IsNullOrEmpty(resourceName) ? null : Resources.Load<Sprite>(resourceName);
            TransitionToBackground(nextSprite);
        }

        private void TransitionToBackground(Sprite newSprite)
        {
            if (_storyBackground == null) return;
            if (_backgroundTransition != null) StopCoroutine(_backgroundTransition);
            _backgroundTransition = StartCoroutine(TransitionBackgroundRoutine(newSprite));
        }

        private System.Collections.IEnumerator TransitionBackgroundRoutine(Sprite newSprite)
        {
            if (_blurMaterial == null)
            {
                var shader = Shader.Find("Custom/FastBoxBlur");
                if (shader != null) _blurMaterial = new Material(shader);
            }

            if (_blurMaterial != null)
            {
                _storyBackground.material = _blurMaterial;
            }

            // If there was no previous image
            if (_storyBackground.sprite == null)
            {
                _storyBackground.sprite = newSprite;
                _storyBackground.enabled = newSprite != null;
                if (newSprite != null && _blurMaterial != null)
                {
                    float t = 0f;
                    while (t < 0.4f)
                    {
                        t += Time.unscaledDeltaTime;
                        _blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(12f, 0f, t / 0.4f));
                        yield return null;
                    }
                    _blurMaterial.SetFloat("_BlurSize", 0f);
                }
                yield break;
            }

            // Phase 1: Fast Box Blur out current image (~0.4s)
            if (_blurMaterial != null)
            {
                float t = 0f;
                while (t < 0.4f)
                {
                    t += Time.unscaledDeltaTime;
                    _blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(0f, 14f, t / 0.4f));
                    yield return null;
                }
                _blurMaterial.SetFloat("_BlurSize", 14f);
            }

            // Phase 2: 1-second pause/interval while blurred
            float waitTimer = 0f;
            bool swapped = false;
            while (waitTimer < 1.0f)
            {
                waitTimer += Time.unscaledDeltaTime;
                if (!swapped && waitTimer >= 0.5f)
                {
                    swapped = true;
                    _storyBackground.sprite = newSprite;
                    _storyBackground.enabled = newSprite != null;
                }
                yield return null;
            }

            if (!swapped)
            {
                _storyBackground.sprite = newSprite;
                _storyBackground.enabled = newSprite != null;
            }

            // Phase 3: Fast Box Blur in new image (~0.4s down to 0)
            if (newSprite != null && _blurMaterial != null)
            {
                float t = 0f;
                while (t < 0.4f)
                {
                    t += Time.unscaledDeltaTime;
                    _blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(14f, 0f, t / 0.4f));
                    yield return null;
                }
                _blurMaterial.SetFloat("_BlurSize", 0f);
            }
            else if (_blurMaterial != null)
            {
                _blurMaterial.SetFloat("_BlurSize", 0f);
            }
        }

        public void Intro() => Show("ฉากที่ 1 — วันแรกใต้ธงแห่งโบอา", new[] {
            "ฉากที่ 1 — วันแรกใต้ธงแห่งโบอา",
            "สถานที่: ลานฝึกปราสาทโบอา, เวลา: ยามเช้า",
            "บทบรรยาย — ตัวเอก\n\nข้าเคยคิดว่าอัศวินต้องไม่หวาดกลัว\nแต่ในวันแรกที่ก้าวเข้าปราสาทโบอา ข้ากลับเป็นเพียงเด็กหนุ่มคนหนึ่ง ท่ามกลางนักรบผู้ผ่านสมรภูมิมานับครั้งไม่ถ้วน",
            "อัศวินผู้ฝึกสอน\n\n“ที่นี่ไม่มีใครสนใจว่าเจ้ามาจากไหน มีเพียงสิ่งที่เจ้าทำในสนามรบเท่านั้นที่พิสูจน์ตัวเจ้า”",
            "ตัวเอก\n\n“ครับ”",
            "อัศวินผู้ฝึกสอน\n\n“ฝึกให้หนัก อย่าทำให้ตราอัศวินต้องมัวหมอง”",
            "บทบรรยาย — ตัวเอก\n\nวันนั้น ข้าสาบานว่าจะใช้ดาบปกป้องโบอา\nข้าเชื่อว่าอาณาจักรจะไม่มีวันล่มสลาย ตราบใดที่ยังมีอัศวินยืนหยัด\nข้าไม่รู้เลยว่า วันหนึ่งคำสาบานนั้นจะเป็นสิ่งเดียวที่เหลืออยู่",
            "ฉากที่ 2 — คำสาบาน ณ ชายแดน",
            "สถานที่: ป้อมปราการชายแดน, เวลาผ่านไปหลายปี",
            "บทบรรยาย — ตัวเอก\n\nหลายปีผ่านไป ข้ากลายเป็นหัวหน้าหน่วยอัศวิน และถูกส่งไปประจำชายแดนทางเหนือ\nเบื้องหน้าข้าคือเหล่าอัศวินหน้าใหม่ ผู้ยังมีความฝันเหมือนข้าในวันแรก",
            "ตัวเอก — กล่าวต่อหน้าอัศวินหน้าใหม่\n\n“อัศวินแห่งโบอา จงเงยหน้าขึ้น”\n\n“เมื่อความกลัวบอกให้พวกเจ้าหนี จงมองคนที่ยืนอยู่ข้างหลัง”\n\n“ดาบของอัศวินมีไว้ปกป้องผู้ที่ปกป้องตัวเองไม่ได้”",
            "อัศวินหน้าใหม่\n\n“เพื่อโบอา! เพื่อประชาชน! เพื่อเกียรติแห่งอัศวิน!”",
            "บทบรรยาย — ตัวเอก\n\nเสียงของพวกเขาก้องไปทั่วป้อมปราการ\nข้ายิ้ม โดยไม่รู้ว่าสงครามที่กำลังมาถึงจะพรากพวกเขาไป\nและข้าจะรักษาคำพูดของตัวเองไว้ไม่ได้ทั้งหมด",
            "ฉากที่ 3 — รอยแยกจากอีกฟากหนึ่ง",
            "สถานที่: ชายแดนทางเหนือ, เวลา: กลางคืน",
            "บทบรรยาย — ตัวเอก\n\nคืนนั้น ท้องฟ้าฉีกออกเป็นรอยแผล\nแสงสีแดงสาดลงบนผืนดิน พร้อมเสียงคำรามจากอีกฟากของความมืด",
            "อัศวินยามเฝ้าป้อม\n\n“ท่านหัวหน้า! นั่นคืออะไร?!”",
            "ตัวเอก\n\n“ส่งสัญญาณเตือนภัย เรียกทุกคนขึ้นกำแพง!”",
            "อัศวินอีกคน\n\n“มีบางอย่างออกมาจากรอยแยก!”",
            "บทบรรยาย — ตัวเอก\n\nข้าเห็นเงาร่างแรกตกลงมาจากท้องฟ้า ตามด้วยอีกสิบ ร้อย และนับไม่ถ้วน\nปีศาจจากอีกมิติหลั่งไหลเข้าสู่โลก ข้าได้ยินสัญญาณเตือนภัยถูกส่งไปยังเมืองหลวง",
            "ตัวเอก\n\n“ตั้งแนวป้องกัน!”",
            "บทบรรยาย — ตัวเอก\n\nข้าชักดาบ ยังเชื่อว่าเราจะหยุดพวกมันได้\nข้าคืออัศวินแห่งโบอา และนี่คือดินแดนที่ข้าสาบานว่าจะปกป้อง",
            "ฉากที่ 4 — วันที่กำแพงพังทลาย",
            "สถานที่: ป้อมชายแดนและเส้นทางสู่เมืองหลวง",
            "บทบรรยาย — ตัวเอก\n\nการต่อสู้เริ่มก่อนรุ่งสาง\nอัศวินของข้าสู้จนหมดแรง แต่ทุกครั้งที่ศัตรูล้มลง ตัวใหม่ก็ข้ามร่างของมันเข้ามา\nนี่ไม่ใช่กองทัพ แต่เป็นคลื่นที่ไม่มีวันสิ้นสุด",
            "อัศวินหน้าใหม่\n\n“พวกมันทะลวงประตูชั้นนอกแล้ว!”",
            "ตัวเอก\n\n“ถอยไปประตูชั้นใน พาผู้บาดเจ็บออกไป!”",
            "อัศวินหน้าใหม่\n\n“แล้วท่านล่ะครับ?!”",
            "ตัวเอก\n\n“นั่นคือคำสั่ง”",
            "บทบรรยาย\n\nคำสั่งของข้าหยุดความตายไม่ได้\nอัศวินที่เคยยืนเคียงข้างข้าล้มลง ธงโบอาถูกไฟเผา และกำแพงพังทลาย\nเราสู้จนถึงที่สุด แต่ความกล้าหาญไม่อาจหยุดศัตรูที่ไร้ขอบเขต",
            "อัศวินผู้บาดเจ็บ\n\n“ท่านต้องไป… ไม่อย่างนั้นจะไม่มีใครบอกพวกเขาว่าเกิดอะไรขึ้น”",
            "ตัวเอก\n\n“ยังมีคนต้องช่วย…”",
            "อัศวินผู้บาดเจ็บ\n\n“ได้โปรด… ไปเถอะครับ”",
            "บทบรรยาย — ตัวเอก\n\nมือข้าสั่นอยู่บนด้ามดาบ\nข้าเคยสัญญาว่าจะปกป้องทุกคน แต่วันนั้นข้าทำได้เพียงหนีจากสนามรบ\nข้าได้ยินเสียงระฆังดังอยู่เบื้องหลัง ก่อนที่มันจะเงียบลงตลอดกาล",
            "ฉากที่ 5 — ผู้รอดชีวิต",
            "สถานที่: ซากป่าหลังแนวรบ, หลายวันหลังการพ่ายแพ้",
            "บทบรรยาย — ตัวเอก\n\nข้าตื่นขึ้นท่ามกลางควันและกลิ่นเลือด\nเกราะแตกหัก แขนแทบขยับไม่ได้ ทุกลมหายใจเต็มไปด้วยความเจ็บปวด\nสิ่งเดียวที่รู้คือ ข้ายังมีชีวิตอยู่",
            "ตัวเอก — พึมพำ\n\n“ทำไม… ข้าถึงรอด”",
            "บทบรรยาย — ตัวเอก\n\nข้านึกถึงเหล่าอัศวินหน้าใหม่ พวกเขาเชื่อในคำพูดของข้า เชื่อว่าข้าจะพาพวกเขากลับบ้าน",
            "บทบรรยาย — ตัวเอก\n\nข้าอยากวางดาบ แล้วปล่อยให้ความมืดกลืนกิน\nแต่ตราอัศวินที่แตกร้าวยังอยู่ข้างกาย\nข้านึกถึงคำพูดที่เคยให้ไว้: ดาบมีไว้ปกป้องผู้ที่ปกป้องตัวเองไม่ได้",
            "ตัวเอก\n\n“ถ้ายังมีคนรอความช่วยเหลือ… ข้าจะไม่ยอมแพ้”",
            "บทบรรยาย — ตัวเอก\n\nข้ารู้ว่าประตูมิติยังเปิดอยู่ และปีศาจจะไม่หยุดตราบใดที่มันยังคงอยู่\nข้าช่วยคนที่ตายไปแล้วไม่ได้ แต่อาจยังช่วยคนที่เหลืออยู่",
            "บทบรรยาย — ตัวเอก\n\nข้าหยิบดาบบิ่นหักขึ้นมา แล้วเดินออกจากซากป่าเพียงลำพัง"
        });
        public void Ending()
        {
            Show("ฉากที่ 6 — อัศวินคนสุดท้าย", new[] {
                "ฉากที่ 6 — อัศวินคนสุดท้าย",
                "สถานที่: ใจกลางรอยแยกมิติ, การต่อสู้ครั้งสุดท้าย",
                "บทบรรยาย — ตัวเอก\n\nเส้นทางสู่รอยแยกเต็มไปด้วยซากเมือง ธงที่ไหม้เกรียม และดาบของผู้ที่ไม่มีวันกลับมา\nไม่มีเสียงตอบรับ ไม่มีทหารให้สั่งการ\nเหลือเพียงดาบในมือกับรอยแยกที่กำลังฉีกโลก",
                "บทบรรยาย — ตัวเอก\n\nข้าเข้าใจแล้วว่ารอยแยกไม่ใช่แค่ประตู แต่มันคือบาดแผลที่เชื่อมโลกของเราเข้ากับดินแดนปีศาจ\nข้าเห็นมันกำลังขยายตัว หากปล่อยไว้ จะไม่เหลือสิ่งใดให้ปกป้อง",
                "ตัวเอก\n\n“นี่คือต้นเหตุ…”\n\n“ข้าจะไม่ยอมให้เจ้าเอาสิ่งที่เหลือไปอีก”",
                "ตัวเอก\n\n“ในนามของอัศวินแห่งโบอา… ข้าจะจบสงครามนี้”",
                "บทบรรยาย — ตัวเอก\n\nข้าเห็นรอยแยกหดตัว แสงสีแดงดับลง และปีศาจสลายไปพร้อมพลังที่หล่อเลี้ยงพวกมัน\nท้องฟ้าค่อย ๆ กลับคืนสู่สภาพเดิม หลายเดือนต่อมา โลกก็สงบลงเป็นครั้งแรก",
                "ตัวเอก — เสียงแผ่วเบา\n\n“จบแล้วสินะ”",
                "บทบรรยาย — ตัวเอก\n\nข้าทรุดลง ดาบหลุดจากมือ\nข้าไม่รู้ว่าจะรอดถึงวันพรุ่งนี้หรือไม่\nแต่ไม่มีปีศาจผ่านประตูนั้นมาได้อีก\nแค่นั้นก็เพียงพอแล้ว",
                "ฉากที่ 7 — รุ่งอรุณของอาณาจักร",
                "สถานที่: เมืองหลวงของโบอา, หลายเดือนต่อมา",
                "บทบรรยาย — ตัวเอก\n\nหลายเดือนต่อมา ข้าเดินไปยังเนินเขาที่มองเห็นอาณาจักรทั้งเมือง เสื้อคลุมสีแดงขาดวิ่นปลิวไปตามสายลม แสงอาทิตย์ยามเช้าส่องกระทบหลังคาที่กำลังสร้างขึ้นใหม่\n\nราชาปีศาจพ่ายแพ้ รอยแยกประตูมิติถูกปิดลง แสงอรุณสาดส่องมายังโบอา และงานฟื้นฟูอันยาวนานก็เริ่มต้นขึ้น\n\nโบอาไม่อาจกลับไปเป็นเหมือนวันวาน ผู้ที่จากไปจะไม่มีวันหวนคืน และบาดแผลจากสงครามจะยังคงอยู่ในความทรงจำของผู้รอดชีวิต\n\nแต่ในดินแดนที่ครั้งหนึ่งเคยเต็มไปด้วยเสียงกรีดร้อง บัดนี้ข้าได้ยินเสียงค้อนของช่างก่อสร้าง เสียงหัวเราะของเด็ก ๆ และเสียงระฆังที่ต้อนรับวันใหม่\n\nฤดูกาลผันผ่าน เสียงระฆังกลับมาดังอีกครั้ง บ้านเรือนค่อย ๆ ถูกสร้างขึ้นใหม่ ตลาดกลับมาเต็มไปด้วยเสียงผู้คน ข้าเฝ้ามองอาณาจักรที่เคยให้คำสัตย์ว่าจะปกป้อง",
                "ตัวเอก — บทพูดสุดท้าย\n\n“ข้าขอโทษที่ไม่อาจพาพวกเจ้าทุกคนกลับบ้านได้...”\n\n“แต่ข้าสัญญาไว้แล้ว ว่าจะปกป้องผู้คนที่ยังเหลืออยู่”\n\n“และตราบใดที่ยังมีใครต้องการความช่วยเหลือ... ข้าก็จะยังคงเป็นอัศวิน”"
            }, () => GameManager.Instance.Load("MainMenu", false));
            _ending = true;
        }

        private void RollCredits()
        {
            _rolling = true; _elapsed = 0f;
            _speaker.transform.parent.gameObject.SetActive(false);
            _body.transform.parent.gameObject.SetActive(false);
            var viewport = CreatePanel(_panel.transform.Find("Backdrop"), "Rolling Credits", new Color(0.01f, 0.01f, 0.02f, 0.92f), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            viewport.AddComponent<RectMask2D>();
            _creditsViewport = viewport.GetComponent<RectTransform>();
            var assetCredits = Resources.Load<TextAsset>("Credits/EndCreditAssets");
            if (assetCredits == null) Debug.LogError("End-credit asset list is missing.");
            // Credits retain the original game, team, asset and creator names in both languages.
            var text = CreateText(viewport.transform,
                "<size=44><b>THE LAST KNIGHT</b></size>\n\nBOA\n\n\nCreated by\nThe Ngu lueam team\n\n\nAn oath endures.\nBoa's story continues.\n\n\n" +
                (assetCredits != null ? assetCredits.text.Trim() + "\n\n\n" : "") +
                "Thank you for playing.", 24, Color.white, TextAnchor.UpperCenter, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f), false);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.2f;
            _credits = text.rectTransform;
            _credits.pivot = new Vector2(0.5f, 1f);
            _credits.sizeDelta = new Vector2(-40f, 0f);
            Canvas.ForceUpdateCanvases();
            // Measure wrapped text rather than imposing a fixed height or duration.
            _credits.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, text.preferredHeight + 48f);
            _credits.anchoredPosition = new Vector2(0f, -_creditsViewport.rect.height);
            BuildCreditsBanners(viewport.transform);
        }

        private void BuildCreditsBanners(Transform parent)
        {
            var artwork = Resources.Load<TextAsset>("Credits/PythonSwordBanner");
            _creditsBanners = null;
            if (artwork == null)
            {
                Debug.LogError("End-credit ASCII banner is missing.");
                return;
            }
            if (_creditsBannerFont == null)
                _creditsBannerFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Liberation Mono", "DejaVu Sans Mono" }, 14);

            _creditsBanners = new RectTransform[3];
            _creditsBannerStart = _credits.rect.height + CreditsBannerGap;
            for (int i = 0; i < _creditsBanners.Length; i++)
            {
                var banner = CreateText(parent, artwork.text.TrimEnd(), 14, new Color(0.95f, 0.78f, 0.43f),
                    TextAnchor.UpperLeft, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), false);
                banner.gameObject.name = "Python Sword ASCII Banner " + (i + 1);
                banner.font = _creditsBannerFont;
                banner.supportRichText = false;
                banner.horizontalOverflow = HorizontalWrapMode.Overflow;
                banner.verticalOverflow = VerticalWrapMode.Overflow;
                banner.lineSpacing = 0.9f;
                var rect = banner.rectTransform;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(banner.preferredWidth + 16f, banner.preferredHeight + 16f);
                float scale = Mathf.Min(1f, Mathf.Max(32f, _creditsViewport.rect.height - 64f) / rect.rect.height);
                rect.localScale = Vector3.one * scale;
                if (i == 0) _creditsBannerPeriod = rect.rect.height * scale + CreditsBannerGap;
                rect.anchoredPosition = new Vector2(0f, -_creditsViewport.rect.height - _creditsBannerStart - i * _creditsBannerPeriod);
                _creditsBanners[i] = rect;
            }
        }
        private void Update()
        {
            if (_panel == null || Time.frameCount <= _openedFrame + 1 || _cutscenePlaying) return;
            if (_sceneTitlePanel != null)
            {
                _sceneTitleElapsed += Time.unscaledDeltaTime;
                if (_sceneTitleElapsed >= 2.2f) Advance();
                else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Advance();
                return;
            }
            if (_isDialogueHidden)
            {
                if ((Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.hKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) ||
                    (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
                {
                    ToggleHideDialogue();
                }
                return;
            }
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                ToggleHideDialogue();
                return;
            }
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ToggleHideDialogue();
                return;
            }
            if (_ending)
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_rolling)
                {
                    float position = -_creditsViewport.rect.height + _elapsed * CreditsScrollSpeed;
                    if (_creditsBanners != null && position >= _creditsBannerStart + _creditsBannerPeriod)
                    {
                        // Recycle identical banners after the text has left the viewport.
                        // Bound the elapsed time so the loop remains smooth during long sessions.
                        position = _creditsBannerStart + Mathf.Repeat(position - _creditsBannerStart, _creditsBannerPeriod);
                        _elapsed = (position + _creditsViewport.rect.height) / CreditsScrollSpeed;
                    }
                    _credits.anchoredPosition = new Vector2(0f, position);
                    if (_creditsBanners != null)
                    {
                        for (int i = 0; i < _creditsBanners.Length; i++)
                            _creditsBanners[i].anchoredPosition = new Vector2(0f, position - _creditsBannerStart - i * _creditsBannerPeriod);
                    }
                    else if (position >= _credits.rect.height) { Finish(); return; }
                }
                else if (_elapsed >= 9f) { Advance(); return; }
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Advance();
        }
        private void Advance()
        {
            if (_cutscenePlaying) return;
            if (_isDialogueHidden)
            {
                ToggleHideDialogue();
                return;
            }
            _openedFrame = Time.frameCount;
            _elapsed = 0f;
            if (_sceneTitlePanel != null) HideSceneTitle();
            if (_rolling) { Finish(); return; }

            // Check if current line is the end of the quote:
            // "ตัวเอก\n\n“ในนามของอัศวินแห่งโบอา… ข้าจะจบสงครามนี้”"
            if (_index >= 0 && _index < _lines.Length &&
                (_lines[_index].Contains("ข้าจะจบสงครามนี้") || _lines[_index].Contains("I will end this war")))
            {
                StartCoroutine(PlayWarriorSkill4Cutscene());
                return;
            }

            if (++_index >= _lines.Length)
            {
                if (_ending) RollCredits();
                else if (_currentStoryScene == 5)
                {
                    StartCoroutine(TransitionScene5ToGameplayRoutine());
                }
                else Finish();
            }
            else SetLine(_index);
        }

        private Sprite CaptureSceneView()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return null;

            int width = Screen.width > 0 ? Screen.width : 1280;
            int height = Screen.height > 0 ? Screen.height : 720;
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private System.Collections.IEnumerator TransitionScene5ToGameplayRoutine()
        {
            _cutscenePlaying = true;
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            if (_menuPanel != null) _menuPanel.SetActive(false);

            if (_blurMaterial == null)
            {
                var shader = Shader.Find("Custom/FastBoxBlur");
                if (shader != null) _blurMaterial = new Material(shader);
            }
            if (_blurMaterial != null && _storyBackground != null)
            {
                _storyBackground.material = _blurMaterial;
            }

            // Phase 1: Fast Box Blur out Scene 5 image (~0.4s)
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                if (_blurMaterial != null) _blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(0f, 15f, t / 0.4f));
                yield return null;
            }
            if (_blurMaterial != null) _blurMaterial.SetFloat("_BlurSize", 15f);

            // Phase 2: 1-second pause/interval while blurred
            float waitTimer = 0f;
            while (waitTimer < 1.0f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // Phase 3: Fade out blurred image to reveal gameplay (~0.5s)
            float fadeTimer = 0f;
            var startColor = _storyBackground != null ? _storyBackground.color : Color.white;
            while (fadeTimer < 0.5f)
            {
                fadeTimer += Time.unscaledDeltaTime;
                float progress = fadeTimer / 0.5f;
                if (_storyBackground != null)
                {
                    _storyBackground.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);
                }
                if (_blurMaterial != null)
                {
                    _blurMaterial.SetFloat("_BlurSize", Mathf.Lerp(15f, 0f, progress));
                }
                yield return null;
            }

            _cutscenePlaying = false;
            Finish();
        }

        private System.Collections.IEnumerator PlayWarriorSkill4Cutscene()
        {
            _cutscenePlaying = true;
            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            if (_menuPanel != null) _menuPanel.SetActive(false);

            var redGate = FindAnyObjectByType<TheLastKnight.Environment.RedGate>();
            Vector3 targetPos = redGate != null
                ? redGate.transform.position + new Vector3(0f, 2.2f, 0f)
                : new Vector3(46f, 1.6f, 0f);

            var cam = UnityEngine.Camera.main;
            Vector3 camOrigin = cam != null ? cam.transform.position : Vector3.zero;
            if (cam != null && redGate != null)
            {
                cam.transform.position = new Vector3(targetPos.x, targetPos.y, camOrigin.z);
            }

            // 1. Pause after dialogue closes before unleashing skill (anticipation)
            float pauseIntro = 0.8f;
            while (pauseIntro > 0f) { pauseIntro -= Time.unscaledDeltaTime; yield return null; }

            // 2. Play upside-down WarriorSkill4 animation
            var prefab = Resources.Load<GameObject>("WarriorSkill4_Effect");
            GameObject effect = null;
            if (prefab != null)
            {
                effect = Instantiate(prefab, targetPos, Quaternion.identity);
                effect.transform.localScale = new Vector3(2.25f, 2.25f, 1f);
            }

            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("excalibur");

            // Wait for full slash animation to complete (~0.6s)
            float slashTimer = 0.6f;
            while (slashTimer > 0f) { slashTimer -= Time.unscaledDeltaTime; yield return null; }

            // 3. Pause between skill finish and gate breaking
            float pauseBetween = 0.65f;
            while (pauseBetween > 0f) { pauseBetween -= Time.unscaledDeltaTime; yield return null; }

            // 4. Red Gate breaks and crumbles!
            if (redGate != null)
            {
                redGate.BreakGate();
            }
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("enemy_death");

            // Camera shake during destruction
            float shakeTimer = 0f;
            while (shakeTimer < 0.35f)
            {
                shakeTimer += Time.unscaledDeltaTime;
                if (cam != null)
                {
                    cam.transform.position = new Vector3(targetPos.x, targetPos.y, camOrigin.z) + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.4f);
                }
                yield return null;
            }
            if (cam != null)
            {
                cam.transform.position = new Vector3(targetPos.x, targetPos.y, camOrigin.z);
            }

            if (effect != null) Destroy(effect);

            // Wait for broken animation and rubble to settle
            float settleTimer = 0.8f;
            while (settleTimer > 0f) { settleTimer -= Time.unscaledDeltaTime; yield return null; }

            // 5. Calm pause after gate is destroyed before narrative text appears
            float pauseAfter = 1.2f;
            while (pauseAfter > 0f) { pauseAfter -= Time.unscaledDeltaTime; yield return null; }

            if (_dialoguePanel != null) _dialoguePanel.SetActive(true);
            if (_menuPanel != null) _menuPanel.SetActive(true);
            _cutscenePlaying = false;
            _openedFrame = Time.frameCount;
            _elapsed = 0f;

            if (++_index >= _lines.Length)
            {
                if (_ending) RollCredits(); else Finish();
            }
            else SetLine(_index);
        }
        private void Finish()
        {
            if (_panel == null) return;
            if (_backgroundTransition != null) { StopCoroutine(_backgroundTransition); _backgroundTransition = null; }
            if (_logPanel != null) Destroy(_logPanel);
            Destroy(_panel); _panel = _logPanel = null; Time.timeScale = 1f;
            _isDialogueHidden = false;
            _hideButtonText = null;
            SetHudVisible(true);
            GameManager.Instance.SetInputBlocked(false);
            if (!_ending && _currentStoryScene > 0)
                TheLastKnight.Audio.AudioManager.Instance?.PlaySceneMusic(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            _complete?.Invoke();
        }
    }
}
