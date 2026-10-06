using System;
using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.AI;
using TheLastKnight.Inventory;

namespace TheLastKnight.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyLootDrop : MonoBehaviour
    {
        [Header("Item Drop Configuration")]
        [Tooltip("Direct reference to the ItemDefinition to drop. If null, dropItemId will be used.")]
        [SerializeField] private ItemDefinition _dropItem;
        [Tooltip("Fallback ID if _dropItem is null. If empty, auto-detects by enemy name.")]
        [SerializeField] private string _dropItemId;

        [Header("Drop Rates")]
        [Tooltip("If true, drops guaranteed 1 item (100%). If false, normal monster: rolls 3 times with 33% chance each.")]
        [SerializeField] private bool _isBoss = false;
        [Range(0f, 1f)]
        [Tooltip("Drop chance per roll for normal monsters (default: 33%).")]
        [SerializeField] private float _dropChance = 0.33f;
        [Min(1)]
        [Tooltip("Number of drop attempts for normal monsters (default: 3 rolls).")]
        [SerializeField] private int _dropRolls = 3;
        [Min(1)]
        [Tooltip("Guaranteed drop count for bosses (default: 1).")]
        [SerializeField] private int _bossGuaranteedCount = 1;

        [Header("Spawn Position Offset")]
        [SerializeField] private Vector3 _spawnOffset = Vector3.zero;

        public ItemDefinition DropItem { get => _dropItem; set => _dropItem = value; }
        public string DropItemId { get => _dropItemId; set => _dropItemId = value; }
        public bool IsBoss { get => _isBoss; set => _isBoss = value; }
        public float DropChance { get => _dropChance; set => _dropChance = value; }
        public int DropRolls { get => _dropRolls; set => _dropRolls = value; }
        public int BossGuaranteedCount { get => _bossGuaranteedCount; set => _bossGuaranteedCount = value; }
        public Vector3 SpawnOffset { get => _spawnOffset; set => _spawnOffset = value; }

        private bool _hasDropped = false;

        private void Awake()
        {
            var controller = GetComponent<EnemyController>();
            if (controller != null && controller.IsBoss)
            {
                _isBoss = true;
            }
            else if (IsKnownBossName(gameObject.name))
            {
                _isBoss = true;
            }

            if (_dropItem == null && string.IsNullOrEmpty(_dropItemId))
            {
                _dropItemId = GetDefaultDropItemId(gameObject.name);
            }
        }

        private void OnEnable()
        {
            _hasDropped = false;
            var stats = GetComponent<EnemyStats>();
            if (stats != null)
            {
                stats.OnDeath += DropLoot;
            }
        }

        private void OnDisable()
        {
            var stats = GetComponent<EnemyStats>();
            if (stats != null)
            {
                stats.OnDeath -= DropLoot;
            }
        }

        public void DropLoot()
        {
            if (_hasDropped) return;
            _hasDropped = true;

            bool boss = _isBoss;
            var controller = GetComponent<EnemyController>();
            if (controller != null && controller.IsBoss)
            {
                boss = true;
            }

            int count = 0;
            if (boss)
            {
                count = _bossGuaranteedCount;
            }
            else
            {
                for (int i = 0; i < _dropRolls; i++)
                {
                    if (UnityEngine.Random.value < _dropChance)
                    {
                        count++;
                    }
                }
            }

            if (count <= 0) return;

            InventoryItemData template = null;
            if (_dropItem != null)
            {
                template = _dropItem.CreateItem(1);
            }
            else
            {
                string id = !string.IsNullOrEmpty(_dropItemId) ? _dropItemId : GetDefaultDropItemId(gameObject.name);
                if (!string.IsNullOrEmpty(id))
                {
                    template = ItemRegistry.CreateItem(id, 1);
                }
            }

            if (template == null) return;

            Vector3 dropPosition = WorldItemPickup.GetDropPosition(transform) + _spawnOffset;
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = dropPosition;
                if (count > 1)
                {
                    spawnPos.x += UnityEngine.Random.Range(-0.2f, 0.2f);
                }
                WorldItemPickup.Spawn(template.Clone(1), spawnPos);
            }
        }

        public static bool IsKnownBossName(string enemyName)
        {
            if (string.IsNullOrEmpty(enemyName)) return false;
            string clean = CleanName(enemyName);
            return clean.Equals("DemonBoss", StringComparison.OrdinalIgnoreCase)
                || clean.Equals("Volcanox", StringComparison.OrdinalIgnoreCase)
                || clean.Equals("MechaStoneGolem", StringComparison.OrdinalIgnoreCase)
                || clean.Equals("Fox", StringComparison.OrdinalIgnoreCase)
                || clean.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GetDefaultDropItemId(string enemyName)
        {
            if (string.IsNullOrEmpty(enemyName)) return null;
            string clean = CleanName(enemyName);

            // Check exact or partial matches
            if (clean.IndexOf("ArchDemon", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_archdemon";
            if (clean.IndexOf("DemonBoss", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_demonboss";
            if (clean.IndexOf("DemonKin", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_demonkin";
            if (clean.IndexOf("ShadowDemonDragon", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_shadowdemondragon";
            if (clean.IndexOf("Small_Dragon", StringComparison.OrdinalIgnoreCase) >= 0 || clean.IndexOf("SmallDragon", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_smalldragon";
            if (clean.IndexOf("Dragon", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_dragon";
            if (clean.IndexOf("BlueSlime", StringComparison.OrdinalIgnoreCase) >= 0 || clean.IndexOf("Slime", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_blueslime";
            if (clean.IndexOf("BringerOfDeath", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_bringerofdeath";
            if (clean.IndexOf("Demon", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_demon";
            if (clean.IndexOf("FantasyMushroom", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_fantasymushroom";
            if (clean.IndexOf("ForestMushroom", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_forestmushroom";
            if (clean.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_forestmushroom";
            if (clean.IndexOf("FireWorm", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_fireworm";
            if (clean.IndexOf("FlyingEye", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_flyingeye";
            if (clean.IndexOf("Fox", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_fox";
            if (clean.IndexOf("Goblin", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_goblin";
            if (clean.IndexOf("HoodedProtagonist", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_hoodedprotagonist";
            if (clean.IndexOf("Jinn", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_jinn";
            if (clean.IndexOf("Lizard", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_lizard";
            if (clean.IndexOf("MechaStoneGolem", StringComparison.OrdinalIgnoreCase) >= 0 || clean.IndexOf("Golem", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_mechastonegolem";
            if (clean.IndexOf("Minotaur", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_minotaur";
            if (clean.IndexOf("Necromancer", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_necromancer";
            if (clean.IndexOf("Reaper", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_reaper";
            if (clean.IndexOf("Satyr", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_satyr";
            if (clean.IndexOf("SkeletonKnight", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_skeletonknight";
            if (clean.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_skeleton";
            if (clean.IndexOf("Skullwolf", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_skullwolf";
            if (clean.IndexOf("UndeadExecutioner", StringComparison.OrdinalIgnoreCase) >= 0 || clean.IndexOf("Executioner", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_undeadexecutioner";
            if (clean.IndexOf("Volcanox", StringComparison.OrdinalIgnoreCase) >= 0) return "drop_volcanox";

            return null;
        }

        private static string CleanName(string name)
        {
            int cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (cloneIndex >= 0) name = name.Substring(0, cloneIndex);
            return name.Trim();
        }
    }
}
