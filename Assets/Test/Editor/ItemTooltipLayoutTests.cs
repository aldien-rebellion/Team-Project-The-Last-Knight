using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.Tests
{
    public class ItemTooltipLayoutTests
    {
        private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        [TestCase("ShopUI")]
        [TestCase("CharacterStatusUI")]
        public void AllItemDescriptionsAndHints_FitInsideTooltip(string uiName)
        {
            var owner = new GameObject("TooltipLayoutTest");
            owner.SetActive(false);
            var root = new GameObject("TooltipCanvas", typeof(Canvas));
            var cameraObject = new GameObject("TooltipCamera", typeof(Camera));
            var texture = new RenderTexture(1280, 720, 24);
            var previousRT = RenderTexture.active;
            Texture2D screenshot = null;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.18f, 0.22f);
                camera.targetTexture = texture;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                var type = Find("TheLastKnight.UI." + uiName);
                var ui = owner.AddComponent(type);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                if (uiName == "ShopUI") type.GetField("_panel", flags).SetValue(ui, root);
                else
                {
                    type.GetField("_canvasObject", flags).SetValue(ui, root);
                    type.GetField("_canvas", flags).SetValue(ui, canvas);
                }
                type.GetMethod("BuildTooltipBox", flags).Invoke(ui, new object[] { root.transform });
                var tooltip = (GameObject)type.GetField(uiName == "ShopUI" ? "_tooltipGo" : "_tooltipBox", flags).GetValue(ui);
                var rect = tooltip.GetComponent<RectTransform>();
                Action<string, string, string> show = (title, category, description) =>
                {
                    string hint = "<color=#98E498>[คลิกซ้าย: ย้าย / จัดกระเป๋า]</color>  <color=#85C1E9>[คลิกขวา: ใช้]</color>\nใช้ด้วยปุ่ม Q ไม่ได้";
                    type.GetMethod("ShowTooltip").Invoke(ui, uiName == "ShopUI"
                        ? new object[] { title, category, description + "\n" + hint }
                        : new object[] { title, category, description, hint });
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    CheckTextBounds(tooltip, title);
                };
                var items = (IEnumerable)Find("TheLastKnight.Inventory.ItemRegistry").GetMethod("GetAllItems").Invoke(null, null);
                foreach (var item in items)
                {
                    var itemType = item.GetType();
                    show((string)itemType.GetField("name").GetValue(item),
                        (string)itemType.GetField("typeName").GetValue(item),
                        (string)itemType.GetField("description").GetValue(item) + "\nCurrent Stack: 64 / 64\nMerchant does not buy this item");
                }
                show("รูนมือปีศาจ / A long item title that wraps onto a second line", "Rune / ไอเท็มพิเศษ",
                    "ผนึกปีศาจที่ได้จากจุดบันทึกในเมือง นำไปใช้แลกประตูปราสาท พ่อค้าไม่รับซื้อไอเท็มนี้\n" +
                    "A longer description must grow the frame when its text wraps. All instructions and stack information must remain inside the frame.\nCurrent Stack: 64 / 64");
                rect.anchoredPosition = new Vector2(-rect.rect.width * 0.5f, rect.rect.height * 0.5f);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = texture;
                screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                screenshot.Apply();
                System.IO.Directory.CreateDirectory("Temp");
                System.IO.File.WriteAllBytes("Temp/" + uiName + "TooltipLayout.png", screenshot.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousRT;
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                if (screenshot != null) UnityEngine.Object.DestroyImmediate(screenshot);
            }
        }

        private static void CheckTextBounds(GameObject tooltip, string itemName)
        {
            var frame = tooltip.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            foreach (var component in tooltip.GetComponentsInChildren<Component>())
            {
                if (component == null || component.GetType().FullName != "TMPro.TextMeshProUGUI") continue;
                var textRect = component.GetComponent<RectTransform>();
                float preferredHeight = (float)component.GetType().GetProperty("preferredHeight").GetValue(component);
                Assert.GreaterOrEqual(textRect.rect.height + 0.5f, preferredHeight, itemName + ": text height");
                textRect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = frame.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(frame.rect.xMin - 0.5f, frame.rect.xMax + 0.5f), itemName + ": width");
                    Assert.That(local.y, Is.InRange(frame.rect.yMin - 0.5f, frame.rect.yMax + 0.5f), itemName + ": height");
                }
            }
        }
    }
}
