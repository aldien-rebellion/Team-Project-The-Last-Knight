using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TheLastKnight.Stats;
using TheLastKnight.Input;
using TheLastKnight.Player;
using TheLastKnight.UI;

namespace TheLastKnight.Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public PlayerSaveData State { get; private set; } = new PlayerSaveData();
        public PlayerStats Player { get; private set; }
        public bool InputBlocked { get; private set; }
        public bool ArenaLocked { get; set; }
        private PlayerSaveData _checkpoint;
        private bool _hasRecallPoint;
        public bool CanRecall => _hasRecallPoint && _checkpoint != null && !_restoring &&
            Player != null && !Player.IsDead && !InputBlocked && !ArenaLocked && Time.timeScale > 0f;
        private bool _restoring, _restorePosition, _deathShown;
        private GameObject _deathPanel;
        private Coroutine _deathRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("GameManager").AddComponent<GameManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            gameObject.AddComponent<TheLastKnight.Environment.DemonRuneManager>();
            gameObject.AddComponent<TheLastKnight.Audio.AudioManager>();
            gameObject.AddComponent<StoryDialogueUI>();
            gameObject.AddComponent<CharacterStatusUI>();
            gameObject.AddComponent<PauseMenuUI>();
            gameObject.AddComponent<MinimapUI>();
            gameObject.AddComponent<GameBrightnessManager>();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CancelDeathRoutine();
            if (_deathPanel != null) Destroy(_deathPanel);
            _deathPanel = null;
            Player = null;
            _restoring = true;
            ArenaLocked = false;
            _deathShown = false;
            SetInputBlocked(false);
            StartCoroutine(BindPlayer());
        }

        private IEnumerator BindPlayer()
        {
            yield return null; // Let portal Start methods select the arrival position first.
            Player = FindAnyObjectByType<PlayerStats>();
            if (Player != null)
            {
                if (State.initialized)
                {
                    Player.Restore(State);
                    TheLastKnight.Inventory.InventoryManager.Instance?.LoadFrom(State, Player);
                }
                if (_restorePosition) Player.transform.position = State.position;
                Player.GetComponent<PlayerController>().ResetVelocity();
                if (Player.GetComponent<PlayerRecall>() == null) Player.gameObject.AddComponent<PlayerRecall>();
                if (!State.initialized)
                {
                    TheLastKnight.Inventory.InventoryManager.Instance?.InitializeDefaultInventory(Player);
                    Capture();
                }
                if (_checkpoint == null) { Capture(); _checkpoint = State.Copy(); }
                // The player object can be created after sceneLoaded. Restore its input map only
                // after it is bound, otherwise a previously opened UI may leave movement disabled.
                SetInputBlocked(false);
            }
            GetComponent<TheLastKnight.Environment.DemonRuneManager>()?.RestoreDrops();
            _restorePosition = false;
            _restoring = false;
            if (Player != null && SceneManager.GetActiveScene().name == "CityCenter" && !State.introSeen)
            {
                State.introSeen = true;
                GetComponent<StoryDialogueUI>().Intro();
            }
        }

        private void LateUpdate()
        {
            if (!_restoring && Player != null) Capture();
            if (_deathShown) EnsureDeathPanelInput();
        }

        private void EnsureDeathPanelInput()
        {
            var actions = InputSystem.actions;
            var uiMap = actions != null ? actions.FindActionMap("UI") : null;
            if (uiMap != null && !uiMap.enabled) uiMap.Enable();

            var eventSystem = EventSystem.current;
            if (eventSystem == null || !eventSystem.isActiveAndEnabled) return;
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            module.enabled = true;
            if (actions != null) module.actionsAsset = actions;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Capture()
        {
            if (Player == null) return;
            Player.Capture(State);
            State.scene = SceneManager.GetActiveScene().name;
            State.position = Player.transform.position;
            State.difficulty = GameDifficultyManager.Current;
            TheLastKnight.Inventory.InventoryManager.Instance?.SaveTo(State);
        }

        public void SetInputBlocked(bool blocked)
        {
            InputBlocked = blocked;
            foreach (var input in FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None))
            {
                input.enabled = !blocked;
                if (!blocked) input.EnablePlayerActions();
            }
            foreach (var controller in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                controller.ResetVelocity();
                controller.enabled = !blocked;
            }
        }

        public void NewGame(GameDifficulty difficulty)
        {
            NewGame(difficulty, null);
        }

        public void NewGame(GameDifficulty difficulty, string saveName)
        {
            string worldId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(saveName))
            {
                int count = SaveSystem.GetAllSaves().Count + 1;
                saveName = $"{LocalizationManager.Get("WORLD_DEFAULT_NAME")} {count}";
            }

            State = new PlayerSaveData
            {
                difficulty = difficulty,
                worldId = worldId,
                saveName = saveName.Trim(),
                createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                lastSavedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };

            SaveSystem.ActiveWorldId = worldId;
            GameDifficultyManager.Current = difficulty;
            _checkpoint = null;
            _hasRecallPoint = false;
            ClearPortalArrival();
            Load("CityCenter", false);
        }

        public bool ContinueGame()
        {
            if (!SaveSystem.TryLoad(out var saved)) return false;
            RestoreCheckpoint(saved);
            return true;
        }

        public bool LoadWorld(string worldId)
        {
            if (!SaveSystem.TryLoadWorld(worldId, out var saved)) return false;
            RestoreCheckpoint(saved);
            return true;
        }

        private void RestoreCheckpoint(PlayerSaveData checkpoint)
        {
            State = checkpoint.Copy();
            _checkpoint = checkpoint.Copy();
            _hasRecallPoint = true;
            GameDifficultyManager.Current = State.difficulty;
            _restorePosition = true;
            ClearPortalArrival();
            Load(State.scene, false);
        }

        public bool SaveAt(Vector3 position, out string error)
        {
            Player.Rest();
            Capture();
            State.position = position;
            if (!SaveSystem.Save(State, out error)) return false;
            _checkpoint = State.Copy();
            _hasRecallPoint = true;
            return true;
        }

        // Recall changes only location; death/continue restore the saved snapshot instead.
        public bool RecallToLastSave()
        {
            if (!CanRecall) return false;
            string scene = _checkpoint.scene;
            Vector3 position = _checkpoint.position;
            ClearPortalArrival();
            if (SceneManager.GetActiveScene().name == scene)
            {
                Player.GetComponent<PlayerController>().ResetVelocity();
                Player.transform.position = position;
                Physics2D.SyncTransforms();
                Capture();
            }
            else
            {
                Capture();
                State.scene = scene;
                State.position = position;
                _restorePosition = true;
                Load(scene, false);
            }
            return true;
        }

        public void Travel(string scene)
        {
            if (ArenaLocked || InputBlocked) return;
            Load(scene, true);
        }

        public void Load(string scene, bool capture = true)
        {
            CancelDeathRoutine();
            if (capture) Capture();
            _restoring = true;
            Player = null;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        private static void ClearPortalArrival()
        {
            ScenePortal.lastSceneLoaded = ScenePortal.lastPortalUsed = ScenePortal.targetPortalExpected = "";
        }

        public void PlayerDied()
        {
            if (_deathShown || _deathRoutine != null) return;
            SetInputBlocked(true);
            _deathRoutine = StartCoroutine(ShowDeathPanelAfterAnimation(Player));
        }

        private IEnumerator ShowDeathPanelAfterAnimation(PlayerStats dyingPlayer)
        {
            var animator = dyingPlayer != null ? dyingPlayer.GetComponent<Animator>() : null;
            int deadState = Animator.StringToHash("Base Layer.Dead");
            bool canAnimate = animator != null && animator.isActiveAndEnabled &&
                animator.runtimeAnimatorController != null && animator.HasState(0, deadState);
            if (canAnimate)
            {
                animator.speed = 1f; // Attack speed must not shorten the death animation.
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Bool)
                        animator.SetBool(parameter.nameHash, false);
                    else if (parameter.type == AnimatorControllerParameterType.Trigger)
                        animator.ResetTrigger(parameter.nameHash);
                }
                animator.SetBool("IsDead", true);
                animator.Play(deadState, 0, 0f);
                var sprite = dyingPlayer.GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    var color = sprite.color;
                    color.a = 1f;
                    sprite.color = color;
                }
            }

            // Let the Animator enter Dead before reading its progress.
            yield return null;
            while (canAnimate && dyingPlayer != null && dyingPlayer == Player && dyingPlayer.IsDead &&
                animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == deadState && state.normalizedTime >= 1f) break;
                yield return null;
            }

            // Hold the final death pose for one second before showing the respawn options.
            yield return new WaitForSecondsRealtime(1f);
            _deathRoutine = null;
            if (dyingPlayer == null || dyingPlayer != Player || !dyingPlayer.IsDead || _restoring) yield break;
            ShowDeathPanel();
        }

        private void CancelDeathRoutine()
        {
            if (_deathRoutine != null) StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        private void ShowDeathPanel()
        {
            _deathShown = true;
            _deathPanel = RuntimeUI.Panel("YOU DIED", out var content);
            RuntimeUI.Label(content, "Arthur's journey is not over.", 22);
            RuntimeUI.Button(content, "Respawn", () =>
            {
                CloseDeathPanel();
                if (_checkpoint != null) RestoreCheckpoint(_checkpoint);
                else if (SaveSystem.TryLoad(out var saved)) RestoreCheckpoint(saved);
                else NewGame(GameDifficultyManager.Current, State?.saveName);
            });
            RuntimeUI.Button(content, "Exit to Main Menu", () =>
            {
                CloseDeathPanel();
                Load("MainMenu", false);
            });
            EnsureDeathPanelInput();
        }

        private void CloseDeathPanel()
        {
            if (_deathPanel != null) Destroy(_deathPanel);
            _deathPanel = null;
            _deathShown = false;
            SetInputBlocked(false);
        }
    }
}
