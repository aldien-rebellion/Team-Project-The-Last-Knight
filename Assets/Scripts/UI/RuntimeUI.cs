using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TheLastKnight.Core;

namespace TheLastKnight.UI
{
    public static class RuntimeUI
    {
        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static GameObject Panel(string title, out Transform content, int sortingOrder = 200)
        {
            EnsureEventSystem();
            var root = new GameObject(title, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var veil = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            veil.transform.SetParent(root.transform, false);
            var vr = veil.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            var veilImg = veil.GetComponent<Image>();
            veilImg.color = new Color(0.025f, 0.035f, 0.065f, 0.94f);
            veilImg.raycastTarget = true;

            var body = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            body.transform.SetParent(veil.transform, false);
            var rect = body.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.08f); rect.anchorMax = new Vector2(0.8f, 0.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var layout = body.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.childControlWidth = true; layout.childForceExpandWidth = true;

            content = body.transform;
            Label(content, title, 38, new Color(0.9f, 0.77f, 0.48f));
            return root;
        }

        public static void EnsureEventSystem()
        {
            var all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
            EventSystem activeEs = null;
            if (all != null && all.Length > 0)
            {
                activeEs = all[0];
                for (int i = 1; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].gameObject != null)
                    {
                        all[i].enabled = false;
                        all[i].gameObject.SetActive(false);
                        if (Application.isPlaying) Object.Destroy(all[i].gameObject);
                        else Object.DestroyImmediate(all[i].gameObject);
                    }
                }
            }
            else
            {
                var go = new GameObject("EventSystem");
                activeEs = go.AddComponent<EventSystem>();
            }

            if (activeEs != null)
            {
                activeEs.enabled = true;
                activeEs.gameObject.SetActive(true);

                var legacy = activeEs.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                {
                    legacy.enabled = false;
                    if (Application.isPlaying) Object.Destroy(legacy);
                    else Object.DestroyImmediate(legacy);
                }

                var module = activeEs.GetComponent<InputSystemUIInputModule>();
                if (module == null)
                {
                    module = activeEs.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                module.enabled = true;

                if (UnityEngine.InputSystem.InputSystem.actions != null)
                {
                    module.actionsAsset = UnityEngine.InputSystem.InputSystem.actions;
                    var uiMap = UnityEngine.InputSystem.InputSystem.actions.FindActionMap("UI");
                    if (uiMap != null && !uiMap.enabled)
                    {
                        uiMap.Enable();
                    }
                }
                else
                {
                    module.AssignDefaultActions();
                }
            }
        }

        public static Text Label(Transform parent, string text, int size = 20, Color? color = null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Font; label.text = text; label.fontSize = size; label.color = color ?? Color.white;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = size * 1.6f + (text.Length > 100 ? 70 : 0);
            return label;
        }

        public static Button Button(Transform parent, string text, UnityAction action)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 48;
            le.minHeight = 44;

            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = img;

            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.20f, 0.28f, 1f);
            colors.highlightedColor = new Color(0.26f, 0.36f, 0.52f, 1f);
            colors.pressedColor = new Color(0.10f, 0.12f, 0.18f, 1f);
            colors.selectedColor = new Color(0.24f, 0.34f, 0.48f, 1f);
            button.colors = colors;

            var label = Label(go.transform, text);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            button.onClick.AddListener(() => { TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("click"); action(); });
            return button;
        }

        public static Slider Slider(Transform parent, string name, float value, UnityAction<float> changed, float min = 0f, float max = 1f)
        {
            var label = Label(parent, $"{name}  {Mathf.RoundToInt(value * 100)}%", 20);
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 32;

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            var rect = track.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.35f); rect.anchorMax = new Vector2(1, 0.65f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            var trackImg = track.GetComponent<Image>();
            trackImg.color = new Color(0.2f, 0.25f, 0.35f);
            trackImg.raycastTarget = true;

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(go.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(24, 30);
            var handleImg = handle.GetComponent<Image>();
            handleImg.color = new Color(0.9f, 0.77f, 0.48f);
            handleImg.raycastTarget = true;

            var slider = go.GetComponent<Slider>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;
            slider.minValue = min; slider.maxValue = max; slider.value = Mathf.Clamp(value, min, max);
            slider.onValueChanged.AddListener(v => { label.text = $"{name}  {Mathf.RoundToInt(v * 100)}%"; changed(v); });
            return slider;
        }

        public static Button ActionRow(Transform parent, string labelText, string buttonText, UnityAction onButtonClick)
        {
            var rowGo = new GameObject("Row_" + labelText, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            var le = rowGo.GetComponent<LayoutElement>();
            le.preferredHeight = 36;
            le.minHeight = 32;

            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var lbl = Label(rowGo.transform, labelText, 17, Color.white);
            lbl.alignment = TextAnchor.MiddleLeft;
            var lblLe = lbl.GetComponent<LayoutElement>();
            lblLe.flexibleWidth = 1f;

            var btn = Button(rowGo.transform, buttonText, onButtonClick);
            var btnLe = btn.GetComponent<LayoutElement>();
            btnLe.preferredWidth = 180;
            btnLe.preferredHeight = 34;
            btnLe.flexibleWidth = 0f;

            return btn;
        }

        public static UnityEngine.UI.InputField InputField(Transform parent, string placeholderText, string defaultText, UnityAction<string> onValueChanged = null)
        {
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.minHeight = 40;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.14f, 0.20f, 0.95f);
            bg.raycastTarget = true;

            // Placeholder Text
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            phGo.transform.SetParent(go.transform, false);
            var phRect = phGo.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(14, 2); phRect.offsetMax = new Vector2(-14, -2);
            var phText = phGo.GetComponent<Text>();
            phText.font = Font;
            phText.fontSize = 20;
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0.6f, 0.65f, 0.75f, 0.6f);
            phText.alignment = TextAnchor.MiddleLeft;
            phText.text = placeholderText;

            // Input Text
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14, 2); textRect.offsetMax = new Vector2(-14, -2);
            var inputText = textGo.GetComponent<Text>();
            inputText.font = Font;
            inputText.fontSize = 20;
            inputText.color = new Color(0.95f, 0.90f, 0.75f, 1f);
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;

            var input = go.GetComponent<UnityEngine.UI.InputField>();
            input.textComponent = inputText;
            input.placeholder = phText;
            input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            input.characterLimit = 32;
            input.text = defaultText;

            if (onValueChanged != null)
            {
                input.onValueChanged.AddListener(onValueChanged);
            }

            return input;
        }

        public static ScrollRect ScrollView(Transform parent, out Transform scrollContent, float preferredHeight = 320)
        {
            var scrollRoot = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect), typeof(LayoutElement));
            scrollRoot.transform.SetParent(parent, false);

            var le = scrollRoot.GetComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleHeight = 1f;

            var bgImg = scrollRoot.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.06f, 0.10f, 0.6f);

            var mask = scrollRoot.GetComponent<Mask>();
            mask.showMaskGraphic = true;

            var contentGo = new GameObject("ScrollContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollRoot.transform, false);

            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            scrollContent = contentGo.transform;
            return scroll;
        }

        public static GameObject WorldCard(Transform parent, PlayerSaveData save, UnityAction onPlay, UnityAction onRename, UnityAction onDelete)
        {
            var card = new GameObject("Card_" + save.worldId, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            card.transform.SetParent(parent, false);

            var le = card.GetComponent<LayoutElement>();
            le.preferredHeight = 64;
            le.minHeight = 58;

            var img = card.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.24f, 0.85f);

            var hLayout = card.GetComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10;
            hLayout.padding = new RectOffset(14, 14, 6, 6);
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childControlWidth = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandHeight = true;

            // Info column (left)
            var infoGo = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            infoGo.transform.SetParent(card.transform, false);
            var infoLe = infoGo.GetComponent<LayoutElement>();
            infoLe.flexibleWidth = 1f;

            var vLayout = infoGo.GetComponent<VerticalLayoutGroup>();
            vLayout.spacing = 2;
            vLayout.childAlignment = TextAnchor.MiddleLeft;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandHeight = false;

            string difficultyName = save.difficulty.ToString();
            string titleText = $"{save.saveName}   [{difficultyName}]";
            var titleLbl = Label(infoGo.transform, titleText, 18, new Color(0.95f, 0.85f, 0.45f));
            titleLbl.alignment = TextAnchor.MiddleLeft;
            titleLbl.GetComponent<LayoutElement>().preferredHeight = 24;

            string dateStr = !string.IsNullOrEmpty(save.lastSavedDate) ? save.lastSavedDate : "—";
            string subText = $"Lv.{save.level}  •  {save.scene}  •  {dateStr}";
            var subLbl = Label(infoGo.transform, subText, 14, new Color(0.70f, 0.75f, 0.85f));
            subLbl.alignment = TextAnchor.MiddleLeft;
            subLbl.GetComponent<LayoutElement>().preferredHeight = 20;

            // Buttons (right)
            var btnPlay = Button(card.transform, LocalizationManager.Get("BTN_LOAD_WORLD"), onPlay);
            var btnPlayLe = btnPlay.GetComponent<LayoutElement>();
            btnPlayLe.preferredWidth = 90;
            btnPlayLe.preferredHeight = 42;
            btnPlayLe.flexibleWidth = 0f;

            var btnRename = Button(card.transform, LocalizationManager.Get("BTN_RENAME_WORLD"), onRename);
            var btnRenameLe = btnRename.GetComponent<LayoutElement>();
            btnRenameLe.preferredWidth = 95;
            btnRenameLe.preferredHeight = 42;
            btnRenameLe.flexibleWidth = 0f;
            var renColors = btnRename.colors;
            renColors.normalColor = new Color(0.20f, 0.28f, 0.38f, 1f);
            renColors.highlightedColor = new Color(0.30f, 0.42f, 0.58f, 1f);
            renColors.pressedColor = new Color(0.14f, 0.18f, 0.26f, 1f);
            btnRename.colors = renColors;

            var btnDel = Button(card.transform, LocalizationManager.Get("BTN_DELETE_WORLD"), onDelete);
            var btnDelLe = btnDel.GetComponent<LayoutElement>();
            btnDelLe.preferredWidth = 65;
            btnDelLe.preferredHeight = 42;
            btnDelLe.flexibleWidth = 0f;

            var delColors = btnDel.colors;
            delColors.normalColor = new Color(0.40f, 0.15f, 0.18f, 1f);
            delColors.highlightedColor = new Color(0.60f, 0.20f, 0.25f, 1f);
            delColors.pressedColor = new Color(0.30f, 0.10f, 0.12f, 1f);
            btnDel.colors = delColors;

            return card;
        }
    }
}
