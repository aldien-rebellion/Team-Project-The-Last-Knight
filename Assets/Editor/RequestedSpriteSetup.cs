using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Environment;

public static class RequestedSpriteSetup
{
    public const string BehindPath = "Assets/sprites/Player/player-behind.png";
    public const string MedusaPath = "Assets/sprites/Environment/Materials/medusa Statue.png";

    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before updating scene assets.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes before applying sprites.");
        SetPixelsPerUnit(BehindPath, 850f);
        SetPixelsPerUnit(MedusaPath, 232.55f);
        var behind = AssetDatabase.LoadAllAssetsAtPath(BehindPath).OfType<Sprite>().Single();
        var medusa = AssetDatabase.LoadAllAssetsAtPath(MedusaPath).OfType<Sprite>().Single();
        int statues = 0;
        try
        {
            var entrance = EditorSceneManager.OpenScene("Assets/Scenes/Maps/DemonCastleEntrance.unity");
            var gate = UnityEngine.Object.FindAnyObjectByType<DemonCastleGateVN>();
            Undo.RecordObject(gate, "Use requested player-behind sprite");
            gate.arthurBackView = behind;
            foreach (var pickup in UnityEngine.Object.FindObjectsByType<RunePickup>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(pickup.gameObject, "Disable obsolete gate test rune pickup");
                pickup.gameObject.SetActive(false);
            }
            EditorSceneManager.SaveScene(entrance);
            foreach (string name in new[] { "CityCenter", "OutdoorMarket", "SuburbToForest", "DemonCastle" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maps/" + name + ".unity");
                foreach (var point in UnityEngine.Object.FindObjectsByType<MedusaSavePoint>(FindObjectsSortMode.None))
                {
                    var renderer = point.GetComponent<SpriteRenderer>();
                    float bottom = renderer.bounds.min.y;
                    Undo.RecordObjects(new UnityEngine.Object[] { renderer, point.transform }, "Use requested Medusa Statue sprite");
                    renderer.sprite = medusa;
                    point.transform.position += Vector3.up * (bottom - renderer.bounds.min.y);
                    statues++;
                }
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        return "Assigned player-behind to gate and medusa Statue to " + statues + " save points.";
    }

    private static void SetPixelsPerUnit(string path, float units)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null || importer.textureType != TextureImporterType.Sprite)
            throw new InvalidOperationException("Expected imported sprite: " + path);
        importer.spritePixelsPerUnit = units;
        importer.SaveAndReimport();
    }
}
