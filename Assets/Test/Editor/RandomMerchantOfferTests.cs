using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class RandomMerchantOfferTests
    {
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RandomOfferRow_RefreshesIconAndPrice()
        {
            var owner = new GameObject("RandomOfferRowTest");
            owner.SetActive(false);
            var root = new GameObject("OfferCanvas", typeof(Canvas));
            var cameraObject = new GameObject("OfferCamera", typeof(Camera));
            var texture = new RenderTexture(640, 240, 24);
            var previousRT = RenderTexture.active;
            Texture2D image = null;
            object offer = null;
            string previousItem = null;
            int previousPrice = 200;
            var randomState = UnityEngine.Random.state;
            try
            {
                var type = Find("TheLastKnight.UI.ShopUI");
                var shop = owner.AddComponent(type);
                type.GetMethod("LoadSprites", Private).Invoke(shop, null);
                type.GetMethod("SyncRandomOffer", Private).Invoke(shop, null);
                offer = type.GetProperty("RandomOffer", Private).GetValue(shop);
                previousItem = (string)offer.GetType().GetField("itemId").GetValue(offer);
                previousPrice = (int)offer.GetType().GetField("price").GetValue(offer);
                var config = type.GetField("_randomOfferConfig", Private).GetValue(shop);
                var row = type.GetMethod("CreateShopItemRow", Private).Invoke(shop, new[] { root.transform, config });
                ((IList)type.GetField("_shopRowViews", Private).GetValue(shop)).Add(row);
                var rowTransform = (Transform)row.GetType().GetField("rowTransform").GetValue(row);
                rowTransform.GetComponent<RectTransform>().sizeDelta = new Vector2(450f, 74f);
                offer.GetType().GetField("itemId").SetValue(offer, "earth_scroll");
                offer.GetType().GetField("price").SetValue(offer, 220);
                type.GetMethod("Refresh").Invoke(shop, null);
                var priceText = row.GetType().GetField("txtPrice").GetValue(row);
                Assert.AreEqual("220", priceText.GetType().GetProperty("text").GetValue(priceText));
                var icon = (UnityEngine.UI.Image)row.GetType().GetField("imgIcon").GetValue(row);
                Assert.IsNotNull(icon.sprite);
                Assert.AreEqual("earth scroll_0", icon.sprite.name);
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.13f, 0.18f);
                camera.targetTexture = texture;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                image = new Texture2D(640, 240, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 640, 240), 0, 0);
                image.Apply();
                System.IO.Directory.CreateDirectory("Temp");
                System.IO.File.WriteAllBytes("Temp/RandomMerchantOffer.png", image.EncodeToPNG());
            }
            finally
            {
                if (offer != null)
                {
                    offer.GetType().GetField("itemId").SetValue(offer, previousItem);
                    offer.GetType().GetField("price").SetValue(offer, previousPrice);
                }
                UnityEngine.Random.state = randomState;
                RenderTexture.active = previousRT;
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void Offer_UsesAllSevenItemsWithEqualWeights_CompoundsPrice_AndSurvivesSaveCopy()
        {
            var type = Find("TheLastKnight.Core.RandomMerchantOffer");
            var offer = Activator.CreateInstance(type);
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(18731);
                type.GetMethod("EnsureInitialized").Invoke(offer, null);
                Assert.AreEqual(200, type.GetField("price").GetValue(offer));
                foreach (int price in new[] { 220, 242, 267, 294 })
                {
                    type.GetMethod("AdvanceAfterPurchase").Invoke(offer, null);
                    Assert.AreEqual(price, type.GetField("price").GetValue(offer));
                }
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                for (int i = 0; i < 7000; i++)
                {
                    type.GetMethod("AdvanceAfterPurchase").Invoke(offer, null);
                    string id = (string)type.GetField("itemId").GetValue(offer);
                    counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
                }
                CollectionAssert.AreEquivalent(new[] { "advanced_spellbook", "earth_scroll", "earth_spellbook", "fire_scroll", "ice_spellbook", "light_scroll", "thunder_scroll" }, counts.Keys);
                foreach (var count in counts.Values) Assert.That(count, Is.InRange(850, 1150));
                type.GetField("price").SetValue(offer, 267);
                var saveType = Find("TheLastKnight.Core.PlayerSaveData");
                var save = Activator.CreateInstance(saveType);
                saveType.GetField("randomMerchantOffer").SetValue(save, offer);
                var copy = saveType.GetMethod("Copy").Invoke(save, null);
                var copiedOffer = saveType.GetField("randomMerchantOffer").GetValue(copy);
                Assert.AreEqual(type.GetField("itemId").GetValue(offer), type.GetField("itemId").GetValue(copiedOffer));
                Assert.AreEqual(267, type.GetField("price").GetValue(copiedOffer));
            }
            finally { UnityEngine.Random.state = randomState; }
        }

        [Test]
        public void Purchase_ChargesDisplayedPrice_AddsOneItem_AndOnlyAdvancesOnSuccess()
        {
            var owner = new GameObject("RandomMerchantTest");
            owner.SetActive(false);
            var playerObject = new GameObject("RandomMerchantPlayer");
            var shopType = Find("TheLastKnight.UI.ShopUI");
            var inventoryType = Find("TheLastKnight.Inventory.InventoryManager");
            var playerType = Find("TheLastKnight.Stats.PlayerStats");
            var randomState = UnityEngine.Random.state;
            object offer = null;
            string previousItem = null;
            int previousPrice = 200;
            try
            {
                var shop = owner.AddComponent(shopType);
                var inventory = owner.AddComponent(inventoryType);
                var player = playerObject.AddComponent(playerType);
                playerType.GetField("_gold", Private).SetValue(player, 1000);
                shopType.GetMethod("SyncRandomOffer", Private).Invoke(shop, null);
                offer = shopType.GetProperty("RandomOffer", Private).GetValue(shop);
                var offerType = offer.GetType();
                previousItem = (string)offerType.GetField("itemId").GetValue(offer);
                previousPrice = (int)offerType.GetField("price").GetValue(offer);
                offerType.GetField("price").SetValue(offer, 200);
                offerType.GetField("itemId").SetValue(offer, "earth_scroll");
                var buy = shopType.GetMethod("TryBuyRandomOffer", Private);
                Assert.IsTrue((bool)buy.Invoke(shop, new object[] { player, inventory }));
                Assert.AreEqual(800, playerType.GetProperty("Gold").GetValue(player));
                Assert.AreEqual(1, inventoryType.GetMethod("CountItem").Invoke(inventory, new object[] { "earth_scroll" }));
                Assert.AreEqual(220, offerType.GetField("price").GetValue(offer));
                string next = (string)offerType.GetField("itemId").GetValue(offer);
                shopType.GetMethod("SyncRandomOffer", Private).Invoke(shop, null);
                Assert.AreEqual(next, offerType.GetField("itemId").GetValue(offer), "Reopening must keep the same offer.");
                playerType.GetField("_gold", Private).SetValue(player, 0);
                Assert.IsFalse((bool)buy.Invoke(shop, new object[] { player, inventory }));
                Assert.AreEqual(next, offerType.GetField("itemId").GetValue(offer));
                Assert.AreEqual(220, offerType.GetField("price").GetValue(offer));
                playerType.GetField("_gold", Private).SetValue(player, 800);
                var item = Find("TheLastKnight.Inventory.ItemRegistry").GetMethod("CreateItem").Invoke(null, new object[] { "moonstone_shard", 64 });
                foreach (string fieldName in new[] { "_inventorySlots", "_quickSlots" })
                {
                    var slots = (Array)inventoryType.GetField(fieldName, Private).GetValue(inventory);
                    for (int i = 0; i < slots.Length; i++) slots.SetValue(item, i);
                }
                Assert.IsFalse((bool)buy.Invoke(shop, new object[] { player, inventory }));
                Assert.AreEqual(800, playerType.GetProperty("Gold").GetValue(player));
                Assert.AreEqual(next, offerType.GetField("itemId").GetValue(offer));
                Assert.AreEqual(220, offerType.GetField("price").GetValue(offer));
            }
            finally
            {
                if (offer != null)
                {
                    offer.GetType().GetField("itemId").SetValue(offer, previousItem);
                    offer.GetType().GetField("price").SetValue(offer, previousPrice);
                }
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }
    }
}
