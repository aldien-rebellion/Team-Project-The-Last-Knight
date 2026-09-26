using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_2021_2_OR_NEWER
using UnityEngine.Rendering.Universal;
#endif

namespace TheLastKnight.Core
{
    [DefaultExecutionOrder(-500)]
    public class GameBrightnessManager : MonoBehaviour
    {
        public static GameBrightnessManager Instance { get; private set; }
        private const string PrefKey = "TheLastKnight_BrightnessMultiplier";

        private float _brightness = 1.0f;
        public float Brightness => _brightness;

        public Canvas OverlayCanvas => _overlayCanvas;
        public Image DimImage => _dimImage;
        public Image AddImage => _addImage;

        private Canvas _overlayCanvas;
        private Image _dimImage;
        private Image _addImage;
        private Material _additiveMaterial;

        private readonly Dictionary<Light2D, float> _baseLightIntensities = new Dictionary<Light2D, float>();
        private Color _baseAmbient = new Color(0.25f, 0.25f, 0.25f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("GameBrightnessManager");
                go.AddComponent<GameBrightnessManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            _brightness = PlayerPrefs.GetFloat(PrefKey, 1.0f);
            _baseAmbient = RenderSettings.ambientLight;

            EnsureOverlay();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyBrightness();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_additiveMaterial != null)
            {
                if (Application.isPlaying) Destroy(_additiveMaterial);
                else DestroyImmediate(_additiveMaterial);
                _additiveMaterial = null;
            }
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _baseLightIntensities.Clear();
            _baseAmbient = RenderSettings.ambientLight;
            ApplyBrightness();
        }

        public void EnsureOverlay()
        {
            if (_overlayCanvas != null && _dimImage != null && _addImage != null) return;

            Transform existing = transform.Find("ScreenBrightnessOverlay");
            GameObject overlayGo;
            if (existing != null)
            {
                overlayGo = existing.gameObject;
            }
            else
            {
                overlayGo = new GameObject("ScreenBrightnessOverlay");
                overlayGo.transform.SetParent(transform, false);
            }

            _overlayCanvas = overlayGo.GetComponent<Canvas>();
            if (_overlayCanvas == null) _overlayCanvas = overlayGo.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 32767;

            var cg = overlayGo.GetComponent<CanvasGroup>();
            if (cg == null) cg = overlayGo.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            cg.ignoreParentGroups = true;

            // 1. Dim Image (Black overlay with standard alpha for dimming <= 1.0f)
            Transform dimTrans = overlayGo.transform.Find("DimOverlay");
            GameObject dimGo = dimTrans != null ? dimTrans.gameObject : new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(overlayGo.transform, false);
            var dimRt = dimGo.GetComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;

            _dimImage = dimGo.GetComponent<Image>();
            _dimImage.raycastTarget = false;

            // 2. Additive Image (White overlay with additive blending for brightening > 1.0f)
            Transform addTrans = overlayGo.transform.Find("AddOverlay");
            GameObject addGo = addTrans != null ? addTrans.gameObject : new GameObject("AddOverlay", typeof(RectTransform), typeof(Image));
            addGo.transform.SetParent(overlayGo.transform, false);
            var addRt = addGo.GetComponent<RectTransform>();
            addRt.anchorMin = Vector2.zero;
            addRt.anchorMax = Vector2.one;
            addRt.offsetMin = Vector2.zero;
            addRt.offsetMax = Vector2.zero;

            _addImage = addGo.GetComponent<Image>();
            _addImage.raycastTarget = false;

            if (_additiveMaterial == null)
            {
                var shader = Shader.Find("UI/AdditiveBrightness")
                    ?? Shader.Find("Legacy Shaders/Particles/Additive")
                    ?? Shader.Find("Mobile/Particles/Additive");
                if (shader != null)
                {
                    _additiveMaterial = new Material(shader);
                }
            }

            if (_additiveMaterial != null)
            {
                _addImage.material = _additiveMaterial;
            }
        }

        public void SetBrightness(float value)
        {
            _brightness = Mathf.Clamp(value, 0.3f, 2.0f);
            PlayerPrefs.SetFloat(PrefKey, _brightness);
            PlayerPrefs.Save();
            ApplyBrightness();
        }

        public void ApplyBrightness()
        {
            EnsureOverlay();

            // 1. Apply UI / Screen Dimming (brightness < 1.0f)
            if (_dimImage != null)
            {
                if (_brightness < 1.0f)
                {
                    _dimImage.gameObject.SetActive(true);
                    _dimImage.color = new Color(0f, 0f, 0f, 1.0f - _brightness);
                }
                else
                {
                    _dimImage.color = new Color(0f, 0f, 0f, 0f);
                    _dimImage.gameObject.SetActive(false);
                }
            }

            // 2. Apply UI / Screen Brightening (brightness > 1.0f)
            if (_addImage != null)
            {
                if (_brightness > 1.0f)
                {
                    _addImage.gameObject.SetActive(true);
                    float extra = Mathf.Clamp01((_brightness - 1.0f) * 0.35f);
                    _addImage.color = new Color(1f, 1f, 1f, extra);
                }
                else
                {
                    _addImage.color = new Color(1f, 1f, 1f, 0f);
                    _addImage.gameObject.SetActive(false);
                }
            }

            // 3. Apply 2D Lights for scene lighting (scales up when brightness > 1.0f)
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            if (lights != null)
            {
                foreach (var light in lights)
                {
                    if (light == null) continue;

                    if (!_baseLightIntensities.TryGetValue(light, out float baseIntensity))
                    {
                        baseIntensity = light.intensity > 0.001f ? light.intensity : 1.0f;
                        _baseLightIntensities[light] = baseIntensity;
                    }

                    light.intensity = _brightness > 1.0f ? baseIntensity * _brightness : baseIntensity;
                }
            }

            // 4. Apply to ambient lighting
            RenderSettings.ambientLight = _brightness > 1.0f ? _baseAmbient * _brightness : _baseAmbient;
        }
    }
}
