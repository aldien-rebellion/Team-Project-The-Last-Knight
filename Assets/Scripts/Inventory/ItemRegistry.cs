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
                name = "Healing Potion",
                typeName = "Potion",
                description = "Restores 30% Max HP + 50 HP immediately. Place in Quick Slot 1 to drink with [Q].",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-1",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null && player.CurrentHP < player.MaxHP)
                    {
                        player.Heal(player.MaxHP * 0.3f + 50f);
                    }
                }
            },
            ["potion_might"] = () => new InventoryItemData
            {
                id = "potion_might",
                name = "Potion of Might",
                typeName = "Potion",
                description = "Increases attack power by 25% for 30 seconds. Using again refreshes the duration.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-6",
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
            ["potion_swiftness"] = () => new InventoryItemData
            {
                id = "potion_swiftness",
                name = "Potion of Swiftness",
                typeName = "Potion",
                description = "Increases attack speed and movement speed (walk and sprint) by 25% for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-2",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplySwiftnessBuff();
                    }
                }
            },
            ["potion_endurance"] = () => new InventoryItemData
            {
                id = "potion_endurance",
                name = "Potion of Endurance",
                typeName = "Potion",
                description = "Reduces all stamina consumption by 25% for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-3",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyEnduranceBuff();
                    }
                }
            },
            ["potion_purity"] = () => new InventoryItemData
            {
                id = "potion_purity",
                name = "Potion of Purity",
                typeName = "Potion",
                description = "Cleanses negative effects and grants complete immunity to stun and status ailments for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-4",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyPurityBuff();
                    }
                }
            },
            ["potion_regeneration"] = () => new InventoryItemData
            {
                id = "potion_regeneration",
                name = "Potion of Regeneration",
                typeName = "Potion",
                description = "Continuously regenerates 5% of Max HP per second for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-5",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyRegenBuff();
                    }
                }
            },
            ["potion_fortitude"] = () => new InventoryItemData
            {
                id = "potion_fortitude",
                name = "Potion of Fortitude",
                typeName = "Potion",
                description = "Bolsters defenses, increasing Max HP and DEF by 25% for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-7",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyFortitudeBuff();
                    }
                }
            },
            ["potion_undying"] = () => new InventoryItemData
            {
                id = "potion_undying",
                name = "Potion of the Undying",
                typeName = "Potion",
                description = "Forbidden elixir. Sets HP to 1 and prevents all healing, but grants complete invincibility for 30 seconds.",
                iconPath = "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion 1-8",
                category = ItemCategory.Consumable,
                maxStack = 64,
                isConsumable = true,
                onUse = player =>
                {
                    if (player != null)
                    {
                        player.ApplyUndyingBuff();
                    }
                }
            },
            ["gold_pouch"] = () => new InventoryItemData
            {
                id = "gold_pouch",
                name = "Gold Pouch",
                typeName = "Valuables",
                description = "A heavy pouch filled with gleaming coins. Grants 500 Gold.",
                iconPath = "CharacterStatus/Items/Gold Pouch",
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
            ["moonstone_shard"] = () => new InventoryItemData
            {
                id = "moonstone_shard",
                name = "Moonstone Shard",
                typeName = "Material",
                description = "A rare lunar mineral glowing with primordial energy. Used for sacred crafting.",
                iconPath = "CharacterStatus/Items/craftpix-net-924817-free-crystals-pixel-art-asset-pack/PNG/crystals_black/crystal_black1",
                category = ItemCategory.Material,
                maxStack = 64,
                isConsumable = false
            }
        };

        public static IEnumerable<string> LegacyIds => _registry.Keys;

        public static List<InventoryItemData> GetAllItems()
        {
            LoadDefinitions();
            var items = new List<InventoryItemData>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in _definitions.Values)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id)) continue;
                ids.Add(definition.id);
                items.Add(definition.CreateItem(1, CreateLegacyItem(definition.id)));
            }
            foreach (var id in _registry.Keys)
                if (ids.Add(id)) items.Add(CreateItem(id));
            items.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return items;
        }

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

        private static readonly Dictionary<string, int> _monsterDropSellPrices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["drop_blueslime"] = 9,
            ["drop_skeleton"] = 18,
            ["drop_demonkin"] = 90,
            ["drop_lizard"] = 90,
            ["drop_dragon"] = 117,
            ["drop_minotaur"] = 153,
            ["drop_demon"] = 180,
            ["drop_jinn"] = 225,
            ["drop_skeletonknight"] = 288,
            ["drop_goblin"] = 360,
            ["drop_hoodedprotagonist"] = 405,
            ["drop_reaper"] = 432,
            ["drop_satyr"] = 477,
            ["drop_archdemon"] = 522,
            ["drop_fantasymushroom"] = 630,
            ["drop_fireworm"] = 648,
            ["drop_forestmushroom"] = 684,
            ["drop_flyingeye"] = 693,
            ["drop_undeadexecutioner"] = 711,
            ["drop_skullwolf"] = 738,
            ["drop_bringerofdeath"] = 810,
            ["drop_necromancer"] = 837,
            ["drop_smalldragon"] = 846,

            // Boss drops (Equal to maximum gold drop of that boss: Level * 80)
            ["drop_mechastonegolem"] = 1600,
            ["drop_demonboss"] = 4800,
            ["drop_fox"] = 6400,
            ["drop_shadowdemondragon"] = 7680,
            ["drop_volcanox"] = 8000,
            ["moonstone_shard"] = 3200
        };

        public static int GetSellPrice(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (_monsterDropSellPrices.TryGetValue(itemId, out int price)) return price;
            return 0;
        }

        public static InventoryItemData CreateItem(string id, int count = 1)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return null;

            LoadDefinitions();
            InventoryItemData item = null;
            if (_definitions.TryGetValue(id, out var definition))
            {
                item = definition.CreateItem(count, CreateLegacyItem(id));
            }
            else if (_registry.TryGetValue(id, out var factory))
            {
                item = factory();
                item.count = Mathf.Clamp(count, 1, item.maxStack);
            }
            else
            {
                // Fallback for custom or unknown items
                item = new InventoryItemData
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

            if (item != null && item.sellPrice <= 0 && _monsterDropSellPrices.TryGetValue(item.id, out int defaultSellPrice))
            {
                item.sellPrice = defaultSellPrice;
            }

            return item;
        }
    }
}
