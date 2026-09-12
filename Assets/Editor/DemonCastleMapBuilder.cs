using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastKnight.Camera;
using TheLastKnight.Environment;

namespace TheLastKnight.Editor
{
    public static class DemonCastleMapBuilder
    {
        private const string TilesetPath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/Tilesetv3.png";
        private const string TorchPath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/spr_torch.png";
        private const string StoneFloorPath = "Assets/sprites/Environment/stone floor.png";

        private static Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static Sprite _stoneFloorSprite;
        private static Material _spriteLitMat;
        private static Material _spriteUnlitMat;

        [MenuItem("Tools/Build Demon Castle Map")]
        public static void BuildMap()
        {
            LoadAssets();

            // Find or create Map Root
            GameObject mapRoot = GameObject.Find("DemonCastle_Interior");
            if (mapRoot != null)
            {
                Undo.DestroyObjectImmediate(mapRoot);
            }

            mapRoot = new GameObject("DemonCastle_Interior");
            Undo.RegisterCreatedObjectUndo(mapRoot, "Build Demon Castle Interior");

            // Setup Global 2D Light to moody ambient tone
            SetupGlobalLighting();

            // Create individual cutaway rooms
            var r1 = BuildEntranceHall(mapRoot.transform);
            var r2 = BuildDungeon(mapRoot.transform);
            var r3 = BuildCentralShaft(mapRoot.transform);
            var r4 = BuildThroneRoom(mapRoot.transform);
            var r5 = BuildLibrary(mapRoot.transform);
            var r6 = BuildUpperBattlements(mapRoot.transform);

            // Connect and position Portal_Left and Player
            SetupPlayerAndPortal(r1);

            // Configure CameraFollow2D initial boundary
            SetupCamera(r1);

            // Clean up placeholder Ground if present
            GameObject oldGround = GameObject.Find("Ground");
            if (oldGround != null && oldGround.transform.parent == null && oldGround.transform.localScale.x >= 70f)
            {
                Undo.DestroyObjectImmediate(oldGround);
            }

            EditorUtility.SetDirty(mapRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapRoot.scene);
            Debug.Log("<color=green>Demon Castle Interior built successfully!</color>");
        }

        private static void LoadAssets()
        {
            _sprites.Clear();
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(TilesetPath);
            foreach (var obj in allAssets)
            {
                if (obj is Sprite s)
                {
                    _sprites[s.name] = s;
                }
            }

            Object[] torchAssets = AssetDatabase.LoadAllAssetsAtPath(TorchPath);
            foreach (var obj in torchAssets)
            {
                if (obj is Sprite s)
                {
                    _sprites[s.name] = s;
                }
            }

            _stoneFloorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StoneFloorPath);
            _spriteLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            _spriteUnlitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        }

        private static void SetupGlobalLighting()
        {
            var globalLightObj = GameObject.Find("Global Light 2D");
            if (globalLightObj != null)
            {
                var light = globalLightObj.GetComponent<Light2D>();
                if (light != null)
                {
                    Undo.RecordObject(light, "Adjust Global Light");
                    light.color = new Color(0.80f, 0.80f, 0.90f, 1f); // Crisp castle ambient
                    light.intensity = 0.85f;
                }
            }

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.04f, 0.03f, 0.07f, 1f); // Deep abyss black
            }
        }

        private static CastleRoom CreateRoomRoot(Transform parent, string roomName, Vector2 confinerCenter, Vector2 confinerSize)
        {
            GameObject roomGo = new GameObject(roomName);
            roomGo.transform.SetParent(parent);
            Undo.RegisterCreatedObjectUndo(roomGo, "Create " + roomName);

            // Trigger collider for room entry
            var triggerCol = roomGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.offset = confinerCenter;
            triggerCol.size = confinerSize;

            // Camera Confiner child
            GameObject confinerGo = new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(roomGo.transform);
            var confinerCol = confinerGo.AddComponent<BoxCollider2D>();
            confinerCol.isTrigger = true;
            confinerCol.offset = confinerCenter;
            confinerCol.size = confinerSize;

            // Room content & visuals
            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(roomGo.transform);

            // Visuals
            GameObject visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(contentGo.transform);

            // Colliders
            GameObject collidersGo = new GameObject("Colliders");
            collidersGo.transform.SetParent(contentGo.transform);

            // Occluder (darkness mask when outside)
            GameObject occluderGo = new GameObject("RoomOccluder");
            occluderGo.transform.SetParent(roomGo.transform);
            var occluderSr = occluderGo.AddComponent<SpriteRenderer>();
            occluderSr.sprite = GetSprite("Tilesetv3_18");
            occluderSr.drawMode = SpriteDrawMode.Tiled;
            occluderSr.size = new Vector2(confinerSize.x + 2f, confinerSize.y + 2f);
            occluderSr.color = new Color(0.03f, 0.02f, 0.05f, 1f);
            occluderSr.sortingOrder = 50;
            occluderGo.transform.position = new Vector3(confinerCenter.x, confinerCenter.y, 0f);
            occluderGo.SetActive(false); // Initially visible rooms toggle via CastleRoom

            // Setup CastleRoom component
            var roomComp = roomGo.AddComponent<CastleRoom>();
            var propName = typeof(CastleRoom).GetField("_roomName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            propName?.SetValue(roomComp, roomName);

            var propConf = typeof(CastleRoom).GetField("_cameraConfiner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            propConf?.SetValue(roomComp, confinerCol);

            var propCont = typeof(CastleRoom).GetField("_roomContent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            propCont?.SetValue(roomComp, contentGo);

            var propOcc = typeof(CastleRoom).GetField("_roomOccluder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            propOcc?.SetValue(roomComp, occluderGo);

            return roomComp;
        }

        // =========================================================================
        // ROOM 1: Ground Floor - Entrance Hall & Guard Barracks
        // =========================================================================
        private static CastleRoom BuildEntranceHall(Transform parent)
        {
            // Center = (-10, 5), Size = (28, 10)
            // Floor Y = 0, Ceiling Y = 10, Left X = -24, Right X = 4
            var room = CreateRoomRoot(parent, "Room_EntranceHall", new Vector2(-10f, 5f), new Vector2(28f, 10f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Background wall
            CreateBackdrop(visuals, new Vector2(-10f, 5f), new Vector2(28f, 10f), new Color(0.12f, 0.10f, 0.16f, 1f));

            // Solid Floor (with opening at X: -2 to 2 for Dungeon stairs)
            // Left floor segment: X = -24 to -2 (width 22, center -13, Y = -0.5)
            CreateSolidBox(colliders, visuals, new Vector2(-13f, -0.5f), new Vector2(22f, 1f), "Floor_Left");
            // Right floor segment: X = 2 to 4 (width 2, center 3, Y = -0.5)
            CreateSolidBox(colliders, visuals, new Vector2(3f, -0.5f), new Vector2(2f, 1f), "Floor_Right");

            // Solid Ceiling: X = -24 to 4 (width 28, center -10, Y = 10.5)
            CreateSolidBox(colliders, visuals, new Vector2(-10f, 10.5f), new Vector2(28f, 1f), "Ceiling");

            // Solid Left Wall: X = -24.5, Y = 5 (height 10)
            CreateSolidBox(colliders, visuals, new Vector2(-24.5f, 5f), new Vector2(1f, 10f), "Wall_Left");

            // Solid Right Wall: Doorway to Shaft from Y = 0 to 6.0 (Height 6.0 clearance for 2x Monster!)
            // Wall above door: Y = 6.0 to 10 (height 4, center Y = 8.0, X = 4.5)
            CreateSolidBox(colliders, visuals, new Vector2(4.5f, 8f), new Vector2(1f, 4f), "Wall_Right_Upper");

            // Props and Architecture
            // Grand Entrance Archway framing the portal
            CreateProp(visuals, "Tilesetv3_3", new Vector3(-20f, 3.2f, 0f), new Vector3(3f, 3f, 1f), 1);
            CreateProp(visuals, "Tilesetv3_6", new Vector3(-13f, 3.0f, 0f), new Vector3(2.5f, 2.5f, 1f), 1);

            // Life-size Knight Armor Statues
            CreateProp(visuals, "Tilesetv3_1", new Vector3(-16f, 1.8f, 0f), new Vector3(4.5f, 4.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_2", new Vector3(-6f, 1.8f, 0f), new Vector3(4.5f, 4.5f, 1f), 2);

            // Stairs leading down to Dungeon
            CreateProp(visuals, "Tilesetv3_40", new Vector3(0f, 0.5f, 0f), new Vector3(3f, 3f, 1f), 2);

            // Weapon racks and crates
            CreateProp(visuals, "Tilesetv3_8", new Vector3(-10f, 0.8f, 0f), new Vector3(3f, 3f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_11", new Vector3(-3f, 0.8f, 0f), new Vector3(3f, 3f, 1f), 2);

            // Torches with Point Lights
            CreateTorch(visuals, new Vector3(-18f, 4.5f, 0f));
            CreateTorch(visuals, new Vector3(-11f, 5.0f, 0f));
            CreateTorch(visuals, new Vector3(-4f, 4.5f, 0f));
            CreateTorch(visuals, new Vector3(3f, 5.0f, 0f));

            return room;
        }

        // =========================================================================
        // ROOM 2: Sub-Level - Dungeon & Prison Cells (คุกใต้ดิน)
        // =========================================================================
        private static CastleRoom BuildDungeon(Transform parent)
        {
            // Center = (-5, -8.5), Size = (36, 11)
            // Floor Y = -14, Ceiling Y = -3, Left X = -23, Right X = 13
            var room = CreateRoomRoot(parent, "Room_Dungeon", new Vector2(-5f, -8.5f), new Vector2(36f, 11f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Dark damp subterranean background
            CreateBackdrop(visuals, new Vector2(-5f, -8.5f), new Vector2(36f, 11f), new Color(0.08f, 0.08f, 0.12f, 1f));

            // Solid Floor: X = -23 to 13 (width 36, center -5, Y = -14.5)
            CreateSolidBox(colliders, visuals, new Vector2(-5f, -14.5f), new Vector2(36f, 1f), "Floor");

            // Solid Ceiling: X = -23 to 13 (with stair hole at X: -2 to 2)
            CreateSolidBox(colliders, visuals, new Vector2(-12.5f, -2.5f), new Vector2(21f, 1f), "Ceiling_Left");
            CreateSolidBox(colliders, visuals, new Vector2(7.5f, -2.5f), new Vector2(11f, 1f), "Ceiling_Right");

            // Solid Left Wall: X = -23.5, Y = -8.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(-23.5f, -8.5f), new Vector2(1f, 11f), "Wall_Left");

            // Solid Right Wall: X = 13.5, Y = -8.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(13.5f, -8.5f), new Vector2(1f, 11f), "Wall_Right");

            // Prison Cell 1 (Left): Iron bars
            for (int i = 0; i < 5; i++)
            {
                CreateProp(visuals, "Tilesetv3_44", new Vector3(-20f + i * 1.8f, -12.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
                CreateProp(visuals, "Tilesetv3_45", new Vector3(-20f + i * 1.8f, -9.0f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
            }

            // Prison Cell 2 (Right): Iron bars
            for (int i = 0; i < 5; i++)
            {
                CreateProp(visuals, "Tilesetv3_47", new Vector3(4f + i * 1.8f, -12.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
                CreateProp(visuals, "Tilesetv3_48", new Vector3(4f + i * 1.8f, -9.0f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
            }

            // Torture stone slabs & chains
            CreateProp(visuals, "Tilesetv3_46", new Vector3(-8f, -13.2f, 0f), new Vector3(3.5f, 3f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_46", new Vector3(2f, -13.2f, 0f), new Vector3(3.5f, 3f, 1f), 2);

            // Dungeon stone arches
            CreateProp(visuals, "Tilesetv3_42", new Vector3(-5f, -11.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 1);

            // Access Stairway / Ladder connecting Y = -14 up to Y = 0
            for (float y = -13f; y <= -3f; y += 2.5f)
            {
                CreateProp(visuals, "Tilesetv3_40", new Vector3(0f, y, 0f), new Vector3(3f, 3f, 1f), 1);
            }

            // Eerie Green/Purple Dungeon Torches
            CreateTorch(visuals, new Vector3(-18f, -7f, 0f), new Color(0.3f, 1.0f, 0.5f), 2.2f);
            CreateTorch(visuals, new Vector3(-10f, -6.5f, 0f), new Color(0.9f, 0.4f, 1.0f), 2.2f);
            CreateTorch(visuals, new Vector3(1f, -6.5f, 0f), new Color(0.9f, 0.4f, 1.0f), 2.2f);
            CreateTorch(visuals, new Vector3(10f, -7f, 0f), new Color(0.3f, 1.0f, 0.5f), 2.2f);

            return room;
        }

        // =========================================================================
        // ROOM 3: Central Nexus - Elevator & Stairway Shaft
        // =========================================================================
        private static CastleRoom BuildCentralShaft(Transform parent)
        {
            // Center = (10, 19), Size = (12, 38)
            // Floor Y = 0, Top Y = 38, Left X = 4, Right X = 16
            var room = CreateRoomRoot(parent, "Room_CentralShaft", new Vector2(10f, 19f), new Vector2(12f, 38f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Shaft backdrop
            CreateBackdrop(visuals, new Vector2(10f, 19f), new Vector2(12f, 38f), new Color(0.10f, 0.08f, 0.14f, 1f));

            // Solid Floor: X = 4 to 16 (width 12, center 10, Y = -0.5)
            CreateSolidBox(colliders, visuals, new Vector2(10f, -0.5f), new Vector2(12f, 1f), "Floor");

            // Solid Top: X = 4 to 16 (with doorway to Upper Battlements at X: 8 to 12)
            CreateSolidBox(colliders, visuals, new Vector2(6f, 38.5f), new Vector2(4f, 1f), "Top_Left");
            CreateSolidBox(colliders, visuals, new Vector2(14f, 38.5f), new Vector2(4f, 1f), "Top_Right");

            // Left Wall:
            // Opening to Entrance Hall: Y = 0 to 6.0 (Doorway clearance 6.0)
            // Wall segment 1: Y = 6.0 to 13.0 (height 7, center 9.5)
            CreateSolidBox(colliders, visuals, new Vector2(3.5f, 9.5f), new Vector2(1f, 7f), "Wall_Left_Mid");
            // Opening to Throne Room: Y = 13.0 to 19.0 (Doorway clearance 6.0)
            // Wall segment 2: Y = 19.0 to 38.0 (height 19, center 28.5)
            CreateSolidBox(colliders, visuals, new Vector2(3.5f, 28.5f), new Vector2(1f, 19f), "Wall_Left_Upper");

            // Right Wall:
            // Wall segment 1: Y = 0 to 26.0 (height 26, center 13.0)
            CreateSolidBox(colliders, visuals, new Vector2(16.5f, 13f), new Vector2(1f, 26f), "Wall_Right_Lower");
            // Opening to Library: Y = 26.0 to 32.0 (Doorway clearance 6.0)
            // Wall segment 2: Y = 32.0 to 38.0 (height 6, center 35.0)
            CreateSolidBox(colliders, visuals, new Vector2(16.5f, 35f), new Vector2(1f, 6f), "Wall_Right_Upper");

            // Ascending Platforms (Spaced 5.5 to 6.0 units for 2x Monster clearance!)
            CreateOneWayPlatform(colliders, visuals, new Vector2(10f, 6f), new Vector2(7f, 0.6f), "Platform_1");
            CreateOneWayPlatform(colliders, visuals, new Vector2(8f, 12.5f), new Vector2(6f, 0.6f), "Platform_2");
            CreateOneWayPlatform(colliders, visuals, new Vector2(11f, 18.5f), new Vector2(6f, 0.6f), "Platform_3");
            CreateOneWayPlatform(colliders, visuals, new Vector2(13f, 25.5f), new Vector2(6f, 0.6f), "Platform_4");
            CreateOneWayPlatform(colliders, visuals, new Vector2(9f, 31.5f), new Vector2(6f, 0.6f), "Platform_5");
            CreateOneWayPlatform(colliders, visuals, new Vector2(10f, 37f), new Vector2(5f, 0.6f), "Platform_6");

            // Torches
            CreateTorch(visuals, new Vector3(5f, 7f, 0f));
            CreateTorch(visuals, new Vector3(14f, 14f, 0f));
            CreateTorch(visuals, new Vector3(5f, 20f, 0f));
            CreateTorch(visuals, new Vector3(14f, 27f, 0f));
            CreateTorch(visuals, new Vector3(6f, 33f, 0f));

            return room;
        }

        // =========================================================================
        // ROOM 4: Level 1 - Demon Throne Room (ห้องบัลลังก์ปีศาจ)
        // =========================================================================
        private static CastleRoom BuildThroneRoom(Transform parent)
        {
            // Center = (-11, 18.5), Size = (30, 11)
            // Floor Y = 13, Ceiling Y = 24, Left X = -26, Right X = 4
            var room = CreateRoomRoot(parent, "Room_ThroneRoom", new Vector2(-11f, 18.5f), new Vector2(30f, 11f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Regal demonic dark red/purple backdrop
            CreateBackdrop(visuals, new Vector2(-11f, 18.5f), new Vector2(30f, 11f), new Color(0.20f, 0.10f, 0.16f, 1f));

            // Solid Floor: X = -26 to 4 (width 30, center -11, Y = 12.5)
            CreateSolidBox(colliders, visuals, new Vector2(-11f, 12.5f), new Vector2(30f, 1f), "Floor");

            // Solid Ceiling: X = -26 to 4 (width 30, center -11, Y = 24.5)
            CreateSolidBox(colliders, visuals, new Vector2(-11f, 24.5f), new Vector2(30f, 1f), "Ceiling");

            // Solid Left Wall: X = -26.5, Y = 18.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(-26.5f, 18.5f), new Vector2(1f, 11f), "Wall_Left");

            // Solid Right Wall: Doorway to Shaft from Y = 13.0 to 19.0 (Height 6.0 clearance)
            CreateSolidBox(colliders, visuals, new Vector2(4.5f, 21.5f), new Vector2(1f, 5f), "Wall_Right_Upper");

            // Throne Dais (Raised platform for Demon Lord throne)
            CreateSolidBox(colliders, visuals, new Vector2(-19f, 13.4f), new Vector2(10f, 0.8f), "Dais_Step1");
            CreateSolidBox(colliders, visuals, new Vector2(-19f, 14.2f), new Vector2(6f, 0.8f), "Dais_Step2");

            // Demon Throne Visual
            CreateProp(visuals, "Tilesetv3_35", new Vector3(-19f, 16.8f, 0f), new Vector3(3.5f, 4.0f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_19", new Vector3(-19f, 15.3f, 0f), new Vector3(3.2f, 3.2f, 1f), 3);

            // Demon Banners & Royal Crest
            CreateBackdrop(visuals, new Vector2(-19f, 20f), new Vector2(5f, 7f), new Color(0.75f, 0.12f, 0.18f, 0.95f));

            // Grand Gothic Arches
            CreateProp(visuals, "Tilesetv3_3", new Vector3(-10f, 16.5f, 0f), new Vector3(3.2f, 3.2f, 1f), 1);
            CreateProp(visuals, "Tilesetv3_6", new Vector3(-2f, 16.0f, 0f), new Vector3(3.0f, 3.0f, 1f), 1);

            // Flanking Knight Guard Statues
            CreateProp(visuals, "Tilesetv3_1", new Vector3(-23f, 15.8f, 0f), new Vector3(4.5f, 4.5f, 1f), 4);
            CreateProp(visuals, "Tilesetv3_2", new Vector3(-15f, 15.8f, 0f), new Vector3(4.5f, 4.5f, 1f), 4);

            // Torches and Grand Chandeliers
            CreateTorch(visuals, new Vector3(-24f, 18.5f, 0f), new Color(1f, 0.5f, 0.2f), 2.2f);
            CreateTorch(visuals, new Vector3(-14f, 19.0f, 0f), new Color(1f, 0.5f, 0.2f), 2.2f);
            CreateTorch(visuals, new Vector3(-6f, 18.5f, 0f), new Color(1f, 0.5f, 0.2f), 2.2f);
            CreateTorch(visuals, new Vector3(2f, 19.0f, 0f), new Color(1f, 0.5f, 0.2f), 2.2f);

            return room;
        }

        // =========================================================================
        // ROOM 5: Level 2 - Arcane Library & Laboratory (ห้องสมุด & คลังอาคม)
        // =========================================================================
        private static CastleRoom BuildLibrary(Transform parent)
        {
            // Center = (30, 31.5), Size = (28, 11)
            // Floor Y = 26, Ceiling Y = 37, Left X = 16, Right X = 44
            var room = CreateRoomRoot(parent, "Room_Library", new Vector2(30f, 31.5f), new Vector2(28f, 11f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Mystic dark blue/indigo backdrop
            CreateBackdrop(visuals, new Vector2(30f, 31.5f), new Vector2(28f, 11f), new Color(0.10f, 0.11f, 0.18f, 1f));

            // Solid Floor: X = 16 to 44 (width 28, center 30, Y = 25.5)
            CreateSolidBox(colliders, visuals, new Vector2(30f, 25.5f), new Vector2(28f, 1f), "Floor");

            // Solid Ceiling: X = 16 to 44 (width 28, center 30, Y = 37.5)
            CreateSolidBox(colliders, visuals, new Vector2(30f, 37.5f), new Vector2(28f, 1f), "Ceiling");

            // Solid Left Wall: Doorway from Shaft at Y = 26.0 to 32.0 (Height 6.0 clearance)
            CreateSolidBox(colliders, visuals, new Vector2(15.5f, 34.5f), new Vector2(1f, 5f), "Wall_Left_Upper");

            // Solid Right Wall: X = 44.5, Y = 31.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(44.5f, 31.5f), new Vector2(1f, 11f), "Wall_Right");

            // Multi-Tier Bookshelves
            CreateProp(visuals, "Tilesetv3_14", new Vector3(22f, 28.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_12", new Vector3(26f, 28.2f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_35", new Vector3(38f, 28.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_14", new Vector3(41f, 28.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 2);

            // Alchemy Research Table & Flasks
            CreateProp(visuals, "Tilesetv3_33", new Vector3(31f, 27.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 3);
            CreateProp(visuals, "Tilesetv3_41", new Vector3(34f, 27.5f, 0f), new Vector3(3.0f, 3.0f, 1f), 3);

            // Upper Floating Book Platform: Y = 31.5 (Accessible by jumping)
            CreateOneWayPlatform(colliders, visuals, new Vector2(30f, 31.5f), new Vector2(12f, 0.6f), "Library_Platform");
            CreateProp(visuals, "Tilesetv3_12", new Vector3(27f, 33.5f, 0f), new Vector3(2.5f, 2.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_14", new Vector3(33f, 34.0f, 0f), new Vector3(2.5f, 2.5f, 1f), 2);

            // Mystic Arched Windows
            CreateProp(visuals, "Tilesetv3_15", new Vector3(20f, 33.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 1);
            CreateProp(visuals, "Tilesetv3_15", new Vector3(40f, 33.5f, 0f), new Vector3(3.5f, 3.5f, 1f), 1);

            // Mystic Cyan/Violet Torches
            CreateTorch(visuals, new Vector3(18f, 29.5f, 0f), new Color(0.4f, 0.6f, 1f), 2.0f);
            CreateTorch(visuals, new Vector3(30f, 35.0f, 0f), new Color(0.8f, 0.3f, 1f), 2.0f);
            CreateTorch(visuals, new Vector3(42f, 29.5f, 0f), new Color(0.4f, 0.6f, 1f), 2.0f);

            return room;
        }

        // =========================================================================
        // ROOM 6: Level 3 - Castle Spire & Upper Battlements (ยอดป้อมปราการ & เชิงเทิน)
        // =========================================================================
        private static CastleRoom BuildUpperBattlements(Transform parent)
        {
            // Center = (10, 44.5), Size = (32, 11)
            // Floor Y = 39, Top Y = 50, Left X = -6, Right X = 26
            var room = CreateRoomRoot(parent, "Room_UpperBattlements", new Vector2(10f, 44.5f), new Vector2(32f, 11f));
            Transform visuals = room.transform.Find("Content/Visuals");
            Transform colliders = room.transform.Find("Content/Colliders");

            // Night Sky dark backdrop
            CreateBackdrop(visuals, new Vector2(10f, 44.5f), new Vector2(32f, 11f), new Color(0.08f, 0.07f, 0.14f, 1f));

            // Solid Floor: X = -6 to 26 (width 32, center 10, Y = 38.5)
            CreateSolidBox(colliders, visuals, new Vector2(1f, 38.5f), new Vector2(14f, 1f), "Floor_Left");
            CreateSolidBox(colliders, visuals, new Vector2(19f, 38.5f), new Vector2(14f, 1f), "Floor_Right");

            // Solid Left Parapet Wall: X = -6.5, Y = 44.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(-6.5f, 44.5f), new Vector2(1f, 11f), "Wall_Left");

            // Solid Right Parapet Wall: X = 26.5, Y = 44.5 (height 11)
            CreateSolidBox(colliders, visuals, new Vector2(26.5f, 44.5f), new Vector2(1f, 11f), "Wall_Right");

            // Solid Ceiling / Sky limit: Y = 50.5
            CreateSolidBox(colliders, visuals, new Vector2(10f, 50.5f), new Vector2(32f, 1f), "Ceiling");

            // Stone Crenellations & Battlements along the roof
            for (float x = -5f; x <= 25f; x += 3.5f)
            {
                CreateProp(visuals, "Tilesetv3_18", new Vector3(x, 40f, 0f), new Vector3(3f, 3f, 1f), 1);
            }

            // High Gothic Spire Towers & Statues
            CreateProp(visuals, "Tilesetv3_0", new Vector3(-2f, 43f, 0f), new Vector3(1.5f, 1.5f, 1f), 0);
            CreateProp(visuals, "Tilesetv3_0", new Vector3(22f, 43f, 0f), new Vector3(1.5f, 1.5f, 1f), 0);
            CreateProp(visuals, "Tilesetv3_1", new Vector3(4f, 41.2f, 0f), new Vector3(4.5f, 4.5f, 1f), 2);
            CreateProp(visuals, "Tilesetv3_2", new Vector3(16f, 41.2f, 0f), new Vector3(4.5f, 4.5f, 1f), 2);

            // Spooky Dead Tree atop the castle ruins
            CreateProp(visuals, "Tilesetv3_49", new Vector3(10f, 42.0f, 0f), new Vector3(4.5f, 4.5f, 1f), 1);

            // Rooftop Braziers & Torches
            CreateTorch(visuals, new Vector3(-4f, 42.5f, 0f), new Color(1f, 0.4f, 0.1f), 2.4f);
            CreateTorch(visuals, new Vector3(7f, 41.5f, 0f), new Color(1f, 0.4f, 0.1f), 2.4f);
            CreateTorch(visuals, new Vector3(13f, 41.5f, 0f), new Color(1f, 0.4f, 0.1f), 2.4f);
            CreateTorch(visuals, new Vector3(24f, 42.5f, 0f), new Color(1f, 0.4f, 0.1f), 2.4f);

            return room;
        }

        // =========================================================================
        // HELPER BUILDERS
        // =========================================================================

        private static void CreateBackdrop(Transform parent, Vector2 center, Vector2 size, Color color)
        {
            GameObject bgGo = new GameObject("Backdrop");
            bgGo.transform.SetParent(parent);
            bgGo.transform.position = new Vector3(center.x, center.y, 1f);

            var sr = bgGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite("Tilesetv3_18");
            sr.color = color;
            sr.material = _spriteLitMat;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = -10;
        }

        private static void CreateSolidBox(Transform colParent, Transform visParent, Vector2 center, Vector2 size, string name)
        {
            // Collider
            GameObject boxGo = new GameObject(name);
            boxGo.transform.SetParent(colParent);
            boxGo.transform.position = new Vector3(center.x, center.y, 0f);
            var col = boxGo.AddComponent<BoxCollider2D>();
            col.size = size;

            // Visual Stone Floor / Wall
            GameObject visGo = new GameObject(name + "_Vis");
            visGo.transform.SetParent(visParent);
            visGo.transform.position = new Vector3(center.x, center.y, 0f);
            var sr = visGo.AddComponent<SpriteRenderer>();
            sr.sprite = _stoneFloorSprite != null ? _stoneFloorSprite : GetSprite("Tilesetv3_18");
            sr.material = _spriteLitMat;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = 0;
        }

        private static void CreateOneWayPlatform(Transform colParent, Transform visParent, Vector2 center, Vector2 size, string name)
        {
            GameObject platGo = new GameObject(name);
            platGo.transform.SetParent(colParent);
            platGo.transform.position = new Vector3(center.x, center.y, 0f);
            var col = platGo.AddComponent<BoxCollider2D>();
            col.size = size;

            GameObject visGo = new GameObject(name + "_Vis");
            visGo.transform.SetParent(visParent);
            visGo.transform.position = new Vector3(center.x, center.y, 0f);
            var sr = visGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite("Tilesetv3_46");
            sr.material = _spriteLitMat;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = 1;
        }

        private static void CreateProp(Transform parent, string spriteName, Vector3 pos, Vector3 scale, int order)
        {
            Sprite s = GetSprite(spriteName);
            if (s == null) return;

            GameObject prop = new GameObject("Prop_" + spriteName);
            prop.transform.SetParent(parent);
            prop.transform.position = pos;
            prop.transform.localScale = scale;

            var sr = prop.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.material = _spriteLitMat;
            sr.sortingOrder = order;
        }

        private static void CreateTorch(Transform parent, Vector3 pos, Color? color = null, float intensity = 2.0f)
        {
            GameObject torchGo = new GameObject("Torch");
            torchGo.transform.SetParent(parent);
            torchGo.transform.position = pos;
            torchGo.transform.localScale = new Vector3(3.5f, 3.5f, 1f);

            var sr = torchGo.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite("spr_torch_0");
            sr.material = _spriteUnlitMat != null ? _spriteUnlitMat : _spriteLitMat;
            sr.sortingOrder = 5;

            // 2D Point Light
            GameObject lightGo = new GameObject("TorchLight");
            lightGo.transform.SetParent(torchGo.transform);
            lightGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color ?? new Color(1f, 0.65f, 0.25f, 1f);
            light.intensity = intensity;
            light.pointLightInnerRadius = 0.8f;
            light.pointLightOuterRadius = 7.0f;
        }

        private static Sprite GetSprite(string name)
        {
            if (_sprites.TryGetValue(name, out Sprite s))
            {
                return s;
            }
            return null;
        }

        private static void SetupPlayerAndPortal(CastleRoom entranceRoom)
        {
            // Position Portal_Left inside Entrance Hall
            var portal = GameObject.Find("Portal_Left");
            if (portal != null)
            {
                Undo.RecordObject(portal.transform, "Position Portal_Left");
                portal.transform.position = new Vector3(-20f, 2.2f, 0f);
                var scenePortal = portal.GetComponent<ScenePortal>();
                if (scenePortal != null)
                {
                    scenePortal.targetSceneName = "SuburbToForest";
                }
            }

            // Position Player in front of portal
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Position Player");
                player.transform.position = new Vector3(-16f, 1.5f, 0f);
            }
        }

        private static void SetupCamera(CastleRoom entranceRoom)
        {
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(-16f, 5f, -10f);
                var camFollow = cam.GetComponent<CameraFollow2D>();
                if (camFollow != null && entranceRoom != null)
                {
                    Undo.RecordObject(camFollow, "Set Camera Boundaries");
                    camFollow.SetBoundaries(entranceRoom.CameraConfiner);
                }
            }
        }
    }
}
