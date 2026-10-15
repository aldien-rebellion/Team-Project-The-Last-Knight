using System.Collections.Generic;
using UnityEngine;

namespace TheLastKnight.Stats
{
    // Resource-backed presentation is kept out of the timed-effect module.
    internal sealed class BuffDisplay
    {
        private readonly List<BuffSnapshot> _active = new List<BuffSnapshot>(9);
        private sealed class Description
        {
            public readonly string Id, Name, Category, Text, Potion, Fallback;
            public readonly Color Color;
            public Description(string id, string name, string text, string potion, string fallback, Color color, string category = "Elixir Enhancement")
            { Id = id; Name = name; Text = text; Potion = potion; Fallback = fallback; Color = color; Category = category; }
        }
        // The order matches TemporaryBuff and preserves the existing HUD order.
        private static readonly Description[] Descriptions =
        {
            new Description("buff_iron_will", "Iron Will", "Boosts Attack Power by +22% with unwavering warrior spirit.", null, "CharacterStatus/Skill_BerserkBuff", new Color(1f, 0.48f, 0.15f), "Combat Skill Buff"),
            new Description("buff_might", "Potion of Might", "Infuses weapons with brute force, boosting Attack Power by +25%.", "potion_might", "CharacterStatus/Item_BluePotion", new Color(0.72f, 0.35f, 0.95f)),
            new Description("buff_swiftness", "Potion of Swiftness", "Increases Attack Speed and Movement Speed (walk & sprint) by +25%.", "potion_swiftness", "CharacterStatus/Item_BluePotion", new Color(0.2f, 0.85f, 0.95f)),
            new Description("buff_endurance", "Potion of Endurance", "Reduces all Stamina consumption by 50%.", "potion_endurance", "CharacterStatus/Item_GreenPotion", new Color(0.35f, 0.85f, 0.35f)),
            new Description("buff_purity", "Potion of Purity", "Grants absolute immunity to Stun and all negative status effects.", "potion_purity", "CharacterStatus/Item_RedPotion_Clean", new Color(0.95f, 0.95f, 0.45f)),
            new Description("buff_regeneration", "Potion of Regeneration", "Rapidly regenerates +5% of Max HP per second.", "potion_regeneration", "CharacterStatus/Item_RedPotion_Clean", new Color(0.3f, 0.95f, 0.55f)),
            new Description("buff_fortitude", "Potion of Fortitude", "Bolsters defenses, increasing Max HP and Defense by +25%.", "potion_fortitude", "CharacterStatus/Item_BluePotion", new Color(0.35f, 0.55f, 0.95f)),
            new Description("buff_undying", "Potion of the Undying", "Reduces HP to 1 and blocks all healing in exchange for absolute invincibility.", "potion_undying", "CharacterStatus/Item_RedPotion", new Color(0.95f, 0.2f, 0.2f), "Forbidden Elixir"),
            new Description("buff_crit_damage", "Potion Crit DMG", PlayerStats.CritDamageBuffDescription, "potion_crit_damage", "CharacterStatus/Items/Cat Fantasy - 32x32 Potion Pack/Potion 1/Potion Crit DMG", new Color(0.95f, 0.4f, 0.2f))
        };

        public void Read(TemporaryBuffs buffs, bool aura, bool hurt, List<ActiveBuffInfo> target)
        {
            if (target == null) return;
            target.Clear();
            buffs.GetActive(_active);
            foreach (var active in _active)
            {
                var description = Descriptions[(int)active.Kind];
                target.Add(new ActiveBuffInfo
                {
                    id = description.Id, name = description.Name, category = description.Category, description = description.Text,
                    remainingSeconds = active.Remaining, totalDuration = active.Duration, formattedTime = FormatTime(active.Remaining),
                    icon = LoadIcon(description), isDebuff = false, themeColor = description.Color
                });
            }
            if (aura) target.Add(new ActiveBuffInfo
            {
                id = "aura_medusa", name = "Fountain Grace", category = "Sacred Aura",
                description = "Sacred blessing restoring Stamina (+20%/s) and HP up to 70% Max HP.",
                remainingSeconds = -1f, totalDuration = -1f, formattedTime = "**:**",
                icon = Resources.Load<Sprite>("BuffIcons/buff_medusa") ?? Resources.Load<Sprite>("CharacterStatus/Item_RuneTrident"),
                isDebuff = false, themeColor = new Color(0.25f, 0.85f, 1f)
            });
            if (hurt) target.Add(new ActiveBuffInfo
            {
                id = "debuff_stun", name = "Stunned", category = "Status Debuff",
                description = "Stunned and incapacitated. Cannot move, attack, or cast skills.",
                remainingSeconds = 1f, totalDuration = 1f, formattedTime = "0:01",
                icon = Resources.Load<Sprite>("BuffIcons/debuff_stun") ?? Resources.Load<Sprite>("CharacterStatus/Slot_Frame"),
                isDebuff = true, themeColor = new Color(1f, 0.25f, 0.25f)
            });
        }

        private static Sprite LoadIcon(Description description)
        {
            var icon = Resources.Load<Sprite>("BuffIcons/" + description.Id);
            if (icon != null) return icon;
            if (description.Potion != null)
            {
                var definition = Resources.Load<TheLastKnight.Inventory.ItemDefinition>("Items/Definitions/Consumables/" + description.Potion);
                if (definition != null && definition.icon != null) return definition.icon;
            }
            return Resources.Load<Sprite>(description.Fallback);
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) return "**:**";
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60}:{total % 60:D2}";
        }
    }
}
