using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TheLastKnight.Core;

namespace TheLastKnight.UI
{
    public class StoryDialogueUI : MonoBehaviour
    {
        private GameObject _panel;
        private string[] _lines;
        private int _index;
        private Text _body;
        private Action _complete;
        private int _openedFrame;
        public void Show(string title, string[] lines, Action complete = null)
        {
            if (_panel != null) Destroy(_panel);
            _lines = lines; _index = 0; _complete = complete; _openedFrame = Time.frameCount;
            GameManager.Instance.SetInputBlocked(true); Time.timeScale = 0f;
            _panel = RuntimeUI.Panel(title, out var content);
            _panel.transform.Find("Backdrop").GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, 0.72f);
            _body = RuntimeUI.Label(content, lines[0], 25);
            RuntimeUI.Button(content, "Continue  •  Space / Click", Advance);
            RuntimeUI.Button(content, "Skip", Finish);
        }
        public void Intro() => Show("THE KINGDOM OF MOA", new[] {
            "Moa once rang with bells and market songs. Then the demon king broke its gates, and the kingdom fell silent.",
            "Arthur, the last knight, returns to the people he could not protect. Four scattered runes hold the path to the demon castle.",
            "Seek the Moonstone Keeper in the church, rest by Medusa, bargain in the Shadow Market, and follow the fox into the forest. Keep your oath." });
        public void Ending() => Show("A KINGDOM REBORN", new[] {
            "The demon falls. Beyond the ruined battlements, Arthur sees dawn reach Moa for the first time in years.",
            "The bells return. Homes rise from the ashes, and the market fills with voices. Arthur watches over a kingdom rebuilding its future.",
            "THE LAST KNIGHT\nCreated by The Last Knight team\nThank you for playing." }, () => GameManager.Instance.Load("MainMenu", false));
        private void Update()
        {
            if (_panel == null || Time.frameCount <= _openedFrame + 1) return;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Advance();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) Advance();
        }
        private void Advance()
        {
            _openedFrame = Time.frameCount;
            if (++_index >= _lines.Length) Finish(); else _body.text = _lines[_index];
        }
        private void Finish()
        {
            if (_panel == null) return;
            Destroy(_panel); _panel = null; Time.timeScale = 1f;
            GameManager.Instance.SetInputBlocked(false);
            _complete?.Invoke();
        }
    }
}
