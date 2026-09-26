using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastKnight.Core
{
    public enum GameLanguage
    {
        Thai,
        English
    }

    public static class LocalizationManager
    {
        private const string PrefKey = "TheLastKnight_SelectedLanguage";
        private static GameLanguage? _current;

        public static GameLanguage Current
        {
            get
            {
                if (!_current.HasValue)
                {
                    string saved = PlayerPrefs.GetString(PrefKey, "Thai");
                    _current = saved == "English" ? GameLanguage.English : GameLanguage.Thai;
                }
                return _current.Value;
            }
            set
            {
                _current = value;
                PlayerPrefs.SetString(PrefKey, value.ToString());
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke(value);
            }
        }

        public static event Action<GameLanguage> OnLanguageChanged;

        private static readonly Dictionary<string, string> EnTexts = new Dictionary<string, string>
        {
            // Main Menu
            { "MAIN_TITLE", "THE LAST KNIGHT" },
            { "MAIN_SUBTITLE", "A fallen kingdom. Four seals. One last oath." },
            { "MAIN_PLAY", "Play" },
            { "MAIN_CONTINUE", "Continue" },
            { "MAIN_SETTINGS", "Settings" },
            { "MAIN_EXIT", "Exit" },
            { "MAIN_CONTROLS_HINT", "A/D Move   Space Jump   Shift / RMB Dash\nLMB Attack / Parry   Q Potion   F Interact\nE Carnage Burst   R Buff   T Excalibur   B Status" },

            // Difficulty Selection
            { "DIFF_TITLE", "CHOOSE YOUR JOURNEY" },
            { "DIFF_SUBTITLE", "A new game replaces your current journey when you next save." },
            { "DIFF_EASY", "Easy — full damage and combat guides" },
            { "DIFF_NORMAL", "Normal — stronger enemies, reduced damage" },
            { "DIFF_HARD", "Hard — slow recovery, hidden enemy guides" },

            // Worlds / Save System
            { "WORLD_NEW_TITLE", "CREATE NEW WORLD" },
            { "WORLD_NEW_SUBTITLE", "Enter save name and choose your difficulty" },
            { "WORLD_NAME_LABEL", "World Name:" },
            { "WORLD_NAME_PLACEHOLDER", "Enter world name..." },
            { "WORLD_DEFAULT_NAME", "World" },
            { "WORLD_SELECT_TITLE", "SELECT WORLD" },
            { "WORLD_SELECT_SUBTITLE", "Choose a world to continue your journey" },
            { "WORLD_NO_SAVES", "No saved worlds found." },
            { "BTN_LOAD_WORLD", "Play" },
            { "BTN_DELETE_WORLD", "Delete" },
            { "DELETE_CONFIRM_TITLE", "DELETE WORLD" },
            { "DELETE_CONFIRM_MSG", "Are you sure you want to delete '{0}'?\nThis world will be lost forever!" },
            { "BTN_CONFIRM_DELETE", "Delete World" },
            { "BTN_CANCEL", "Cancel" },

            // Main Pause
            { "PAUSE_TITLE", "PAUSED" },
            { "PAUSE_SUBTITLE", "Game is paused" },
            { "BTN_RESUME", "[1]  Resume" },
            { "BTN_SETTINGS", "[2]  Settings" },
            { "BTN_MAIN_MENU", "[3]  Exit to Main Menu" },

            // Settings
            { "SETTINGS_TITLE", "SETTINGS" },
            { "SETTINGS_SUBTITLE", "Game System Settings" },
            { "AUDIO_MASTER", "Master Volume" },
            { "AUDIO_MUSIC", "Music Volume" },
            { "AUDIO_SFX", "Sound Effects" },
            { "BRIGHTNESS", "Game Brightness" },
            { "LANGUAGE_LABEL", "Language" },
            { "LANGUAGE_CURRENT", "English" },
            { "LANGUAGE_CHANGE_PROMPT", "Click to switch to ภาษาไทย" },
            { "BTN_CONTROLS", "Customize Player Controls" },
            { "BTN_BACK", "[1]  Back" },

            // Controls
            { "CONTROLS_TITLE", "PLAYER CONTROLS" },
            { "CONTROLS_SUBTITLE", "Click a button to rebind, or Esc to cancel" },
            { "REBIND_WAITING", "... Press any key ..." },
            { "BTN_RESET_CONTROLS", "Reset Controls to Default" },
            { "ACTION_MOVE_LEFT", "Move Left" },
            { "ACTION_MOVE_RIGHT", "Move Right" },
            { "ACTION_JUMP", "Jump" },
            { "ACTION_ATTACK", "Attack" },
            { "ACTION_DASH", "Dash" },
            { "ACTION_PARRY", "Parry / Counter" },
            { "ACTION_DRINK", "Drink Healing Potion" },
            { "ACTION_SKILL", "Skill: Carnage Burst" },
            { "ACTION_BUFF", "Skill: Iron Will" },
            { "ACTION_EXCALIBUR", "Skill: Excalibur" },
            { "ACTION_INTERACT", "Interact" }
        };

        private static readonly Dictionary<string, string> ThTexts = new Dictionary<string, string>
        {
            // Main Menu
            { "MAIN_TITLE", "THE LAST KNIGHT" },
            { "MAIN_SUBTITLE", "อาณาจักรที่ล่มสลาย สี่ผนึก หนึ่งคำสาบานสุดท้าย" },
            { "MAIN_PLAY", "เริ่มเกม" },
            { "MAIN_CONTINUE", "เล่นต่อ" },
            { "MAIN_SETTINGS", "ตั้งค่า" },
            { "MAIN_EXIT", "ออกจากเกม" },
            { "MAIN_CONTROLS_HINT", "A/D เดิน   Space กระโดด   Shift / คลิกขวา พุ่งตัว\nคลิกซ้าย โจมตี / ปัดป้อง   Q ดื่มยา   F โต้ตอบ\nE สกิลหมุนดาบ   R บัฟ   T ดาบศักดิ์สิทธิ์   B สเตตัส" },

            // Difficulty Selection
            { "DIFF_TITLE", "เลือกระดับความยาก" },
            { "DIFF_SUBTITLE", "การเริ่มเกมใหม่จะบันทึกทับการเดินทางปัจจุบันเมื่อบันทึกครั้งถัดไป" },
            { "DIFF_EASY", "ง่าย — ความเสียหายเต็มที่ พร้อมคำแนะนำการต่อสู้" },
            { "DIFF_NORMAL", "ปกติ — ศัตรูแข็งแกร่งขึ้น ความเสียหายลดลง" },
            { "DIFF_HARD", "ยาก — ฟื้นฟูช้า ซ่อนคำแนะนำศัตรู" },

            // Worlds / Save System
            { "WORLD_NEW_TITLE", "สร้างเซฟใหม่" },
            { "WORLD_NEW_SUBTITLE", "ตั้งชื่อเซฟและเลือกระดับความยาก" },
            { "WORLD_NAME_LABEL", "ชื่อเซฟ (โลก):" },
            { "WORLD_NAME_PLACEHOLDER", "พิมพ์ชื่อเซฟที่นี่..." },
            { "WORLD_DEFAULT_NAME", "โลก" },
            { "WORLD_SELECT_TITLE", "เลือกเซฟ (โลก)" },
            { "WORLD_SELECT_SUBTITLE", "เลือกเซฟเพื่อออกเดินทางต่อ" },
            { "WORLD_NO_SAVES", "ยังไม่มีข้อมูลเซฟ" },
            { "BTN_LOAD_WORLD", "เข้าเล่น" },
            { "BTN_DELETE_WORLD", "ลบ" },
            { "DELETE_CONFIRM_TITLE", "ยืนยันการลบเซฟ" },
            { "DELETE_CONFIRM_MSG", "คุณแน่ใจหรือไม่ว่าต้องการลบเซฟ '{0}'?\nโลกนี้จะถูกลบถาวรและไม่สามารถกู้คืนได้!" },
            { "BTN_CONFIRM_DELETE", "ยืนยันลบเซฟ" },
            { "BTN_CANCEL", "ยกเลิก" },

            // Main Pause
            { "PAUSE_TITLE", "หยุดเกม" },
            { "PAUSE_SUBTITLE", "เกมถูกหยุดชั่วคราว" },
            { "BTN_RESUME", "[1]  เล่นต่อ" },
            { "BTN_SETTINGS", "[2]  ตั้งค่า" },
            { "BTN_MAIN_MENU", "[3]  ออกไปที่เมนูหลัก" },

            // Settings
            { "SETTINGS_TITLE", "ตั้งค่า" },
            { "SETTINGS_SUBTITLE", "การตั้งค่าระบบเกม" },
            { "AUDIO_MASTER", "ระดับเสียงหลัก" },
            { "AUDIO_MUSIC", "ระดับเสียงดนตรี" },
            { "AUDIO_SFX", "ระดับเสียงเอฟเฟกต์" },
            { "BRIGHTNESS", "ความสว่างทั้งเกม" },
            { "LANGUAGE_LABEL", "ภาษา" },
            { "LANGUAGE_CURRENT", "ภาษาไทย" },
            { "LANGUAGE_CHANGE_PROMPT", "คลิกเพื่อเปลี่ยนเป็น English" },
            { "BTN_CONTROLS", "ปรับปุ่มควบคุมตัวละคร" },
            { "BTN_BACK", "[1]  ย้อนกลับ" },

            // Controls
            { "CONTROLS_TITLE", "ปุ่มควบคุมตัวละคร" },
            { "CONTROLS_SUBTITLE", "คลิกปุ่มเพื่อเปลี่ยน หรือกด Esc เพื่อยกเลิก" },
            { "REBIND_WAITING", "... กดปุ่มที่ต้องการ ..." },
            { "BTN_RESET_CONTROLS", "คืนค่าปุ่มเริ่มต้น" },
            { "ACTION_MOVE_LEFT", "เดินซ้าย" },
            { "ACTION_MOVE_RIGHT", "เดินขวา" },
            { "ACTION_JUMP", "กระโดด" },
            { "ACTION_ATTACK", "โจมตี" },
            { "ACTION_DASH", "พุ่งตัว" },
            { "ACTION_PARRY", "ปัดการโจมตี (Parry)" },
            { "ACTION_DRINK", "ดื่มยาฟื้นฟู" },
            { "ACTION_SKILL", "สกิล: Carnage Burst" },
            { "ACTION_BUFF", "สกิล: Iron Will" },
            { "ACTION_EXCALIBUR", "สกิล: Excalibur" },
            { "ACTION_INTERACT", "โต้ตอบ" }
        };

        public static string Get(string key)
        {
            if (Current == GameLanguage.English)
            {
                if (EnTexts.TryGetValue(key, out var en)) return en;
            }
            else
            {
                if (ThTexts.TryGetValue(key, out var th)) return th;
            }
            return key;
        }

        public static void ToggleLanguage()
        {
            Current = (Current == GameLanguage.Thai) ? GameLanguage.English : GameLanguage.Thai;
        }
    }
}
