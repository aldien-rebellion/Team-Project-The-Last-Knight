using System.IO;
using UnityEngine;
using UnityEditor;
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

        private static void BuildDirectProjectile(string name, float speed, float damage, float colliderRadius, bool isCircle)
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
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;

            var anim = go.AddComponent<Animator>();
            if (controller != null)
            {
                anim.runtimeAnimatorController = controller;
                AssignFirstSpriteFromClip(sr, spellClip);
            }

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.8f, 3.2f);
            col.offset = new Vector2(0f, 1.6f);

            var spell = go.AddComponent<GroundSpellArea>();
            var serializedSpell = new SerializedObject(spell);
            serializedSpell.FindProperty("_damage").floatValue = 30f;
            serializedSpell.FindProperty("_delayBeforeDamage").floatValue = 0.45f;
            serializedSpell.FindProperty("_damageDuration").floatValue = 0.5f;
            serializedSpell.FindProperty("_totalLifetime").floatValue = 1.4f;
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
                new MonsterConfig("ArchDemon", 500f, 35f, 5f, 1.8f, 3.8f, 8f, 2.2f),
                new MonsterConfig("BlueSlime", 35f, 10f, 0f, 1.8f, 3.2f, 6f, 1.2f),
                new MonsterConfig("BringerOfDeath", 600f, 40f, 8f, 1.6f, 3.5f, 9f, 2.5f, false, true, null, "BringerOfDeath_Spell"),
                new MonsterConfig("Demon", 70f, 15f, 2f, 2.0f, 4.0f, 7f, 1.6f),
                new MonsterConfig("DemonBoss", 800f, 50f, 10f, 1.5f, 3.6f, 10f, 3.2f),
                new MonsterConfig("DemonKin", 60f, 12f, 1f, 2.2f, 4.2f, 7f, 1.5f),
                new MonsterConfig("Dragon", 450f, 35f, 6f, 2.0f, 4.0f, 9f, 2.5f, false, true, "Dragon_FireBall"),
                new MonsterConfig("Fairy", 25f, 8f, 0f, 2.5f, 4.5f, 7f, 1.0f, true),
                new MonsterConfig("FantasyMushroom", 55f, 12f, 1f, 1.8f, 3.5f, 7f, 1.3f, false, true, "FantasyMushroom_Projectile"),
                new MonsterConfig("FireWorm", 50f, 14f, 1f, 1.6f, 3.2f, 7f, 1.4f, false, true, "FireWorm_FireBall"),
                new MonsterConfig("FlyingEye", 35f, 10f, 0f, 2.2f, 4.2f, 7f, 1.2f, true, true, "FlyingEye_Projectile"),
                new MonsterConfig("ForestMushroom", 50f, 12f, 1f, 1.8f, 3.5f, 6f, 1.3f),
                new MonsterConfig("Fox", 40f, 12f, 0f, 3.0f, 5.5f, 7f, 1.2f),
                new MonsterConfig("Goblin", 45f, 12f, 0f, 2.2f, 4.2f, 7f, 1.3f, false, true, "Goblin_Bomb"),
                new MonsterConfig("Jinn", 300f, 30f, 4f, 2.0f, 4.0f, 8f, 2.0f, false, true, null, "Jinn_Magic"),
                new MonsterConfig("Lizard", 60f, 15f, 2f, 2.0f, 3.8f, 7f, 1.5f),
                new MonsterConfig("MechaStoneGolem", 750f, 45f, 12f, 1.5f, 3.2f, 9f, 2.8f, false, true, "Golem_ArmProjectile"),
                new MonsterConfig("Minotaur_1", 120f, 22f, 3f, 2.0f, 4.2f, 7f, 1.8f),
                new MonsterConfig("Minotaur_2", 140f, 25f, 4f, 1.9f, 4.0f, 7f, 1.8f),
                new MonsterConfig("Minotaur_3", 160f, 28f, 5f, 1.8f, 3.8f, 7f, 1.8f),
                new MonsterConfig("MoonstoneKeeper", 350f, 32f, 6f, 2.2f, 4.5f, 8f, 2.2f),
                new MonsterConfig("Necromancer", 200f, 25f, 3f, 1.8f, 3.8f, 8f, 1.8f),
                new MonsterConfig("Reaper", 280f, 30f, 5f, 2.2f, 4.5f, 8f, 2.2f),
                new MonsterConfig("Satyr", 110f, 20f, 3f, 2.2f, 4.2f, 7f, 1.6f),
                new MonsterConfig("ShadowDemonDragon", 900f, 55f, 12f, 1.8f, 3.8f, 10f, 3.5f),
                new MonsterConfig("Skeleton", 45f, 12f, 1f, 1.8f, 3.5f, 7f, 1.4f, false, true, "Skeleton_Sword"),
                new MonsterConfig("SkeletonKnight", 220f, 28f, 5f, 2.0f, 4.2f, 7f, 1.8f),
                new MonsterConfig("Skullwolf", 65f, 18f, 1f, 3.2f, 5.8f, 8f, 1.4f),
                new MonsterConfig("Small_dragon", 80f, 16f, 2f, 2.2f, 4.2f, 7f, 1.5f, false, true, "SmallDragon_FireBall"),
                new MonsterConfig("Trader_1", 100f, 0f, 0f, 0f, 0f, 0f, 0f), // NPC
                new MonsterConfig("UndeadExecutioner", 550f, 40f, 7f, 1.6f, 3.6f, 8f, 2.4f)
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

            // SpriteRenderer
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
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

            // Rigidbody2D
            var rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = cfg.IsFlying ? 0f : 2.5f;

            // Collider2D (CapsuleCollider2D)
            var col = root.AddComponent<CapsuleCollider2D>();
            Bounds bounds = sr.bounds;
            float colHeight = Mathf.Max(0.6f, bounds.size.y * 0.8f);
            float colWidth = Mathf.Max(0.4f, bounds.size.x * 0.45f);
            col.size = new Vector2(colWidth, colHeight);
            col.offset = new Vector2(0f, colHeight * 0.5f);

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
            serializedAI.FindProperty("_meleeRange").floatValue = cfg.MeleeRange;
            serializedAI.FindProperty("_isFlying").boolValue = cfg.IsFlying;
            serializedAI.FindProperty("_hasRangedAttack").boolValue = cfg.HasRanged;
            if (projPrefab != null) serializedAI.FindProperty("_projectilePrefab").objectReferenceValue = projPrefab;
            if (spellPrefab != null) serializedAI.FindProperty("_groundSpellPrefab").objectReferenceValue = spellPrefab;
            serializedAI.ApplyModifiedProperties();
            ConfigureMonsterSkillsAndParry(ai, cfg.Name);

            // Setup Floating Health Bar Canvas
            CreateHealthBarCanvas(root, stats, colHeight);

            // Setup Hitbox for damage dealing
            CreateContactHitbox(root, cfg.Attack, colWidth * 1.1f, colHeight * 1.05f);

            // Special handling for ShadowDemonDragon: Audio Controller
            if (cfg.Name == "ShadowDemonDragon")
            {
                SetupDragonAudio(root);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[EnemyPrefabBuilder] Created monster prefab: {cfg.Name}");
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

            public SkillData(string name, string animName, int actionIndex, float mult, float cd, float minR, float maxR, bool parry, string proj = null, string spell = null)
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
                isParryable = d.IsParryable
            };
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
                        new SkillData("AttackWithStun", "AttackWithStun", 1, 1.5f, 6.0f, 0f, 1.6f, true)
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
                        new SkillData("SwordThrow", "Attack3", -1, 1.4f, 4.5f, 2.5f, 6.5f, true, "Skeleton_Sword"),
                        new SkillData("ShieldGuard", "Shield", -1, 0.5f, 8.0f, 0f, 2.0f, false)
                    });
                    break;

                case "FlyingEye":
                    basicAnim = "Attack";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("EyeBeam", "Attack3", -1, 1.5f, 5.0f, 2.5f, 7.5f, true, "FlyingEye_Projectile")
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
                        new SkillData("HeavySlam", "Attack_3", -1, 1.6f, 6.0f, 0f, 2.0f, true),
                        new SkillData("DoubleHop", "Attack_2", -1, 1.3f, 3.5f, 0f, 1.8f, false),
                        new SkillData("SlideTackle", "Run+Attack", -1, 1.2f, 5.0f, 2.5f, 5.5f, false)
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
                        new SkillData("SoulBlast", "Attack3", -1, 2.2f, 9.0f, 1.5f, 7.0f, true),
                        new SkillData("DarkWave", "Attack2", -1, 1.6f, 5.0f, 2.0f, 7.5f, false)
                    });
                    break;

                case "SkeletonKnight":
                    basicAnim = "FwdSwing";
                    skills = BuildSkillArray(new SkillData[]
                    {
                        new SkillData("DownSwing", "DownSwing", -1, 1.6f, 5.0f, 0f, 2.0f, true),
                        new SkillData("SideSwing", "SideSwing", -1, 1.3f, 4.0f, 0f, 1.8f, false),
                        new SkillData("FullCombo", "FullCombo", -1, 2.2f, 9.0f, 0f, 2.2f, false)
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
                        new SkillData("HellfirePillar", "Cast", -1, 2.0f, 8.0f, 1.5f, 8.0f, true, null, "BringerOfDeath_Spell")
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
                        new SkillData("SmallFireBall", "Attack", -1, 1.3f, 4.0f, 2.5f, 7.0f, false, "SmallDragon_FireBall")
                    });
                    break;

                default:
                    basicAnim = "Attack";
                    break;
            }

            ai.SetSkills(skills);
            ai.SetBasicAttackConfiguration(basicAnim, 1.0f, true, 4.0f);
            ai.SetBoss(isBoss);
            EditorUtility.SetDirty(ai);
        }
    }
}
