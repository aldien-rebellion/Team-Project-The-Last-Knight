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
                Assert.AreEqual(15, allIds.Count, "Every eligible item must be reachable.");
            }
            finally { UnityEngine.Random.state = state; }
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
                var entry = entries[0];
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
