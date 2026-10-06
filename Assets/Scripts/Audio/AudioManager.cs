using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastKnight.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        private static readonly Dictionary<AudioSource, float> EffectsSources = new Dictionary<AudioSource, float>();
        private static readonly List<AudioSource> DestroyedSources = new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEffectsSources()
        {
            EffectsSources.Clear();
            DestroyedSources.Clear();
        }

        // Keep the authored source gain separate from the player's settings.
        // Registration can happen before the persistent manager's Awake.
        public static void RegisterEffectsSource(AudioSource source)
        {
            if (source == null || EffectsSources.ContainsKey(source)) return;
            SetEffectsSourceVolume(source, source.volume);
        }

        public static void SetEffectsSourceVolume(AudioSource source, float volume)
        {
            if (source == null) return;
            float baseVolume = Mathf.Clamp01(volume);
            EffectsSources[source] = baseVolume;
            source.volume = baseVolume * EffectsGain;
        }

        private static float EffectsGain => Instance != null
            ? Instance.Master * Instance.Effects
            : Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", 1f))
                * Mathf.Clamp01(PlayerPrefs.GetFloat("EffectsVolume", 1f));

        private static void ApplyEffectsVolumes()
        {
            float gain = EffectsGain;
            foreach (var entry in EffectsSources)
            {
                if (entry.Key == null) DestroyedSources.Add(entry.Key);
                else entry.Key.volume = entry.Value * gain;
            }
            foreach (var source in DestroyedSources) EffectsSources.Remove(source);
            DestroyedSources.Clear();
        }
        [SerializeField] private AudioCatalog _catalog;
        private AudioSource _bgmA, _bgmB, _sfx, _loopingSfx, _stoppableSfx;
        private string _loopingSfxId;
        private string _stoppableSfxId;
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
            _loopingSfx = gameObject.AddComponent<AudioSource>();
            _stoppableSfx = gameObject.AddComponent<AudioSource>();
            _bgmA.loop = _bgmB.loop = true;
            _loopingSfx.loop = true;
            _bgmA.playOnAwake = _bgmB.playOnAwake = _sfx.playOnAwake = _loopingSfx.playOnAwake = _stoppableSfx.playOnAwake = false;
            SetVolumes(PlayerPrefs.GetFloat("MasterVolume", 1f), PlayerPrefs.GetFloat("MusicVolume", 0.7f), PlayerPrefs.GetFloat("EffectsVolume", 1f));
            SceneManager.sceneLoaded += SceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneChanged;
            if (Instance == this) Instance = null;
        }

        private void SceneChanged(Scene scene, LoadSceneMode mode)
        {
            ApplyEffectsVolumes();
            PlaySceneMusic(scene.name);
        }
        public void PlaySceneMusic(string sceneName)
        {
            string musicId = sceneName switch
            {
                "MainMenu" => "MainMenu",
                "CityCenter" => "Town",
                "OutdoorMarket" => "Market",
                "SuburbToForest" => "Forest",
                "Church" => "Church",
                "DemonCastle" or "DemonCastleEntrance" => "Castle",
                _ => null
            };
            if (musicId != null) PlayMusic(musicId);
        }

        public void PlaySfx(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            if (clip != null) _sfx.PlayOneShot(clip, id == "click" ? 0.5f : 1f);
        }

        public void PlaySfxLoop(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            if (clip == null || _loopingSfx == null) return;
            if (_loopingSfxId == id && _loopingSfx.isPlaying) return;

            _loopingSfxId = id;
            _loopingSfx.clip = clip;
            _loopingSfx.Play();
        }

        public void StopSfxLoop(string id)
        {
            if (_loopingSfx == null || _loopingSfxId != id) return;
            _loopingSfx.Stop();
            _loopingSfx.clip = null;
            _loopingSfxId = null;
        }

        public void PlaySfxUntilStopped(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            if (clip == null || _stoppableSfx == null) return;

            _stoppableSfx.Stop();
            _stoppableSfxId = id;
            _stoppableSfx.loop = false;
            _stoppableSfx.clip = clip;
            _stoppableSfx.Play();
        }

        public void StopSfxUntilStopped(string id)
        {
            if (_stoppableSfx == null || _stoppableSfxId != id) return;
            _stoppableSfx.Stop();
            _stoppableSfx.clip = null;
            _stoppableSfxId = null;
        }

        public void PlayMusic(string id)
        {
            var clip = _catalog != null ? _catalog.Find(id) : null;
            // An unset track (such as the not-yet-authored boss music) must not
            // fade out the current map's music.
            if (clip == null) return;
            if (clip == _bgmA.clip) return;
            if (_fade != null) StopCoroutine(_fade);
            var previous = _bgmA; _bgmA = _bgmB; _bgmB = previous;
            _bgmA.clip = clip; _bgmA.volume = 0f;
            if (clip != null) _bgmA.Play();
            _fade = StartCoroutine(CrossFade());
        }

        public void StopMusic()
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            _bgmA.Stop();
            _bgmB.Stop();
            _bgmA.clip = null;
            _bgmB.clip = null;
            _blend = 1f;
            ApplyVolumes();
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
            ApplyEffectsVolumes();
        }
        private void ApplyVolumes()
        {
            _bgmA.volume = Master * Music * _blend;
            _bgmB.volume = Master * Music * (1f - _blend);
            _sfx.volume = Master * Effects;
            if (_loopingSfx != null) _loopingSfx.volume = Master * Effects;
            if (_stoppableSfx != null) _stoppableSfx.volume = Master * Effects;
        }
    }
}
