using System;
using System.IO;
using UnityEngine;

namespace TheLastKnight.Core
{
    public static class SaveSystem
    {
#if UNITY_EDITOR
        // Editor integration checks can exercise the real UI without touching a player's save.
        public static string EditorTestSavePath { get; set; }
#endif
        public static string SavePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSavePath)) return EditorTestSavePath;
#endif
                return Path.Combine(Application.persistentDataPath, "save.json");
            }
        }
        public static bool HasSave => TryLoad(out _);
        public static bool Save(PlayerSaveData state, out string error, string path = null)
        {
            if (!IsValid(state)) { error = "Could not save: player state is invalid."; return false; }
            path = path ?? SavePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                string temporary = path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Could not save: " + exception.Message;
                return false;
            }
        }

        public static bool TryLoad(out PlayerSaveData state, string path = null)
        {
            path = path ?? SavePath;
            return TryRead(path, out state) || TryRead(path + ".bak", out state);
        }

        public static bool IsValid(PlayerSaveData state)
        {
            if (state == null || state.version != 1 || !state.initialized || state.runes == null || state.runes.Length != 4) return false;
            if (state.scene != "CityCenter" && state.scene != "OutdoorMarket" && state.scene != "Church" && state.scene != "SuburbToForest" && state.scene != "DemonCastle") return false;
            return state.level >= 1 && state.level <= 1000 && state.exp >= 0 && state.statPoints >= 0
                && state.strength > 0 && state.vitality > 0 && state.dexterity > 0 && state.agility > 0
                && state.gold >= 0 && state.potions >= 0 && state.potions <= 5 && Enum.IsDefined(typeof(GameDifficulty), state.difficulty)
                && float.IsFinite(state.hp) && state.hp > 0 && float.IsFinite(state.stamina) && state.stamina >= 0 && state.stamina <= 100
                && float.IsFinite(state.position.x) && float.IsFinite(state.position.y) && float.IsFinite(state.position.z);
        }

        private static bool TryRead(string path, out PlayerSaveData state)
        {
            state = null;
            try
            {
                if (!File.Exists(path)) return false;
                state = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
                if (IsValid(state)) return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException) { }
            state = null;
            return false;
        }
    }
}
