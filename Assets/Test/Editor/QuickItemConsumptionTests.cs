using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class QuickItemConsumptionTests
    {
        private GameObject _owner;
        private Component _inventory, _player;
        private object _use, _quick;
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private void SetHP(float value) => _player.GetType().GetField("_currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_player, value);
        private void SetPotion(int index, int count)
        {
            var item = Find("TheLastKnight.Inventory.ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { "potion_heal", count });
            Call(_inventory, "SetSlot", _quick, index, item);
        }
        private int Count => (int)Call(_inventory, "CountItem", "potion_heal");

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("QuickItemConsumptionTests");
            _owner.SetActive(false);
            _inventory = _owner.AddComponent(Find("TheLastKnight.Inventory.InventoryManager"));
            _player = _owner.AddComponent(Find("TheLastKnight.Stats.PlayerStats"));
            _player.GetType().GetField("<MaxHP>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_player, 500f);
            SetHP(1f);
            _quick = Enum.Parse(Find("TheLastKnight.Inventory.SlotType"), "QuickSlot");
            SetPotion(4, 2);
            _use = Prop(_inventory, "QuickItemUse");
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);

        [Test]
        public void Completion_ConsumesSelectedItemExactlyOnce()
        {
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            Assert.IsFalse((bool)Call(_use, "TryBegin", _player));
            Assert.IsTrue((bool)Call(_use, "Complete"));
            Assert.IsFalse((bool)Call(_use, "Complete"));
            Assert.AreEqual(1, Count);
            Assert.IsFalse((bool)Prop(_use, "IsPending"));
        }

        [Test]
        public void ReplacementAndCancellation_ConserveItems()
        {
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            SetPotion(4, 2);
            Assert.IsFalse((bool)Call(_use, "Complete"));
            Assert.AreEqual(2, Count);
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            Call(_use, "Cancel");
            Assert.IsFalse((bool)Call(_use, "Complete"));
            Assert.AreEqual(2, Count);
            Assert.AreEqual(1f, Prop(_player, "CurrentHP"));
        }

        [Test]
        public void ChangedSelection_DoesNotConsumeNewFirstSlot()
        {
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            SetPotion(0, 2);
            Assert.IsTrue((bool)Call(_use, "Complete"));
            var first = Call(_inventory, "GetSlot", _quick, 0);
            Assert.AreEqual(2, first.GetType().GetField("count").GetValue(first));
            Assert.AreEqual(3, Count);
        }

        [Test]
        public void DeathFullHealthOrHeldCursor_RejectsUseWithoutConsumption()
        {
            SetHP(500f);
            Assert.IsFalse((bool)Call(_use, "TryBegin", _player));
            SetHP(1f);
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            SetHP(0f);
            Assert.IsFalse((bool)Call(_use, "Complete"));
            SetHP(1f);
            Call(_inventory, "HandleLeftClick", _quick, 4, false, _player);
            Assert.IsFalse((bool)Call(_use, "TryBegin", _player));
            Assert.AreEqual(2, Count);
        }

        [Test]
        public void LegacyCompletion_SharesPendingIdentityAndCannotCompleteTwice()
        {
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            Assert.IsTrue((bool)Call(_use, "CompleteHealingPotion", _player));
            Assert.IsFalse((bool)Call(_use, "Complete"));
            Assert.AreEqual(1, Count);
            SetHP(1f);
            Assert.IsTrue((bool)Call(_use, "TryBegin", _player));
            SetPotion(4, 2);
            Assert.IsFalse((bool)Call(_use, "CompleteHealingPotion", _player));
            Assert.AreEqual(2, Count);
        }
    }
}
