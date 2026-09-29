using System.Collections;
using UnityEngine;

namespace TheLastKnight.Combat
{
    /// <summary>Visual-only apparition shown during the executioner's Dark Summon.</summary>
    public sealed class UndeadExecutionerSummonEffect : MonoBehaviour
    {
        [SerializeField] private Sprite[] _appearFrames;
        [SerializeField] private Sprite[] _idleFrames;
        [SerializeField] private Sprite[] _deathFrames;
        [SerializeField] private float _frameDuration = 0.1f;
        [SerializeField] private float _idleDuration = 0.45f;
        [SerializeField] private float _headGap = 0.08f;

        private Coroutine _routine;
        private GameObject _visual;
        private SpriteRenderer _targetSprite;

        private void LateUpdate()
        {
            if (_visual == null || _targetSprite == null) return;
            Bounds bounds = SpriteVisualBounds.GetWorldBounds(_targetSprite);
            _visual.transform.position = new Vector3(bounds.center.x, bounds.max.y + _headGap, _targetSprite.transform.position.z);
            _visual.GetComponent<SpriteRenderer>().flipX = _targetSprite.flipX;
        }

        public void Play(GameObject player)
        {
            if (player == null || _appearFrames == null || _appearFrames.Length == 0) return;
            _targetSprite = player.GetComponent<SpriteRenderer>();
            if (_targetSprite == null) _targetSprite = player.GetComponentInChildren<SpriteRenderer>();
            if (_targetSprite == null) return;
            if (_routine != null) StopCoroutine(_routine);
            if (_visual != null) Destroy(_visual);
            _routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            _visual = new GameObject("Dark Summon Effect");
            _visual.transform.SetParent(transform, false);
            var effectSprite = _visual.AddComponent<SpriteRenderer>();
            effectSprite.sortingLayerID = _targetSprite.sortingLayerID;
            effectSprite.sortingOrder = _targetSprite.sortingOrder + 1;
            effectSprite.flipX = _targetSprite.flipX;
            LateUpdate();

            yield return PlayFrames(effectSprite, _appearFrames);
            float idleUntil = Time.time + Mathf.Max(0f, _idleDuration);
            int idleIndex = 0;
            while (_idleFrames != null && _idleFrames.Length > 0 && Time.time < idleUntil)
            {
                effectSprite.sprite = _idleFrames[idleIndex++ % _idleFrames.Length];
                yield return new WaitForSeconds(_frameDuration);
            }
            yield return PlayFrames(effectSprite, _deathFrames);
            Destroy(_visual);
            _visual = null;
            _routine = null;
        }

        private IEnumerator PlayFrames(SpriteRenderer renderer, Sprite[] frames)
        {
            if (frames == null) yield break;
            foreach (var frame in frames)
            {
                renderer.sprite = frame;
                yield return new WaitForSeconds(_frameDuration);
            }
        }
    }
}
