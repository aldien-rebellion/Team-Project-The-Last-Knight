using System.Collections.Generic;
using UnityEngine;
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

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyBrightness();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _baseLightIntensities.Clear();
            _baseAmbient = RenderSettings.ambientLight;
            ApplyBrightness();
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
            // 1. Apply to 2D Lights (Global Light 2D, Spot Light 2D, etc.)
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

                    light.intensity = baseIntensity * _brightness;
                }
            }

            // 2. Apply to ambient lighting
            RenderSettings.ambientLight = _baseAmbient * _brightness;
        }
    }
}
