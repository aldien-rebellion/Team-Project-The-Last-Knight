using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class ItemAuthoringTests
    {
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TheLastKnight.Inventory." + name)).First(t => t != null);
        private static object Field(object item, string name) => item.GetType().GetField(name).GetValue(item);

        [Test]
        public void AuthoredCatalog_PreservesLegacyIconsAndCallbacks()
        {
            var registry = Find("ItemRegistry");
            var definitions = Resources.LoadAll("Items/Definitions", Find("ItemDefinition"));
            Assert.GreaterOrEqual(definitions.Length, 8);
            foreach (string id in (System.Collections.Generic.IEnumerable<string>)registry.GetProperty("LegacyIds").GetValue(null))
            {
                var legacy = registry.GetMethod("CreateLegacyItem").Invoke(null, new object[] { id });
                var item = registry.GetMethod("CreateItem").Invoke(null, new object[] { id, 3 });
                Assert.AreEqual(Field(legacy, "name"), Field(item, "name"), id);
                var icon = item.GetType().GetProperty("Icon").GetValue(item);
                var legacyIcon = legacy.GetType().GetProperty("Icon").GetValue(legacy);
                if (legacyIcon != null) Assert.AreEqual(legacyIcon, icon, id);
                Assert.AreEqual(Field(legacy, "onUse") != null, Field(item, "onUse") != null, id);
            }
        }

        [Test]
        public void NewDefinition_UsesDirectSpriteAndCreatesIndependentStacks()
        {
            var type = Find("ItemDefinition");
            var definition = ScriptableObject.CreateInstance(type);
            try
            {
                type.GetField("id").SetValue(definition, "test_custom");
                type.GetField("maxStack").SetValue(definition, 5);
                type.GetField("useEffect").SetValue(definition, Enum.Parse(Find("ItemUseEffect"), "Heal"));
                var sprite = Resources.Load<Sprite>("CharacterStatus/Item_RedPotion_Clean");
                Assert.IsNotNull(sprite);
                type.GetField("icon").SetValue(definition, sprite);
                var first = type.GetMethod("CreateItem").Invoke(definition, new object[] { 99, null });
                var second = type.GetMethod("CreateItem").Invoke(definition, new object[] { 2, null });
                Assert.AreEqual(5, Field(first, "count"));
                Assert.AreEqual(2, Field(second, "count"));
                Assert.AreSame(sprite, first.GetType().GetProperty("Icon").GetValue(first));
                Assert.IsNotNull(Field(first, "onUse"));
                Assert.IsNotNull(Field(first, "canUse"));
                var clone = first.GetType().GetMethod("Clone").Invoke(first, new object[] { 1 });
                Assert.AreSame(sprite, clone.GetType().GetProperty("Icon").GetValue(clone));
                Assert.AreSame(Field(first, "canUse"), Field(clone, "canUse"));
                Assert.AreEqual(5, Field(first, "count"));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void AuthoredPotions_AllHaveValidDefinitionsAndIcons()
        {
            var registry = Find("ItemRegistry");
            string[] potionIds = new[]
            {
                "potion_heal",
                "potion_swiftness",
                "potion_endurance",
                "potion_purity",
                "potion_regeneration",
                "potion_might",
                "potion_fortitude",
                "potion_undying"
            };

            foreach (var id in potionIds)
            {
                var item = registry.GetMethod("CreateItem").Invoke(null, new object[] { id, 1 });
                Assert.IsNotNull(item, "Item must exist: " + id);
                Assert.AreEqual("Potion", Field(item, "typeName"), "TypeName must be Potion for: " + id);
                var icon = item.GetType().GetProperty("Icon").GetValue(item);
                Assert.IsNotNull(icon, "Icon must be loaded for: " + id);
                Assert.IsNotNull(Field(item, "onUse"), "onUse must be configured for: " + id);
            }
        }
    }
}
