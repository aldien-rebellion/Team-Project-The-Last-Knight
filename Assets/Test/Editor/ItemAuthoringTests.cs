using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class ItemAuthoringTests
    {
        [TestCase("earth_spellbook", 10, 10, 0)]
        [TestCase("earth_spellbook", 10, 20, 37)]
        [TestCase("advanced_spellbook", 20, 20, 0)]
        [TestCase("advanced_spellbook", 20, 30, 37)]
        [TestCase("ice_spellbook", 30, 30, 0)]
        [TestCase("ice_spellbook", 30, 40, 37)]
        public void Spellbook_GrantsReferenceLevelThresholdAndPreservesExistingEXP(string itemId, int referenceLevel, int level, int startingEXP)
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
                var item = Find("ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { itemId, 2 });
                var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
                Assert.IsNotNull(icon);
                Assert.That(Mathf.Max(icon.bounds.size.x, icon.bounds.size.y), Is.EqualTo(0.32f).Within(0.001f));
                Assert.IsTrue((bool)Field(item, "isConsumable"));
                ((Delegate)Field(item, "onUse")).DynamicInvoke(player);
                int expected = Mathf.RoundToInt(100f * Mathf.Pow(1.25f, referenceLevel - 1));
                Assert.AreEqual(level == referenceLevel ? level + 1 : level, playerType.GetProperty("Level").GetValue(player));
                Assert.AreEqual(level == referenceLevel ? 0 : startingEXP + expected, playerType.GetProperty("EXP").GetValue(player));
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

        [TestCase("earth_spellbook", 0)]
        [TestCase("advanced_spellbook", 0)]
        [TestCase("ice_spellbook", 0)]
        [TestCase("earth_scroll", 0)]
        [TestCase("fire_scroll", 0)]
        [TestCase("light_scroll", 0)]
        [TestCase("thunder_scroll", 0)]
        [TestCase("gold_pouch", 500)]
        [TestCase("gold_bag", 1000)]
        public void StatusConsumable_MenuShowsUseAndConsumesOneItem(string itemId, int goldAmount)
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
                if (itemId == "earth_scroll" || itemId == "fire_scroll" || itemId == "light_scroll" || itemId == "thunder_scroll")
                {
                    playerType.GetField("_vitality", flags).SetValue(player, 10);
                    playerType.GetField("_strength", flags).SetValue(player, 10);
                    playerType.GetField("_dexterity", flags).SetValue(player, 10);
                    playerType.GetField("_agility", flags).SetValue(player, 10);
                    playerType.GetField("_availableStatPoints", flags).SetValue(player, 0);
                    playerType.GetMethod("RecalculateStats").Invoke(player, new object[] { false });
                }
                playerType.GetField("_currentHP", flags).SetValue(player, 100f);
                var inventory = owner.AddComponent(inventoryType);
                singleton.SetValue(null, inventory);
                var item = Find("ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { itemId, 2 });
                Assert.IsTrue((bool)item.GetType().GetProperty("HasStatusUseMenu").GetValue(item));
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
                int initialGold = (int)playerType.GetProperty("Gold").GetValue(player);
                int initialEXP = (int)playerType.GetProperty("EXP").GetValue(player);
                int initialLevel = (int)playerType.GetProperty("Level").GetValue(player);
                int initialVIT = (int)playerType.GetProperty("VIT").GetValue(player);
                int initialSTR = (int)playerType.GetProperty("STR").GetValue(player);
                int initialDEX = (int)playerType.GetProperty("DEX").GetValue(player);
                int initialAGI = (int)playerType.GetProperty("AGI").GetValue(player);
                float initialAttack = (float)playerType.GetProperty("AttackPower").GetValue(player);
                {
                    var quickSlots = (Array)inventoryType.GetField("_quickSlots", flags).GetValue(inventory);
                    quickSlots.SetValue(item, 0);
                    Assert.IsFalse((bool)inventoryType.GetMethod("CanUseQuickSlot").Invoke(inventory, new object[] { 0, player }));
                    Assert.IsFalse((bool)inventoryType.GetMethod("UseQuickSlot").Invoke(inventory, new object[] { 0, player }));
                    Assert.AreEqual(initialGold, playerType.GetProperty("Gold").GetValue(player));
                    Assert.AreEqual(initialEXP, playerType.GetProperty("EXP").GetValue(player));
                    Assert.AreEqual(initialLevel, playerType.GetProperty("Level").GetValue(player));
                    Assert.AreEqual(initialVIT, playerType.GetProperty("VIT").GetValue(player));
                    Assert.AreEqual(initialSTR, playerType.GetProperty("STR").GetValue(player));
                    Assert.AreEqual(initialDEX, playerType.GetProperty("DEX").GetValue(player));
                    Assert.AreEqual(initialAGI, playerType.GetProperty("AGI").GetValue(player));
                    Assert.AreEqual(2, Field(item, "count"));
                    quickSlots.SetValue(null, 0);
                }
                Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                Assert.AreEqual(1, Field(item, "count"));
                Assert.AreEqual(initialGold + goldAmount, playerType.GetProperty("Gold").GetValue(player));
                if (itemId == "earth_scroll")
                {
                    Assert.AreEqual(initialVIT + 5, playerType.GetProperty("VIT").GetValue(player));
                    Assert.AreEqual(0, playerType.GetProperty("StatPoints").GetValue(player));
                    Assert.AreEqual(150f, playerType.GetProperty("MaxHP").GetValue(player));
                    Assert.AreEqual(105f, playerType.GetProperty("MaxStamina").GetValue(player));
                    Assert.AreEqual(initialEXP, playerType.GetProperty("EXP").GetValue(player));
                    var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
                    Assert.IsNotNull(icon);
                    Assert.That(Mathf.Max(icon.bounds.size.x, icon.bounds.size.y), Is.EqualTo(0.32f).Within(0.001f));
                    Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                    Assert.AreEqual(initialVIT + 10, playerType.GetProperty("VIT").GetValue(player));
                    Assert.IsNull(slots.GetValue(0));
                }
                if (itemId == "fire_scroll")
                {
                    Assert.AreEqual(initialSTR + 5, playerType.GetProperty("STR").GetValue(player));
                    Assert.AreEqual(initialAttack + 7.5f, playerType.GetProperty("AttackPower").GetValue(player));
                    Assert.AreEqual(initialVIT, playerType.GetProperty("VIT").GetValue(player));
                    Assert.AreEqual(0, playerType.GetProperty("StatPoints").GetValue(player));
                    Assert.AreEqual(initialEXP, playerType.GetProperty("EXP").GetValue(player));
                    var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
                    Assert.IsNotNull(icon);
                    Assert.AreEqual("fire scroll_0", icon.name);
                    Assert.That(Mathf.Max(icon.bounds.size.x, icon.bounds.size.y), Is.EqualTo(0.32f).Within(0.001f));
                    Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                    Assert.AreEqual(initialSTR + 10, playerType.GetProperty("STR").GetValue(player));
                    Assert.IsNull(slots.GetValue(0));
                }
                if (itemId == "light_scroll")
                {
                    Assert.AreEqual(initialDEX + 5, playerType.GetProperty("DEX").GetValue(player));
                    Assert.AreEqual(7.5f, playerType.GetProperty("CriticalChance").GetValue(player));
                    Assert.AreEqual(0, playerType.GetProperty("StatPoints").GetValue(player));
                    Assert.AreEqual(initialEXP, playerType.GetProperty("EXP").GetValue(player));
                    var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
                    Assert.IsNotNull(icon);
                    Assert.That(Mathf.Max(icon.bounds.size.x, icon.bounds.size.y), Is.EqualTo(0.32f).Within(0.001f));
                    playerType.GetField("_dexterity", flags).SetValue(player, 198);
                    Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                    Assert.AreEqual(203, playerType.GetProperty("DEX").GetValue(player));
                    Assert.IsNull(slots.GetValue(0));
                    item.GetType().GetField("count").SetValue(item, 1);
                    slots.SetValue(item, 0);
                    Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                    Assert.AreEqual(208, playerType.GetProperty("DEX").GetValue(player));
                    Assert.IsNull(slots.GetValue(0));
                    playerType.GetMethod("RecalculateStats").Invoke(player, new object[] { false });
                    Assert.AreEqual(208, playerType.GetProperty("DEX").GetValue(player));
                    var save = Activator.CreateInstance(FindType("TheLastKnight.Core.PlayerSaveData"));
                    playerType.GetMethod("Capture").Invoke(player, new[] { save });
                    playerType.GetField("_dexterity", flags).SetValue(player, 10);
                    playerType.GetMethod("Restore").Invoke(player, new[] { save });
                    Assert.AreEqual(208, playerType.GetProperty("DEX").GetValue(player));
                }
                if (itemId == "thunder_scroll")
                {
                    Assert.AreEqual(initialAGI + 5, playerType.GetProperty("AGI").GetValue(player));
                    Assert.That((float)playerType.GetProperty("AttackSpeedMultiplier").GetValue(player), Is.EqualTo(1.02f).Within(0.001f));
                    Assert.AreEqual(initialSTR, playerType.GetProperty("STR").GetValue(player));
                    Assert.AreEqual(initialDEX, playerType.GetProperty("DEX").GetValue(player));
                    Assert.AreEqual(initialVIT, playerType.GetProperty("VIT").GetValue(player));
                    Assert.AreEqual(initialEXP, playerType.GetProperty("EXP").GetValue(player));
                    var icon = (Sprite)item.GetType().GetProperty("Icon").GetValue(item);
                    Assert.IsNotNull(icon);
                    Assert.That(Mathf.Max(icon.bounds.size.x, icon.bounds.size.y), Is.EqualTo(0.32f).Within(0.001f));
                    Assert.IsTrue((bool)inventoryType.GetMethod("UseSlot").Invoke(inventory, new object[] { slotType, 0, player }));
                    Assert.AreEqual(initialAGI + 10, playerType.GetProperty("AGI").GetValue(player));
                    Assert.IsNull(slots.GetValue(0));
                }
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
                "potion_undying",
                "potion_crit_damage"
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
