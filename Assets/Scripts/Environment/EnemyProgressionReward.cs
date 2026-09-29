using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;
using TheLastKnight.Inventory;

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
                WorldItemPickup.Spawn(ItemRegistry.CreateItem("church_key", 1), transform.position);
                var gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    var defeated = gameManager.State.defeatedAreaBosses;
                    if (defeated == null)
                        gameManager.State.defeatedAreaBosses = defeated = new System.Collections.Generic.List<string>();
                    if (!defeated.Contains("Church")) defeated.Add("Church");
                }
            }
            if (runeIndex >= 0) DemonRuneManager.Instance.DropRune(runeIndex, transform.position);
            if (finalBoss)
            {
                GameManager.Instance.State.victory = true;
                GameManager.Instance.GetComponent<TheLastKnight.UI.StoryDialogueUI>().Ending();
            }
        }
    }
}
