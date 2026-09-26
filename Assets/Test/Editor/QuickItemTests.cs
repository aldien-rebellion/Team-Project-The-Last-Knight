using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class QuickItemTests
    {
        private GameObject _holder;
        private Component _manager;
        private Type _managerType;
        private Type _slotDataType;

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        [SetUp]
        public void SetUp()
        {
            _managerType = RuntimeType("TheLastKnight.Core.QuickItemManager");
            _slotDataType = RuntimeType("TheLastKnight.Core.QuickItemSlotData");
            Assert.That(_managerType, Is.Not.Null, "QuickItemManager type must exist");
            Assert.That(_slotDataType, Is.Not.Null, "QuickItemSlotData type must exist");

            _holder = new GameObject("Test_QuickItemManager");
            _manager = _holder.AddComponent(_managerType);
            _managerType.GetMethod("InitializeDefaultSlots")?.Invoke(_manager, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
            }
        }

        private object GetSlot(int index)
        {
            return _managerType.GetMethod("GetSlot")?.Invoke(_manager, new object[] { index });
        }

        private void SetSlot(int index, object item)
        {
            _managerType.GetMethod("SetSlot")?.Invoke(_manager, new object[] { index, item });
        }

        private void SyncItemCount(string itemId, int count)
        {
            _managerType.GetMethod("SyncItemCount")?.Invoke(_manager, new object[] { itemId, count });
        }

        private void ShiftQueue()
        {
            _managerType.GetMethod("ShiftQueue")?.Invoke(_manager, null);
        }

        private object CreateSlotData(string id, string name, int count, int maxCount)
        {
            var data = Activator.CreateInstance(_slotDataType);
            _slotDataType.GetField("id")?.SetValue(data, id);
            _slotDataType.GetField("name")?.SetValue(data, name);
            _slotDataType.GetField("count")?.SetValue(data, count);
            _slotDataType.GetField("maxCount")?.SetValue(data, maxCount);
            return data;
        }

        private object GetFieldValue(object obj, string field)
        {
            return _slotDataType.GetField(field)?.GetValue(obj);
        }

        [Test]
        public void QuickItemManager_Initializes_WithHealingPotionInSlot1_AndSlots2To5Empty()
        {
            var slot1 = GetSlot(0);
            Assert.That(slot1, Is.Not.Null, "Slot 1 (Index 0) should contain the default healing potion.");
            Assert.That(GetFieldValue(slot1, "id"), Is.EqualTo("potion_heal"));
            Assert.That(GetFieldValue(slot1, "count"), Is.EqualTo(3));
            Assert.That(GetFieldValue(slot1, "maxCount"), Is.EqualTo(5));

            for (int i = 1; i < 5; i++)
            {
                Assert.That(GetSlot(i), Is.Null, $"Slot {i + 1} should be empty by default.");
            }
        }

        [Test]
        public void ShiftQueue_AdvancesSlot2ToSlot1_AndClearsSlot5()
        {
            var item2 = CreateSlotData("item_stamina", "Stamina Brew", 2, 5);
            var item3 = CreateSlotData("item_bomb", "Fire Bomb", 1, 3);

            SetSlot(1, item2);
            SetSlot(2, item3);

            ShiftQueue();

            var newSlot1 = GetSlot(0);
            var newSlot2 = GetSlot(1);
            var newSlot3 = GetSlot(2);
            var newSlot5 = GetSlot(4);

            Assert.That(newSlot1, Is.Not.Null, "Slot 1 should now hold item 2.");
            Assert.That(GetFieldValue(newSlot1, "id"), Is.EqualTo("item_stamina"));

            Assert.That(newSlot2, Is.Not.Null, "Slot 2 should now hold item 3.");
            Assert.That(GetFieldValue(newSlot2, "id"), Is.EqualTo("item_bomb"));

            Assert.That(newSlot3, Is.Null, "Slot 3 should now be empty.");
            Assert.That(newSlot5, Is.Null, "Slot 5 should be empty.");
        }

        [Test]
        public void SyncItemCount_WhenSlot1ReachesZero_AutomaticallyShiftsQueue()
        {
            var item2 = CreateSlotData("item_stamina", "Stamina Brew", 4, 5);
            SetSlot(1, item2);

            // Potion in slot 1 is consumed down to 0
            SyncItemCount("potion_heal", 0);

            var active = _managerType.GetMethod("GetActiveItem")?.Invoke(_manager, null);
            Assert.That(active, Is.Not.Null, "Active item should have shifted to item 2.");
            Assert.That(GetFieldValue(active, "id"), Is.EqualTo("item_stamina"));
            Assert.That(GetFieldValue(active, "count"), Is.EqualTo(4));

            Assert.That(GetSlot(1), Is.Null, "Slot 2 should now be empty after shifting.");
        }

        [Test]
        public void SyncItemCount_WhenOnlyItemReachesZero_Slot1BecomesEmpty()
        {
            SyncItemCount("potion_heal", 0);

            var active = _managerType.GetMethod("GetActiveItem")?.Invoke(_manager, null);
            Assert.That(active, Is.Null, "Active item should be null when the only item is depleted.");
        }

        [Test]
        public void SyncItemCount_WhenPotionsReplenished_AssignsBackToSlot1IfEmpty()
        {
            SyncItemCount("potion_heal", 0);
            var activeBefore = _managerType.GetMethod("GetActiveItem")?.Invoke(_manager, null);
            Assert.That(activeBefore, Is.Null, "Slot 1 should be empty.");

            SyncItemCount("potion_heal", 2);

            var active = _managerType.GetMethod("GetActiveItem")?.Invoke(_manager, null);
            Assert.That(active, Is.Not.Null, "Healing potion should be assigned back to Slot 1.");
            Assert.That(GetFieldValue(active, "id"), Is.EqualTo("potion_heal"));
            Assert.That(GetFieldValue(active, "count"), Is.EqualTo(2));
        }
    }
}
