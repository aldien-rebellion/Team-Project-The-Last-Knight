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
    }
}
