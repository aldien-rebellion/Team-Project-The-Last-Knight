using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Combat;
using TheLastKnight.Core;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class BossArena : MonoBehaviour
    {
        public EnemyStats boss;
        [Tooltip("Scene object name used when the boss reference points to a prefab asset instead of a scene instance.")]
        public string bossObjectName;
        public GameObject[] barriers;
        [Tooltip("Optional persistent identifier. Leave empty to use this scene name.")]
        public string defeatId;
        [Tooltip("When true the arena uses only a single forward-facing barrier so the player can retreat.")]
        public bool oneSided;
        private bool _entered;
        private BoxCollider2D _area;

        private void Awake()
        {
            _area = GetComponent<BoxCollider2D>();
            _area.isTrigger = true;
            ResolveSceneBoss();

            // Check saved progression before any trigger can arm this arena.
            // The Red Seals in every map are the entries in this barriers array.
            if (HasBeenDefeated())
            {
                DeactivateClearedArena();
                enabled = false;
                return;
            }

            foreach (var barrier in barriers) if (barrier != null) barrier.SetActive(false);
            if (boss != null)
            {
                boss.GetComponent<TheLastKnight.AI.EnemyController>()?.DisableRespawn();
                HideWorldHealthBars();
                boss.OnDeath += Unlock;
                boss.OnDamaged += ShowBossHealth;
            }
        }

        private void ResolveSceneBoss()
        {
            if (boss != null && boss.gameObject.scene == gameObject.scene) return;

            string targetName = !string.IsNullOrWhiteSpace(bossObjectName)
                ? bossObjectName.Trim()
                : boss != null ? boss.gameObject.name : string.Empty;
            boss = null;
            if (string.IsNullOrEmpty(targetName)) return;

            foreach (var candidate in FindObjectsByType<EnemyStats>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene && candidate.gameObject.name == targetName)
                {
                    boss = candidate;
                    return;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<TheLastKnight.Stats.PlayerStats>() == null) return;
            EnterArena();
        }

        // A player can be restored/spawned inside the trigger. In that case Unity does
        // not guarantee OnTriggerEnter2D, so detect that position as a fallback.
        private void TryEnterArenaFromPlayerPosition()
        {
            if (_entered || _area == null || GameManager.Instance == null || GameManager.Instance.Player == null) return;
            var playerCollider = GameManager.Instance.Player.GetComponentInChildren<Collider2D>();
            if (playerCollider != null && _area.bounds.Intersects(playerCollider.bounds)) EnterArena();
        }

        private void EnterArena()
        {
            if (HasBeenDefeated())
            {
                DeactivateClearedArena();
                enabled = false;
                return;
            }
            if (_entered || boss == null || boss.IsDead) return;
            _entered = true;
            if (!oneSided) GameManager.Instance.ArenaLocked = true;
            foreach (var barrier in barriers) barrier.SetActive(true);
            TheLastKnight.UI.BossHealthBarUI.Show(boss);
            TheLastKnight.Audio.AudioManager.Instance?.PlayMusic("Boss");
        }
        private void Update()
        {
            if (boss == null)
            {
                ResolveSceneBoss();
                if (boss != null && !HasBeenDefeated())
                {
                    boss.GetComponent<TheLastKnight.AI.EnemyController>()?.DisableRespawn();
                    HideWorldHealthBars();
                    boss.OnDeath += Unlock;
                    boss.OnDamaged += ShowBossHealth;
                }
            }
            if (HasBeenDefeated())
            {
                DeactivateClearedArena();
                enabled = false;
                return;
            }
            TryEnterArenaFromPlayerPosition();
            if (_entered && boss != null && !boss.IsDead && !TheLastKnight.UI.BossHealthBarUI.IsShowing)
                TheLastKnight.UI.BossHealthBarUI.Show(boss);
        }

        // This is the final fallback: a registered area boss always reveals its HUD
        // as soon as it takes damage, even if its arena trigger was missed.
        private void ShowBossHealth(DamageData damage)
        {
            if (HasBeenDefeated()) return;
            if (boss != null && !boss.IsDead && !TheLastKnight.UI.BossHealthBarUI.IsShowing)
                TheLastKnight.UI.BossHealthBarUI.Show(boss);
        }

        private void Unlock()
        {
            RecordDefeat();
            GameManager.Instance.ArenaLocked = false;
            foreach (var barrier in barriers) if (barrier != null) barrier.SetActive(false);
            TheLastKnight.UI.BossHealthBarUI.Hide();
            // Only arenas which actually remove a Red Seal announce that a new route opened.
            if (barriers != null && barriers.Length > 0) TheLastKnight.UI.AreaUnlockNoticeUI.Show();
            TheLastKnight.Audio.AudioManager.Instance?.PlaySceneMusic(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private string DefeatId => string.IsNullOrWhiteSpace(defeatId)
            ? SceneManager.GetActiveScene().name
            : defeatId;

        private bool HasBeenDefeated()
        {
            var state = GameManager.Instance != null ? GameManager.Instance.State : null;
            if (state == null) return false;
            if (state.defeatedAreaBosses != null && state.defeatedAreaBosses.Contains(DefeatId)) return true;

            // Older saves predate the defeated-boss list. The Church Key is unique
            // to MoonstoneKeeper, so it safely identifies an already-cleared Church.
            if (DefeatId == "Church")
            {
                if (state.churchKey) return true;
                if (state.inventory != null)
                {
                    foreach (var item in state.inventory)
                        if (item != null && item.count > 0 && item.itemId == "church_key") return true;
                }
            }
            return false;
        }

        private void RecordDefeat()
        {
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.State;
            if (state.defeatedAreaBosses == null) state.defeatedAreaBosses = new System.Collections.Generic.List<string>();
            if (!state.defeatedAreaBosses.Contains(DefeatId)) state.defeatedAreaBosses.Add(DefeatId);

            // Keep the defeat in the current world's runtime state. It is persisted only
            // when the player explicitly saves at a save point.
        }

        private void HideWorldHealthBars()
        {
            foreach (var healthBar in boss.GetComponentsInChildren<FloatingHealthBar>(true))
                healthBar.gameObject.SetActive(false);
        }

        private void DeactivateClearedArena()
        {
            if (boss != null) boss.gameObject.SetActive(false);
            foreach (var barrier in barriers) if (barrier != null) barrier.SetActive(false);
            TheLastKnight.UI.BossHealthBarUI.Hide();
        }

        private void OnDestroy()
        {
            if (boss != null)
            {
                boss.OnDeath -= Unlock;
                boss.OnDamaged -= ShowBossHealth;
            }
            TheLastKnight.UI.BossHealthBarUI.Hide();
        }
    }
}
