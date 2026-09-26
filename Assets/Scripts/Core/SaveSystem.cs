using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastKnight.Core
{
    public static class SaveSystem
    {
#if UNITY_EDITOR
        // Editor integration checks can exercise the real UI without touching a player's save.
        public static string EditorTestSavePath { get; set; }
#endif

        private static string _activeWorldId;
        public static string ActiveWorldId
        {
            get
            {
                if (string.IsNullOrEmpty(_activeWorldId))
                {
                    _activeWorldId = PlayerPrefs.GetString("TheLastKnight_ActiveWorldId", string.Empty);
                }
                return _activeWorldId;
            }
            set
            {
                _activeWorldId = value;
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString("TheLastKnight_ActiveWorldId", value);
                }
                else
                {
                    PlayerPrefs.DeleteKey("TheLastKnight_ActiveWorldId");
                }
                PlayerPrefs.Save();
            }
        }

        public static string SavesDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSavePath))
                {
                    return Path.Combine(Path.GetDirectoryName(EditorTestSavePath), "saves");
                }
#endif
                return Path.Combine(Application.persistentDataPath, "saves");
            }
        }

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

        public static bool HasAnySave
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSavePath))
                {
                    return HasSave;
                }
#endif
                return GetAllSaves().Count > 0 || HasSave;
            }
        }

        public static string GetWorldSavePath(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) worldId = "default_world";
            return Path.Combine(SavesDirectory, $"{worldId}.json");
        }

        public static List<PlayerSaveData> GetAllSaves()
        {
            var list = new List<PlayerSaveData>();
            string dir = SavesDirectory;

            MigrateLegacySaveIfPresent();

            if (!Directory.Exists(dir)) return list;

            var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                if (file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryRead(file, out var state) || TryRead(file + ".bak", out state))
                {
                    if (string.IsNullOrEmpty(state.worldId))
                    {
                        state.worldId = Path.GetFileNameWithoutExtension(file);
                    }
                    if (string.IsNullOrEmpty(state.saveName))
                    {
                        state.saveName = "World " + (state.worldId.Length > 4 ? state.worldId.Substring(0, 4) : state.worldId);
                    }
                    list.Add(state);
                }
            }

            list.Sort((a, b) =>
            {
                DateTime dtA, dtB;
                bool parsedA = DateTime.TryParse(a.lastSavedDate, out dtA);
                bool parsedB = DateTime.TryParse(b.lastSavedDate, out dtB);
                if (parsedA && parsedB) return dtB.CompareTo(dtA);
                if (parsedA) return -1;
                if (parsedB) return 1;
                return string.Compare(b.worldId, a.worldId, StringComparison.Ordinal);
            });

            return list;
        }

        private static void MigrateLegacySaveIfPresent()
        {
            try
            {
                string legacy = Path.Combine(Application.persistentDataPath, "save.json");
                if (File.Exists(legacy))
                {
                    if (TryRead(legacy, out var legacyState))
                    {
                        if (string.IsNullOrEmpty(legacyState.worldId))
                        {
                            legacyState.worldId = "legacy_save";
                            legacyState.saveName = "Legacy Save";
                            legacyState.lastSavedDate = File.GetLastWriteTime(legacy).ToString("yyyy-MM-dd HH:mm");
                        }
                        string target = GetWorldSavePath(legacyState.worldId);
                        if (!File.Exists(target))
                        {
                            Save(legacyState, out _, target);
                        }
                    }
                }
            }
            catch { }
        }

        public static bool TryLoadWorld(string worldId, out PlayerSaveData state)
        {
            state = null;
            if (string.IsNullOrEmpty(worldId)) return false;
            string path = GetWorldSavePath(worldId);
            if (TryRead(path, out state) || TryRead(path + ".bak", out state))
            {
                ActiveWorldId = worldId;
                return true;
            }
            return false;
        }

        public static bool DeleteWorld(string worldId, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(worldId))
            {
                error = "World ID is empty.";
                return false;
            }

            try
            {
                string path = GetWorldSavePath(worldId);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");

                if (ActiveWorldId == worldId)
                {
                    ActiveWorldId = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool RenameWorld(string worldId, string newName, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(worldId))
            {
                error = "World ID is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                error = "New name cannot be empty.";
                return false;
            }

            if (!TryLoadWorld(worldId, out var state))
            {
                error = "Could not find world to rename.";
                return false;
            }

            state.saveName = newName.Trim();
            bool saved = Save(state, out error);
            if (saved && GameManager.Instance != null && GameManager.Instance.State != null && GameManager.Instance.State.worldId == worldId)
            {
                GameManager.Instance.State.saveName = state.saveName;
            }
            return saved;
        }

        public static bool Save(PlayerSaveData state, out string error, string path = null)
        {
            if (!IsValid(state)) { error = "Could not save: player state is invalid."; return false; }

            if (path == null)
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorTestSavePath) && string.IsNullOrEmpty(state.worldId))
                {
                    path = EditorTestSavePath;
                }
                else
#endif
                {
                    if (string.IsNullOrEmpty(state.worldId))
                    {
                        state.worldId = string.IsNullOrEmpty(ActiveWorldId)
                            ? Guid.NewGuid().ToString("N")
                            : ActiveWorldId;
                    }
                    if (string.IsNullOrEmpty(state.saveName))
                    {
                        state.saveName = "World";
                    }
                    state.lastSavedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    if (string.IsNullOrEmpty(state.createdDate))
                    {
                        state.createdDate = state.lastSavedDate;
                    }
                    path = GetWorldSavePath(state.worldId);
                    ActiveWorldId = state.worldId;
                }
            }

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
            if (path != null)
            {
                return TryRead(path, out state) || TryRead(path + ".bak", out state);
            }

            if (!string.IsNullOrEmpty(ActiveWorldId))
            {
                if (TryLoadWorld(ActiveWorldId, out state)) return true;
            }

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(EditorTestSavePath))
            {
                return TryRead(EditorTestSavePath, out state) || TryRead(EditorTestSavePath + ".bak", out state);
            }
#endif

            var all = GetAllSaves();
            if (all.Count > 0)
            {
                state = all[0];
                ActiveWorldId = state.worldId;
                return true;
            }

            return TryRead(SavePath, out state) || TryRead(SavePath + ".bak", out state);
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

