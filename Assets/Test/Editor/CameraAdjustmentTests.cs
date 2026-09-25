using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class CameraAdjustmentTests
    {
        private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

        private static object GetProp(object target, string prop) =>
            target.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        private static void SetProp(object target, string prop, object value) =>
            target.GetType().GetProperty(prop, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);

        private static void Invoke(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

        [Test]
        public void CameraFollow_FeetFraming_PositionsFeetAtTwentyPercentAboveBottom()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            var targetGo = new GameObject("TestPlayer");
            targetGo.transform.position = new Vector3(0f, 2f, 0f);
            var col = targetGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 2f);
            col.offset = new Vector2(0f, 1f); // feet at targetGo.position.y (2.0f)

            try
            {
                Invoke(follow, "SetTarget", targetGo.transform);
                SetProp(follow, "UseFeetFraming", true);
                SetProp(follow, "TargetViewportY", 0.20f);
                Invoke(follow, "SnapTo", targetGo.transform.position);

                Vector3 feetPos = new Vector3(targetGo.transform.position.x, col.bounds.min.y, targetGo.transform.position.z);
                Vector3 vp = cam.WorldToViewportPoint(feetPos);

                Assert.That(vp.y, Is.EqualTo(0.20f).Within(0.01f), "Player's feet should be 20% above bottom edge of camera viewport");
                Assert.That(vp.x, Is.EqualTo(0.50f).Within(0.01f), "Player should be horizontally centered when idle");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void CameraFollow_ZoomOut_SupportsUpTo2Point5x()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                SetProp(follow, "EnableZoom", true);
                SetProp(follow, "MaxZoomMultiplier", 2.5f);
                Invoke(follow, "SetZoomMultiplier", 2.5f);

                float targetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(targetSize, Is.EqualTo(12.5f).Within(0.01f), "Target orthographic size should be 2.5x of base size 5");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraFollow_FeetFraming_MaintainsTwentyPercentAt2Point5xZoom()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            var targetGo = new GameObject("TestPlayer");
            targetGo.transform.position = new Vector3(0f, 0f, 0f);
            var col = targetGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 2f);
            col.offset = new Vector2(0f, 1f); // feet at y=0

            try
            {
                Invoke(follow, "SetTarget", targetGo.transform);
                SetProp(follow, "UseFeetFraming", true);
                SetProp(follow, "TargetViewportY", 0.20f);

                // Zoom to 2.5x
                SetProp(follow, "MaxZoomMultiplier", 2.5f);
                Invoke(follow, "SetZoomMultiplier", 2.5f);
                cam.orthographicSize = 12.5f;
                Invoke(follow, "SnapTo", targetGo.transform.position);

                Vector3 feetPos = new Vector3(targetGo.transform.position.x, col.bounds.min.y, targetGo.transform.position.z);
                Vector3 vp = cam.WorldToViewportPoint(feetPos);

                Assert.That(vp.y, Is.EqualTo(0.20f).Within(0.01f), "Player's feet should remain at 20% viewport height even when zoomed out 2.5x");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void CameraFollow_LookAhead_ShiftsForwardWhenMoving_AndReturnsWhenIdle()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                SetProp(follow, "EnableLookAhead", true);
                SetProp(follow, "LookAheadDistance", 2.5f);
                SetProp(follow, "LookAheadSmoothTime", 0.3f);
                SetProp(follow, "LookAheadSpeedThreshold", 0.5f);

                // 1. Simulate moving right (speed = +8) over multiple updates
                for (int i = 0; i < 20; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", 8.0f, 0.05f);
                }
                float lookAheadRight = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadRight, Is.GreaterThan(1.5f), "Look-ahead should lead forward to the right when moving right");

                // 2. Simulate moving left (speed = -8) over multiple updates
                for (int i = 0; i < 40; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", -8.0f, 0.05f);
                }
                float lookAheadLeft = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadLeft, Is.LessThan(-1.5f), "Look-ahead should lead forward to the left when moving left");

                // 3. Simulate standing still / idle (speed = 0) over multiple updates
                for (int i = 0; i < 40; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", 0f, 0.05f);
                }
                float lookAheadIdle = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadIdle, Is.EqualTo(0f).Within(0.05f), "Look-ahead should smoothly return to center (0) when idle");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraFollow_Boundaries_ClampsCameraInsideBoundaryBox()
        {
            var confinerGo = new GameObject("Boundary");
            var box = confinerGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(100f, 50f);
            confinerGo.transform.position = Vector3.zero;

            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                Invoke(follow, "SetBoundaries", box);
                // Attempt to snap camera far outside the boundary (e.g. X = 200)
                Invoke(follow, "SnapTo", new Vector3(200f, 0f, 0f));

                float camWidth = 5f * cam.aspect;
                float expectedMaxX = box.bounds.max.x - camWidth;

                Assert.That(camGo.transform.position.x, Is.EqualTo(expectedMaxX).Within(0.01f), "Camera should be clamped within boundary");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(confinerGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [TestCase("CityCenter", true)]
        [TestCase("Church", true)]
        [TestCase("OutdoorMarket", true)]
        [TestCase("SuburbToForest", true)]
        [TestCase("DemonCastleEntrance", true)]
        [TestCase("DemonCastle", false)]
        public void SceneMaps_BoundaryConfiguration_MatchesRequirement(string sceneName, bool shouldHaveBoundaries)
        {
            string path = "Assets/Scenes/Maps/" + sceneName + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            try
            {
                var cam = UnityEngine.Camera.main;
                Assert.That(cam, Is.Not.Null, $"Camera should exist in scene {sceneName}");
                var follow = cam.GetComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));
                Assert.That(follow, Is.Not.Null, $"CameraFollow2D should exist in scene {sceneName}");

                bool useBounds = (bool)GetProp(follow, "UseBoundaries");
                Assert.That(useBounds, Is.EqualTo(shouldHaveBoundaries),
                    $"Scene {sceneName} UseBoundaries should be {shouldHaveBoundaries}");

                var boundaryBox = GetProp(follow, "BoundaryBox");
                if (shouldHaveBoundaries)
                {
                    Assert.That(boundaryBox, Is.Not.Null,
                        $"Scene {sceneName} should have BoundaryBox assigned");
                }
                else
                {
                    Assert.That(boundaryBox, Is.Null,
                        $"Scene {sceneName} should NOT have BoundaryBox assigned (demoncastle excluded)");
                }
            }
            finally
            {
                // clean up
            }
        }
    }
}
