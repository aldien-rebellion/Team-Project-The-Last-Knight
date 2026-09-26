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
            veil.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, 0.94f);

            var body = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            body.transform.SetParent(veil.transform, false);
            var rect = body.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.2f, 0.08f); rect.anchorMax = new Vector2(0.8f, 0.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var layout = body.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true; layout.childForceExpandHeight = false;

            content = body.transform;
            Label(content, title, 42, new Color(0.9f, 0.77f, 0.48f));
            return root;
        }

        public static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null)
            {
                module = es.gameObject.AddComponent<InputSystemUIInputModule>();
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
            go.GetComponent<LayoutElement>().preferredHeight = 46;
            go.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.28f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
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
            go.GetComponent<LayoutElement>().preferredHeight = 28;

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            var rect = track.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.35f); rect.anchorMax = new Vector2(1, 0.65f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(go.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 28);
            handle.GetComponent<Image>().color = new Color(0.9f, 0.77f, 0.48f);

            var slider = go.GetComponent<Slider>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            slider.onValueChanged.AddListener(v => { label.text = $"{name}  {Mathf.RoundToInt(v * 100)}%"; changed(v); });
            return slider;
        }
    }
}
