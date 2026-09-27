using System;
using UnityEditor;
using UnityEngine;
using TheLastKnight.Stats;
using TheLastKnight.Player;

namespace TheLastKnight.Editor
{
    [CustomEditor(typeof(PlayerStats))]
    public class PlayerStatsEditor : UnityEditor.Editor
    {
        private bool _showMultipliers = true;
        private bool _showDerivedPreview = true;
        private UnityEditor.Editor _templateEditor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var playerStats = (PlayerStats)target;

            // 1. Template Reference
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Player Stats & Attribute System", EditorStyles.boldLabel);

            var templateProp = serializedObject.FindProperty("_statsTemplate");
            EditorGUILayout.PropertyField(templateProp, new GUIContent("Stats Template (SO)", "ScriptableObject containing all stat multipliers and tuning values."));

            var template = templateProp.objectReferenceValue as CharacterStatsSO;

            // 2. Embedded Multipliers Section (Tuneable directly in Inspector)
            if (template != null)
            {
                EditorGUILayout.Space(6);
                GUI.backgroundColor = new Color(0.85f, 0.95f, 1f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                EditorGUILayout.BeginHorizontal();
                _showMultipliers = EditorGUILayout.Foldout(_showMultipliers, "⚙️ Multipliers & Formulas (ปรับตัวคูณทั้งหมดตรงนี้)", true, EditorStyles.foldoutHeader);
                if (GUILayout.Button("🔍 Select Asset", EditorStyles.miniButton, GUILayout.Width(95)))
                {
                    Selection.activeObject = template;
                    EditorGUIUtility.PingObject(template);
                }
                EditorGUILayout.EndHorizontal();

                if (_showMultipliers)
                {
                    EditorGUILayout.HelpBox("ปรับค่าตัวคูณทุกตัวด้านล่างนี้ได้โดยตรง ระบบจะอัปเดตสถานะของ Player ทันที:", MessageType.Info);

                    if (_templateEditor == null || _templateEditor.target != template)
                    {
                        UnityEditor.Editor.CreateCachedEditor(template, null, ref _templateEditor);
                    }

                    if (_templateEditor != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        _templateEditor.OnInspectorGUI();
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorUtility.SetDirty(template);
                            AssetDatabase.SaveAssets();
                            playerStats.RecalculateStats();
                            EditorUtility.SetDirty(playerStats);
                        }
                    }
                }

                EditorGUILayout.EndVertical();

                // 3. Live Derived Preview Box
                EditorGUILayout.Space(6);
                GUI.backgroundColor = new Color(0.92f, 1f, 0.92f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                _showDerivedPreview = EditorGUILayout.Foldout(_showDerivedPreview, "📊 Live Derived Stats Preview (สถานะที่คำนวณสด)", true, EditorStyles.foldoutHeader);
                if (_showDerivedPreview)
                {
                    var ctrl = playerStats.GetComponent<PlayerController>();

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("⚔️ Physical ATK", $"{playerStats.AttackPower:F1} (Base {template.baseAttack} + STR×{template.attackPerSTR})");
                    EditorGUILayout.LabelField("🛡️ DEF", $"{playerStats.Defense:F1} (Base {template.baseDEF} + Lv.{playerStats.Level}×{template.defPerLevel})");
                    EditorGUILayout.LabelField("❤️ Max HP", $"{Mathf.CeilToInt(playerStats.MaxHP)} (VIT×{template.hpPerVIT})");
                    EditorGUILayout.LabelField("⚡ Max Stamina", $"{Mathf.CeilToInt(playerStats.MaxStamina)} (Base {template.baseStamina} + VIT_bonus×{template.staminaPerVIT})");
                    EditorGUILayout.LabelField("🎯 Crit Chance", $"{playerStats.CriticalChance:F2}% (Limit ~100%, {template.targetCritAtCap}% @ {template.dexLimitTarget} DEX)");
                    EditorGUILayout.LabelField("🗡️ Attack Speed", $"{playerStats.AttackSpeedMultiplier:F2}x (Base {template.baseAttackSpeed}x + AGI×{template.attackSpeedPerAGI})");

                    if (ctrl != null)
                    {
                        EditorGUILayout.LabelField("🏃 Movement Speed", $"{ctrl.MoveSpeed:F1} (Walk) / {ctrl.SprintSpeed:F1} (Sprint) ➔ ปรับที่ 'speedPerAGI' ค่าเดียว");
                        EditorGUILayout.LabelField("⚡ Dash Speed", $"{ctrl.DashSpeed:F2} (Base {ctrl.BaseDashSpeed:F1} + AGI×{template.dashSpeedPerAGI})");
                    }

                    string djStatus = playerStats.CanDoubleJump
                        ? "✅ Unlocked (ปลดล็อคแล้ว!)"
                        : $"🔒 Locked (ต้องการ AGI ≥ {template.doubleJumpAgiThreshold})";
                    EditorGUILayout.LabelField("🦘 Double Jump", djStatus);

                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(2);
                    if (GUILayout.Button("🔄 Recalculate Stats Now"))
                    {
                        playerStats.RecalculateStats();
                        EditorUtility.SetDirty(playerStats);
                    }
                }

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("⚠️ ยังไม่ได้กำหนด Stats Template กรุณาลาก PlayerStatsTemplate ใส่ในช่อง Stats Template เพื่อปรับตัวคูณ", MessageType.Warning);
            }

            // 4. Runtime Progression & Attributes
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Current Progression & Attributes", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_currentLevel"), new GUIContent("Level"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_currentEXP"), new GUIContent("EXP"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_availableStatPoints"), new GUIContent("Stat Points (SP)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Attribute Allocations", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_strength"), new GUIContent("Strength (STR)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_vitality"), new GUIContent("Vitality (VIT)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_dexterity"), new GUIContent("Dexterity (DEX)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_agility"), new GUIContent("Agility (AGI)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Runtime Resources", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_currentHP"), new GUIContent("Current HP"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_currentStamina"), new GUIContent("Current Stamina"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_gold"), new GUIContent("Gold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_healingPotions"), new GUIContent("Healing Potions"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats();
                EditorUtility.SetDirty(playerStats);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }

            // 5. Quick Test & Debug Controls (One-click testing during Play Mode)
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("⚡ Quick Test Controls (ปุ่มลัดสำหรับทดสอบสเตตัส)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⚡ Set AGI = 250\n(Double Jump + 1.96x Spd)", GUILayout.Height(36)))
            {
                serializedObject.FindProperty("_agility").intValue = 250;
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats(true);
                EditorUtility.SetDirty(playerStats);
            }
            if (GUILayout.Button("➕ +50 AGI\n(เพิ่มความเร็ว)", GUILayout.Height(36)))
            {
                serializedObject.FindProperty("_agility").intValue += 50;
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats(true);
                EditorUtility.SetDirty(playerStats);
            }
            if (GUILayout.Button("➕ +100 SP\n(เพิ่มแต้มอัป)", GUILayout.Height(36)))
            {
                serializedObject.FindProperty("_availableStatPoints").intValue += 100;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(playerStats);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎯 Set DEX = 250 (99% Crit)"))
            {
                serializedObject.FindProperty("_dexterity").intValue = 250;
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats(true);
                EditorUtility.SetDirty(playerStats);
            }
            if (GUILayout.Button("🛡️ +1 Level (DEF +1)"))
            {
                serializedObject.FindProperty("_currentLevel").intValue += 1;
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats(true);
                EditorUtility.SetDirty(playerStats);
            }
            if (GUILayout.Button("🔄 Reset All (Base 10)"))
            {
                serializedObject.FindProperty("_strength").intValue = 10;
                serializedObject.FindProperty("_vitality").intValue = 10;
                serializedObject.FindProperty("_dexterity").intValue = 10;
                serializedObject.FindProperty("_agility").intValue = 10;
                serializedObject.FindProperty("_currentLevel").intValue = 1;
                serializedObject.ApplyModifiedProperties();
                playerStats.RecalculateStats(true);
                EditorUtility.SetDirty(playerStats);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }
}
