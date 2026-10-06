using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class ScenePortalTests
    {
        private Type _portalType;
        private MethodInfo _isArrivalPortal;
        private GameObject _root;
        private string _previousScene;
        private string _previousPortal;
        private string _previousExpectedPortal;

        [SetUp]
        public void SetUp()
        {
            _portalType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ScenePortal")).First(type => type != null);
            _previousScene = GetArrival("lastSceneLoaded");
            _previousPortal = GetArrival("lastPortalUsed");
            _previousExpectedPortal = GetArrival("targetPortalExpected");
            _isArrivalPortal = _portalType.GetMethod("IsArrivalPortal", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(_isArrivalPortal, Is.Not.Null);
            SetArrival("", "", "");
            _root = new GameObject("ScenePortal test root");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_portalType == null) return;
            SetArrival(_previousScene, _previousPortal, _previousExpectedPortal);
        }

        private string GetArrival(string name) => (string)_portalType.GetField(name).GetValue(null);

        private void SetArrival(string previousScene, string previousPortal, string expectedPortal)
        {
            _portalType.GetField("lastSceneLoaded").SetValue(null, previousScene);
            _portalType.GetField("lastPortalUsed").SetValue(null, previousPortal);
            _portalType.GetField("targetPortalExpected").SetValue(null, expectedPortal);
        }

        private Component CreatePortal(string name, string destination)
        {
            var portalObject = new GameObject(name);
            portalObject.transform.SetParent(_root.transform);
            var portal = portalObject.AddComponent(_portalType);
            _portalType.GetField("targetSceneName").SetValue(portal, destination);
            return portal;
        }

        private bool IsArrival(Component portal) => (bool)_isArrivalPortal.Invoke(portal, null);

        [Test]
        public void ChurchExit_ExplicitLeftDestination_RejectsRightPortalCheckedFirst()
        {
            var right = CreatePortal("Portal_Right", "OutdoorMarket");
            var left = CreatePortal("Portal_Left", "Church");
            SetArrival("Church", "Portal_Left", "Portal_Left");

            Assert.That(IsArrival(right), Is.False,
                "The opposite-side fallback must not claim an arrival addressed to Portal_Left.");
            Assert.That(GetArrival("targetPortalExpected"), Is.EqualTo("Portal_Left"));
            Assert.That(IsArrival(left), Is.True);
            Assert.That(IsArrival(right), Is.False, "Portal selection must also be independent of check order.");
        }

        [Test]
        public void PreviousSceneWithoutExplicitDestination_SelectsReciprocalPortalBeforeOppositeName()
        {
            var right = CreatePortal("Portal_Right", "OutdoorMarket");
            var left = CreatePortal("Portal_Left", "Church");
            SetArrival("Church", "Portal_Left", "");

            Assert.That(IsArrival(right), Is.False);
            Assert.That(IsArrival(left), Is.True);
        }

        [TestCase("Portal_Left", "Portal_Right")]
        [TestCase("Portal_Right", "Portal_Left")]
        public void ArrivalWithoutDestinationOrPreviousScene_UsesOppositeSide(string departedPortal, string arrivalPortal)
        {
            var arrival = CreatePortal(arrivalPortal, "OtherScene");
            var departedSide = CreatePortal(departedPortal, "OtherScene");
            SetArrival("", departedPortal, "");

            Assert.That(IsArrival(arrival), Is.True);
            Assert.That(IsArrival(departedSide), Is.False);
        }

        [Test]
        public void ExplicitDestination_MatchesNameCaseInsensitively()
        {
            var portal = CreatePortal("Portal_Left", "Church");
            SetArrival("Church", "Portal_Left", "portal_left");

            Assert.That(IsArrival(portal), Is.True);
        }

        [Test]
        public void UnknownExplicitDestination_RejectsReciprocalAndOppositeFallbacks()
        {
            var right = CreatePortal("Portal_Right", "OutdoorMarket");
            var left = CreatePortal("Portal_Left", "Church");
            SetArrival("Church", "Portal_Left", "MissingPortal");

            Assert.That(IsArrival(right), Is.False);
            Assert.That(IsArrival(left), Is.False);
        }

        [TestCase("Portal_Left")]
        [TestCase("Portal_Right")]
        public void NoPendingArrival_RejectsPortal(string portalName)
        {
            var portal = CreatePortal(portalName, "Church");

            Assert.That(IsArrival(portal), Is.False);
        }

        [TestCase("Church", "CityCenter")]
        [TestCase("CityCenter", "Church")]
        public void ChurchConnection_UsesLeftPortalInBothDirections(string sceneName, string destination)
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Maps/" + sceneName + ".unity");
            try
            {
                var portals = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(_portalType, true))
                    .Where(portal => portal.name == "Portal_Left")
                    .ToArray();
                Assert.That(portals.Length, Is.EqualTo(1));
                Assert.That(_portalType.GetField("targetSceneName").GetValue(portals[0]), Is.EqualTo(destination));
                Assert.That(_portalType.GetField("targetPortalName").GetValue(portals[0]), Is.EqualTo("Portal_Left"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
