using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Inventory;

namespace TheLastKnight.Environment
{
    public enum ChestItemRarity { Normal, Rare, Epic }

    [CreateAssetMenu(menuName = "The Last Knight/Chest Loot Table")]
    public class ChestLootTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public bool enabled = true;
            public ItemDefinition item;
            public ChestItemRarity rarity;
        }
        [Tooltip("All eligible items. Disable entries to exclude them from random loot.")]
        public List<Entry> items = new List<Entry>();

        public static bool IsExcluded(ItemDefinition item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id)) return true;
            string id = item.id.ToLowerInvariant();
            return id.StartsWith("rune_") || id == "church_key" || id == "moonstone_shard" ||
                id == "drop_demonboss" || id == "drop_fox" || id == "drop_mechastonegolem" || id == "drop_volcanox";
        }

        public static int GetItemValue(ItemDefinition item)
        {
            if (item == null) return 0;
            int potionValue = PotionPrices.GetSellPrice(item.id);
            if (potionValue > 0) return potionValue;
            // Gold containers are valued by the Gold received on use.
            if (string.Equals(item.id, "gold_pouch", StringComparison.OrdinalIgnoreCase)) return 500;
            if (item.useEffect == ItemUseEffect.AddGold) return Mathf.Max(1, item.effectAmount);
            return item.sellPrice > 0 ? item.sellPrice : Mathf.Max(0, ItemRegistry.GetSellPrice(item.id));
        }

        public static ChestItemRarity RarityForValue(int value)
        {
            return value <= 50 ? ChestItemRarity.Normal : value <= 100 ? ChestItemRarity.Rare : ChestItemRarity.Epic;
        }

        private void OnValidate()
        {
            foreach (var entry in items)
            {
                if (entry == null) continue;
                entry.rarity = RarityForValue(GetItemValue(entry.item));
                if (IsExcluded(entry.item)) entry.enabled = false;
            }
        }

        public List<ItemDefinition> GetPool(ChestItemRarity rarity)
        {
            var pool = new List<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in items)
                if (entry != null && entry.enabled && !IsExcluded(entry.item) && RarityForValue(GetItemValue(entry.item)) == rarity && entry.item != null &&
                    !string.IsNullOrWhiteSpace(entry.item.id) && ids.Add(entry.item.id)) pool.Add(entry.item);
            return pool;
        }
    }
}
