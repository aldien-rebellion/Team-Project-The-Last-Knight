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

        private readonly QuickItemSlotData[] _slots = new QuickItemSlotData[MaxSlots];
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

        public void SyncFromInventory(TheLastKnight.Inventory.InventoryItemData[] items)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                var item = items[i];
                _slots[i] = item == null ? null : new QuickItemSlotData
                {
                    id = item.id, name = item.name, typeName = item.typeName,
                    description = item.description, icon = item.Icon,
                    count = item.count, maxCount = item.maxStack, onUse = item.onUse
                };
            }
            OnQuickItemsChanged?.Invoke();
        }

        public QuickItemSlotData GetSlot(int index) => index >= 0 && index < MaxSlots ? _slots[index] : null;
        public int GetActiveSlotIndex()
        {
            for (int i = 0; i < MaxSlots; i++)
                if (_slots[i] != null && _slots[i].count > 0) return i;
            return -1;
        }
        public QuickItemSlotData GetActiveItem() => GetSlot(GetActiveSlotIndex());
        public bool UseSlot(int index, PlayerStats player) =>
            TheLastKnight.Inventory.InventoryManager.Instance?.UseQuickSlot(index, player) ?? false;
    }
}
