using System;
using UnityEngine;

namespace TheLastKnight.Core
{
    [Serializable]
    public class RandomMerchantOffer
    {
        private static readonly string[] ItemIds =
        {
            "advanced_spellbook", "earth_scroll", "earth_spellbook", "fire_scroll",
            "ice_spellbook", "light_scroll", "thunder_scroll"
        };

        public string itemId;
        public int price = 200;

        public void EnsureInitialized()
        {
            if (Array.IndexOf(ItemIds, itemId) < 0) RollItem();
            price = Mathf.Max(200, price);
        }

        public void AdvanceAfterPurchase()
        {
            // Round upward to whole Gold, using long arithmetic to avoid overflow.
            price = (int)Math.Min(int.MaxValue, (price * 11L + 9L) / 10L);
            RollItem();
        }

        private void RollItem() => itemId = ItemIds[UnityEngine.Random.Range(0, ItemIds.Length)];
    }
}
