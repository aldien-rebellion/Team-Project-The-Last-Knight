using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class EnemyLootDropTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type FindType(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName)).FirstOrDefault(type => type != null);

        private readonly (string enemy, string itemId, string expectedSpriteStart, bool isBoss)[] _monsters = new[]
        {
            ("ArchDemon", "drop_archdemon", "Free-Goblin-Loot-Icon42", false),
            ("BlueSlime", "drop_blueslime", "free-rpg-loot_Icon15", false),
            ("BringerOfDeath", "drop_bringerofdeath", "Free-Goblin-Loot-Icon48", false),
            ("Demon", "drop_demon", "Free-Goblin-Loot-Icon31", false),
            ("DemonBoss", "drop_demonboss", "Free-Goblin-Loot-Icon8", true),
            ("DemonKin", "drop_demonkin", "Free-Goblin-Loot-Icon25", false),
            ("Dragon", "drop_dragon", "free-rpg-loot_Icon9", false),
            ("FantasyMushroom", "drop_fantasymushroom", "20", false),
            ("FireWorm", "drop_fireworm", "free-rpg-loot_Icon42", false),
            ("FlyingEye", "drop_flyingeye", "Free-Goblin-Loot-Icon10", false),
            ("ForestMushroom", "drop_forestmushroom", "44", false),
            ("Fox", "drop_fox", "free-rpg-loot_Icon40", true),
            ("Goblin", "drop_goblin", "Free-Goblin-Loot-Icon3", false),
            ("HoodedProtagonist", "drop_hoodedprotagonist", "Free-Goblin-Loot-Icon22", false),
            ("Jinn", "drop_jinn", "free-rpg-loot_Icon19", false),
            ("Lizard", "drop_lizard", "free-rpg-loot_Icon18", false),
            ("MechaStoneGolem", "drop_mechastonegolem", "crystal_white-gold1", true),
            ("Minotaur", "drop_minotaur", "Free-Goblin-Loot-Icon43", false),
            ("Necromancer", "drop_necromancer", "Free-Goblin-Loot-Icon39", false),
            ("Reaper", "drop_reaper", "Free-Goblin-Loot-Icon7", false),
            ("Satyr", "drop_satyr", "40", false),
            ("ShadowDemonDragon", "drop_shadowdemondragon", "Free-Goblin-Loot-Icon9", true),
            ("Skeleton", "drop_skeleton", "free-rpg-loot_Icon8", false),
            ("SkeletonKnight", "drop_skeletonknight", "free-rpg-loot_Icon20", false),
            ("Skullwolf", "drop_skullwolf", "free-rpg-loot_Icon32", false),
            ("Small_dragon", "drop_smalldragon", "Free-Goblin-Loot-Icon44", false),
            ("UndeadExecutioner", "drop_undeadexecutioner", "free-rpg-loot_Icon34", false),
            ("Volcanox", "drop_volcanox", "sea of fire", true)
        };

        [Test]
        public void All28MonsterItems_ExistAndHaveCorrectSprites()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });
            Assert.IsNotNull(createItemMethod, "CreateItem method must exist.");

            foreach (var m in _monsters)
            {
                var item = createItemMethod.Invoke(null, new object[] { m.itemId, 1 });
                Assert.IsNotNull(item, $"Item '{m.itemId}' for monster '{m.enemy}' must exist in ItemRegistry.");

                var iconProp = item.GetType().GetProperty("Icon");
                var icon = (Sprite)iconProp.GetValue(item);
                Assert.IsNotNull(icon, $"Item '{m.itemId}' must have an Icon sprite.");
                Assert.That(icon.name.StartsWith(m.expectedSpriteStart, StringComparison.OrdinalIgnoreCase),
                    $"Item '{m.itemId}' sprite '{icon.name}' does not start with expected prefix '{m.expectedSpriteStart}'.");
            }
        }

        [Test]
        public void Boss_AlwaysDropsGuaranteedOneItem()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Boss_DemonBoss");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_demonboss");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, true);
                enemyLootDropType.GetProperty("BossGuaranteedCount").SetValue(loot, 1);

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(1, spawned.Count, "Boss must drop exactly 1 guaranteed item.");
                var pickupComp = spawned[0].GetComponent(pickupType);
                var itemData = pickupType.GetProperty("ItemData").GetValue(pickupComp);
                string id = (string)itemData.GetType().GetField("id").GetValue(itemData);
                Assert.AreEqual("drop_demonboss", id);
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NormalMonster_WhenChanceGuaranteed_DropsThreeItems()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Normal_BlueSlime");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_blueslime");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, false);
                enemyLootDropType.GetProperty("DropRolls").SetValue(loot, 3);
                enemyLootDropType.GetProperty("DropChance").SetValue(loot, 1f); // 100% chance per roll -> 3 items

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(3, spawned.Count, "Normal monster with 100% drop chance over 3 rolls must drop 3 items.");
                foreach (var s in spawned)
                {
                    var pickupComp = s.GetComponent(pickupType);
                    var itemData = pickupType.GetProperty("ItemData").GetValue(pickupComp);
                    string id = (string)itemData.GetType().GetField("id").GetValue(itemData);
                    Assert.AreEqual("drop_blueslime", id);
                }
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NormalMonster_WhenChanceZero_DropsZeroItems()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Normal_BlueSlime_Zero");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_blueslime");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, false);
                enemyLootDropType.GetProperty("DropRolls").SetValue(loot, 3);
                enemyLootDropType.GetProperty("DropChance").SetValue(loot, 0f); // 0% chance

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(0, spawned.Count, "Normal monster with 0% drop chance must drop 0 items.");
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnemyLootDrop_DefaultItemIdLookup_MatchesAll28Monsters()
        {
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var lookupMethod = enemyLootDropType.GetMethod("GetDefaultDropItemId", StaticMembers);

            foreach (var m in _monsters)
            {
                string id = (string)lookupMethod.Invoke(null, new object[] { m.enemy });
                Assert.AreEqual(m.itemId, id, $"Lookup for '{m.enemy}' should return '{m.itemId}'.");
            }
        }

        [Test]
        public void All23NormalMonsterDropItems_HaveExpectedSellPrices()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var getSellPriceMethod = registryType.GetMethod("GetSellPrice", StaticMembers);
            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });

            var expectedPrices = new Dictionary<string, int>
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
                ["drop_smalldragon"] = 846
            };

            foreach (var kvp in expectedPrices)
            {
                if (getSellPriceMethod != null)
                {
                    int staticPrice = (int)getSellPriceMethod.Invoke(null, new object[] { kvp.Key });
                    Assert.AreEqual(kvp.Value, staticPrice, $"Static GetSellPrice for {kvp.Key} mismatch");
                }

                var item = createItemMethod.Invoke(null, new object[] { kvp.Key, 1 });
                Assert.IsNotNull(item, $"Item {kvp.Key} must be created");

                var sellPriceField = item.GetType().GetField("sellPrice", InstanceMembers);
                if (sellPriceField != null)
                {
                    int itemPrice = (int)sellPriceField.GetValue(item);
                    Assert.AreEqual(kvp.Value, itemPrice, $"Item.sellPrice for {kvp.Key} mismatch");
                }
            }
        }

        [Test]
        public void AllBossDropItems_HaveExpectedMaxGoldSellPrices()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var getSellPriceMethod = registryType.GetMethod("GetSellPrice", StaticMembers);
            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });

            var expectedBossPrices = new Dictionary<string, int>
            {
                // MechaStoneGolem: Lv 20 -> MaxGold (20 * 80) = 1600
                ["drop_mechastonegolem"] = 1600,
                // DemonBoss: Lv 60 -> MaxGold (60 * 80) = 4800
                ["drop_demonboss"] = 4800,
                // Fox: Lv 80 -> MaxGold (80 * 80) = 6400
                ["drop_fox"] = 6400,
                // ShadowDemonDragon: Lv 96 -> MaxGold (96 * 80) = 7680
                ["drop_shadowdemondragon"] = 7680,
                // Volcanox: Lv 100 -> MaxGold (100 * 80) = 8000
                ["drop_volcanox"] = 8000,
                // MoonstoneKeeper: Lv 40 -> MaxGold (40 * 80) = 3200
                ["moonstone_shard"] = 3200
            };

            foreach (var kvp in expectedBossPrices)
            {
                if (getSellPriceMethod != null)
                {
                    int staticPrice = (int)getSellPriceMethod.Invoke(null, new object[] { kvp.Key });
                    Assert.AreEqual(kvp.Value, staticPrice, $"Static GetSellPrice for boss item {kvp.Key} mismatch");
                }

                var item = createItemMethod.Invoke(null, new object[] { kvp.Key, 1 });
                Assert.IsNotNull(item, $"Item {kvp.Key} must be created");

                var sellPriceField = item.GetType().GetField("sellPrice", InstanceMembers);
                if (sellPriceField != null)
                {
                    int itemPrice = (int)sellPriceField.GetValue(item);
                    Assert.AreEqual(kvp.Value, itemPrice, $"Item.sellPrice for boss item {kvp.Key} mismatch");
                }
            }
        }
    }
}
