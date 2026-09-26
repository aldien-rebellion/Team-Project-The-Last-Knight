using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

            // 1. If pause menu is already open, handle navigation and shortcuts
            if (_isOpen)
            {
                if (escPressed)
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

                // Keyboard Number Shortcuts
                int num = GetNumberKeyPressed();
                if (!_inSettings)
                {
                    if (num == 1) ResumeGame();
                    else if (num == 2) ShowSettings();
                    else if (num == 3) ExitToMainMenu();
                }
                else
                {
                    if (num == 1 || IsBackKeyPressed())
                    {
                        PlayerPrefs.Save();
                        ShowMainPauseMenu();
                    }
                }
                return;
            }

            if (!escPressed) return;

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

        private int GetNumberKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) return 1;
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) return 2;
                if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) return 3;
            }
#endif
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) return 1;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) return 2;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) return 3;
            }
            catch { }
            return -1;
        }

        private bool IsBackKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace)) return true;
            }
            catch { }
            return false;
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

            // Enable UI Action Map and disable Player Action Map so UI raycasts and mouse clicks work flawlessly
#if ENABLE_INPUT_SYSTEM
            if (InputSystem.actions != null)
            {
                InputSystem.actions.FindActionMap("Player")?.Disable();
                var uiMap = InputSystem.actions.FindActionMap("UI");
                if (uiMap != null && !uiMap.enabled)
                {
                    uiMap.Enable();
                }
            }
#endif

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            ShowMainPauseMenu();
        }

        private void ShowMainPauseMenu()
        {
            _inSettings = false;
            DestroyPanel();

            _panel = RuntimeUI.Panel("PAUSED / หยุดเกม", out var content, 600);

            RuntimeUI.Label(content, "Game is paused • เกมถูกหยุดชั่วคราว", 19, new Color(0.85f, 0.88f, 0.95f));

            var btnResume = RuntimeUI.Button(content, "[1]  Resume • เล่นต่อ", ResumeGame);
            RuntimeUI.Button(content, "[2]  Settings • ตั้งค่า", ShowSettings);
            RuntimeUI.Button(content, "[3]  Exit to Main Menu • กลับสู่เมนูหลัก", ExitToMainMenu);

            // Auto-focus the first button for keyboard/gamepad navigation
            if (EventSystem.current != null && btnResume != null)
            {
                EventSystem.current.SetSelectedGameObject(btnResume.gameObject);
            }
        }

        private void ShowSettings()
        {
            _inSettings = true;
            DestroyPanel();

            _panel = RuntimeUI.Panel("SETTINGS / ตั้งค่า", out var content, 600);

            RuntimeUI.Label(content, "Audio Settings • ปรับระดับเสียง", 19, new Color(0.85f, 0.88f, 0.95f));

            var audio = AudioManager.Instance;
            float master = audio != null ? audio.Master : PlayerPrefs.GetFloat("MasterVolume", 1f);
            float music = audio != null ? audio.Music : PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float effects = audio != null ? audio.Effects : PlayerPrefs.GetFloat("EffectsVolume", 1f);

            RuntimeUI.Slider(content, "Master Volume • เสียงหลัก", master, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(v, AudioManager.Instance.Music, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MasterVolume", v);
            });
            RuntimeUI.Slider(content, "Music Volume • เสียงดนตรี", music, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, v, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MusicVolume", v);
            });
            RuntimeUI.Slider(content, "Sound Effects • เสียงเอฟเฟกต์", effects, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, AudioManager.Instance.Music, v);
                else PlayerPrefs.SetFloat("EffectsVolume", v);
            });

            var btnBack = RuntimeUI.Button(content, "[1]  Back • ย้อนกลับ", () =>
            {
                PlayerPrefs.Save();
                ShowMainPauseMenu();
            });

            if (EventSystem.current != null && btnBack != null)
            {
                EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
            }
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

#if ENABLE_INPUT_SYSTEM
                if (InputSystem.actions != null)
                {
                    InputSystem.actions.FindActionMap("Player")?.Enable();
                }
#endif
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
