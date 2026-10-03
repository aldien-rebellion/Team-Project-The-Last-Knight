using System;
using System.Linq;
using TheLastKnight.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small scene tool for selecting and positioning enemy instances.</summary>
public sealed class MonsterPlacementWindow : EditorWindow
{
    private EnemyController[] _monsters = Array.Empty<EnemyController>();
    private Vector2 _scrollPosition;
    private string _search = string.Empty;

    [MenuItem("Tools/Monster Placement")]
    private static void Open()
    {
        var window = GetWindow<MonsterPlacementWindow>();
        window.titleContent = new GUIContent("Monster Placement");
        window.minSize = new Vector2(360f, 260f);
        window.Refresh();
    }

    private void OnEnable()
    {
        EditorApplication.hierarchyChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Refresh;
    }

    private void OnGUI()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        EditorGUILayout.LabelField("Active Scene", activeScene.IsValid() ? activeScene.name : "None",
            EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _search = EditorGUILayout.TextField("Filter", _search);
            if (GUILayout.Button("Refresh", GUILayout.Width(70f))) Refresh();
        }

        EnemyController[] visibleMonsters = _monsters
            .Where(monster => monster != null &&
                (string.IsNullOrWhiteSpace(_search) ||
                 monster.gameObject.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToArray();

        EditorGUILayout.LabelField($"Monsters in active scene: {visibleMonsters.Length}");
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Stop Play Mode before moving monsters so the positions can be saved to the scene.",
                MessageType.Info);
        }
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        foreach (EnemyController monster in visibleMonsters)
        {
            DrawMonster(monster);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMonster(EnemyController monster)
    {
        Transform monsterTransform = monster.transform;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(monster.gameObject.name, EditorStyles.boldLabel);
            if (GUILayout.Button("Select", GUILayout.Width(65f)))
            {
                Selection.activeGameObject = monster.gameObject;
                EditorGUIUtility.PingObject(monster.gameObject);
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = EditorGUILayout.Vector3Field("World Position", monsterTransform.position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(monsterTransform, "Move Monster");
                monsterTransform.position = newPosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(monsterTransform);
                EditorUtility.SetDirty(monsterTransform);
                EditorSceneManager.MarkSceneDirty(monster.gameObject.scene);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void Refresh()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        _monsters = activeScene.IsValid()
            ? FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(monster => monster != null && monster.gameObject.scene == activeScene)
                .OrderBy(monster => monster.transform.position.x)
                .ThenBy(monster => monster.gameObject.name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<EnemyController>();

        Repaint();
    }
}
