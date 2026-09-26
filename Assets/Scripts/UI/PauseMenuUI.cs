using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TheLastKnight.Core;
using TheLastKnight.Audio;

namespace TheLastKnight.UI
{
    [DefaultExecutionOrder(-400)]
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        private GameObject _panel;
        private bool _isOpen;
        private bool _inSettings;

        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("PauseMenuUI_Manager");
                go.AddComponent<PauseMenuUI>();
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

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Close pause menu automatically whenever a scene changes
            if (_isOpen)
            {
                Close(false);
            }
        }

        private void Update()
        {
            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }
#endif
            if (!escPressed)
            {
                try
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                        escPressed = true;
                }
                catch { }
            }

            if (!escPressed) return;

            // 1. If pause menu is already open, handle navigation
            if (_isOpen)
            {
                if (_inSettings)
                {
                    PlayerPrefs.Save();
                    ShowMainPauseMenu();
                }
                else
                {
                    ResumeGame();
                }
                return;
            }

            // 2. Do not open during MainMenu scene
            string currentScene = SceneManager.GetActiveScene().name;
            if (string.Equals(currentScene, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 3. Do not open if player is dead (Death screen handles respawn/quit)
            if (GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.IsDead)
            {
                return;
            }

            // 4. Do not open if other menus/popups are consuming Escape
            if (CharacterStatusUI.Instance != null && CharacterStatusUI.Instance.IsOpen)
            {
                return;
            }

            var tpUI = FindAnyObjectByType<TheLastKnight.Environment.TeleportDoorUI>();
            if (tpUI != null && tpUI.IsOpen)
            {
                return;
            }

            var shopUI = FindAnyObjectByType<ShopUI>();
            if (shopUI != null && shopUI.IsOpen)
            {
                return;
            }

            // 5. Open Pause Menu!
            OpenPauseMenu();
        }

        public void OpenPauseMenu()
        {
            if (_isOpen) return;

            _isOpen = true;
            _inSettings = false;

            if (Application.isPlaying)
            {
                Time.timeScale = 0f;
                GameManager.Instance?.SetInputBlocked(true);
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ShowMainPauseMenu();
        }

        private void ShowMainPauseMenu()
        {
            _inSettings = false;
            DestroyPanel();

            _panel = RuntimeUI.Panel("หยุดเกม", out var content, 600);

            RuntimeUI.Label(content, "เกมถูกหยุดชั่วคราว", 20, new Color(0.85f, 0.88f, 0.95f));
            RuntimeUI.Button(content, "เล่นต่อ", ResumeGame);
            RuntimeUI.Button(content, "ตั้งค่า", ShowSettings);
            RuntimeUI.Button(content, "ออก (กลับไปที่ Main Menu)", ExitToMainMenu);
        }

        private void ShowSettings()
        {
            _inSettings = true;
            DestroyPanel();

            _panel = RuntimeUI.Panel("ตั้งค่า", out var content, 600);

            var audio = AudioManager.Instance;
            float master = audio != null ? audio.Master : PlayerPrefs.GetFloat("MasterVolume", 1f);
            float music = audio != null ? audio.Music : PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float effects = audio != null ? audio.Effects : PlayerPrefs.GetFloat("EffectsVolume", 1f);

            RuntimeUI.Slider(content, "Master", master, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(v, AudioManager.Instance.Music, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MasterVolume", v);
            });
            RuntimeUI.Slider(content, "Music", music, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, v, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MusicVolume", v);
            });
            RuntimeUI.Slider(content, "Sound effects", effects, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, AudioManager.Instance.Music, v);
                else PlayerPrefs.SetFloat("EffectsVolume", v);
            });

            RuntimeUI.Button(content, "ย้อนกลับ", () =>
            {
                PlayerPrefs.Save();
                ShowMainPauseMenu();
            });
        }

        public void ResumeGame()
        {
            Close(true);
        }

        public void ExitToMainMenu()
        {
            _isOpen = false;
            _inSettings = false;

            DestroyPanel();
            PlayerPrefs.Save();

            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
                GameManager.Instance?.SetInputBlocked(false);

                // Preserve game progress if player is alive
                if (GameManager.Instance != null && GameManager.Instance.Player != null && !GameManager.Instance.Player.IsDead)
                {
                    GameManager.Instance.Capture();
                }

                GameManager.Instance?.Load("MainMenu", false);
            }
        }

        private void Close(bool restoreControls)
        {
            _isOpen = false;
            _inSettings = false;

            DestroyPanel();

            if (restoreControls && Application.isPlaying)
            {
                Time.timeScale = 1f;
                GameManager.Instance?.SetInputBlocked(false);
            }
        }

        private void DestroyPanel()
        {
            if (_panel != null)
            {
                if (Application.isPlaying) Destroy(_panel);
                else DestroyImmediate(_panel);
                _panel = null;
            }
        }
    }
}
