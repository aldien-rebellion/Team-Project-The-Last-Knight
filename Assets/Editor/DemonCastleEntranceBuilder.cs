using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TheLastKnight.Camera;
using TheLastKnight.Environment;
using TheLastKnight.UI;

namespace TheLastKnight.Editor
{
    public static class DemonCastleEntranceBuilder
    {
        private const string ClosedSpritePath = "Assets/sprites/Environment/background_image/demon_castle_entrance_closed.png";
        private const string OpenedSpritePath = "Assets/sprites/Environment/background_image/demon_castle_entrance_opened.png";
        private const string RunesDir = "Assets/sprites/Environment/runes";
        private const string ScenePath = "Assets/Scenes/Maps/DemonCastleEntrance.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string PortalPrefabPath = "Assets/Prefabs/Portal.prefab";
        private const string HudUxmlPath = "Assets/UI/HUD/PlayerHUD.uxml";

        [MenuItem("Tools/Build Demon Castle Entrance Map")]
        public static void BuildEntranceMap()
        {
            // 1. Configure Texture Importers
            ConfigureTextures();

            // 2. Load or Create Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Load Sprites
            Sprite sClosed = AssetDatabase.LoadAssetAtPath<Sprite>(ClosedSpritePath);
            Sprite sOpened = AssetDatabase.LoadAssetAtPath<Sprite>(OpenedSpritePath);

            if (sClosed == null || sOpened == null)
            {
                Debug.LogError("[DemonCastleEntranceBuilder] Could not load closed or opened sprite! Make sure textures are imported as Sprite.");
                return;
            }

            // Load Rune Sprites
            Sprite[] unlitRunes = new Sprite[4]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_pentagram_unlit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_hand_unlit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_eye_unlit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_trident_unlit.png")
            };

            Sprite[] litRunes = new Sprite[4]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_pentagram_lit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_hand_lit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_eye_lit.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>($"{RunesDir}/rune_trident_lit.png")
            };

            // Material
            Material spriteLitMat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");

            // 4. Map Root
            GameObject mapRoot = new GameObject("DemonCastleEntrance_Map");

            // 1024x571 Image geometry: Walkway surface at Y=515 (from top). Center is Y=285.5.
            // Offset from center = -2.295 units. With scale 2.0: -4.59 units.
            // If walkway top is at Y = -2.5, background center Y = -2.5 - (-4.59) = +2.09.
            Vector3 bgScale = new Vector3(2f, 2f, 1f);
            Vector3 bgPos = new Vector3(0f, 2.09f, 0f);

            // Background Closed
            GameObject bgClosedGo = new GameObject("Background_Closed");
            bgClosedGo.transform.SetParent(mapRoot.transform, false);
            bgClosedGo.transform.position = bgPos;
            bgClosedGo.transform.localScale = bgScale;
            var srClosed = bgClosedGo.AddComponent<SpriteRenderer>();
            srClosed.sprite = sClosed;
            srClosed.sortingOrder = -10;
            if (spriteLitMat != null) srClosed.material = spriteLitMat;

            // Background Opened
            GameObject bgOpenedGo = new GameObject("Background_Opened");
            bgOpenedGo.transform.SetParent(mapRoot.transform, false);
            bgOpenedGo.transform.position = bgPos;
            bgOpenedGo.transform.localScale = bgScale;
            var srOpened = bgOpenedGo.AddComponent<SpriteRenderer>();
            srOpened.sprite = sOpened;
            srOpened.sortingOrder = -10;
            if (spriteLitMat != null) srOpened.material = spriteLitMat;
            bgOpenedGo.SetActive(false); // Closed initially

            // 5. Ground & Boundary Colliders
            GameObject groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(mapRoot.transform, false);
            // Walkway top at y = -2.5. Box center at y = -3.0, height = 1.0 (top is -2.5)
            groundGo.transform.position = new Vector3(0f, -3.0f, 0f);
            var groundCol = groundGo.AddComponent<BoxCollider2D>();
            groundCol.size = new Vector2(24f, 1f);

            // Left Wall
            GameObject wallLeft = new GameObject("Boundary_Left");
            wallLeft.transform.SetParent(mapRoot.transform, false);
            wallLeft.transform.position = new Vector3(-10.5f, 0f, 0f);
            var wallLeftCol = wallLeft.AddComponent<BoxCollider2D>();
            wallLeftCol.size = new Vector2(1f, 10f);

            // Right Wall
            GameObject wallRight = new GameObject("Boundary_Right");
            wallRight.transform.SetParent(mapRoot.transform, false);
            wallRight.transform.position = new Vector3(10.5f, 0f, 0f);
            var wallRightCol = wallRight.AddComponent<BoxCollider2D>();
            wallRightCol.size = new Vector2(1f, 10f);

            // Camera Confiner Boundary Box
            GameObject confinerGo = new GameObject("CameraConfiner");
            confinerGo.transform.SetParent(mapRoot.transform, false);
            confinerGo.transform.position = new Vector3(0f, 1.5f, 0f);
            var confinerBox = confinerGo.AddComponent<BoxCollider2D>();
            confinerBox.isTrigger = true;
            confinerBox.size = new Vector2(20.0f, 10.0f);

            // 6. Global Light 2D
            GameObject lightGlobal = new GameObject("Global Light 2D");
            lightGlobal.transform.SetParent(mapRoot.transform, false);
            var globalLightComp = lightGlobal.AddComponent<Light2D>();
            globalLightComp.lightType = Light2D.LightType.Global;
            globalLightComp.color = new Color(0.62f, 0.55f, 0.72f, 1f);
            globalLightComp.intensity = 0.85f;

            // Gate Red Accent Light
            GameObject gateLight = new GameObject("Gate_RedLight");
            gateLight.transform.SetParent(mapRoot.transform, false);
            gateLight.transform.position = new Vector3(0f, -0.5f, 0f);
            var pLight = gateLight.AddComponent<Light2D>();
            pLight.lightType = Light2D.LightType.Point;
            pLight.color = new Color(1f, 0.25f, 0.2f, 1f);
            pLight.intensity = 1.3f;
            pLight.pointLightInnerRadius = 0.8f;
            pLight.pointLightOuterRadius = 6.0f;

            // 7. Portal_Left (Back to SuburbToForest)
            GameObject portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPrefabPath);
            if (portalPrefab != null)
            {
                GameObject portalLeft = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab);
                portalLeft.name = "Portal_Left";
                portalLeft.transform.position = new Vector3(-8.5f, -1.3f, 0f);
                var sp = portalLeft.GetComponent<ScenePortal>();
                if (sp != null)
                {
                    sp.targetSceneName = "SuburbToForest";
                }
            }

            // 8. Demon Castle Gate (Center Gate to DemonCastle)
            GameObject gateObj = new GameObject("DemonCastleGate");
            gateObj.transform.position = new Vector3(0f, -1.2f, 0f);
            var gateCol = gateObj.AddComponent<BoxCollider2D>();
            gateCol.isTrigger = true;
            gateCol.size = new Vector2(4.5f, 3.5f);

            var gateComp = gateObj.AddComponent<DemonCastleGate>();
            gateComp.targetSceneName = "DemonCastle";
            gateComp.closedVisual = bgClosedGo;
            gateComp.openedVisual = bgOpenedGo;

            // 4 Rune Sockets on the Castle Gate Pillars
            Vector3[] socketPositions = new Vector3[4]
            {
                new Vector3(-2.12f,  0.35f, 0f), // Pentagram
                new Vector3(-2.12f, -0.68f, 0f), // Hand
                new Vector3( 2.16f,  0.35f, 0f), // Eye
                new Vector3( 2.16f, -0.68f, 0f)  // Trident
            };

            gateComp.socketObjects = new GameObject[4];
            gateComp.socketRenderers = new SpriteRenderer[4];
            gateComp.unlitRuneSprites = unlitRunes;
            gateComp.litRuneSprites = litRunes;

            for (int i = 0; i < 4; i++)
            {
                GameObject socketGo = new GameObject($"Gate_RuneSocket_{i + 1}");
                socketGo.transform.SetParent(gateObj.transform, false);
                socketGo.transform.position = socketPositions[i];
                socketGo.transform.localScale = new Vector3(2f, 2f, 1f);

                var sr = socketGo.AddComponent<SpriteRenderer>();
                sr.sprite = unlitRunes[i];
                sr.sortingOrder = -5;
                if (spriteLitMat != null) sr.material = spriteLitMat;

                gateComp.socketObjects[i] = socketGo;
                gateComp.socketRenderers[i] = sr;
            }

            // Build Gate Prompt UI
            GameObject promptCanvas = BuildGatePromptCanvas(gateObj.transform, gateComp);
            gateComp.promptCanvas = promptCanvas;

            // 9. DemonRuneManager
            GameObject runeMgrGo = new GameObject("DemonRuneManager");
            runeMgrGo.AddComponent<DemonRuneManager>();

            // 10. Test Rune Pickups in the Scene
            CreateTestRunePickups(mapRoot.transform, unlitRunes);

            // 11. Player Prefab
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject playerObj = null;
            if (playerPrefab != null)
            {
                playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerObj.name = "Player";
                playerObj.transform.position = new Vector3(-6.5f, -1.8f, 0f);
                var playerSr = playerObj.GetComponent<SpriteRenderer>();
                if (playerSr != null)
                {
                    var idleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/Player/player-idle1.png");
                    if (idleSpr != null) playerSr.sprite = idleSpr;
                }
            }

            // 12. Main Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-6.5f, 0f, -10f);
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var follow = camGo.AddComponent<CameraFollow2D>();
            if (playerObj != null) follow.SetTarget(playerObj.transform);
            follow.SetBoundaries(confinerBox);
            if (playerObj != null) follow.SnapTo(playerObj.transform.position);

            // 13. HUD
            GameObject hudGo = new GameObject("HUD");
            var uiDoc = hudGo.AddComponent<UIDocument>();
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            if (uxmlAsset != null) uiDoc.visualTreeAsset = uxmlAsset;
            hudGo.AddComponent<HUDController>();

            // 14. EventSystem
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            // 15. Save Scene
            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"<color=green>[DemonCastleEntranceBuilder]</color> บันทึกฉากสำเร็จ: {ScenePath}");

            // 16. Update Build Settings & Map Connections
            AddSceneToBuildSettings(ScenePath);
            UpdateConnectedMapPortals();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureTextures()
        {
            // Configure Backgrounds
            string[] bgPaths = new string[] { ClosedSpritePath, OpenedSpritePath };
            foreach (var p in bgPaths)
            {
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.spritePixelsPerUnit != 100f) { importer.spritePixelsPerUnit = 100f; changed = true; }
                    if (changed) importer.SaveAndReimport();
                }
            }

            // Configure Runes
            if (Directory.Exists(RunesDir))
            {
                string[] runeFiles = Directory.GetFiles(RunesDir, "*.png");
                foreach (var rf in runeFiles)
                {
                    string unityPath = rf.Replace('\\', '/');
                    var importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
                    if (importer != null)
                    {
                        bool changed = false;
                        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                        if (importer.spritePixelsPerUnit != 100f) { importer.spritePixelsPerUnit = 100f; changed = true; }
                        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                        if (changed) importer.SaveAndReimport();
                    }
                }
            }
        }

        private static GameObject BuildGatePromptCanvas(Transform parent, DemonCastleGate gate)
        {
            GameObject canvasGo = new GameObject("PromptCanvas");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            canvasGo.transform.localScale = new Vector3(0.012f, 0.012f, 1f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(480f, 160f);

            // Background panel
            GameObject bgPanel = new GameObject("Panel_BG");
            bgPanel.transform.SetParent(canvasGo.transform, false);
            var panelImg = bgPanel.AddComponent<UnityEngine.UI.Image>();
            panelImg.color = new Color(0.05f, 0.03f, 0.08f, 0.88f);
            var panelRt = bgPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Title Text
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = defaultFont;
            titleTxt.fontSize = 24;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1f, 0.35f, 0.35f, 1f);
            titleTxt.text = "ผนึกโบราณปราสาทปีศาจ";
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.65f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // Rune Status Text (Slots)
            GameObject slotsGo = new GameObject("RuneSlotsText");
            slotsGo.transform.SetParent(canvasGo.transform, false);
            var slotsTxt = slotsGo.AddComponent<Text>();
            slotsTxt.font = defaultFont;
            slotsTxt.fontSize = 20;
            slotsTxt.alignment = TextAnchor.MiddleCenter;
            slotsTxt.color = Color.white;
            slotsTxt.supportRichText = true;
            slotsTxt.text = "ช่องใส่รูน: [○] [○] [○] [○] (0/4)";
            var slotsRt = slotsGo.GetComponent<RectTransform>();
            slotsRt.anchorMin = new Vector2(0f, 0.35f);
            slotsRt.anchorMax = new Vector2(1f, 0.65f);
            slotsRt.offsetMin = Vector2.zero;
            slotsRt.offsetMax = Vector2.zero;

            // Action Text
            GameObject actionGo = new GameObject("ActionText");
            actionGo.transform.SetParent(canvasGo.transform, false);
            var actionTxt = actionGo.AddComponent<Text>();
            actionTxt.font = defaultFont;
            actionTxt.fontSize = 20;
            actionTxt.alignment = TextAnchor.MiddleCenter;
            actionTxt.color = new Color(1f, 0.9f, 0.3f, 1f);
            actionTxt.supportRichText = true;
            actionTxt.text = "ต้องการรูน 4 ชิ้นเพื่อเปิดประตู";
            var actionRt = actionGo.GetComponent<RectTransform>();
            actionRt.anchorMin = new Vector2(0f, 0f);
            actionRt.anchorMax = new Vector2(1f, 0.35f);
            actionRt.offsetMin = Vector2.zero;
            actionRt.offsetMax = Vector2.zero;

            gate.promptTitleText = titleTxt;
            gate.runeSlotsText = slotsTxt;
            gate.promptActionText = actionTxt;

            canvasGo.SetActive(false);
            return canvasGo;
        }

        private static void CreateTestRunePickups(Transform parent, Sprite[] runeSprites)
        {
            GameObject pickupsGroup = new GameObject("RunePickups_Testing");
            pickupsGroup.transform.SetParent(parent, false);

            float[] xPositions = new float[] { -4.5f, -2.8f, 2.8f, 4.5f };
            string[] names = new string[] {
                "Rune of the Pentagram (รูนเพนทาแกรม)",
                "Rune of the Demon Hand (รูนหัตถ์ปีศาจ)",
                "Rune of the Evil Eye (รูนเนตรอสูร)",
                "Rune of the Trident (รูนตรีศูลทมิฬ)"
            };
            Color[] lightColors = new Color[] {
                new Color(1f, 0.3f, 0.2f, 1f),
                new Color(0.7f, 0.3f, 1f, 1f),
                new Color(1f, 0.2f, 0.4f, 1f),
                new Color(0.3f, 0.7f, 1f, 1f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject runeGo = new GameObject($"RunePickup_{i + 1}");
                runeGo.transform.SetParent(pickupsGroup.transform, false);
                runeGo.transform.position = new Vector3(xPositions[i], -1.8f, 0f);
                int itemDropLayer = LayerMask.NameToLayer("ItemDrop");
                if (itemDropLayer != -1) runeGo.layer = itemDropLayer;

                var col = runeGo.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.5f;

                var pickup = runeGo.AddComponent<RunePickup>();
                pickup.runeId = i;
                pickup.runeDisplayName = names[i];

                // Point light visual
                GameObject lightGo = new GameObject("Light");
                lightGo.transform.SetParent(runeGo.transform, false);
                var l2d = lightGo.AddComponent<Light2D>();
                l2d.lightType = Light2D.LightType.Point;
                l2d.color = lightColors[i];
                l2d.intensity = 1.0f;
                l2d.pointLightInnerRadius = 0.2f;
                l2d.pointLightOuterRadius = 1.5f;

                // Rune Medallion Sprite
                var sr = runeGo.AddComponent<SpriteRenderer>();
                if (runeSprites != null && i < runeSprites.Length && runeSprites[i] != null)
                {
                    sr.sprite = runeSprites[i];
                }
                sr.sortingLayerName = "ItemDrops";
                sr.sortingOrder = 0;
                runeGo.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            }
        }

        public static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Already added
                }
            }

            var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
            Array.Copy(scenes, newScenes, scenes.Length);
            newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"<color=green>[DemonCastleEntranceBuilder]</color> เพิ่ม {scenePath} เข้าสู่ Build Settings เรียบร้อยแล้ว");
        }

        public static void UpdateConnectedMapPortals()
        {
            // 1. Update SuburbToForest Portal_Right
            string suburbPath = "Assets/Scenes/Maps/SuburbToForest.unity";
            if (File.Exists(suburbPath))
            {
                var scene = EditorSceneManager.OpenScene(suburbPath, OpenSceneMode.Additive);
                var portalRight = GameObject.Find("Portal_Right");
                if (portalRight != null)
                {
                    var sp = portalRight.GetComponent<ScenePortal>();
                    if (sp != null)
                    {
                        sp.targetSceneName = "DemonCastleEntrance";
                        EditorUtility.SetDirty(sp);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log("<color=green>[DemonCastleEntranceBuilder]</color> อัปเดต SuburbToForest -> Portal_Right ชี้ไปที่ 'DemonCastleEntrance'");
                    }
                }
                EditorSceneManager.CloseScene(scene, true);
            }

            // 2. Update DemonCastle Portal_Left
            string castlePath = "Assets/Scenes/Maps/DemonCastle.unity";
            if (File.Exists(castlePath))
            {
                var scene = EditorSceneManager.OpenScene(castlePath, OpenSceneMode.Additive);
                var portalLeft = GameObject.Find("Portal_Left");
                if (portalLeft != null)
                {
                    var sp = portalLeft.GetComponent<ScenePortal>();
                    if (sp != null)
                    {
                        sp.targetSceneName = "DemonCastleEntrance";
                        EditorUtility.SetDirty(sp);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log("<color=green>[DemonCastleEntranceBuilder]</color> อัปเดต DemonCastle -> Portal_Left ชี้ไปที่ 'DemonCastleEntrance'");
                    }
                }
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
