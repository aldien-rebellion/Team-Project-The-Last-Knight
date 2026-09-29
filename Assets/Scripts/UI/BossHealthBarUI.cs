using TheLastKnight.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.UI
{
    /// <summary>Screen-space boss health bar shown only while a BossArena is active.</summary>
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        private static BossHealthBarUI _active;

        private EnemyStats _boss;
        private Image _fill;
        private TextMeshProUGUI _title;

        public static bool IsShowing => _active != null;

        public static void Show(EnemyStats boss)
        {
            Hide();
            if (boss == null || boss.IsDead) return;

            var root = new GameObject("Boss Health Bar", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(BossHealthBarUI));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            _active = root.GetComponent<BossHealthBarUI>();
            _active.Initialize(boss);
        }

        public static void Hide()
        {
            if (_active != null) Destroy(_active.gameObject);
            _active = null;
        }

        private void Initialize(EnemyStats boss)
        {
            _boss = boss;
            var panel = CreateImage("Boss Bar Panel", transform, new Color(0.04f, 0.04f, 0.06f, 0.92f));
            var panelRect = panel.rectTransform;
            // Top-center anchoring keeps the bar centered at every resolution and
            // leaves the top-left player HUD (level, HP and stamina) unobstructed.
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -12f);
            panelRect.sizeDelta = new Vector2(760f, 88f);

            _title = CreateText("Boss Title", panel.transform, 30, Color.white);
            var titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.03f, 1f);
            titleRect.anchorMax = new Vector2(0.97f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -5f);
            titleRect.sizeDelta = new Vector2(0f, 38f);
            _title.text = $"{DisplayName(boss.name)}   Lv. {boss.Level}";

            var background = CreateImage("Health Background", panel.transform, new Color(0.12f, 0.12f, 0.15f, 1f));
            var backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0.02f, 0f);
            backgroundRect.anchorMax = new Vector2(0.98f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 0f);
            backgroundRect.anchoredPosition = new Vector2(0f, 9f);
            backgroundRect.sizeDelta = new Vector2(0f, 27f);

            _fill = CreateImage("Health Fill", background.transform, new Color(0.8f, 0.08f, 0.1f, 1f));
            var fillRect = _fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            _boss.OnHealthChanged += UpdateHealth;
            _boss.OnDeath += HandleDeath;
            UpdateHealth(_boss.CurrentHealth, _boss.MaxHealth);
        }

        private void OnDestroy()
        {
            if (_boss != null)
            {
                _boss.OnHealthChanged -= UpdateHealth;
                _boss.OnDeath -= HandleDeath;
            }
            if (_active == this) _active = null;
        }

        private void UpdateHealth(float current, float maximum)
        {
            if (_fill == null || maximum <= 0f) return;
            _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(current / maximum), 1f);
        }

        private void HandleDeath() => Hide();

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static string DisplayName(string value) => value.Replace("(Clone)", string.Empty).Trim();
    }
}
