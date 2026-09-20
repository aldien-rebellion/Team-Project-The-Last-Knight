using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Player;
using TheLastKnight.Combat;
using TheLastKnight.UI;

public static class PlanSceneSetup
{
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
