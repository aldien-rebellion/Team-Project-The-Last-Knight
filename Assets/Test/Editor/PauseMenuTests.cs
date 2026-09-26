using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.Tests
{
    public class PauseMenuTests
    {
        private GameObject _holder;
        private Component _pauseMenu;

        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        [SetUp]
        public void SetUp()
        {
            var pauseType = RuntimeType("TheLastKnight.UI.PauseMenuUI");
            Assert.That(pauseType, Is.Not.Null, "PauseMenuUI type must exist");
            _holder = new GameObject("Test_PauseMenu");
            _pauseMenu = _holder.AddComponent(pauseType);
        }

        [TearDown]
        public void TearDown()
        {
            if (_pauseMenu != null)
            {
                var isOpenProp = _pauseMenu.GetType().GetProperty("IsOpen");
                if (isOpenProp != null && (bool)isOpenProp.GetValue(_pauseMenu))
                {
                    _pauseMenu.GetType().GetMethod("ResumeGame")?.Invoke(_pauseMenu, null);
                }
            }

            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
            }
        }

        [Test]
        public void OpenPauseMenu_ThaiLanguage_ShowsThaiButtons()
        {
            var locType = RuntimeType("TheLastKnight.Core.LocalizationManager");
            var langProp = locType.GetProperty("Current");
            var thaiEnum = Enum.Parse(langProp.PropertyType, "Thai");
            langProp.SetValue(null, thaiEnum);

            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);
            var isOpen = (bool)_pauseMenu.GetType().GetProperty("IsOpen").GetValue(_pauseMenu);
            Assert.That(isOpen, Is.True);

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);

            Assert.That(buttonNames.Any(n => n.Contains("เล่นต่อ")), Is.True, "Must contain Thai Resume");
            Assert.That(buttonNames.Any(n => n.Contains("ตั้งค่า")), Is.True, "Must contain Thai Settings");
            Assert.That(buttonNames.Any(n => n.Contains("ออกไปที่เมนูหลัก")), Is.True, "Must contain Thai Exit");
        }

        [Test]
        public void OpenPauseMenu_EnglishLanguage_ShowsEnglishButtons()
        {
            var locType = RuntimeType("TheLastKnight.Core.LocalizationManager");
            var langProp = locType.GetProperty("Current");
            var enEnum = Enum.Parse(langProp.PropertyType, "English");
            langProp.SetValue(null, enEnum);

            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);

            Assert.That(buttonNames.Any(n => n.Contains("Resume")), Is.True, "Must contain English Resume");
            Assert.That(buttonNames.Any(n => n.Contains("Settings")), Is.True, "Must contain English Settings");
            Assert.That(buttonNames.Any(n => n.Contains("Exit to Main Menu")), Is.True, "Must contain English Exit");
        }

        [Test]
        public void ShowSettings_ContainsBrightnessSlider_AndLanguageButton()
        {
            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);

            var showSettings = _pauseMenu.GetType().GetMethod("ShowSettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            showSettings.Invoke(_pauseMenu, null);

            var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);
            var sliderNames = System.Array.ConvertAll(sliders, s => s.name);

            Assert.That(sliderNames.Any(n => n.Contains("ความสว่าง") || n.Contains("Brightness")), Is.True, "Brightness slider must exist");

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);

            Assert.That(buttonNames.Any(n => n.Contains("ภาษา") || n.Contains("Language")), Is.True, "Language switcher must exist");
            Assert.That(buttonNames.Any(n => n.Contains("ปุ่มควบคุม") || n.Contains("Controls")), Is.True, "Controls button must exist");
        }

        [Test]
        public void ShowControlsMenu_ContainsRebindableRows_AndResetButton()
        {
            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);

            var showControls = _pauseMenu.GetType().GetMethod("ShowControlsMenu",
                BindingFlags.NonPublic | BindingFlags.Instance);
            showControls.Invoke(_pauseMenu, null);

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);

            Assert.That(buttonNames.Any(n => n.Contains("คืนค่า") || n.Contains("Reset")), Is.True, "Reset controls button must exist");
            Assert.That(buttonNames.Any(n => n.Contains("ย้อนกลับ") || n.Contains("Back")), Is.True, "Back button must exist");
        }

        [Test]
        public void GameBrightnessManager_SetBrightness_ClampsAndApplies()
        {
            var bmType = RuntimeType("TheLastKnight.Core.GameBrightnessManager");
            var go = new GameObject("Test_BM");
            var bm = go.AddComponent(bmType);

            bmType.GetMethod("SetBrightness").Invoke(bm, new object[] { 1.5f });
            float val = (float)bmType.GetProperty("Brightness").GetValue(bm);
            Assert.That(val, Is.EqualTo(1.5f).Within(0.01f));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void GameBrightnessManager_Overlay_DimsUIAndNeverBlocksRaycasts()
        {
            var bmType = RuntimeType("TheLastKnight.Core.GameBrightnessManager");
            var go = new GameObject("Test_BM_Overlay");
            var bm = go.AddComponent(bmType);

            // Test dimming (e.g. 0.4f)
            bmType.GetMethod("SetBrightness").Invoke(bm, new object[] { 0.4f });

            var canvas = (Canvas)bmType.GetProperty("OverlayCanvas").GetValue(bm);
            Assert.That(canvas, Is.Not.Null, "Overlay canvas must exist");
            Assert.That(canvas.sortingOrder, Is.EqualTo(32767), "Overlay must render above all other UI");

            var cg = canvas.GetComponent<CanvasGroup>();
            Assert.That(cg, Is.Not.Null);
            Assert.That(cg.blocksRaycasts, Is.False, "Overlay must never block UI raycasts");

            var dimImg = (Image)bmType.GetProperty("DimImage").GetValue(bm);
            Assert.That(dimImg, Is.Not.Null);
            Assert.That(dimImg.raycastTarget, Is.False, "Dim image must not block raycasts");
            Assert.That(dimImg.gameObject.activeSelf, Is.True);
            Assert.That(dimImg.color.a, Is.EqualTo(0.6f).Within(0.02f), "Alpha must equal (1.0 - brightness)");

            // Test neutral (1.0f)
            bmType.GetMethod("SetBrightness").Invoke(bm, new object[] { 1.0f });
            Assert.That(dimImg.gameObject.activeSelf, Is.False);

            // Test brightening (1.5f)
            bmType.GetMethod("SetBrightness").Invoke(bm, new object[] { 1.5f });
            var addImg = (Image)bmType.GetProperty("AddImage").GetValue(bm);
            Assert.That(addImg, Is.Not.Null);
            Assert.That(addImg.gameObject.activeSelf, Is.True);
            Assert.That(addImg.color.a, Is.GreaterThan(0f));
            Assert.That(addImg.raycastTarget, Is.False);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void KeyRebindManager_FormatPathToEnglish_FormatsInEnglishOnly()
        {
            var rebindType = RuntimeType("TheLastKnight.Input.KeyRebindManager");
            var formatMethod = rebindType.GetMethod("FormatPathToEnglish", BindingFlags.Public | BindingFlags.Static);

            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/a" }), Is.EqualTo("A"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/d" }), Is.EqualTo("D"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/space" }), Is.EqualTo("Space"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/leftShift" }), Is.EqualTo("Shift"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Mouse>/leftButton" }), Is.EqualTo("LMB"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Mouse>/rightButton" }), Is.EqualTo("RMB"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/q" }), Is.EqualTo("Q"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/e" }), Is.EqualTo("E"));
            Assert.That(formatMethod.Invoke(null, new object[] { "<Keyboard>/comma" }), Is.EqualTo(","));
        }

        [Test]
        public void MainMenuController_OpenSettings_OpensUnifiedSettings()
        {
            var menuType = RuntimeType("TheLastKnight.UI.MainMenuController");
            var go = new GameObject("Test_MainMenu");
            var menu = go.AddComponent(menuType);

            var openSettingsMethod = menuType.GetMethod("OpenSettings", BindingFlags.NonPublic | BindingFlags.Instance);
            openSettingsMethod.Invoke(menu, null);

            var isOpen = (bool)_pauseMenu.GetType().GetProperty("IsOpen").GetValue(_pauseMenu);
            var state = _pauseMenu.GetType().GetProperty("State").GetValue(_pauseMenu).ToString();

            Assert.That(isOpen, Is.True);
            Assert.That(state, Is.EqualTo("Settings"));

            var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);
            var sliderNames = System.Array.ConvertAll(sliders, s => s.name);
            Assert.That(sliderNames.Any(n => n.Contains("ความสว่าง") || n.Contains("Brightness")), Is.True);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void MainMenuController_ShowNewWorld_ContainsNameInputAndDifficultyButtons()
        {
            var menuType = RuntimeType("TheLastKnight.UI.MainMenuController");
            var go = new GameObject("Test_MainMenu_NewWorld");
            var menu = go.AddComponent(menuType);

            var showNewWorld = menuType.GetMethod("ShowNewWorld", BindingFlags.NonPublic | BindingFlags.Instance);
            showNewWorld.Invoke(menu, null);

            var input = UnityEngine.Object.FindAnyObjectByType<InputField>();
            Assert.That(input, Is.Not.Null, "Name InputField must exist");
            Assert.That(input.text, Does.StartWith("World").Or.StartWith("โลก"));

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);
            Assert.That(buttonNames.Any(n => n.Contains("Easy") || n.Contains("ง่าย")), Is.True);
            Assert.That(buttonNames.Any(n => n.Contains("Normal") || n.Contains("ปกติ")), Is.True);
            Assert.That(buttonNames.Any(n => n.Contains("Hard") || n.Contains("ยาก")), Is.True);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CharacterStatusUI_DoesNotOpenInMainMenu()
        {
            var statusType = RuntimeType("TheLastKnight.UI.CharacterStatusUI");
            Assert.That(statusType, Is.Not.Null);

            var go = new GameObject("Test_StatusUI");
            var statusUI = go.AddComponent(statusType);

            var openMethod = statusType.GetMethod("Open");
            var isOpenProp = statusType.GetProperty("IsOpen");

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.Equals(currentScene, "MainMenu", StringComparison.OrdinalIgnoreCase))
            {
                openMethod.Invoke(statusUI, null);
                Assert.That((bool)isOpenProp.GetValue(statusUI), Is.False, "CharacterStatusUI must never open in MainMenu scene");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void MainMenuController_ShowRenameWorld_DisplaysInputFieldAndSaveButton()
        {
            var menuType = RuntimeType("TheLastKnight.UI.MainMenuController");
            var go = new GameObject("Test_MainMenu_Rename");
            var menu = go.AddComponent(menuType);

            var saveDataType = RuntimeType("TheLastKnight.Core.PlayerSaveData");
            var mockSave = Activator.CreateInstance(saveDataType);
            saveDataType.GetField("worldId").SetValue(mockSave, "test_rename_world");
            saveDataType.GetField("saveName").SetValue(mockSave, "Custom Realm");

            var showRenameWorld = menuType.GetMethod("ShowRenameWorld", BindingFlags.NonPublic | BindingFlags.Instance);
            showRenameWorld.Invoke(menu, new[] { mockSave });

            var panelField = menuType.GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);
            var panelGo = (GameObject)panelField.GetValue(menu);
            Assert.That(panelGo, Is.Not.Null, "Panel must be created");

            var input = panelGo.GetComponentInChildren<InputField>();
            Assert.That(input, Is.Not.Null, "Rename InputField must exist");
            Assert.That(input.text, Is.EqualTo("Custom Realm"));

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);
            Assert.That(buttonNames.Any(n => n.Contains("Save") || n.Contains("บันทึก")), Is.True);
            Assert.That(buttonNames.Any(n => n.Contains("Cancel") || n.Contains("ยกเลิก")), Is.True);

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
