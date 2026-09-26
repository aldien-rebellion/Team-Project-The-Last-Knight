using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.Audio;

namespace TheLastKnight.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private GameObject _panel;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(GameLanguage lang)
        {
            if (_panel != null && (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsOpen))
            {
                ShowMain();
            }
        }

        private void Start()
        {
            ShowMain();
        }

        private Transform Replace(string title)
        {
            if (_panel != null) Destroy(_panel);
            _panel = RuntimeUI.Panel(title, out var content);
            return content;
        }

        public void ShowMain()
        {
            Time.timeScale = 1f;
            var content = Replace(LocalizationManager.Get("MAIN_TITLE"));
            RuntimeUI.Label(content, LocalizationManager.Get("MAIN_SUBTITLE"), 22);
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_PLAY"), ShowDifficulty);
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_CONTINUE"), () =>
            {
                if (!GameManager.Instance.ContinueGame()) ShowMain();
            }).interactable = SaveSystem.HasSave;
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_SETTINGS"), OpenSettings);
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_EXIT"), Application.Quit);
            RuntimeUI.Label(content, LocalizationManager.Get("MAIN_CONTROLS_HINT"), 17);
        }

        private void ShowDifficulty()
        {
            var content = Replace(LocalizationManager.Get("DIFF_TITLE"));
            RuntimeUI.Label(content, LocalizationManager.Get("DIFF_SUBTITLE"), 19);
            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_EASY"), () => GameManager.Instance.NewGame(GameDifficulty.Easy));
            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_NORMAL"), () => GameManager.Instance.NewGame(GameDifficulty.Normal));
            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_HARD"), () => GameManager.Instance.NewGame(GameDifficulty.Hard));
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), ShowMain);
        }

        private void OpenSettings()
        {
            if (_panel != null)
            {
                Destroy(_panel);
                _panel = null;
            }

            var pauseMenu = PauseMenuUI.Instance;
            if (pauseMenu == null)
            {
                pauseMenu = FindAnyObjectByType<PauseMenuUI>();
            }
            if (pauseMenu == null)
            {
                var go = new GameObject("PauseMenuUI_Manager");
                pauseMenu = go.AddComponent<PauseMenuUI>();
            }

            pauseMenu.OpenSettingsFromExternal(ShowMain);
        }
    }
}

