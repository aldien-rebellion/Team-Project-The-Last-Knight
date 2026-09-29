using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheLastKnight.Inventory;

public static class ItemAuthoring
{
    public const string Definitions = "Assets/Resources/Items/Definitions";
    public const string Icons = "Assets/Resources/CharacterStatus/Items";

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    [MenuItem("The Last Knight/Items/Create New Item")]
    public static void CreateItem()
    {
        EnsureFolder(Definitions);
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var path = AssetDatabase.GenerateUniqueAssetPath(Definitions + "/new_item.asset");
        item.id = System.IO.Path.GetFileNameWithoutExtension(path).Replace(' ', '_');
        item.displayName = "New Item";
        AssetDatabase.CreateAsset(item, path);
        Undo.RegisterCreatedObjectUndo(item, "Create item");
        Selection.activeObject = item;
        EditorGUIUtility.PingObject(item);
        ItemRegistry.ReloadDefinitions();
    }

    [MenuItem("The Last Knight/Items/Create Missing Legacy Definitions")]
    public static void CreateLegacyDefinitions()
    {
        EnsureFolder(Definitions);
        foreach (var folder in new[] { "Consumables", "Equipment", "Materials" })
            EnsureFolder(Icons + "/" + folder);
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { Definitions }))
        {
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.id != null) existing.Add(definition.id);
        }
        int created = 0;
        foreach (var id in ItemRegistry.LegacyIds)
        {
            if (existing.Contains(id)) continue;
            var source = ItemRegistry.CreateLegacyItem(id);
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.id = id;
            item.displayName = source.name;
            item.typeName = source.typeName;
            item.description = source.description;
            item.icon = source.Icon;
            item.category = source.category;
            item.maxStack = source.maxStack;
            item.isConsumable = source.isConsumable;
            item.useEffect = ItemUseEffect.Legacy;
            string folder = source.category == ItemCategory.Consumable ? "Consumables" :
                source.category == ItemCategory.Material ? "Materials" : "Equipment";
            EnsureFolder(Definitions + "/" + folder);
            AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath(Definitions + "/" + folder + "/" + id + ".asset"));
            Undo.RegisterCreatedObjectUndo(item, "Create legacy definition");
            created++;
        }
        AssetDatabase.SaveAssets();
        ItemRegistry.ReloadDefinitions();
        Debug.Log($"Created {created} item definitions. Existing definitions were preserved.");
    }

    [MenuItem("The Last Knight/Items/Validate Items")]
    public static void ValidateItems()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int issues = 0, count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            count++;
            var errors = new List<string>();
            if (!path.StartsWith(Definitions + "/", StringComparison.Ordinal)) errors.Add("Move this asset into " + Definitions);
            if (string.IsNullOrWhiteSpace(item.id) || item.id.Trim() != item.id) errors.Add("Set a non-empty ID without leading/trailing spaces");
            else if (!ids.Add(item.id)) errors.Add("Duplicate ID: " + item.id);
            if (string.IsNullOrWhiteSpace(item.displayName)) errors.Add("Set Display Name");
            if (item.icon == null) errors.Add("Assign a Sprite to Icon");
            if (item.maxStack < 1) errors.Add("Max Stack must be at least 1");
            if (item.useEffect == ItemUseEffect.Legacy && ItemRegistry.CreateLegacyItem(item.id) == null) errors.Add("Legacy requires an existing built-in ID");
            if (item.isConsumable && (item.useEffect == ItemUseEffect.None ||
                (item.useEffect == ItemUseEffect.Legacy && ItemRegistry.CreateLegacyItem(item.id)?.onUse == null)))
                errors.Add("No use effect implemented; this item cannot be consumed yet");
            if (errors.Count == 0) continue;
            issues++;
            Debug.LogWarning(path + ": " + string.Join("; ", errors), item);
        }
        ItemRegistry.ReloadDefinitions();
        Debug.Log($"Checked {count} item definitions; {issues} need attention.");
    }
}

[CustomEditor(typeof(ItemDefinition))]
public class ItemDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Images: " + ItemAuthoring.Icons + "\nDrag a Sprite into Icon. Keep this asset under " + ItemAuthoring.Definitions + ".", MessageType.Info);
        DrawDefaultInspector();
        if (GUI.changed) ItemRegistry.ReloadDefinitions();
        var item = (ItemDefinition)target;
        if (item.icon == null) EditorGUILayout.HelpBox("Icon is missing. Import your PNG as Sprite (2D and UI), then drag the Sprite here.", MessageType.Warning);
        if (GUILayout.Button("Validate All Items")) ItemAuthoring.ValidateItems();
    }
}

public class ItemDefinitionImportWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        // Reset on asset changes too, including when Enter Play Mode skips domain reload.
        ItemRegistry.ReloadDefinitions();
    }
}
