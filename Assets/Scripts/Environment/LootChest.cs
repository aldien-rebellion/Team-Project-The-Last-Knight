using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Inventory;
using TheLastKnight.Combat;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class LootChest : WorldInteractable
    {
        public ChestLootTable lootTable;
        [Header("Item count (inclusive, duplicates allowed)")]
        [Min(0)] public int minimumItems = 1;
        [Min(0)] public int maximumItems = 3;
        [Header("Rarity weights (normalized across non-empty tiers)")]
        [InspectorName("Normal Weight"), Min(0)] public float commonWeight = 75;
        [Min(0)] public float rareWeight = 20;
        [Min(0)] public float epicWeight = 5;
        [Header("Gold (inclusive)")]
        [Min(0)] public int minimumGold = 10;
        [Min(0)] public int maximumGold = 50;
        [Header("Opening animation")]
        public Sprite[] openingFrames;
        [Min(0.01f)] public float frameDuration = 0.1f;
        public Vector2 dropOffset = new Vector2(0, 0.5f);
        public bool IsOpened { get; private set; }
        private string _persistentId;

        // Scene and authored hierarchy identify each placed instance, including copies of a prefab.
        // Cache before rewards/prompt objects can alter the runtime hierarchy.
        public string PersistentId
        {
            get
            {
                if (_persistentId != null) return _persistentId;
                string path = "";
                for (var node = transform; node != null; node = node.parent)
                    path = "/" + node.GetSiblingIndex() + ":" + node.name + path;
                return _persistentId = gameObject.scene.path + path;
            }
        }

        private void Awake() { prompt = "F  Open chest"; promptHeight = 1.2f; _ = PersistentId; }
        private void Start() { RestoreOpenedState(GameManager.Instance?.State); }

        private bool RestoreOpenedState(PlayerSaveData state)
        {
            if (state?.openedLootChests == null || !state.openedLootChests.Contains(PersistentId)) return false;
            SetOpened();
            if (openingFrames != null)
                for (int i = openingFrames.Length - 1; i >= 0; i--)
                    if (openingFrames[i] != null)
                    {
                        GetComponent<SpriteRenderer>().sprite = openingFrames[i];
                        break;
                    }
            return true;
        }

        private void SetOpened()
        {
            IsOpened = true;
            interactionRange = -1;
            prompt = "Opened";
        }
        private void OnValidate()
        {
            minimumItems = Mathf.Max(0, minimumItems); maximumItems = Mathf.Max(minimumItems, maximumItems);
            minimumGold = Mathf.Max(0, minimumGold); maximumGold = Mathf.Max(minimumGold, maximumGold);
            commonWeight = Mathf.Max(0, commonWeight); rareWeight = Mathf.Max(0, rareWeight); epicWeight = Mathf.Max(0, epicWeight);
            frameDuration = Mathf.Max(0.01f, frameDuration);
        }

        public static int RollInclusive(int minimum, int maximum)
        {
            minimum = Mathf.Max(0, minimum); maximum = Mathf.Max(minimum, maximum);
            return (int)System.Math.Min(maximum, minimum + (long)(Random.value * ((long)maximum - minimum + 1)));
        }

        public List<InventoryItemData> RollItems()
        {
            var result = new List<InventoryItemData>();
            if (lootTable == null) return result;
            var pools = new[] { lootTable.GetPool(ChestItemRarity.Normal), lootTable.GetPool(ChestItemRarity.Rare), lootTable.GetPool(ChestItemRarity.Epic) };
            var weights = new[] { commonWeight, rareWeight, epicWeight };
            double total = 0;
            for (int tier = 0; tier < 3; tier++)
            {
                if (pools[tier].Count == 0 || float.IsNaN(weights[tier]) || float.IsInfinity(weights[tier])) weights[tier] = 0;
                weights[tier] = Mathf.Max(0, weights[tier]); total += weights[tier];
            }
            if (total <= 0) return result;
            int count = RollInclusive(minimumItems, maximumItems);
            for (int i = 0; i < count; i++)
            {
                double roll = Random.value * total;
                int selected = -1;
                for (int tier = 0; tier < 3; tier++)
                {
                    if (weights[tier] <= 0) continue;
                    selected = tier; roll -= weights[tier];
                    if (roll < 0) break;
                }
                var pool = pools[selected];
                var definition = pool[Random.Range(0, pool.Count)];
                result.Add(definition.CreateItem(1, ItemRegistry.CreateLegacyItem(definition.id)));
            }
            return result;
        }

        public override void Interact()
        {
            if (IsOpened || GameManager.Instance == null || GameManager.Instance.Player == null) return;
            var state = GameManager.Instance.State;
            if (RestoreOpenedState(state)) return;
            var items = RollItems();
            if (maximumItems > 0 && items.Count == 0 && minimumItems > 0)
            {
                Debug.LogWarning("Chest needs an enabled loot item with a positive rarity weight.", this);
                return;
            }
            if (state.openedLootChests == null) state.openedLootChests = new List<string>();
            state.openedLootChests.Add(PersistentId);
            SetOpened();
            foreach (var item in items) WorldItemPickup.Spawn(item, transform.position + (Vector3)dropOffset);
            int gold = RollInclusive(minimumGold, maximumGold);
            GameManager.Instance.Player.AddGold(gold);
            if (gold > 0) FloatingCombatText.Show(transform.position + Vector3.up, "+" + gold + " Gold", Color.yellow);
            StartCoroutine(AnimateOpening());
        }

        private IEnumerator AnimateOpening()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (openingFrames == null) yield break;
            foreach (var frame in openingFrames)
            {
                if (frame != null) renderer.sprite = frame;
                yield return new WaitForSeconds(Mathf.Max(0.01f, frameDuration));
            }
        }
    }
}
