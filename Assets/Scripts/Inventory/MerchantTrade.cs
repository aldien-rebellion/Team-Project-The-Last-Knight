using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Environment;
using TheLastKnight.Stats;
using TheLastKnight.UI;

namespace TheLastKnight.Inventory
{
    public enum TradeFailure { None, Unavailable, InvalidOffer, SoldOut, NotEnoughGold, InventoryFull, AlreadyOwned, EffectRejected, NotSellable }

    public readonly struct TradeResult
    {
        public bool Success => Failure == TradeFailure.None;
        public TradeFailure Failure { get; }
        public string ItemName { get; }
        public int Quantity { get; }
        public int GoldAmount { get; }

        internal TradeResult(TradeFailure failure, string itemName = null, int quantity = 0, int goldAmount = 0)
        {
            Failure = failure;
            ItemName = itemName;
            Quantity = quantity;
            GoldAmount = goldAmount;
        }
    }

    // Owns transaction outcomes. ShopItemConfig remains in its original namespace
    // to preserve existing serialized catalogs and callers.
    public sealed class MerchantTrade
    {
        private readonly IEnumerable<ShopItemConfig> _catalog;
        private readonly float _sellMultiplier;

        public MerchantTrade(IEnumerable<ShopItemConfig> catalog, float sellMultiplier)
        {
            _catalog = catalog;
            _sellMultiplier = sellMultiplier;
        }

        private static string CanonicalId(string id)
        {
            if (string.Equals(id, "HEAL", StringComparison.OrdinalIgnoreCase)) return "potion_heal";
            if (string.Equals(id, "RUNE", StringComparison.OrdinalIgnoreCase)) return "rune_trident";
            return id;
        }

        public ShopItemConfig FindCatalogItem(string id)
        {
            if (string.IsNullOrEmpty(id) || _catalog == null) return null;
            foreach (var config in _catalog)
                if (config != null && string.Equals(CanonicalId(config.id), CanonicalId(id), StringComparison.OrdinalIgnoreCase)) return config;
            return null;
        }

        public int GetSellPrice(string id)
        {
            int potionPrice = PotionPrices.GetSellPrice(CanonicalId(id));
            if (potionPrice > 0) return potionPrice;
            var config = FindCatalogItem(id);
            if (config != null)
                return config.customSellPrice > 0 ? config.customSellPrice : Mathf.Max(1, Mathf.RoundToInt(config.buyPrice * _sellMultiplier));
            var item = ItemRegistry.CreateItem(id);
            if (item != null && item.sellPrice > 0) return item.sellPrice;
            int price = ItemRegistry.GetSellPrice(id);
            return price > 0 ? price : -1;
        }

        public TradeResult Buy(ShopItemConfig config, PlayerStats player, InventoryManager inventory, DemonRuneManager runes)
        {
            if (config == null || config.buyPrice < 0) return new TradeResult(TradeFailure.InvalidOffer);
            if (player == null) return new TradeResult(TradeFailure.Unavailable);
            config.EnsureStockInitialized();
            if (config.initialStock >= 0 && config.currentStock <= 0) return new TradeResult(TradeFailure.SoldOut);
            if (player.Gold < config.buyPrice) return new TradeResult(TradeFailure.NotEnoughGold);

            InventoryItemData item = null;
            switch (config.itemType)
            {
                case ShopItemType.InventoryItem:
                    item = ItemRegistry.CreateItem(CanonicalId(config.id));
                    if (item == null) return new TradeResult(TradeFailure.InvalidOffer);
                    if (inventory == null) return new TradeResult(TradeFailure.Unavailable);
                    if (!inventory.CanAccept(item)) return new TradeResult(TradeFailure.InventoryFull);
                    break;
                case ShopItemType.StatPotion:
                    if (config.statName != "STR" && config.statName != "VIT" && config.statName != "DEX" && config.statName != "AGI")
                        return new TradeResult(TradeFailure.InvalidOffer);
                    if (config.statName == "DEX" && player.DEX >= PlayerStats.MaxDexterity)
                        return new TradeResult(TradeFailure.EffectRejected);
                    break;
                case ShopItemType.RuneReward:
                    if (runes == null || GameManager.Instance == null) return new TradeResult(TradeFailure.Unavailable);
                    if (config.runeIndex < 0 || config.runeIndex >= DemonRuneManager.ItemIds.Length) return new TradeResult(TradeFailure.InvalidOffer);
                    if (runes.HasReward(config.runeIndex)) return new TradeResult(TradeFailure.AlreadyOwned);
                    break;
                default:
                    return new TradeResult(TradeFailure.InvalidOffer);
            }

            if (!player.TrySpendGold(config.buyPrice)) return new TradeResult(TradeFailure.NotEnoughGold);
            bool received = config.itemType == ShopItemType.InventoryItem ? inventory.TryAddItem(item) :
                config.itemType == ShopItemType.StatPotion ? player.AddStatPotion(config.statName) :
                runes.DropRune(config.runeIndex, player.transform.position);
            if (!received)
            {
                player.AddGold(config.buyPrice);
                return new TradeResult(TradeFailure.EffectRejected);
            }
            if (config.initialStock >= 0) config.currentStock--;
            inventory?.SyncWithQuickItemManager(player);
            return new TradeResult(TradeFailure.None, config.displayName, 1, config.buyPrice);
        }

        public TradeResult BuyRandom(RandomMerchantOffer offer, PlayerStats player, InventoryManager inventory)
        {
            if (offer == null) return new TradeResult(TradeFailure.InvalidOffer);
            offer.EnsureInitialized();
            var item = ItemRegistry.CreateItem(offer.itemId);
            var result = Buy(new ShopItemConfig { id = offer.itemId, displayName = item?.name, buyPrice = offer.price }, player, inventory, null);
            if (result.Success) offer.AdvanceAfterPurchase();
            return result;
        }

        public TradeResult Sell(SlotType type, int index, bool all, PlayerStats player, InventoryManager inventory)
        {
            if (player == null || inventory == null) return new TradeResult(TradeFailure.Unavailable);
            var item = inventory.GetSlot(type, index);
            if (item == null || item.count <= 0) return new TradeResult(TradeFailure.Unavailable);
            int price = GetSellPrice(item.id);
            if (price <= 0) return new TradeResult(TradeFailure.NotSellable);
            int quantity = all ? item.count : 1;
            long gold = (long)price * quantity;
            if (gold > int.MaxValue - (long)player.Gold) return new TradeResult(TradeFailure.EffectRejected);
            string name = item.name;
            if (!inventory.TryRemoveFromSlot(type, index, quantity, player)) return new TradeResult(TradeFailure.Unavailable);
            player.AddGold((int)gold);
            return new TradeResult(TradeFailure.None, name, quantity, (int)gold);
        }
    }
}
