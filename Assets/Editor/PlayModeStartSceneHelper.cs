using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastKnight.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartSceneHelper
    {
        private const string MenuPath = "Tools/Play Mode/Always Start From Main Menu";
        private const string PrefKey = "TheLastKnight_PlayMode_AlwaysStartFromMainMenu";
        private const string DefaultMainMenuPath = "Assets/Scenes/MainMenu.unity";

        static PlayModeStartSceneHelper()
        {
            EditorApplication.delayCall += ApplySetting;
        }

        [MenuItem(MenuPath, false, 100)]
        private static void ToggleStartFromMainMenu()
        {
            bool isEnabled = !EditorPrefs.GetBool(PrefKey, true);
            EditorPrefs.SetBool(PrefKey, isEnabled);
            ApplySetting();
            Debug.Log($"[PlayModeStartScene] Always start from Main Menu: {(isEnabled ? "ENABLED" : "DISABLED")}");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateToggleStartFromMainMenu()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefKey, true));
            return true;
        }

        public static void ApplySetting()
        {
            bool isEnabled = EditorPrefs.GetBool(PrefKey, true);
            if (isEnabled)
            {
                string targetPath = GetTargetScenePath();
                if (!string.IsNullOrEmpty(targetPath))
                {
                    SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath);
                    if (sceneAsset != null)
                    {
                        EditorSceneManager.playModeStartScene = sceneAsset;
                        return;
                    }
                }

                Debug.LogWarning("[PlayModeStartScene] Could not find MainMenu scene to set as Play Mode Start Scene.");
                EditorSceneManager.playModeStartScene = null;
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        private static string GetTargetScenePath()
        {
            if (System.IO.File.Exists(DefaultMainMenuPath))
            {
                return DefaultMainMenuPath;
            }

            if (EditorBuildSettings.scenes != null && EditorBuildSettings.scenes.Length > 0)
            {
                return EditorBuildSettings.scenes[0].path;
            }

            return null;
        }
    }
}
