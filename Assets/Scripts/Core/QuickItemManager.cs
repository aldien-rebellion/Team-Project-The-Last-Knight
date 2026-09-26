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
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeDefaultSlots();
        }

        public void InitializeDefaultSlots()
        {
            for (int i = 0; i < MaxSlots; i++) _slots[i] = null;

            int initialPotions = 3;
            if (PlayerStatsActive != null)
            {
                initialPotions = PlayerStatsActive.HealingPotions;
            }

            if (initialPotions > 0)
            {
                _slots[0] = CreateHealingPotionSlot(initialPotions);
            }

            OnQuickItemsChanged?.Invoke();
        }

        private PlayerStats PlayerStatsActive
        {
            get
            {
                if (GameManager.Instance != null && GameManager.Instance.Player != null)
                {
                    return GameManager.Instance.Player;
                }
                return FindAnyObjectByType<PlayerStats>();
            }
        }

        public QuickItemSlotData CreateHealingPotionSlot(int count)
        {
            var sprite = Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean")
                         ?? Resources.Load<Sprite>("CharacterStatus/Item_RedPotion");
            return new QuickItemSlotData
            {
                id = "potion_heal",
                name = "Healing Potion [Q]",
                typeName = "Consumable",
                description = "Restores 50 HP immediately. Hotkey [Q]. [Click to drink]",
                icon = sprite,
                count = count,
                maxCount = 5,
                onUse = (player) =>
                {
                    if (player != null && player.HealingPotions > 0 && player.CurrentHP < player.MaxHP)
                    {
                        player.CompletePotionDrink();
                    }
                }
            };
        }

        public QuickItemSlotData GetSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return null;
            return _slots[index];
        }

        public QuickItemSlotData GetActiveItem()
        {
            return _slots[0];
        }

        public void SetSlot(int index, QuickItemSlotData item)
        {
            if (index < 0 || index >= MaxSlots) return;
            _slots[index] = item;
            OnQuickItemsChanged?.Invoke();
        }

        /// <summary>
        /// Syncs item count for a specific item id (e.g. potion count synced from PlayerStats).
        /// If count reaches 0 and item is in Slot 1, shifts the queue automatically.
        /// </summary>
        public void SyncItemCount(string itemId, int count)
        {
            bool changed = false;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] != null && _slots[i].id == itemId)
                {
                    _slots[i].count = count;
                    changed = true;
                    if (count <= 0)
                    {
                        if (i == 0)
                        {
                            ShiftQueue();
                            return; // ShiftQueue already invokes OnQuickItemsChanged
                        }
                        else
                        {
                            _slots[i] = null;
                        }
                    }
                }
            }

            if (count > 0 && !HasItem(itemId))
            {
                AssignToFirstEmptySlot(itemId, count);
                changed = true;
            }

            if (changed)
            {
                OnQuickItemsChanged?.Invoke();
            }
        }

        public bool HasItem(string itemId)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] != null && _slots[i].id == itemId) return true;
            }
            return false;
        }

        public void AssignToFirstEmptySlot(string itemId, int count)
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                if (_slots[i] == null)
                {
                    if (itemId == "potion_heal")
                    {
                        _slots[i] = CreateHealingPotionSlot(count);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Shifts quick items: Slot 2 -> Slot 1, Slot 3 -> Slot 2, Slot 4 -> Slot 3, Slot 5 -> Slot 4, Slot 5 becomes empty.
        /// </summary>
        public void ShiftQueue()
        {
            for (int i = 0; i < MaxSlots - 1; i++)
            {
                _slots[i] = _slots[i + 1];
            }
            _slots[MaxSlots - 1] = null;
            OnQuickItemsChanged?.Invoke();
        }

        /// <summary>
        /// Uses the item in the specified slot (0-4).
        /// </summary>
        public bool UseSlot(int index, PlayerStats player)
        {
            if (index < 0 || index >= MaxSlots) return false;
            var slot = _slots[index];
            if (slot == null || slot.count <= 0) return false;

            if (slot.id == "potion_heal")
            {
                if (player == null || player.CurrentHP >= player.MaxHP || player.HealingPotions <= 0) return false;
                player.CompletePotionDrink();
                return true;
            }

            slot.onUse?.Invoke(player);
            slot.count--;
            if (slot.count <= 0)
            {
                if (index == 0) ShiftQueue();
                else _slots[index] = null;
            }
            OnQuickItemsChanged?.Invoke();
            return true;
        }
    }
}
