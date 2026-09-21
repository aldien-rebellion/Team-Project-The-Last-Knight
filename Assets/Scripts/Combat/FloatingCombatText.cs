using UnityEngine;

namespace TheLastKnight.Combat
{
    public class FloatingCombatText : MonoBehaviour
    {
        private TextMesh _text;
        private float _remaining = 1f;

        public static void Show(Vector3 position, string message, Color color)
        {
            var prefab = Resources.Load<FloatingCombatText>("FloatingCombatText");
            var popup = prefab != null ? Instantiate(prefab, position, Quaternion.identity)
                : new GameObject("Combat Text").AddComponent<FloatingCombatText>();
            popup.transform.position = position + Vector3.up;
            popup._text = popup.GetComponent<TextMesh>();
            if (popup._text == null) popup._text = popup.gameObject.AddComponent<TextMesh>();
            popup._text.text = message;
            popup._text.color = color;
            popup._text.fontSize = 48;
            popup._text.characterSize = 0.06f;
            popup._text.anchor = TextAnchor.MiddleCenter;
            popup.GetComponent<MeshRenderer>().sortingOrder = 100;
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
