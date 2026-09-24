using UnityEngine;
using TheLastKnight.Combat;
using TheLastKnight.Core;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class BossArena : MonoBehaviour
    {
        public EnemyStats boss;
        public GameObject[] barriers;
        private bool _entered;
        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
            foreach (var barrier in barriers) barrier.SetActive(false);
            if (boss != null) boss.OnDeath += Unlock;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_entered || boss == null || boss.IsDead || other.GetComponentInParent<TheLastKnight.Stats.PlayerStats>() == null) return;
            _entered = true;
            GameManager.Instance.ArenaLocked = true;
            foreach (var barrier in barriers) barrier.SetActive(true);
            TheLastKnight.Audio.AudioManager.Instance?.PlayMusic("Boss");
        }
        private void Unlock()
        {
            GameManager.Instance.ArenaLocked = false;
            foreach (var barrier in barriers) if (barrier != null) barrier.SetActive(false);
            TheLastKnight.Audio.AudioManager.Instance?.PlaySceneMusic(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        private void OnDestroy() { if (boss != null) boss.OnDeath -= Unlock; }
    }
}
