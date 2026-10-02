using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UI;
using TheLastKnight.Combat;
using TheLastKnight.Combat.Projectiles;
using TheLastKnight.AI;

namespace TheLastKnight.EditorTools
{
    public static class EnemyPrefabBuilder
    {
        private const string EnemiesOutputDir = "Assets/Prefabs/Enemies";
        private const string ProjectilesOutputDir = "Assets/Prefabs/Enemies/Projectiles";
        private const string AnimationsEnemiesDir = "Assets/Animations/Enemies";
        private const string AnimationsProjectilesDir = "Assets/Animations/Enemies/Projectiles";
        private const string DragonAudioDir = "Assets/sprites/Monsters/Shadow_Demon_Dragon_Asset_Pack/Shadow_Demon_Dragon_Asset_Pack/Audio";

        [MenuItem("Tools/Build Monster & Projectile Prefabs")]
        public static void BuildAll()
        {
            EnsureDirectories();
            BuildProjectiles();
            BuildMonsters();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[EnemyPrefabBuilder] Successfully built all Monster and Projectile prefabs!</color>");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(EnemiesOutputDir)) Directory.CreateDirectory(EnemiesOutputDir);
            if (!Directory.Exists(ProjectilesOutputDir)) Directory.CreateDirectory(ProjectilesOutputDir);
            AssetDatabase.Refresh();
        }

        private static void BuildProjectiles()
        {
            Debug.Log("[EnemyPrefabBuilder] Building Projectile Prefabs...");

            // 1. Dragon_FireBall
            BuildDirectProjectile("Dragon_FireBall", 9f, 20f, 0.4f, true);

            // 2. FantasyMushroom_Projectile
            BuildDirectProjectile("FantasyMushroom_Projectile", 7f, 10f, 0.25f, true);

            // 3. FireWorm_FireBall
            BuildDirectProjectile("FireWorm_FireBall", 8f, 12f, 0.3f, true);

            // 4. FlyingEye_Projectile
            BuildDirectProjectile("FlyingEye_Projectile", 9f, 10f, 0.25f, true);

            // 5. Goblin_Bomb
            BuildDirectProjectile("Goblin_Bomb", 7f, 15f, 0.35f, true);

            // 6. Golem_ArmProjectile
            BuildDirectProjectile("Golem_ArmProjectile", 11f, 25f, 0.4f, false);

            // 7. Golem_Laser
            BuildDirectProjectile("Golem_Laser", 16f, 35f, 0.5f, false);

            // 8. Jinn_Magic (Ground Spell)
            BuildGroundSpell("Jinn_Magic", 25f, 0.35f, 0.4f, 1.2f);

            // 9. Skeleton_Sword
            BuildDirectProjectile("Skeleton_Sword", 8.5f, 12f, 0.35f, false);

            // 10. SmallDragon_FireBall
            BuildDirectProjectile("SmallDragon_FireBall", 8f, 10f, 0.3f, true);

            // 11. BringerOfDeath_Spell (Ground Spell)
            BuildBringerOfDeathSpell();
        }

        [MenuItem("Tools/Rebuild FlyingEye Projectile Prefab")]
        public static void RebuildFlyingEyeProjectilePrefab()
        {
            BuildDirectProjectile("FlyingEye_Projectile", 9f, 10f, 0.25f, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildDirectProjectile(string name, float speed, float damage, float colliderRadius, bool isCircle)
        {
            string prefabPath = $"{ProjectilesOutputDir}/{name}.prefab";
            string controllerPath = $"{AnimationsProjectilesDir}/{name}/{name}Controller.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            AnimationClip fallbackClip = null;
            if (name == "FlyingEye_Projectile" && controller == null)
            {
                string clipPath = $"{AnimationsProjectilesDir}/{name}/{name}_Projectile.anim";
                fallbackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (fallbackClip != null)
                {
                    var generatedController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    controller = generatedController;
                    var state = generatedController.layers[0].stateMachine.AddState("Projectile");
                    state.motion = fallbackClip;
                    generatedController.layers[0].stateMachine.defaultState = state;
                }
            }

            GameObject go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = name == "FlyingEye_Projectile" ? 9 : 5;

            var anim = go.AddComponent<Animator>();
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
                AssignFirstSpriteFromController(sr, controller);
            }
            else if (name == "Skeleton_Sword")
            {
                const string swordSpriteSheet = "Assets/sprites/Monsters/Monsters_Creatures_Fantasy/Monster_Creatures_Fantasy(Projectile_Attack)/Skeleton/Sword_sprite.png";
                Sprite swordSprite = null;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(swordSpriteSheet))
                {
                    if (asset is Sprite sprite && sprite.name == "Sword_sprite_0000")
                    {
                        swordSprite = sprite;
                        break;
                    }
                }
                sr.sprite = swordSprite;
            }
            else if (name == "FlyingEye_Projectile")
            {
                AssignFirstSpriteFromClip(sr, fallbackClip);
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            if (isCircle)
            {
                var col = go.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = colliderRadius;
            }
            else
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(colliderRadius * 2f, colliderRadius * 1.5f);
            }

            var proj = go.AddComponent<EnemyProjectile>();
            var serializedProj = new SerializedObject(proj);
            serializedProj.FindProperty("_speed").floatValue = speed;
            serializedProj.FindProperty("_damage").floatValue = damage;
            if (name == "Goblin_Bomb")
            {
                serializedProj.FindProperty("_impactAudioClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/ระเบิด.wav");
                serializedProj.FindProperty("_impactAudioVolume").floatValue = 1f;
            }
            else if (name == "FantasyMushroom_Projectile" || name == "FireWorm_FireBall" || name == "FlyingEye_Projectile")
            {
                serializedProj.FindProperty("_impactAudioClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงก้อนแตก.WAV");
                serializedProj.FindProperty("_impactAudioVolume").floatValue = 1f;
            }
            serializedProj.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[EnemyPrefabBuilder] Created projectile prefab: {name}");
        }

        private static void BuildGroundSpell(string name, float damage, float delay, float activeDuration, float lifetime)
        {
            string prefabPath = $"{ProjectilesOutputDir}/{name}.prefab";
            string controllerPath = $"{AnimationsProjectilesDir}/{name}/{name}Controller.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            GameObject go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var anim = go.AddComponent<Animator>();
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
                AssignFirstSpriteFromController(sr, controller);
            }

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 2.5f);
            col.offset = new Vector2(0f, 1.25f);

            var spell = go.AddComponent<GroundSpellArea>();
            var serializedSpell = new SerializedObject(spell);
            serializedSpell.FindProperty("_damage").floatValue = damage;
            serializedSpell.FindProperty("_delayBeforeDamage").floatValue = delay;
            serializedSpell.FindProperty("_damageDuration").floatValue = activeDuration;
            serializedSpell.FindProperty("_totalLifetime").floatValue = lifetime;
            if (name == "Jinn_Magic")
            {
                serializedSpell.FindProperty("_impactAudioClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงลมโดน.WAV");
                serializedSpell.FindProperty("_impactAudioVolume").floatValue = 1f;
            }
            serializedSpell.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[EnemyPrefabBuilder] Created ground spell prefab: {name}");
        }
private static void BuildBringerOfDeathSpell()
        {
            string name = "BringerOfDeath_Spell";
            string prefabPath = $"{ProjectilesOutputDir}/{name}.prefab";
            string spellAnimPath = $"{AnimationsEnemiesDir}/BringerOfDeath/BringerOfDeath_Spell.anim";
            var spellClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spellAnimPath);

            // Create controller if not exists
            string spellControllerDir = $"{AnimationsProjectilesDir}/{name}";
            if (!Directory.Exists(spellControllerDir)) Directory.CreateDirectory(spellControllerDir);
            string spellControllerPath = $"{spellControllerDir}/{name}Controller.controller";

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(spellControllerPath);
            if (controller == null && spellClip != null)
            {
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(spellControllerPath);
                var state = controller.layers[0].stateMachine.AddState("Spell");
                state.motion = spellClip;
            }

            GameObject go = new GameObject(name);
            go.transform.localScale = new Vector3(8.75f, 8.75f, 1f);
            go.layer = LayerMask.NameToLayer("Player");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Player";
            sr.sortingOrder = 0;

            var anim = go.AddComponent<Animator>();
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
                AssignFirstSpriteFromClip(sr, spellClip);
            }

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.36f, 0.64f);
            col.offset = new Vector2(0f, 0.32f);

            var spell = go.AddComponent<GroundSpellArea>();
            var serializedSpell = new SerializedObject(spell);
            serializedSpell.FindProperty("_damage").floatValue = 30f;
            serializedSpell.FindProperty("_delayBeforeDamage").floatValue = 0.2f;
            serializedSpell.FindProperty("_damageDuration").floatValue = 0.5f;
            serializedSpell.FindProperty("_totalLifetime").floatValue = 1.4f;
            serializedSpell.FindProperty("_alignSpriteBottomToSpawn").boolValue = true;
            serializedSpell.FindProperty("_secondDamageDelay").floatValue = 1.05f;
            serializedSpell.FindProperty("_secondDamageDuration").floatValue = 0.18f;
            serializedSpell.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log($"[EnemyPrefabBuilder] Created BringerOfDeath_Spell prefab!");
        }

        private struct MonsterConfig
        {
            public string Name;
            public float HP;
            public float Attack;
            public float Defense;
            public float PatrolSpeed;
            public float ChaseSpeed;
            public float DetectionRange;
            public float MeleeRange;
            public bool IsFlying;
            public bool HasRanged;
            public string ProjectileName;
            public string GroundSpellName;
            public float Scale;

            public MonsterConfig(string name, float hp, float attack, float defense, float patrolSpd, float chaseSpd,
                float detectRange, float meleeRange, bool flying = false, bool ranged = false,
                string projName = null, string spellName = null, float scale = 1f)
            {
                Name = name;
                HP = hp;
                Attack = attack;
                Defense = defense;
                PatrolSpeed = patrolSpd;
                ChaseSpeed = chaseSpd;
                DetectionRange = detectRange;
                MeleeRange = meleeRange;
                IsFlying = flying;
                HasRanged = ranged;
                ProjectileName = projName;
                GroundSpellName = spellName;
                Scale = scale;
            }
        }

        private static void BuildMonsters()
        {
            Debug.Log("[EnemyPrefabBuilder] Building 31 Monster Prefabs...");

            MonsterConfig[] configs = new MonsterConfig[]
            {
                // Bosses & Elites
                new MonsterConfig("ArchDemon", 500f, 35f, 5f, 1.8f, 3.8f, 8f, 2.2f, scale: 5f),
                new MonsterConfig("BlueSlime", 35f, 10f, 0f, 1.8f, 3.2f, 6f, 0.05f, scale: 4f),
                new MonsterConfig("BringerOfDeath", 600f, 40f, 8f, 1.6f, 3.5f, 9f, 0.05f, false, false, null, "BringerOfDeath_Spell"),
                new MonsterConfig("Demon", 70f, 15f, 2f, 2.0f, 4.0f, 7f, 1.6f),
                new MonsterConfig("DemonBoss", 800f, 50f, 10f, 1.5f, 3.6f, 10f, 3.2f),
                new MonsterConfig("DemonKin", 60f, 12f, 1f, 2.2f, 4.2f, 7f, 1.5f),
                new MonsterConfig("Dragon", 450f, 35f, 6f, 2.0f, 4.0f, 9f, 2.5f, false, true, "Dragon_FireBall"),
                new MonsterConfig("Fairy", 25f, 8f, 0f, 2.5f, 4.5f, 7f, 1.0f, true),
                new MonsterConfig("FantasyMushroom", 55f, 12f, 1f, 1.8f, 3.5f, 7f, 1.3f, false, true, "FantasyMushroom_Projectile"),
                new MonsterConfig("FireWorm", 50f, 14f, 1f, 1.6f, 3.2f, 7f, 1.4f, false, true, "FireWorm_FireBall"),
                // FlyingEye closes in for its basic Attack; the projectile is reserved for EyeBeam.
                new MonsterConfig("FlyingEye", 35f, 10f, 0f, 2.2f, 4.2f, 7f, 1.2f, true, false),
                new MonsterConfig("ForestMushroom", 50f, 12f, 1f, 1.8f, 3.5f, 6f, 0.05f),
                new MonsterConfig("Fox", 40f, 12f, 0f, 3.0f, 5.5f, 7f, 1.2f),
                new MonsterConfig("Goblin", 45f, 12f, 0f, 2.2f, 4.2f, 7f, 1.3f, false, true, "Goblin_Bomb"),
                new MonsterConfig("Jinn", 300f, 30f, 4f, 2.0f, 4.0f, 8f, 2.0f, false, true, null, "Jinn_Magic"),
                new MonsterConfig("Lizard", 60f, 15f, 2f, 2.0f, 3.8f, 7f, 1.5f),
                new MonsterConfig("MechaStoneGolem", 750f, 45f, 12f, 1.5f, 3.2f, 9f, 2.8f, false, true, "Golem_ArmProjectile"),
                new MonsterConfig("Minotaur_1", 120f, 22f, 3f, 2.0f, 4.2f, 7f, 1.8f),
                new MonsterConfig("Minotaur_2", 140f, 25f, 4f, 1.9f, 4.0f, 7f, 1.8f),
                new MonsterConfig("Minotaur_3", 160f, 28f, 5f, 1.8f, 3.8f, 7f, 1.8f),
                new MonsterConfig("MoonstoneKeeper", 350f, 32f, 6f, 2.2f, 4.5f, 8f, 2.2f),
                new MonsterConfig("Necromancer", 45f, 12f, 1f, 1.8f, 3.8f, 8f, 0.05f),
                new MonsterConfig("Reaper", 280f, 30f, 5f, 2.2f, 4.5f, 8f, 0.05f, scale: 7f),
                new MonsterConfig("Satyr", 110f, 20f, 3f, 2.2f, 4.2f, 7f, 1.6f),
                new MonsterConfig("ShadowDemonDragon", 900f, 55f, 12f, 1.8f, 3.8f, 10f, 0.05f),
                new MonsterConfig("Skeleton", 45f, 12f, 1f, 1.8f, 3.5f, 7f, 0.05f, scale: 5f),
                new MonsterConfig("SkeletonKnight", 220f, 28f, 5f, 2.0f, 4.2f, 7f, 1.8f),
                new MonsterConfig("Skullwolf", 65f, 18f, 1f, 3.2f, 5.8f, 8f, 1.4f),
                new MonsterConfig("Small_dragon", 80f, 16f, 2f, 2.2f, 4.2f, 7f, 0.05f, false, false, "SmallDragon_FireBall", scale: 5f),
                new MonsterConfig("Trader_1", 100f, 0f, 0f, 0f, 0f, 0f, 0f), // NPC
                new MonsterConfig("UndeadExecutioner", 550f, 40f, 7f, 1.6f, 3.6f, 8f, 0.05f, scale: 5f)
            };

            foreach (var cfg in configs)
            {
                BuildSingleMonster(cfg);
            }
        }

        private static void BuildSingleMonster(MonsterConfig cfg)
        {
            string prefabPath = $"{EnemiesOutputDir}/{cfg.Name}.prefab";
            string controllerPath = $"{AnimationsEnemiesDir}/{cfg.Name}/{cfg.Name}Controller.controller";
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            GameObject root = new GameObject(cfg.Name);
            root.transform.localScale = new Vector3(cfg.Scale, cfg.Scale, cfg.Scale);

            // SpriteRenderer
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sortingOrder = cfg.Name == "FlyingEye" ? 8 : cfg.Name == "BringerOfDeath" ? 0 : 2;
            if (cfg.Name == "BringerOfDeath")
            {
                root.layer = LayerMask.NameToLayer("Player");
                sr.sortingLayerName = "Player";
            }
            if (controller != null)
            {
                AssignFirstSpriteFromController(sr, controller);
            }

            // Animator
            var anim = root.AddComponent<Animator>();
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
            }

            if (cfg.Name == "Small_dragon")
            {
                SetupSmallDragonFireAttack(root);
            }

            // Rigidbody2D
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (cfg.Name == "SkeletonKnight") rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = cfg.IsFlying ? 0f : 2.5f;

            // Collider2D (CapsuleCollider2D)
            var col = root.AddComponent<CapsuleCollider2D>();
            Bounds bounds = sr.bounds;
            if (cfg.Name == "ArchDemon" && !Mathf.Approximately(cfg.Scale, 0f))
                bounds.size /= Mathf.Abs(cfg.Scale);
            float colHeight = Mathf.Max(0.6f, bounds.size.y * 0.8f);
            float colWidth = Mathf.Max(0.4f, bounds.size.x * 0.45f);
            col.size = new Vector2(colWidth, colHeight);
            col.offset = new Vector2(0f, colHeight * 0.5f);
            if (cfg.Name == "Skeleton")
            {
                col.size = new Vector2(0.23f, 0.51f);
                col.offset = new Vector2(0.05f, 0.255f);
            }

            // EnemyStats
            var stats = root.AddComponent<EnemyStats>();
            stats.SetStats(cfg.HP, cfg.Defense, cfg.Attack);

            // EnemyController
            var ai = root.AddComponent<EnemyController>();
            GameObject projPrefab = !string.IsNullOrEmpty(cfg.ProjectileName) 
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectilesOutputDir}/{cfg.ProjectileName}.prefab") 
                : null;
            GameObject spellPrefab = !string.IsNullOrEmpty(cfg.GroundSpellName) 
                ? AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectilesOutputDir}/{cfg.GroundSpellName}.prefab") 
                : null;

            var serializedAI = new SerializedObject(ai);
            serializedAI.FindProperty("_patrolSpeed").floatValue = cfg.PatrolSpeed;
            serializedAI.FindProperty("_chaseSpeed").floatValue = cfg.ChaseSpeed;
            serializedAI.FindProperty("_detectionRange").floatValue = cfg.DetectionRange;
            serializedAI.FindProperty("_bringToFrontWhileAttacking").boolValue = cfg.Name == "BringerOfDeath" || cfg.Name == "Demon" || cfg.Name == "ArchDemon";
            serializedAI.FindProperty("_attackSortingOrder").intValue = 1;
            serializedAI.FindProperty("_attackSortingLayerName").stringValue = cfg.Name == "Demon" || cfg.Name == "ArchDemon" ? "Player" : string.Empty;
serializedAI.FindProperty("_initialFacingRight").boolValue = cfg.Name != "BringerOfDeath";
            serializedAI.FindProperty("_meleeRange").floatValue = cfg.Name == "ArchDemon" || cfg.Name == "FlyingEye" || cfg.Name == "Reaper" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer" || cfg.Name == "Demon" ? 0.05f : cfg.MeleeRange;
            serializedAI.FindProperty("_useColliderEdgeAttackRanges").boolValue = cfg.Name == "FlyingEye" || cfg.Name == "SkeletonKnight" || cfg.Name == "Demon";
            serializedAI.FindProperty("_meleeCooldown").floatValue = cfg.Name == "Small_dragon" ? 5f : cfg.Name == "ArchDemon" || cfg.Name == "BlueSlime" || cfg.Name == "Skeleton" || cfg.Name == "FlyingEye" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon" ? 0f : cfg.Name == "UndeadExecutioner" ? 1f : 1.5f;
            serializedAI.FindProperty("_basicAttackDamageDelay").floatValue = cfg.Name == "ArchDemon" ? 7f / 12f : cfg.Name == "BringerOfDeath" ? 5f / 12f : 0f;
            serializedAI.FindProperty("_canRespawn").boolValue = cfg.Name == "ArchDemon";
            serializedAI.FindProperty("_maxRespawns").intValue = cfg.Name == "ArchDemon" ? 1 : -1;
            serializedAI.FindProperty("_respawnsUsed").intValue = 0;
            serializedAI.FindProperty("_respawnTime").floatValue = cfg.Name == "ArchDemon" ? 0.5f : 30f;
            serializedAI.FindProperty("_requirePlayerAwayToRespawn").boolValue = cfg.Name != "ArchDemon";
            serializedAI.FindProperty("_respawnAtDeathPosition").boolValue = cfg.Name == "ArchDemon";
            serializedAI.FindProperty("_rangedCooldown").floatValue = cfg.Name == "FlyingEye" ? 0f : 3f;
            serializedAI.FindProperty("_basicParryEveryNAttacks").intValue = cfg.Name == "ArchDemon" || cfg.Name == "FlyingEye" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon" ? 4 : cfg.Name == "ShadowDemonDragon" ? 2 : cfg.Name == "Reaper" ? 3 : 0;
            serializedAI.FindProperty("_useColliderEdgeAttackDistance").boolValue = cfg.Name == "ArchDemon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "Small_dragon" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath";
            serializedAI.FindProperty("_useColliderEdgeAttackRanges").boolValue = cfg.Name == "ArchDemon" || cfg.Name == "FlyingEye" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon";
            serializedAI.FindProperty("_requireCloseRangeForContactSkills").boolValue = cfg.Name == "UndeadExecutioner" || cfg.Name == "Small_dragon" || cfg.Name == "ForestMushroom" || cfg.Name == "SkeletonKnight" || cfg.Name == "Necromancer";
            serializedAI.FindProperty("_allowBasicParryWithSkills").boolValue = cfg.Name == "Small_dragon";
            serializedAI.FindProperty("_playAttackStatesDirectly").boolValue = cfg.Name == "UndeadExecutioner" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "SkeletonKnight";
            serializedAI.FindProperty("_usePassiveStanceAnimations").boolValue = cfg.Name == "Reaper";
            serializedAI.FindProperty("_useMovementAnimationStates").boolValue = cfg.Name == "Demon";
            serializedAI.FindProperty("_flipSpriteInsteadOfTransformScale").boolValue = cfg.Name == "Demon";
            serializedAI.FindProperty("_flipSpriteInsteadOfTransformScale").boolValue = cfg.Name == "ArchDemon" || cfg.Name == "SkeletonKnight";
            serializedAI.FindProperty("_facingFlipDeadZone").floatValue = cfg.Name == "SkeletonKnight" ? 0.12f : cfg.Name == "ArchDemon" ? 0.1f : 0f;
            serializedAI.FindProperty("_avoidTeleportOnReturn").boolValue = cfg.Name == "ArchDemon" || cfg.Name == "SkeletonKnight";
            if (cfg.Name == "UndeadExecutioner")
                serializedAI.FindProperty("_deathDestroyDelay").floatValue = 2.2f;
            else if (cfg.Name == "ArchDemon")
                serializedAI.FindProperty("_deathDestroyDelay").floatValue = 0.8f;
            serializedAI.FindProperty("_cycleNonParryableSkills").boolValue = cfg.Name == "BlueSlime" || cfg.Name == "Skeleton";
            serializedAI.FindProperty("_continuousActions").boolValue = cfg.Name == "Skeleton";
            if (cfg.Name == "SkeletonKnight")
            {
                var attackSequence = serializedAI.FindProperty("_basicAttackAnimStates");
                attackSequence.arraySize = 3;
                attackSequence.GetArrayElementAtIndex(0).stringValue = "SideSwing";
                attackSequence.GetArrayElementAtIndex(1).stringValue = "FwdSwing";
                attackSequence.GetArrayElementAtIndex(2).stringValue = "DownSwing";
                var damageDelays = serializedAI.FindProperty("_basicAttackDamageStartDelays");
                damageDelays.arraySize = 3;
                damageDelays.GetArrayElementAtIndex(0).floatValue = 0.25f;
                damageDelays.GetArrayElementAtIndex(1).floatValue = 0.125f;
                damageDelays.GetArrayElementAtIndex(2).floatValue = 0.375f;
            }
            if (cfg.Name == "Skeleton")
                serializedAI.FindProperty("_projectileSpawnOffset").vector2Value = new Vector2(0.6f, 1.4f);
            serializedAI.FindProperty("_isFlying").boolValue = cfg.IsFlying;
            serializedAI.FindProperty("_flyingChaseHeightOffset").floatValue = cfg.Name == "FlyingEye" ? -0.7f : 0f;
            serializedAI.FindProperty("_flyingIdleHeightOffset").floatValue = cfg.Name == "FlyingEye" ? -0.7f : 0f;
            serializedAI.FindProperty("_hasRangedAttack").boolValue = cfg.HasRanged;
            if (projPrefab != null) serializedAI.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
            if (spellPrefab != null) serializedAI.FindProperty("_groundSpellPrefab").objectReferenceValue = spellPrefab;
            serializedAI.ApplyModifiedProperties();
            ConfigureMonsterSkillsAndParry(ai, cfg.Name);
            if (cfg.Name == "FlyingEye") SetupFlyingEyeAudio(root);
            if (cfg.Name == "FlyingEye") SetupFlyingEyeParry(root, sr);
            if (cfg.Name == "FireWorm") SetupFireWormParry(root, sr);
            if (cfg.Name == "Jinn") SetupJinnParry(root, sr);
            if (cfg.Name == "Goblin") SetupStandardParryStunEffect(root, sr);
            if (cfg.Name == "ForestMushroom") root.AddComponent<ForestMushroomRunSync>();
            if (cfg.Name == "UndeadExecutioner")
            {
                var effect = root.AddComponent<UndeadExecutionerSummonEffect>();
                var effectData = new SerializedObject(effect);
                string spriteDir = "Assets/sprites/Monsters/Undead executioner/Undead executioner puppet/png";
                AssignSummonFrames(effectData.FindProperty("_appearFrames"), $"{spriteDir}/summonAppear.png");
                AssignSummonFrames(effectData.FindProperty("_idleFrames"), $"{spriteDir}/summonIdle.png");
                AssignSummonFrames(effectData.FindProperty("_deathFrames"), $"{spriteDir}/summonDeath.png");
                effectData.ApplyModifiedPropertiesWithoutUndo();
            }

            // Setup Floating Health Bar Canvas
            CreateHealthBarCanvas(root, stats, colHeight);
            if (cfg.Name == "FlyingEye")
            {
                var healthBar = root.GetComponentInChildren<FloatingHealthBar>();
                if (healthBar != null)
                {
                    float parentScale = Mathf.Max(0.01f, Mathf.Abs(root.transform.lossyScale.x));
                    float matchingLocalScale = 0.024f / parentScale;
                    healthBar.transform.localScale = new Vector3(matchingLocalScale, matchingLocalScale, 1f);
                    healthBar.GetComponent<RectTransform>().sizeDelta = new Vector2(59.1742f, 8.8075f);
                    var serializedHealthBar = new SerializedObject(healthBar);
                    serializedHealthBar.FindProperty("_followSprite").objectReferenceValue = sr;
                    serializedHealthBar.FindProperty("_useVisibleSpriteBounds").boolValue = true;
                    serializedHealthBar.FindProperty("_followSpriteBounds").boolValue = true;
                    serializedHealthBar.FindProperty("_headOffset").floatValue = 0.08f;
                    serializedHealthBar.ApplyModifiedPropertiesWithoutUndo();

                    var receiver = root.GetComponent<ParryReceiver>();
                    if (receiver == null) receiver = root.AddComponent<ParryReceiver>();
                    var parry = new SerializedObject(receiver);
                    parry.FindProperty("_centerSprite").objectReferenceValue = sr;
                    parry.FindProperty("_useVisibleSpriteBounds").boolValue = true;
                    parry.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (cfg.Name == "BlueSlime" || cfg.Name == "Skeleton" || cfg.Name == "SkeletonKnight" || cfg.Name == "Small_dragon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon" || cfg.Name == "ArchDemon")
            {
                // Match BlueSlime's world-space UI scale (root 4x, canvas 0.006).
                float barScale = cfg.Name == "ArchDemon" ? 0.004f : cfg.Name == "Skeleton" || cfg.Name == "SkeletonKnight" || cfg.Name == "Small_dragon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Necromancer" || cfg.Name == "Demon" ? 0.0048f : 0.006f;
                if (cfg.Name == "SkeletonKnight") barScale = 0.0032f;
                if (cfg.Name == "BringerOfDeath")
                    barScale = 0.024f / Mathf.Max(0.01f, Mathf.Abs(root.transform.lossyScale.x));
                var healthBar = root.GetComponentInChildren<FloatingHealthBar>();
                healthBar.transform.localScale = new Vector3(barScale, barScale, 1f);
                if (cfg.Name == "ShadowDemonDragon")
                    healthBar.transform.localScale = new Vector3(0.024f, 0.024f, 1f);
                if (cfg.Name == "Reaper")
                {
                    float matchingLocalScale = 0.042f / Mathf.Abs(root.transform.lossyScale.x);
                    healthBar.transform.localScale = new Vector3(matchingLocalScale, matchingLocalScale, 1f);
                }
                if (cfg.Name == "SkeletonKnight" || cfg.Name == "ArchDemon")
                    healthBar.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 14f);
                else if (cfg.Name == "Skeleton" || cfg.Name == "Small_dragon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon")
                    healthBar.GetComponent<RectTransform>().sizeDelta = new Vector2(59.1742f, 8.8075f);
                var bar = new SerializedObject(root.GetComponentInChildren<FloatingHealthBar>());
                bar.FindProperty("_followSprite").objectReferenceValue = cfg.Name == "ArchDemon" ? null : sr;
                bar.FindProperty("_useVisibleSpriteBounds").boolValue = cfg.Name == "Skeleton" || cfg.Name == "SkeletonKnight" || cfg.Name == "Small_dragon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon";
                bar.FindProperty("_followSpriteBounds").boolValue = cfg.Name == "BringerOfDeath";
                bar.FindProperty("_headGap").floatValue = 0.08f;
                bar.FindProperty("_headHorizontalOffset").floatValue = cfg.Name == "Demon" ? -0.08f : 0f;
                if (cfg.Name == "BringerOfDeath") bar.FindProperty("_headOffset").floatValue = 0.08f;
                bar.ApplyModifiedPropertiesWithoutUndo();
                if (cfg.Name == "ArchDemon")
                {
                    // Keep this boss bar at a stable height relative to the enemy root;
                    // attack frames have sprite bounds that extend far above the head.
                    healthBar.transform.localPosition = Vector3.zero;
                    healthBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0.95f);
                }
                var receiver = root.GetComponent<ParryReceiver>();
                if (receiver == null) receiver = root.AddComponent<ParryReceiver>();
                var parry = new SerializedObject(receiver);
                parry.FindProperty("_centerSprite").objectReferenceValue = sr;
                parry.FindProperty("_useVisibleSpriteBounds").boolValue = cfg.Name == "Skeleton" || cfg.Name == "SkeletonKnight" || cfg.Name == "Small_dragon" || cfg.Name == "UndeadExecutioner" || cfg.Name == "ShadowDemonDragon" || cfg.Name == "ForestMushroom" || cfg.Name == "Reaper" || cfg.Name == "Necromancer" || cfg.Name == "BringerOfDeath" || cfg.Name == "Demon" || cfg.Name == "FantasyMushroom";
                parry.FindProperty("_centerLocalOffset").vector2Value = cfg.Name == "Demon" ? new Vector2(-0.08f, 0f) : Vector2.zero;
                if (cfg.Name == "FantasyMushroom")
                {
                    string[] stunFramePaths =
                    {
                        "Assets/sprites/effect/stun/Stun_1.png",
                        "Assets/sprites/effect/stun/Stun_2.png",
                        "Assets/sprites/effect/stun/Stun_3.png",
                        "Assets/sprites/effect/stun/Stun_4.png",
                        "Assets/sprites/effect/stun/Stun_5.png"
                    };
                    var stunFrames = parry.FindProperty("_stunEffectFrames");
                    stunFrames.arraySize = stunFramePaths.Length;
                    for (int i = 0; i < stunFramePaths.Length; i++)
                    {
                        stunFrames.GetArrayElementAtIndex(i).objectReferenceValue =
                            AssetDatabase.LoadAssetAtPath<Texture2D>(stunFramePaths[i]);
                    }
                    parry.FindProperty("_stunEffectDuration").floatValue = 2f;
                    parry.FindProperty("_stunEffectHeadXFraction").floatValue = 0f;
                    parry.FindProperty("_stunEffectHeadYFraction").floatValue = 0f;
                    parry.FindProperty("_stunEffectHeadYOffset").floatValue = 0.1f;
                    parry.FindProperty("_stunEffectWorldScale").floatValue = 0.5f;
                }
                parry.ApplyModifiedPropertiesWithoutUndo();
                if (cfg.Name == "Skeleton")
                {
                    var healthRect = root.GetComponentInChildren<FloatingHealthBar>().GetComponent<RectTransform>();
                    var visible = SpriteVisualBounds.GetWorldBounds(sr);
                    healthRect.position = new Vector3(visible.center.x, visible.max.y + 0.08f + healthRect.rect.height * healthRect.lossyScale.y * 0.5f, 0);
                }
            }

            // Setup Hitbox for damage dealing
            CreateContactHitbox(root, cfg.Attack, colWidth * (cfg.Name == "Necromancer" ? 1.2f : 1.1f), colHeight * 1.05f);
            if (cfg.Name == "Reaper")
                root.GetComponentInChildren<EnemyHitbox2D>().GetComponent<BoxCollider2D>().size = new Vector2(0.52f, 0.63f);
            if (cfg.Name == "ForestMushroom")
                root.GetComponentInChildren<EnemyHitbox2D>().GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.65f);
            if (cfg.Name == "BringerOfDeath")
            {
                var hitbox = root.GetComponentInChildren<EnemyHitbox2D>();
                var hitboxCollider = hitbox.GetComponent<BoxCollider2D>();
                hitboxCollider.size = new Vector2(0.6f, 0.63f);
                hitbox.transform.localPosition = new Vector3(col.offset.x,
                    col.offset.y - col.size.y * 0.5f + hitboxCollider.size.y * 0.5f, 0f);
            }
            if (cfg.Name == "Skeleton")
            {
                var hitbox = root.GetComponentInChildren<EnemyHitbox2D>();
                hitbox.GetComponent<BoxCollider2D>().size = new Vector2(0.36f, 0.51f);
                hitbox.transform.localPosition = new Vector3(0.16f, 0.255f, 0);
            }
            if (cfg.Name == "SkeletonKnight")
            {
                var hitbox = root.GetComponentInChildren<EnemyHitbox2D>();
                hitbox.GetComponent<BoxCollider2D>().size = new Vector2(0.95f, colHeight * 1.05f);
                hitbox.transform.localPosition = new Vector3(0f, colHeight * 0.525f, 0f);
            }

            // Special handling for ShadowDemonDragon: Audio Controller
            if (cfg.Name == "ShadowDemonDragon")
            {
                SetupDragonAudio(root);
            }
            else if (cfg.Name == "FantasyMushroom")
            {
                SetupFantasyMushroomAudio(root);
            }
            else if (cfg.Name == "FireWorm")
            {
                SetupFireWormAudio(root);
            }
            else if (cfg.Name == "ForestMushroom")
            {
                SetupForestMushroomAudio(root);
            }
            else if (cfg.Name == "Fox")
            {
                SetupFoxAudio(root);
            }
            else if (cfg.Name == "Goblin")
            {
                SetupGoblinAudio(root);
            }
            else if (cfg.Name == "Jinn")
            {
                SetupJinnAudio(root);
            }
            else if (cfg.Name == "Lizard")
            {
                SetupLizardAudio(root);
                SetupStandardParryStunEffect(root, sr);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[EnemyPrefabBuilder] Created monster prefab: {cfg.Name}");
        }

        private static void AssignSummonFrames(SerializedProperty property, string assetPath)
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>()
                .OrderBy(sprite => sprite.name, System.StringComparer.Ordinal).ToArray();
            property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        private static void SetupSmallDragonFireAttack(GameObject root)
        {
            var effect = new GameObject("FireAttack");
            effect.transform.SetParent(root.transform, false);
            effect.transform.localPosition = new Vector3(0.46f, 0.08f, -0.02f);
            effect.transform.localScale = new Vector3(1.75f, 1.75f, 1f);

            var fireRenderer = effect.AddComponent<SpriteRenderer>();
            fireRenderer.enabled = false;
            fireRenderer.sortingOrder = 3;

            var fireEffect = effect.AddComponent<SmallDragonFireAttackEffect>();

            var sprites = new Sprite[6];
            for (int i = 0; i < 5; i++)
            {
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/sprites/Monsters/craftpix-561178-free-rpg-monster-sprites-pixel-art/PNG/small_dragon/Fire_Attack{i + 1}.png");
            }

            // Fire_Attack6 is imported as a single Sprite in Unity; use that
            // full frame instead of an automatically sliced sub-sprite.
            sprites[5] = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/sprites/Monsters/craftpix-561178-free-rpg-monster-sprites-pixel-art/PNG/small_dragon/Fire_Attack6.png");
            if (sprites.Any(sprite => sprite == null)) return;

            var effectData = new SerializedObject(fireEffect);
            effectData.FindProperty("_renderer").objectReferenceValue = fireRenderer;
            var frameProperty = effectData.FindProperty("_frames");
            frameProperty.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                frameProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            effectData.FindProperty("_duration").floatValue = 2f;
            effectData.FindProperty("_frameRate").floatValue = 16f;
            effectData.ApplyModifiedPropertiesWithoutUndo();
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
            rt.sizeDelta = new Vector2(50f, 14f);

            // Background
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // Fill
            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(canvasGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;

            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = new Vector2(-4f, -4f); // 2px border inset

            // FloatingHealthBar script
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

        private static void SetupDragonAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 25f;

            var dragonAudio = root.AddComponent<DragonAudioController>();

            var idle = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_idle.mp3");
            var footstep = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_foot tep.mp3");
            var attack = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_attack.mp3");
            var hit = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_hit.mp3");
            var death = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_death.mp3");
            var deathFall = AssetDatabase.LoadAssetAtPath<AudioClip>($"{DragonAudioDir}/dragon_death_(Falling_to_the_ground).mp3");

            dragonAudio.SetClips(idle, footstep, attack, hit, death, deathFall);
            Debug.Log("[EnemyPrefabBuilder] Connected Shadow Demon Dragon SFX clips successfully!");
        }

        private static void SetupFantasyMushroomAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var mushroomAudio = root.AddComponent<FantasyMushroomAudioController>();
            mushroomAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงแทง.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงยิง.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Fantasy Mushroom SFX clips successfully!");
        }

        private static void SetupForestMushroomAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var mushroomAudio = root.AddComponent<ForestMushroomAudioController>();
            mushroomAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงก้อนแตก.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Forest Mushroom SFX clips successfully!");
        }

        private static void SetupFlyingEyeAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var flyingEyeAudio = root.AddComponent<FlyingEyeAudioController>();
            flyingEyeAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงกัด.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงยิง.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected FlyingEye SFX clips successfully!");
        }

        private static void SetupFlyingEyeParry(GameObject root, SpriteRenderer spriteRenderer)
        {
            var receiver = root.GetComponent<ParryReceiver>();
            if (receiver == null) receiver = root.AddComponent<ParryReceiver>();

            var parry = new SerializedObject(receiver);
            parry.FindProperty("_centerSprite").objectReferenceValue = spriteRenderer;
            parry.FindProperty("_useVisibleSpriteBounds").boolValue = true;
            parry.FindProperty("_centerLocalOffset").vector2Value = Vector2.zero;

            string[] stunFramePaths =
            {
                "Assets/sprites/effect/Stun/Stun_1.png",
                "Assets/sprites/effect/Stun/Stun_2.png",
                "Assets/sprites/effect/Stun/Stun_3.png",
                "Assets/sprites/effect/Stun/Stun_4.png",
                "Assets/sprites/effect/Stun/Stun_5.png"
            };
            var stunFrames = parry.FindProperty("_stunEffectFrames");
            stunFrames.arraySize = stunFramePaths.Length;
            for (int i = 0; i < stunFramePaths.Length; i++)
            {
                stunFrames.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(stunFramePaths[i]);
            }

            parry.FindProperty("_stunEffectDuration").floatValue = 2f;
            parry.FindProperty("_stunEffectHeadXFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYOffset").floatValue = 0.25f;
            parry.FindProperty("_stunEffectWorldScale").floatValue = 0.5f;
            parry.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupFireWormAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var fireWormAudio = root.AddComponent<FireWormAudioController>();
            fireWormAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/ปล่อยลูกไฟ.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Fire Worm SFX clips successfully!");
        }

        private static void SetupJinnAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var jinnAudio = root.AddComponent<JinnAudioController>();
            jinnAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงยิงน้ำ.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงลม.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Jinn SFX clips successfully!");
        }

        private static void SetupJinnParry(GameObject root, SpriteRenderer spriteRenderer)
        {
            var receiver = root.GetComponent<ParryReceiver>();
            if (receiver == null) receiver = root.AddComponent<ParryReceiver>();

            var parry = new SerializedObject(receiver);
            parry.FindProperty("_centerSprite").objectReferenceValue = spriteRenderer;
            parry.FindProperty("_useVisibleSpriteBounds").boolValue = true;
            parry.FindProperty("_centerLocalOffset").vector2Value = Vector2.zero;

            string[] stunFramePaths =
            {
                "Assets/sprites/effect/Stun/Stun_1.png",
                "Assets/sprites/effect/Stun/Stun_2.png",
                "Assets/sprites/effect/Stun/Stun_3.png",
                "Assets/sprites/effect/Stun/Stun_4.png",
                "Assets/sprites/effect/Stun/Stun_5.png"
            };
            var stunFrames = parry.FindProperty("_stunEffectFrames");
            stunFrames.arraySize = stunFramePaths.Length;
            for (int i = 0; i < stunFramePaths.Length; i++)
            {
                stunFrames.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(stunFramePaths[i]);
            }

            parry.FindProperty("_stunEffectDuration").floatValue = 2f;
            parry.FindProperty("_stunEffectHeadXFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYOffset").floatValue = 0.25f;
            parry.FindProperty("_stunEffectWorldScale").floatValue = 0.5f;
            parry.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupFoxAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var foxAudio = root.AddComponent<FoxAudioController>();
            foxAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงกัด.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงหมาหอน.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/ปล่อยลูกไฟ.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงก้อนแตก.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงหมาป่าหายตัว.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Fox SFX clips successfully!");
        }

        private static void SetupGoblinAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var goblinAudio = root.AddComponent<GoblinAudioController>();
            goblinAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/ฟันดาบ.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงขว้าง.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Goblin SFX clips successfully!");
        }

        private static void SetupLizardAudio(GameObject root)
        {
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            var lizardAudio = root.AddComponent<LizardAudioController>();
            lizardAudio.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/เสียงแทง.WAV"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/โดนผู้เล่นโจมตี.WAV"));
            Debug.Log("[EnemyPrefabBuilder] Connected Lizard SFX clips successfully!");
        }

        private static void SetupFireWormParry(GameObject root, SpriteRenderer spriteRenderer)
        {
            var receiver = root.GetComponent<ParryReceiver>();
            if (receiver == null) receiver = root.AddComponent<ParryReceiver>();

            var parry = new SerializedObject(receiver);
            parry.FindProperty("_centerSprite").objectReferenceValue = spriteRenderer;
            parry.FindProperty("_useVisibleSpriteBounds").boolValue = true;
            parry.FindProperty("_centerLocalOffset").vector2Value = Vector2.zero;

            string[] stunFramePaths =
            {
                "Assets/sprites/effect/Stun/Stun_1.png",
                "Assets/sprites/effect/Stun/Stun_2.png",
                "Assets/sprites/effect/Stun/Stun_3.png",
                "Assets/sprites/effect/Stun/Stun_4.png",
                "Assets/sprites/effect/Stun/Stun_5.png"
            };
            var stunFrames = parry.FindProperty("_stunEffectFrames");
            stunFrames.arraySize = stunFramePaths.Length;
            for (int i = 0; i < stunFramePaths.Length; i++)
            {
                stunFrames.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(stunFramePaths[i]);
            }

            parry.FindProperty("_stunEffectDuration").floatValue = 2f;
            parry.FindProperty("_stunEffectHeadXFraction").floatValue = 0.15f;
            parry.FindProperty("_stunEffectHeadYFraction").floatValue = 0.1f;
            parry.FindProperty("_stunEffectHeadYOffset").floatValue = 0.08f;
            parry.FindProperty("_stunEffectWorldScale").floatValue = 0.5f;
            parry.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupStandardParryStunEffect(GameObject root, SpriteRenderer spriteRenderer)
        {
            var receiver = root.GetComponent<ParryReceiver>();
            if (receiver == null) receiver = root.AddComponent<ParryReceiver>();

            var parry = new SerializedObject(receiver);
            parry.FindProperty("_centerSprite").objectReferenceValue = spriteRenderer;
            parry.FindProperty("_useVisibleSpriteBounds").boolValue = true;
            parry.FindProperty("_centerLocalOffset").vector2Value = Vector2.zero;

            string[] stunFramePaths =
            {
                "Assets/sprites/effect/Stun/Stun_1.png",
                "Assets/sprites/effect/Stun/Stun_2.png",
                "Assets/sprites/effect/Stun/Stun_3.png",
                "Assets/sprites/effect/Stun/Stun_4.png",
                "Assets/sprites/effect/Stun/Stun_5.png"
            };
            var stunFrames = parry.FindProperty("_stunEffectFrames");
            stunFrames.arraySize = stunFramePaths.Length;
            for (int i = 0; i < stunFramePaths.Length; i++)
            {
                stunFrames.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(stunFramePaths[i]);
            }

            parry.FindProperty("_stunEffectDuration").floatValue = 2f;
            parry.FindProperty("_stunEffectHeadXFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYFraction").floatValue = 0f;
            parry.FindProperty("_stunEffectHeadYOffset").floatValue = 0.25f;
            parry.FindProperty("_stunEffectWorldScale").floatValue = 0.5f;
            parry.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignFirstSpriteFromController(SpriteRenderer sr, RuntimeAnimatorController controller)
        {
            if (controller == null || controller.animationClips == null || controller.animationClips.Length == 0) return;

            // Look for Idle clip first
            AnimationClip clipToUse = null;
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clipToUse = clip;
                    break;
                }
            }

            if (clipToUse == null) clipToUse = controller.animationClips[0];
            AssignFirstSpriteFromClip(sr, clipToUse);
        }

        private static void AssignFirstSpriteFromClip(SpriteRenderer sr, AnimationClip clip)
        {
            if (clip == null) return;

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (keyframes != null && keyframes.Length > 0 && keyframes[0].value is Sprite sprite)
                    {
                        sr.sprite = sprite;
                        return;
                    }
                }
            }
        }

        private struct SkillData
        {
            public string Name;
            public string AnimName;
            public int ActionIndex;
            public float Multiplier;
            public float Cooldown;
            public float MinRange;
            public float MaxRange;
            public bool IsParryable;
            public string ProjName;
            public string SpellName;
            public bool RequireLineOfSight;

            public SkillData(string name, string animName, int actionIndex, float mult, float cd, float minR, float maxR, bool parry, string proj = null, string spell = null, bool requireLineOfSight = false)
            {
                Name = name;
                AnimName = animName;
                ActionIndex = actionIndex;
                Multiplier = mult;
                Cooldown = cd;
                MinRange = minR;
                MaxRange = maxR;
                IsParryable = parry;
                ProjName = proj;
                SpellName = spell;
                RequireLineOfSight = requireLineOfSight;
            }
        }

        private static TheLastKnight.Combat.EnemySkill CreateSkill(SkillData d)
        {
            var s = new TheLastKnight.Combat.EnemySkill
            {
                skillName = d.Name,
                animationName = d.AnimName,
                actionIndex = d.ActionIndex,
                damageMultiplier = d.Multiplier,
                cooldown = d.Cooldown,
                minRange = d.MinRange,
                maxRange = d.MaxRange,
                isParryable = d.IsParryable,
                requireLineOfSight = d.RequireLineOfSight
            };
            if (d.Name == "FullCombo")
            {
                // The 21-frame clip contains three seven-frame swing sections.
                // Match one damage window to the impact frame in each section
                // (Animator state speed is 0.8, clip sample rate is 12 fps).
                s.damageStartDelay = 0.42f;
                s.damageDuration = 0.18f;
                s.additionalDamageHitTimes = new[] { 1.15f, 1.88f };
            }
            if (!string.IsNullOrEmpty(d.ProjName))
            {
                s.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectilesOutputDir}/{d.ProjName}.prefab");
            }
            if (!string.IsNullOrEmpty(d.SpellName))
            {
                s.groundSpellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjectilesOutputDir}/{d.SpellName}.prefab");
            }
            return s;
        }

        private static TheLastKnight.Combat.EnemySkill[] BuildSkillArray(SkillData[] list)
        {
            var arr = new TheLastKnight.Combat.EnemySkill[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                arr[i] = CreateSkill(list[i]);
            }
            return arr;
        }

        private static void ConfigureMonsterSkillsAndParry(EnemyController ai, string name)
        {
            string basicAnim = "Attack";
            bool isBoss = (name == "DemonBoss" || name == "ShadowDemonDragon");
            TheLastKnight.Combat.EnemySkill[] skills = new TheLastKnight.Combat.EnemySkill[0];

            switch (name)
            {
                case "ForestMushroom":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("AttackWithStun", "AttackWithStun", 1, 1.5f, 5.0f, 0f, 1.6f, true)
                    });
                    break;

                case "Goblin":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("GoblinBomb", "Attack3", -1, 1.6f, 5.0f, 2.5f, 7.0f, true, "Goblin_Bomb")
                    });
                    break;

                case "Skeleton":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("AttackFirst", "Attack", -1, 1f, 0f, 0f, 0.05f, false),
                        new SkillData("AttackSecond", "Attack", -1, 1f, 0f, 0f, 0.05f, false),
                        new SkillData("ShieldGuard", "Shield", -1, 0.5f, 0f, 0f, 0.05f, false),
                        new SkillData("SwordThrow", "Attack3", -1, 1.4f, 5f, 0f, 0.05f, true, "Skeleton_Sword")
                    });
                    skills[2].guardDuration = 1f;
                    skills[3].initialDelay = 5f;
                    break;

                case "FlyingEye":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("EyeBeam", "Attack3", -1, 1.5f, 5.0f, 0f, 7.0f, true, "FlyingEye_Projectile", requireLineOfSight: true)
                    });
                    break;

                case "FantasyMushroom":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("SporeShot", "Attack3", -1, 1.4f, 5.0f, 2.5f, 7.0f, true, "FantasyMushroom_Projectile")
                    });
                    break;

                case "UndeadExecutioner":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("SpinningCleave", "Skill1", 3, 2.0f, 7.0f, 0f, 2.5f, true),
                        new SkillData("DarkSummon", "Summon", 4, 1.5f, 15.0f, 0f, 4.0f, false)
                    });
                    break;

                case "MoonstoneKeeper":
                    basicAnim = "Attack1";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("GroundSlam", "Attack2", -1, 1.8f, 6.0f, 0f, 2.2f, true),
                        new SkillData("DashThrust", "Dash", -1, 1.3f, 5.0f, 3.0f, 6.5f, false)
                    });
                    break;

                case "MechaStoneGolem":
                    basicAnim = "Melee";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("RocketPunch", "Shoot", -1, 1.5f, 5.0f, 2.5f, 8.0f, true, "Golem_ArmProjectile"),
                        new SkillData("LaserBeam", "LaserCast", -1, 2.5f, 10.0f, 2.5f, 9.0f, false, "Golem_Laser"),
                        new SkillData("StoneShield", "ShieldCast", -1, 0.5f, 12.0f, 0f, 3.0f, false)
                    });
                    break;

                case "BlueSlime":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("HeavySlam", "Attack_3", -1, 1.6f, 6.0f, 0f, 0.05f, true),
                        new SkillData("DoubleHop", "Attack_2", -1, 1.3f, 0f, 0f, 0.05f, false),
                        new SkillData("SlideTackle", "Run+Attack", -1, 1.2f, 0f, 0f, 0.05f, false)
                    });
                    break;

                case "Satyr":
                    basicAnim = "Attack1";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("Dropkick", "Skill1", -1, 1.8f, 7.0f, 0f, 2.2f, true),
                        new SkillData("Slash2", "Attack2", -1, 1.4f, 4.0f, 0f, 1.8f, false),
                        new SkillData("NatureCast", "Cast", -1, 2.0f, 9.0f, 2.5f, 7.0f, false)
                    });
                    break;

                case "Necromancer":
                    basicAnim = "Attack1";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("DarkWave", "Attack2", -1, 1.6f, 5.0f, 0f, 0.05f, false),
                        new SkillData("SoulBlast", "Attack3", -1, 2.2f, 10.0f, 0f, 0.05f, true)
                    });
                    break;

                case "SkeletonKnight":
                    basicAnim = "FwdSwing";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("FullCombo", "FullCombo", -1, 2.2f, 10.0f, 0f, 0.05f, false)
                    });
                    break;

                case "DemonBoss":
                    basicAnim = "Attack_01";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("DualClawCleave", "Attack_02", -1, 1.8f, 5.5f, 0f, 3.2f, true),
                        new SkillData("EarthquakeJump", "Jump", -1, 1.6f, 8.0f, 3.5f, 8.0f, false),
                        new SkillData("TerrifyingRoar", "Shout", -1, 0.8f, 12.0f, 0f, 5.0f, false)
                    });
                    break;

                case "BringerOfDeath":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("HellfirePillar", "Cast", -1, 2.0f, 10.0f, 1.5f, 8.0f, true, null, "BringerOfDeath_Spell")
                    });
                    break;

                case "Fox":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("WarpAmbush", "Disappear", -1, 1.5f, 6.0f, 2.5f, 6.5f, true)
                    });
                    break;

                case "ArchDemon":
                    basicAnim = "BasicAtk";
                    break;

                case "Demon":
                    basicAnim = "Attack";
                    break;

                case "DemonKin":
                    basicAnim = "BasicAtk";
                    break;

                case "Dragon":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("FireBall", "Attack", -1, 1.5f, 4.5f, 2.5f, 8.0f, false, "Dragon_FireBall")
                    });
                    break;

                case "FireWorm":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("FireBall", "Attack", -1, 1.4f, 4.0f, 2.5f, 7.0f, false, "FireWorm_FireBall")
                    });
                    break;

                case "Jinn":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("MagicCast", "Attack", -1, 1.8f, 6.0f, 1.5f, 7.0f, false, null, "Jinn_Magic")
                    });
                    break;

                case "Reaper":
                    basicAnim = "HostileAttack";
                    break;

                case "ShadowDemonDragon":
                    basicAnim = "Attack_Left";
                    break;

                case "Small_dragon":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("SmallFireBall", "Attack", -1, 1.3f, 5.0f, 0f, 0.05f, true, "SmallDragon_FireBall")
                    });
                    break;

                default:
                    basicAnim = "Attack";
                    break;
            }

            ai.SetSkills(skills);
            ai.SetBasicAttackConfiguration(basicAnim, 1.0f, name != "Skeleton" && name != "UndeadExecutioner" && name != "ForestMushroom", name == "Small_dragon" ? 5.0f : 4.0f);
            ai.SetBoss(isBoss);
            EditorUtility.SetDirty(ai);
        }
    }
}
