using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class MerchantTradeTests
    {
        private GameObject _owner;
        private Component _player, _inventory;
        private object _config, _trade;
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static void Field(object target, string name, object value) => target.GetType().GetField(name).SetValue(target, value);
        private static object SlotType(string name) => Enum.Parse(Find("TheLastKnight.Inventory.SlotType"), name);
        private static object Item(string id, int count) => Find("TheLastKnight.Inventory.ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { id, count });

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("MerchantTradeTests");
            _owner.SetActive(false);
            _player = _owner.AddComponent(Find("TheLastKnight.Stats.PlayerStats"));
            _inventory = _owner.AddComponent(Find("TheLastKnight.Inventory.InventoryManager"));
            Call(_player, "AddGold", 1000);
            _config = Activator.CreateInstance(Find("TheLastKnight.UI.ShopItemConfig"));
            Field(_config, "id", "potion_heal");
            Field(_config, "displayName", "Healing Potion");
            Field(_config, "buyPrice", 100);
            Field(_config, "initialStock", 2);
            var catalog = Array.CreateInstance(_config.GetType(), 1);
            catalog.SetValue(_config, 0);
            _trade = Activator.CreateInstance(Find("TheLastKnight.Inventory.MerchantTrade"), new object[] { catalog, 0.75f });
        }

        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_owner);
        private object Buy() => Call(_trade, "Buy", _config, _player, _inventory, null);
        private int Count(string id) => (int)Call(_inventory, "CountItem", id);
        private void Fill()
        {
            foreach (var pair in new[] { ("Inventory", 24), ("QuickSlot", 5) })
                for (int i = 0; i < pair.Item2; i++) Call(_inventory, "SetSlot", SlotType(pair.Item1), i, Item("moonstone_shard", 64));
        }

        [Test]
        public void PurchaseAndSale_ConserveGoldQuantityAndLimitedStock()
        {
            Assert.IsTrue((bool)Prop(Buy(), "Success"));
            Assert.AreEqual(900, Prop(_player, "Gold"));
            Assert.AreEqual(1, Count("potion_heal"));
            Assert.AreEqual(1, _config.GetType().GetField("currentStock").GetValue(_config));
            Assert.IsTrue((bool)Prop(Buy(), "Success"));
            Assert.AreEqual("SoldOut", Prop(Buy(), "Failure").ToString());
            var sale = Call(_trade, "Sell", SlotType("QuickSlot"), 0, false, _player, _inventory);
            int price = (int)Call(_trade, "GetSellPrice", "potion_heal");
            Assert.IsTrue((bool)Prop(sale, "Success"));
            Assert.AreEqual(800 + price, Prop(_player, "Gold"));
            Assert.AreEqual(1, Count("potion_heal"));
            Assert.AreEqual(1, Prop(sale, "Quantity"));
            Assert.IsTrue((bool)Prop(Call(_trade, "Sell", SlotType("QuickSlot"), 0, true, _player, _inventory), "Success"));
            Assert.AreEqual(0, Count("potion_heal"));
            Assert.AreEqual(800 + 2 * price, Prop(_player, "Gold"));
        }

        [Test]
        public void FailedPurchase_DoesNotChargeOrUseStock()
        {
            Fill();
            Assert.AreEqual("InventoryFull", Prop(Buy(), "Failure").ToString());
            Assert.AreEqual(1000, Prop(_player, "Gold"));
            Assert.AreEqual(2, _config.GetType().GetField("currentStock").GetValue(_config));
            Call(_player, "TrySpendGold", 1000);
            Assert.AreEqual("NotEnoughGold", Prop(Buy(), "Failure").ToString());
            Assert.AreEqual(0, Count("potion_heal"));
        }

        [Test]
        public void AtomicReceipt_RejectsPartialCapacity_AndCanUseAvailableStackSpace()
        {
            Fill();
            Call(_inventory, "SetSlot", SlotType("QuickSlot"), 0, Item("potion_heal", 63));
            var two = Item("potion_heal", 2);
            Assert.IsFalse((bool)Call(_inventory, "TryAddItem", two));
            Assert.AreEqual(63, Count("potion_heal"));
            Assert.AreEqual(2, two.GetType().GetField("count").GetValue(two));
            Assert.IsTrue((bool)Prop(Buy(), "Success"));
            Assert.AreEqual(64, Count("potion_heal"));
            Assert.AreEqual(900, Prop(_player, "Gold"));
        }

        [Test]
        public void RandomPurchase_OnlyAdvancesAfterReceipt()
        {
            var randomState = UnityEngine.Random.state;
            try
            {
                var offer = Activator.CreateInstance(Find("TheLastKnight.Core.RandomMerchantOffer"));
                Field(offer, "itemId", "earth_scroll");
                Field(offer, "price", 200);
                Fill();
                Assert.IsFalse((bool)Prop(Call(_trade, "BuyRandom", offer, _player, _inventory), "Success"));
                Assert.AreEqual(200, offer.GetType().GetField("price").GetValue(offer));
                Assert.AreEqual("earth_scroll", offer.GetType().GetField("itemId").GetValue(offer));
                Assert.AreEqual(1000, Prop(_player, "Gold"));
                Call(_inventory, "SetSlot", SlotType("Inventory"), 0, null);
                Assert.IsTrue((bool)Prop(Call(_trade, "BuyRandom", offer, _player, _inventory), "Success"));
                Assert.AreEqual(1, Count("earth_scroll"));
                Assert.AreEqual(800, Prop(_player, "Gold"));
                Assert.AreEqual(220, offer.GetType().GetField("price").GetValue(offer));
            }
            finally { UnityEngine.Random.state = randomState; }
        }

        [Test]
        public void RejectedStatAndRuneOffers_DoNotChargeOrCreateRewards()
        {
            Field(_config, "itemType", Enum.Parse(Find("TheLastKnight.UI.ShopItemType"), "StatPotion"));
            Field(_config, "statName", "invalid");
            Assert.AreEqual("InvalidOffer", Prop(Buy(), "Failure").ToString());
            Field(_config, "statName", "DEX");
            _player.GetType().GetField("_dexterity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_player, 200);
            Assert.AreEqual("EffectRejected", Prop(Buy(), "Failure").ToString());
            Field(_config, "itemType", Enum.Parse(Find("TheLastKnight.UI.ShopItemType"), "RuneReward"));
            Assert.AreEqual("Unavailable", Prop(Buy(), "Failure").ToString());
            Assert.AreEqual(1000, Prop(_player, "Gold"));
        }
    }
}
