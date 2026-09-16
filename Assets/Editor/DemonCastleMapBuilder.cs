using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using TheLastKnight.Camera;
using TheLastKnight.Environment;

namespace TheLastKnight.Editor
{
    public static class DemonCastleMapBuilder
    {
        // Floor 1 Assets (dungeon_sidescroller-Raou)
        private const string Room1SpritePath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/Room1_GrandPillarHall.png";
        private const string Room2SpritePath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/Room2_TowerShaft.png";
        private const string Room3SpritePath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/Room3_SunkenStudy_Expanded.png";

        // Floor 2 & 3 Assets (PlatformerSet1 Expanded)
        private const string Floor2SpritePath = "Assets/sprites/Environment/PlatformerSet1/Floor2_GreenBastion_Expanded.png";
        private const string Floor2WestWingPath = "Assets/sprites/Environment/PlatformerSet1/Floor2_WestWing_AlchemistLab.png";
        private const string Floor2EastWingPath = "Assets/sprites/Environment/PlatformerSet1/Floor2_EastWing_Ramparts.png";
        private const string Floor3SpritePath = "Assets/sprites/Environment/PlatformerSet1/Floor3_BlackSanctum_Expanded.png";
        private const string Floor3WestWingPath = "Assets/sprites/Environment/PlatformerSet1/Floor3_WestWing_TortureCrypt.png";
        private const string Floor3EastWingPath = "Assets/sprites/Environment/PlatformerSet1/Floor3_EastWing_CeremonialGate.png";
        private const string InterFloorF1F2Path = "Assets/sprites/Environment/PlatformerSet1/InterFloor_Divider_F1_F2.png";
        private const string InterFloorF2F3Path = "Assets/sprites/Environment/PlatformerSet1/InterFloor_Divider_F2_F3.png";
        private const string StairsUpSpritePath = "Assets/sprites/Environment/PlatformerSet1/spr_spiral_stairs_up.png";
        private const string StairsDownSpritePath = "Assets/sprites/Environment/PlatformerSet1/spr_spiral_stairs_down.png";
        private const string Floor1MasonrySpritePath = "Assets/sprites/Environment/dungeon_sidescroller-Raou/Floor1_WestUpperMasonry.png";

        private static Sprite _sRoom1;
        private static Sprite _sRoom2;
        private static Sprite _sRoom3;
        private static Sprite _sMasonry;
        private static Sprite _sFloor2;
        private static Sprite _sFloor2West;
        private static Sprite _sFloor2East;
        private static Sprite _sFloor3;
        private static Sprite _sFloor3West;
        private static Sprite _sFloor3East;
        private static Sprite _sInterFloor12;
        private static Sprite _sInterFloor23;
        private static Sprite _sStairsUp;
        private static Sprite _sStairsDown;
        private static Material _spriteLitMat;

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
            Undo.RegisterCreatedObjectUndo(mapRoot, "Build Demon Castle Interior (Stacked)");

            SetupGlobalLighting();

            // Template prompt canvas from Portal_Left if available
            GameObject templatePrompt = null;
            var portalObj = GameObject.Find("Portal_Left");
            if (portalObj != null)
            {
                var canvasTr = portalObj.transform.Find("PopupCanvas");
                if (canvasTr != null)
                {
                    templatePrompt = canvasTr.gameObject;
                }
            }

            // Build the 3 realistic stacked floors
            CastleRoom f1 = BuildFloor1_Stacked(mapRoot.transform, templatePrompt);
            CastleRoom f2 = BuildFloor2_Stacked(mapRoot.transform, templatePrompt);
            CastleRoom f3 = BuildFloor3_Stacked(mapRoot.transform, templatePrompt);

            // Build massive structural inter-floor masonry slabs
            BuildInterFloorSlabs(mapRoot.transform);

            // Connect Spiral Staircases between vertically stacked floors
            ConnectStackedStaircases(f1, f2, f3);

            // Configure Player and Portal_Left
            SetupPlayerAndPortal(f1);

            // Setup Camera on Floor 1
            SetupCamera(f1);

            Physics2D.SyncTransforms();

            // Remove any old placeholder Ground
            GameObject oldGround = GameObject.Find("Ground");
            if (oldGround != null && oldGround.transform.parent == null)
            {
                Undo.DestroyObjectImmediate(oldGround);
            }

            EditorUtility.SetDirty(mapRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapRoot.scene);
            Debug.Log("<color=green>Demon Castle Interior (Stacked Realistic Architecture) built successfully!</color>");
        }

        private static void LoadAssets()
        {
            _sRoom1 = AssetDatabase.LoadAssetAtPath<Sprite>(Room1SpritePath);
            _sRoom2 = AssetDatabase.LoadAssetAtPath<Sprite>(Room2SpritePath);
            _sRoom3 = AssetDatabase.LoadAssetAtPath<Sprite>(Room3SpritePath);
            _sMasonry = AssetDatabase.LoadAssetAtPath<Sprite>(Floor1MasonrySpritePath);
            _sFloor2 = AssetDatabase.LoadAssetAtPath<Sprite>(Floor2SpritePath);
            _sFloor2West = AssetDatabase.LoadAssetAtPath<Sprite>(Floor2WestWingPath);
            _sFloor2East = AssetDatabase.LoadAssetAtPath<Sprite>(Floor2EastWingPath);
            _sFloor3 = AssetDatabase.LoadAssetAtPath<Sprite>(Floor3SpritePath);
            _sFloor3West = AssetDatabase.LoadAssetAtPath<Sprite>(Floor3WestWingPath);
            _sFloor3East = AssetDatabase.LoadAssetAtPath<Sprite>(Floor3EastWingPath);
            _sInterFloor12 = AssetDatabase.LoadAssetAtPath<Sprite>(InterFloorF1F2Path);
            _sInterFloor23 = AssetDatabase.LoadAssetAtPath<Sprite>(InterFloorF2F3Path);
            _sStairsUp = AssetDatabase.LoadAssetAtPath<Sprite>(StairsUpSpritePath);
            _sStairsDown = AssetDatabase.LoadAssetAtPath<Sprite>(StairsDownSpritePath);
            _spriteLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
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
                    light.color = new Color(0.65f, 0.60f, 0.75f, 1f);
                    light.intensity = 0.85f;
                }
            }

            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            }
        }

        // =========================================================================
        // FLOOR 1: Grand Castle Fortress (dungeon_sidescroller-Raou)
        // Stack Level: Base Y = 0. Spans X from -61 to +61 (Width ~122)
        // Integrates: Room 3 (West Vault) + Room 1 (Grand Hall) + Room 2 (East Spiral Shaft)
        // =========================================================================
        private static CastleRoom BuildFloor1_Stacked(Transform parent, GameObject templatePrompt)
        {
            GameObject roomGo = new GameObject("Floor1_GrandFortress");
            roomGo.transform.SetParent(parent, false);
            roomGo.transform.localPosition = Vector3.zero;

            var triggerCol = roomGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.offset = new Vector2(-7.25f, 0f);
            triggerCol.size = new Vector2(138.5f, 24.0f);

            GameObject confinerGo = new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(roomGo.transform, false);
            confinerGo.transform.localPosition = Vector3.zero;
            var confinerCol = confinerGo.AddComponent<BoxCollider2D>();
            confinerCol.isTrigger = true;
            confinerCol.offset = new Vector2(-7.25f, 0f);
            confinerCol.size = new Vector2(138.5f, 24.0f);

            GameObject visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(roomGo.transform, false);
            visualsGo.transform.localPosition = Vector3.zero;

            // 1. West Wing: Room 3 Expanded Sunken Study & Library (Width 40.0, centered at X = -56.0, Y = 0)
            GameObject r3Go = new GameObject("Wing_West_SunkenStudy");
            r3Go.transform.SetParent(visualsGo.transform, false);
            r3Go.transform.localPosition = new Vector3(-56.0f, 0f, 0f);
            var sr3 = r3Go.AddComponent<SpriteRenderer>();
            sr3.sprite = _sRoom3;
            sr3.material = _spriteLitMat;
            sr3.sortingOrder = -21;

            // 2. Central Hall: Room 1 Grand Pillar Hall (Width 72.57, centered at X = 0, Y = 0)
            GameObject r1Go = new GameObject("Hall_Center_GrandPillars");
            r1Go.transform.SetParent(visualsGo.transform, false);
            r1Go.transform.localPosition = Vector3.zero;
            var sr1 = r1Go.AddComponent<SpriteRenderer>();
            sr1.sprite = _sRoom1;
            sr1.material = _spriteLitMat;
            sr1.sortingOrder = -21;

            // 3. East Tower: Room 2 Tower Shaft (Width 24.79, centered at X = +48.67, Y = 0)
            GameObject r2Go = new GameObject("Tower_East_Shaft");
            r2Go.transform.SetParent(visualsGo.transform, false);
            r2Go.transform.localPosition = new Vector3(48.67f, 0f, 0f);
            var sr2 = r2Go.AddComponent<SpriteRenderer>();
            sr2.sprite = _sRoom2;
            sr2.material = _spriteLitMat;
            sr2.sortingOrder = -21;

            // Colliders
            GameObject collidersGo = new GameObject("Colliders");
            collidersGo.transform.SetParent(roomGo.transform, false);
            collidersGo.transform.localPosition = Vector3.zero;

            // West Wing Colliders (Expanded Sunken Study & Mezzanine)
            CreateBox(collidersGo.transform, new Vector2(-72.2f, -3.85f), new Vector2(3.2f, 1.5f), "Floor_WestLedge");
            CreateBox(collidersGo.transform, new Vector2(-57.8f, -6.65f), new Vector2(25.5f, 1.4f), "Floor_WestSunken");
            CreateBox(collidersGo.transform, new Vector2(-60.1f, 1.15f), new Vector2(19.6f, 0.4f), "Mezzanine_Walkway");
            CreateBox(collidersGo.transform, new Vector2(-46.3f, -1.70f), new Vector2(2.6f, 0.4f), "Platform_Wall_1");
            CreateBox(collidersGo.transform, new Vector2(-48.3f, -0.27f), new Vector2(2.6f, 0.4f), "Platform_Wall_2");
            CreateBox(collidersGo.transform, new Vector2(-44.0f, -5.5f), new Vector2(1.0f, 0.6f), "WestStair_1");
            CreateBox(collidersGo.transform, new Vector2(-43.0f, -4.7f), new Vector2(1.0f, 0.6f), "WestStair_2");
            CreateBox(collidersGo.transform, new Vector2(-42.0f, -3.9f), new Vector2(1.0f, 0.6f), "WestStair_3");
            CreateBox(collidersGo.transform, new Vector2(-39.4f, -3.85f), new Vector2(6.7f, 1.5f), "Floor_WestEntranceLanding");

            // Central Grand Hall Colliders
            CreateBox(collidersGo.transform, new Vector2(-27.3f, -3.85f), new Vector2(18f, 1.5f), "Floor_Left");
            CreateBox(collidersGo.transform, new Vector2(-9.0f, -3.85f), new Vector2(9.0f, 1.5f), "Floor_Pillar2");
            CreateBox(collidersGo.transform, new Vector2(5.0f, -3.85f), new Vector2(9.0f, 1.5f), "Floor_Pillar3");
            CreateBox(collidersGo.transform, new Vector2(25.1f, -3.85f), new Vector2(22.5f, 1.5f), "Floor_Right");
            CreateBox(collidersGo.transform, new Vector2(0f, -11.0f), new Vector2(73f, 1.5f), "Floor_PitBase");

            // East Tower Shaft Colliders
            CreateBox(collidersGo.transform, new Vector2(41.0f, -5.0f), new Vector2(9.5f, 0.6f), "Ledge_EastMidLeft");
            CreateBox(collidersGo.transform, new Vector2(48.67f, -9.0f), new Vector2(25f, 1.5f), "Floor_EastBottom");
            CreateBox(collidersGo.transform, new Vector2(55.67f, -3.9f), new Vector2(6.0f, 0.5f), "Platform_EastMid1");
            CreateBox(collidersGo.transform, new Vector2(50.67f, 0.4f), new Vector2(6.0f, 0.5f), "Platform_EastMid2");
            CreateBox(collidersGo.transform, new Vector2(41.67f, 4.3f), new Vector2(11.0f, 0.6f), "Ledge_EastUpper");

            // Outer Perimeter Colliders
            CreateBox(collidersGo.transform, new Vector2(-76.0f, 0f), new Vector2(1f, 24f), "Wall_OuterLeft");
            CreateBox(collidersGo.transform, new Vector2(61.5f, 0f), new Vector2(1f, 24f), "Wall_OuterRight");
            CreateBox(collidersGo.transform, new Vector2(-56.0f, 9.5f), new Vector2(40f, 1.5f), "Ceiling_West");
            CreateBox(collidersGo.transform, new Vector2(0f, 9.5f), new Vector2(73f, 1.5f), "Ceiling_Center");
            CreateBox(collidersGo.transform, new Vector2(48.67f, 9.5f), new Vector2(25f, 1.5f), "Ceiling_East");

            // Point Lights Across Floor 1
            CreatePointLight(roomGo.transform, new Vector3(-67.0f, -4.5f, 0f), new Color(1f, 0.70f, 0.35f), 2.2f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-53.0f, -4.5f, 0f), new Color(1f, 0.70f, 0.35f), 2.2f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-60.0f, 2.5f, 0f), new Color(1f, 0.75f, 0.40f), 2.0f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-22.5f, 0.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-9.0f, 0.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(4.8f, 0.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(23.0f, 0.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.0f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(48.0f, 5.0f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(52.0f, -5.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);

            // Spiral Staircase Up to Floor 2 (at East Tower Shaft bottom floor, X = 52.0, Y = -6.5)
            CreateSpiralDoorway(roomGo.transform, "Doorway_Stairs_F1_to_F2", new Vector3(52.0f, -6.5f, 0f), _sStairsUp, "กด F เพื่อขึ้นบันไดวนสู่ชั้น 2", templatePrompt);

            var roomComp = roomGo.AddComponent<CastleRoom>();
            SetField(roomComp, "_roomName", "Floor 1: Grand Fortress");
            SetField(roomComp, "_cameraConfiner", confinerCol);
            SetField(roomComp, "_roomContent", visualsGo);

            return roomComp;
        }

        // =========================================================================
        // =========================================================================
        // FLOOR 2: Green Bastion (PlatformerSet1 green_floor)
        // Stack Level: Center Y = 25.0. Spans X from -134.86 to +134.86 (Width ~269.7)
        // Wings: West Wing (Alchemist Lab) + Center Hall + East Wing (Ramparts)
        // =========================================================================
        private static CastleRoom BuildFloor2_Stacked(Transform parent, GameObject templatePrompt)
        {
            Vector3 origin = new Vector3(0f, 25.0f, 0f);
            GameObject roomGo = new GameObject("Floor2_GreenBastion");
            roomGo.transform.SetParent(parent, false);
            roomGo.transform.localPosition = origin;

            var triggerCol = roomGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.offset = new Vector2(0f, 2.0f);
            triggerCol.size = new Vector2(274.0f, 38.0f);

            GameObject confinerGo = new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(roomGo.transform, false);
            confinerGo.transform.localPosition = Vector3.zero;
            var confinerCol = confinerGo.AddComponent<BoxCollider2D>();
            confinerCol.isTrigger = true;
            confinerCol.offset = new Vector2(0f, 2.0f);
            confinerCol.size = new Vector2(272.0f, 36.0f);

            GameObject visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(roomGo.transform, false);
            visualsGo.transform.localPosition = Vector3.zero;

            // 1. West Wing: Alchemist Laboratory & Secret Archive (Width 73.14m, local X = -98.286, Y = -1.821)
            GameObject westWingGo = new GameObject("Wing_West_AlchemistLab");
            westWingGo.transform.SetParent(visualsGo.transform, false);
            westWingGo.transform.localPosition = new Vector3(-98.286f, -1.821f, 0f);
            var srWest = westWingGo.AddComponent<SpriteRenderer>();
            srWest.sprite = _sFloor2West;
            srWest.material = _spriteLitMat;
            srWest.sortingOrder = -17;

            // 2. Center Hall: Green Bastion Grand Hall (Width 123.43m, local X = 0, Y = 0)
            GameObject centerHallGo = new GameObject("Hall_Center_GreenBastion");
            centerHallGo.transform.SetParent(visualsGo.transform, false);
            centerHallGo.transform.localPosition = Vector3.zero;
            var srCenter = centerHallGo.AddComponent<SpriteRenderer>();
            srCenter.sprite = _sFloor2;
            srCenter.material = _spriteLitMat;
            srCenter.sortingOrder = -17;

            // 3. East Wing: Castle Ramparts & Balcony Terrace (Width 73.14m, local X = +98.286, Y = +3.643)
            GameObject eastWingGo = new GameObject("Wing_East_Ramparts");
            eastWingGo.transform.SetParent(visualsGo.transform, false);
            eastWingGo.transform.localPosition = new Vector3(98.286f, 3.643f, 0f);
            var srEast = eastWingGo.AddComponent<SpriteRenderer>();
            srEast.sprite = _sFloor2East;
            srEast.material = _spriteLitMat;
            srEast.sortingOrder = -17;

            GameObject collidersGo = new GameObject("Colliders");
            collidersGo.transform.SetParent(roomGo.transform, false);
            collidersGo.transform.localPosition = Vector3.zero;

            // West Wing Colliders (Alchemist Lab)
            CreateBox(collidersGo.transform, new Vector2(-121.4f, -9.8f), new Vector2(27.0f, 1.5f), "Floor_AlchemistStudy");
            CreateBox(collidersGo.transform, new Vector2(-99.0f, -7.5f), new Vector2(18.0f, 0.8f), "Stairs_AlchemistHall");
            CreateBox(collidersGo.transform, new Vector2(-75.8f, -6.2f), new Vector2(28.0f, 1.0f), "Floor_WestHallway");
            CreateBox(collidersGo.transform, new Vector2(-88.0f, 0.0f), new Vector2(6.0f, 0.6f), "Alcove_AlchemistCrystals");

            // Central Bastion Floor & Platforms
            CreateBox(collidersGo.transform, new Vector2(0f, -9.8f), new Vector2(124.0f, 1.5f), "Floor_Center_Main");
            CreateBox(collidersGo.transform, new Vector2(-45.1f, -0.7f), new Vector2(10.5f, 0.6f), "Platform_WestArmory");
            CreateBox(collidersGo.transform, new Vector2(-20.3f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_Bastion_Left");
            CreateBox(collidersGo.transform, new Vector2(0.4f, +2.1f), new Vector2(13.0f, 0.8f), "Platform_Bastion_HighCenter");
            CreateBox(collidersGo.transform, new Vector2(21.1f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_Bastion_Right");
            CreateBox(collidersGo.transform, new Vector2(43.4f, -0.7f), new Vector2(10.5f, 0.6f), "Platform_EastLanding");

            // East Wing Colliders (Ramparts)
            CreateBox(collidersGo.transform, new Vector2(71.0f, -9.8f), new Vector2(19.0f, 1.5f), "Floor_RampartsLowerHall");
            CreateBox(collidersGo.transform, new Vector2(87.0f, -13.0f), new Vector2(14.0f, 0.8f), "Stairs_RampartsLower");
            CreateBox(collidersGo.transform, new Vector2(114.4f, -17.2f), new Vector2(41.0f, 1.5f), "Floor_RampartsCourtyard");
            CreateBox(collidersGo.transform, new Vector2(95.0f, +6.8f), new Vector2(30.0f, 0.8f), "Walkway_RampartsUpper");
            CreateBox(collidersGo.transform, new Vector2(122.4f, +9.8f), new Vector2(25.0f, 0.8f), "Parapet_RampartsBalcony");
            CreateBox(collidersGo.transform, new Vector2(74.0f, +15.0f), new Vector2(12.0f, 0.8f), "Platform_RampartsCrateRoom");

            // Ceiling & Outer Castle Walls
            CreateBox(collidersGo.transform, new Vector2(0f, 25.0f), new Vector2(272f, 1.5f), "Ceiling");
            CreateBox(collidersGo.transform, new Vector2(-134.86f, 5.0f), new Vector2(1.5f, 40f), "Wall_Left_Outer");
            CreateBox(collidersGo.transform, new Vector2(134.86f, 5.0f), new Vector2(1.5f, 40f), "Wall_Right_Outer");

            // Point Lights Across Floor 2 Wings & Hall
            CreatePointLight(roomGo.transform, new Vector3(-120.0f, -6.5f, 0f), new Color(1f, 0.75f, 0.40f), 2.2f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-130.0f, -4.0f, 0f), new Color(0.4f, 0.90f, 0.80f), 1.8f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(-115.0f, -8.5f, 0f), new Color(0.2f, 0.75f, 1.0f), 2.0f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(-88.0f, 0.5f, 0f), new Color(0.2f, 0.80f, 1.0f), 2.2f, 6.5f);
            CreatePointLight(roomGo.transform, new Vector3(-75.0f, -3.0f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-45.0f, 2.0f, 0f), new Color(1f, 0.7f, 0.3f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-20.0f, 2.0f, 0f), new Color(1f, 0.7f, 0.3f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(0f, 4.0f, 0f), new Color(1f, 0.75f, 0.35f), 2.5f, 8.5f);
            CreatePointLight(roomGo.transform, new Vector3(21.0f, 2.0f, 0f), new Color(1f, 0.7f, 0.3f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(45.0f, 2.0f, 0f), new Color(1f, 0.7f, 0.3f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(56.3f, -5.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.0f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(-56.3f, -5.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.0f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(75.0f, -7.0f, 0f), new Color(1f, 0.60f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(90.0f, -7.0f, 0f), new Color(1f, 0.60f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(95.0f, 8.5f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(75.0f, 16.0f, 0f), new Color(1f, 0.65f, 0.25f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(115.0f, 12.0f, 0f), new Color(1.0f, 0.70f, 0.35f), 2.5f, 8.5f);
            CreatePointLight(roomGo.transform, new Vector3(132.0f, 12.0f, 0f), new Color(1.0f, 0.70f, 0.35f), 2.5f, 8.5f);

            // Right Spiral Stairs Down to Floor 1 East Tower (local X = 56.3, Y = -7.0)
            CreateSpiralDoorway(roomGo.transform, "Doorway_Stairs_F2_to_F1", new Vector3(56.3f, -7.0f, 0f), null, "กด F เพื่อลงบันไดวนสู่ชั้น 1", templatePrompt);

            // Left Spiral Stairs Up to Floor 3 (local X = -56.3, Y = -7.0)
            CreateSpiralDoorway(roomGo.transform, "Doorway_Stairs_F2_to_F3", new Vector3(-56.3f, -7.0f, 0f), null, "กด F เพื่อขึ้นบันไดวนสู่ชั้น 3", templatePrompt);

            var roomComp = roomGo.AddComponent<CastleRoom>();
            SetField(roomComp, "_roomName", "Floor 2: Green Bastion");
            SetField(roomComp, "_cameraConfiner", confinerCol);
            SetField(roomComp, "_roomContent", visualsGo);

            return roomComp;
        }

        // =========================================================================
        // FLOOR 3: Black Sanctum (PlatformerSet1 black_floor)
        // Stack Level: Center Y = 50.7. Spans X from -134.86 to +98.57 (Width ~233.4)
        // Wings: West Wing (Torture Crypt) + Center Dais + East Wing (Ceremonial Gate)
        // =========================================================================
        private static CastleRoom BuildFloor3_Stacked(Transform parent, GameObject templatePrompt)
        {
            Vector3 origin = new Vector3(0f, 50.7f, 0f);
            GameObject roomGo = new GameObject("Floor3_BlackSanctum");
            roomGo.transform.SetParent(parent, false);
            roomGo.transform.localPosition = origin;

            var triggerCol = roomGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.offset = new Vector2(0f, 2.0f);
            triggerCol.size = new Vector2(274.0f, 38.0f);

            GameObject confinerGo = new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(roomGo.transform, false);
            confinerGo.transform.localPosition = Vector3.zero;
            var confinerCol = confinerGo.AddComponent<BoxCollider2D>();
            confinerCol.isTrigger = true;
            confinerCol.offset = new Vector2(0f, 2.0f);
            confinerCol.size = new Vector2(272.0f, 36.0f);

            GameObject visualsGo = new GameObject("Visuals");
            visualsGo.transform.SetParent(roomGo.transform, false);
            visualsGo.transform.localPosition = Vector3.zero;

            // 1. West Wing: Demonic Torture Crypt (Width 73.14m, local X = -98.286, Y = +3.643)
            GameObject westWingGo = new GameObject("Wing_West_TortureCrypt");
            westWingGo.transform.SetParent(visualsGo.transform, false);
            westWingGo.transform.localPosition = new Vector3(-98.286f, 3.643f, 0f);
            var srWest = westWingGo.AddComponent<SpriteRenderer>();
            srWest.sprite = _sFloor3West;
            srWest.material = _spriteLitMat;
            srWest.sortingOrder = -14;

            // 2. Center Hall: Black Sanctum Demon Lord Dais (Width 123.43m, local X = 0, Y = 0)
            GameObject centerHallGo = new GameObject("Hall_Center_BlackSanctum");
            centerHallGo.transform.SetParent(visualsGo.transform, false);
            centerHallGo.transform.localPosition = Vector3.zero;
            var srCenter = centerHallGo.AddComponent<SpriteRenderer>();
            srCenter.sprite = _sFloor3;
            srCenter.material = _spriteLitMat;
            srCenter.sortingOrder = -14;

            // 3. East Wing: Grand Ceremonial Gate & Relic Chamber (Width 36.86m, local X = +80.143, Y = +0.036)
            GameObject eastWingGo = new GameObject("Wing_East_CeremonialGate");
            eastWingGo.transform.SetParent(visualsGo.transform, false);
            eastWingGo.transform.localPosition = new Vector3(80.143f, 0.036f, 0f);
            var srEast = eastWingGo.AddComponent<SpriteRenderer>();
            srEast.sprite = _sFloor3East;
            srEast.material = _spriteLitMat;
            srEast.sortingOrder = -14;

            GameObject collidersGo = new GameObject("Colliders");
            collidersGo.transform.SetParent(roomGo.transform, false);
            collidersGo.transform.localPosition = Vector3.zero;

            // West Wing Torture Crypt Colliders
            CreateBox(collidersGo.transform, new Vector2(-71.8f, -9.8f), new Vector2(20.5f, 1.5f), "Floor_CryptSpikeCorridor");
            CreateBox(collidersGo.transform, new Vector2(-92.5f, -0.86f), new Vector2(21.0f, 0.6f), "Platform_CryptMidScaffold");
            CreateBox(collidersGo.transform, new Vector2(-114.0f, -4.43f), new Vector2(10.0f, 0.6f), "Platform_CryptDropShaftUpper");
            CreateBox(collidersGo.transform, new Vector2(-114.0f, -8.0f), new Vector2(10.0f, 0.6f), "Platform_CryptDropShaftLower");
            CreateBox(collidersGo.transform, new Vector2(-118.9f, -15.3f), new Vector2(32.0f, 1.5f), "Floor_CryptTorturePit");
            CreateBox(collidersGo.transform, new Vector2(-120.9f, +9.5f), new Vector2(28.0f, 0.8f), "Platform_CryptUpperCells");

            // Multi-wing Platforms & Demon Lord Dais
            CreateBox(collidersGo.transform, new Vector2(0f, -9.8f), new Vector2(124.0f, 1.5f), "Floor_Center_Main");
            CreateBox(collidersGo.transform, new Vector2(-45.1f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_WestSanctum");
            CreateBox(collidersGo.transform, new Vector2(-20.3f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_Monolith_Left");
            CreateBox(collidersGo.transform, new Vector2(0f, -0.4f), new Vector2(29.0f, 1.2f), "Platform_DemonLordDais");
            CreateBox(collidersGo.transform, new Vector2(-15.8f, -1.8f), new Vector2(3.5f, 0.8f), "Platform_DaisLeftStep");
            CreateBox(collidersGo.transform, new Vector2(15.8f, -1.8f), new Vector2(3.5f, 0.8f), "Platform_DaisRightStep");
            CreateBox(collidersGo.transform, new Vector2(20.3f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_Monolith_Right");
            CreateBox(collidersGo.transform, new Vector2(43.4f, -0.7f), new Vector2(11.5f, 0.8f), "Platform_EastReliquary");

            // East Wing Ceremonial Gate Colliders
            CreateBox(collidersGo.transform, new Vector2(67.3f, -9.8f), new Vector2(11.5f, 1.5f), "Floor_GateBaseLeft");
            CreateBox(collidersGo.transform, new Vector2(80.0f, -7.5f), new Vector2(14.0f, 0.8f), "Platform_CeremonialPortalDais");
            CreateBox(collidersGo.transform, new Vector2(92.8f, -9.8f), new Vector2(11.5f, 1.5f), "Floor_GateBaseRight");

            // Ceiling & Outer Walls
            CreateBox(collidersGo.transform, new Vector2(-18.0f, 25.0f), new Vector2(236f, 1.5f), "Ceiling");
            CreateBox(collidersGo.transform, new Vector2(-134.86f, 5.0f), new Vector2(1.5f, 40f), "Wall_Left_Outer");
            CreateBox(collidersGo.transform, new Vector2(98.57f, 5.0f), new Vector2(1.5f, 40f), "Wall_Right_Outer");

            // Demonic Point Lights (Purple, Amber & Cyan Soul-Crystals)
            CreatePointLight(roomGo.transform, new Vector3(-125.0f, 11.0f, 0f), new Color(0.95f, 0.45f, 0.20f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-110.0f, 1.0f, 0f), new Color(0.95f, 0.45f, 0.20f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-95.0f, 2.5f, 0f), new Color(0.2f, 0.80f, 1.0f), 2.5f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-125.0f, -13.5f, 0f), new Color(0.15f, 0.75f, 1.0f), 2.8f, 8.5f);
            CreatePointLight(roomGo.transform, new Vector3(-95.0f, -13.5f, 0f), new Color(0.15f, 0.75f, 1.0f), 2.8f, 8.5f);
            CreatePointLight(roomGo.transform, new Vector3(-75.0f, -6.5f, 0f), new Color(0.95f, 0.45f, 0.20f), 2.2f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(-45.0f, 2.0f, 0f), new Color(0.85f, 0.50f, 1.0f), 2.4f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-20.0f, 2.0f, 0f), new Color(0.85f, 0.50f, 1.0f), 2.4f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(0f, 3.5f, 0f), new Color(1.0f, 0.40f, 0.20f), 2.8f, 9.0f);
            CreatePointLight(roomGo.transform, new Vector3(21.0f, 2.0f, 0f), new Color(0.85f, 0.50f, 1.0f), 2.4f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(45.0f, 2.0f, 0f), new Color(0.85f, 0.50f, 1.0f), 2.4f, 7.5f);
            CreatePointLight(roomGo.transform, new Vector3(-56.3f, -5.5f, 0f), new Color(0.95f, 0.65f, 0.30f), 2.0f, 6.0f);
            CreatePointLight(roomGo.transform, new Vector3(75.5f, -6.5f, 0f), new Color(0.2f, 0.80f, 1.0f), 2.6f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(84.5f, -6.5f, 0f), new Color(0.2f, 0.80f, 1.0f), 2.6f, 7.0f);
            CreatePointLight(roomGo.transform, new Vector3(80.0f, 2.0f, 0f), new Color(1.0f, 0.35f, 0.15f), 3.0f, 9.0f);
            CreatePointLight(roomGo.transform, new Vector3(80.0f, 11.0f, 0f), new Color(0.8f, 0.30f, 0.40f), 1.8f, 8.0f);

            // Left Spiral Stairs Down to Floor 2 (local X = -56.3, Y = -7.0)
            CreateSpiralDoorway(roomGo.transform, "Doorway_Stairs_F3_to_F2", new Vector3(-56.3f, -7.0f, 0f), null, "กด F เพื่อลงบันไดวนสู่ชั้น 2", templatePrompt);

            var roomComp = roomGo.AddComponent<CastleRoom>();
            SetField(roomComp, "_roomName", "Floor 3: Black Sanctum");
            SetField(roomComp, "_cameraConfiner", confinerCol);
            SetField(roomComp, "_roomContent", visualsGo);

            return roomComp;
        }

        // =========================================================================
        // INTER-FLOOR STRUCTURAL MASONRY DIVIDERS (Massive Fortress Load-Bearing Slabs)
        // Completely seals the cutaway gaps between floors with heavy ancient stone!
        // =========================================================================
        private static void BuildInterFloorSlabs(Transform parent)
        {
            // 1. Slab between Floor 1 and Floor 2 (Global Y = 12.8f, Height ~9m dual-tier)
            GameObject slab12Go = new GameObject("InterFloor_Slab_F1_F2");
            slab12Go.transform.SetParent(parent, false);
            slab12Go.transform.localPosition = new Vector3(0f, 12.8f, 0f);

            GameObject visuals12 = new GameObject("Visuals");
            visuals12.transform.SetParent(slab12Go.transform, false);
            visuals12.transform.localPosition = Vector3.zero;

            // Tile 9 modular segments across X in [-144, +144], reinforced dual-tier masonry
            float[] slabXPositions = new float[] { -128f, -96f, -64f, -32f, 0f, 32f, 64f, 96f, 128f };
            for (int i = 0; i < slabXPositions.Length; i++)
            {
                // Upper tier
                GameObject tileUpper = new GameObject($"Slab_F1_F2_Upper_{i}");
                tileUpper.transform.SetParent(visuals12.transform, false);
                tileUpper.transform.localPosition = new Vector3(slabXPositions[i], 1.0f, 0f);
                var srUpper = tileUpper.AddComponent<SpriteRenderer>();
                srUpper.sprite = _sInterFloor12;
                srUpper.material = _spriteLitMat;
                srUpper.sortingOrder = -20;

                // Lower foundation tier (thick ancient divider)
                GameObject tileLower = new GameObject($"Slab_F1_F2_Lower_{i}");
                tileLower.transform.SetParent(visuals12.transform, false);
                tileLower.transform.localPosition = new Vector3(slabXPositions[i], -1.0f, 0f);
                var srLower = tileLower.AddComponent<SpriteRenderer>();
                srLower.sprite = _sInterFloor12;
                srLower.material = _spriteLitMat;
                srLower.sortingOrder = -20;
            }

            // Heavy solid masonry boundary collider (8.5m thickness)
            CreateBox(slab12Go.transform, Vector2.zero, new Vector2(288f, 8.5f), "Collider_Solid_Slab_F1_F2");

            // 2. Slab between Floor 2 and Floor 3 (Global Y = 38.8f, Height ~9m dual-tier)
            GameObject slab23Go = new GameObject("InterFloor_Slab_F2_F3");
            slab23Go.transform.SetParent(parent, false);
            slab23Go.transform.localPosition = new Vector3(0f, 38.8f, 0f);

            GameObject visuals23 = new GameObject("Visuals");
            visuals23.transform.SetParent(slab23Go.transform, false);
            visuals23.transform.localPosition = Vector3.zero;

            for (int i = 0; i < slabXPositions.Length; i++)
            {
                // Upper tier
                GameObject tileUpper = new GameObject($"Slab_F2_F3_Upper_{i}");
                tileUpper.transform.SetParent(visuals23.transform, false);
                tileUpper.transform.localPosition = new Vector3(slabXPositions[i], 1.0f, 0f);
                var srUpper = tileUpper.AddComponent<SpriteRenderer>();
                srUpper.sprite = _sInterFloor23;
                srUpper.material = _spriteLitMat;
                srUpper.sortingOrder = -14;

                // Lower foundation tier (thick ancient divider)
                GameObject tileLower = new GameObject($"Slab_F2_F3_Lower_{i}");
                tileLower.transform.SetParent(visuals23.transform, false);
                tileLower.transform.localPosition = new Vector3(slabXPositions[i], -1.0f, 0f);
                var srLower = tileLower.AddComponent<SpriteRenderer>();
                srLower.sprite = _sInterFloor23;
                srLower.material = _spriteLitMat;
                srLower.sortingOrder = -14;
            }

            CreateBox(slab23Go.transform, Vector2.zero, new Vector2(288f, 8.5f), "Collider_Solid_Slab_F2_F3");
        }

        // =========================================================================
        // SPIRAL STAIRCASE CONNECTIONS BETWEEN VERTICALLY STACKED FLOORS
        // =========================================================================
        private static void ConnectStackedStaircases(CastleRoom f1, CastleRoom f2, CastleRoom f3)
        {
            // Floor 1 -> Floor 2 (East Tower)
            var doorF1toF2 = f1.transform.Find("Doorway_Stairs_F1_to_F2")?.GetComponent<RoomDoorway>();
            var spawnF2_East = new GameObject("SpawnPoint_F2_East");
            spawnF2_East.transform.SetParent(f2.transform, false);
            spawnF2_East.transform.localPosition = new Vector3(56.3f, -7.5f, 0f);

            if (doorF1toF2 != null)
            {
                doorF1toF2.TargetSpawnPoint = spawnF2_East.transform;
                doorF1toF2.TargetRoom = f2;
            }

            // Floor 2 -> Floor 1 (East Tower)
            var doorF2toF1 = f2.transform.Find("Doorway_Stairs_F2_to_F1")?.GetComponent<RoomDoorway>();
            var spawnF1_East = new GameObject("SpawnPoint_F1_East");
            spawnF1_East.transform.SetParent(f1.transform, false);
            spawnF1_East.transform.localPosition = new Vector3(52.0f, -7.5f, 0f);

            if (doorF2toF1 != null)
            {
                doorF2toF1.TargetSpawnPoint = spawnF1_East.transform;
                doorF2toF1.TargetRoom = f1;
            }

            // Floor 2 -> Floor 3 (West Tower)
            var doorF2toF3 = f2.transform.Find("Doorway_Stairs_F2_to_F3")?.GetComponent<RoomDoorway>();
            var spawnF3_West = new GameObject("SpawnPoint_F3_West");
            spawnF3_West.transform.SetParent(f3.transform, false);
            spawnF3_West.transform.localPosition = new Vector3(-56.3f, -7.5f, 0f);

            if (doorF2toF3 != null)
            {
                doorF2toF3.TargetSpawnPoint = spawnF3_West.transform;
                doorF2toF3.TargetRoom = f3;
            }

            // Floor 3 -> Floor 2 (West Tower)
            var doorF3toF2 = f3.transform.Find("Doorway_Stairs_F3_to_F2")?.GetComponent<RoomDoorway>();
            var spawnF2_West = new GameObject("SpawnPoint_F2_West");
            spawnF2_West.transform.SetParent(f2.transform, false);
            spawnF2_West.transform.localPosition = new Vector3(-56.3f, -7.5f, 0f);

            if (doorF3toF2 != null)
            {
                doorF3toF2.TargetSpawnPoint = spawnF2_West.transform;
                doorF3toF2.TargetRoom = f2;
            }
        }

        private static GameObject CreateSpiralDoorway(Transform parent, string name, Vector3 localPos, Sprite stairSprite, string promptText, GameObject templatePrompt)
        {
            GameObject doorGo = new GameObject(name);
            doorGo.transform.SetParent(parent, false);
            doorGo.transform.localPosition = localPos;

            var col = doorGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3.5f, 5.0f);

            var doorComp = doorGo.AddComponent<RoomDoorway>();
            SetField(doorComp, "_autoTransitionOnEnter", false);
            SetField(doorComp, "_interactKey", KeyCode.F);

            if (stairSprite != null)
            {
                var sr = doorGo.AddComponent<SpriteRenderer>();
                sr.sprite = stairSprite;
                sr.material = _spriteLitMat;
                sr.sortingOrder = -14;
            }

            if (templatePrompt != null)
            {
                GameObject promptClone = Object.Instantiate(templatePrompt, doorGo.transform);
                promptClone.name = "PopupCanvas";
                promptClone.transform.localPosition = new Vector3(0f, 3.2f, 0f);

                var textComp = promptClone.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (textComp != null)
                {
                    textComp.text = promptText;
                }

                SetField(doorComp, "_promptUI", promptClone);
                promptClone.SetActive(false);
            }

            return doorGo;
        }

        // =========================================================================
        // HELPERS
        // =========================================================================
        private static void CreateBox(Transform parent, Vector2 localCenter, Vector2 size, string name)
        {
            GameObject boxGo = new GameObject(name);
            boxGo.transform.SetParent(parent, false);
            boxGo.transform.localPosition = new Vector3(localCenter.x, localCenter.y, 0f);
            var col = boxGo.AddComponent<BoxCollider2D>();
            col.size = size;
        }

        private static void CreatePointLight(Transform parent, Vector3 localPos, Color color, float intensity, float radius)
        {
            GameObject lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = localPos;

            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = radius;
        }

        private static void SetupPlayerAndPortal(CastleRoom f1)
        {
            var portal = GameObject.Find("Portal_Left");
            if (portal != null)
            {
                Undo.RecordObject(portal.transform, "Position Portal_Left");
                portal.transform.position = new Vector3(-28.0f, -2.1f, 0f);
                var scenePortal = portal.GetComponent<ScenePortal>();
                if (scenePortal != null)
                {
                    scenePortal.targetSceneName = "SuburbToForest";
                }
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Undo.RecordObject(player.transform, "Position Player");
                player.transform.position = new Vector3(-25.0f, -2.1f, 0f);
            }
        }

        private static void SetupCamera(CastleRoom f1)
        {
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(-25.0f, 0.0f, -10f);
                var camFollow = cam.GetComponent<CameraFollow2D>();
                if (camFollow != null && f1 != null)
                {
                    Undo.RecordObject(camFollow, "Set Camera Boundaries");
                    camFollow.SetBoundaries(f1.CameraConfiner);
                }
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
