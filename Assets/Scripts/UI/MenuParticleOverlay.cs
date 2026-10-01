using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.UI
{
    [DisallowMultipleComponent]
    public sealed class MenuParticleOverlay : MonoBehaviour
    {
        private const int ParticleLayer = 31;
        private const int CanvasOrder = 100;

        private UnityEngine.Camera _overlayCamera;
        private RenderTexture _renderTexture;
        private GameObject _overlayCanvas;

        private void Awake()
        {
            var sourceCamera = UnityEngine.Camera.main;
            var particleSystem = GetComponent<ParticleSystem>();
            if (sourceCamera == null || particleSystem == null)
            {
                Debug.LogWarning("[MenuParticleOverlay] A main camera and Particle System are required.", this);
                return;
            }

            var shape = particleSystem.shape;
            var shapeScale = shape.scale;
            shapeScale.x = 20f;
            shape.scale = shapeScale;

            gameObject.layer = ParticleLayer;
            foreach (Transform child in transform)
                child.gameObject.layer = ParticleLayer;

            _renderTexture = new RenderTexture(
                Mathf.Max(1, Screen.width),
                Mathf.Max(1, Screen.height),
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = "MenuAshParticles",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear
            };
            _renderTexture.Create();

            var cameraObject = new GameObject("MenuAshParticleCamera");
            cameraObject.transform.SetPositionAndRotation(
                sourceCamera.transform.position,
                sourceCamera.transform.rotation);
            _overlayCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            _overlayCamera.CopyFrom(sourceCamera);
            _overlayCamera.transform.SetPositionAndRotation(
                sourceCamera.transform.position,
                sourceCamera.transform.rotation);
            _overlayCamera.enabled = true;
            _overlayCamera.cullingMask = 1 << ParticleLayer;
            _overlayCamera.clearFlags = CameraClearFlags.SolidColor;
            _overlayCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _overlayCamera.targetTexture = _renderTexture;
            _overlayCamera.depth = -1f;
            _overlayCamera.allowHDR = false;
            _overlayCamera.allowMSAA = false;
            _overlayCamera.aspect = sourceCamera.aspect;

            _overlayCanvas = new GameObject(
                "MenuAshParticleOverlay",
                typeof(Canvas),
                typeof(CanvasScaler));
            var canvas = _overlayCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasOrder;

            var scaler = _overlayCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var imageObject = new GameObject("AshParticles", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(_overlayCanvas.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<RawImage>();
            image.texture = _renderTexture;
            image.raycastTarget = false;
        }

        private void LateUpdate()
        {
            var sourceCamera = UnityEngine.Camera.main;
            if (_overlayCamera == null || sourceCamera == null)
                return;

            _overlayCamera.transform.SetPositionAndRotation(
                sourceCamera.transform.position,
                sourceCamera.transform.rotation);
            _overlayCamera.orthographic = sourceCamera.orthographic;
            _overlayCamera.orthographicSize = sourceCamera.orthographicSize;
            _overlayCamera.fieldOfView = sourceCamera.fieldOfView;
            _overlayCamera.aspect = sourceCamera.aspect;
            _overlayCamera.nearClipPlane = sourceCamera.nearClipPlane;
            _overlayCamera.farClipPlane = sourceCamera.farClipPlane;
        }

        private void OnDestroy()
        {
            if (_overlayCanvas != null)
                Destroy(_overlayCanvas);

            if (_overlayCamera != null)
                Destroy(_overlayCamera.gameObject);

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }
    }
}