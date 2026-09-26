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
        public void OpenPauseMenu_OpensPanel_WithExpectedButtons()
        {
            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);
            var isOpen = (bool)_pauseMenu.GetType().GetProperty("IsOpen").GetValue(_pauseMenu);
            Assert.That(isOpen, Is.True);

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);

            Assert.That(buttonNames.Any(n => n.Contains("Resume") && n.Contains("เล่นต่อ")), Is.True, "Resume button must exist");
            Assert.That(buttonNames.Any(n => n.Contains("Settings") && n.Contains("ตั้งค่า")), Is.True, "Settings button must exist");
            Assert.That(buttonNames.Any(n => n.Contains("Main Menu") && (n.Contains("เมนูหลัก") || n.Contains("Main Menu"))), Is.True, "Main menu button must exist");
        }

        [Test]
        public void ResumeGame_ClosesPanel()
        {
            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);
            var isOpen = (bool)_pauseMenu.GetType().GetProperty("IsOpen").GetValue(_pauseMenu);
            Assert.That(isOpen, Is.True);

            _pauseMenu.GetType().GetMethod("ResumeGame")?.Invoke(_pauseMenu, null);
            isOpen = (bool)_pauseMenu.GetType().GetProperty("IsOpen").GetValue(_pauseMenu);
            Assert.That(isOpen, Is.False);
        }

        [Test]
        public void ShowSettings_CreatesAudioSliders_AndBackButton()
        {
            _pauseMenu.GetType().GetMethod("OpenPauseMenu")?.Invoke(_pauseMenu, null);

            var showSettings = _pauseMenu.GetType().GetMethod("ShowSettings",
                BindingFlags.NonPublic | BindingFlags.Instance);
            showSettings.Invoke(_pauseMenu, null);

            var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);
            var sliderNames = System.Array.ConvertAll(sliders, s => s.name);

            Assert.That(sliderNames.Any(n => n.Contains("Master")), Is.True, "Master slider must exist");
            Assert.That(sliderNames.Any(n => n.Contains("Music")), Is.True, "Music slider must exist");
            Assert.That(sliderNames.Any(n => n.Contains("Sound Effects") || n.Contains("Sound effects")), Is.True, "Sound effects slider must exist");

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);
            Assert.That(buttonNames.Any(n => n.Contains("Back") && n.Contains("ย้อนกลับ")), Is.True, "Back button must exist");
        }
    }
}
