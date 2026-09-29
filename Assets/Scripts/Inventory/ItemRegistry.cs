using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    public static class ItemRegistry
    {
        private static readonly Dictionary<string, Func<InventoryItemData>> _registry = new Dictionary<string, Func<InventoryItemData>>(StringComparer.OrdinalIgnoreCase)
        {
            ["potion_heal"] = () => new InventoryItemData
            {
                id = "potion_heal",
                name = "Healing Potion [Q]",
                typeName = "Consumable",
                description = "Restores 50 HP immediately. Place in Quick Slot 1 to drink with [Q].",
                iconPath = "CharacterStatus/Item_RedPotion_Clean",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null && player.CurrentHP < player.MaxHP)
                    {
                        player.Heal(50f);
                    }
                }
            },
            ["potion_stamina"] = () => new InventoryItemData
            {
                id = "potion_stamina",
                name = "Stamina Elixir",
                typeName = "Consumable",
                description = "Instantly replenishes 100 Stamina points.",
                iconPath = "CharacterStatus/Items/Item_GreenPotion",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.RestoreStamina(100f);
                    }
                }
            },
            ["golden_seed"] = () => new InventoryItemData
            {
                id = "golden_seed",
                name = "Golden Seed",
                typeName = "Sacred Relic",
                description = "A seed imbued with ancient holy light. Bestows +1 Attribute Stat Point (SP).",
                iconPath = "CharacterStatus/Items/Item_GoldenSeed",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.AddStatPoints(1);
                    }
                }
            },
            ["potion_might"] = () => new InventoryItemData
            {
                id = "potion_might",
                name = "Potion of Might",
                typeName = "Elixir",
                description = "Increases attack power by 25% for 30 seconds. Using again refreshes the duration.",
                iconPath = "CharacterStatus/Items/Item_VioletPotion",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyMightBuff();
                    }
                }
            },
            ["bread"] = () => new InventoryItemData
            {
                id = "bread",
                name = "Field Ration",
                typeName = "Ration",
                description = "A field ration. Restores 25 HP.",
                iconPath = "CharacterStatus/Items/Item_Herb",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null && player.CurrentHP < player.MaxHP)
                    {
                        player.Heal(25f);
                    }
                }
            },
            ["gold_pouch"] = () => new InventoryItemData
            {
                id = "gold_pouch",
                name = "Gold Pouch",
                typeName = "Valuables",
                description = "A heavy pouch filled with gleaming coins. Grants 500 Gold.",
                iconPath = "CharacterStatus/Items/Item_Pouch",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.AddGold(500);
                    }
                }
            },
            ["smoke_bomb"] = () => new InventoryItemData
            {
                id = "smoke_bomb",
                name = "Shadow Smoke Bomb",
                typeName = "Tactical Tool",
                description = "Conceals the user and creates distraction in combat.",
                iconPath = "CharacterStatus/Items/Item_SmokeBomb",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true
            },
            ["throwing_dart"] = () => new InventoryItemData
            {
                id = "throwing_dart",
                name = "Hunting Dart",
                typeName = "Ranged Tool",
                description = "Sharpened iron throwing dart crafted for silent attacks.",
                iconPath = "CharacterStatus/Items/Item_Dart",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true
            },
            ["knight_sword"] = () => new InventoryItemData
            {
                id = "knight_sword",
                name = "Arthur's Knight Sword",
                typeName = "Weapon",
                description = "A finely forged royal blade engraved with kingdom crests. Increases attack power.",
                iconPath = "CharacterStatus/Items/Item_KnightSword",
                category = ItemCategory.Weapon,
                maxStack = 1,
                isConsumable = false
            },
            ["silver_armor"] = () => new InventoryItemData
            {
                id = "silver_armor",
                name = "Royal Silver Armor",
                typeName = "Armor",
                description = "Blessed silver plate armor forged to withstand dark demonic assaults.",
                iconPath = "CharacterStatus/Items/Item_SilverArmor",
                category = ItemCategory.Armor,
                maxStack = 1,
                isConsumable = false
            },
            ["heavy_boots"] = () => new InventoryItemData
            {
                id = "heavy_boots",
                name = "Heavy Knight Greaves",
                typeName = "Boots",
                description = "Reinforced steel greaves granting stability and protection.",
                iconPath = "CharacterStatus/Items/Item_HeavyBoots",
                category = ItemCategory.Boots,
                maxStack = 1,
                isConsumable = false
            },
            ["moonstone_shard"] = () => new InventoryItemData
            {
                id = "moonstone_shard",
                name = "Moonstone Shard",
                typeName = "Material",
                description = "A rare lunar mineral glowing with primordial energy. Used for sacred crafting.",
                iconPath = "CharacterStatus/Items/Item_MoonstoneShard",
                category = ItemCategory.Material,
                maxStack = 64,
                isConsumable = false
            }
        };

        public static IEnumerable<string> LegacyIds => _registry.Keys;

        public static InventoryItemData CreateLegacyItem(string id)
        {
            return id != null && _registry.TryGetValue(id, out var factory) ? factory() : null;
        }

        private static Dictionary<string, ItemDefinition> _definitions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReloadDefinitions() => _definitions = null;

        private static void LoadDefinitions()
        {
            if (_definitions != null) return;
            _definitions = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in Resources.LoadAll<ItemDefinition>("Items/Definitions"))
            {
                if (string.IsNullOrWhiteSpace(definition.id))
                {
                    Debug.LogError($"Item definition '{definition.name}' needs an ID.", definition);
                    continue;
                }
                if (duplicates.Contains(definition.id)) continue;
                if (_definitions.ContainsKey(definition.id))
                {
                    Debug.LogError($"Duplicate item ID '{definition.id}'. Definitions for this ID are ignored.", definition);
                    _definitions.Remove(definition.id);
                    duplicates.Add(definition.id);
                    continue;
                }
                _definitions.Add(definition.id, definition);
            }
        }

        public static InventoryItemData CreateItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return null;

            LoadDefinitions();
            if (_definitions.TryGetValue(id, out var definition))
                return definition.CreateItem(count, CreateLegacyItem(id));

            if (_registry.TryGetValue(id, out var factory))
            {
                var item = factory();
                item.count = Mathf.Clamp(count, 1, item.maxStack);
                return item;
            }

            // Fallback for custom or unknown items
            return new InventoryItemData
            {
                id = id,
                name = id,
                typeName = "Item",
                description = "An item found in the world.",
                iconPath = "CharacterStatus/Items/Item_Pouch",
                count = Mathf.Clamp(count, 1, 64),
                maxStack = 64,
                category = ItemCategory.Material,
                isConsumable = false
            };
        }
    }
}
