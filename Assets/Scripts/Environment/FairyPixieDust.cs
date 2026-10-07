using UnityEngine;

namespace TheLastKnight.Environment
{
    /// <summary>A short burst of pixel stars. Contains no physics components.</summary>
    public sealed class FairyPixieDust : MonoBehaviour
    {
        private readonly SpriteRenderer[] _stars = new SpriteRenderer[24];
        private readonly Vector3[] _velocities = new Vector3[24];
        private readonly Color[] _colors = new Color[24];
        private float _elapsed;

        public static void Burst(Vector3 position, Sprite sprite, Color color, int layer, int order)
        {
            if (sprite == null) return;
            var go = new GameObject("Fairy Pixie Dust");
            go.transform.position = position;
            go.AddComponent<FairyPixieDust>().Initialize(sprite, color, layer, order);
        }

        private void Initialize(Sprite sprite, Color color, int layer, int order)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                var star = new GameObject("Pixie star");
                star.transform.SetParent(transform, false);
                var renderer = star.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order;
                _colors[i] = Color.Lerp(color, Color.white, Random.Range(0.2f, 0.8f));
                renderer.color = _colors[i];
                _stars[i] = renderer;
                var direction = Random.insideUnitCircle;
                _velocities[i] = new Vector3(direction.x, direction.y + 0.25f, 0f) * Random.Range(0.7f, 1.9f);
                star.transform.localScale = Vector3.one * Random.Range(0.35f, 0.8f);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - _elapsed / 0.75f);
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].transform.localPosition += _velocities[i] * Time.deltaTime;
                var color = _colors[i];
                color.a = alpha * (0.6f + 0.4f * Mathf.Sin(_elapsed * 25f + i));
                _stars[i].color = color;
            }
            if (_elapsed >= 0.75f) Destroy(gameObject);
        }
    }
}
