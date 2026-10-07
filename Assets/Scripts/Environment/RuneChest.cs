using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;
using TheLastKnight.Inventory;

namespace TheLastKnight.Environment
{
    public class RuneChest : WorldInteractable
    {
        private void Awake() { prompt = "F  Open Pentagram chest"; }
        public override void Interact()
        {
            var runes = DemonRuneManager.Instance;
            if (runes == null || GameManager.Instance == null) return;
            var state = GameManager.Instance.State;
            if (state.pentagramRuneChestOpened || runes.IsSocketed(0))
            {
                ShowOpened();
                return;
            }
            var inventory = InventoryManager.Instance;
            if (inventory == null || !inventory.TryConsumeItem("church_key"))
            {
                FloatingCombatText.Show(transform.position, "Requires the Moonstone Keeper's key", Color.yellow);
                return;
            }
            state.pentagramRuneChestOpened = true;
            if (!runes.HasReward(0)) runes.DropRune(0, transform.position);
            ShowOpened();
        }

        private void ShowOpened()
        {
            interactionRange = -1f;
            prompt = "Pentagram collected";
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.color = new Color(0.65f, 0.65f, 0.65f);
        }
    }
}
