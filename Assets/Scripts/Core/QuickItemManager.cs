using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Core
{
    [Serializable]
    public class QuickItemSlotData
    {
        public string id;
        public string name;
        public string typeName;
        public string description;
        public Sprite icon;
        public int count;
        public int maxCount;
        public Action<PlayerStats> onUse;

        public QuickItemSlotData Copy()
        {
            return new QuickItemSlotData
            {
                id = this.id,
                name = this.name,
                typeName = this.typeName,
                description = this.description,
                icon = this.icon,
                count = this.count,
                maxCount = this.maxCount,
                onUse = this.onUse
            };
        }
    }

    public class QuickItemManager : MonoBehaviour
    {
        public static QuickItemManager Instance { get; private set; }
        public const int MaxSlots = 5;

        private TheLastKnight.Inventory.InventoryManager _inventory;
        private readonly QuickItemSlotData[] _views = new QuickItemSlotData[MaxSlots];
        public event Action OnQuickItemsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("QuickItemManager");
                go.AddComponent<QuickItemManager>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null && Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeDefaultSlots();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void InitializeDefaultSlots()
        {
            TheLastKnight.Inventory.InventoryManager.Instance?.SyncWithQuickItemManager();
        }

        public void SyncFromInventory(TheLastKnight.Inventory.InventoryManager inventory)
        {
            _inventory = inventory;
            OnQuickItemsChanged?.Invoke();
        }

        // Reuse display records, but always read selection/counts from inventory.
        public QuickItemSlotData GetSlot(int index)
        {
            if (index < 0 || index >= MaxSlots || _inventory == null) return null;
            var item = _inventory.GetSlot(TheLastKnight.Inventory.SlotType.QuickSlot, index);
            if (item == null) return null;
            var view = _views[index] ?? (_views[index] = new QuickItemSlotData());
            view.id = item.id; view.name = item.name; view.typeName = item.typeName;
            view.description = item.description; view.icon = item.Icon;
            view.count = item.count; view.maxCount = item.maxStack; view.onUse = item.onUse;
            return view;
        }
        public int GetActiveSlotIndex() => _inventory != null ? _inventory.GetActiveQuickSlotIndex() : -1;
        public QuickItemSlotData GetActiveItem() => GetSlot(GetActiveSlotIndex());
        public bool UseSlot(int index, PlayerStats player) =>
            TheLastKnight.Inventory.InventoryManager.Instance?.UseQuickSlot(index, player) ?? false;
    }
}
