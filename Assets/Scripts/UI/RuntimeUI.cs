using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

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

        public static Slider Slider(Transform parent, string name, float value, UnityAction<float> changed)
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
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
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
    }
}
