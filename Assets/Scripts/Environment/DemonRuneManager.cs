using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastKnight.Inventory;
using TheLastKnight.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheLastKnight.Environment
{
    public class DemonRuneManager : MonoBehaviour
    {
        public static DemonRuneManager Instance { get; private set; }

        [Header("Rune Names")]
        public string[] runeNames = new string[4]
        {
            "Rune of the Pentagram (รูนเพนทาแกรม)",
            "Rune of the Demon Hand (รูนหัตถ์ปีศาจ)",
            "Rune of the Evil Eye (รูนเนตรอสูร)",
            "Rune of the Trident (รูนตรีศูลทมิฬ)"
        };

        [Header("Collected State")]
        private bool[] _collectedRunes => TheLastKnight.Core.GameManager.Instance.State.runes;

        public event Action OnRunesChanged;

        public static readonly string[] ItemIds = { "rune_pentagram", "rune_hand", "rune_eye", "rune_trident" };
        public static int ItemIndex(string id) => Array.IndexOf(ItemIds, id);
        public bool IsSocketed(int index) => index >= 0 && index < 4 && _collectedRunes[index];
        public bool HasReward(int index) => index >= 0 && index < 4 && (HasRune(index) ||
            (GameManager.Instance.State.runeDrops != null && GameManager.Instance.State.runeDrops.Exists(d => d != null && d.runeId == index)));

        public bool DropRune(int index, Vector3 position)
        {
            if (index < 0 || index >= 4 || HasReward(index)) return false;
            return WorldItemPickup.Spawn(ItemRegistry.CreateItem(ItemIds[index]), position) != null;
        }

        public void TrackDrop(int index, Vector3 position)
        {
            var state = GameManager.Instance.State;
            if (state.runeDrops == null) state.runeDrops = new System.Collections.Generic.List<SavedRuneDrop>();
            var drops = state.runeDrops;
            drops.RemoveAll(d => d.runeId == index);
            drops.Add(new SavedRuneDrop { runeId = index, scene = SceneManager.GetActiveScene().name, position = position });
        }

        public void PickedUp(int index)
        {
            GameManager.Instance.State.runeDrops?.RemoveAll(d => d != null && d.runeId == index);
            OnRunesChanged?.Invoke();
        }

        public void RestoreDrops()
        {
            var state = GameManager.Instance.State;
            if (state.runeDrops == null) state.runeDrops = new System.Collections.Generic.List<SavedRuneDrop>();
            foreach (var drop in state.runeDrops.ToArray())
            {
                if (drop.runeId < 0 || drop.runeId >= 4 || drop.scene != SceneManager.GetActiveScene().name) continue;
                if (HasRune(drop.runeId)) { state.runeDrops.Remove(drop); continue; }
                bool exists = false;
                foreach (var pickup in FindObjectsByType<WorldItemPickup>(FindObjectsSortMode.None))
                    if (pickup.ItemData?.id == ItemIds[drop.runeId]) { exists = true; break; }
                if (!exists) WorldItemPickup.Spawn(ItemRegistry.CreateItem(ItemIds[drop.runeId]), drop.position);
            }
        }

        public bool TrySocketRune(int index)
        {
            if (index < 0 || index >= 4 || IsSocketed(index)) return false;
            if (InventoryManager.Instance == null || !InventoryManager.Instance.TryConsumeItem(ItemIds[index])) return false;
            CollectRune(index);
            return true;
        }

        public int CollectedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _collectedRunes.Length; i++)
                {
                    if (HasRune(i)) count++;
                }
                return count;
            }
        }

        public bool HasAllRunes => CollectedCount >= 4;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public bool HasRune(int runeId)
        {
            if (runeId < 0 || runeId >= _collectedRunes.Length) return false;
            return _collectedRunes[runeId] || (InventoryManager.Instance != null && InventoryManager.Instance.CountItem(ItemIds[runeId]) > 0);
        }

        public void CollectRune(int runeId)
        {
            if (runeId < 0 || runeId >= _collectedRunes.Length) return;

            if (!_collectedRunes[runeId])
            {
                _collectedRunes[runeId] = true;
                TheLastKnight.Audio.AudioManager.Instance?.PlaySfx("rune");
                TheLastKnight.Combat.FloatingCombatText.Show(TheLastKnight.Core.GameManager.Instance.Player != null ? TheLastKnight.Core.GameManager.Instance.Player.transform.position : transform.position, "Rune acquired", Color.yellow);
                string name = (runeId < runeNames.Length) ? runeNames[runeId] : $"Rune #{runeId + 1}";
                Debug.Log($"<color=red>[DemonRuneManager]</color> รวบรวมสำเร็จ: {name} (ปัจจุบัน: {CollectedCount}/4)");
                OnRunesChanged?.Invoke();
            }
        }

        public void ToggleRune(int runeId)
        {
            if (runeId < 0 || runeId >= _collectedRunes.Length) return;
            _collectedRunes[runeId] = !_collectedRunes[runeId];
            Debug.Log($"<color=yellow>[DemonRuneManager]</color> สลับสถานะรูน {runeId + 1}: {_collectedRunes[runeId]} (รวม: {CollectedCount}/4)");
            OnRunesChanged?.Invoke();
        }

        public void CollectAllRunes()
        {
            for (int i = 0; i < _collectedRunes.Length; i++)
            {
                _collectedRunes[i] = true;
            }
            Debug.Log("<color=green>[DemonRuneManager]</color> รวบรวมรูนครบทั้ง 4 ชิ้นเรียบร้อยแล้ว!");
            OnRunesChanged?.Invoke();
        }

        public void ResetRunes()
        {
            for (int i = 0; i < _collectedRunes.Length; i++)
            {
                _collectedRunes[i] = false;
            }
            Debug.Log("<color=cyan>[DemonRuneManager]</color> รีเซ็ตสถานะรูนทั้งหมดเป็น 0/4");
            OnRunesChanged?.Invoke();
        }

        public string GetRuneSlotVisualText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            string[] shortLabels = new string[] { "ดาว", "มือ", "ตา", "ตรีศูล" };
            for (int i = 0; i < 4; i++)
            {
                string state = _collectedRunes[i] ? "<color=#FF3333>●</color>" : "<color=#888888>○</color>";
                sb.Append($"[{shortLabels[i]} {state}] ");
            }
            sb.Append($"({CollectedCount}/4)");
            return sb.ToString();
        }

        private void Update()
        {
            // Rune acquisition is driven by world interactions, never debug keys in a build.
        }

        private void HandleDebugHotkeys()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) ToggleRune(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) ToggleRune(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) ToggleRune(2);
                if (Keyboard.current.digit4Key.wasPressedThisFrame) ToggleRune(3);
                if (Keyboard.current.rKey.wasPressedThisFrame && Keyboard.current.ctrlKey.isPressed) CollectAllRunes();
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleRune(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleRune(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleRune(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleRune(3);
            if (Input.GetKeyDown(KeyCode.R) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) CollectAllRunes();
#endif
        }
    }
}
