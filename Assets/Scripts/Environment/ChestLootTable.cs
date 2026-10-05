using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Inventory;

namespace TheLastKnight.Environment
{
    public enum ChestItemRarity { Common, Rare, Epic }

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

        public List<ItemDefinition> GetPool(ChestItemRarity rarity)
        {
            var pool = new List<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in items)
                if (entry != null && entry.enabled && entry.rarity == rarity && entry.item != null &&
                    !string.IsNullOrWhiteSpace(entry.item.id) && ids.Add(entry.item.id)) pool.Add(entry.item);
            return pool;
        }
    }
}
