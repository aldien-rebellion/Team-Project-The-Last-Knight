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
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_PLAY"), ShowNewWorld);
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_CONTINUE"), ShowWorldSelection)
                .interactable = SaveSystem.HasAnySave;
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_SETTINGS"), OpenSettings);
            RuntimeUI.Button(content, LocalizationManager.Get("MAIN_EXIT"), Application.Quit);
            RuntimeUI.Label(content, LocalizationManager.Get("MAIN_CONTROLS_HINT"), 17);
        }

        private void ShowNewWorld()
        {
            var content = Replace(LocalizationManager.Get("WORLD_NEW_TITLE"));
            RuntimeUI.Label(content, LocalizationManager.Get("WORLD_NEW_SUBTITLE"), 18);

            int nextIndex = SaveSystem.GetAllSaves().Count + 1;
            string defaultName = $"{LocalizationManager.Get("WORLD_DEFAULT_NAME")} {nextIndex}";

            RuntimeUI.Label(content, LocalizationManager.Get("WORLD_NAME_LABEL"), 16, new Color(0.9f, 0.85f, 0.7f));
            var input = RuntimeUI.InputField(content, LocalizationManager.Get("WORLD_NAME_PLACEHOLDER"), defaultName);

            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_EASY"), () => StartNewGame(input.text, defaultName, GameDifficulty.Easy));
            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_NORMAL"), () => StartNewGame(input.text, defaultName, GameDifficulty.Normal));
            RuntimeUI.Button(content, LocalizationManager.Get("DIFF_HARD"), () => StartNewGame(input.text, defaultName, GameDifficulty.Hard));
            RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), ShowMain);
        }

        private void StartNewGame(string inputName, string defaultName, GameDifficulty difficulty)
        {
            string finalName = string.IsNullOrWhiteSpace(inputName) ? defaultName : inputName.Trim();
            GameManager.Instance.NewGame(difficulty, finalName);
        }

        private void ShowWorldSelection()
        {
            var content = Replace(LocalizationManager.Get("WORLD_SELECT_TITLE"));
            RuntimeUI.Label(content, LocalizationManager.Get("WORLD_SELECT_SUBTITLE"), 18);

            var saves = SaveSystem.GetAllSaves();
            if (saves.Count == 0)
            {
                RuntimeUI.Label(content, LocalizationManager.Get("WORLD_NO_SAVES"), 20, new Color(0.7f, 0.7f, 0.8f));
            }
            else
            {
                RuntimeUI.ScrollView(content, out var scrollContent, 340);
                foreach (var save in saves)
                {
                    var targetSave = save;
                    RuntimeUI.WorldCard(scrollContent, targetSave,
                        onPlay: () => GameManager.Instance.LoadWorld(targetSave.worldId),
                        onRename: () => ShowRenameWorld(targetSave),
                        onDelete: () => ShowDeleteConfirm(targetSave)
                    );
                }
            }

            RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), ShowMain);
        }

        private void ShowRenameWorld(PlayerSaveData save)
        {
            var content = Replace(LocalizationManager.Get("RENAME_TITLE"));
            RuntimeUI.Label(content, LocalizationManager.Get("RENAME_SUBTITLE"), 18);

            RuntimeUI.Label(content, LocalizationManager.Get("WORLD_NAME_LABEL"), 16, new Color(0.9f, 0.85f, 0.7f));
            var input = RuntimeUI.InputField(content, LocalizationManager.Get("WORLD_NAME_PLACEHOLDER"), save.saveName);

            RuntimeUI.Button(content, LocalizationManager.Get("BTN_SAVE_NAME"), () =>
            {
                string newName = input.text;
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    SaveSystem.RenameWorld(save.worldId, newName, out _);
                }
                ShowWorldSelection();
            });

            RuntimeUI.Button(content, LocalizationManager.Get("BTN_CANCEL"), ShowWorldSelection);
        }

        private void ShowDeleteConfirm(PlayerSaveData save)
        {
            var content = Replace(LocalizationManager.Get("DELETE_CONFIRM_TITLE"));
            string msg = string.Format(LocalizationManager.Get("DELETE_CONFIRM_MSG"), save.saveName);
            RuntimeUI.Label(content, msg, 19, new Color(1f, 0.45f, 0.45f));

            var btnDel = RuntimeUI.Button(content, LocalizationManager.Get("BTN_CONFIRM_DELETE"), () =>
            {
                SaveSystem.DeleteWorld(save.worldId, out _);
                ShowWorldSelection();
            });

            var delColors = btnDel.colors;
            delColors.normalColor = new Color(0.55f, 0.15f, 0.18f, 1f);
            delColors.highlightedColor = new Color(0.75f, 0.20f, 0.25f, 1f);
            btnDel.colors = delColors;

            RuntimeUI.Button(content, LocalizationManager.Get("BTN_CANCEL"), ShowWorldSelection);
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

