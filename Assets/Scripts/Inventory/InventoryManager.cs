using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Stats;
using TheLastKnight.Core;

namespace TheLastKnight.Inventory
{
    public enum SlotType { Inventory, QuickSlot }

    public class InventoryManager : MonoBehaviour
    {
        private static InventoryManager _instance;
        public static InventoryManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindAnyObjectByType<InventoryManager>();
                if (_instance == null && Application.isPlaying)
                    _instance = new GameObject("InventoryManager").AddComponent<InventoryManager>();
                return _instance;
            }
        }

        public const int InventorySlotCount = 24;
        public const int QuickSlotCount = 5;
        private readonly InventoryItemData[] _inventorySlots = new InventoryItemData[InventorySlotCount];
        private readonly InventoryItemData[] _quickSlots = new InventoryItemData[QuickSlotCount];
        public InventoryItemData CursorHeldItem { get; private set; }
        public event Action OnInventoryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize() { _ = Instance; }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            if (transform.parent == null && Application.isPlaying) DontDestroyOnLoad(gameObject);
            InitializeDefaultInventory();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
        private InventoryItemData[] Slots(SlotType type) => type == SlotType.Inventory ? _inventorySlots : _quickSlots;
        private bool ValidSlot(SlotType type, int index) =>
            (type == SlotType.Inventory || type == SlotType.QuickSlot) && index >= 0 && index < Slots(type).Length;
        private void Changed(PlayerStats player = null)
        {
            SyncWithQuickItemManager(player);
            OnInventoryChanged?.Invoke();
        }

        public InventoryItemData GetSlot(SlotType type, int index) => ValidSlot(type, index) ? Slots(type)[index] : null;
        public void SetSlot(SlotType type, int index, InventoryItemData item)
        {
            if (!ValidSlot(type, index)) return;
            Slots(type)[index] = item != null && item.count > 0 ? item.Clone(Mathf.Min(item.count, item.maxStack)) : null;
            Changed();
        }
        public void ClearHeldItem() { CursorHeldItem = null; Changed(); }

        public void HandleLeftClick(SlotType type, int index, bool isShift, PlayerStats player)
        {
            if (!ValidSlot(type, index)) return;
            var slots = Slots(type);
            var item = slots[index];
            if (isShift)
            {
                if (item == null) return;
                item.count = TryAddToSlots(type == SlotType.Inventory ? _quickSlots : _inventorySlots, item);
                if (item.count <= 0) slots[index] = null;
            }
            else if (CursorHeldItem == null)
            {
                CursorHeldItem = item;
                slots[index] = null;
            }
            else if (item == null)
            {
                slots[index] = CursorHeldItem;
                CursorHeldItem = null;
            }
            else if (string.Equals(item.id, CursorHeldItem.id, StringComparison.OrdinalIgnoreCase))
            {
                int amount = Mathf.Min(CursorHeldItem.count, Mathf.Max(0, item.maxStack - item.count));
                item.count += amount;
                CursorHeldItem.count -= amount;
                if (CursorHeldItem.count <= 0) CursorHeldItem = null;
            }
            else
            {
                slots[index] = CursorHeldItem;
                CursorHeldItem = item;
            }
            Changed(player);
        }

        public void HandleRightClick(SlotType type, int index, PlayerStats player)
        {
            if (!ValidSlot(type, index)) return;
            var slots = Slots(type);
            var item = slots[index];
            if (CursorHeldItem == null)
            {
                if (item == null) return;
                int take = (item.count + 1) / 2;
                CursorHeldItem = item.Clone(take);
                item.count -= take;
                if (item.count <= 0) slots[index] = null;
            }
            else if (item == null || (item.CanStackWith(CursorHeldItem) && item.count < item.maxStack))
            {
                if (item == null) slots[index] = CursorHeldItem.Clone(1);
                else item.count++;
                CursorHeldItem.count--;
                if (CursorHeldItem.count <= 0) CursorHeldItem = null;
            }
            else return;
            Changed(player);
        }

        public void DropCursorItemToWorld(bool dropOneOnly, Vector3 dropPos)
        {
            if (CursorHeldItem == null) return;
            int count = dropOneOnly ? 1 : CursorHeldItem.count;
            var pickup = WorldItemPickup.Spawn(CursorHeldItem.Clone(count), dropPos);
            if (pickup == null) return;
            pickup.ConfigurePlayerDrop(FindAnyObjectByType<PlayerStats>());
            CursorHeldItem.count -= count;
            if (CursorHeldItem.count <= 0) CursorHeldItem = null;
            Changed();
        }

        public void Close()
        {
            if (CursorHeldItem == null) return;
            CursorHeldItem.count = TryAddToSlots(_inventorySlots, CursorHeldItem);
            if (CursorHeldItem.count <= 0) CursorHeldItem = null;
            else
            {
                var player = FindAnyObjectByType<PlayerStats>();
                DropCursorItemToWorld(false, player != null ? player.transform.position : Vector3.zero);
            }
            Changed();
        }

        // The caller retains the unaccepted remainder (used by world pickups).
        public int AddItem(InventoryItemData item)
        {
            if (item == null || item.count <= 0) return 0;
            item.count = TryMergeIntoExistingSlots(_quickSlots, item);
            item.count = TryMergeIntoExistingSlots(_inventorySlots, item);
            item.count = TryAddToSlots(_quickSlots, item);
            item.count = TryAddToSlots(_inventorySlots, item);
            Changed();
            return item.count;
        }

        private int TryMergeIntoExistingSlots(InventoryItemData[] slots, InventoryItemData item)
        {
            int remaining = item.count;
            foreach (var existing in slots)
            {
                if (remaining <= 0) break;
                if (existing == null || !existing.CanStackWith(item)) continue;
                int add = Mathf.Min(remaining, Mathf.Max(0, existing.maxStack - existing.count));
                existing.count += add;
                remaining -= add;
            }
            return remaining;
        }

        private int TryAddToSlots(InventoryItemData[] slots, InventoryItemData item)
        {
            int remaining = TryMergeIntoExistingSlots(slots, item);
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i] != null) continue;
                int count = Mathf.Min(remaining, item.maxStack);
                slots[i] = item.Clone(count);
                remaining -= count;
            }
            return remaining;
        }

        // Contextual use (e.g. a chest) consumes one physical item, never an unlock flag.
        public int CountItem(string id)
        {
            int count = 0;
            foreach (var slots in new[] { _inventorySlots, _quickSlots })
                foreach (var item in slots)
                    if (item != null && string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase)) count += item.count;
            if (CursorHeldItem != null && string.Equals(CursorHeldItem.id, id, StringComparison.OrdinalIgnoreCase)) count += CursorHeldItem.count;
            return count;
        }

        public bool TryConsumeItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            foreach (var slots in new[] { _inventorySlots, _quickSlots })
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var item = slots[i];
                    if (item == null || item.count <= 0 || !string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase)) continue;
                    if (--item.count == 0) slots[i] = null;
                    Changed();
                    return true;
                }
            }
            if (CursorHeldItem == null || CursorHeldItem.count <= 0 ||
                !string.Equals(CursorHeldItem.id, id, StringComparison.OrdinalIgnoreCase)) return false;
            if (--CursorHeldItem.count == 0) CursorHeldItem = null;
            Changed();
            return true;
        }

        public bool CanUseQuickSlot(int index, PlayerStats player)
        {
            var item = GetSlot(SlotType.QuickSlot, index);
            if (player == null || player.IsDead || item == null || item.count <= 0 || !item.isConsumable || item.onUse == null) return false;
            if (item.canUse != null) return item.canUse(player);
            if ((item.id == "potion_heal" || item.id == "bread") && player.CurrentHP >= player.MaxHP) return false;
            if (item.id == "potion_stamina" && player.CurrentStamina >= player.MaxStamina) return false;
            return true;
        }

        public bool UseQuickSlot(int index, PlayerStats player)
        {
            if (!CanUseQuickSlot(index, player)) return false;
            var item = _quickSlots[index];
            item.onUse(player);
            if (--item.count <= 0)
            {
                _quickSlots[index] = null;
                if (index == 0)
                {
                    for (int i = 0; i < QuickSlotCount - 1; i++) _quickSlots[i] = _quickSlots[i + 1];
                    _quickSlots[QuickSlotCount - 1] = null;
                }
            }
            Changed(player);
            return true;
        }

        public void SyncWithQuickItemManager(PlayerStats player = null)
        {
            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            int potions = CursorHeldItem != null && CursorHeldItem.id == "potion_heal" ? CursorHeldItem.count : 0;
            foreach (var item in _inventorySlots) if (item != null && item.id == "potion_heal") potions += item.count;
            foreach (var item in _quickSlots) if (item != null && item.id == "potion_heal") potions += item.count;
            if (player != null) player.SyncHealingPotions(potions);
            QuickItemManager.Instance?.SyncFromInventory(_quickSlots);
        }

        public void InitializeDefaultInventory(PlayerStats player = null)
        {
            Array.Clear(_inventorySlots, 0, InventorySlotCount);
            Array.Clear(_quickSlots, 0, QuickSlotCount);
            CursorHeldItem = null;
            _quickSlots[0] = ItemRegistry.CreateItem("potion_heal", 3);
            Changed(player);
        }

        public void SaveTo(PlayerSaveData data)
        {
            if (data == null) return;
            if (data.inventory == null) data.inventory = new List<SavedItemData>();
            data.inventory.Clear();
            data.inventoryInitialized = true;
            for (int i = 0; i < InventorySlotCount; i++) SaveItem(data, _inventorySlots[i], i);
            for (int i = 0; i < QuickSlotCount; i++) SaveItem(data, _quickSlots[i], 100 + i);
            // Preserve a held stack even when saving before the inventory window closes.
            data.cursorItem = CursorHeldItem == null ? null : new SavedItemData { itemId = CursorHeldItem.id, count = CursorHeldItem.count };
        }

        private static void SaveItem(PlayerSaveData data, InventoryItemData item, int index)
        {
            if (item != null && item.count > 0)
                data.inventory.Add(new SavedItemData { slotIndex = index, itemId = item.id, count = item.count });
        }

        public void LoadFrom(PlayerSaveData data, PlayerStats player = null)
        {
            if (data == null) return;
            Array.Clear(_inventorySlots, 0, InventorySlotCount);
            Array.Clear(_quickSlots, 0, QuickSlotCount);
            CursorHeldItem = null;
            if (data.inventory != null)
                foreach (var saved in data.inventory)
                {
                    if (saved == null || saved.count <= 0) continue;
                    var item = ItemRegistry.CreateItem(saved.itemId, saved.count);
                    if (saved.slotIndex >= 100 && saved.slotIndex < 100 + QuickSlotCount) _quickSlots[saved.slotIndex - 100] = item;
                    else if (saved.slotIndex >= 0 && saved.slotIndex < InventorySlotCount) _inventorySlots[saved.slotIndex] = item;
                }
            if (!data.inventoryInitialized && (data.inventory == null || data.inventory.Count == 0) && data.potions > 0)
            {
                var legacyPotions = ItemRegistry.CreateItem("potion_heal");
                legacyPotions.count = data.potions;
                legacyPotions.count = TryAddToSlots(_quickSlots, legacyPotions);
                TryAddToSlots(_inventorySlots, legacyPotions);
            }
            if (data.cursorItem != null && data.cursorItem.count > 0)
            {
                var held = ItemRegistry.CreateItem(data.cursorItem.itemId, data.cursorItem.count);
                if (held != null)
                {
                    held.count = TryAddToSlots(_inventorySlots, held);
                    if (held.count > 0) CursorHeldItem = held;
                }
            }
            // Convert old permanent-key saves once. If every slot and the cursor are
            // occupied, retain the legacy flag until there is room on a later load.
            if (data.churchKey)
            {
                bool alreadyOwned = Array.Exists(_inventorySlots, i => i != null && i.id == "church_key") ||
                    Array.Exists(_quickSlots, i => i != null && i.id == "church_key") || CursorHeldItem?.id == "church_key";
                if (alreadyOwned || (data.runes != null && data.runes.Length > 0 && data.runes[0])) data.churchKey = false;
                else
                {
                    var key = ItemRegistry.CreateItem("church_key");
                    key.count = TryAddToSlots(_inventorySlots, key);
                    key.count = TryAddToSlots(_quickSlots, key);
                    if (key.count > 0 && CursorHeldItem == null) { CursorHeldItem = key; key = null; }
                    if (key == null || key.count == 0) data.churchKey = false;
                }
            }
            Changed(player);
        }
    }
}
