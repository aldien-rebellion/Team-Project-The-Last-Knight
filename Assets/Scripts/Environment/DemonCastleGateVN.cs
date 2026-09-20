using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.UI;

namespace TheLastKnight.Environment
{
    public class DemonCastleGateVN : MonoBehaviour
    {
        public Sprite arthurBackView;
        public DemonCastleGate gate;
        private readonly bool[] _socketed = new bool[4];
        private bool _opening;
        private GameObject _panel;
        private Button[] _buttons = new Button[4];
        public int SocketedCount { get { int count = 0; foreach (bool value in _socketed) if (value) count++; return count; } }

        private IEnumerator Start()
        {
            if (gate == null) gate = GetComponent<DemonCastleGate>();
            if (gate != null) { gate.enabled = false; if (gate.promptCanvas != null) gate.promptCanvas.SetActive(false); }
            foreach (var portal in FindObjectsByType<ScenePortal>(FindObjectsSortMode.None)) portal.enabled = false;
            while (GameManager.Instance.Player == null) yield return null;
            GameManager.Instance.SetInputBlocked(true);
            var player = GameManager.Instance.Player;
            player.transform.position = new Vector3(0f, -1.8f, 0f);
            player.GetComponent<Animator>().enabled = false;
            if (arthurBackView != null) player.GetComponent<SpriteRenderer>().sprite = arthurBackView;
            var camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                var follow = camera.GetComponent<TheLastKnight.Camera.CameraFollow2D>();
                if (follow != null) follow.enabled = false;
                camera.transform.position = new Vector3(0, 0.7f, -10);
                camera.orthographicSize = 5.7f;
            }
            _panel = RuntimeUI.Panel("THE FOUR SEALS", out var content);
            _panel.transform.Find("Backdrop").GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.15f);
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.15f, 0.01f); rect.anchorMax = new Vector2(0.85f, 0.43f);
            content.GetComponentInChildren<Text>().fontSize = 28;
            RuntimeUI.Label(content, "The gate remembers four ancient runes. Choose each collected rune to place it in its pillar.", 19);
            var tray = new GameObject("Rune Tray", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            tray.transform.SetParent(content, false);
            tray.GetComponent<LayoutElement>().preferredHeight = 48;
            tray.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            string[] names = { "Pentagram", "Demon Hand", "Evil Eye", "Trident" };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                _buttons[i] = RuntimeUI.Button(tray.transform, names[i], () => InsertRune(index));
                _buttons[i].interactable = DemonRuneManager.Instance.HasRune(i);
                if (gate != null && gate.socketRenderers[i] != null) gate.socketRenderers[i].sprite = gate.unlitRuneSprites[i];
            }
            RuntimeUI.Button(content, "Return to Suburb to Forest", () =>
            {
                ScenePortal.lastSceneLoaded = "DemonCastleEntrance";
                ScenePortal.targetPortalExpected = "";
                GameManager.Instance.Load("SuburbToForest");
            });
        }

        public bool InsertRune(int index)
        {
            if (_opening || index < 0 || index >= 4 || _socketed[index] || !DemonRuneManager.Instance.HasRune(index)) return false;
            _socketed[index] = true;
            if (_buttons[index] != null) { _buttons[index].interactable = false; _buttons[index].GetComponent<Image>().color = new Color(0.3f, 0.55f, 0.45f); }
            if (gate != null && gate.socketRenderers[index] != null) gate.socketRenderers[index].sprite = gate.litRuneSprites[index];
            TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("rune");
            if (SocketedCount == 4) StartCoroutine(OpenGate());
            return true;
        }

        private IEnumerator OpenGate()
        {
            _opening = true;
            if (_panel != null) _panel.SetActive(false);
            var camera = UnityEngine.Camera.main;
            Vector3 origin = camera != null ? camera.transform.position : Vector3.zero;
            float until = Time.time + 1.2f;
            while (Time.time < until)
            {
                if (camera != null) camera.transform.position = origin + (Vector3)(Random.insideUnitCircle * 0.07f);
                yield return null;
            }
            if (camera != null) camera.transform.position = origin;
            if (gate != null) { gate.isUnlocked = true; gate.UpdateVisuals(); }
            yield return new WaitForSeconds(0.8f);
            ScenePortal.lastSceneLoaded = "DemonCastleEntrance"; ScenePortal.targetPortalExpected = "";
            GameManager.Instance.Load("DemonCastle");
        }
    }
}

