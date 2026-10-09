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
            { "MAIN_CONTROLS_HINT", "{0}/{1} Move   {2} Jump   {3} / {4} Dash\n{5} Attack   {6} Parry   {7} Potion   {8} Interact\n{9} Carnage Burst   {10} Buff   {11} Excalibur\n{12} Status   {13} Map   {14} Recall" },

            // Difficulty Selection
            { "DIFF_TITLE", "CHOOSE YOUR JOURNEY" },
            { "DIFF_SUBTITLE", "A new game replaces your current journey when you next save." },
            { "DIFF_EASY", "Easy — enemy damage 50%, player damage 150%" },
            { "DIFF_NORMAL", "Normal — enemy and player damage 100%" },
            { "DIFF_HARD", "Hard — slow recovery, hidden enemy guides" },
            { "DIFF_COMPARE_BUTTON", "Compare difficulties" },
            { "DIFF_COMPARE_TITLE", "DIFFICULTY COMPARISON" },
            { "DIFF_COMPARE_SUBTITLE", "Compare each journey before creating your world" },
            { "DIFF_COMPARE_BASELINE", "Percentages are relative to Normal (100%). HP recovery still requires a healing area." },
            { "DIFF_FEATURE", "Gameplay setting" },
            { "DIFF_NAME_EASY", "Easy" },
            { "DIFF_NAME_NORMAL", "Normal" },
            { "DIFF_NAME_HARD", "Hard" },
            { "DIFF_ENEMY_DAMAGE", "Enemy damage" },
            { "DIFF_PLAYER_DAMAGE", "Player damage" },
            { "DIFF_STAMINA_COST", "Stamina consumption" },
            { "DIFF_RECOVERY", "HP / Stamina recovery" },
            { "DIFF_ENEMY_HEALTH_BAR", "Enemy health bars" },
            { "DIFF_ATTACK_WARNING", "Enemy attack warnings" },
            { "DIFF_ENEMY_LEVEL", "Enemy levels" },
            { "DIFF_SHOWN", "Shown" },
            { "DIFF_HIDDEN", "Hidden" },
            { "DIFF_CLOSE", "Close" },

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
            { "BTN_RENAME_WORLD", "Rename" },
            { "BTN_DELETE_WORLD", "Delete" },
            { "RENAME_TITLE", "RENAME WORLD" },
            { "RENAME_SUBTITLE", "Enter a new name for this save" },
            { "BTN_SAVE_NAME", "Save Name" },
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
            { "LANGUAGE_CHANGE_PROMPT", "Click to switch to Thai" },
            { "BTN_CONTROLS", "Customize Player Controls" },
            { "BTN_TUTORIAL", "How to play" },
            { "TUTORIAL_TITLE", "HOW TO PLAY" },
            { "TUTORIAL_SUBTITLE", "Choose a topic. Scroll for details. Key labels follow your current control settings." },
            { "BTN_BACK", "[1]  Back" },

            // Controls
            { "CONTROLS_TITLE", "PLAYER CONTROLS" },
            { "CONTROLS_SUBTITLE", "Scroll for more controls. Click to rebind; Esc cancels" },
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
            { "ACTION_INTERACT", "Interact" },
            { "ACTION_ADMIN_MODIFIER", "Admin Mode Modifier" },
            { "ACTION_ADMIN_KEY", "Admin Mode Key" },
            { "ACTION_MOVE_LEFT_ALT", "Move Left (alternate)" },
            { "ACTION_MOVE_RIGHT_ALT", "Move Right (alternate)" },
            { "ACTION_SPRINT", "Sprint" },
            { "ACTION_SPRINT_ALT", "Sprint (alternate)" },
            { "ACTION_ATTACK_ALT", "Attack (alternate)" },
            { "ACTION_DASH_ALT", "Dash (alternate)" },
            { "ACTION_PREVIOUS", "Previous Skill" },
            { "ACTION_NEXT", "Next Skill" },
            { "ACTION_STATUS", "Open / Close Status" },
            { "ACTION_MAP", "Open / Close Map" },
            { "ACTION_MAP_VIEW", "Switch Map View" },
            { "ACTION_RECALL", "Recall to Save Point" },
            { "ACTION_DROP_ITEM", "Drop Held Item (Status)" }
        };

        private static readonly Dictionary<string, string> ThTexts = new Dictionary<string, string>
        {
            // Main Menu
            { "MAIN_TITLE", "อัศวินคนสุดท้าย" },
            { "MAIN_SUBTITLE", "อาณาจักรที่ล่มสลาย สี่ผนึก หนึ่งคำสาบานสุดท้าย" },
            { "MAIN_PLAY", "เริ่มเกม" },
            { "MAIN_CONTINUE", "เล่นต่อ" },
            { "MAIN_SETTINGS", "ตั้งค่า" },
            { "MAIN_EXIT", "ออกจากเกม" },
            { "MAIN_CONTROLS_HINT", "{0}/{1} เดิน   {2} กระโดด   {3} / {4} พุ่งตัว\n{5} โจมตี   {6} ปัดป้อง   {7} ดื่มยา   {8} โต้ตอบ\n{9} พายุดาบ   {10} บัฟ   {11} ดาบศักดิ์สิทธิ์\n{12} สเตตัส   {13} แผนที่   {14} กลับจุดบันทึก" },

            // Difficulty Selection
            { "DIFF_TITLE", "เลือกระดับความยาก" },
            { "DIFF_SUBTITLE", "การเริ่มเกมใหม่จะบันทึกทับการเดินทางปัจจุบันเมื่อบันทึกครั้งถัดไป" },
            { "DIFF_EASY", "ง่าย — ดาเมจศัตรู 50%, ดาเมจผู้เล่น 150%" },
            { "DIFF_NORMAL", "ปกติ — ดาเมจศัตรูและผู้เล่น 100%" },
            { "DIFF_HARD", "ยาก — ฟื้นฟูช้า ซ่อนคำแนะนำศัตรู" },
            { "DIFF_COMPARE_BUTTON", "เปรียบเทียบความยาก" },
            { "DIFF_COMPARE_TITLE", "ตารางเปรียบเทียบระดับความยาก" },
            { "DIFF_COMPARE_SUBTITLE", "ดูความแตกต่างของแต่ละระดับก่อนสร้างโลก" },
            { "DIFF_COMPARE_BASELINE", "เปอร์เซ็นต์เทียบกับระดับปกติ (100%) การฟื้นฟู HP ยังต้องอยู่ในพื้นที่ฟื้นฟู" },
            { "DIFF_FEATURE", "รายละเอียด" },
            { "DIFF_NAME_EASY", "ง่าย" },
            { "DIFF_NAME_NORMAL", "ปกติ" },
            { "DIFF_NAME_HARD", "ยาก" },
            { "DIFF_ENEMY_DAMAGE", "ดาเมจศัตรู" },
            { "DIFF_PLAYER_DAMAGE", "ดาเมจผู้เล่น" },
            { "DIFF_STAMINA_COST", "การใช้ Stamina" },
            { "DIFF_RECOVERY", "การฟื้นฟู HP / Stamina" },
            { "DIFF_ENEMY_HEALTH_BAR", "แถบพลังชีวิตศัตรู" },
            { "DIFF_ATTACK_WARNING", "สัญญาณเตือนการโจมตีศัตรู" },
            { "DIFF_ENEMY_LEVEL", "ระดับเลเวลศัตรู" },
            { "DIFF_SHOWN", "แสดง" },
            { "DIFF_HIDDEN", "ซ่อน" },
            { "DIFF_CLOSE", "ปิด" },

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
            { "BTN_RENAME_WORLD", "เปลี่ยนชื่อ" },
            { "BTN_DELETE_WORLD", "ลบ" },
            { "RENAME_TITLE", "เปลี่ยนชื่อเซฟ (โลก)" },
            { "RENAME_SUBTITLE", "พิมพ์ชื่อใหม่สำหรับเซฟนี้" },
            { "BTN_SAVE_NAME", "บันทึกชื่อ" },
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
            { "LANGUAGE_CHANGE_PROMPT", "คลิกเพื่อเปลี่ยนเป็นภาษาอังกฤษ" },
            { "BTN_CONTROLS", "ปรับปุ่มควบคุมตัวละคร" },
            { "BTN_TUTORIAL", "สอนเล่น" },
            { "TUTORIAL_TITLE", "สอนเล่น" },
            { "TUTORIAL_SUBTITLE", "เลือกหัวข้อและเลื่อนอ่านรายละเอียด ปุ่มที่แสดงตรงกับการตั้งค่าปัจจุบัน" },
            { "BTN_BACK", "[1]  ย้อนกลับ" },

            // Controls
            { "CONTROLS_TITLE", "ปุ่มควบคุมตัวละคร" },
            { "CONTROLS_SUBTITLE", "เลื่อนดูปุ่มทั้งหมด คลิกเพื่อเปลี่ยน กด Esc เพื่อยกเลิก" },
            { "REBIND_WAITING", "... กดปุ่มที่ต้องการ ..." },
            { "BTN_RESET_CONTROLS", "คืนค่าปุ่มเริ่มต้น" },
            { "ACTION_MOVE_LEFT", "เดินซ้าย" },
            { "ACTION_MOVE_RIGHT", "เดินขวา" },
            { "ACTION_JUMP", "กระโดด" },
            { "ACTION_ATTACK", "โจมตี" },
            { "ACTION_DASH", "พุ่งตัว" },
            { "ACTION_PARRY", "ปัดการโจมตี / สวนกลับ" },
            { "ACTION_DRINK", "ดื่มยาฟื้นฟู" },
            { "ACTION_SKILL", "ทักษะ: พายุดาบ" },
            { "ACTION_BUFF", "ทักษะ: เจตจำนงเหล็กกล้า" },
            { "ACTION_EXCALIBUR", "ทักษะ: ดาบศักดิ์สิทธิ์" },
            { "ACTION_INTERACT", "โต้ตอบ" },
            { "ACTION_ADMIN_MODIFIER", "ปุ่มเสริมโหมดแอดมิน" },
            { "ACTION_ADMIN_KEY", "ปุ่มเปิดโหมดแอดมิน" },
            { "ACTION_MOVE_LEFT_ALT", "เดินซ้าย (ปุ่มเสริม)" },
            { "ACTION_MOVE_RIGHT_ALT", "เดินขวา (ปุ่มเสริม)" },
            { "ACTION_SPRINT", "วิ่ง" },
            { "ACTION_SPRINT_ALT", "วิ่ง (ปุ่มเสริม)" },
            { "ACTION_ATTACK_ALT", "โจมตี (ปุ่มเสริม)" },
            { "ACTION_DASH_ALT", "พุ่งตัว (ปุ่มเสริม)" },
            { "ACTION_PREVIOUS", "สกิลก่อนหน้า" },
            { "ACTION_NEXT", "สกิลถัดไป" },
            { "ACTION_STATUS", "เปิด / ปิดสเตตัส" },
            { "ACTION_MAP", "เปิด / ปิดแผนที่" },
            { "ACTION_MAP_VIEW", "สลับมุมมองแผนที่" },
            { "ACTION_RECALL", "กลับจุดบันทึก" },
            { "ACTION_DROP_ITEM", "ทิ้งไอเทมที่ถือ (หน้าสเตตัส)" }
        };

        public static string Get(string key)
        {
            var texts = Current == GameLanguage.English ? EnTexts : ThTexts;
            if (!texts.TryGetValue(key, out string text)) return key;
            if (key != "MAIN_CONTROLS_HINT") return text;
            string[] actions = { "Move", "Move", "Jump", "Dash", "Dash", "Attack", "CounterAttack", "UseDrink", "Interact", "UseSkill", "UseBuff", "UseExcalibur", "ToggleStatus", "ToggleMap", "Recall" };
            int[] indices = { 6, 8, 0, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            var keys = new object[actions.Length];
            for (int i = 0; i < actions.Length; i++) keys[i] = TheLastKnight.Input.KeyRebindManager.GetCurrentBindingDisplay(actions[i], indices[i]);
            return string.Format(text, keys);
        }

        public static void ToggleLanguage()
        {
            Current = (Current == GameLanguage.Thai) ? GameLanguage.English : GameLanguage.Thai;
        }

        private static readonly Dictionary<string, string> TranslationKeys = BuildTranslationKeys();

        private static Dictionary<string, string> BuildTranslationKeys()
        {
            var keys = new Dictionary<string, string>();
            foreach (var pair in EnTexts) keys[pair.Value] = pair.Key;
            foreach (var pair in ThTexts) keys[pair.Value] = pair.Key;
            return keys;
        }

        public static string Translate(string source)
        {
            if (source != null && TranslationKeys.TryGetValue(source, out var key)) return Get(key);
            return LocalizedTextCatalog.Translate(source, Current);
        }
    }
}
