using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Player;
using TheLastKnight.Combat;
using TheLastKnight.UI;
using TheLastKnight.Environment;
using TheLastKnight.AI;

public static class PlanSceneSetup
{
    public static string Surfaces(string name, float[] xs, float top)
    {
        string path = "Assets/Scenes/Maps/" + name + ".unity";
        var existing = SceneManager.GetSceneByPath(path);
        var scene = existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Physics2D.SyncTransforms();
            return name + "\n" + string.Join("\n", xs.Select(x => x + ": " + string.Join(", ", Physics2D.RaycastAll(new Vector2(x, top), Vector2.down, 100)
                .Where(h => h.collider.gameObject.scene == scene && !h.collider.isTrigger)
                .Select(h => h.collider.name + " at " + h.point.y + " layer " + h.collider.gameObject.layer))));
        }
        finally { if (!existing.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void ImportProgressionArt()
    {
        foreach (string file in new[] { "ArthurBack", "Medusa", "RuneChest" })
        {
            string path = "Assets/sprites/Progression/" + file + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = file == "Medusa" ? 400 : file == "ArthurBack" ? 520 : 650;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    public static void SetupGate()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maps/DemonCastleEntrance.unity", OpenSceneMode.Single);
        var gate = Object.FindAnyObjectByType<DemonCastleGate>();
        var vn = gate.GetComponent<DemonCastleGateVN>();
        if (vn == null) vn = gate.gameObject.AddComponent<DemonCastleGateVN>();
        vn.gate = gate;
        vn.arthurBackView = AssetDatabase.LoadAllAssetsAtPath("Assets/sprites/Player/player-behind.png").OfType<Sprite>().First();
        foreach (var pickup in Object.FindObjectsByType<RunePickup>(FindObjectsSortMode.None)) pickup.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene);
    }

    public static string Populate(string name)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maps/" + name + ".unity", OpenSceneMode.Single);
        if (GameObject.Find("Plan Gameplay") != null) return name + " already populated";
        var root = new GameObject("Plan Gameplay");
        // Existing terrain is deliberately preserved. Its Default layer is included in enemy probes.
        if (name == "CityCenter")
        {
            Statue(root.transform, 5, -4.5f, true);
            float[] xs = { -180, -140, -100, -60, -30, 30, 60, 90, 120, 150 };
            for (int i = 0; i < xs.Length; i++) Enemy(root.transform, i % 2 == 0 ? "BlueSlime" : "Skeleton", xs[i], -4.5f);
        }
        else if (name == "Church")
        {
            var boss = Enemy(root.transform, "MoonstoneKeeper", 60, -4.5f, 250, 25);
            boss.gameObject.AddComponent<EnemyProgressionReward>().churchKey = true;
            Arena(root.transform, boss, 60, -4.5f, 10);
            var chest = Prop(root.transform, "Pentagram Chest", "Assets/sprites/Progression/RuneChest.png", 76, 16.75f);
            chest.AddComponent<RuneChest>();
        }
        else if (name == "OutdoorMarket")
        {
            var trader = Prop(root.transform, "The Shadow Market", "Assets/sprites/Monsters/Free-City-Trader-Character-Sprite-Sheets-Pixel-Art/Trader_1/Idle.png", -105, -4.5f);
            trader.AddComponent<ShadowMarketNPC>();
            Statue(root.transform, -112, -4.5f, false);
            Enemy(root.transform, "Skeleton", -65, -4.5f);
            Enemy(root.transform, "BlueSlime", -20, -4.5f);
            var boss = Enemy(root.transform, "DemonBoss", 40, -4.5f, 350, 28);
            Arena(root.transform, boss, 40, -4.5f, 11);
            Enemy(root.transform, "Skeleton", 85, -4.5f);
        }
        else if (name == "SuburbToForest")
        {
            Statue(root.transform, -185, -6.7f, false);
            var fox = Enemy(root.transform, "Fox", -130, -6.245f);
            fox.gameObject.AddComponent<EnemyProgressionReward>().runeIndex = 2;
            Enemy(root.transform, "FireWorm", -80, -6.38f);
            Enemy(root.transform, "Fox", -25, -5.6f);
            Enemy(root.transform, "FireWorm", 30, -6.0f);
        }
        else if (name == "DemonCastle")
        {
            Statue(root.transform, -10, -1.85f, false);
            var boss = Enemy(root.transform, "DemonBoss", 25, -1.885f, 600, 35);
            boss.gameObject.AddComponent<EnemyProgressionReward>().finalBoss = true;
            Arena(root.transform, boss, 25, -1.885f, 9);
        }
        EditorSceneManager.SaveScene(scene);
        return name + " populated: " + root.GetComponentsInChildren<EnemyStats>().Length + " enemies";
    }

    private static GameObject Prop(Transform parent, string name, string path, float x, float floor)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null) throw new System.InvalidOperationException("Sprite missing: " + path);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sortingLayerName = "Interactable_Back";
        float height = name == "The Shadow Market" ? 2.1f : sprite.bounds.size.y;
        if (name == "The Shadow Market") go.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
        go.transform.position = new Vector3(x, floor - renderer.bounds.min.y + 0.03f, 0);
        return go;
    }

    private static void Statue(Transform parent, float x, float floor, bool rune)
    {
        var go = Prop(parent, "Medusa Save Point", "Assets/sprites/Environment/Materials/medusa Statue.png", x, floor);
        go.AddComponent<MedusaSavePoint>().grantsCityRune = rune;
        var area = go.GetComponent<CircleCollider2D>(); area.isTrigger = true; area.radius = 4;
    }

    private static EnemyStats Enemy(Transform parent, string name, float x, float floor, float hp = 0, float attack = 0)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + name + ".prefab");
        if (prefab == null) throw new System.InvalidOperationException("Enemy prefab missing: " + name);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        var body = go.GetComponents<Collider2D>().First(c => !c.isTrigger);
        Physics2D.SyncTransforms();
        go.transform.position = new Vector3(x, floor - body.bounds.min.y + 0.08f, 0);
        var stats = go.GetComponent<EnemyStats>();
        var serializedStats = new SerializedObject(stats);
        serializedStats.FindProperty("_goldReward").intValue = hp > 0 ? 100 : 8;
        serializedStats.FindProperty("_expReward").intValue = hp > 0 ? 150 : 15;
        if (hp > 0)
        {
            serializedStats.FindProperty("_maxHealth").floatValue = hp;
            serializedStats.FindProperty("_attackPower").floatValue = attack;
            serializedStats.FindProperty("_defense").floatValue = 2;
        }
        serializedStats.ApplyModifiedPropertiesWithoutUndo();
        var ai = new SerializedObject(go.GetComponent<EnemyController>());
        ai.FindProperty("_groundLayer").intValue = (1 << 0) | (1 << 6) | (1 << 7);
        ai.FindProperty("_patrolDistance").floatValue = hp > 0 ? 2f : 4f;
        ai.ApplyModifiedPropertiesWithoutUndo();
        foreach (var hitbox in go.GetComponentsInChildren<EnemyHitbox2D>()) hitbox.Damage = hp > 0 ? attack : stats.AttackPower;
        PrefabUtility.RecordPrefabInstancePropertyModifications(stats);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<EnemyController>());
        return stats;
    }

    private static void Arena(Transform parent, EnemyStats boss, float x, float floor, float halfWidth)
    {
        var go = new GameObject("Boss Arena", typeof(BoxCollider2D));
        go.transform.SetParent(parent, false); go.transform.position = new Vector3(x, floor + 3, 0);
        var area = go.GetComponent<BoxCollider2D>(); area.isTrigger = true; area.size = new Vector2(halfWidth * 2 - 3, 6);
        var arena = go.AddComponent<BossArena>(); arena.boss = boss;
        arena.barriers = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            var barrier = new GameObject("Red Seal " + i, typeof(BoxCollider2D), typeof(SpriteRenderer));
            barrier.transform.SetParent(go.transform, false); barrier.layer = 6;
            barrier.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * halfWidth, 0, 0);
            barrier.GetComponent<BoxCollider2D>().size = new Vector2(0.4f, 10);
            var renderer = barrier.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = new Vector2(0.4f, 10);
            renderer.color = new Color(1, 0.12f, 0.15f, 0.75f); renderer.sortingLayerName = "VFX";
            arena.barriers[i] = barrier;
        }
    }

    public static void ConfigureBuild()
    {
        string[] names = { "MainMenu", "CityCenter", "OutdoorMarket", "SuburbToForest", "DemonCastleEntrance", "DemonCastle", "Church" };
        EditorBuildSettings.scenes = names.Select(n => new EditorBuildSettingsScene("Assets/Scenes/" + (n == "MainMenu" ? "" : "Maps/") + n + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets();
    }
    public static string Inspect(string name)
    {
        string path = "Assets/Scenes/Maps/" + name + ".unity";
        var existing = SceneManager.GetSceneByPath(path);
        var scene = existing.isLoaded ? existing : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var selected = transforms.Where(t => t.GetComponent<PlayerController>() != null || t.GetComponent<EnemyStats>() != null
                || t.GetComponent<ScenePortal>() != null || t.GetComponent<TheLastKnight.Environment.TeleportDoor>() != null
                || t.GetComponent<Camera>() != null || t.name.ToLower().Contains("medusa") || t.name.ToLower().Contains("statue")
                || t.name.ToLower().Contains("chest") || t.GetComponent<TheLastKnight.Environment.DemonCastleGate>() != null);
            return name + "\n" + string.Join("\n", selected.Select(t => t.name + " " + t.position
                + (t.GetComponent<ScenePortal>() != null ? " -> " + t.GetComponent<ScenePortal>().targetSceneName : "")))
                + "\nMissing scripts: " + transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        }
        finally { if (!existing.isLoaded) EditorSceneManager.CloseScene(scene, true); }
    }

    public static void CreateMainMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        SceneManager.MoveGameObjectToScene(camera, scene);
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().orthographic = true;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.025f, 0.035f, 0.065f);
        var menu = new GameObject("Main Menu", typeof(MainMenuController));
        SceneManager.MoveGameObjectToScene(menu, scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        EditorSceneManager.CloseScene(scene, true);
    }
}
