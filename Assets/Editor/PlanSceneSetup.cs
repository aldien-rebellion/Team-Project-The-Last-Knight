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
        vn.arthurBackView = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/Progression/ArthurBack.png");
        EditorSceneManager.SaveScene(scene);
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
        var camera = new GameObject("Main Camera", typeof(Camera));
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
