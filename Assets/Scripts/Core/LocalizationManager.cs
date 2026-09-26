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
