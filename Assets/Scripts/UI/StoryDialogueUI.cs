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
        private bool _ending, _rolling;
        private float _elapsed;
        private Transform _content;
        private RectTransform _credits, _creditsViewport;
        public void Show(string title, string[] lines, Action complete = null)
        {
            if (_panel != null) Destroy(_panel);
            _ending = _rolling = false; _elapsed = 0f;
            _lines = lines; _index = 0; _complete = complete; _openedFrame = Time.frameCount;
            GameManager.Instance.SetInputBlocked(true); Time.timeScale = 0f;
            _panel = RuntimeUI.Panel(title, out var content);
            _content = content;
            _panel.transform.Find("Backdrop").GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, 0.72f);
            _body = RuntimeUI.Label(content, lines[0], 25);
            RuntimeUI.Button(content, "Continue  •  Space / Click", Advance);
            RuntimeUI.Button(content, "Skip", Finish);
        }
        public void Intro() => Show("THE KINGDOM OF MOA", new[] {
            "Moa once rang with bells and market songs. Then the demon king broke its gates, and the kingdom fell silent.",
            "Arthur, the last knight, returns to the people he could not protect. Four scattered runes hold the path to the demon castle.",
            "Seek the Moonstone Keeper in the church, rest by Medusa, bargain in the Shadow Market, and follow the fox into the forest. Keep your oath." });
        public void Ending()
        {
            Show("A KINGDOM REBORN", new[] {
                "The demon falls. Dawn reaches Moa, and the long work of rebuilding begins.",
                "Seasons pass. The bells return, homes rise, and the market fills with voices. Arthur watches over the kingdom he kept his oath to protect."
            }, () => GameManager.Instance.Load("MainMenu", false));
            _ending = true;
            _panel.GetComponent<Canvas>().sortingOrder = 300;
            _body.GetComponent<LayoutElement>().preferredHeight = 175;
            _content.GetComponentInChildren<Text>().fontSize = 32;
            var backdrop = _panel.transform.Find("Backdrop");
            backdrop.GetComponent<Image>().color = Color.black;
            var art = new GameObject("Rebuilt Moa", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            art.transform.SetParent(backdrop, false); art.transform.SetAsFirstSibling();
            var texture = Resources.Load<Texture2D>("RebuiltMoa");
            art.GetComponent<RawImage>().texture = texture;
            art.GetComponent<RawImage>().raycastTarget = false;
            var fit = art.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = texture != null ? (float)texture.width / texture.height : 16f / 9f;
            var rect = _content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.55f, 0.2f); rect.anchorMax = new Vector2(0.95f, 0.84f);
            var shade = _content.gameObject.AddComponent<Image>();
            shade.color = new Color(0.025f, 0.035f, 0.065f, 0.85f);
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            var skip = _content.Find("Skip").GetComponent<RectTransform>();
            skip.SetParent(backdrop, false);
            skip.anchorMin = new Vector2(0.8f, 0.06f); skip.anchorMax = new Vector2(0.95f, 0.12f);
            skip.offsetMin = skip.offsetMax = Vector2.zero;
            TheLastKnight.Audio.AudioManager.Instance?.PlaySceneMusic("CityCenter");
        }

        private void RollCredits()
        {
            _rolling = true; _elapsed = 0f; _content.gameObject.SetActive(false);
            var viewport = new GameObject("Rolling Credits", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(_panel.transform.Find("Backdrop"), false);
            viewport.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.065f, 0.85f);
            _creditsViewport = viewport.GetComponent<RectTransform>();
            _creditsViewport.anchorMin = new Vector2(0.55f, 0.2f); _creditsViewport.anchorMax = new Vector2(0.95f, 0.84f);
            _creditsViewport.offsetMin = _creditsViewport.offsetMax = Vector2.zero;
            var text = RuntimeUI.Label(viewport.transform,
                "THE LAST KNIGHT\n\nA KINGDOM REBORN\n\n\nCreated by\nThe Last Knight team\n\n\nArthur's oath endures.\nMoa's story continues.\n\n\nThank you for playing.", 28);
            text.lineSpacing = 1.2f;
            _credits = text.rectTransform;
            _credits.anchorMin = new Vector2(0, 0); _credits.anchorMax = new Vector2(1, 0);
            _credits.pivot = new Vector2(0.5f, 0);
            _credits.sizeDelta = new Vector2(-40, 680);
            _credits.anchoredPosition = new Vector2(0, -680);
        }
        private void Update()
        {
            if (_panel == null || Time.frameCount <= _openedFrame + 1) return;
            if (_ending)
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_rolling)
                {
                    _credits.anchoredPosition = new Vector2(0, Mathf.Lerp(-680, _creditsViewport.rect.height, _elapsed / 24f));
                    if (_elapsed >= 24f) { Finish(); return; }
                }
                else if (_elapsed >= 9f) { Advance(); return; }
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Advance();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) Advance();
        }
        private void Advance()
        {
            _openedFrame = Time.frameCount;
            _elapsed = 0f;
            if (_rolling) { Finish(); return; }
            if (++_index >= _lines.Length)
            {
                if (_ending) RollCredits(); else Finish();
            }
            else _body.text = _lines[_index];
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
