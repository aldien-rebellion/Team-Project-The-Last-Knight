using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Audio;
using TheLastKnight.Combat;

public static class PlanSupportAssets
{
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new System.InvalidOperationException("Save current scene changes first.");
        const string catalogPath = "Assets/Resources/AudioCatalog.asset";
        if (AssetDatabase.LoadAssetAtPath<AudioCatalog>(catalogPath) == null)
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.clips = new[] { "Town", "Forest", "Church", "Castle", "Boss", "slash", "jump", "dash", "drink", "skill", "excalibur", "hurt", "enemy_hurt", "enemy_death", "parry", "click", "rune" }
                .Select(id => new AudioCatalog.Entry { id = id }).ToArray();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }
        const string popupPath = "Assets/Resources/FloatingCombatText.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(popupPath) == null)
        {
            var popup = new GameObject("FloatingCombatText", typeof(TextMesh), typeof(FloatingCombatText));
            try
            {
                var text = popup.GetComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 48; text.characterSize = 0.06f; text.anchor = TextAnchor.MiddleCenter;
                var renderer = popup.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = text.font.material;
                renderer.sortingLayerName = "InGame_UI"; renderer.sortingOrder = 100;
                PrefabUtility.SaveAsPrefabAsset(popup, popupPath);
            }
            finally { Object.DestroyImmediate(popup); }
        }
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var camera = Object.FindAnyObjectByType<Camera>();
            if (camera.GetComponent<AudioListener>() == null) Undo.AddComponent<AudioListener>(camera.gameObject);
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        return "Created audio assignment catalog and floating-text prefab; ensured menu AudioListener.";
    }
}
