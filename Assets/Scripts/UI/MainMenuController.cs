using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.Audio;

namespace TheLastKnight.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private GameObject _panel;
        private void Start() { ShowMain(); }
        private Transform Replace(string title)
        {
            if (_panel != null) Destroy(_panel);
            _panel = RuntimeUI.Panel(title, out var content);
            return content;
        }
        public void ShowMain()
        {
            Time.timeScale = 1f;
            var content = Replace("THE LAST KNIGHT");
            RuntimeUI.Label(content, "A fallen kingdom. Four seals. One last oath.", 22);
            RuntimeUI.Button(content, "Play", ShowDifficulty);
            RuntimeUI.Button(content, "Continue", () =>
            {
                if (!GameManager.Instance.ContinueGame()) ShowMain();
            }).interactable = SaveSystem.HasSave;
            RuntimeUI.Button(content, "Settings", ShowSettings);
            RuntimeUI.Button(content, "Exit", Application.Quit);
            RuntimeUI.Label(content, "A/D Move   Space Jump   Shift / Right-click Dash\nLeft-click Attack / Parry   Q Potion   F Interact\nE Carnage Burst   R Buff   T Excalibur   B Status", 17);
        }
        private void ShowDifficulty()
        {
            var content = Replace("CHOOSE YOUR JOURNEY");
            RuntimeUI.Label(content, "A new game replaces your current journey when you next save.", 19);
            RuntimeUI.Button(content, "Easy — full damage and combat guides", () => GameManager.Instance.NewGame(GameDifficulty.Easy));
            RuntimeUI.Button(content, "Normal — stronger enemies, reduced damage", () => GameManager.Instance.NewGame(GameDifficulty.Normal));
            RuntimeUI.Button(content, "Hard — slow recovery, hidden enemy guides", () => GameManager.Instance.NewGame(GameDifficulty.Hard));
            RuntimeUI.Button(content, "Back", ShowMain);
        }
        private void ShowSettings()
        {
            var content = Replace("SETTINGS");
            var audio = AudioManager.Instance;
            AddSlider(content, "Master", audio.Master, value => audio.SetVolumes(value, audio.Music, audio.Effects));
            AddSlider(content, "Music", audio.Music, value => audio.SetVolumes(audio.Master, value, audio.Effects));
            AddSlider(content, "Sound effects", audio.Effects, value => audio.SetVolumes(audio.Master, audio.Music, value));
            RuntimeUI.Button(content, "Back", () => { PlayerPrefs.Save(); ShowMain(); });
        }
        private static void AddSlider(Transform parent, string name, float value, UnityEngine.Events.UnityAction<float> changed)
        {
            var label = RuntimeUI.Label(parent, $"{name}  {Mathf.RoundToInt(value * 100)}%", 20);
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28;
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(go.transform, false);
            var rect = track.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0.35f); rect.anchorMax = new Vector2(1, 0.65f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.35f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(go.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 28);
            handle.GetComponent<Image>().color = new Color(0.9f, 0.77f, 0.48f);
            var slider = go.GetComponent<Slider>();
            slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.value = value;
            slider.onValueChanged.AddListener(v => { label.text = $"{name}  {Mathf.RoundToInt(v * 100)}%"; changed(v); });
        }
    }
}
