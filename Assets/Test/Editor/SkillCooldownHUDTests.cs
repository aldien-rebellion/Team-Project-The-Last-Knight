using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.Tests
{
    public class SkillCooldownHUDTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        private GameObject _hudObj;
        private Component _hud;
        private Type _hudType;
        private Type _playerType;

        private static object GetProp(object target, string prop) => target.GetType().GetProperty(prop).GetValue(target);
        private static void SetProp(object target, string prop, object val) => target.GetType().GetProperty(prop).SetValue(target, val);
        private static object GetField(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static void Invoke(Component target, string method, params object[] args)
        {
            var types = args != null && args.Length > 0 ? args.Select(a => a.GetType()).ToArray() : Type.EmptyTypes;
            var m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, types, null)
                 ?? target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m.Invoke(target, args != null && args.Length == 0 ? null : args);
        }

        [SetUp]
        public void SetUp()
        {
            _hudType = RuntimeType("TheLastKnight.UI.SkillCooldownHUD");
            _playerType = RuntimeType("TheLastKnight.Player.PlayerController");

            _hudObj = new GameObject("Test_SkillCooldownHUD");
            _hud = _hudObj.AddComponent(_hudType);
            Invoke(_hud, "Awake");
            Invoke(_hud, "SetVisible", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudObj != null)
            {
                UnityEngine.Object.DestroyImmediate(_hudObj);
            }
        }

        [Test]
        public void HUD_Initializes_WithThreeSkillSlots_AtBottomLeftCorner()
        {
            var canvas = GetProp(_hud, "Canvas") as Canvas;
            var canvasObj = GetProp(_hud, "CanvasObject") as GameObject;

            Assert.IsNotNull(canvas, "Canvas must be created");
            Assert.IsNotNull(canvasObj, "CanvasObject must be created");

            var panel = canvasObj.transform.Find("SkillCooldown_Panel") as RectTransform;
            Assert.IsNotNull(panel, "Panel must exist under Canvas");

            Assert.AreEqual(Vector2.zero, panel.anchorMin, "Panel must be anchored to bottom-left (anchorMin = 0,0)");
            Assert.AreEqual(Vector2.zero, panel.anchorMax, "Panel must be anchored to bottom-left (anchorMax = 0,0)");
            Assert.AreEqual(Vector2.zero, panel.pivot, "Panel pivot must be bottom-left (0,0)");
            Assert.AreEqual(new Vector2(24f, 24f), panel.anchoredPosition, "Panel must be positioned at bottom-left corner with offset");

            var slotsList = GetProp(_hud, "Slots") as IList;
            Assert.IsNotNull(slotsList);
            Assert.AreEqual(3, slotsList.Count, "HUD must have exactly 3 skill slots");

            string s0 = GetField(slotsList[0], "SkillName") as string;
            string s1 = GetField(slotsList[1], "SkillName") as string;
            string s2 = GetField(slotsList[2], "SkillName") as string;

            Assert.AreEqual("Carnage Burst", s0);
            Assert.AreEqual("Berserk Buff", s1);
            Assert.AreEqual("Excalibur", s2);
        }

        [Test]
        public void SkillSlots_HaveRadialFillShadowOverlay_ConfiguredForClockRotation()
        {
            var slotsList = GetProp(_hud, "Slots") as IList;
            for (int i = 0; i < slotsList.Count; i++)
            {
                var slot = slotsList[i];
                var shadow = GetField(slot, "ShadowOverlay") as Image;

                Assert.IsNotNull(shadow, $"Slot {i} must have a shadow overlay image");
                Assert.IsNotNull(shadow.sprite, $"Slot {i} shadow overlay must have a sprite assigned for Filled mode");
                Assert.AreEqual(Image.Type.Filled, shadow.type, $"Slot {i} shadow must use Image.Type.Filled");
                Assert.AreEqual(Image.FillMethod.Radial360, shadow.fillMethod, $"Slot {i} shadow must use Radial360 fill method");
                Assert.AreEqual((int)Image.Origin360.Top, shadow.fillOrigin, $"Slot {i} shadow fill origin must be Top (12 o'clock)");
                Assert.IsFalse(shadow.fillClockwise, $"Slot {i} shadow fillClockwise must be false for clockwise reveal/rotation");
                Assert.IsFalse(shadow.raycastTarget, $"Slot {i} shadow must not block raycasts");
            }
        }

        [Test]
        public void SkillCooldown_UpdatesFillAmount_AndText_DuringCooldown()
        {
            var slotsList = GetProp(_hud, "Slots") as IList;
            var slot0 = slotsList[0];
            var updateMethod = slot0.GetType().GetMethod("UpdateCooldown");

            // Cooldown active: 3.5s out of 5.0s => 70% fill
            updateMethod.Invoke(slot0, new object[] { 3.5f, 5.0f });

            var shadow = GetField(slot0, "ShadowOverlay") as Image;
            var cdText = GetField(slot0, "CooldownText");
            string textValue = cdText.GetType().GetProperty("text").GetValue(cdText) as string;
            var icon = GetField(slot0, "IconImage") as Image;

            Assert.IsTrue(shadow.gameObject.activeSelf, "Shadow overlay must be active during cooldown");
            Assert.AreEqual(0.7f, shadow.fillAmount, 0.01f, "Shadow fillAmount must match remaining cooldown ratio");
            Assert.AreEqual("3.5", textValue, "Cooldown text must show remaining seconds");
            Assert.Less(icon.color.r, 1f, "Icon should be dimmed during cooldown");

            // Long cooldown: 15.2s out of 20s => rounded to whole integer
            updateMethod.Invoke(slot0, new object[] { 15.2f, 20.0f });
            textValue = cdText.GetType().GetProperty("text").GetValue(cdText) as string;
            Assert.AreEqual("16", textValue, "Cooldown >= 10s should display whole seconds");
        }

        [Test]
        public void SkillCooldown_ClearsWhenCooldownReachesZero()
        {
            var slotsList = GetProp(_hud, "Slots") as IList;
            var slot0 = slotsList[0];
            var updateMethod = slot0.GetType().GetMethod("UpdateCooldown");

            // Start cooldown
            updateMethod.Invoke(slot0, new object[] { 2.0f, 5.0f });
            var shadow = GetField(slot0, "ShadowOverlay") as Image;
            Assert.IsTrue(shadow.gameObject.activeSelf);

            // Cooldown finished
            updateMethod.Invoke(slot0, new object[] { 0f, 5.0f });

            var cdText = GetField(slot0, "CooldownText");
            string textValue = cdText.GetType().GetProperty("text").GetValue(cdText) as string;
            var icon = GetField(slot0, "IconImage") as Image;

            Assert.IsFalse(shadow.gameObject.activeSelf, "Shadow overlay must be inactive when not on cooldown");
            Assert.AreEqual(0f, shadow.fillAmount, "Shadow fillAmount must be 0 when ready");
            Assert.AreEqual("", textValue, "Cooldown text must be empty when ready");
            Assert.AreEqual(Color.white, icon.color, "Icon must return to full brightness when ready");
        }

        [Test]
        public void SkillSlots_DisplayCorrectHotkeyBadges()
        {
            var slotsList = GetProp(_hud, "Slots") as IList;

            var b0 = GetField(slotsList[0], "KeyBadgeText");
            var b1 = GetField(slotsList[1], "KeyBadgeText");
            var b2 = GetField(slotsList[2], "KeyBadgeText");

            string t0 = b0.GetType().GetProperty("text").GetValue(b0) as string;
            string t1 = b1.GetType().GetProperty("text").GetValue(b1) as string;
            string t2 = b2.GetType().GetProperty("text").GetValue(b2) as string;

            Assert.AreEqual("E", t0, "Skill 1 badge must be 'E'");
            Assert.AreEqual("R", t1, "Skill 2 badge must be 'R'");
            Assert.AreEqual("T", t2, "Skill 3 badge must be 'T'");
        }

        [Test]
        public void LateUpdate_SynchronizesWithPlayerController()
        {
            GameObject playerGo = null;
            try
            {
                playerGo = new GameObject("TestPlayer");
                var player = playerGo.AddComponent(_playerType);
                SetProp(player, "SkillCooldownTimer", 2.5f);
                SetProp(player, "SkillCooldown", 5.0f);
                SetProp(player, "BuffCooldownTimer", 4.0f);
                SetProp(player, "BuffCooldown", 10.0f);
                SetProp(player, "ExcaliburCooldownTimer", 12.0f);
                SetProp(player, "ExcaliburCooldown", 25.0f);

                Invoke(_hud, "BindPlayer", player);
                Invoke(_hud, "LateUpdate");

                var slotsList = GetProp(_hud, "Slots") as IList;

                // Slot 0 (Carnage Burst)
                var s0 = GetField(slotsList[0], "ShadowOverlay") as Image;
                var txt0 = GetField(slotsList[0], "CooldownText");
                string val0 = txt0.GetType().GetProperty("text").GetValue(txt0) as string;

                Assert.IsTrue(s0.gameObject.activeSelf);
                Assert.Greater(s0.fillAmount, 0f);
                Assert.AreEqual("2.5", val0);

                // Slot 1 (Buff)
                var s1 = GetField(slotsList[1], "ShadowOverlay") as Image;
                var txt1 = GetField(slotsList[1], "CooldownText");
                string val1 = txt1.GetType().GetProperty("text").GetValue(txt1) as string;

                Assert.IsTrue(s1.gameObject.activeSelf);
                Assert.Greater(s1.fillAmount, 0f);
                Assert.AreEqual("4.0", val1);

                // Slot 2 (Excalibur)
                var s2 = GetField(slotsList[2], "ShadowOverlay") as Image;
                var txt2 = GetField(slotsList[2], "CooldownText");
                string val2 = txt2.GetType().GetProperty("text").GetValue(txt2) as string;

                Assert.IsTrue(s2.gameObject.activeSelf);
                Assert.Greater(s2.fillAmount, 0f);
                Assert.AreEqual("12", val2);
            }
            finally
            {
                if (playerGo != null) UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void SkillCooldowns_MatchInspectorSettings_WithoutDurationInflation()
        {
            GameObject playerGo = null;
            try
            {
                playerGo = new GameObject("TestPlayer_CDMatch");
                var player = playerGo.AddComponent(_playerType);
                SetProp(player, "SkillCooldown", 5.0f);
                SetProp(player, "BuffCooldown", 30.0f);
                SetProp(player, "ExcaliburCooldown", 15.0f);

                float sMax = (float)GetProp(player, "SkillMaxCooldown");
                float bMax = (float)GetProp(player, "BuffMaxCooldown");
                float eMax = (float)GetProp(player, "ExcaliburMaxCooldown");

                Assert.AreEqual(5.0f, sMax, 0.001f, "SkillMaxCooldown must exactly equal SkillCooldown inspector value");
                Assert.AreEqual(30.0f, bMax, 0.001f, "BuffMaxCooldown must exactly equal BuffCooldown inspector value");
                Assert.AreEqual(15.0f, eMax, 0.001f, "ExcaliburMaxCooldown must exactly equal ExcaliburCooldown inspector value");
            }
            finally
            {
                if (playerGo != null) UnityEngine.Object.DestroyImmediate(playerGo);
            }
        }
    }
}
