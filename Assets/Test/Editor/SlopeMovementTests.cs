using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class SlopeMovementTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void WalkableSlope_AllowsTangentMotion_StillStopsAtWall(bool addWall)
        {
            GameObject floor = null, player = null, wall = null;
            try
            {
                floor = new GameObject("QA slope", typeof(BoxCollider2D));
                floor.transform.position = new Vector3(1000, -0.5f);
                floor.transform.rotation = Quaternion.Euler(0, 0, 1.2f);
                floor.GetComponent<BoxCollider2D>().size = new Vector2(20, 1);
                player = new GameObject("QA walker", typeof(BoxCollider2D), typeof(Rigidbody2D));
                player.transform.position = new Vector3(1000, 0.03f);
                player.GetComponent<BoxCollider2D>().size = new Vector2(1, 2);
                player.GetComponent<BoxCollider2D>().offset = Vector2.up;
                if (addWall)
                {
                    wall = new GameObject("QA wall", typeof(BoxCollider2D));
                    wall.transform.position = new Vector3(1003, 2);
                    wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 5);
                }
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("TheLastKnight.Physics.KinematicCharacterController2D")).First(t => t != null);
                var controller = player.AddComponent(type);
                type.GetField("_obstacleMask", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, (LayerMask)~0);
                type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                // EditMode has no render/physics interpolation loop.
                player.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
                Physics2D.SyncTransforms();
                var move = type.GetMethod("Move");
                for (int i = 0; i < 150; i++)
                {
                    move.Invoke(controller, new object[] { new Vector2(2, -1), 0.02f });
                    player.transform.position = player.GetComponent<Rigidbody2D>().position;
                    Physics2D.SyncTransforms();
                }
                Assert.Greater(player.transform.position.x, 1001.5f, "Ground contact must not stop tangent movement");
                if (addWall) Assert.Less(player.transform.position.x, 1002.3f, "The wall must still block movement");
                else Assert.Greater(player.transform.position.x, 1005f);
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            }
        }
    }
}
