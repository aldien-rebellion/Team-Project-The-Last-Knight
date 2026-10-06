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
        public bool moonstoneShard;
        public int runeIndex = -1;
        public bool finalBoss;
        private void OnEnable() { GetComponent<EnemyStats>().OnDeath += Award; }
        private void OnDisable() { GetComponent<EnemyStats>().OnDeath -= Award; }
        private void Award()
        {
            Vector3 dropPosition = WorldItemPickup.GetDropPosition(transform);
            if (churchKey)
            {
                WorldItemPickup.Spawn(ItemRegistry.CreateItem("church_key", 1), dropPosition);
                var gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    var defeated = gameManager.State.defeatedAreaBosses;
                    if (defeated == null)
                        gameManager.State.defeatedAreaBosses = defeated = new System.Collections.Generic.List<string>();
                    if (!defeated.Contains("Church")) defeated.Add("Church");
                }
            }
            if (moonstoneShard || (name != null && name.IndexOf("MoonstoneKeeper", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                WorldItemPickup.Spawn(ItemRegistry.CreateItem("moonstone_shard", 1), dropPosition);
            }
            if (runeIndex >= 0) DemonRuneManager.Instance.DropRune(runeIndex, dropPosition);
            if (finalBoss)
            {
                GameManager.Instance.State.victory = true;
                GameManager.Instance.GetComponent<TheLastKnight.UI.StoryDialogueUI>().Ending();
            }
        }
    }
}
