using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using TheLastKnight.Inventory;
using TheLastKnight.UI;

// Exercises the real generated window through input actions and the UI module.
public static class InventoryPointerCheck
{
    public static IEnumerator Run()
    {
        var holder = new GameObject("InventoryPointerCheck_UI");
        var cameraObject = new GameObject("InventoryPointerCheck_Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = new Vector3(0, 0, -10);
        var texture = new RenderTexture(1280, 720, 24);
        camera.targetTexture = texture;
        var previousMouse = Mouse.current;
        var mouse = InputSystem.AddDevice<Mouse>();
        EventSystem eventSystem = null;
        var previousEvents = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        try
        {
            var ui = holder.AddComponent<CharacterStatusUI>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterStatusUI).GetMethod("BuildUI", flags).Invoke(ui, null);
            ui.CanvasObject.SetActive(true);
            ui.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            ui.Canvas.worldCamera = camera;
            ui.Canvas.planeDistance = 1f;
            eventSystem = EventSystem.current;
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            module.point.action.Disable();
            module.leftClick.action.Disable();
            typeof(CharacterStatusUI).GetMethod("EnsureEventSystem", flags).Invoke(ui, null);
            Check(module.point.action.enabled && module.leftClick.action.enabled, "UI pointer actions remained disabled after opening the window");
            typeof(EventSystem).GetMethod("OnApplicationFocus", flags).Invoke(eventSystem, new object[] { true });
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var slots = ui.GetComponentsInChildren<InventorySlotUI>();
            var source = slots.First(s => s.slotType == SlotType.QuickSlot && s.slotIndex == 0);
            var target = slots.First(s => s.slotType == SlotType.Inventory && s.slotIndex == 0);
            var inv = InventoryManager.Instance;
            inv.InitializeDefaultInventory();
            var from = RectTransformUtility.WorldToScreenPoint(camera, source.transform.position);
            var to = RectTransformUtility.WorldToScreenPoint(camera, target.transform.position);
            Action<Vector2, bool> send = (position, pressed) =>
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
                InputSystem.Update();
                module.Process();
            };
            send(from, false);
            send(from, true);
            send(from + new Vector2(30, 0), true);
            send(to, true);
            send(to, false);
            Check(inv.GetSlot(SlotType.QuickSlot, 0) == null, "Real input did not pick up the potion");
            Check(inv.GetSlot(SlotType.Inventory, 0)?.count == 3 && inv.CursorHeldItem == null, "Real input did not drop three potions into the bag");
            // Verify click-to-pick and click-to-place through the same module.
            send(to, true); send(to, false);
            Check(inv.CursorHeldItem?.count == 3, "Click-to-pick did not receive input");
            send(from, false); send(from, true); send(from, false);
            Check(inv.GetSlot(SlotType.QuickSlot, 0)?.count == 3 && inv.CursorHeldItem == null, "Click-to-place did not receive input");
        }
        finally
        {
            InputSystem.RemoveDevice(mouse);
            if (previousMouse != null) previousMouse.MakeCurrent();
            UnityEngine.Object.DestroyImmediate(holder);
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            if (eventSystem != null && !previousEvents.Contains(eventSystem)) UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
