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

        [Header("Framing")]
        [Tooltip("When enabled, player is framed relative to the viewport height (e.g. 10% above bottom).")]
        [SerializeField] private bool _useFeetFraming = true;
        [Tooltip("Target viewport Y position for player's feet (0.1 = 10% above bottom edge).")]
        [Range(0f, 1f)]
        [SerializeField] private float _targetViewportY = 0.10f;

        [Header("Zoom")]
        [SerializeField] private bool _enableZoom = true;
        [SerializeField] private float _maxZoomMultiplier = 4.0f;
        [SerializeField] private float _zoomStep = 1.0f;
        [SerializeField] private float _zoomSmoothTime = 0.15f;

        [Header("Deadzone")]
        [SerializeField] private Vector2 _deadzoneSize = new Vector2(1f, 0f);

        [Header("Boundaries")]
        [SerializeField] private bool _useBoundaries = false;
        [SerializeField] private BoxCollider2D _boundaryBox;

        private Vector3 _currentVelocity;
        private UnityEngine.Camera _cam;
        private float _baseOrthographicSize;
        private float _targetOrthographicSize;
        private float _zoomVelocity;

        public float TargetViewportY
        {
            get => _targetViewportY;
            set => _targetViewportY = Mathf.Clamp01(value);
        }

        public bool UseFeetFraming
        {
            get => _useFeetFraming;
            set => _useFeetFraming = value;
        }

        public bool EnableZoom
        {
            get => _enableZoom;
            set => _enableZoom = value;
        }

        public float BaseOrthographicSize => _baseOrthographicSize;
        public float TargetOrthographicSize => _targetOrthographicSize;
        public float CurrentZoomMultiplier => (_baseOrthographicSize > 0.01f && _cam != null) ? _cam.orthographicSize / _baseOrthographicSize : 1f;
        public bool UseBoundaries => _useBoundaries;
        public BoxCollider2D BoundaryBox => _boundaryBox;

        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            InitializeZoom();
            CheckDemonCastleExclusion();
        }

        private void Start()
        {
            InitializeZoom();
            CheckDemonCastleExclusion();

            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    _target = player.transform;
                }
            }
        }

        private void InitializeZoom()
        {
            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
            if (_cam != null && _baseOrthographicSize <= 0.01f)
            {
                _baseOrthographicSize = _cam.orthographicSize;
                _targetOrthographicSize = _baseOrthographicSize;
            }
        }

        private void CheckDemonCastleExclusion()
        {
            string sceneName = gameObject.scene.name;
            // Exclude DemonCastle map specifically from global camera boundaries
            if (string.Equals(sceneName, "DemonCastle", System.StringComparison.OrdinalIgnoreCase))
            {
                _useBoundaries = false;
                _boundaryBox = null;
            }
            else if (_useBoundaries && _boundaryBox == null)
            {
                var confiner = GameObject.Find("CameraConfiner");
                if (confiner != null)
                {
                    _boundaryBox = confiner.GetComponent<BoxCollider2D>();
                }
            }
        }

        private void HandleZoomInput()
        {
            if (!_enableZoom || _cam == null) return;

            bool isCtrlHeld = false;
            float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                isCtrlHeld = UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.isPressed ||
                             UnityEngine.InputSystem.Keyboard.current.rightCtrlKey.isPressed;
            }
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
            }
#endif

            if (!isCtrlHeld)
            {
                try
                {
                    isCtrlHeld = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
                }
                catch { }
            }
            if (Mathf.Approximately(scroll, 0f))
            {
                try
                {
                    scroll = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
                }
                catch { }
            }

            if (isCtrlHeld && Mathf.Abs(scroll) > 0.001f)
            {
                if (_baseOrthographicSize <= 0.01f)
                {
                    _baseOrthographicSize = _cam.orthographicSize;
                    _targetOrthographicSize = _baseOrthographicSize;
                }

                float minSize = _baseOrthographicSize;
                float maxSize = _baseOrthographicSize * _maxZoomMultiplier;
                float step = _zoomStep > 0f ? _zoomStep : (_baseOrthographicSize * 0.25f);

                // scroll < 0 is scroll backward/down -> zoom out (increase orthographic size)
                // scroll > 0 is scroll forward/up -> zoom in (decrease orthographic size)
                if (scroll < 0f)
                {
                    _targetOrthographicSize = Mathf.Min(_targetOrthographicSize + step, maxSize);
                }
                else
                {
                    _targetOrthographicSize = Mathf.Max(_targetOrthographicSize - step, minSize);
                }
            }

            if (!Mathf.Approximately(_cam.orthographicSize, _targetOrthographicSize))
            {
                if (Application.isPlaying)
                {
                    _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _targetOrthographicSize, ref _zoomVelocity, _zoomSmoothTime);
                }
                else
                {
                    _cam.orthographicSize = _targetOrthographicSize;
                }
            }
        }

        private Vector3 GetTargetFeetPosition()
        {
            if (_target == null) return transform.position;

            Collider2D col = _target.GetComponent<Collider2D>();
            if (col == null || col.isTrigger)
            {
                var cols = _target.GetComponentsInChildren<Collider2D>();
                for (int i = 0; i < cols.Length; i++)
                {
                    if (!cols[i].isTrigger)
                    {
                        col = cols[i];
                        break;
                    }
                }
            }

            if (col != null && !col.isTrigger)
            {
                return new Vector3(_target.position.x, col.bounds.min.y, _target.position.z);
            }

            return _target.position;
        }

        private void LateUpdate()
        {
            HandleZoomInput();

            if (_target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _target = player.transform;
                else return;
            }

            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
            float camHeight = _cam != null ? _cam.orthographicSize : 5f;

            float targetCamY;
            if (_useFeetFraming)
            {
                Vector3 feetPos = GetTargetFeetPosition();
                // Target feet position at _targetViewportY (0.1 = 10% from bottom edge of camera viewport)
                targetCamY = feetPos.y + (0.5f - _targetViewportY) * (2f * camHeight) + _offset.y;
            }
            else
            {
                targetCamY = _target.position.y + _offset.y;
            }

            Vector3 targetWorldPos = new Vector3(_target.position.x + _offset.x, targetCamY, _target.position.z + _offset.z);
            Vector3 desiredPos = transform.position;
            Vector2 diff = new Vector2(targetWorldPos.x - desiredPos.x, targetWorldPos.y - desiredPos.y);

            // Horizontal deadzone tracking
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

            // Vertical deadzone tracking
            if (_deadzoneSize.y > 0f && !_useFeetFraming)
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

            Vector3 nextPos = Application.isPlaying
                ? Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, _smoothTime)
                : desiredPos;

            // Clamp to boundaries if enabled (except demoncastle)
            if (_useBoundaries && _boundaryBox != null && !string.Equals(gameObject.scene.name, "DemonCastle", System.StringComparison.OrdinalIgnoreCase))
            {
                Bounds bounds = _boundaryBox.bounds;
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
            if (string.Equals(gameObject.scene.name, "DemonCastle", System.StringComparison.OrdinalIgnoreCase))
            {
                _useBoundaries = false;
                _boundaryBox = null;
                return;
            }

            _boundaryBox = boundaryBox;
            _useBoundaries = (boundaryBox != null);
        }

        public void SetZoomMultiplier(float multiplier)
        {
            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
            if (_baseOrthographicSize <= 0.01f && _cam != null) _baseOrthographicSize = _cam.orthographicSize;
            _targetOrthographicSize = Mathf.Clamp(_baseOrthographicSize * multiplier, _baseOrthographicSize, _baseOrthographicSize * _maxZoomMultiplier);
        }

        public void ResetZoom()
        {
            _targetOrthographicSize = _baseOrthographicSize;
        }

        public void SnapTo(Vector3 worldPos)
        {
            if (_cam == null) _cam = GetComponent<UnityEngine.Camera>();
            float camHeight = _cam != null ? _cam.orthographicSize : 5f;

            float targetCamY;
            if (_useFeetFraming)
            {
                targetCamY = worldPos.y + (0.5f - _targetViewportY) * (2f * camHeight) + _offset.y;
            }
            else
            {
                targetCamY = worldPos.y + _offset.y;
            }

            Vector3 targetWorldPos = new Vector3(worldPos.x + _offset.x, targetCamY, transform.position.z);

            if (_useBoundaries && _boundaryBox != null && !string.Equals(gameObject.scene.name, "DemonCastle", System.StringComparison.OrdinalIgnoreCase))
            {
                Bounds bounds = _boundaryBox.bounds;
                float camWidth = camHeight * (_cam != null ? _cam.aspect : (16f / 9f));

                float minX = bounds.min.x + camWidth;
                float maxX = bounds.max.x - camWidth;
                float minY = bounds.min.y + camHeight;
                float maxY = bounds.max.y - camHeight;

                if (minX > maxX) targetWorldPos.x = bounds.center.x;
                else targetWorldPos.x = Mathf.Clamp(targetWorldPos.x, minX, maxX);

                if (minY > maxY) targetWorldPos.y = bounds.center.y;
                else targetWorldPos.y = Mathf.Clamp(targetWorldPos.y, minY, maxY);
            }

            transform.position = targetWorldPos;
            _currentVelocity = Vector3.zero;
        }
    }
}
