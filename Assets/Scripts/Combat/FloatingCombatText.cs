using UnityEngine;

namespace TheLastKnight.Combat
{
    public class FloatingCombatText : MonoBehaviour
    {
        private TextMesh _text;
        private float _remaining = 1f;
        private static Font _thaiFont;

        public static void Show(Vector3 position, string message, Color color)
        {
            var prefab = Resources.Load<FloatingCombatText>("FloatingCombatText");
            var popup = prefab != null ? Instantiate(prefab, position, Quaternion.identity)
                : new GameObject("Combat Text").AddComponent<FloatingCombatText>();
            popup.transform.position = position + Vector3.up;
            popup._text = popup.GetComponent<TextMesh>();
            if (popup._text == null) popup._text = popup.gameObject.AddComponent<TextMesh>();
            for (int i = 0; i < message.Length; i++)
            {
                if (message[i] < '\u0E00' || message[i] > '\u0E7F') continue;
                if (_thaiFont == null)
                    _thaiFont = Font.CreateDynamicFontFromOSFont(new[] { "Tahoma", "Leelawadee UI", "Arial" }, 48)
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                popup._text.font = _thaiFont;
                popup.GetComponent<MeshRenderer>().sharedMaterial = _thaiFont.material;
                break;
            }
            TheLastKnight.UI.LocalizedText.Set(popup._text, message);
            popup._text.color = color;
            popup._text.fontSize = 48;
            popup._text.characterSize = 0.06f;
            popup._text.anchor = TextAnchor.MiddleCenter;
            popup.GetComponent<MeshRenderer>().sortingOrder = 100;
            popup.GetComponent<MeshRenderer>().sortingLayerName = "InGame_UI";
        }

        private void Update()
        {
            transform.position += Vector3.up * Time.deltaTime;
            _remaining -= Time.deltaTime;
            if (_text != null)
            {
                var color = _text.color;
                color.a = Mathf.Clamp01(_remaining);
                _text.color = color;
            }
            if (_remaining <= 0f) Destroy(gameObject);
        }
    }
}
