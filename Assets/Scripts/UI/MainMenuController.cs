using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.Audio;

namespace TheLastKnight.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Background sprite for the main menu. Assign 'main menu background' sprite here.")]
        [SerializeField] private Sprite _backgroundSprite;

        private GameObject _panel;
        private GameObject _backgroundCanvas;
        private GameObject _difficultyDetails;

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

        private void OnDestroy()
        {
            if (_panel != null)
            {
                if (Application.isPlaying) Destroy(_panel);
                else DestroyImmediate(_panel);
                _panel = null;
            }
            if (_backgroundCanvas != null)
            {
                if (Application.isPlaying) Destroy(_backgroundCanvas);
                else DestroyImmediate(_backgroundCanvas);
                _backgroundCanvas = null;
            }
        }

        private void Start()
        {
            CreateBackground();
            ShowMain();
        }

        private void CreateBackground()
        {
            if (_backgroundCanvas != null) return;

            // Try to find the sprite if not assigned via Inspector
            Sprite bgSprite = _backgroundSprite;
            if (bgSprite == null)
            {
                // Load from known asset path via Resources or direct sprite load
                bgSprite = Resources.Load<Sprite>("main menu background");
            }

            if (bgSprite == null)
            {
                Debug.LogWarning("[MainMenuController] Background sprite not assigned and not found in Resources. Skipping background.");
                return;
            }

            // Create a separate canvas below the UI panels
            _backgroundCanvas = new GameObject("MainMenuBackground", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _backgroundCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0; // Below UI panels (sortingOrder 200)

            var scaler = _backgroundCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Create the background image
            var bgGo = new GameObject("BackgroundImage", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(_backgroundCanvas.transform, false);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = bgGo.GetComponent<Image>();
            img.sprite = bgSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        private Transform Replace(string title)
        {
            CloseDifficultyDetails();
            if (_panel != null)
            {
                if (Application.isPlaying) Destroy(_panel);
                else DestroyImmediate(_panel);
                _panel = null;
            }
            _panel = RuntimeUI.Panel(title, out var content);

            // Make the backdrop semi-transparent so the background image shows through
            if (_backgroundCanvas != null)
            {
                var backdrop = _panel.transform.Find("Backdrop");
                if (backdrop != null)
                {
                    var backdropImg = backdrop.GetComponent<Image>();
                    if (backdropImg != null)
                    {
                        backdropImg.color = new Color(0.02f, 0.03f, 0.06f, 0.55f);
                    }
                }
            }

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
            var infoButton = RuntimeUI.Button(_panel.transform, "i", ShowDifficultyDetails);
            infoButton.name = "DifficultyInfoButton";
            var infoRect = infoButton.GetComponent<RectTransform>();
            infoRect.anchorMin = infoRect.anchorMax = Vector2.one;
            infoRect.pivot = Vector2.one;
            infoRect.anchoredPosition = new Vector2(-24f, -20f);
            infoRect.sizeDelta = new Vector2(44f, 44f);
            var infoLabel = infoButton.GetComponentInChildren<Text>();
            infoLabel.fontSize = 28;
            infoLabel.fontStyle = FontStyle.Bold;
            infoLabel.color = new Color(0.9f, 0.77f, 0.48f);
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

        private void Update()
        {
            if (_difficultyDetails != null && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                CloseDifficultyDetails();
        }

        private void CloseDifficultyDetails()
        {
            if (_difficultyDetails == null) return;
            _difficultyDetails.SetActive(false);
            if (Application.isPlaying) Destroy(_difficultyDetails);
            else DestroyImmediate(_difficultyDetails);
            _difficultyDetails = null;
        }

        private void ShowDifficultyDetails()
        {
            if (_difficultyDetails != null || _panel == null) return;
            _difficultyDetails = new GameObject("DifficultyComparison", typeof(RectTransform), typeof(Image));
            _difficultyDetails.transform.SetParent(_panel.transform, false);
            var overlayRect = _difficultyDetails.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
            _difficultyDetails.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.04f, 0.93f);

            var dialog = new GameObject("ComparisonDialog", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            dialog.transform.SetParent(_difficultyDetails.transform, false);
            var dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.06f, 0.04f);
            dialogRect.anchorMax = new Vector2(0.94f, 0.96f);
            dialogRect.offsetMin = dialogRect.offsetMax = Vector2.zero;
            dialog.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.15f);
            var layout = dialog.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RuntimeUI.Label(dialog.transform, LocalizationManager.Get("DIFF_COMPARE_TITLE"), 28, new Color(0.9f, 0.77f, 0.48f));
            RuntimeUI.Label(dialog.transform, LocalizationManager.Get("DIFF_COMPARE_SUBTITLE"), 16, new Color(0.75f, 0.8f, 0.88f));
            var table = new GameObject("ComparisonTable", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            table.transform.SetParent(dialog.transform, false);
            table.GetComponent<LayoutElement>().preferredHeight = 334f;
            table.GetComponent<LayoutElement>().minHeight = 334f;
            var tableLayout = table.GetComponent<VerticalLayoutGroup>();
            tableLayout.spacing = 2f;
            tableLayout.childControlWidth = tableLayout.childControlHeight = true;
            tableLayout.childForceExpandWidth = true;
            tableLayout.childForceExpandHeight = false;
            AddDifficultyRow(table.transform, "DIFF_FEATURE", new[] {
                LocalizationManager.Get("DIFF_NAME_EASY"), LocalizationManager.Get("DIFF_NAME_NORMAL"), LocalizationManager.Get("DIFF_NAME_HARD") }, true);
            AddDifficultyValues(table.transform, "DIFF_ENEMY_DAMAGE", d => Percentage(GameDifficultyManager.GetEnemyDamage(d)));
            AddDifficultyValues(table.transform, "DIFF_PLAYER_DAMAGE", d => Percentage(GameDifficultyManager.GetPlayerDamage(d)));
            AddDifficultyValues(table.transform, "DIFF_STAMINA_COST", d => Percentage(GameDifficultyManager.GetStaminaConsumption(d)));
            AddDifficultyValues(table.transform, "DIFF_RECOVERY", d => Percentage(GameDifficultyManager.GetRegeneration(d)));
            AddDifficultyValues(table.transform, "DIFF_ENEMY_HEALTH_BAR", d => Visibility(GameDifficultyManager.ShouldShowHelpers(d)));
            AddDifficultyValues(table.transform, "DIFF_ATTACK_WARNING", d => Visibility(GameDifficultyManager.ShouldShowHelpers(d)));
            AddDifficultyValues(table.transform, "DIFF_ENEMY_LEVEL", d => Visibility(GameDifficultyManager.ShouldShowEnemyLevel(d)));
            var note = RuntimeUI.Label(dialog.transform, LocalizationManager.Get("DIFF_COMPARE_BASELINE"), 16, new Color(0.75f, 0.8f, 0.88f));
            note.GetComponent<LayoutElement>().preferredHeight = 44f;
            RuntimeUI.Button(dialog.transform, LocalizationManager.Get("DIFF_CLOSE"), CloseDifficultyDetails);
        }

        private static string Percentage(float multiplier) => (multiplier * 100f).ToString("0.#") + "%";
        private static string Visibility(bool visible) => LocalizationManager.Get(visible ? "DIFF_SHOWN" : "DIFF_HIDDEN");

        private static void AddDifficultyValues(Transform parent, string key, System.Func<GameDifficulty, string> value)
        {
            AddDifficultyRow(parent, key, new[] { value(GameDifficulty.Easy), value(GameDifficulty.Normal), value(GameDifficulty.Hard) });
        }

        private static void AddDifficultyRow(Transform parent, string key, string[] values, bool header = false)
        {
            var row = new GameObject(key, typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 40f;
            row.GetComponent<LayoutElement>().minHeight = 36f;
            row.GetComponent<Image>().color = header ? new Color(0.16f, 0.22f, 0.32f) : new Color(0.1f, 0.14f, 0.21f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            for (int i = 0; i < 4; i++)
            {
                var cell = RuntimeUI.Label(row.transform, i == 0 ? LocalizationManager.Get(key) : values[i - 1], header ? 20 : 18,
                    header ? new Color(0.9f, 0.77f, 0.48f) : Color.white);
                cell.name = "Column" + i;
                cell.alignment = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                var element = cell.GetComponent<LayoutElement>();
                element.minWidth = element.preferredWidth = 0f;
                element.flexibleWidth = i == 0 ? 2f : 1f;
            }
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

