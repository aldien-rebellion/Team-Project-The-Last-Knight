using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;
using UnityEngine.InputSystem;
using TheLastKnight.Inventory;
using TheLastKnight.UI;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(MedusaAura))]
    public class MedusaSavePoint : WorldInteractable
    {
        public bool grantsCityRune;
        [Tooltip("Only pre-boss checkpoints use this. Healing and interaction stop until this boss encounter ends.")]
        public BossArena bossCombatArena;
        public bool IsBossCombatBlocked => bossCombatArena != null && bossCombatArena.IsCombatActive;
        private int _selectedOption;
        private bool _canChoose;
        private GameObject _choiceCanvas;
        private RectTransform _choicePanel;
        private UnityEngine.UI.Text[] _choiceLabels;
        private UnityEngine.UI.Button[] _choiceButtons;
        private UnityEngine.UI.Text _choiceHint;
        protected override bool UsesWorldPrompt => false;
        protected override TextAnchor PromptAnchor => TextAnchor.MiddleLeft;
        private int OptionCount => grantsCityRune ? 3 : 2;

        private void Awake() { RefreshPrompt(); }

        protected override void UpdateSelection(bool selected)
        {
            _canChoose = selected && !IsBossCombatBlocked;
            if (!selected) _selectedOption = 0;
            else if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll != 0f)
                    _selectedOption = (_selectedOption + (scroll < 0f ? 1 : OptionCount - 1)) % OptionCount;
            }
            RefreshPrompt();
            if (selected && _choiceCanvas == null) BuildChoices();
            if (_choiceCanvas == null) return;
            _choiceCanvas.SetActive(selected);
            if (!selected) return;
            var camera = UnityEngine.Camera.main;
            if (camera == null) camera = FindAnyObjectByType<UnityEngine.Camera>();
            if (camera == null) { _choiceCanvas.SetActive(false); return; }
            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * promptHeight);
            if (screen.z <= 0f) { _choiceCanvas.SetActive(false); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_choiceCanvas.GetComponent<RectTransform>(), screen, null, out var point);
            _choicePanel.anchoredPosition = point;
            RefreshChoiceLabels();
        }

        private string OptionLabel(int index) => index == 0 ? "[F พักผ่อน/บันทึก]" :
            grantsCityRune && index == 1 ? "[F สำรวจรูปปั้นเมดูซ่า]" : "[F บังคับมอนสเตอร์เกิดใหม่]";

        private void BuildChoices()
        {
            RuntimeUI.EnsureEventSystem();
            _choiceCanvas = new GameObject("Medusa clickable choices", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            _choiceCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _choiceCanvas.GetComponent<Canvas>().sortingOrder = 80;
            var scaler = _choiceCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Choices", typeof(RectTransform));
            panel.transform.SetParent(_choiceCanvas.transform, false);
            _choicePanel = panel.GetComponent<RectTransform>();
            _choicePanel.pivot = new Vector2(0f, 0.5f);
            _choicePanel.sizeDelta = new Vector2(270f, OptionCount * 30f + 20f);
            _choiceLabels = new UnityEngine.UI.Text[OptionCount];
            _choiceButtons = new UnityEngine.UI.Button[OptionCount];
            for (int i = 0; i < OptionCount; i++)
            {
                int option = i;
                var button = RuntimeUI.Button(panel.transform, OptionLabel(i), () =>
                {
                    if (!_canChoose) return;
                    _selectedOption = option;
                    RefreshPrompt();
                    RefreshChoiceLabels();
                    Interact();
                });
                // These choices use F / mouse wheel, not UI keyboard navigation.
                // Clicking a row must not make Space submit it instead of jumping.
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                _choiceButtons[i] = button;
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, -30f * i);
                rect.sizeDelta = new Vector2(270f, 30f);
                var colors = button.colors;
                colors.normalColor = new Color(0.03f, 0.04f, 0.06f, 0.65f);
                colors.highlightedColor = new Color(0.3f, 0.25f, 0.12f, 0.85f);
                button.colors = colors;
                _choiceLabels[i] = button.GetComponentInChildren<UnityEngine.UI.Text>();
                _choiceLabels[i].fontSize = 18;
                _choiceLabels[i].alignment = TextAnchor.MiddleLeft;
            }
            var hint = RuntimeUI.Label(panel.transform, "ลูกกลิ้งเลือก • F หรือคลิกเพื่อยืนยัน", 12);
            _choiceHint = hint;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0f, 1f);
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -30f * OptionCount);
            hint.rectTransform.sizeDelta = new Vector2(270f, 20f);
        }

        private void RefreshChoiceLabels()
        {
            if (_choiceLabels == null) return;
            bool blocked = IsBossCombatBlocked;
            for (int i = 0; i < _choiceLabels.Length; i++)
            {
                _choiceButtons[i].interactable = !blocked;
                LocalizedText.Set(_choiceLabels[i], (i == _selectedOption ? "> " : "   ") + OptionLabel(i));
                _choiceLabels[i].color = blocked ? Color.gray : i == _selectedOption ? new Color(1f, 0.88f, 0.45f) : Color.white;
            }
            LocalizedText.Set(_choiceHint, blocked ? "ใช้รูปปั้นไม่ได้ระหว่างต่อสู้กับบอส" : "ลูกกลิ้งเลือก • F หรือคลิกเพื่อยืนยัน");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _canChoose = false;
            if (_choiceCanvas != null) _choiceCanvas.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_choiceCanvas != null) Destroy(_choiceCanvas);
        }

        private void RefreshPrompt()
        {
            const string rest = "[F พักผ่อน/บันทึก]";
            prompt = (_selectedOption == 0 ? "> " : "   ") + rest + "\n";
            if (grantsCityRune)
                prompt += (_selectedOption == 1 ? "> " : "   ") + "[F สำรวจรูปปั้นเมดูซ่า]\n";
            prompt += (_selectedOption == OptionCount - 1 ? "> " : "   ") +
                "[F บังคับมอนสเตอร์เกิดใหม่]\n<size=28>เลื่อนลูกกลิ้งเมาส์เพื่อเลือก</size>";
        }

        public override void Interact()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.Player == null || manager.InputBlocked || manager.Player.IsDead) return;
            if (IsBossCombatBlocked)
            {
                FloatingCombatText.Show(transform.position, "ใช้รูปปั้นไม่ได้ระหว่างต่อสู้กับบอส", Color.yellow);
                return;
            }
            if (_selectedOption == OptionCount - 1) { RespawnMonsters(); return; }
            if (grantsCityRune && _selectedOption == 1) { Examine(); return; }
            bool saved = manager.SaveAt(manager.Player.transform.position, out string error);
            FloatingCombatText.Show(transform.position, saved ? "ฟื้นฟูพลังแล้ว • บันทึกเกมสำเร็จ" : error, saved ? Color.green : Color.yellow);
        }

        private void RespawnMonsters()
        {
            int count = 0;
            foreach (var enemy in FindObjectsByType<TheLastKnight.AI.EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.gameObject.scene == gameObject.scene && enemy.ForceRespawn()) count++;
            }
            FloatingCombatText.Show(transform.position,
                count > 0 ? $"มอนสเตอร์เกิดใหม่ {count} ตัว" : "ไม่มีมอนสเตอร์ที่รอเกิดใหม่", Color.yellow);
        }

        private void Examine()
        {
            var runes = DemonRuneManager.Instance;
            var dialogue = GameManager.Instance.GetComponent<StoryDialogueUI>();
            if (runes == null || dialogue == null) return;
            bool found = !runes.HasReward(1);
            dialogue.Show("รูปปั้นเมดูซ่า", found ? new[]
            {
                "อาเธอร์\n\nเมดูซ่า… เทพผู้พิทักษ์ประจำอาณาจักรโบอา แม้บ้านเมืองจะพังทลาย ผู้คนก็ยังฝากความหวังไว้ใต้สายตาของนาง",
                "อาเธอร์\n\nตรงฐานรูปปั้นมีแสงลอดออกมาจากรอยแยก… นี่มันรูนหัตถ์ปีศาจ! ใครบางคนคงซ่อนมันไว้ภายใต้การคุ้มครองของนาง",
                "อาเธอร์\n\nข้าจะนำรูนนี้ไปปลดผนึกประตูปราสาท ขอให้การคุ้มครองของท่านนำทางข้า เมดูซ่า"
            } : new[]
            {
                "อาเธอร์\n\nเมดูซ่า เทพผู้พิทักษ์แห่งโบอา… รูนที่ซ่อนอยู่ใต้ฐานถูกค้นพบแล้ว แต่สายตาของนางยังเฝ้าดูอาณาจักรนี้อยู่"
            }, found ? (System.Action)GrantCityRune : null);
        }

        private void GrantCityRune()
        {
            if (this == null) return;
            var runes = DemonRuneManager.Instance;
            if (runes == null || runes.HasReward(1)) return;
            var inventory = InventoryManager.Instance;
            var item = ItemRegistry.CreateItem(DemonRuneManager.ItemIds[1]);
            if (inventory != null && item != null && inventory.AddItem(item) == 0)
            {
                runes.PickedUp(1);
                TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("rune");
                FloatingCombatText.Show(transform.position, "ได้รับรูนหัตถ์ปีศาจ", Color.yellow);
            }
            else if (runes.DropRune(1, transform.position))
                FloatingCombatText.Show(transform.position, "กระเป๋าเต็ม • รูนวางอยู่ข้างรูปปั้น", Color.yellow);
        }
    }
}
