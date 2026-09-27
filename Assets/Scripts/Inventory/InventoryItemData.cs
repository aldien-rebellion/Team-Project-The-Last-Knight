using System;
using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    public enum ItemCategory
    {
        Consumable,
        Weapon,
        Armor,
        Boots,
        Material,
        Quest,
        KeyItem = Quest,
        Accessory
    }

    [Serializable]
    public class InventoryItemData
    {
        public string id;
        public string name;
        public string typeName;
        public string description;
        public string iconPath;
        public int count = 1;
        public int maxStack = 64;
        public ItemCategory category = ItemCategory.Consumable;
        public bool isConsumable = true;

        [NonSerialized]
        private Sprite _cachedIcon;
        public Sprite Icon
        {
            get
            {
                if (_cachedIcon == null && !string.IsNullOrEmpty(iconPath))
                {
                    _cachedIcon = Resources.Load<Sprite>(iconPath);
                }
                return _cachedIcon;
            }
            set => _cachedIcon = value;
        }

        public Action<PlayerStats> onUse;

        public InventoryItemData Clone(int customCount = -1)
        {
            return new InventoryItemData
            {
                id = this.id,
                name = this.name,
                typeName = this.typeName,
                description = this.description,
                iconPath = this.iconPath,
                _cachedIcon = this._cachedIcon,
                count = customCount > 0 ? customCount : this.count,
                maxStack = this.maxStack,
                category = this.category,
                isConsumable = this.isConsumable,
                onUse = this.onUse
            };
        }

        public bool CanStackWith(InventoryItemData other)
        {
            if (other == null) return false;
            return string.Equals(id, other.id, StringComparison.OrdinalIgnoreCase) && maxStack > 1;
        }
    }
}
