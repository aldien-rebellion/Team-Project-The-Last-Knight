using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheLastKnight.Environment;

namespace TheLastKnight.Editor
{
    [CustomEditor(typeof(TeleportDoor))]
    public class TeleportDoorEditor : UnityEditor.Editor
    {
        private bool _showAdvanced = false;

        public override void OnInspectorGUI()
        {
            var door = (TeleportDoor)target;

            // Auto-sync connections across all scene doors
            TeleportDoor.SyncAllDoorConnections();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🚪 Anywhere Door (Local Teleport)", EditorStyles.boldLabel);

            // 1. Door Identity
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorId"), new GUIContent("ชื่อประตูนี้ (ID)"));
            if (GUILayout.Button("Auto ID", GUILayout.Width(75), GUILayout.Height(20)))
            {
                AutoAssignDoorId(door);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_isInvisible"), new GUIContent("ล่องหนในเกม (Is Invisible)"));
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // 2. CUSTOM DESTINATIONS LIST (User can write targets and button text)
            DrawCustomDestinationsSection(door);

            // 3. Status Box
            DrawConnectionStatus(door);

            EditorGUILayout.Space(5);

            // 4. Advanced Settings (Folded by default)
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "⚙️ การตั้งค่าเพิ่มเติม (Spawn Offset / UI / Keys)", true);
            if (_showAdvanced)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnOffset"), new GUIContent("Spawn Offset (ระยะจุดเกิด)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_interactKey"), new GUIContent("ปุ่มโต้ตอบ"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_promptUI"), new GUIContent("Prompt UI"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_selectionMenuUI"), new GUIContent("Selection Menu UI"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetRoom"), new GUIContent("Target Room (Confiner)"));
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomDestinationsSection(TeleportDoor door)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📋 ตัวเลือกจุดหมายปลายทาง (เขียนชื่อเองได้ตามต้องการ):", EditorStyles.boldLabel);

            string myId = !string.IsNullOrEmpty(door.doorId) ? door.doorId : door.gameObject.name;

            if (door.customDestinations == null)
            {
                door.customDestinations = new List<DoorDestination>();
            }

            // Purge Map_1 and self
            door.customDestinations.RemoveAll(d => 
                d == null || 
                string.Equals(d.targetDoorId, "Map_1", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(d.targetDoorId, myId, StringComparison.OrdinalIgnoreCase)
            );

            // Render each custom destination
            for (int i = 0; i < door.customDestinations.Count; i++)
            {
                var dest = door.customDestinations[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"ตัวเลือกที่ {i + 1}:", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("✕ ลบ", GUILayout.Width(50), GUILayout.Height(18)))
                {
                    Undo.RecordObject(door, "Remove Destination");
                    string removedTargetId = dest.targetDoorId;
                    door.customDestinations.RemoveAt(i);
                    EditorUtility.SetDirty(door);

                    if (!string.IsNullOrEmpty(removedTargetId))
                    {
                        var otherDoor = TeleportDoor.FindLocalDoor(removedTargetId);
                        if (otherDoor != null)
                        {
                            Undo.RecordObject(otherDoor, "Remove Bidirectional Destination");
                            otherDoor.customDestinations.RemoveAll(x => string.Equals(x.targetDoorId, myId, StringComparison.OrdinalIgnoreCase));
                            EditorUtility.SetDirty(otherDoor);
                        }
                    }

                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                string newTargetId = EditorGUILayout.TextField("ID ประตูปลายทาง:", dest.targetDoorId);
                string newDisplayName = EditorGUILayout.TextField("ข้อความบนปุ่ม:", dest.displayName);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(door, "Modify Destination");
                    dest.targetDoorId = newTargetId;
                    dest.displayName = newDisplayName;
                    EditorUtility.SetDirty(door);
                    TeleportDoor.SyncAllDoorConnections();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.Space(5);

            // Button: Add New Custom Option
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button("+ เพิ่มตัวเลือกจุดหมายใหม่", GUILayout.Height(26)))
            {
                Undo.RecordObject(door, "Add Custom Destination");
                door.customDestinations.Add(new DoorDestination("", ""));
                EditorUtility.SetDirty(door);
            }

            GUI.backgroundColor = new Color(0.8f, 0.9f, 0.8f);
            if (GUILayout.Button("🔄 ซิงค์ประตูทั้งฉาก", GUILayout.Width(130), GUILayout.Height(26)))
            {
                TeleportDoor.SyncAllDoorConnections();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Quick Shortcut Buttons for other doors found in scene
            var allSceneDoors = UnityEngine.Object.FindObjectsByType<TeleportDoor>(FindObjectsInactive.Include);
            var quickAddList = new List<string>();

            foreach (var d in allSceneDoors)
            {
                if (d == door) continue;
                string dId = !string.IsNullOrEmpty(d.doorId) ? d.doorId : d.gameObject.name;
                if (string.Equals(dId, myId, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(dId, "Map_1", StringComparison.OrdinalIgnoreCase)) continue;

                bool alreadyIn = false;
                foreach (var cd in door.customDestinations)
                {
                    if (string.Equals(cd.targetDoorId, dId, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyIn = true;
                        break;
                    }
                }

                if (!alreadyIn)
                {
                    quickAddList.Add(dId);
                }
            }

            if (quickAddList.Count > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("⚡ ทางลัดดึงประตูในฉากมาใส่ (จะเชื่อมกัน 2 ฝั่งอัตโนมัติ):", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                foreach (var qId in quickAddList)
                {
                    if (GUILayout.Button($"+ {qId}"))
                    {
                        Undo.RecordObject(door, "Quick Add Destination");
                        door.customDestinations.Add(new DoorDestination(qId, qId));
                        EditorUtility.SetDirty(door);

                        // Also add reverse to target door
                        var otherDoor = TeleportDoor.FindLocalDoor(qId);
                        if (otherDoor != null)
                        {
                            Undo.RecordObject(otherDoor, "Quick Add Destination Reverse");
                            if (!otherDoor.customDestinations.Exists(x => string.Equals(x.targetDoorId, myId, StringComparison.OrdinalIgnoreCase)))
                            {
                                otherDoor.customDestinations.Add(new DoorDestination(myId, myId));
                            }
                            EditorUtility.SetDirty(otherDoor);
                        }

                        TeleportDoor.SyncAllDoorConnections();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConnectionStatus(TeleportDoor door)
        {
            var formatted = door.GetFormattedDestinations();
            if (formatted.Count == 0)
            {
                EditorGUILayout.HelpBox("⚠️ ยังไม่มีจุดหมายปลายทาง (กดปุ่ม '+ เพิ่มตัวเลือกจุดหมายใหม่' ด้านบน)", MessageType.Warning);
            }
            else if (formatted.Count == 1)
            {
                EditorGUILayout.HelpBox($"⚡ มีจุดหมาย 1 แห่ง: '{formatted[0].GetLabel()}' (ID: {formatted[0].targetDoorId})\nเมื่อกด F จะวาร์ปไปทันที (ไม่ถาม)", MessageType.Info);
            }
            else
            {
                string summary = "";
                for (int i = 0; i < formatted.Count; i++)
                {
                    summary += $"\n  [{i + 1}] {formatted[i].GetLabel()} -> ID: {formatted[i].targetDoorId}";
                }
                EditorGUILayout.HelpBox($"📋 มี {formatted.Count} จุดหมาย (เมื่อกด F จะเปิดเมนู UI แสดงตามชื่อที่คุณเขียน):{summary}", MessageType.Info);
            }
        }

        private void AutoAssignDoorId(TeleportDoor door)
        {
            if (door == null) return;
            string sceneName = door.gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName)) return;

            var allDoors = UnityEngine.Object.FindObjectsByType<TeleportDoor>(FindObjectsInactive.Include);
            int maxIndex = 0;
            foreach (var d in allDoors)
            {
                if (d == null || d == door) continue;
                string checkId = !string.IsNullOrEmpty(d.doorId) ? d.doorId : d.gameObject.name;
                if (checkId.StartsWith(sceneName + "_"))
                {
                    string numStr = checkId.Substring((sceneName + "_").Length);
                    if (int.TryParse(numStr, out int num) && num > maxIndex)
                    {
                        maxIndex = num;
                    }
                }
            }

            Undo.RecordObject(door, "Auto Assign Door ID");
            string newId = $"{sceneName}_{maxIndex + 1}";
            door.doorId = newId;
            door.gameObject.name = newId;
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(door.gameObject);
        }
    }
}
