using System;
using UnityEngine;
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
        [SerializeField] private bool[] _collectedRunes = new bool[4];

        public event Action OnRunesChanged;

        public int CollectedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _collectedRunes.Length; i++)
                {
                    if (_collectedRunes[i]) count++;
                }
                return count;
            }
        }

        public bool HasAllRunes => CollectedCount >= 4;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool HasRune(int runeId)
        {
            if (runeId < 0 || runeId >= _collectedRunes.Length) return false;
            return _collectedRunes[runeId];
        }

        public void CollectRune(int runeId)
        {
            if (runeId < 0 || runeId >= _collectedRunes.Length) return;

            if (!_collectedRunes[runeId])
            {
                _collectedRunes[runeId] = true;
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
            HandleDebugHotkeys();
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
