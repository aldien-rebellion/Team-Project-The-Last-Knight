using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TheLastKnight.Core;
using TheLastKnight.Environment;

namespace TheLastKnight.UI
{
    public class ShopUI : MonoBehaviour
    {
        private GameObject _panel;
        public bool IsOpen => _panel != null;
        private Text _balance, _message;
        private Button _rune, _potion;
        public void Open()
        {
            if (_panel != null) return;
            GameManager.Instance.SetInputBlocked(true);
            Time.timeScale = 0f;
            _panel = RuntimeUI.Panel("THE SHADOW MARKET", out var content);
            _balance = RuntimeUI.Label(content, "");
            _rune = RuntimeUI.Button(content, "Rune of the Trident — 150 Gold", () => Buy("RUNE"));
            _potion = RuntimeUI.Button(content, "Medium Healing Potion — 50 Gold", () => Buy("HEAL"));
            foreach (string stat in new[] { "STR", "VIT", "DEX", "AGI" })
            {
                string choice = stat;
                RuntimeUI.Button(content, choice + " Potion (+1) — 100 Gold", () => Buy(choice));
            }
            _message = RuntimeUI.Label(content, "Choose an item.", 18);
            RuntimeUI.Button(content, "Close", Close);
            Refresh();
        }

        public bool TryPurchase(string item)
        {
            var player = GameManager.Instance.Player;
            bool rune = item == "RUNE", heal = item == "HEAL";
            bool stat = item == "STR" || item == "VIT" || item == "DEX" || item == "AGI";
            if (player == null || (!rune && !heal && !stat)) return false;
            if (rune && DemonRuneManager.Instance.HasRune(3)) return false;
            if (heal && player.HealingPotions >= 5) return false;
            int cost = rune ? 150 : heal ? 50 : 100;
            if (!player.TrySpendGold(cost)) return false;
            if (rune) DemonRuneManager.Instance.CollectRune(3);
            else if (heal) player.AddPotion();
            else player.AddStatPotion(item);
            GameManager.Instance.Capture();
            return true;
        }

        private void Buy(string item)
        {
            _message.text = TryPurchase(item) ? "Purchased." : "Not enough gold, or already at capacity.";
            Refresh();
        }

        private void Refresh()
        {
            var player = GameManager.Instance.Player;
            _balance.text = $"Gold: {player.Gold}     Potions: {player.HealingPotions}/5";
            _rune.interactable = !DemonRuneManager.Instance.HasRune(3) && player.Gold >= 150;
            _potion.interactable = player.HealingPotions < 5 && player.Gold >= 50;
        }

        private void Update()
        {
            if (_panel != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void Close()
        {
            if (_panel != null) Destroy(_panel);
            Time.timeScale = 1f;
            GameManager.Instance.SetInputBlocked(false);
        }
    }
}
