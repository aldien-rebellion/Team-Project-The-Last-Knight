using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace TheLastKnight.Tests
{
    public class SkeletonCombatTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TheLastKnight." + name)).First(t => t != null);
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Flags).Invoke(obj, args);
        private static Bounds VisualBounds(SpriteRenderer sprite) => (Bounds)TypeOf("Combat.SpriteVisualBounds")
            .GetMethod("GetWorldBounds").Invoke(null, new object[] { sprite });

        [UnityTest]
        public IEnumerator SwordThrow_ParryCancelsImmediately_AndUnparriedCastLaunchesSwordOnly()
        {
            yield return new EnterPlayMode();
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Skeleton.prefab"));
            root.transform.position = new Vector3(1000, 1000, 0);
            root.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            var ai = root.GetComponent(TypeOf("AI.EnemyController"));
            ((Behaviour)ai).enabled = false;
            yield return null;
            var parry = root.GetComponent(TypeOf("Combat.ParryReceiver"));
            var skill = ((Array)Get(ai, "_skills")).GetValue(3);
            Set(ai, "_nextCyclicSkill", 1);
            Call(ai, "PerformSkill", skill);
            while (Time.time - (float)Get(parry, "_start") < 0.35f) yield return null;
            Assert.That((bool)Call(parry, "TryParry"), Is.True,
                "Windup=" + Get(parry, "_windingUp") + ", elapsed=" + (Time.time - (float)Get(parry, "_start")));
            root.GetComponent<Animator>().Update(0f);
            Assert.That(root.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("TakeHit"), Is.True);
            Assert.That((bool)ai.GetType().GetProperty("CanDealMeleeDamage").GetValue(ai), Is.False);
            Assert.That(Get(ai, "_nextCyclicSkill"), Is.EqualTo(1));
            float cancelledDeadline = Time.time + 0.5f;
            while (Time.time < cancelledDeadline) yield return null;
            Assert.That(Get(ai, "_activeSkillProjectile"), Is.Null, "Cancelled windup must never release a sword later.");
            while ((bool)parry.GetType().GetProperty("IsStaggered").GetValue(parry)) yield return null;
            Call(ai, "PerformSkill", skill);
            float releaseDeadline = Time.time + 2f;
            while ((GameObject)Get(ai, "_activeSkillProjectile") == null && Time.time < releaseDeadline)
                yield return null;
            var sword = (GameObject)Get(ai, "_activeSkillProjectile");
            Assert.That(sword != null, Is.True, "Unparried windup must release a sword; staggerUntil=" + Get(parry, "_staggerUntil") + ", now=" + Time.time);
            Assert.That(sword.name, Does.StartWith("Skeleton_Sword"));
            Assert.That(sword.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "Thrown Skeleton_Sword must render Sword_sprite.");
            Assert.That(sword.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Sword_sprite_0000"));
            Assert.That(sword.GetComponent(TypeOf("Combat.Projectiles.EnemyProjectile")), Is.Not.Null);
            Assert.That((bool)ai.GetType().GetProperty("CanDealMeleeDamage").GetValue(ai), Is.False, "Attack3 must deal projectile damage only.");
            Vector3 position = sword.transform.position;
            float movementDeadline = Time.time + 0.1f;
            while (Time.time < movementDeadline) yield return null;
            Assert.That(Vector3.Distance(position, sword.transform.position), Is.GreaterThan(0.1f));
            Call(ai, "CancelAttack");
            Assert.That(sword == null || !sword.activeSelf, Is.True, "Cancellation disables a released sword immediately too.");
            UnityEngine.Object.Destroy(root);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LiveCombat_PlaysTwoFullAttacksThenOneSecondShield_AndResumesAfterSpecial()
        {
            yield return new EnterPlayMode();
            // This scene exists only during the test's Play Mode session.
            var scene = SceneManager.CreateScene("Skeleton combat verification " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(scene);
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Skeleton.prefab"));
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            root.transform.position = new Vector3(1000, 1000, 0);
            player.GetComponent(TypeOf("Player.PlayerController")).GetType().GetProperty("enabled").SetValue(player.GetComponent(TypeOf("Player.PlayerController")), false);
            var ai = root.GetComponent(TypeOf("AI.EnemyController"));
            Set(ai, "_avoidLedges", false);
            Set(ai, "_isBoss", true); // Isolate combat from the scene's original player/leash.
            Call(ai, "SetSpawnPosition", root.transform.position);
            root.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            var playerStats = player.GetComponent(TypeOf("Stats.PlayerStats"));
            Set(playerStats, "_currentHP", 100000f);
            var body = root.GetComponent<CapsuleCollider2D>();
            var target = player.GetComponentsInChildren<Collider2D>().First(c => !c.isTrigger);
            Physics2D.SyncTransforms();
            player.transform.position += body.bounds.center - target.bounds.center;
            player.transform.position += Vector3.right * (body.bounds.extents.x + target.bounds.extents.x + 0.049f);
            Physics2D.SyncTransforms();
            Set(ai, "_player", player);
            float gap = (float)Call(ai, "GetAttackDistance");
            player.transform.position += Vector3.right * (0.049f - gap);
            Physics2D.SyncTransforms();
            yield return null;
            Set(ai, "_player", player);
            var animator = root.GetComponent<Animator>();
            var actions = new List<string>();
            var starts = new List<float>();
            var ends = new List<float>();
            string previous = "";
            float previousProgress = 0;
            float started = Time.time;
            bool captured = false;
            while (Time.time - started < 9f)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                string current = state.IsName("Attack") ? "Attack" : state.IsName("Shield") ? "Shield" : state.IsName("Attack3") ? "Attack3" : "";
                bool restart = current == previous && current == "Attack" && state.normalizedTime < previousProgress;
                if (current != previous || restart)
                {
                    if (previous != "") ends.Add(Time.time - started);
                    if (current != "") { actions.Add(current); starts.Add(Time.time - started); }
                }
                previous = current;
                previousProgress = state.normalizedTime;
                if (!captured && current == "Shield")
                {
                    captured = true;
                    Capture(root, player);
                }
                yield return null;
            }
            Debug.Log("Skeleton live sequence: " + string.Join(", ", actions) + "; gap=" + Call(ai, "GetAttackDistance") + "; AI=" + Get(ai, "_currentState"));
            Assert.That(actions.Take(3), Is.EqualTo(new[] { "Attack", "Attack", "Shield" }));
            int special = actions.IndexOf("Attack3");
            Assert.That(special, Is.GreaterThan(2), "Special must trigger at melee range after its cooldown.");
            Assert.That(starts[special], Is.GreaterThanOrEqualTo(5f));
            Assert.That(actions[special + 1], Is.EqualTo("Attack"));
            for (int i = 0; i < Math.Min(ends.Count, actions.Count); i++)
            {
                if (actions[i] == "Shield") Assert.That(ends[i] - starts[i], Is.GreaterThanOrEqualTo(0.95f));
                if (actions[i] == "Attack") Assert.That(ends[i] - starts[i], Is.GreaterThanOrEqualTo(0.75f));
            }
            Assert.That((float)Get(playerStats, "_currentHP"), Is.LessThan(100000f));
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(player);
            yield return new ExitPlayMode();
        }

        private static void Capture(GameObject root, GameObject player)
        {
            Call(root.GetComponentInChildren(TypeOf("Combat.FloatingHealthBar")), "LateUpdate");
            var cameraObject = new GameObject("Skeleton verification camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.3f;
            camera.transform.position = root.transform.position + new Vector3(0.8f, 1.4f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f);
            var texture = new RenderTexture(960, 720, 24);
            var old = RenderTexture.active;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes("Temp/Skeleton-combat-verified.png", image.EncodeToPNG());
            RenderTexture.active = old;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void Sequence_PrioritizesReadySpecial_AndResumesPendingAction()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Skeleton.prefab"));
            try
            {
                var ai = root.GetComponent(TypeOf("AI.EnemyController"));
                Call(ai, "Awake");
                var skills = (Array)Get(ai, "_skills");
                Assert.That(skills.Length, Is.EqualTo(4));
                Assert.That(Get(ai, "_meleeCooldown"), Is.EqualTo(0f));
                Assert.That(Get(ai, "_basicAttackCanParry"), Is.EqualTo(false));
                for (int i = 0; i < 3; i++)
                {
                    Set(ai, "_nextCyclicSkill", i);
                    Assert.That(Call(ai, "GetReadySkill", 0.05f), Is.SameAs(skills.GetValue(i)));
                    Assert.That(Get(skills.GetValue(i), "isParryable"), Is.EqualTo(false));
                    Assert.That(Get(skills.GetValue(i), "cooldown"), Is.EqualTo(0f));
                }
                Assert.That(Get(skills.GetValue(2), "guardDuration"), Is.EqualTo(1f));
                var special = skills.GetValue(3);
                Assert.That(Get(special, "cooldown"), Is.EqualTo(5f));
                Assert.That(Call(ai, "GetReadySkill", 0.05f), Is.Not.SameAs(special), "First special must wait five seconds.");
                Set(special, "nextReadyTime", 0f);
                Assert.That(Call(ai, "GetReadySkill", 0.05f), Is.SameAs(special));
                Assert.That(Get(ai, "_nextCyclicSkill"), Is.EqualTo(2));
                Set(special, "nextReadyTime", Time.time + 5f);
                Assert.That(Call(ai, "GetReadySkill", 0.05f), Is.SameAs(skills.GetValue(2)));
                Assert.That(Call(ai, "GetReadySkill", 0.06f), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void CloseRange_HitboxOverlapsPlayer_AndDealsDamage(float facing)
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Skeleton.prefab"));
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
            try
            {
                root.transform.position = new Vector3(10000, 10000, 0);
                root.transform.localScale = new Vector3(facing * 5, 5, 5);
                player.transform.position = root.transform.position;
                var ai = root.GetComponent(TypeOf("AI.EnemyController"));
                var stats = root.GetComponent(TypeOf("Combat.EnemyStats"));
                var playerStats = player.GetComponent(TypeOf("Stats.PlayerStats"));
                Call(player.GetComponent(TypeOf("Player.PlayerController")), "Awake");
                Call(ai, "Awake"); Call(stats, "Awake"); Call(playerStats, "Awake");
                Set(ai, "_player", player);
                var body = root.GetComponent<CapsuleCollider2D>();
                var target = player.GetComponentsInChildren<Collider2D>().First(c => !c.isTrigger);
                Physics2D.SyncTransforms();
                player.transform.position += (Vector3)(body.bounds.center - target.bounds.center);
                player.transform.position += Vector3.right * facing * (body.bounds.extents.x + target.bounds.extents.x + 0.049f);
                Physics2D.SyncTransforms();
                float gap = (float)Call(ai, "GetAttackDistance");
                player.transform.position += Vector3.right * facing * (0.049f - gap);
                Physics2D.SyncTransforms();
                gap = (float)Call(ai, "GetAttackDistance");
                Assert.That(gap, Is.InRange(0.045f, 0.05f));
                var hitbox = root.GetComponentInChildren(TypeOf("Combat.EnemyHitbox2D"));
                Assert.That(hitbox.GetComponent<Collider2D>().Distance(target).isOverlapped, Is.True);
                var health = playerStats.GetType().GetProperty("CurrentHP");
                float before = (float)health.GetValue(playerStats);
                Set(ai, "_damageUntil", Time.time + 1f);
                Call(hitbox, "TryDealDamage", target.gameObject);
                Assert.That((float)health.GetValue(playerStats), Is.LessThan(before));
                float after = (float)health.GetValue(playerStats);
                Call(hitbox, "TryDealDamage", target.gameObject);
                Assert.That((float)health.GetValue(playerStats), Is.EqualTo(after));
                Set(ai, "_damageUntil", 0f);
                Call(hitbox, "BeginAttack");
                Call(hitbox, "TryDealDamage", target.gameObject);
                Assert.That((float)health.GetValue(playerStats), Is.EqualTo(after), "Shield / inactive hitbox must not damage.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(player); }
        }

        [Test]
        public void HealthBar_FollowsSpriteTopWithGap_AndParryUsesSpriteCenter()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Skeleton.prefab"));
            try
            {
                var sprite = root.GetComponent<SpriteRenderer>();
                Assert.That(VisualBounds(sprite).size.y, Is.LessThan(3f), "Transparent sprite padding must be excluded.");
                var bar = root.GetComponentInChildren(TypeOf("Combat.FloatingHealthBar"));
                var parry = root.GetComponent(TypeOf("Combat.ParryReceiver"));
                Assert.That(Get(parry, "_centerSprite"), Is.SameAs(sprite));
                Assert.That(bar.transform.localScale.x, Is.EqualTo(0.0048f));
                var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/BlueSlime.prefab");
                var slimeBar = slimePrefab.GetComponentInChildren(TypeOf("Combat.FloatingHealthBar")).GetComponent<RectTransform>();
                Assert.That(((RectTransform)bar.transform).sizeDelta.x, Is.EqualTo(slimeBar.sizeDelta.x).Within(0.001f));
                Assert.That(((RectTransform)bar.transform).sizeDelta.y, Is.EqualTo(slimeBar.sizeDelta.y).Within(0.001f));
                var slime = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/BlueSlime.prefab"));
                Assert.That(bar.transform.lossyScale.x, Is.EqualTo(slime.GetComponentInChildren(TypeOf("Combat.FloatingHealthBar")).transform.lossyScale.x).Within(0.0001f));
                UnityEngine.Object.DestroyImmediate(slime);
                Call(bar, "Awake");
                foreach (float x in new[] { 2f, -3f })
                {
                    root.transform.position = new Vector3(x, 4f, 0);
                    root.transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
                    Call(bar, "LateUpdate");
                    var rect = (RectTransform)bar.transform;
                    float bottom = rect.position.y - rect.rect.height * Mathf.Abs(rect.lossyScale.y) / 2f;
                    Assert.That(bottom - VisualBounds(sprite).max.y, Is.EqualTo(0.08f).Within(0.0001f));
                    Assert.That(rect.position.x, Is.EqualTo(VisualBounds(sprite).center.x).Within(0.0001f));
                    Assert.That(rect.lossyScale.x, Is.GreaterThan(0f));
                }
                var ringObject = new GameObject("Test ring");
                ringObject.transform.SetParent(root.transform);
                var ring = ringObject.AddComponent<LineRenderer>();
                ring.positionCount = 48;
                Call(parry, "DrawRing", ring, 0.35f);
                Assert.That(Vector3.Distance((ring.GetPosition(0) + ring.GetPosition(24)) / 2f, VisualBounds(sprite).center), Is.LessThan(0.0001f));
                UnityEngine.Object.DestroyImmediate(ringObject);
                root.transform.position = new Vector3(1000, 1000, 0);
                root.transform.localScale = Vector3.one * 5f;
                var shield = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Enemies/Skeleton/Skeleton_Shield.anim");
                shield.SampleAnimation(root, 0.2f);
                Call(bar, "LateUpdate");
                Capture(root, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
