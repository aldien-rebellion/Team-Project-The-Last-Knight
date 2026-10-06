using UnityEditor;
using TheLastKnight.Combat;

namespace TheLastKnight.EditorTools
{
    [CustomEditor(typeof(EnemyStats)), CanEditMultipleObjects]
    public class EnemyStatsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rewards rolled on every kill", EditorStyles.boldLabel);
            foreach (var selected in targets)
            {
                var enemy = (EnemyStats)selected;
                long level = System.Math.Max(1, enemy.Level);
                EditorGUILayout.LabelField(selected.name + " EXP", $"{level * 20:N0} – {level * 80:N0}");
                EditorGUILayout.LabelField(selected.name + " Gold", $"{level * 5:N0} – {level * 10:N0}");
            }
            EditorGUILayout.HelpBox("Inclusive ranges use this scene instance's Level. Each death rolls again; consecutive rolls can match. EXP is granted without a level-difference penalty.", MessageType.Info);
        }
    }
}
