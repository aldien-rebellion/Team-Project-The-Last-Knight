using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.EditorTools
{
    public class MonsterPreviewWindow : EditorWindow
    {
        private string[] monsterFolders = new string[0];
        private int selectedMonsterIndex = 0;
        private string[] currentClips = new string[0];
        private int selectedClipIndex = 0;

        [MenuItem("Tools/Monster Animation Previewer")]
        public static void ShowWindow()
        {
            var win = GetWindow<MonsterPreviewWindow>("Monster Previewer");
            win.minSize = new Vector2(350, 250);
            win.LoadMonsters();
        }

        private void OnEnable()
        {
            LoadMonsters();
        }

        private void OnFocus()
        {
            LoadMonsters();
        }

        private void LoadMonsters()
        {
            string baseDir = "Assets/Animations/Enemies";
            if (!Directory.Exists(baseDir)) return;

            var list = new System.Collections.Generic.List<string>();
            foreach (var d in Directory.GetDirectories(baseDir))
            {
                string name = Path.GetFileName(d);
                if (name == "Projectiles")
                {
                    foreach (var pd in Directory.GetDirectories(d))
                    {
                        list.Add("Projectiles/" + Path.GetFileName(pd));
                    }
                }
                else
                {
                    list.Add(name);
                }
            }
            monsterFolders = list.OrderBy(x => x).ToArray();
            if (selectedMonsterIndex >= monsterFolders.Length) selectedMonsterIndex = 0;
            LoadClipsForCurrentMonster();
        }

        private void LoadClipsForCurrentMonster()
        {
            if (monsterFolders.Length == 0 || selectedMonsterIndex >= monsterFolders.Length)
            {
                currentClips = new string[0];
                return;
            }

            string path = $"Assets/Animations/Enemies/{monsterFolders[selectedMonsterIndex]}";
            var files = Directory.GetFiles(path, "*.anim");
            currentClips = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
            selectedClipIndex = 0;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Monster Animation Previewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Select a monster to automatically spawn or update the Monster_Preview GameObject in the scene with its Animator Controller.", MessageType.Info);
            EditorGUILayout.Space(5);

            if (monsterFolders.Length == 0)
            {
                EditorGUILayout.HelpBox("No monster folders found in Assets/Animations/Enemies.", MessageType.Warning);
                if (GUILayout.Button("Refresh")) LoadMonsters();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            selectedMonsterIndex = EditorGUILayout.Popup("Monster / Entity", selectedMonsterIndex, monsterFolders);
            if (EditorGUI.EndChangeCheck())
            {
                LoadClipsForCurrentMonster();
                UpdateSceneMonster();
            }
            if (GUILayout.Button("↻", GUILayout.Width(25)))
            {
                LoadMonsters();
            }
            EditorGUILayout.EndHorizontal();

            if (currentClips.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                selectedClipIndex = EditorGUILayout.Popup("Animation Clip", selectedClipIndex, currentClips);
                if (EditorGUI.EndChangeCheck())
                {
                    SampleSelectedClip();
                }
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Spawn / Select in Scene", GUILayout.Height(32)))
            {
                UpdateSceneMonster();
            }

            if (GUILayout.Button("Preview Clip (Frame 0)", GUILayout.Height(24)))
            {
                SampleSelectedClip();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Instructions:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. Click 'Spawn / Select in Scene' above.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("2. In the bottom 'Animation' window, select the clip to play.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("3. Press Space or Play (▶) to view the animation live in Scene.", EditorStyles.wordWrappedLabel);
        }

        private void UpdateSceneMonster()
        {
            if (monsterFolders.Length == 0 || selectedMonsterIndex >= monsterFolders.Length) return;

            string mName = monsterFolders[selectedMonsterIndex];
            string folderPath = $"Assets/Animations/Enemies/{mName}";

            var go = GameObject.Find("Monster_Preview");
            if (go == null)
            {
                go = new GameObject("Monster_Preview");
                go.transform.position = new Vector3(0, -1.5f, 0);
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();

            var ctrlFiles = Directory.GetFiles(folderPath, "*.controller");
            if (ctrlFiles.Length > 0)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlFiles[0].Replace("\\", "/"));
                anim.runtimeAnimatorController = ctrl;
            }

            SampleSelectedClip();

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private void SampleSelectedClip()
        {
            if (monsterFolders.Length == 0 || selectedMonsterIndex >= monsterFolders.Length) return;
            if (currentClips.Length == 0 || selectedClipIndex >= currentClips.Length) return;

            var go = GameObject.Find("Monster_Preview");
            if (go == null) return;

            string mName = monsterFolders[selectedMonsterIndex];
            string clipPath = $"Assets/Animations/Enemies/{mName}/{currentClips[selectedClipIndex]}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null)
            {
                clip.SampleAnimation(go, 0f);
                EditorUtility.SetDirty(go);
            }
        }
    }
}
