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

            Assert.That(buttonNames, Does.Contain("เล่นต่อ"));
            Assert.That(buttonNames, Does.Contain("ตั้งค่า"));
            Assert.That(buttonNames, Does.Contain("ออก (กลับไปที่ Main Menu)"));
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

            Assert.That(sliderNames, Does.Contain("Master"));
            Assert.That(sliderNames, Does.Contain("Music"));
            Assert.That(sliderNames, Does.Contain("Sound effects"));

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var buttonNames = System.Array.ConvertAll(buttons, b => b.name);
            Assert.That(buttonNames, Does.Contain("ย้อนกลับ"));
        }
    }
}
