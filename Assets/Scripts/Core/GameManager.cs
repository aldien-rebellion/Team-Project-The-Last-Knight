using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private bool _restoring, _restorePosition, _deathShown;
        private GameObject _deathPanel;

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
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
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
                if (State.initialized) Player.Restore(State);
                if (_restorePosition) Player.transform.position = State.position;
                Player.GetComponent<PlayerController>().ResetVelocity();
                if (!State.initialized) Capture();
                if (_checkpoint == null) { Capture(); _checkpoint = State.Copy(); }
            }
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
        }

        public void Capture()
        {
            if (Player == null) return;
            Player.Capture(State);
            State.scene = SceneManager.GetActiveScene().name;
            State.position = Player.transform.position;
            State.difficulty = GameDifficultyManager.Current;
        }

        public void SetInputBlocked(bool blocked)
        {
            InputBlocked = blocked;
            foreach (var input in FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None)) input.enabled = !blocked;
            foreach (var controller in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                controller.ResetVelocity();
                controller.enabled = !blocked;
            }
        }

        public void NewGame(GameDifficulty difficulty)
        {
            State = new PlayerSaveData { difficulty = difficulty };
            GameDifficultyManager.Current = difficulty;
            _checkpoint = null;
            ClearPortalArrival();
            Load("CityCenter", false);
        }

        public bool ContinueGame()
        {
            if (!SaveSystem.TryLoad(out var saved)) return false;
            RestoreCheckpoint(saved);
            return true;
        }

        private void RestoreCheckpoint(PlayerSaveData checkpoint)
        {
            State = checkpoint.Copy();
            _checkpoint = checkpoint.Copy();
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
            return true;
        }

        public void Travel(string scene)
        {
            if (ArenaLocked || InputBlocked) return;
            Load(scene, true);
        }

        public void Load(string scene, bool capture = true)
        {
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
            if (_deathShown) return;
            _deathShown = true;
            SetInputBlocked(true);
            _deathPanel = RuntimeUI.Panel("YOU DIED", out var content);
            RuntimeUI.Label(content, "Arthur's journey is not over.", 22);
            RuntimeUI.Button(content, "Respawn", () =>
            {
                if (_checkpoint != null) RestoreCheckpoint(_checkpoint);
                else NewGame(GameDifficultyManager.Current);
            });
            RuntimeUI.Button(content, "Exit to Main Menu", () => Load("MainMenu", false));
        }
    }
}
