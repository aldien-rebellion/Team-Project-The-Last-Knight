using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    // A pending use belongs to this inventory. Animation and legacy completion
    // callers share it, so completion is at most once even with repeated callbacks.
    public sealed class QuickItemConsumption
    {
        private readonly InventoryManager _inventory;
        private PlayerStats _player;
        private InventoryItemData _item;
        private int _slot = -1;
        public bool IsPending => _item != null;
        public string PendingItemId => _item?.id;

        public QuickItemConsumption(InventoryManager inventory) => _inventory = inventory;

        public bool TryBegin(PlayerStats player)
        {
            if (IsPending || _inventory == null || _inventory.CursorHeldItem != null) return false;
            int slot = _inventory.GetActiveQuickSlotIndex();
            if (!_inventory.CanUseQuickSlot(slot, player)) return false;
            _slot = slot;
            _item = _inventory.GetSlot(SlotType.QuickSlot, slot);
            _player = player;
            return true;
        }

        public bool Complete()
        {
            var item = _item;
            var player = _player;
            int slot = _slot;
            Cancel();
            return _inventory != null && player != null && item != null &&
                ReferenceEquals(_inventory.GetSlot(SlotType.QuickSlot, slot), item) &&
                _inventory.UseQuickSlot(slot, player);
        }

        public void Cancel()
        {
            _item = null;
            _player = null;
            _slot = -1;
        }

        public bool CompleteHealingPotion(PlayerStats player)
        {
            if (IsPending) return _player == player && PendingItemId == "potion_heal" && Complete();
            int slot = _inventory != null ? _inventory.GetActiveQuickSlotIndex() : -1;
            if (_inventory == null || _inventory.GetSlot(SlotType.QuickSlot, slot)?.id != "potion_heal") return false;
            return TryBegin(player) && Complete();
        }
    }
}
