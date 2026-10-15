using System;
using UnityEngine;

// Preserve the original type names for serialized catalogs and existing callers.
namespace TheLastKnight.UI
{
    public enum ShopItemType
    {
        InventoryItem, // Added to player inventory (potions, food, bombs, darts, etc.)
        StatPotion,    // Directly upgrades STR, VIT, DEX, or AGI attribute
        RuneReward     // Demon rune reward dropped into the world
    }

    [System.Serializable]
    public class ShopItemConfig
    {
        [Tooltip("Unique ID matching ItemRegistry id or 'RUNE', 'STR', 'VIT', 'DEX', 'AGI'")]
        public string id = "potion_heal";

        [Tooltip("Display name shown in shop cards")]
        public string displayName = "Healing Potion [Q]";

        [Tooltip("Category or subtitle (e.g. Consumable, Attribute Elixir)")]
        public string category = "Consumable Potion";

        [TextArea(2, 4)]
        [Tooltip("Item description shown in tooltips")]
        public string description = "Restores 50 HP immediately. Place in Quick Slot to drink on demand with [Q]. (Max 5 held)";

        [Tooltip("Purchase cost in Gold")]
        public int buyPrice = 50;

        [Tooltip("Initial stock limit. Set to -1 for infinite / unlimited stock.")]
        public int initialStock = -1;

        [Tooltip("Custom sell price override. Set to -1 to automatically calculate 75% of buyPrice.")]
        public int customSellPrice = -1;

        [Tooltip("Type of item behavior when purchased")]
        public ShopItemType itemType = ShopItemType.InventoryItem;

        [Tooltip("Specific stat to upgrade if itemType is StatPotion ('STR', 'VIT', 'DEX', 'AGI')")]
        public string statName = "STR";

        [Tooltip("Rune index (0-3) if itemType is RuneReward")]
        public int runeIndex = 3;

        [Tooltip("Custom icon override. If null, loads from ItemRegistry or Resources/CharacterStatus.")]
        public Sprite customIcon;

        // Runtime stock tracking
        [NonSerialized] public int currentStock = -1;
        [NonSerialized] public bool stockInitialized = false;

        public void EnsureStockInitialized()
        {
            if (!stockInitialized)
            {
                currentStock = initialStock;
                stockInitialized = true;
            }
        }
    }

}
