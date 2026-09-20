using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyProgressionReward : MonoBehaviour
    {
        public bool churchKey;
        public int runeIndex = -1;
        public bool finalBoss;
        private void OnEnable() { GetComponent<EnemyStats>().OnDeath += Award; }
        private void OnDisable() { GetComponent<EnemyStats>().OnDeath -= Award; }
        private void Award()
        {
            if (churchKey)
            {
                GameManager.Instance.State.churchKey = true;
                FloatingCombatText.Show(transform.position + Vector3.up, "Church Key acquired", Color.yellow);
            }
            if (runeIndex >= 0) DemonRuneManager.Instance.CollectRune(runeIndex);
            if (finalBoss) GameManager.Instance.State.victory = true;
        }
    }
}
