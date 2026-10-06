using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace TheLastKnight.Tests
{
    public class EnemyLootDropTests
    {
        [TestCase(1)]
        [TestCase(40)]
        [TestCase(100)]
        public void EveryDeath_RollsRewardsFromCurrentInstanceLevel(int level)
        {
            var randomState = UnityEngine.Random.state;
            var go = new GameObject("Reward test");
            try
            {
                var type = FindType("TheLastKnight.Combat.EnemyStats");
                var stats = go.AddComponent(type);
                type.GetField("_level", InstanceMembers).SetValue(stats, level);
                var goldField = type.GetField("_goldReward", InstanceMembers);
                var expField = type.GetField("_expReward", InstanceMembers);
                var pairs = new HashSet<string>();
                UnityEngine.Random.InitState(915);
                for (int i = 0; i < 50; i++)
                {
                    type.GetMethod("Revive").Invoke(stats, null);
                    goldField.SetValue(stats, -1);
                    expField.SetValue(stats, -1);
                    type.GetMethod("Die", InstanceMembers).Invoke(stats, null);
                    int gold = (int)goldField.GetValue(stats);
                    int exp = (int)expField.GetValue(stats);
                    Assert.That(gold, Is.InRange(level * 5, level * 10));
                    Assert.That(exp, Is.InRange(level * 20, level * 80));
                    pairs.Add(gold + ":" + exp);
                    type.GetMethod("Die", InstanceMembers).Invoke(stats, null);
                    Assert.That(goldField.GetValue(stats), Is.EqualTo(gold), "Dead enemies cannot roll twice.");
                }
                Assert.Greater(pairs.Count, 1, "Rewards must vary across kills.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Random.state = randomState;
            }
        }

        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type FindType(string fullName) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName)).FirstOrDefault(type => type != null);

        private readonly (string enemy, string itemId, string expectedSpriteStart, bool isBoss)[] _monsters = new[]
        {
            ("ArchDemon", "drop_archdemon", "Free-Goblin-Loot-Icon42", false),
            ("BlueSlime", "drop_blueslime", "free-rpg-loot_Icon15", false),
            ("BringerOfDeath", "drop_bringerofdeath", "Free-Goblin-Loot-Icon48", false),
            ("Demon", "drop_demon", "Free-Goblin-Loot-Icon31", false),
            ("DemonBoss", "drop_demonboss", "Free-Goblin-Loot-Icon8", true),
            ("DemonKin", "drop_demonkin", "Free-Goblin-Loot-Icon25", false),
            ("Dragon", "drop_dragon", "free-rpg-loot_Icon9", false),
            ("FantasyMushroom", "drop_fantasymushroom", "20", false),
            ("FireWorm", "drop_fireworm", "free-rpg-loot_Icon42", false),
            ("FlyingEye", "drop_flyingeye", "Free-Goblin-Loot-Icon10", false),
            ("ForestMushroom", "drop_forestmushroom", "44", false),
            ("Fox", "drop_fox", "free-rpg-loot_Icon40", true),
            ("Goblin", "drop_goblin", "Free-Goblin-Loot-Icon3", false),
            ("HoodedProtagonist", "drop_hoodedprotagonist", "Free-Goblin-Loot-Icon22", false),
            ("Jinn", "drop_jinn", "free-rpg-loot_Icon19", false),
            ("Lizard", "drop_lizard", "free-rpg-loot_Icon18", false),
            ("MechaStoneGolem", "drop_mechastonegolem", "crystal_white-gold1", true),
            ("Minotaur", "drop_minotaur", "Free-Goblin-Loot-Icon43", false),
            ("Necromancer", "drop_necromancer", "Free-Goblin-Loot-Icon39", false),
            ("Reaper", "drop_reaper", "Free-Goblin-Loot-Icon7", false),
            ("Satyr", "drop_satyr", "40", false),
            ("ShadowDemonDragon", "drop_shadowdemondragon", "Free-Goblin-Loot-Icon9", false),
            ("Skeleton", "drop_skeleton", "free-rpg-loot_Icon8", false),
            ("SkeletonKnight", "drop_skeletonknight", "free-rpg-loot_Icon20", false),
            ("Skullwolf", "drop_skullwolf", "free-rpg-loot_Icon32", false),
            ("Small_dragon", "drop_smalldragon", "Free-Goblin-Loot-Icon44", false),
            ("UndeadExecutioner", "drop_undeadexecutioner", "free-rpg-loot_Icon34", false),
            ("Volcanox", "drop_volcanox", "sea of fire", true)
        };

        [Test]
        public void All28MonsterItems_ExistAndHaveCorrectSprites()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });
            Assert.IsNotNull(createItemMethod, "CreateItem method must exist.");

            foreach (var m in _monsters)
            {
                var item = createItemMethod.Invoke(null, new object[] { m.itemId, 1 });
                Assert.IsNotNull(item, $"Item '{m.itemId}' for monster '{m.enemy}' must exist in ItemRegistry.");

                var iconProp = item.GetType().GetProperty("Icon");
                var icon = (Sprite)iconProp.GetValue(item);
                Assert.IsNotNull(icon, $"Item '{m.itemId}' must have an Icon sprite.");
                Assert.That(icon.name.StartsWith(m.expectedSpriteStart, StringComparison.OrdinalIgnoreCase),
                    $"Item '{m.itemId}' sprite '{icon.name}' does not start with expected prefix '{m.expectedSpriteStart}'.");
            }
        }

        [TestCase("drop_blueslime", 2f, 0f, 0f)]
        [TestCase("drop_demonboss", 12f, 0f, 0f)]
        [TestCase("drop_blueslime", 2f, 2f, 0f)]
        [TestCase("drop_blueslime", 2f, 0f, 15f)]
        [TestCase("drop_blueslime", -1.4f, 0f, 0f)]
        [TestCase("drop_mechastonegolem", -1.4f, 0f, 0f)]
        [TestCase("drop_volcanox", -1.4f, 0f, 0f)]
        [TestCase("drop_blueslime", -1.4f, 0f, 0f, false)]
        public void Pickup_LandsOnActualGroundWithoutBobbing(string itemId, float height, float landingX, float slope, bool startInColliders = true)
        {
            var randomState = UnityEngine.Random.state;
            bool originalStartInColliders = Physics2D.queriesStartInColliders;
            Physics2D.queriesStartInColliders = startInColliders;
            var ground = new GameObject("Loot grounding test floor");
            var enemy = new GameObject("Loot grounding test enemy");
            GameObject pickupObject = null;
            try
            {
                Vector3 origin = new Vector3(10000f, 1000f, 0f);
                ground.transform.position = origin + new Vector3(landingX, -0.5f, 0f);
                ground.transform.rotation = Quaternion.Euler(0f, 0f, slope);
                ground.AddComponent<BoxCollider2D>().size = new Vector2(landingX == 0f ? 10f : 1.5f, 1f);
                enemy.transform.position = origin + Vector3.up;
                enemy.AddComponent(FindType("TheLastKnight.Combat.EnemyStats"));
                enemy.AddComponent<BoxCollider2D>().size = Vector2.one;
                Physics2D.SyncTransforms();

                var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");
                var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
                var item = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) })
                    .Invoke(null, new object[] { itemId, 1 });
                var pickup = (Component)pickupType.GetMethod("Spawn", StaticMembers)
                    .Invoke(null, new object[] { item, origin + Vector3.up * height });
                pickupObject = pickup.gameObject;
                pickupObject.transform.position += Vector3.right * landingX;
                pickupType.GetField("_velocity", InstanceMembers).SetValue(pickup, Vector2.zero);
                var simulate = pickupType.GetMethod("SimulateFall", InstanceMembers);
                var settled = pickupType.GetField("_isSettled", InstanceMembers);
                for (int i = 0; i < 500 && !(bool)settled.GetValue(pickup); i++)
                    simulate.Invoke(pickup, new object[] { 0.02f });
                Assert.IsTrue((bool)settled.GetValue(pickup), "Must reach the terrain even after a long fall.");
                var bounds = pickupObject.GetComponent<SpriteRenderer>().bounds;
                float surface = float.NegativeInfinity;
                for (int i = -1; i <= 1; i++)
                {
                    foreach (var hit in Physics2D.RaycastAll(
                        new Vector2(bounds.center.x + i * bounds.extents.x, origin.y + 20f), Vector2.down, 40f))
                        if (hit.collider.gameObject == ground) surface = Mathf.Max(surface, hit.point.y);
                }
                Assert.That(bounds.min.y - surface, Is.EqualTo(0.01f).Within(0.002f), "Icon bottom must rest on terrain, not on the monster.");
                Vector3 restingPosition = pickupObject.transform.position;
                pickupType.GetMethod("Update", InstanceMembers).Invoke(pickup, null);
                Assert.AreEqual(restingPosition, pickupObject.transform.position, "Landed loot must not bob into or above the ground.");
            }
            finally
            {
                if (pickupObject != null) UnityEngine.Object.DestroyImmediate(pickupObject);
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(ground);
                Physics2D.SyncTransforms();
                UnityEngine.Random.state = randomState;
                Physics2D.queriesStartInColliders = originalStartInColliders;
            }
        }

        [Test]
        public void All28MonsterPickups_LandOnLoadedCityCenterGround()
        {
            var verificationScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Maps/CityCenter.unity");
            bool openedScene = !verificationScene.isLoaded;
            if (openedScene)
                verificationScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    "Assets/Scenes/Maps/CityCenter.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            var ground = UnityEngine.Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
                .FirstOrDefault(collider => collider.name == "Ground" && collider.gameObject.scene.name == "CityCenter");
            var randomState = UnityEngine.Random.state;
            var spawned = new List<GameObject>();
            try
            {
                Assert.IsNotNull(ground, "CityCenter must have its actual floor collider.");
                Physics2D.SyncTransforms();
                var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");
                var createItem = FindType("TheLastKnight.Inventory.ItemRegistry")
                    .GetMethod("CreateItem", new[] { typeof(string), typeof(int) });
                var spawn = pickupType.GetMethod("Spawn", StaticMembers);
                var simulate = pickupType.GetMethod("SimulateFall", InstanceMembers);
                var settled = pickupType.GetField("_isSettled", InstanceMembers);
                for (int index = 0; index < _monsters.Length; index++)
                {
                    var monster = _monsters[index];
                    float x = ground.bounds.center.x + index - 14f;
                    float y = ground.bounds.max.y + (index % 2 == 0 ? -1.4f : 12f);
                    var item = createItem.Invoke(null, new object[] { monster.itemId, 1 });
                    var pickup = (Component)spawn.Invoke(null, new object[] { item, new Vector3(x, y, 0f) });
                    spawned.Add(pickup.gameObject);
                    for (int step = 0; step < 500 && !(bool)settled.GetValue(pickup); step++)
                        simulate.Invoke(pickup, new object[] { 0.02f });
                    Assert.IsTrue((bool)settled.GetValue(pickup), monster.itemId + " must land.");
                    Assert.That(pickup.GetComponent<SpriteRenderer>().bounds.min.y - ground.bounds.max.y,
                        Is.EqualTo(0.01f).Within(0.002f), monster.itemId + " must remain above CityCenter's floor.");
                }
            }
            finally
            {
                foreach (var pickup in spawned) UnityEngine.Object.DestroyImmediate(pickup);
                Physics2D.SyncTransforms();
                UnityEngine.Random.state = randomState;
                if (openedScene) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(verificationScene, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DemonKinPrefab_DropsAboveFloorEvenWhenDeathDisablesBody(bool disabledBody)
        {
            var randomState = UnityEngine.Random.state;
            var ground = new GameObject("DemonKin actual pivot regression floor");
            GameObject enemy = null;
            var spawned = new List<GameObject>();
            try
            {
                Vector3 origin = new Vector3(10000f, 1000f, 0f);
                ground.transform.position = origin - Vector3.up * 0.5f;
                ground.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/DemonKin.prefab");
                enemy = UnityEngine.Object.Instantiate(prefab);
                var body = enemy.GetComponent<CapsuleCollider2D>();
                float rootToFeet = (body.offset.y - body.size.y * 0.5f) * enemy.transform.localScale.y;
                enemy.transform.position = origin - Vector3.up * rootToFeet;
                Assert.Less(enemy.transform.position.y, ground.GetComponent<BoxCollider2D>().bounds.min.y,
                    "The real DemonKin pivot must be below the entire floor collider to reproduce the bug.");
                Physics2D.SyncTransforms();
                body.enabled = !disabledBody;
                var lootType = FindType("TheLastKnight.Combat.EnemyLootDrop");
                var loot = enemy.GetComponent(lootType);
                lootType.GetProperty("DropChance").SetValue(loot, 1f);
                lootType.GetProperty("DropRolls").SetValue(loot, 3);
                lootType.GetMethod("DropLoot").Invoke(loot, null);
                var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");
                var simulate = pickupType.GetMethod("SimulateFall", InstanceMembers);
                var settled = pickupType.GetField("_isSettled", InstanceMembers);
                foreach (Component pickup in UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None))
                {
                    spawned.Add(pickup.gameObject);
                    Assert.Greater(pickup.GetComponent<SpriteRenderer>().bounds.min.y, origin.y,
                        "Loot must start above the floor, not at DemonKin's underground pivot.");
                    for (int step = 0; step < 500 && !(bool)settled.GetValue(pickup); step++)
                        simulate.Invoke(pickup, new object[] { 0.02f });
                    Assert.IsTrue((bool)settled.GetValue(pickup));
                    Assert.That(pickup.GetComponent<SpriteRenderer>().bounds.min.y - origin.y,
                        Is.EqualTo(0.01f).Within(0.002f));
                }
                Assert.AreEqual(3, spawned.Count);
            }
            finally
            {
                foreach (var pickup in spawned) UnityEngine.Object.DestroyImmediate(pickup);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(ground);
                Physics2D.SyncTransforms();
                UnityEngine.Random.state = randomState;
            }
        }

        public static IEnumerable<TestCaseData> EnemyPrefabDropCases()
        {
            foreach (var path in System.IO.Directory.GetFiles("Assets/Prefabs/Enemies", "*.prefab").OrderBy(path => path))
                foreach (bool disabledBody in new[] { false, true })
                    foreach (float height in new[] { 0f, 8f })
                        yield return new TestCaseData(path.Replace('\\', '/'), disabledBody, height)
                            .SetName("MonsterPrefabDrop_" + System.IO.Path.GetFileNameWithoutExtension(path)
                                + "_" + (disabledBody ? "Dead" : "Alive") + "_Height" + height);
        }

        [TestCaseSource(nameof(EnemyPrefabDropCases))]
        public void MonsterPrefabDrop_LandsAboveFloor(string prefabPath, bool disabledBody, float height)
        {
            var randomState = UnityEngine.Random.state;
            var ground = new GameObject("Monster prefab drop audit floor");
            GameObject enemy = null;
            var spawned = new List<GameObject>();
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");
            var existingPickups = new HashSet<UnityEngine.Object>(UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None));
            try
            {
                Vector3 origin = new Vector3(10000f, 1000f, 0f);
                ground.transform.position = origin - Vector3.up * 0.5f;
                ground.AddComponent<BoxCollider2D>().size = new Vector2(100f, 1f);
                enemy = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                var body = enemy.GetComponents<Collider2D>().FirstOrDefault(collider => !collider.isTrigger);
                Assert.IsNotNull(body, prefabPath + " must have a solid body collider.");
                body.enabled = true;
                enemy.transform.position = origin;
                Physics2D.SyncTransforms();
                enemy.transform.position += Vector3.up * (origin.y + height - body.bounds.min.y);
                Physics2D.SyncTransforms();
                float pivotBelowFeet = body.bounds.min.y - enemy.transform.position.y;
                if (!disabledBody && height == 0f)
                    TestContext.WriteLine("PIVOT " + System.IO.Path.GetFileNameWithoutExtension(prefabPath)
                        + " belowFeet=" + pivotBelowFeet.ToString("F3")
                        + " oldSpawnBelowFloor=" + (enemy.transform.position.y + 0.5f < origin.y));
                var expectedFeet = new Vector3(body.bounds.center.x, body.bounds.min.y, enemy.transform.position.z);
                if (disabledBody)
                    foreach (var collider in enemy.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                var lootType = FindType("TheLastKnight.Combat.EnemyLootDrop");
                var loot = enemy.GetComponent(lootType);
                if (loot != null)
                {
                    lootType.GetProperty("DropChance").SetValue(loot, 1f);
                    lootType.GetProperty("DropRolls").SetValue(loot, 3);
                    lootType.GetProperty("BossGuaranteedCount").SetValue(loot, 1);
                    lootType.GetMethod("DropLoot").Invoke(loot, null);
                }
                else
                {
                    Assert.AreEqual("MoonstoneKeeper.prefab", System.IO.Path.GetFileName(prefabPath),
                        "Unexpected monster without EnemyLootDrop.");
                    var dropPosition = (Vector3)pickupType.GetMethod("GetDropPosition", StaticMembers)
                        .Invoke(null, new object[] { enemy.transform });
                    Assert.That(Vector3.Distance(dropPosition, expectedFeet), Is.LessThan(0.002f));
                    var item = FindType("TheLastKnight.Inventory.ItemRegistry")
                        .GetMethod("CreateItem", new[] { typeof(string), typeof(int) })
                        .Invoke(null, new object[] { "moonstone_shard", 1 });
                    pickupType.GetMethod("Spawn", StaticMembers).Invoke(null, new object[] { item, dropPosition });
                }
                var simulate = pickupType.GetMethod("SimulateFall", InstanceMembers);
                var settled = pickupType.GetField("_isSettled", InstanceMembers);
                foreach (Component pickup in UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None))
                {
                    if (existingPickups.Contains(pickup)) continue;
                    spawned.Add(pickup.gameObject);
                    Assert.Greater(pickup.GetComponent<SpriteRenderer>().bounds.min.y, origin.y,
                        prefabPath + " must spawn loot above the floor.");
                    for (int step = 0; step < 600 && !(bool)settled.GetValue(pickup); step++)
                        simulate.Invoke(pickup, new object[] { step % 3 == 0 ? 0.1f : 0.02f });
                    Assert.IsTrue((bool)settled.GetValue(pickup), prefabPath + " loot must land.");
                    Assert.That(pickup.GetComponent<SpriteRenderer>().bounds.min.y - origin.y,
                        Is.EqualTo(0.01f).Within(0.002f), prefabPath + " loot must stay above the floor.");
                }
                Assert.IsNotEmpty(spawned, prefabPath + " must drop its configured item.");
            }
            finally
            {
                foreach (Component pickup in UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None))
                    if (!existingPickups.Contains(pickup)) UnityEngine.Object.DestroyImmediate(pickup.gameObject);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(ground);
                Physics2D.SyncTransforms();
                UnityEngine.Random.state = randomState;
            }
        }

        [Test]
        public void Boss_AlwaysDropsGuaranteedOneItem()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Boss_DemonBoss");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_demonboss");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, true);
                enemyLootDropType.GetProperty("BossGuaranteedCount").SetValue(loot, 1);

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(1, spawned.Count, "Boss must drop exactly 1 guaranteed item.");
                var pickupComp = spawned[0].GetComponent(pickupType);
                var itemData = pickupType.GetProperty("ItemData").GetValue(pickupComp);
                string id = (string)itemData.GetType().GetField("id").GetValue(itemData);
                Assert.AreEqual("drop_demonboss", id);
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NormalMonster_WhenChanceGuaranteed_DropsThreeItems()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Normal_BlueSlime");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_blueslime");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, false);
                enemyLootDropType.GetProperty("DropRolls").SetValue(loot, 3);
                enemyLootDropType.GetProperty("DropChance").SetValue(loot, 1f); // 100% chance per roll -> 3 items

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(3, spawned.Count, "Normal monster with 100% drop chance over 3 rolls must drop 3 items.");
                foreach (var s in spawned)
                {
                    var pickupComp = s.GetComponent(pickupType);
                    var itemData = pickupType.GetProperty("ItemData").GetValue(pickupComp);
                    string id = (string)itemData.GetType().GetField("id").GetValue(itemData);
                    Assert.AreEqual("drop_blueslime", id);
                }
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NormalMonster_WhenChanceZero_DropsZeroItems()
        {
            var enemyStatsType = FindType("TheLastKnight.Combat.EnemyStats");
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var pickupType = FindType("TheLastKnight.Inventory.WorldItemPickup");

            var go = new GameObject("Test_Normal_BlueSlime_Zero");
            var spawned = new List<GameObject>();
            try
            {
                go.AddComponent(enemyStatsType);
                var loot = go.AddComponent(enemyLootDropType);

                enemyLootDropType.GetProperty("DropItemId").SetValue(loot, "drop_blueslime");
                enemyLootDropType.GetProperty("IsBoss").SetValue(loot, false);
                enemyLootDropType.GetProperty("DropRolls").SetValue(loot, 3);
                enemyLootDropType.GetProperty("DropChance").SetValue(loot, 0f); // 0% chance

                enemyLootDropType.GetMethod("DropLoot", InstanceMembers).Invoke(loot, null);

                var pickups = UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsSortMode.None);
                foreach (var p in pickups)
                {
                    spawned.Add(((Component)p).gameObject);
                }

                Assert.AreEqual(0, spawned.Count, "Normal monster with 0% drop chance must drop 0 items.");
            }
            finally
            {
                foreach (var s in spawned) if (s != null) UnityEngine.Object.DestroyImmediate(s);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnemyLootDrop_DefaultItemIdLookup_MatchesAll28Monsters()
        {
            var enemyLootDropType = FindType("TheLastKnight.Combat.EnemyLootDrop");
            var lookupMethod = enemyLootDropType.GetMethod("GetDefaultDropItemId", StaticMembers);

            foreach (var m in _monsters)
            {
                string id = (string)lookupMethod.Invoke(null, new object[] { m.enemy });
                Assert.AreEqual(m.itemId, id, $"Lookup for '{m.enemy}' should return '{m.itemId}'.");
            }
        }

        [Test]
        public void All24NormalMonsterDropItems_HaveSceneBasedSellPrices()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var getSellPriceMethod = registryType.GetMethod("GetSellPrice", StaticMembers);
            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });

            var expectedPrices = new Dictionary<string, int>
            {
                ["drop_blueslime"] = 2,
                ["drop_skeleton"] = 3,
                ["drop_demonkin"] = 15,
                ["drop_lizard"] = 15,
                ["drop_dragon"] = 20,
                ["drop_minotaur"] = 26,
                ["drop_demon"] = 30,
                ["drop_jinn"] = 38,
                ["drop_skeletonknight"] = 48,
                ["drop_goblin"] = 60,
                ["drop_hoodedprotagonist"] = 68,
                ["drop_reaper"] = 72,
                ["drop_satyr"] = 80,
                ["drop_archdemon"] = 87,
                ["drop_fantasymushroom"] = 105,
                ["drop_fireworm"] = 108,
                ["drop_forestmushroom"] = 114,
                ["drop_flyingeye"] = 116,
                ["drop_undeadexecutioner"] = 119,
                ["drop_skullwolf"] = 123,
                ["drop_bringerofdeath"] = 135,
                ["drop_necromancer"] = 140,
                ["drop_smalldragon"] = 141,
                ["drop_shadowdemondragon"] = 144
            };

            foreach (var kvp in expectedPrices)
            {
                if (getSellPriceMethod != null)
                {
                    int staticPrice = (int)getSellPriceMethod.Invoke(null, new object[] { kvp.Key });
                    Assert.AreEqual(kvp.Value, staticPrice, $"Static GetSellPrice for {kvp.Key} mismatch");
                }

                var item = createItemMethod.Invoke(null, new object[] { kvp.Key, 1 });
                Assert.IsNotNull(item, $"Item {kvp.Key} must be created");

                var sellPriceField = item.GetType().GetField("sellPrice", InstanceMembers);
                if (sellPriceField != null)
                {
                    int itemPrice = (int)sellPriceField.GetValue(item);
                    Assert.AreEqual(kvp.Value, itemPrice, $"Item.sellPrice for {kvp.Key} mismatch");
                }
            }
        }

        [Test]
        public void AllBossDropItems_HaveExpectedMaxGoldSellPrices()
        {
            var registryType = FindType("TheLastKnight.Inventory.ItemRegistry");
            Assert.IsNotNull(registryType, "ItemRegistry type must exist.");

            registryType.GetMethod("ReloadDefinitions", StaticMembers).Invoke(null, null);

            var getSellPriceMethod = registryType.GetMethod("GetSellPrice", StaticMembers);
            var createItemMethod = registryType.GetMethod("CreateItem", new[] { typeof(string), typeof(int) });

            var expectedBossPrices = new Dictionary<string, int>
            {
                ["drop_mechastonegolem"] = 200,
                ["drop_demonboss"] = 600,
                ["drop_fox"] = 800,
                ["drop_volcanox"] = 1000,
                ["moonstone_shard"] = 400
            };

            foreach (var kvp in expectedBossPrices)
            {
                if (getSellPriceMethod != null)
                {
                    int staticPrice = (int)getSellPriceMethod.Invoke(null, new object[] { kvp.Key });
                    Assert.AreEqual(kvp.Value, staticPrice, $"Static GetSellPrice for boss item {kvp.Key} mismatch");
                }

                var item = createItemMethod.Invoke(null, new object[] { kvp.Key, 1 });
                Assert.IsNotNull(item, $"Item {kvp.Key} must be created");

                var sellPriceField = item.GetType().GetField("sellPrice", InstanceMembers);
                if (sellPriceField != null)
                {
                    int itemPrice = (int)sellPriceField.GetValue(item);
                    Assert.AreEqual(kvp.Value, itemPrice, $"Item.sellPrice for boss item {kvp.Key} mismatch");
                }
            }
        }
    }
}
