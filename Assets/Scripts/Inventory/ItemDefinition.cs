using System;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    public enum ItemUseEffect { None, Legacy, Heal, RestoreStamina, AddGold, AddStatPoints, MightBuff }

    [CreateAssetMenu(fileName = "new_item", menuName = "The Last Knight/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [Tooltip("Unique permanent ID used by saves, e.g. potion_heal. Do not rename after release.")]
        public string id;
        public string displayName;
        public string typeName;
        [TextArea(3, 6)] public string description;
        [Tooltip("Drag a Sprite from the Project window here.")]
        public Sprite icon;
        public ItemCategory category = ItemCategory.Material;
        [Min(1)] public int maxStack = 64;
        public bool isConsumable;
        [Tooltip("Legacy preserves existing coded behavior for an existing item ID.")]
        public ItemUseEffect useEffect;
        [Min(1)] public int effectAmount = 50;

        public InventoryItemData CreateItem(int count = 1, InventoryItemData legacy = null)
        {
            var item = new InventoryItemData
            {
                id = id, name = displayName, typeName = typeName, description = description,
                category = category, maxStack = Mathf.Max(1, maxStack),
                count = Mathf.Clamp(count, 1, Mathf.Max(1, maxStack)), isConsumable = isConsumable,
                Icon = icon
            };
            int amount = Mathf.Max(1, effectAmount);
            switch (useEffect)
            {
                case ItemUseEffect.Legacy:
                    item.onUse = legacy?.onUse;
                    item.canUse = legacy?.canUse;
                    break;
                case ItemUseEffect.Heal:
                    item.onUse = p => { if (p != null) p.Heal(amount); };
                    item.canUse = p => p != null && p.CurrentHP < p.MaxHP;
                    break;
                case ItemUseEffect.RestoreStamina:
                    item.onUse = p => { if (p != null) p.RestoreStamina(amount); };
                    item.canUse = p => p != null && p.CurrentStamina < p.MaxStamina;
                    break;
                case ItemUseEffect.AddGold:
                    item.onUse = p => { if (p != null) p.AddGold(amount); };
                    break;
                case ItemUseEffect.AddStatPoints:
                    item.onUse = p => { if (p != null) p.AddStatPoints(amount); };
                    break;
                case ItemUseEffect.MightBuff:
                    item.onUse = p => { if (p != null) p.ApplyMightBuff(); };
                    break;
            }
            return item;
        }
    }
}
