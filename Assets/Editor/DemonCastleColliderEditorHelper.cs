using UnityEditor;
using UnityEngine;
using UnityEditor.EditorTools;

namespace TheLastKnight.Editor
{
    [InitializeOnLoad]
    public static class DemonCastleColliderEditorHelper
    {
        private const string PrefKeyIsOpen = "DemonCastle_Helper_IsOpen";

        public static bool IsOpen
        {
            get => EditorPrefs.GetBool(PrefKeyIsOpen, true);
            set => EditorPrefs.SetBool(PrefKeyIsOpen, value);
        }

        public static bool ShowAllColliders = false;
        public static bool ShowLabels = false;
        public static bool EnableDirectClick = true;

        private static readonly Color WireColor = new Color(0.1f, 1f, 0.4f, 0.5f);
        private static readonly Color FillColor = new Color(0.1f, 1f, 0.4f, 0.05f);
        private static readonly Color SelectedFillColor = new Color(1f, 0.85f, 0.1f, 0.2f);
        private static readonly Color SelectedWireColor = new Color(1f, 0.85f, 0.1f, 0.95f);

        private static readonly string[] RootsToLock = new string[]
        {
            "void",
            "outside background",
            "outside background (1)",
            "outside background (2)",
            "outside background (3)",
            "outside background (4)",
            "PlatformerSet1_Scenes",
            "RaouDungeon_Scenes",
            "fix art that ai make 1st floor",
            "fix art that ai make  2nd 3rd floor",
            "DemonCastle_Interior",
            "DemonCastle_Lighting"
        };

        static DemonCastleColliderEditorHelper()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Tools.hidden = false;
        }

        [MenuItem("Tools/Demon Castle/🛠️ Toggle Collider Tool Window _F4", false, 1)]
        public static void ToggleToolWindow()
        {
            IsOpen = !IsOpen;
            Debug.Log($"[DemonCastle] Collider Tool Window is now {(IsOpen ? "ENABLED" : "DISABLED")}.");
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Demon Castle/🛠️ Toggle Collider Tool Window _F4", true)]
        public static bool ToggleToolWindowValidate()
        {
            Menu.SetChecked("Tools/Demon Castle/🛠️ Toggle Collider Tool Window _F4", IsOpen);
            return true;
        }

        [MenuItem("Tools/Demon Castle/🔒 Lock Backgrounds (Click Colliders Only) _F2", false, 10)]
        public static void LockBackgrounds()
        {
            var svm = SceneVisibilityManager.instance;
            var bgRoot = GameObject.Find("DemonCastle_Background");
            if (bgRoot != null)
            {
                svm.DisablePicking(bgRoot, true);
            }

            var lightRoot = GameObject.Find("DemonCastle_Lighting");
            if (lightRoot != null)
            {
                svm.DisablePicking(lightRoot, true);
            }

            int count = 0;
            foreach (var name in RootsToLock)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    svm.DisablePicking(go, true);
                    count++;
                }
            }

            var colRoot = GameObject.Find("DemonCastle_Colliders");
            if (colRoot != null)
            {
                svm.EnablePicking(colRoot, true);
            }

            Debug.Log("[DemonCastle] Locked picking for background/art objects. Colliders are now directly selectable in Scene View!");
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Demon Castle/🔓 Unlock All Backgrounds _F3", false, 11)]
        public static void UnlockAll()
        {
            SceneVisibilityManager.instance.EnableAllPicking();
            Debug.Log("[DemonCastle] All objects are now pickable.");
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Demon Castle/👁️ Select All Colliders", false, 20)]
        public static void SelectCollidersRoot()
        {
            var colRoot = GameObject.Find("DemonCastle_Colliders");
            if (colRoot != null)
            {
                Selection.activeGameObject = colRoot;
                EditorGUIUtility.PingObject(colRoot);
            }
        }

        public static void ActivateMoveTool()
        {
            Tools.current = Tool.Move;
            SceneView.RepaintAll();
        }

        public static void ActivateColliderTool()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            System.Type toolType = null;
            if (go.GetComponent<BoxCollider2D>() != null)
            {
                toolType = System.Type.GetType("UnityEditor.BoxCollider2DTool, UnityEditor");
            }
            else if (go.GetComponent<EdgeCollider2D>() != null)
            {
                toolType = System.Type.GetType("UnityEditor.EdgeCollider2DTool, UnityEditor");
            }

            if (toolType != null)
            {
                var method = typeof(ToolManager).GetMethod("SetActiveTool", new System.Type[] { typeof(System.Type) });
                method?.Invoke(null, new object[] { toolType });
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // If closed by user, do not render or intercept clicks
            if (!IsOpen) return;

            // ALWAYS keep Unity native tools visible
            if (Tools.hidden)
            {
                Tools.hidden = false;
            }

            var colRoot = GameObject.Find("DemonCastle_Colliders");
            if (colRoot == null) return;

            var colliders = colRoot.GetComponentsInChildren<Collider2D>();
            if (colliders == null || colliders.Length == 0) return;

            GameObject selectedGo = Selection.activeGameObject;
            Event e = Event.current;

            Vector2 mousePos = e.mousePosition;
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePos);
            Vector2 mouseWorld = mouseRay.origin;

            // =========================================================================
            // 1. DIRECT CLICK SELECTION (ONLY on other unselected colliders)
            // =========================================================================
            if (EnableDirectClick && e.type == EventType.MouseDown && e.button == 0 && !e.alt && GUIUtility.hotControl == 0)
            {
                // Check if user clicked inside currently selected collider
                bool clickedCurrent = false;
                if (selectedGo != null && selectedGo.transform.IsChildOf(colRoot.transform))
                {
                    var curCol = selectedGo.GetComponent<Collider2D>();
                    if (curCol != null && curCol.OverlapPoint(mouseWorld))
                    {
                        clickedCurrent = true;
                    }
                }

                // If user did NOT click inside current collider, look for another collider to select
                if (!clickedCurrent)
                {
                    Collider2D bestHit = null;
                    float minDist = float.MaxValue;

                    foreach (var col in colliders)
                    {
                        if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                        if (col.OverlapPoint(mouseWorld))
                        {
                            bestHit = col;
                            break;
                        }

                        float dist = Vector2.Distance(mouseWorld, col.bounds.ClosestPoint(mouseWorld));
                        if (dist < 0.4f && dist < minDist)
                        {
                            minDist = dist;
                            bestHit = col;
                        }
                    }

                    if (bestHit != null && bestHit.gameObject != selectedGo)
                    {
                        Selection.activeGameObject = bestHit.gameObject;
                        EditorGUIUtility.PingObject(bestHit.gameObject);
                        e.Use();
                        SceneView.RepaintAll();
                        return;
                    }
                }
            }

            // =========================================================================
            // 2. DRAW COLLIDERS (Accurately scaled with TransformPoint, no double outline)
            // =========================================================================
            foreach (var col in colliders)
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

                bool isSelected = (selectedGo == col.gameObject);
                if (!isSelected && !ShowAllColliders) continue;

                if (col is BoxCollider2D box)
                {
                    Vector2 o = box.offset;
                    Vector2 s = box.size * 0.5f;

                    // Accurate corner points taking all parent lossyScale and rotation into account
                    Vector3[] corners = new Vector3[]
                    {
                        box.transform.TransformPoint(new Vector3(o.x - s.x, o.y - s.y, 0f)),
                        box.transform.TransformPoint(new Vector3(o.x - s.x, o.y + s.y, 0f)),
                        box.transform.TransformPoint(new Vector3(o.x + s.x, o.y + s.y, 0f)),
                        box.transform.TransformPoint(new Vector3(o.x + s.x, o.y - s.y, 0f))
                    };

                    // When selected, do NOT draw a duplicate wire outline over Unity's native gizmo!
                    // Only draw subtle fill so it never shows a double square.
                    Color fill = isSelected ? SelectedFillColor : FillColor;
                    Color wire = isSelected ? Color.clear : WireColor;

                    Handles.DrawSolidRectangleWithOutline(corners, fill, wire);

                    if (ShowLabels && (isSelected || ShowAllColliders))
                    {
                        Handles.Label(corners[1] + Vector3.up * 0.25f, col.gameObject.name);
                    }
                }
                else if (col is EdgeCollider2D edge)
                {
                    Handles.color = isSelected ? SelectedWireColor : WireColor;
                    var pts = edge.points;
                    if (pts != null && pts.Length > 1)
                    {
                        for (int i = 0; i < pts.Length - 1; i++)
                        {
                            Vector3 p1 = edge.transform.TransformPoint(pts[i] + edge.offset);
                            Vector3 p2 = edge.transform.TransformPoint(pts[i + 1] + edge.offset);
                            Handles.DrawLine(p1, p2, isSelected ? 2.5f : 1.5f);
                        }
                    }
                }
            }

            // =========================================================================
            // 3. FLOATING SCENEVIEW TOOLBAR (Upper Right with Close 'X' Button)
            // =========================================================================
            DrawSceneViewOverlay(selectedGo, colRoot, sceneView);
        }

        private static void DrawSceneViewOverlay(GameObject selectedGo, GameObject colRoot, SceneView sceneView)
        {
            Handles.BeginGUI();
            float xPos = Mathf.Max(60f, sceneView.position.width - 295f);
            GUILayout.BeginArea(new Rect(xPos, 12, 285, 215), GUI.skin.window);

            // Title Bar with [X] Close Button
            GUILayout.BeginHorizontal();
            GUILayout.Label("Demon Castle Collider Tool", EditorStyles.boldLabel);
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
            {
                IsOpen = false;
                SceneView.RepaintAll();
                GUIUtility.ExitGUI();
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔒 Lock Art (F2)", GUILayout.Height(24)))
            {
                LockBackgrounds();
            }
            if (GUILayout.Button("🔓 Unlock (F3)", GUILayout.Height(24)))
            {
                UnlockAll();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ShowAllColliders = GUILayout.Toggle(ShowAllColliders, "Show All Colliders", GUILayout.Width(130));
            ShowLabels = GUILayout.Toggle(ShowLabels, "Show Labels", GUILayout.Width(110));
            GUILayout.EndHorizontal();

            EnableDirectClick = GUILayout.Toggle(EnableDirectClick, "Direct Click Selection");

            GUILayout.Space(4);
            if (selectedGo != null && selectedGo.transform.IsChildOf(colRoot.transform) && selectedGo != colRoot)
            {
                var box = selectedGo.GetComponent<BoxCollider2D>();
                var edge = selectedGo.GetComponent<EdgeCollider2D>();

                GUI.color = Color.yellow;
                GUILayout.Label($"Selected: {selectedGo.name}", EditorStyles.boldLabel);
                GUI.color = Color.white;

                if (box != null)
                {
                    float scaleX = selectedGo.transform.lossyScale.x;
                    float scaleY = selectedGo.transform.lossyScale.y;
                    GUILayout.Label($"Size: ({box.size.x:F1} x {box.size.y:F1}) | World: ({(box.size.x * scaleX):F1} x {(box.size.y * scaleY):F1})");
                    GUILayout.Label($"World Pos: ({selectedGo.transform.position.x:F2}, {selectedGo.transform.position.y:F2})");
                }
                else if (edge != null)
                {
                    GUILayout.Label($"Edge Points: {edge.points.Length} | Pos: ({selectedGo.transform.position.x:F2}, {selectedGo.transform.position.y:F2})");
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("✥ Move (W)", GUILayout.Height(26)))
                {
                    ActivateMoveTool();
                }
                if (GUILayout.Button("⬜ Edit Collider", GUILayout.Height(26)))
                {
                    ActivateColliderTool();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("💡 กด W ย้ายตำแหน่ง | กด Edit Collider ปรับทีละด้าน", EditorStyles.miniLabel);
            }
            else
            {
                GUI.color = Color.gray;
                GUILayout.Label("คลิก Collider ชิ้นใดก็ได้บนจอเพื่อปรับแต่ง", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
