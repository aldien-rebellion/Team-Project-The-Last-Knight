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
        public void CameraFollow_Zoom_SupportsBoth0Point5xAnd2x()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                SetProp(follow, "EnableZoom", true);
                SetProp(follow, "MinZoomMultiplier", 0.5f);
                SetProp(follow, "MaxZoomMultiplier", 2.0f);

                // Test zoom in down to 0.5x
                Invoke(follow, "SetZoomMultiplier", 0.5f);
                float minTargetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(minTargetSize, Is.EqualTo(2.5f).Within(0.01f), "Target orthographic size should be 0.5x (2.5) of base size 5");

                // Test zoom out up to 2.0x
                Invoke(follow, "SetZoomMultiplier", 2.0f);
                float maxTargetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(maxTargetSize, Is.EqualTo(10.0f).Within(0.01f), "Target orthographic size should be 2.0x (10.0) of base size 5");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraFollow_FeetFraming_MaintainsTwentyPercentAcrossZoomLevels()
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
                SetProp(follow, "MinZoomMultiplier", 0.5f);
                SetProp(follow, "MaxZoomMultiplier", 2.0f);

                Vector3 feetPos = new Vector3(targetGo.transform.position.x, col.bounds.min.y, targetGo.transform.position.z);

                // 1. Zoom In 0.5x (size = 2.5)
                Invoke(follow, "SetZoomMultiplier", 0.5f);
                cam.orthographicSize = 2.5f;
                Invoke(follow, "SnapTo", targetGo.transform.position);
                Vector3 vpIn = cam.WorldToViewportPoint(feetPos);
                Assert.That(vpIn.y, Is.EqualTo(0.20f).Within(0.01f), "Feet should be at 20% viewport Y at 0.5x zoom");

                // 2. Normal 1.0x (size = 5.0)
                Invoke(follow, "SetZoomMultiplier", 1.0f);
                cam.orthographicSize = 5.0f;
                Invoke(follow, "SnapTo", targetGo.transform.position);
                Vector3 vpNormal = cam.WorldToViewportPoint(feetPos);
                Assert.That(vpNormal.y, Is.EqualTo(0.20f).Within(0.01f), "Feet should be at 20% viewport Y at 1.0x zoom");

                // 3. Zoom Out 2.0x (size = 10.0)
                Invoke(follow, "SetZoomMultiplier", 2.0f);
                cam.orthographicSize = 10.0f;
                Invoke(follow, "SnapTo", targetGo.transform.position);
                Vector3 vpOut = cam.WorldToViewportPoint(feetPos);
                Assert.That(vpOut.y, Is.EqualTo(0.20f).Within(0.01f), "Feet should be at 20% viewport Y at 2.0x zoom");
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
                SetProp(follow, "LookAheadDistance", 4.0f);
                SetProp(follow, "LookAheadSmoothTime", 1.2f);
                SetProp(follow, "LookAheadReturnSmoothTime", 2.0f);
                SetProp(follow, "LookAheadSpeedThreshold", 0.5f);

                // 1. Simulate moving right (speed = +8) over multiple updates (3 seconds)
                for (int i = 0; i < 60; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", 8.0f, 0.05f);
                }
                float lookAheadRight = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadRight, Is.GreaterThan(2.5f), "Look-ahead should noticeably shift forward to the right when moving right");

                // 2. Simulate moving left (speed = -8) over multiple updates (5 seconds)
                for (int i = 0; i < 100; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", -8.0f, 0.05f);
                }
                float lookAheadLeft = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadLeft, Is.LessThan(-2.5f), "Look-ahead should noticeably shift forward to the left when moving left");

                // 3. Simulate standing still / idle (speed = 0) over multiple updates (6 seconds)
                for (int i = 0; i < 120; i++)
                {
                    Invoke(follow, "SimulateMovementForTesting", 0f, 0.05f);
                }
                float lookAheadIdle = (float)GetProp(follow, "CurrentLookAheadX");
                Assert.That(lookAheadIdle, Is.EqualTo(0f).Within(0.08f), "Look-ahead should smoothly return to center (0) when idle");
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

        [Test]
        public void CameraFollow_Boundaries_ClampsOrthographicSize_WhenBoundarySmallerThanCameraView()
        {
            var confinerGo = new GameObject("Boundary");
            var box = confinerGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(20.48f, 11.42f);
            confinerGo.transform.position = new Vector3(0f, 2.09f, 0f);

            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f; // Greater than boundary allows (especially with aspect > 1.7)
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                Invoke(follow, "SetBoundaries", box);

                float maxAllowed = (float)InvokeWithReturn(follow, "GetMaxAllowedOrthographicSize");
                Assert.That(maxAllowed, Is.LessThan(6f), "Max allowed size should be smaller than 6 to fit boundary");

                float targetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(targetSize, Is.LessThanOrEqualTo(maxAllowed + 0.001f), "Target size must be clamped to maxAllowed");
                Assert.That(cam.orthographicSize, Is.LessThanOrEqualTo(maxAllowed + 0.001f), "Camera orthographic size must be clamped to maxAllowed");

                // Snap camera across various positions and verify viewport edges never exceed boundary
                Vector3[] testPositions = new Vector3[]
                {
                    new Vector3(-50f, 0f, 0f),
                    new Vector3(0f, 2f, 0f),
                    new Vector3(50f, 10f, 0f)
                };

                Bounds b = box.bounds;
                foreach (var pos in testPositions)
                {
                    Invoke(follow, "SnapTo", pos);
                    float camW = cam.orthographicSize * cam.aspect;
                    float camH = cam.orthographicSize;
                    float left = camGo.transform.position.x - camW;
                    float right = camGo.transform.position.x + camW;
                    float bottom = camGo.transform.position.y - camH;
                    float top = camGo.transform.position.y + camH;

                    Assert.That(left, Is.GreaterThanOrEqualTo(b.min.x - 0.005f), $"Left edge at {pos} should not exceed boundary");
                    Assert.That(right, Is.LessThanOrEqualTo(b.max.x + 0.005f), $"Right edge at {pos} should not exceed boundary");
                    Assert.That(bottom, Is.GreaterThanOrEqualTo(b.min.y - 0.005f), $"Bottom edge at {pos} should not exceed boundary");
                    Assert.That(top, Is.LessThanOrEqualTo(b.max.y + 0.005f), $"Top edge at {pos} should not exceed boundary");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(confinerGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraFollow_Boundaries_LimitsZoomOut_ToBoundaryExtents()
        {
            var confinerGo = new GameObject("Boundary");
            var box = confinerGo.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(20.48f, 11.42f);
            confinerGo.transform.position = new Vector3(0f, 2.09f, 0f);

            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                Invoke(follow, "SetBoundaries", box);
                float maxAllowed = (float)InvokeWithReturn(follow, "GetMaxAllowedOrthographicSize");

                // Try zooming out to 2.0x (3.5 * 2 = 7.0), which exceeds boundary
                Invoke(follow, "SetZoomMultiplier", 2.0f);
                float targetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(targetSize, Is.LessThanOrEqualTo(maxAllowed + 0.001f), "Zoom out must be clamped to boundary maxAllowed");

                // Try zooming in to 0.5x (3.5 * 0.5 = 1.75), which fits inside boundary
                Invoke(follow, "SetZoomMultiplier", 0.5f);
                float minTargetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(minTargetSize, Is.EqualTo(1.75f).Within(0.01f), "Zoom in should still reach 0.5x when within boundary");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(confinerGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        private static object InvokeWithReturn(Component target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

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
