using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastKnight.Core
{
    [Serializable]
    public class SavedItemData
    {
        public int slotIndex;
        public string itemId;
        public int count;
    }

    [Serializable]
    public class SavedRuneDrop
    {
        public int runeId;
        public string scene;
        public Vector3 position;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int version = 1;
        public string worldId;
        public string saveName;
        public string lastSavedDate;
        public string createdDate;
        public bool initialized;
        public string scene = "CityCenter";
        public Vector3 position;
        public float hp = 100, stamina = 100;
        public int level = 1, exp, statPoints, strength = 10, vitality = 10, dexterity = 10, agility = 10;
        public int gold, potions = 3;
        public bool[] runes = new bool[4];
        public List<SavedRuneDrop> runeDrops = new List<SavedRuneDrop>();
        // Area-boss identifiers (currently the scene name) that this world has cleared.
        public List<string> defeatedAreaBosses = new List<string>();
        public bool pentagramRuneChestOpened;
        public bool churchKey, introSeen, victory, demonCastleGateUnlocked;
        public GameDifficulty difficulty;
        public bool inventoryInitialized;
        public SavedItemData cursorItem;
        public List<SavedItemData> inventory = new List<SavedItemData>();
        public PlayerSaveData Copy() => JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(this));
    }
}
