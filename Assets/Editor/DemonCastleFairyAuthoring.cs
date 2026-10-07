using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TheLastKnight.Environment;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;

namespace TheLastKnight.EditorTools.FairyAuthoring
{
    internal class CommandScript : IRunCommand
    {
        internal bool Setup;
        public void Execute(ExecutionResult result)
        {
            if (Setup) { DemonCastleFairyAuthoring.Configure(result); return; }
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None)
                .Where(c => c.name.StartsWith("F3_GrandHall")))
                result.Log(c.name + " min=" + c.bounds.min + " max=" + c.bounds.max);
            var p = Object.FindAnyObjectByType<TheLastKnight.Player.PlayerController>();
            if (p != null) result.Log("Player " + p.transform.position + " bounds=" + p.GetComponent<Collider2D>().bounds);
            for (int i = 1; i <= 3; i++)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/sprites/Monsters/Fairy/Fairy " + i + ".png").OfType<Sprite>().ToArray();
                result.Log("Fairy " + i + ": " + sprites.Length + " sprites; size=" + sprites[0].bounds.size);
            }
        }
    }

    public static class DemonCastleFairyAuthoring
    {
        [MenuItem("The Last Knight/Fairies/Configure Demon Castle Lift")]
        public static void Setup() => new CommandScript { Setup = true }.Execute(new ExecutionResult("Configure fairy lift"));

        internal static void Configure(ExecutionResult result)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.name != "DemonCastle") throw new System.InvalidOperationException("Open DemonCastle in Edit Mode first.");
            var colliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            var source = colliders.Single(c => c.name == "F3_GrandHall_UpperShelf (3)");
            var left = colliders.Single(c => c.name == "F3_GrandHall_MidShelf (1)");
            var right = colliders.Single(c => c.name == "F3_GrandHall_UpperMezzanine (1)");
            var lift = Object.FindAnyObjectByType<DemonCastleFairyLift>();
            if (lift == null)
            {
                var go = new GameObject("DemonCastle Fairy Lift");
                result.RegisterObjectCreation(go);
                lift = go.AddComponent<DemonCastleFairyLift>();
            }
            result.RegisterObjectModification(lift);
            var serialized = new SerializedObject(lift);
            serialized.FindProperty("_sourceShelf").objectReferenceValue = source;
            serialized.FindProperty("_leftLanding").objectReferenceValue = left;
            serialized.FindProperty("_rightLanding").objectReferenceValue = right;
            serialized.FindProperty("_fairyAnimation").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Enemies/Fairy/FairyController.controller");
            var frames = serialized.FindProperty("_firstFrames");
            frames.arraySize = 3;
            for (int i = 0; i < 3; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/sprites/Monsters/Fairy/Fairy " + (i + 1) + ".png").OfType<Sprite>().OrderBy(s => s.name).First();
            serialized.FindProperty("_dustSprite").objectReferenceValue = CreateDust(result);
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            result.Log("Configured source, both corner landings, three animated fairy colours and pixie dust; saved DemonCastle.");
        }

        private static Sprite CreateDust(ExecutionResult result)
        {
            const string path = "Assets/sprites/Monsters/Fairy/FairyPixieDust.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            result.RegisterObjectCreation(texture);
            var pixels = new Color[256];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = Mathf.Abs(x - 7.5f), dy = Mathf.Abs(y - 7.5f);
                    if ((dx < 1f && dy < 7f) || (dy < 1f && dx < 7f) || dx + dy < 4f)
                        pixels[y * 16 + x] = Color.white;
                }
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            result.DestroyObject(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            result.RegisterObjectModification(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static string Inspect()
        {
            var result = new ExecutionResult("Demon Castle fairy inspection");
            new CommandScript().Execute(result);
            return string.Join("\n", result.GetFormattedLogs());
        }
    }
}
