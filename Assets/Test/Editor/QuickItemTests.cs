using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace TheLastKnight.Tests
{
    public class QuickItemTests
    {
        private GameObject _inventoryObject, _quickObject;
        private Component _inventory, _quick;
        private Type _registry, _slotType, _saveType;
        private object Bag => Enum.Parse(_slotType, "Inventory");
        private object Quick => Enum.Parse(_slotType, "QuickSlot");
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private object Item(string id, int count) => _registry.GetMethod("CreateItem").Invoke(null, new object[] { id, count });
        private object Slot(object type, int index) => Call(_inventory, "GetSlot", type, index);
        private int Count(object item) => item == null ? 0 : (int)Field(item, "count");
        private object Held => _inventory.GetType().GetProperty("CursorHeldItem").GetValue(_inventory);
        private void Set(object type, int index, string id, int count) => Call(_inventory, "SetSlot", type, index, Item(id, count));
        private void Left(object type, int index, bool shift = false) => Call(_inventory, "HandleLeftClick", type, index, shift, null);
        private void Right(object type, int index) => Call(_inventory, "HandleRightClick", type, index, null);

        [SetUp]
        public void SetUp()
        {
            _registry = RuntimeType("TheLastKnight.Inventory.ItemRegistry");
            _slotType = RuntimeType("TheLastKnight.Inventory.SlotType");
            _saveType = RuntimeType("TheLastKnight.Core.PlayerSaveData");
            _inventoryObject = new GameObject("Test_Inventory");
            _inventory = _inventoryObject.AddComponent(RuntimeType("TheLastKnight.Inventory.InventoryManager"));
            Call(_inventory, "Awake");
            _quickObject = new GameObject("Test_QuickItems");
            _quick = _quickObject.AddComponent(RuntimeType("TheLastKnight.Core.QuickItemManager"));
            Call(_quick, "Awake");
        }
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_quickObject);
            UnityEngine.Object.DestroyImmediate(_inventoryObject);
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator RealWindow_InputModule_DragAndClickMovePotions()
        {
            yield return new UnityEngine.TestTools.EnterPlayMode();
            yield return (System.Collections.IEnumerator)RuntimeType("InventoryPointerCheck").GetMethod("Run").Invoke(null, null);
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator PointerDrag_QuickPotionToBag_UsesRaycastTargetAndConservesStack()
        {

            var cameraObject = new GameObject("Test_DragCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.enabled = false;
            var renderTexture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = renderTexture;
            var canvasObject = new GameObject("Test_DragCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            var eventObject = new GameObject("Test_DragEvents", typeof(EventSystem));
            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 30000;
                var slotUiType = RuntimeType("TheLastKnight.Inventory.InventorySlotUI");
                var source = new GameObject("Source", typeof(RectTransform), typeof(Image));
                var target = new GameObject("Target", typeof(RectTransform), typeof(Image));
                source.transform.SetParent(canvasObject.transform, false);
                target.transform.SetParent(canvasObject.transform, false);
                ((RectTransform)source.transform).anchoredPosition = new Vector2(-100, 0);
                ((RectTransform)target.transform).anchoredPosition = new Vector2(100, 0);
                var sourceUi = source.AddComponent(slotUiType);
                var targetUi = target.AddComponent(slotUiType);
                slotUiType.GetField("slotType").SetValue(sourceUi, Quick);
                slotUiType.GetField("slotType").SetValue(targetUi, Bag);

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var data = new PointerEventData(eventObject.GetComponent<EventSystem>())
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(camera, target.transform.position),
                    eligibleForClick = true
                };
                Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(source), Is.EqualTo(source));
                ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
                Assert.That(Slot(Quick, 0), Is.Null);
                Assert.That(Count(Held), Is.EqualTo(3));
                ExecuteEvents.Execute(source, data, ExecuteEvents.dragHandler);
                var hits = new List<RaycastResult>();
                canvasObject.GetComponent<GraphicRaycaster>().Raycast(data, hits);
                Assert.That(hits.Count, Is.GreaterThan(0), "depth=" + target.GetComponent<Image>().depth + " screen=" + data.position + " rect=" + ((RectTransform)target.transform).rect);
                Assert.That(hits[0].gameObject, Is.EqualTo(target));
                data.pointerCurrentRaycast = hits[0];
                ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
                Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(3));
                Assert.That(Count(Held), Is.EqualTo(0));
                Assert.That(data.eligibleForClick, Is.False);
                Set(Quick, 0, "potion_heal", 3);
                Set(Bag, 0, "potion_heal", 64);
                ExecuteEvents.Execute(source, data, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(source, data, ExecuteEvents.endDragHandler);
                Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(64));
                Assert.That(Count(Held), Is.EqualTo(3));
                ExecuteEvents.Execute(source, data, ExecuteEvents.pointerClickHandler);
                Assert.That(Count(Slot(Quick, 0)), Is.EqualTo(3));
                Assert.That(Held, Is.Null);
            }
            finally
            {
                camera.targetTexture = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(eventObject);
            }
        }
        [Test]
        public void ChurchKey_ConsumesOneFromBagQuickAndCursor_AndSurvivesSaveLoad()
        {
            Set(Bag, 0, "church_key", 1);
            Set(Bag, 1, "church_key", 1);
            Set(Quick, 1, "church_key", 1);
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(true));
            Assert.IsNull(Slot(Bag, 0));
            var save = Activator.CreateInstance(_saveType);
            Call(_inventory, "SaveTo", save);
            Call(_inventory, "LoadFrom", save, null);
            Assert.IsNull(Slot(Bag, 0));
            Assert.That(Count(Slot(Bag, 1)), Is.EqualTo(1));
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(true));
            Assert.IsNull(Slot(Bag, 1));
            Left(Quick, 1);
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(true));
            Assert.IsNull(Held);
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(false));
            Assert.That(Count(Slot(Quick, 0)), Is.EqualTo(3));
        }

        [Test]
        public void LegacyChurchKey_MigratesOnce_AndAlreadyOpenedChestDoesNotGrantKey()
        {
            var save = Activator.CreateInstance(_saveType);
            _saveType.GetField("churchKey").SetValue(save, true);
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Field(save, "churchKey"), Is.EqualTo(false));
            Call(_inventory, "SaveTo", save);
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(true));
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(false));
            save = Activator.CreateInstance(_saveType);
            _saveType.GetField("churchKey").SetValue(save, true);
            ((bool[])Field(save, "runes"))[0] = true;
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Call(_inventory, "TryConsumeItem", "church_key"), Is.EqualTo(false));
            Assert.That(Field(save, "churchKey"), Is.EqualTo(false));
        }

        [Test]
        public void KeeperKey_DropsRequestedSprite_ChestConsumesOnlyOnFirstOpening()
        {
            var gmType = RuntimeType("TheLastKnight.Core.GameManager");
            var runeType = RuntimeType("TheLastKnight.Environment.DemonRuneManager");
            var gmInstance = gmType.GetProperty("Instance");
            var runeInstance = runeType.GetProperty("Instance");
            var oldGm = gmInstance.GetValue(null);
            var oldRunes = runeInstance.GetValue(null);
            var go = new GameObject("Test_KeyContext");
            go.SetActive(false);
            Component pickup = null;
            try
            {
                var gm = go.AddComponent(gmType);
                gmInstance.SetValue(null, gm);
                var runes = go.AddComponent(runeType);
                runeInstance.SetValue(null, runes);
                var chest = go.AddComponent(RuntimeType("TheLastKnight.Environment.RuneChest"));
                var reward = go.AddComponent(RuntimeType("TheLastKnight.Environment.EnemyProgressionReward"));
                reward.GetType().GetField("churchKey").SetValue(reward, true);
                Call(reward, "Award");
                pickup = UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Inventory.WorldItemPickup"), FindObjectsSortMode.None)
                    .Cast<Component>().Single(p => p.name == "Pickup_church_key");
                var dropped = pickup.GetType().GetProperty("ItemData").GetValue(pickup);
                Assert.That(Count(dropped), Is.EqualTo(1));
                Assert.That(pickup.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Key 13 - GOLD - frame0026_0"));
                var state = gmType.GetProperty("State").GetValue(gm);
                Assert.That(Field(state, "churchKey"), Is.EqualTo(false));
                Call(chest, "Interact");
                Assert.That(((bool[])Field(state, "runes"))[0], Is.False, "A world drop is not an owned key");
                Set(Bag, 0, "church_key", 1);
                Set(Bag, 1, "church_key", 1);
                Call(chest, "Interact");
                Assert.That(Field(state, "pentagramRuneChestOpened"), Is.True);
                Assert.IsNull(Slot(Bag, 0));
                Assert.That(Count(Slot(Bag, 1)), Is.EqualTo(1));
                Call(chest, "Interact");
                Assert.That(Count(Slot(Bag, 1)), Is.EqualTo(1), "Reopening must not consume another key");
            }
            finally
            {
                if (pickup != null) UnityEngine.Object.DestroyImmediate(pickup.gameObject);
                UnityEngine.Object.DestroyImmediate(go);
                gmInstance.SetValue(null, oldGm);
                runeInstance.SetValue(null, oldRunes);
                foreach (var popup in UnityEngine.Object.FindObjectsByType(RuntimeType("TheLastKnight.Combat.FloatingCombatText"), FindObjectsSortMode.None))
                    UnityEngine.Object.DestroyImmediate(((Component)popup).gameObject);
            }
        }

        [Test]
        public void DefaultInventory_IsEmpty_WithThreePotionsAndStackLimit64()
        {
            for (int i = 0; i < 24; i++) Assert.That(Slot(Bag, i), Is.Null);
            Assert.That(Count(Slot(Quick, 0)), Is.EqualTo(3));
            for (int i = 1; i < 5; i++) Assert.That(Slot(Quick, i), Is.Null);
            Assert.That(Field(Call(_quick, "GetSlot", 0), "maxCount"), Is.EqualTo(64));
        }
        [Test]
        public void SplitOddStack_DepositOne_AndMerge_ConserveItems()
        {
            Set(Bag, 0, "potion_might", 5);
            Right(Bag, 0);
            Assert.That(Count(Held), Is.EqualTo(3));
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(2));
            Right(Bag, 1);
            Assert.That(Count(Slot(Bag, 1)), Is.EqualTo(1));
            Left(Bag, 0);
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(4));
            Assert.That(Held, Is.Null);
        }
        [Test]
        public void FullStack_DoesNotSwapWithCursor_AndOverflowStaysHeld()
        {
            Set(Bag, 0, "potion_might", 64);
            Set(Bag, 1, "potion_might", 5);
            Left(Bag, 1); Left(Bag, 0);
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(64));
            Assert.That(Count(Held), Is.EqualTo(5));
            Set(Bag, 0, "potion_might", 62); Left(Bag, 0);
            Assert.That(Count(Held), Is.EqualTo(3));
            Right(Bag, 0);
            Assert.That(Count(Held), Is.EqualTo(3));
        }
        [Test]
        public void DifferentItems_LeftSwaps_RightDoesNothing()
        {
            Set(Bag, 0, "potion_might", 2); Set(Bag, 1, "gold_pouch", 1);
            Left(Bag, 0); Right(Bag, 1);
            Assert.That(Field(Held, "id"), Is.EqualTo("potion_might"));
            Left(Bag, 1);
            Assert.That(Field(Held, "id"), Is.EqualTo("gold_pouch"));
            Assert.That(Field(Slot(Bag, 1), "id"), Is.EqualTo("potion_might"));
        }
        [Test]
        public void QuickMove_PartialCapacity_ConservesRemainderAndSyncsHud()
        {
            Set(Quick, 0, "potion_heal", 63);
            for (int i = 1; i < 5; i++) Set(Quick, i, "church_key", 1);
            Set(Bag, 0, "potion_heal", 5); Left(Bag, 0, true);
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(4));
            Assert.That(Count(Call(_quick, "GetSlot", 0)), Is.EqualTo(64));
            Left(Quick, 0, true);
            Assert.That(Slot(Quick, 0), Is.Null);
            Assert.That(Count(Slot(Bag, 0)) + Count(Slot(Bag, 1)), Is.EqualTo(68));
        }
        [Test]
        public void SaveLoad_PreservesEmptyInventory_WithoutFreePotions()
        {
            Call(_inventory, "SetSlot", Quick, 0, null);
            var save = Activator.CreateInstance(_saveType);
            Call(_inventory, "SaveTo", save);
            Call(_inventory, "InitializeDefaultInventory", new object[] { null });
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Slot(Quick, 0), Is.Null);
        }
        [Test]
        public void SaveLoad_PreservesCursorStack_AndCloseReturnsToBag()
        {
            Set(Bag, 3, "potion_might", 9); Left(Bag, 3);
            var save = Activator.CreateInstance(_saveType);
            Call(_inventory, "SaveTo", save);
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(9));
            Assert.That(Held, Is.Null);
            Left(Bag, 0); Call(_inventory, "Close");
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(9));
            Assert.That(Held, Is.Null);
        }
        [Test]
        public void LegacySave_WithZeroPotions_RemainsEmpty()
        {
            var save = Activator.CreateInstance(_saveType);
            _saveType.GetField("potions").SetValue(save, 0);
            Call(_inventory, "LoadFrom", save, null);
            Assert.That(Slot(Quick, 0), Is.Null);
        }
        [Test]
        public void InvalidSlot_DoesNotLoseHeldStack()
        {
            Left(Quick, 0); Left(Bag, -1); Right(Bag, 24);
            Assert.That(Count(Held), Is.EqualTo(3));
        }
        [Test]
        public void QuickPriority_SkipsEmptySlots_AndPotionCompletionConsumesDisplayedStack()
        {
            var playerObject = new GameObject("Test_QuickPriorityPlayer");
            playerObject.SetActive(false);
            try
            {
                var player = playerObject.AddComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                var type = player.GetType();
                type.GetField("<MaxHP>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 500f);
                type.GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 50f);
                for (int i = 0; i < 5; i++) Call(_inventory, "SetSlot", Quick, i, null);
                Set(Quick, 4, "potion_heal", 2);
                Assert.That(Call(_quick, "GetActiveSlotIndex"), Is.EqualTo(4));
                Assert.That(Field(Call(_quick, "GetActiveItem"), "id"), Is.EqualTo("potion_heal"));
                Assert.That(Call(player, "CompletePotionDrink"), Is.EqualTo(true));
                Assert.That(Count(Slot(Quick, 4)), Is.EqualTo(1));
                Set(Quick, 0, "potion_heal", 1);
                Assert.That(Call(_quick, "GetActiveSlotIndex"), Is.EqualTo(0));
                Assert.That(Call(player, "CompletePotionDrink"), Is.EqualTo(true));
                int next = (int)Call(_quick, "GetActiveSlotIndex");
                Assert.That(next, Is.GreaterThan(0), "Must skip the empty slots after the first stack runs out");
                Assert.That(Call(player, "CompletePotionDrink"), Is.EqualTo(true));
                Assert.That(Call(_quick, "GetActiveSlotIndex"), Is.EqualTo(-1));
                Assert.That(Call(_quick, "GetActiveItem"), Is.Null);
                Assert.That(Call(player, "CompletePotionDrink"), Is.EqualTo(false));
                Assert.That(type.GetProperty("CurrentHP").GetValue(player), Is.EqualTo(500f));
            }
            finally { UnityEngine.Object.DestroyImmediate(playerObject); }
        }

        [Test]
        public void Consumption_UpdatesInventoryHudAndStats_WithoutUnintendedEffects()
        {
            var playerObject = new GameObject("Test_ItemPlayer");
            playerObject.SetActive(false);
            try
            {
                var player = playerObject.AddComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                var type = player.GetType();
                Action<string, object> field = (name, value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, value);
                Func<string, object> property = name => type.GetProperty(name).GetValue(player);
                field("<MaxHP>k__BackingField", 200f);
                field("_currentHP", 50f);
                field("_currentStamina", 0f);
                Set(Quick, 0, "potion_heal", 1);
                Set(Quick, 1, "gold_pouch", 1);
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(true));
                Assert.That(property("CurrentHP"), Is.EqualTo(160f));
                Assert.That(property("HealingPotions"), Is.EqualTo(0));
                Assert.That(Field(Slot(Quick, 0), "id"), Is.EqualTo("gold_pouch"));
                Assert.That(Field(Call(_quick, "GetActiveItem"), "id"), Is.EqualTo("gold_pouch"));
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(false));
                Assert.That(property("Gold"), Is.EqualTo(0));
                Assert.That(Call(_inventory, "UseSlot", Quick, 0, player), Is.EqualTo(true));
                Assert.That(property("Gold"), Is.EqualTo(500));
                field("_baseAttackPower", 100f);
                int strength = (int)property("STR");
                Set(Quick, 0, "potion_might", 2); Call(_quick, "UseSlot", 0, player);
                Assert.That(property("AttackPower"), Is.EqualTo(125f));
                Call(_quick, "UseSlot", 0, player);
                Assert.That(property("AttackPower"), Is.EqualTo(125f), "Buff must not stack");
                field("_mightExpiresAt", Time.time - 1f);
                Assert.That(property("AttackPower"), Is.EqualTo(100f));
                Assert.That(property("STR"), Is.EqualTo(strength));
                Set(Quick, 0, "church_key", 1);
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(playerObject); }
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator CritDamagePotion_BuyAndDrinkThroughQuickSlot()
        {
            var playerObject = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            var shopObject = new GameObject("Test_CritDamageShop");
            var cameraObject = new GameObject("Test_CritDamageShopCamera", typeof(Camera));
            var renderTexture = new RenderTexture(1280, 720, 24);
            var priorRenderTexture = RenderTexture.active;
            Component shop = null;
            try
            {
                var player = playerObject.GetComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                Call(playerObject.GetComponent(RuntimeType("TheLastKnight.Player.PlayerController")), "Awake");
                Call(player, "Awake");
                Call(player, "AddGold", 600);
                shop = shopObject.AddComponent(RuntimeType("TheLastKnight.UI.ShopUI"));
                Call(shop, "Awake");
                var catalog = (System.Collections.IList)shop.GetType().GetProperty("Catalog").GetValue(shop);
                var potion = catalog.Cast<object>().Single(i => (string)Field(i, "id") == "potion_crit_damage");
                Call(shop, "Open");
                var panel = (GameObject)shop.GetType().GetField("_panel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shop);
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ItemRow_potion_crit_damage"), Is.True);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(30000, 30000, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                camera.cullingMask = ~0;
                camera.targetTexture = renderTexture;
                var canvas = panel.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                yield return null;
                panel.GetComponentsInChildren<ScrollRect>().First(s => s.content.GetComponentsInChildren<Transform>().Any(t => t.name == "ItemRow_potion_crit_damage")).verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = renderTexture;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    string previewPath = System.IO.Path.Combine(Application.dataPath, "../Temp/CritDamagePotionShop.png");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(previewPath));
                    System.IO.File.WriteAllBytes(previewPath, image.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                Call(shop, "BuyItem", potion);
                Assert.That(player.GetType().GetProperty("Gold").GetValue(player), Is.EqualTo(300));
                int purchasedQuickIndex = Enumerable.Range(0, 5).Single(i => Slot(Quick, i) != null && (string)Field(Slot(Quick, i), "id") == "potion_crit_damage");
                Assert.That(Count(Slot(Quick, purchasedQuickIndex)), Is.EqualTo(1));
                Call(shop, "Close");
                Left(Quick, purchasedQuickIndex, true);
                int bagIndex = Enumerable.Range(0, 24).Single(i => Slot(Bag, i) != null && (string)Field(Slot(Bag, i), "id") == "potion_crit_damage");
                Assert.That(Count(Slot(Bag, bagIndex)), Is.EqualTo(1));
                Left(Bag, bagIndex, true);
                int quickIndex = Enumerable.Range(0, 5).Single(i => Slot(Quick, i) != null && (string)Field(Slot(Quick, i), "id") == "potion_crit_damage");
                Assert.That(Call(_quick, "UseSlot", quickIndex, player), Is.EqualTo(true));
                Assert.That(player.GetType().GetProperty("HasCritDamageBuff").GetValue(player), Is.EqualTo(true));
                Assert.That(Call(_inventory, "CountItem", "potion_crit_damage"), Is.EqualTo(0));
            }
            finally
            {
                RenderTexture.active = priorRenderTexture;
                if (shop != null) Call(shop, "Close");
                UnityEngine.Object.DestroyImmediate(shopObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }
        [Test]
        public void PlayerDrop_WaitsForLandingAndOwnerSeparationBeforePickup()
        {
            var ownerObject = new GameObject("Test_DropOwner");
            var dropObject = new GameObject("Test_OwnerDrop");
            try
            {
                ownerObject.SetActive(false);
                var owner = ownerObject.AddComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                ownerObject.AddComponent<BoxCollider2D>();
                ownerObject.SetActive(true);
                var pickup = dropObject.AddComponent(RuntimeType("TheLastKnight.Inventory.WorldItemPickup"));
                Call(pickup, "Initialize", Item("potion_might", 1), 0f);
                Call(pickup, "ConfigurePlayerDrop", owner);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = pickup.GetType();
                Func<bool> locked = () => (bool)type.GetField("_waitForOwnerSeparation", flags).GetValue(pickup);
                Assert.That(((Vector2)type.GetField("_velocity", flags).GetValue(pickup)).x, Is.GreaterThan(4f));
                ownerObject.transform.position = Vector3.right * 10;
                Physics2D.SyncTransforms();
                Call(pickup, "UpdateOwnerSeparation");
                Assert.That(locked(), Is.True, "Airborne items must not re-arm");
                type.GetField("_isSettled", flags).SetValue(pickup, true);
                ownerObject.transform.position = Vector3.zero;
                Physics2D.SyncTransforms();
                Call(pickup, "UpdateOwnerSeparation");
                Assert.That(locked(), Is.True, "Landing on the owner must not collect the drop");
                ownerObject.transform.position = Vector3.right * 10;
                Physics2D.SyncTransforms();
                Call(pickup, "UpdateOwnerSeparation");
                Assert.That(locked(), Is.False, "Leaving the pickup range must allow returning to collect it");
                ownerObject.transform.position = Vector3.zero;
                Physics2D.SyncTransforms();
                Call(pickup, "UpdateOwnerSeparation");
                Assert.That(locked(), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dropObject);
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void WorldPickup_FullInventoryExplainsFailureAndAcceptsAvailableStackSpace()
        {
            var playerObject = new GameObject("Test_FullBagPlayer");
            var dropObject = new GameObject("Test_FullBagPickup");
            var popupType = RuntimeType("TheLastKnight.Combat.FloatingCombatText");
            var existingPopups = new HashSet<UnityEngine.Object>(UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None));
            var pickupType = RuntimeType("TheLastKnight.Inventory.WorldItemPickup");
            var cooldown = pickupType.GetField("_nextInventoryFullNoticeTime", BindingFlags.Static | BindingFlags.NonPublic);
            float originalCooldown = (float)cooldown.GetValue(null);
            try
            {
                for (int i = 0; i < 24; i++) Set(Bag, i, "church_key", 1);
                for (int i = 0; i < 5; i++) Set(Quick, i, "church_key", 1);
                var player = playerObject.AddComponent(RuntimeType("TheLastKnight.Stats.PlayerStats"));
                player.GetType().GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, 100f);
                var body = playerObject.AddComponent<BoxCollider2D>();
                var pickup = dropObject.AddComponent(pickupType);
                Call(pickup, "Initialize", Item("potion_undying", 2), 0f);
                pickupType.GetField("_spawnTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pickup, Time.time - 1f);
                cooldown.SetValue(null, Time.unscaledTime - 1f);
                Call(pickup, "TryPickup", body);
                Assert.That(Count(pickupType.GetProperty("ItemData").GetValue(pickup)), Is.EqualTo(2), "Full bags must not destroy unaccepted loot.");
                var popups = UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None).Except(existingPopups).Cast<Component>().ToArray();
                Assert.That(popups.Count(popup => popup.GetComponent<TextMesh>().text == "กระเป๋าเต็ม"), Is.EqualTo(1));
                Call(pickup, "TryPickup", body);
                Assert.That(UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None).Except(existingPopups).Count(), Is.EqualTo(1),
                    "Repeated trigger callbacks must not spam full-bag notices.");
                Set(Bag, 0, "potion_undying", 63);
                Call(pickup, "TryPickup", body);
                Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(64), "Matching stacks can accept loot even with every slot occupied.");
                Assert.That(Count(pickupType.GetProperty("ItemData").GetValue(pickup)), Is.EqualTo(1), "Only the unaccepted remainder stays on the floor.");
                Set(Bag, 0, "potion_undying", 62);
                Call(pickup, "Initialize", Item("potion_undying", 3), 0f);
                pickupType.GetField("_spawnTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pickup, Time.time - 1f);
                Call(pickup, "TryPickup", body);
                Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(64));
                Assert.That(Count(pickupType.GetProperty("ItemData").GetValue(pickup)), Is.EqualTo(1));
            }
            finally
            {
                cooldown.SetValue(null, originalCooldown);
                foreach (Component popup in UnityEngine.Object.FindObjectsByType(popupType, FindObjectsSortMode.None).Except(existingPopups))
                    UnityEngine.Object.DestroyImmediate(popup.gameObject);
                UnityEngine.Object.DestroyImmediate(dropObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void WorldDrops_OneThenAll_AndFullBagClose_PreserveQuantities()
        {
            var pickupType = RuntimeType("TheLastKnight.Inventory.WorldItemPickup");
            var before = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
            try
            {
                Left(Quick, 0);
                Call(_inventory, "DropCursorItemToWorld", true, new Vector3(10000, 10000, 0));
                Assert.That(Count(Held), Is.EqualTo(2));
                Call(_inventory, "DropCursorItemToWorld", false, new Vector3(10000, 10000, 0));
                Assert.That(Held, Is.Null);
                Set(Quick, 0, "potion_might", 5); Left(Quick, 0);
                for (int i = 0; i < 24; i++) Set(Bag, i, "church_key", 1);
                Call(_inventory, "Close");
                Assert.That(Held, Is.Null);
                var drops = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None).Except(before).ToArray();
                Assert.That(drops.Length, Is.EqualTo(3));
                Assert.That(drops.Select(d => Count(pickupType.GetProperty("ItemData").GetValue(d))).OrderBy(c => c), Is.EqualTo(new[] { 1, 2, 5 }));
                foreach (Component drop in drops)
                {
                    Assert.That(drop.GetComponent<CircleCollider2D>().isTrigger, Is.True);
                    Assert.That(drop.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
                }
            }
            finally
            {
                foreach (Component drop in UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None).Except(before))
                    UnityEngine.Object.DestroyImmediate(drop.gameObject);
            }
        }
        [Test]
        public void Catalog_AllIconsLoad_AndInvalidCountsAreRejected()
        {
            foreach (string id in new[] { "potion_heal", "potion_might", "gold_pouch", "moonstone_shard", "church_key", "rune_pentagram", "rune_hand", "rune_eye", "rune_trident" })
            {
                var item = Item(id, 1);
                Assert.That(item.GetType().GetProperty("Icon").GetValue(item), Is.Not.Null, id);
            }
            Assert.That(Item("potion_heal", 0), Is.Null);
        }
    }
}
