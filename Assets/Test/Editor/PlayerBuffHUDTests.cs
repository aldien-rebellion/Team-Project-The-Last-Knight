using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.Tests
{
    public class PlayerBuffHUDTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        private GameObject _hudObj;
        private Component _hud;
        private Type _hudType;
        private Type _playerStatsType;
        private Type _characterStatusType;

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
            _hudType = RuntimeType("TheLastKnight.UI.PlayerBuffHUD");
            _playerStatsType = RuntimeType("TheLastKnight.Stats.PlayerStats");
            _characterStatusType = RuntimeType("TheLastKnight.UI.CharacterStatusUI");

            _hudObj = new GameObject("Test_PlayerBuffHUD");
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
        public void BuffHUD_Initializes_AtFarLeftEdge_BetweenTopHUD_AndSkillsHUD()
        {
            var canvas = GetProp(_hud, "Canvas") as Canvas;
            var canvasObj = GetProp(_hud, "CanvasObject") as GameObject;
            Assert.IsNotNull(canvas, "HUD Canvas must be created");
            Assert.IsNotNull(canvasObj, "HUD CanvasObject must be created");

            var containerRect = GetProp(_hud, "ContainerRect") as RectTransform;
            Assert.IsNotNull(containerRect, "Buff container RectTransform must exist");

            // Far-left edge anchoring
            Assert.AreEqual(new Vector2(0f, 1f), containerRect.anchorMin, "Container anchorMin must be top-left");
            Assert.AreEqual(new Vector2(0f, 1f), containerRect.anchorMax, "Container anchorMax must be top-left");
            Assert.AreEqual(new Vector2(0f, 1f), containerRect.pivot, "Container pivot must be top-left");

            // Positioned at X = 16 (far left edge) and Y = -140 (below HP/Stamina HUD which ends at ~-100)
            Assert.AreEqual(16f, containerRect.anchoredPosition.x, "Must be placed on the far left edge of the screen");
            Assert.AreEqual(-140f, containerRect.anchoredPosition.y, "Must start below the top HP/Stamina/Lv HUD");
        }

        [Test]
        public void BuffHUD_DisplaysMinecraftStyleCards_ForActiveBuffs()
        {
            GameObject playerObj = null;
            try
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
                playerObj = UnityEngine.Object.Instantiate(prefab);
                var stats = playerObj.GetComponent(_playerStatsType);
                Invoke(stats, "Awake");

                // Activate Skill 2 Buff (Iron Will) and Potion of Might
                Invoke(stats, "ApplySkill2Buff");
                Invoke(stats, "ApplyMightBuff");

                Invoke(_hud, "BindStats", stats);
                Invoke(_hud, "LateUpdate");

                var cardsList = GetProp(_hud, "Cards") as IList;
                Assert.IsNotNull(cardsList);
                Assert.GreaterOrEqual(cardsList.Count, 2, "Must create cards for the active buffs");

                // Verify first active card is visible and displays compact icon-only format
                var card0 = cardsList[0];
                var card0Root = GetField(card0, "Root") as GameObject;
                Assert.IsTrue(card0Root.activeSelf, "Active buff card must be active");

                var card0Rt = card0Root.GetComponent<RectTransform>();
                Assert.AreEqual(40f, card0Rt.sizeDelta.x, "Gameplay buff card must be compact square");

                var nameTextObj = GetField(card0, "NameText");
                string name = nameTextObj.GetType().GetProperty("text").GetValue(nameTextObj) as string;
                Assert.IsTrue(string.IsNullOrEmpty(name), "Gameplay buff card must show icon only without name text");

                var timeTextObj = GetField(card0, "TimeText");
                string timeStr = timeTextObj.GetType().GetProperty("text").GetValue(timeTextObj) as string;
                Assert.IsTrue(timeStr.Contains(":"), "Time format must be Minecraft style (m:ss e.g. 0:14)");

                var iconImg = GetField(card0, "IconImage") as Image;
                Assert.IsNotNull(iconImg, "Card must have icon image");
                Assert.IsNotNull(iconImg.sprite, "Card must have icon sprite assigned");
                Assert.IsTrue(iconImg.sprite.name.Contains("buff_") || iconImg.sprite.name.Contains("172"), "Must use icon from icon pack 128");
            }
            finally
            {
                if (playerObj != null) UnityEngine.Object.DestroyImmediate(playerObj);
            }
        }

        [Test]
        public void BuffHUD_ShowsTooltip_WithDetails_OnHover()
        {
            GameObject playerObj = null;
            try
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
                playerObj = UnityEngine.Object.Instantiate(prefab);
                var stats = playerObj.GetComponent(_playerStatsType);
                Invoke(stats, "Awake");
                Invoke(stats, "ApplySkill2Buff");

                Invoke(_hud, "BindStats", stats);
                Invoke(_hud, "LateUpdate");

                var cardsList = GetProp(_hud, "Cards") as IList;
                Assert.GreaterOrEqual(cardsList.Count, 1);

                var card0 = cardsList[0];
                Invoke(_hud, "ShowTooltipForCard", card0);

                var tooltipBox = GetField(_hud, "_tooltipBox") as GameObject;
                Assert.IsNotNull(tooltipBox);
                Assert.IsTrue(tooltipBox.activeSelf, "Tooltip box must be active on hover");

                var titleObj = GetField(_hud, "_txtTipTitle");
                string title = titleObj.GetType().GetProperty("text").GetValue(titleObj) as string;
                Assert.AreEqual("Iron Will", title, "Tooltip title must show buff name");

                var descObj = GetField(_hud, "_txtTipDesc");
                string desc = descObj.GetType().GetProperty("text").GetValue(descObj) as string;
                Assert.IsTrue(desc.Contains("+22%"), "Tooltip description must explain buff effect");

                // Hover exit
                Invoke(_hud, "HideTooltip");
                Assert.IsFalse(tooltipBox.activeSelf, "Tooltip box must hide on hover exit");
            }
            finally
            {
                if (playerObj != null) UnityEngine.Object.DestroyImmediate(playerObj);
            }
        }

        [Test]
        public void CharacterStatusUI_LeftBuffDock_IsDockedToWoodenFrame_ExpandingLeftwards()
        {
            GameObject statusObj = null;
            try
            {
                statusObj = new GameObject("Test_CharacterStatusUI_Buffs");
                var statusUI = statusObj.AddComponent(_characterStatusType);
                Invoke(statusUI, "Awake");

                var dock = GetProp(statusUI, "LeftBuffDock") as RectTransform;
                Assert.IsNotNull(dock, "CharacterStatusUI must have LeftBuffDock");

                // Docked to the left edge of the wooden window frame:
                // anchorMin = (0, 1), anchorMax = (0, 1)
                Assert.AreEqual(new Vector2(0f, 1f), dock.anchorMin, "Buff dock must anchor to top-left of wooden frame");
                Assert.AreEqual(new Vector2(0f, 1f), dock.anchorMax, "Buff dock must anchor to top-left of wooden frame");

                // pivot = (1, 1): right edge of the dock aligns with frame, expanding outwards to the left!
                Assert.AreEqual(new Vector2(1f, 1f), dock.pivot, "Buff dock pivot must be (1, 1) so it expands to the left");
                Assert.Less(dock.anchoredPosition.x, 0f, "Dock position X must be negative (outside/left of wooden frame)");
            }
            finally
            {
                if (statusObj != null) UnityEngine.Object.DestroyImmediate(statusObj);
            }
        }

        [Test]
        public void CharacterStatusUI_DisplaysBuffs_WithHoverTooltip()
        {
            GameObject statusObj = null;
            GameObject playerObj = null;
            try
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
                playerObj = UnityEngine.Object.Instantiate(prefab);
                var stats = playerObj.GetComponent(_playerStatsType);
                Invoke(stats, "Awake");
                Invoke(stats, "ApplySkill2Buff");
                Invoke(stats, "ApplyMightBuff");

                statusObj = new GameObject("Test_CharacterStatusUI_Buffs2");
                var statusUI = statusObj.AddComponent(_characterStatusType);
                Invoke(statusUI, "Awake");

                // Refresh with player stats
                Invoke(statusUI, "RefreshBuffDock", stats);

                var cardsList = GetProp(statusUI, "StatusBuffCards") as IList;
                Assert.IsNotNull(cardsList);
                Assert.GreaterOrEqual(cardsList.Count, 2, "Must create buff cards on character status wooden frame");

                var card0 = cardsList[0];
                var card0Root = GetField(card0, "Root") as GameObject;
                Assert.IsTrue(card0Root.activeSelf, "Status buff card must be active");

                var card0Rt = card0Root.GetComponent<RectTransform>();
                Assert.AreEqual(1f, card0Rt.pivot.x, "Buff card pivot.x must be 1 so it expands outwards to the left from the frame");

                var nameTextObj = GetField(card0, "NameText");
                string name = nameTextObj.GetType().GetProperty("text").GetValue(nameTextObj) as string;
                Assert.IsNotEmpty(name);

                var timeTextObj = GetField(card0, "TimeText");
                string timeStr = timeTextObj.GetType().GetProperty("text").GetValue(timeTextObj) as string;
                Assert.IsTrue(timeStr.Contains(":"), "Time format must be Minecraft style");
            }
            finally
            {
                if (playerObj != null) UnityEngine.Object.DestroyImmediate(playerObj);
                if (statusObj != null) UnityEngine.Object.DestroyImmediate(statusObj);
            }
        }
    }
}
