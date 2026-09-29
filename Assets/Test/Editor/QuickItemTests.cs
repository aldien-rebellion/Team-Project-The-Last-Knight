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
            Set(Bag, 0, "bread", 5);
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
            Set(Bag, 0, "bread", 64);
            Set(Bag, 1, "bread", 5);
            Left(Bag, 1); Left(Bag, 0);
            Assert.That(Count(Slot(Bag, 0)), Is.EqualTo(64));
            Assert.That(Count(Held), Is.EqualTo(5));
            Set(Bag, 0, "bread", 62); Left(Bag, 0);
            Assert.That(Count(Held), Is.EqualTo(3));
            Right(Bag, 0);
            Assert.That(Count(Held), Is.EqualTo(3));
        }
        [Test]
        public void DifferentItems_LeftSwaps_RightDoesNothing()
        {
            Set(Bag, 0, "bread", 2); Set(Bag, 1, "golden_seed", 1);
            Left(Bag, 0); Right(Bag, 1);
            Assert.That(Field(Held, "id"), Is.EqualTo("bread"));
            Left(Bag, 1);
            Assert.That(Field(Held, "id"), Is.EqualTo("golden_seed"));
            Assert.That(Field(Slot(Bag, 1), "id"), Is.EqualTo("bread"));
        }
        [Test]
        public void QuickMove_PartialCapacity_ConservesRemainderAndSyncsHud()
        {
            Set(Quick, 0, "potion_heal", 63);
            for (int i = 1; i < 5; i++) Set(Quick, i, "knight_sword", 1);
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
            Set(Bag, 3, "bread", 9); Left(Bag, 3);
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
                Assert.That(type.GetProperty("CurrentHP").GetValue(player), Is.EqualTo(200f));
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
                Set(Quick, 1, "potion_stamina", 1);
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(true));
                Assert.That(property("CurrentHP"), Is.EqualTo(100f));
                Assert.That(property("HealingPotions"), Is.EqualTo(0));
                Assert.That(Field(Slot(Quick, 0), "id"), Is.EqualTo("potion_stamina"));
                Assert.That(Field(Call(_quick, "GetActiveItem"), "id"), Is.EqualTo("potion_stamina"));
                Call(_quick, "UseSlot", 0, player);
                Assert.That(property("CurrentStamina"), Is.EqualTo(100f));
                Assert.That(property("CurrentHP"), Is.EqualTo(100f), "Stamina elixir must not heal");
                Set(Quick, 0, "bread", 1); Call(_quick, "UseSlot", 0, player);
                Assert.That(property("CurrentHP"), Is.EqualTo(125f));
                Set(Quick, 0, "gold_pouch", 1); Call(_quick, "UseSlot", 0, player);
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
                Set(Quick, 0, "smoke_bomb", 2);
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(false));
                Assert.That(Count(Slot(Quick, 0)), Is.EqualTo(2));
                Set(Quick, 0, "knight_sword", 1);
                Assert.That(Call(_quick, "UseSlot", 0, player), Is.EqualTo(false));
            }
            finally { UnityEngine.Object.DestroyImmediate(playerObject); }
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
                Call(pickup, "Initialize", Item("bread", 1), 0f);
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
                Set(Quick, 0, "bread", 5); Left(Quick, 0);
                for (int i = 0; i < 24; i++) Set(Bag, i, "knight_sword", 1);
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
            foreach (string id in new[] { "potion_heal", "potion_stamina", "golden_seed", "potion_might", "bread", "gold_pouch", "smoke_bomb", "throwing_dart", "knight_sword", "silver_armor", "heavy_boots", "moonstone_shard" })
            {
                var item = Item(id, 1);
                Assert.That(item.GetType().GetProperty("Icon").GetValue(item), Is.Not.Null, id);
            }
            Assert.That(Item("bread", 0), Is.Null);
        }
    }
}
