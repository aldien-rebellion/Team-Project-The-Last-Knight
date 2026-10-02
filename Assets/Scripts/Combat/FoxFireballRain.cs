using System.Collections;
using UnityEngine;
using TheLastKnight.AI;
using TheLastKnight.Stats;

namespace TheLastKnight.Combat
{
    public sealed class FoxFireballRain : MonoBehaviour
    {
        [SerializeField] private Sprite[] _fallFrames;
        [SerializeField] private Sprite[] _impactFrames;
        [SerializeField, Min(0.1f)] private float _dropHeight = 8f;
        [SerializeField, Min(0.1f)] private float _fallTime = 0.85f;
        [SerializeField, Min(0.1f)] private float _impactRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float _spreadRadius = 7f;
        [SerializeField, Min(0.1f)] private float _rainDuration = 4f;
        [SerializeField, Min(0.01f)] private float _spawnInterval = 0.08f;
        private int _activeDrops;
        private FoxAudioController _foxAudio;

        private void Awake()
        {
            _foxAudio = GetComponent<FoxAudioController>();
        }

        public IEnumerator CastRoutine(GameObject target, float damage)
        {
            if (target == null || _fallFrames == null || _fallFrames.Length == 0) yield break;

            Collider2D body = null;
            foreach (var collider in target.GetComponentsInChildren<Collider2D>())
            {
                if (collider.enabled && !collider.isTrigger) { body = collider; break; }
            }
            Vector2 center = body != null
                ? new Vector2(body.bounds.center.x, body.bounds.min.y + _impactRadius * 0.5f)
                : (Vector2)target.transform.position;
            float endTime = Time.time + _rainDuration;
            float lastSpawnTime = endTime - _fallTime;
            _activeDrops = 0;
            while (Time.time <= lastSpawnTime)
            {
                float offsetX = Random.Range(-_spreadRadius, _spreadRadius);
                _foxAudio?.PlayFireballLaunch();
                _activeDrops++;
                StartCoroutine(Drop(new Vector2(center.x + offsetX, center.y), Mathf.Max(1f, damage)));
                yield return new WaitForSeconds(_spawnInterval);
            }
            while (Time.time < endTime) yield return null;
            while (_activeDrops > 0) yield return null;
        }

        private IEnumerator Drop(Vector2 landing, float damage)
        {
            var fireball = new GameObject("Fox Fireball");
            var renderer = fireball.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID = GetComponent<SpriteRenderer>().sortingLayerID;
            renderer.sortingOrder = short.MaxValue;
            Vector2 start = landing + Vector2.up * _dropHeight;
            fireball.transform.position = start;

            float elapsed = 0f;
            while (elapsed < _fallTime)
            {
                float progress = Mathf.Clamp01(elapsed / _fallTime);
                fireball.transform.position = Vector2.Lerp(start, landing, progress);
                renderer.sprite = _fallFrames[Mathf.Min(_fallFrames.Length - 1,
                    Mathf.FloorToInt(progress * _fallFrames.Length))];
                elapsed += Time.deltaTime;
                yield return null;
            }

            fireball.transform.position = landing;
            _foxAudio?.PlayFireballImpact();
            foreach (var hit in Physics2D.OverlapCircleAll(landing, _impactRadius))
            {
                var player = hit.GetComponentInParent<PlayerStats>();
                if (player == null) continue;
                player.TakeDamage(damage);
                break;
            }

            if (_impactFrames != null && _impactFrames.Length > 0)
            {
                for (int i = 0; i < _impactFrames.Length; i++)
                {
                    renderer.sprite = _impactFrames[i];
                    yield return new WaitForSeconds(0.055f);
                }
            }
            Destroy(fireball);
            _activeDrops = Mathf.Max(0, _activeDrops - 1);
        }
    }
}
