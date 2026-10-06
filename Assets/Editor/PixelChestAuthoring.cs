using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using TheLastKnight.Environment;
using TheLastKnight.Inventory;

namespace TheLastKnight.EditorTools.PixelChests
{
    internal class CommandScript : IRunCommand
    {
        public void Execute(ExecutionResult result)
        {
            const string output = "Assets/Prefabs/Environment/PixelChests";
            const string tablePath = "Assets/Resources/Items/ChestLootTable.asset";
            Directory.CreateDirectory(output);
            var table = AssetDatabase.LoadAssetAtPath<ChestLootTable>(tablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ChestLootTable>();
                result.RegisterObjectCreation(table);
                foreach (var definition in Resources.LoadAll<ItemDefinition>("Items/Definitions").OrderBy(d => d.id))
                {
                    table.items.Add(new ChestLootTable.Entry { item = definition,
                        enabled = !ChestLootTable.IsExcluded(definition),
                        rarity = ChestLootTable.GetItemRarity(definition) });
                }
                AssetDatabase.CreateAsset(table, tablePath);
            }
            int count = 0;
            foreach (var folder in Directory.GetDirectories("Assets/sprites/Environment/Pixel Chest Pack").OrderBy(p => p))
            {
                string name = Path.GetFileName(folder);
                var frames = Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories)
                    .Where(p => Path.GetFileName(p).Contains("frame"))
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => AssetDatabase.LoadAllAssetsAtPath(p.Replace('\\', '/')).OfType<Sprite>().FirstOrDefault())
                    .Where(s => s != null).ToArray();
                if (frames.Length == 0) { result.LogWarning("No frames available for " + name); continue; }
                string path = output + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { result.Log("Preserved existing prefab " + path); continue; }
                var go = new GameObject(name);
                result.RegisterObjectCreation(go);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0]; renderer.sortingOrder = 5;
                // Pack frames use 100 PPU; size the chest to 1.25 world units wide.
                float width = frames[0].bounds.size.x;
                if (width > 0) go.transform.localScale = Vector3.one * (1.25f / width);
                var chest = go.AddComponent<LootChest>();
                chest.lootTable = table; chest.openingFrames = frames;
                bool golden = name.StartsWith("Golden"), metal = name.StartsWith("Metal");
                chest.minimumItems = golden ? 3 : metal ? 2 : 1;
                chest.maximumItems = golden ? 6 : metal ? 4 : 3;
                chest.commonWeight = golden ? 35 : metal ? 55 : 75;
                chest.rareWeight = golden ? 45 : metal ? 35 : 20;
                chest.epicWeight = golden ? 20 : metal ? 10 : 5;
                chest.minimumGold = golden ? 100 : metal ? 40 : 10;
                chest.maximumGold = golden ? 300 : metal ? 120 : 50;
                // Interaction uses distance through WorldInteractable; no blocking collider is needed.
                PrefabUtility.SaveAsPrefabAsset(go, path);
                result.DestroyObject(go);
                count++;
            }
            AssetDatabase.SaveAssets();
            result.Log("Created " + count + " chest prefabs, loot entries: " + table.items.Count);
        }
    }

    public static class PixelChestAuthoring
    {
        [MenuItem("The Last Knight/Chests/Create Missing Pixel Chest Prefabs")]
        public static string Build()
        {
            var result = new ExecutionResult("Create Pixel Chest Prefabs");
            new CommandScript().Execute(result);
            return string.Join("\n", result.GetFormattedLogs());
        }
    }
}
