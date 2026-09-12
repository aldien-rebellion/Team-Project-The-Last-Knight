using UnityEngine;

namespace TheLastKnight.Camera
{
    [ExecuteAlways]
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
        private UnityEngine.Camera _cam;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Base target position with offset
            Vector3 targetWorldPos = _target.position + _offset;

            // Desired camera position initialized to current camera position
            Vector3 desiredPos = transform.position;

            // Difference between target position and current camera position
            Vector2 diff = new Vector2(targetWorldPos.x - desiredPos.x, targetWorldPos.y - desiredPos.y);

            // Smooth deadzone tracking (continuous target calculation without high-frequency chatter)
            if (_deadzoneSize.x > 0f)
            {
                if (Mathf.Abs(diff.x) > _deadzoneSize.x)
                {
                    desiredPos.x = targetWorldPos.x - Mathf.Sign(diff.x) * _deadzoneSize.x;
                }
            }
            else
            {
                desiredPos.x = targetWorldPos.x;
            }

            if (_deadzoneSize.y > 0f)
            {
                if (Mathf.Abs(diff.y) > _deadzoneSize.y)
                {
                    desiredPos.y = targetWorldPos.y - Mathf.Sign(diff.y) * _deadzoneSize.y;
                }
            }
            else
            {
                desiredPos.y = targetWorldPos.y;
            }

            desiredPos.z = targetWorldPos.z;

            // Smooth tracking
            Vector3 nextPos = Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, _smoothTime);

            // Clamp to boundaries if enabled
            if (_useBoundaries && _boundaryBox != null)
            {
                Bounds bounds = _boundaryBox.bounds;
                if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
                float camHeight = _cam != null ? _cam.orthographicSize : 5f;
                float camWidth = camHeight * (_cam != null ? _cam.aspect : (16f / 9f));

                float minX = bounds.min.x + camWidth;
                float maxX = bounds.max.x - camWidth;
                float minY = bounds.min.y + camHeight;
                float maxY = bounds.max.y - camHeight;

                if (minX > maxX)
                {
                    nextPos.x = bounds.center.x;
                }
                else
                {
                    nextPos.x = Mathf.Clamp(nextPos.x, minX, maxX);
                }

                if (minY > maxY)
                {
                    nextPos.y = bounds.center.y;
                }
                else
                {
                    nextPos.y = Mathf.Clamp(nextPos.y, minY, maxY);
                }
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
