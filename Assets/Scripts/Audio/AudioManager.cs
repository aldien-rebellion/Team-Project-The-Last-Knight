using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastKnight.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        [SerializeField] private AudioCatalog _catalog;
        private AudioSource _bgmA, _bgmB, _sfx;
        private Coroutine _fade;
        private float _blend = 1f;
        public float Master { get; private set; } = 1f;
        public float Music { get; private set; } = 0.7f;
        public float Effects { get; private set; } = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (_catalog == null) _catalog = Resources.Load<AudioCatalog>("AudioCatalog");
            _bgmA = gameObject.AddComponent<AudioSource>(); _bgmB = gameObject.AddComponent<AudioSource>();
            _sfx = gameObject.AddComponent<AudioSource>();
            _bgmA.loop = _bgmB.loop = true;
            _bgmA.playOnAwake = _bgmB.playOnAwake = _sfx.playOnAwake = false;
            SetVolumes(PlayerPrefs.GetFloat("MasterVolume", 1f), PlayerPrefs.GetFloat("MusicVolume", 0.7f), PlayerPrefs.GetFloat("EffectsVolume", 1f));
            SceneManager.sceneLoaded += SceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneChanged;
            if (Instance == this) Instance = null;
        }

        private void SceneChanged(Scene scene, LoadSceneMode mode) => PlaySceneMusic(scene.name);
        public void PlaySceneMusic(string sceneName) => PlayMusic(sceneName == "Church" ? "Church" :
            sceneName == "SuburbToForest" ? "Forest" : sceneName.Contains("Castle") ? "Castle" : "Town");

        public void PlaySfx(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            if (clip != null) _sfx.PlayOneShot(clip);
        }

        public void PlayMusic(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            if (clip == _bgmA.clip) return;
            if (_fade != null) StopCoroutine(_fade);
            var previous = _bgmA; _bgmA = _bgmB; _bgmB = previous;
            _bgmA.clip = clip; _bgmA.volume = 0f;
            if (clip != null) _bgmA.Play();
            _fade = StartCoroutine(CrossFade());
        }

        private IEnumerator CrossFade()
        {
            _blend = 0;
            while (_blend < 1)
            {
                _blend = Mathf.Min(1f, _blend + Time.unscaledDeltaTime);
                ApplyVolumes();
                yield return null;
            }
            _bgmB.Stop();
            _fade = null;
        }

        public void SetVolumes(float master, float music, float effects)
        {
            Master = Mathf.Clamp01(master); Music = Mathf.Clamp01(music); Effects = Mathf.Clamp01(effects);
            PlayerPrefs.SetFloat("MasterVolume", Master); PlayerPrefs.SetFloat("MusicVolume", Music); PlayerPrefs.SetFloat("EffectsVolume", Effects);
            ApplyVolumes();
        }
        private void ApplyVolumes()
        {
            _bgmA.volume = Master * Music * _blend;
            _bgmB.volume = Master * Music * (1f - _blend);
            _sfx.volume = Master * Effects;
        }
    }
}
