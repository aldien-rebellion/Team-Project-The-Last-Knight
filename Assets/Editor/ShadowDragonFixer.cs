using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TheLastKnight.Combat;
using TheLastKnight.AI;

namespace TheLastKnight.EditorTools
{
    [InitializeOnLoad]
    public static class ShadowDragonFixer
    {
        private const string DragonAnimAssetDir = "Assets/sprites/Monsters/Shadow_Demon_Dragon_Asset_Pack/Shadow_Demon_Dragon_Asset_Pack/Animations";
        private const string DragonClipsDir = "Assets/Animations/Enemies/ShadowDemonDragon";
        private const string PrefabsDir = "Assets/Prefabs/Enemies";
        private const string DragonAudioDir = "Assets/sprites/Monsters/Shadow_Demon_Dragon_Asset_Pack/Shadow_Demon_Dragon_Asset_Pack/Audio";

        static ShadowDragonFixer()
        {
            EditorApplication.delayCall += AutoRunOnce;
        }

        private static void AutoRunOnce()
        {
            if (!EditorPrefs.GetBool("ShadowDragonFixed_v4", false))
            {
                EditorPrefs.SetBool("ShadowDragonFixed_v4", true);
                FixAll();
            }
        }

        private struct SheetDef
        {
            public string Subfolder;
            public string FileName;
            public string ClipName;
            public int Rows;
            public float FPS;
            public bool Loop;

            public SheetDef(string subfolder, string fileName, string clipName, int rows, float fps, bool loop)
            {
                Subfolder = subfolder;
                FileName = fileName;
                ClipName = clipName;
                Rows = rows;
                FPS = fps;
                Loop = loop;
            }
        }

        [MenuItem("Tools/Fix Shadow Demon Dragon")]
        public static void FixAll()
        {
            SheetDef[] sheets = new SheetDef[]
            {
                new SheetDef("Idle_Left", "Idle_left.png", "ShadowDemonDragon_Idle_Left", 2, 10f, true),
                new SheetDef("Idle_Right", "Idle_Right.png", "ShadowDemonDragon_Idle_Right", 2, 10f, true),
                new SheetDef("Walk_Left", "Walk_left.png", "ShadowDemonDragon_Walk_Left", 2, 10f, true),
                new SheetDef("Walk_Right", "Walk_Right.png", "ShadowDemonDragon_Walk_Right", 2, 10f, true),
                new SheetDef("Attack_Left", "Attack_left.png", "ShadowDemonDragon_Attack_Left", 2, 12f, false),
                new SheetDef("Attack_Right", "Attack_Right.png", "ShadowDemonDragon_Attack_Right", 2, 12f, false),
                new SheetDef("Hit_Left", "Hit_left.png", "ShadowDemonDragon_Hit_Left", 6, 14f, false),
                new SheetDef("Hit_Right", "Hit_Right.png", "ShadowDemonDragon_Hit_Right", 6, 14f, false),
                new SheetDef("Death_Left", "Death_left.png", "ShadowDemonDragon_Death_Left", 4, 12f, false),
                new SheetDef("Death_Right", "Death_Right.png", "ShadowDemonDragon_Death_Right", 4, 12f, false),
            };

            foreach (var def in sheets)
            {
                SliceTexture(def);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var def in sheets)
            {
                RebuildClip(def);
            }

            RebuildDragonPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[ShadowDragonFixer] Fixed Shadow Demon Dragon 100% successfully (single dragon per cell 1408x792)!</color>");
        }

        private static void SliceTexture(SheetDef def)
        {
            string path = $"{DragonAnimAssetDir}/{def.Subfolder}/{def.FileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[ShadowDragonFixer] Could not find importer for: {path}");
                return;
            }

            importer.isReadable = true;
            importer.maxTextureSize = 8192;
            importer.filterMode = FilterMode.Point;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            const int cols = 5;
            int rows = def.Rows;
            const int cellW = 1408;
            const int cellH = 792;
            int totalH = rows * cellH;

            var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var newRects = new List<SpriteRect>();
            int frameIdx = 0;

            // Iterate rows from top to bottom (row 0 is top of image)
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var sr = new SpriteRect();
                    string baseName = Path.GetFileNameWithoutExtension(def.FileName);
                    sr.name = $"{baseName}_{frameIdx:D2}";

                    int rectX = c * cellW;
                    int rectY = totalH - (r + 1) * cellH;
                    sr.rect = new Rect(rectX, rectY, cellW, cellH);

                    sr.alignment = SpriteAlignment.Custom;
                    sr.pivot = new Vector2(0.5f, 0.31f);
                    sr.spriteID = GUID.Generate();

                    newRects.Add(sr);
                    frameIdx++;
                }
            }

            dataProvider.SetSpriteRects(newRects.ToArray());
            dataProvider.Apply();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[ShadowDragonFixer] Sliced {def.FileName} with {newRects.Count} frames of 1408x792");
        }

        private static void RebuildClip(SheetDef def)
        {
            string texturePath = $"{DragonAnimAssetDir}/{def.Subfolder}/{def.FileName}";
            string clipPath = $"{DragonClipsDir}/{def.ClipName}.anim";

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in allAssets)
            {
                if (asset is Sprite s)
                {
                    sprites.Add(s);
                }
            }

            // Sort by sprite name to ensure proper sequence 00, 01, 02...
            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            if (sprites.Count == 0)
            {
                Debug.LogError($"[ShadowDragonFixer] No sprites found for {texturePath}");
                return;
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = def.FPS;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = def.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding binding = new EditorCurveBinding();
            binding.type = typeof(SpriteRenderer);
            binding.path = "";
            binding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count];
            float frameDuration = 1f / def.FPS;

            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * frameDuration,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            EditorUtility.SetDirty(clip);
            Debug.Log($"[ShadowDragonFixer] Rebuilt clip {def.ClipName} with {sprites.Count} frames");
        }

        private static void RebuildDragonPrefab()
        {
            string prefabPath = $"{PrefabsDir}/ShadowDemonDragon.prefab";
            string controllerPath = $"{DragonClipsDir}/ShadowDemonDragonController.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            // Load first sprite from Idle_Left
            string idlePath = $"{DragonAnimAssetDir}/Idle_Left/Idle_left.png";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(idlePath);
            Sprite firstSprite = null;
            foreach (var a in assets)
            {
                if (a is Sprite s && s.name.EndsWith("_00"))
                {
                    firstSprite = s;
                    break;
                }
            }
            if (firstSprite == null && assets.Length > 1) firstSprite = assets[1] as Sprite;

            GameObject root = new GameObject("ShadowDemonDragon");

            // SpriteRenderer
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            if (firstSprite != null) sr.sprite = firstSprite;

            // Animator
            var anim = root.AddComponent<Animator>();
            if (controller != null) anim.runtimeAnimatorController = controller;

            // Rigidbody2D
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 2.5f;

            // CapsuleCollider2D (Single dragon size!)
            var col = root.AddComponent<CapsuleCollider2D>();
            // 1408x792 at PPU 100 with dragon width ~3.8 units, height ~3.0 units
            col.size = new Vector2(3.5f, 3.2f);
            col.offset = new Vector2(0f, 1.6f);

            // EnemyStats
            var stats = root.AddComponent<EnemyStats>();
            stats.SetStats(900f, 12f, 55f);

            // EnemyController
            var ai = root.AddComponent<EnemyController>();
            var serializedAI = new SerializedObject(ai);
            serializedAI.FindProperty("_patrolSpeed").floatValue = 1.8f;
            serializedAI.FindProperty("_chaseSpeed").floatValue = 3.8f;
            serializedAI.FindProperty("_detectionRange").floatValue = 10f;
            serializedAI.FindProperty("_meleeRange").floatValue = 3.2f;
            serializedAI.FindProperty("_isFlying").boolValue = false;
            serializedAI.FindProperty("_hasRangedAttack").boolValue = false;
            serializedAI.ApplyModifiedProperties();

            // Setup Floating Health Bar Canvas
            CreateHealthBarCanvas(root, stats, 3.2f);

            // Setup Hitbox for damage dealing
            CreateContactHitbox(root, 55f, 3.8f, 3.2f);

            // Audio setup
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 25f;

            var dragonAudio = root.AddComponent<DragonAudioController>();
            var idleClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_idle.mp3");
            var footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_foot tep.mp3");
            var attackClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_attack.mp3");
            var hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_hit.mp3");
            var deathClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_death.mp3");
            var deathFallClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_death_(Falling_to_the_ground).mp3");
            dragonAudio.SetClips(idleClip, footstepClip, attackClip, hitClip, deathClip, deathFallClip);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[ShadowDragonFixer] Saved single-dragon ShadowDemonDragon.prefab!");
        }

        private static void CreateHealthBarCanvas(GameObject parent, EnemyStats stats, float characterHeight)
        {
            GameObject canvasGo = new GameObject("HealthCanvas");
            canvasGo.transform.SetParent(parent.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, characterHeight + 0.35f, 0f);
            canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 14f);

            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillImg = fillGo.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            fillImg.type = UnityEngine.UI.Image.Type.Filled;
            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;

            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = new Vector2(-4f, -4f);

            var floatingBar = canvasGo.AddComponent<FloatingHealthBar>();
            floatingBar.Setup(stats, fillImg);
        }

        private static void CreateContactHitbox(GameObject parent, float attackPower, float width, float height)
        {
            GameObject hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(parent.transform, false);
            hitboxGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var col = hitboxGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(width, height);

            var hitbox = hitboxGo.AddComponent<EnemyHitbox2D>();
            hitbox.Damage = attackPower;
        }
    }
}
