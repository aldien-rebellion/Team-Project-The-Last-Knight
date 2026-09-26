using System;
using UnityEngine;

namespace TheLastKnight.Core
{
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
        public bool churchKey, introSeen, victory;
        public GameDifficulty difficulty;
        public PlayerSaveData Copy() => JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(this));
    }
}
