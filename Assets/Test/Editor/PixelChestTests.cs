using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class PixelChestTests
    {
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TheLastKnight.Environment." + name)).First(t => t != null);
        private static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);

        [Test]
        public void OpenedChest_SurvivesSaveReloadAndRecreatedMapInstance()
        {
            var chestType = Find("LootChest");
            var saveType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TheLastKnight.Core.PlayerSaveData")).First(t => t != null);
            var systemType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TheLastKnight.Core.SaveSystem")).First(t => t != null);
            var root = new GameObject("Chest persistence test");
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                var first = new GameObject("Chest"); first.transform.SetParent(root.transform);
                var chest = first.AddComponent(chestType);
                string id = (string)chestType.GetProperty("PersistentId").GetValue(chest);
                var other = new GameObject("Chest"); other.transform.SetParent(root.transform);
                var otherChest = other.AddComponent(chestType);
                Assert.AreNotEqual(id, chestType.GetProperty("PersistentId").GetValue(otherChest));
                var state = Activator.CreateInstance(saveType);
                saveType.GetField("initialized").SetValue(state, true);
                ((IList)Field(state, "openedLootChests")).Add(id);
                object[] saveArgs = { state, null, file };
                Assert.IsTrue((bool)systemType.GetMethod("Save").Invoke(null, saveArgs), (string)saveArgs[1]);
                object[] loadArgs = { null, file };
                Assert.IsTrue((bool)systemType.GetMethod("TryLoad").Invoke(null, loadArgs));
                UnityEngine.Object.DestroyImmediate(first);
                var restored = new GameObject("Chest"); restored.transform.SetParent(root.transform);
                restored.transform.SetSiblingIndex(0);
                var restoredChest = restored.AddComponent(chestType);
                Assert.AreEqual(id, chestType.GetProperty("PersistentId").GetValue(restoredChest));
                var restore = chestType.GetMethod("RestoreOpenedState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsTrue((bool)restore.Invoke(restoredChest, new[] { loadArgs[0] }));
                Assert.IsTrue((bool)chestType.GetProperty("IsOpened").GetValue(restoredChest));
                Assert.Less((float)Field(restoredChest, "interactionRange"), 0);
                Assert.IsFalse((bool)restore.Invoke(otherChest, new[] { loadArgs[0] }));
                // Old saves and new worlds have no opened-chest record.
                var legacy = JsonUtility.FromJson("{\"version\":1}", saveType);
                Assert.IsFalse((bool)restore.Invoke(otherChest, new[] { legacy }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
            }
        }

        [Test]
        public void AllEightPrefabs_HaveFramesAndRollWithinTheirBounds()
        {
            var state = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(815);
                var type = Find("LootChest");
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Environment/PixelChests" });
                Assert.AreEqual(8, guids.Length);
                var allIds = new System.Collections.Generic.HashSet<string>();
                foreach (var guid in guids)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    var chest = prefab.GetComponent(type);
                    Assert.NotNull(chest, prefab.name);
                    var frames = (Sprite[])Field(chest, "openingFrames");
                    Assert.GreaterOrEqual(frames.Length, 5);
                    Assert.IsTrue(frames.All(s => s != null));
                    Assert.AreEqual(frames[0], prefab.GetComponent<SpriteRenderer>().sprite);
                    var table = Field(chest, "lootTable");
                    Assert.NotNull(table);
                    var entries = (IList)Field(table, "items");
                    var rarityById = new System.Collections.Generic.Dictionary<string, int>();
                    foreach (var entry in entries)
                    {
                        var item = Field(entry, "item");
                        if ((bool)Field(entry, "enabled"))
                            rarityById[(string)Field(item, "id")] = Convert.ToInt32(Field(entry, "rarity"));
                    }
                    int min = (int)Field(chest, "minimumItems"), max = (int)Field(chest, "maximumItems");
                    int minGold = (int)Field(chest, "minimumGold"), maxGold = (int)Field(chest, "maximumGold");
                    var totals = new int[3];
                    for (int i = 0; i < 2000; i++)
                    {
                        var items = (IList)type.GetMethod("RollItems").Invoke(chest, null);
                        Assert.That(items.Count, Is.InRange(min, max));
                        foreach (var item in items)
                        {
                            string id = (string)Field(item, "id");
                            Assert.IsTrue(rarityById.ContainsKey(id));
                            Assert.AreEqual(1, Field(item, "count"));
                            totals[rarityById[id]]++; allIds.Add(id);
                        }
                        int gold = (int)type.GetMethod("RollInclusive").Invoke(null, new object[] { minGold, maxGold });
                        Assert.That(gold, Is.InRange(minGold, maxGold));
                    }
                    var weights = new[] { (float)Field(chest, "commonWeight"), (float)Field(chest, "rareWeight"), (float)Field(chest, "epicWeight") };
                    for (int tier = 0; tier < 3; tier++)
                        Assert.That((double)totals[tier] / totals.Sum(), Is.EqualTo(weights[tier] / weights.Sum()).Within(0.025), prefab.name);
                }
                var tableType = Find("ChestLootTable");
                var catalog = AssetDatabase.LoadAssetAtPath("Assets/Resources/Items/ChestLootTable.asset", tableType);
                var entriesInCatalog = ((IList)Field(catalog, "items")).Cast<object>();
                var eligible = entriesInCatalog.Where(e => (bool)Field(e, "enabled")).Select(e => (string)Field(Field(e, "item"), "id")).ToArray();
                CollectionAssert.AreEquivalent(eligible, allIds, "Every eligible item must be reachable.");
                Assert.IsFalse(allIds.Any(id => id.StartsWith("rune_") || new[] { "church_key", "moonstone_shard", "drop_demonboss", "drop_fox", "drop_mechastonegolem", "drop_volcanox" }.Contains(id)));
            }
            finally { UnityEngine.Random.state = state; }
        }

        [Test]
        public void PotionValues_UseHalfOfShopPurchasePrices()
        {
            var prices = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TheLastKnight.Inventory.PotionPrices")).First(t => t != null);
            var definitionType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TheLastKnight.Inventory.ItemDefinition")).First(t => t != null);
            var ids = new[] { "potion_heal", "potion_might", "potion_swiftness", "potion_fortitude", "potion_regeneration", "potion_endurance", "potion_purity", "potion_undying" };
            var buys = new[] { 50, 100, 100, 100, 200, 200, 200, 300 };
            var definitions = Resources.LoadAll("Items/Definitions", definitionType);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.AreEqual(buys[i], prices.GetMethod("GetBuyPrice").Invoke(null, new object[] { ids[i] }));
                Assert.AreEqual(buys[i] / 2, prices.GetMethod("GetSellPrice").Invoke(null, new object[] { ids[i] }));
                var item = definitions.First(d => (string)Field(d, "id") == ids[i]);
                Assert.AreEqual(buys[i] / 2, Field(item, "sellPrice"));
                Assert.AreEqual(buys[i] / 2, Find("ChestLootTable").GetMethod("GetItemValue").Invoke(null, new object[] { item }));
            }
        }

        [Test]
        public void PriceBoundaries_AndBossExclusionsAreEnforced()
        {
            var type = Find("ChestLootTable");
            var classify = type.GetMethod("RarityForValue");
            foreach (var pair in new[] { new[] { 0, 0 }, new[] { 50, 0 }, new[] { 51, 1 }, new[] { 100, 1 }, new[] { 101, 2 } })
                Assert.AreEqual(pair[1], Convert.ToInt32(classify.Invoke(null, new object[] { pair[0] })));
            var original = AssetDatabase.LoadAssetAtPath("Assets/Resources/Items/ChestLootTable.asset", type);
            var clone = UnityEngine.Object.Instantiate(original);
            try
            {
                foreach (var entry in (IList)Field(clone, "items")) entry.GetType().GetField("enabled").SetValue(entry, true);
                foreach (var rarity in Enum.GetValues(Find("ChestItemRarity")))
                    foreach (var item in (IList)type.GetMethod("GetPool").Invoke(clone, new[] { rarity }))
                        Assert.IsFalse((bool)type.GetMethod("IsExcluded").Invoke(null, new[] { item }));
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public void InclusiveRoll_HandlesFixedZeroAndMaximumInteger()
        {
            var method = Find("LootChest").GetMethod("RollInclusive");
            Assert.AreEqual(0, method.Invoke(null, new object[] { 0, 0 }));
            Assert.AreEqual(7, method.Invoke(null, new object[] { 7, 7 }));
            Assert.AreEqual(int.MaxValue, method.Invoke(null, new object[] { int.MaxValue, int.MaxValue }));
        }

        [Test]
        public void LootPool_IgnoresDisabledNullAndDuplicateEntries()
        {
            var type = Find("ChestLootTable");
            var original = AssetDatabase.LoadAssetAtPath("Assets/Resources/Items/ChestLootTable.asset", type);
            var clone = UnityEngine.Object.Instantiate(original);
            try
            {
                var entries = (IList)Field(clone, "items");
                var entry = entries.Cast<object>().First(e => (bool)Field(e, "enabled"));
                entries.Clear(); entries.Add(entry); entries.Add(entry); entries.Add(null);
                var rarity = Field(entry, "rarity");
                var method = type.GetMethod("GetPool");
                Assert.AreEqual(1, ((IList)method.Invoke(clone, new[] { rarity })).Count);
                entry.GetType().GetField("enabled").SetValue(entry, false);
                Assert.AreEqual(0, ((IList)method.Invoke(clone, new[] { rarity })).Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
    }
}
