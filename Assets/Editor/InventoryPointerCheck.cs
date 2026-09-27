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
        var routine = RunCore();
        while (true)
        {
            bool next;
            try { next = routine.MoveNext(); }
            catch (Exception exception) { System.IO.File.WriteAllText("Temp/InventoryPointerCheck.txt", exception.ToString()); throw new Exception(exception.ToString()); }
            if (!next) yield break;
            yield return routine.Current;
        }
    }
    private static IEnumerator RunCore()
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
        UnityEngine.UI.GraphicRaycaster[] otherRaycasters = null;
        var previousEvents = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        try
        {
            var ui = CharacterStatusUI.Instance;
            if (ui == null) ui = holder.AddComponent<CharacterStatusUI>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(CharacterStatusUI).GetMethod("BuildUI", flags).Invoke(ui, null);
            ui.CanvasObject.SetActive(true);
            otherRaycasters = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject != ui.CanvasObject).ToArray();
            foreach (var raycaster in otherRaycasters) raycaster.enabled = false;
            ui.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            ui.Canvas.worldCamera = camera;
            ui.Canvas.planeDistance = 1f;
            eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (!Application.isPlaying) typeof(EventSystem).GetMethod("OnEnable", flags).Invoke(eventSystem, null);
            EventSystem.current = eventSystem;
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (!Application.isPlaying) typeof(InputSystemUIInputModule).GetMethod("OnEnable", flags).Invoke(module, null);
            module.AssignDefaultActions();
            module.ActivateModule();
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
            var trace = new System.Text.StringBuilder();
            Action<Vector2, bool> send = (position, pressed) =>
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, pressed));
                InputSystem.Update();
                module.Process();
                trace.AppendLine("mouse=" + mouse.position.ReadValue() + " enabled=" + module.point.action.enabled + " point=" + module.point.action.ReadValue<Vector2>() + " click=" + module.leftClick.action.ReadValue<float>() + " hover=" + module.GetLastRaycastResult(mouse.deviceId).gameObject + " held=" + inv.CursorHeldItem?.count);
            };
            send(from, false);
            send(from, true);
            send(from + new Vector2(30, 0), true);
            send(to, true);
            send(to, false);
            Check(inv.GetSlot(SlotType.QuickSlot, 0) == null, "Real input did not pick up the potion " + trace);
            Check(inv.GetSlot(SlotType.Inventory, 0)?.count == 3 && inv.CursorHeldItem == null, "Real input did not drop three potions into the bag");            source.UpdateDisplay(inv.GetSlot(SlotType.QuickSlot, 0));
            target.UpdateDisplay(inv.GetSlot(SlotType.Inventory, 0));
            ui.HideTooltip();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previousTarget = RenderTexture.active;
            RenderTexture.active = texture;
            var screenshot = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            screenshot.Apply();
            System.IO.File.WriteAllBytes("Temp/InventoryPointerCheck.png", screenshot.EncodeToPNG());
            RenderTexture.active = previousTarget;
            UnityEngine.Object.DestroyImmediate(screenshot);
            // Verify click-to-pick and click-to-place through the same module.
            send(to, true); send(to, false);
            Check(inv.CursorHeldItem?.count == 3, "Click-to-pick did not receive input");
            send(from, false); send(from, true); send(from, false);
            Check(inv.GetSlot(SlotType.QuickSlot, 0)?.count == 3 && inv.CursorHeldItem == null, "Click-to-place did not receive input");
            System.IO.File.WriteAllText("Temp/InventoryPointerCheck.txt", "PASS: actual CharacterStatusUI, InputSystem mouse events and InputSystemUIInputModule.Process. Disabled Point/Click recovered; drag moved 3 potions from quick slot to bag; click picked up and placed 3 potions back. " + trace);
        }
        finally
        {
            if (eventSystem != null)
            {
                var module = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (module != null) typeof(InputSystemUIInputModule).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(module, null);
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eventSystem, null);
            }
            if (otherRaycasters != null) foreach (var raycaster in otherRaycasters) if (raycaster != null) raycaster.enabled = true;
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
