using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using TheLastKnight.Core;
using TheLastKnight.Audio;
using TheLastKnight.Input;

namespace TheLastKnight.UI
{
    public enum PauseMenuState
    {
        Closed,
        Main,
        Settings,
        Controls
    }

    [DefaultExecutionOrder(-400)]
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        private GameObject _panel;
        private PauseMenuState _state = PauseMenuState.Closed;
        private string _activeRebindActionName = null;

        public bool IsOpen => _state != PauseMenuState.Closed;
        public PauseMenuState State => _state;

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
            if (IsOpen)
            {
                Close(false);
            }
        }

        private void Update()
        {
            // If actively listening for a key rebind, let the rebind operation consume inputs
            if (!string.IsNullOrEmpty(_activeRebindActionName))
            {
                return;
            }

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

            // 1. Navigation when Pause Menu is open
            if (IsOpen)
            {
                if (escPressed)
                {
                    if (_state == PauseMenuState.Controls)
                    {
                        ShowSettings();
                    }
                    else if (_state == PauseMenuState.Settings)
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

                int num = GetNumberKeyPressed();
                if (_state == PauseMenuState.Main)
                {
                    if (num == 1) ResumeGame();
                    else if (num == 2) ShowSettings();
                    else if (num == 3) ExitToMainMenu();
                }
                else if (_state == PauseMenuState.Settings)
                {
                    if (num == 1 || IsBackKeyPressed())
                    {
                        PlayerPrefs.Save();
                        ShowMainPauseMenu();
                    }
                }
                else if (_state == PauseMenuState.Controls)
                {
                    if (num == 1 || IsBackKeyPressed())
                    {
                        ShowSettings();
                    }
                }
                return;
            }

            if (!escPressed) return;

            // 2. Prevent opening in MainMenu scene
            string currentScene = SceneManager.GetActiveScene().name;
            if (string.Equals(currentScene, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 3. Prevent opening if player is dead
            if (GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.IsDead)
            {
                return;
            }

            // 4. Check other open UIs
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
            if (IsOpen) return;

            _state = PauseMenuState.Main;
            _activeRebindActionName = null;

            if (Application.isPlaying)
            {
                Time.timeScale = 0f;
                GameManager.Instance?.SetInputBlocked(true);
            }

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
            _state = PauseMenuState.Main;
            _activeRebindActionName = null;
            DestroyPanel();

            _panel = RuntimeUI.Panel(LocalizationManager.Get("PAUSE_TITLE"), out var content, 600);

            RuntimeUI.Label(content, LocalizationManager.Get("PAUSE_SUBTITLE"), 20, new Color(0.85f, 0.88f, 0.95f));

            var btnResume = RuntimeUI.Button(content, LocalizationManager.Get("BTN_RESUME"), ResumeGame);
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_SETTINGS"), ShowSettings);
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_MAIN_MENU"), ExitToMainMenu);

            if (EventSystem.current != null && btnResume != null)
            {
                EventSystem.current.SetSelectedGameObject(btnResume.gameObject);
            }
        }

        private void ShowSettings()
        {
            _state = PauseMenuState.Settings;
            _activeRebindActionName = null;
            DestroyPanel();

            _panel = RuntimeUI.Panel(LocalizationManager.Get("SETTINGS_TITLE"), out var content, 600);

            RuntimeUI.Label(content, LocalizationManager.Get("SETTINGS_SUBTITLE"), 18, new Color(0.85f, 0.88f, 0.95f));

            // Audio Sliders
            var audio = AudioManager.Instance;
            float master = audio != null ? audio.Master : PlayerPrefs.GetFloat("MasterVolume", 1f);
            float music = audio != null ? audio.Music : PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            float effects = audio != null ? audio.Effects : PlayerPrefs.GetFloat("EffectsVolume", 1f);

            RuntimeUI.Slider(content, LocalizationManager.Get("AUDIO_MASTER"), master, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(v, AudioManager.Instance.Music, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MasterVolume", v);
            });
            RuntimeUI.Slider(content, LocalizationManager.Get("AUDIO_MUSIC"), music, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, v, AudioManager.Instance.Effects);
                else PlayerPrefs.SetFloat("MusicVolume", v);
            });
            RuntimeUI.Slider(content, LocalizationManager.Get("AUDIO_SFX"), effects, v =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.SetVolumes(AudioManager.Instance.Master, AudioManager.Instance.Music, v);
                else PlayerPrefs.SetFloat("EffectsVolume", v);
            });

            // Brightness Slider
            float brightness = GameBrightnessManager.Instance != null ? GameBrightnessManager.Instance.Brightness : PlayerPrefs.GetFloat("TheLastKnight_BrightnessMultiplier", 1.0f);
            RuntimeUI.Slider(content, LocalizationManager.Get("BRIGHTNESS"), brightness, v =>
            {
                if (GameBrightnessManager.Instance != null)
                {
                    GameBrightnessManager.Instance.SetBrightness(v);
                }
                else
                {
                    PlayerPrefs.SetFloat("TheLastKnight_BrightnessMultiplier", v);
                }
            });

            // Language Switcher Button
            string langButtonText = $"{LocalizationManager.Get("LANGUAGE_LABEL")}: {LocalizationManager.Get("LANGUAGE_CURRENT")}  ({LocalizationManager.Get("LANGUAGE_CHANGE_PROMPT")})";
            RuntimeUI.Button(content, langButtonText, () =>
            {
                LocalizationManager.ToggleLanguage();
                ShowSettings();
            });

            // Controls Rebinding Button
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_CONTROLS"), ShowControlsMenu);

            // Back Button
            var btnBack = RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), () =>
            {
                PlayerPrefs.Save();
                ShowMainPauseMenu();
            });

            if (EventSystem.current != null && btnBack != null)
            {
                EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
            }
        }

        private void ShowControlsMenu()
        {
            _state = PauseMenuState.Controls;
            DestroyPanel();

            _panel = RuntimeUI.Panel(LocalizationManager.Get("CONTROLS_TITLE"), out var content, 600);

            string subtitle = !string.IsNullOrEmpty(_activeRebindActionName)
                ? LocalizationManager.Get("REBIND_WAITING")
                : LocalizationManager.Get("CONTROLS_SUBTITLE");

            RuntimeUI.Label(content, subtitle, 17, new Color(0.9f, 0.85f, 0.55f));

            var actions = KeyRebindManager.GetRebindableActions();
            foreach (var action in actions)
            {
                string actionLabel = LocalizationManager.Get(action.LocalizationKey);
                string keyDisplay = (_activeRebindActionName == action.ActionName + action.BindingIndex)
                    ? LocalizationManager.Get("REBIND_WAITING")
                    : $"[ {KeyRebindManager.GetCurrentBindingDisplay(action.ActionName, action.BindingIndex)} ]";

                var targetAction = action;
                RuntimeUI.ActionRow(content, actionLabel, keyDisplay, () =>
                {
                    StartRebindAction(targetAction);
                });
            }

            // Reset Controls to Default
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_RESET_CONTROLS"), () =>
            {
                KeyRebindManager.ResetAllToDefaults();
                ShowControlsMenu();
            });

            // Back to Settings
            var btnBack = RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), ShowSettings);

            if (EventSystem.current != null && btnBack != null)
            {
                EventSystem.current.SetSelectedGameObject(btnBack.gameObject);
            }
        }

        private void StartRebindAction(RebindableActionInfo action)
        {
            _activeRebindActionName = action.ActionName + action.BindingIndex;
            ShowControlsMenu();

            KeyRebindManager.StartRebind(action,
                onComplete: () =>
                {
                    _activeRebindActionName = null;
                    ShowControlsMenu();
                },
                onCancel: () =>
                {
                    _activeRebindActionName = null;
                    ShowControlsMenu();
                });
        }

        public void ResumeGame()
        {
            Close(true);
        }

        public void ExitToMainMenu()
        {
            _state = PauseMenuState.Closed;
            _activeRebindActionName = null;

            DestroyPanel();
            PlayerPrefs.Save();

            if (Application.isPlaying)
            {
                Time.timeScale = 1f;
                GameManager.Instance?.SetInputBlocked(false);

                if (GameManager.Instance != null && GameManager.Instance.Player != null && !GameManager.Instance.Player.IsDead)
                {
                    GameManager.Instance.Capture();
                }

                GameManager.Instance?.Load("MainMenu", false);
            }
        }

        private void Close(bool restoreControls)
        {
            _state = PauseMenuState.Closed;
            _activeRebindActionName = null;

            KeyRebindManager.CancelOngoingRebind();
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
