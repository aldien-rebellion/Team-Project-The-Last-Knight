using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class ItemAuthoringTests
    {
        [TestCase(10, 0)]
        [TestCase(20, 37)]
        public void EarthSpellbook_GrantsLevelTenThresholdAndPreservesExistingEXP(int level, int startingEXP)
        {
            var playerType = FindType("TheLastKnight.Stats.PlayerStats");
            var template = ScriptableObject.CreateInstance(FindType("TheLastKnight.Stats.CharacterStatsSO"));
            var go = new GameObject("EarthSpellbookTest");
            go.SetActive(false);
            try
            {
                template.GetType().GetField("baseExpNeeded").SetValue(template, 100);
                template.GetType().GetField("expGrowthMultiplier").SetValue(template, 1.25f);
                var player = go.AddComponent(playerType);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                playerType.GetField("_statsTemplate", flags).SetValue(player, template);
                playerType.GetField("_currentLevel", flags).SetValue(player, level);
                playerType.GetField("_currentEXP", flags).SetValue(player, startingEXP);
                var item = Find("ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { "earth_spellbook", 2 });
                Assert.IsNotNull(item.GetType().GetProperty("Icon").GetValue(item));
                Assert.IsTrue((bool)Field(item, "isConsumable"));
                ((Delegate)Field(item, "onUse")).DynamicInvoke(player);
                int expected = Mathf.RoundToInt(100f * Mathf.Pow(1.25f, 9));
                Assert.AreEqual(745, expected);
                Assert.AreEqual(level == 10 ? 11 : 20, playerType.GetProperty("Level").GetValue(player));
                Assert.AreEqual(level == 10 ? 0 : startingEXP + expected, playerType.GetProperty("EXP").GetValue(player));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TheLastKnight.Inventory." + name)).First(t => t != null);
        private static object Field(object item, string name) => item.GetType().GetField(name).GetValue(item);

        [Test]
        public void EarthSpellbook_MenuShowsUseAndConsumptionRemovesOneBook()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var inventoryType = Find("InventoryManager");
            var singleton = inventoryType.GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var previousInventory = singleton.GetValue(null);
            var owner = new GameObject("EarthSpellbookMenuTest");
            owner.SetActive(false);
            var playerObject = new GameObject("EarthSpellbookMenuPlayer");
            var canvasObject = new GameObject("EarthSpellbookMenuCanvas", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            var cameraObject = new GameObject("EarthSpellbookMenuCamera", typeof(Camera));
            var texture = new RenderTexture(640, 360, 24);
            var previousRT = RenderTexture.active;
            Texture2D image = null;
            try
            {
                var playerType = FindType("TheLastKnight.Stats.PlayerStats");
                var player = playerObject.AddComponent(playerType);
                playerType.GetField("_currentHP", flags).SetValue(player, 100f);
                var inventory = owner.AddComponent(inventoryType);
                singleton.SetValue(null, inventory);
                var item = Find("ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { "earth_spellbook", 2 });
                var slots = (Array)inventoryType.GetField("_inventorySlots", flags).GetValue(inventory);
                slots.SetValue(item, 0);
                var uiType = FindType("TheLastKnight.UI.CharacterStatusUI");
                var ui = owner.AddComponent(uiType);
                uiType.GetField("_canvasObject", flags).SetValue(ui, canvasObject);
                uiType.GetField("_cachedStats", flags).SetValue(ui, player);
                uiType.GetField("_isOpen", flags).SetValue(ui, true);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.18f, 0.22f);
                camera.targetTexture = texture;
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                var slotType = Enum.Parse(Find("SlotType"), "Inventory");
                uiType.GetMethod("ShowItemUseMenu").Invoke(ui, new object[] { slotType, 0, new Vector2(320, 180) });
                var use = canvasObject.transform.Find("ItemUseMenu/Use");
                Assert.IsNotNull(use);
                Assert.IsTrue(use.GetComponent<UnityEngine.UI.Button>().interactable);
                Assert.AreEqual(2, Field(item, "count"), "Opening the menu must not split or consume the stack.");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(640, 360, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
                image.Apply();
                System.IO.Directory.CreateDirectory("Temp");
                System.IO.File.WriteAllBytes("Temp/EarthSpellbookMenu.png", image.EncodeToPNG());
                Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                Assert.AreEqual(1, Field(item, "count"));
            }
            finally
            {
                singleton.SetValue(null, previousInventory);
                RenderTexture.active = previousRT;
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void AuthoredCatalog_PreservesLegacyIconsAndCallbacks()
        {
            var registry = Find("ItemRegistry");
            var definitions = Resources.LoadAll("Items/Definitions", Find("ItemDefinition"));
            Assert.GreaterOrEqual(definitions.Length, 8);
            foreach (string id in (System.Collections.Generic.IEnumerable<string>)registry.GetProperty("LegacyIds").GetValue(null))
            {
                var legacy = registry.GetMethod("CreateLegacyItem").Invoke(null, new object[] { id });
                var item = registry.GetMethod("CreateItem").Invoke(null, new object[] { id, 3 });
                Assert.AreEqual(Field(legacy, "name"), Field(item, "name"), id);
                var icon = item.GetType().GetProperty("Icon").GetValue(item);
                var legacyIcon = legacy.GetType().GetProperty("Icon").GetValue(legacy);
                if (legacyIcon != null) Assert.AreEqual(legacyIcon, icon, id);
                Assert.AreEqual(Field(legacy, "onUse") != null, Field(item, "onUse") != null, id);
            }
        }

        [Test]
        public void NewDefinition_UsesDirectSpriteAndCreatesIndependentStacks()
        {
            var type = Find("ItemDefinition");
            var definition = ScriptableObject.CreateInstance(type);
            try
            {
                type.GetField("id").SetValue(definition, "test_custom");
                type.GetField("maxStack").SetValue(definition, 5);
                type.GetField("useEffect").SetValue(definition, Enum.Parse(Find("ItemUseEffect"), "Heal"));
                var sprite = Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean");
                Assert.IsNotNull(sprite);
                type.GetField("icon").SetValue(definition, sprite);
                var first = type.GetMethod("CreateItem").Invoke(definition, new object[] { 99, null });
                var second = type.GetMethod("CreateItem").Invoke(definition, new object[] { 2, null });
                Assert.AreEqual(5, Field(first, "count"));
                Assert.AreEqual(2, Field(second, "count"));
                Assert.AreSame(sprite, first.GetType().GetProperty("Icon").GetValue(first));
                Assert.IsNotNull(Field(first, "onUse"));
                Assert.IsNotNull(Field(first, "canUse"));
                var clone = first.GetType().GetMethod("Clone").Invoke(first, new object[] { 1 });
                Assert.AreSame(sprite, clone.GetType().GetProperty("Icon").GetValue(clone));
                Assert.AreSame(Field(first, "canUse"), Field(clone, "canUse"));
                Assert.AreEqual(5, Field(first, "count"));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void AuthoredPotions_AllHaveValidDefinitionsAndIcons()
        {
            var registry = Find("ItemRegistry");
            string[] potionIds = new[]
            {
                "potion_heal",
                "potion_swiftness",
                "potion_endurance",
                "potion_purity",
                "potion_regeneration",
                "potion_might",
                "potion_fortitude",
                "potion_undying"
            };

            foreach (var id in potionIds)
            {
                var item = registry.GetMethod("CreateItem").Invoke(null, new object[] { id, 1 });
                Assert.IsNotNull(item, "Item must exist: " + id);
                Assert.AreEqual("Potion", Field(item, "typeName"), "TypeName must be Potion for: " + id);
                var icon = item.GetType().GetProperty("Icon").GetValue(item);
                Assert.IsNotNull(icon, "Icon must be loaded for: " + id);
                Assert.IsNotNull(Field(item, "onUse"), "onUse must be configured for: " + id);
            }
        }

        [Test]
        public void MoonstoneShard_HasCorrectIconAndMetadata()
        {
            var registry = Find("ItemRegistry");
            var item = registry.GetMethod("CreateItem").Invoke(null, new object[] { "moonstone_shard", 1 });
            Assert.IsNotNull(item, "Moonstone shard must exist");
            Assert.AreEqual("Moonstone Shard", Field(item, "name"));
            Assert.AreEqual("Material", Field(item, "typeName"));
            var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
            Assert.IsNotNull(icon, "Icon must not be null");
            Assert.That(icon.name, Does.StartWith("crystal_black1"));
        }

        private static Type FindType(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

        [Test]
        public void MoonstoneKeeper_DropsMoonstoneShardOnDeath()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var rewardType = FindType("TheLastKnight.Environment.EnemyProgressionReward");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("MoonstoneKeeper");
            var pickups = new System.Collections.Generic.List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var reward = go.AddComponent(rewardType);
                rewardType.GetField("moonstoneShard").SetValue(reward, true);

                var awardMethod = rewardType.GetMethod("Award",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awardMethod.Invoke(reward, null);

                var spawned = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                object shardPickup = null;
                foreach (var p in spawned)
                {
                    var comp = (Component)p;
                    pickups.Add(comp.gameObject);
                    var itemData = pickupType.GetProperty("ItemData").GetValue(comp);
                    if (itemData != null && (string)itemData.GetType().GetField("id").GetValue(itemData) == "moonstone_shard")
                    {
                        shardPickup = comp;
                    }
                }

                Assert.IsNotNull(shardPickup, "MoonstoneKeeper must drop moonstone_shard");
                var droppedData = pickupType.GetProperty("ItemData").GetValue(shardPickup);
                Assert.AreEqual(1, droppedData.GetType().GetField("count").GetValue(droppedData));
                var icon = (Sprite)droppedData.GetType().GetProperty("Icon").GetValue(droppedData);
                Assert.IsNotNull(icon);
                Assert.That(icon.name, Does.StartWith("crystal_black1"));
            }
            finally
            {
                foreach (var p in pickups)
                {
                    if (p != null) UnityEngine.Object.DestroyImmediate(p);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
