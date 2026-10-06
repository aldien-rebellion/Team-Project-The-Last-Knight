using System;

namespace TheLastKnight.Inventory
{
    public static class PotionPrices
    {
        public static int GetBuyPrice(string id)
        {
            switch ((id ?? "").ToLowerInvariant())
            {
                case "heal": case "potion_heal": return 50;
                case "potion_might": case "potion_swiftness": case "potion_fortitude": return 100;
                case "potion_regeneration": case "potion_endurance": case "potion_purity": return 200;
                case "potion_undying": case "potion_crit_damage": return 300;
                default: return 0;
            }
        }

        public static int GetSellPrice(string id) => GetBuyPrice(id) / 2;
    }
}
