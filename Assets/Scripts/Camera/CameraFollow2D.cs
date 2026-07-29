using UnityEngine;

namespace TheLastKnight.Camera
{
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothTime = 0.2f;
        [SerializeField] private Vector3 _offset = new Vector3(0, 0, -10);

        [Header("Deadzone")]
        [SerializeField] private Vector2 _deadzoneSize = new Vector2(1f, 1f);

        [Header("Boundaries")]
        [SerializeField] private bool _useBoundaries = false;
        [SerializeField] private BoxCollider2D _boundaryBox;

        private Vector3 _currentVelocity;
        private Vector3 _targetPosition;

        private void LateUpdate()
        {
            if (_target == null) return;

            // Calculate target position with offset
            _targetPosition = _target.position + _offset;

            // Apply deadzone logic
            Vector3 currentPos = transform.position;
            Vector2 diff = new Vector2(_targetPosition.x - currentPos.x, _targetPosition.y - currentPos.y);

            if (Mathf.Abs(diff.x) < _deadzoneSize.x) _targetPosition.x = currentPos.x;
            else _targetPosition.x -= Mathf.Sign(diff.x) * _deadzoneSize.x;

            if (Mathf.Abs(diff.y) < _deadzoneSize.y) _targetPosition.y = currentPos.y;
            else _targetPosition.y -= Mathf.Sign(diff.y) * _deadzoneSize.y;

            // Smooth tracking
            Vector3 nextPos = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, _smoothTime);

            // Clamp to boundaries if enabled
            if (_useBoundaries && _boundaryBox != null)
            {
                Bounds bounds = _boundaryBox.bounds;
                float camHeight = UnityEngine.Camera.main.orthographicSize;
                float camWidth = camHeight * UnityEngine.Camera.main.aspect;

                float minX = bounds.min.x + camWidth;
                float maxX = bounds.max.x - camWidth;
                float minY = bounds.min.y + camHeight;
                float maxY = bounds.max.y - camHeight;

                nextPos.x = Mathf.Clamp(nextPos.x, minX, maxX);
                nextPos.y = Mathf.Clamp(nextPos.y, minY, maxY);
            }

            transform.position = nextPos;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        public void SetBoundaries(BoxCollider2D boundaryBox)
        {
            _boundaryBox = boundaryBox;
            _useBoundaries = true;
        }
    }
}
