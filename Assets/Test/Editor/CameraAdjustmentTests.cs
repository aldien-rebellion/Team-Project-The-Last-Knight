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
        public void CameraFollow_FeetFraming_PositionsFeetAtTenPercentAboveBottom()
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
                SetProp(follow, "TargetViewportY", 0.10f);
                Invoke(follow, "SnapTo", targetGo.transform.position);

                Vector3 feetPos = new Vector3(targetGo.transform.position.x, col.bounds.min.y, targetGo.transform.position.z);
                Vector3 vp = cam.WorldToViewportPoint(feetPos);

                Assert.That(vp.y, Is.EqualTo(0.10f).Within(0.01f), "Player's feet should be 10% above bottom edge of camera viewport");
                Assert.That(vp.x, Is.EqualTo(0.50f).Within(0.01f), "Player should be horizontally centered");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void CameraFollow_ZoomOut_SupportsUpTo4x()
        {
            var camGo = new GameObject("TestCamera");
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            var follow = camGo.AddComponent(RuntimeType("TheLastKnight.Camera.CameraFollow2D"));

            try
            {
                SetProp(follow, "EnableZoom", true);
                Invoke(follow, "SetZoomMultiplier", 4.0f);

                float targetSize = (float)GetProp(follow, "TargetOrthographicSize");
                Assert.That(targetSize, Is.EqualTo(20f).Within(0.01f), "Target orthographic size should be 4x of base size 5");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void CameraFollow_FeetFraming_MaintainsTenPercentAt4xZoom()
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
                SetProp(follow, "TargetViewportY", 0.10f);

                // Zoom to 4x
                Invoke(follow, "SetZoomMultiplier", 4.0f);
                cam.orthographicSize = 20f;
                Invoke(follow, "SnapTo", targetGo.transform.position);

                Vector3 feetPos = new Vector3(targetGo.transform.position.x, col.bounds.min.y, targetGo.transform.position.z);
                Vector3 vp = cam.WorldToViewportPoint(feetPos);

                Assert.That(vp.y, Is.EqualTo(0.10f).Within(0.01f), "Player's feet should remain at 10% viewport height even when zoomed out 4x");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
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
