using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.UI
{
    /// <summary>Brief full-screen notice used when an area boss opens the next route.</summary>
    public sealed class AreaUnlockNoticeUI : MonoBehaviour
    {
        private const float VisibleDuration = 3f;
        private const float FadeDuration = 0.5f;
        private static AreaUnlockNoticeUI _active;

        public static void Show()
        {
            if (_active != null) Destroy(_active.gameObject);

            var root = new GameObject("Area Unlock Notice", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AreaUnlockNoticeUI));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _active = root.GetComponent<AreaUnlockNoticeUI>();
            _active.Build();
            _active.StartCoroutine(_active.ShowThenFade());
        }

        private CanvasGroup _group;

        private void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            var labelObject = new GameObject("Unlock Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            labelObject.transform.SetParent(transform, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 70f);
            rect.sizeDelta = new Vector2(1200f, 120f);

            var text = labelObject.GetComponent<Text>();
            // TMP's bundled Liberation Sans asset has no Thai glyphs. Use a Windows
            // system font with Thai support for this Thai-only notification.
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Tahoma", "Leelawadee UI", "Arial" }, 54)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "ปลดล็อคพื้นที่ต่อไป";
            text.fontSize = 54;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.82f, 0.28f, 1f);
            text.raycastTarget = false;

            var outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.05f, 0.02f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private IEnumerator ShowThenFade()
        {
            yield return new WaitForSecondsRealtime(VisibleDuration);
            for (var elapsed = 0f; elapsed < FadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - (elapsed / FadeDuration);
                yield return null;
            }
            _group.alpha = 0f;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
        }
    }
}
