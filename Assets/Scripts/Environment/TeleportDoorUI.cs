using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheLastKnight.Environment
{
    public class TeleportDoorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Text _titleText;
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private Button _buttonTemplate;
        [SerializeField] private Button _closeButton;

        private TeleportDoor _activeDoor;
        private readonly List<Button> _instantiatedButtons = new List<Button>();
        private readonly List<DoorDestination> _currentDestinations = new List<DoorDestination>();

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Awake()
        {
            EnsureEventSystem();
            ClearButtons();

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }

            if (_buttonTemplate != null)
            {
                _buttonTemplate.gameObject.SetActive(false);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }
        }

        private void OnEnable()
        {
            ClearButtons();
        }

        private void ClearButtons()
        {
            if (_buttonContainer != null)
            {
                for (int i = _buttonContainer.childCount - 1; i >= 0; i--)
                {
                    Transform child = _buttonContainer.GetChild(i);
                    if (_buttonTemplate != null && child == _buttonTemplate.transform) continue;
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
            _instantiatedButtons.Clear();
        }

        private void EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            var uiModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            if (InputSystem.actions != null)
            {
                uiModule.actionsAsset = InputSystem.actions;
            }
#else
            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone == null)
            {
                es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
#endif
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Close on Escape
            bool cancelPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                cancelPressed = true;
#endif
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    cancelPressed = true;
            }
            catch {}

            if (cancelPressed)
            {
                Close();
                return;
            }

            // Keyboard Number Selection (1, 2, 3, 4, ...)
            int pressedIndex = GetNumberKeyPressed();
            if (pressedIndex >= 0 && pressedIndex < _currentDestinations.Count)
            {
                string chosen = _currentDestinations[pressedIndex].targetDoorId;
                TeleportDoor doorToTeleport = _activeDoor;
                Close();
                if (doorToTeleport != null)
                {
                    doorToTeleport.TeleportTo(chosen);
                }
            }
        }

        private int GetNumberKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) return 0;
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) return 1;
                if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) return 2;
                if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) return 3;
                if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) return 4;
                if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) return 5;
                if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) return 6;
                if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) return 7;
                if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) return 8;
            }
#endif
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) return 0;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) return 1;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) return 2;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) return 3;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5)) return 4;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha6) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad6)) return 5;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha7) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad7)) return 6;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha8) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad8)) return 7;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha9) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad9)) return 8;
            }
            catch {}
            return -1;
        }

        public void Open(TeleportDoor door, List<DoorDestination> destinations)
        {
            EnsureEventSystem();
            _activeDoor = door;
            _currentDestinations.Clear();

            if (destinations != null)
            {
                foreach (var d in destinations)
                {
                    if (d != null && !string.IsNullOrWhiteSpace(d.targetDoorId) &&
                        !string.Equals(d.targetDoorId, "Map_1", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(d.targetDoorId, door.doorId, StringComparison.OrdinalIgnoreCase))
                    {
                        _currentDestinations.Add(d);
                    }
                }
            }

            if (_currentDestinations.Count == 0)
            {
                return;
            }

            // If only 1 destination, warp immediately!
            if (_currentDestinations.Count == 1)
            {
                string singleDest = _currentDestinations[0].targetDoorId;
                if (_activeDoor != null)
                {
                    _activeDoor.TeleportTo(singleDest);
                }
                return;
            }

            // 1. FREEZE PLAYER INPUT COMPLETELY
            SetPlayerControlsEnabled(false);

            // 2. ENABLE UI INPUT & DISABLE PLAYER INPUT
#if ENABLE_INPUT_SYSTEM
            if (InputSystem.actions != null)
            {
                InputSystem.actions.FindActionMap("Player")?.Disable();
                InputSystem.actions.FindActionMap("UI")?.Enable();
            }
#endif

            // 3. SHOW CURSOR FOR MOUSE CLICKS
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }

            if (_titleText != null)
            {
                _titleText.text = "เลือกจุดหมาย (กดตัวเลข หรือคลิกเมาส์)";
            }

            // Clear previous or leftover buttons completely
            ClearButtons();

            if (_buttonTemplate == null || _buttonContainer == null) return;

            for (int i = 0; i < _currentDestinations.Count; i++)
            {
                var dest = _currentDestinations[i];
                var btnObj = Instantiate(_buttonTemplate, _buttonContainer);
                btnObj.gameObject.SetActive(true);

                // Button label: [1] ชื่อที่เขียนเอง
                var btnText = btnObj.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = $"[{i + 1}]  {dest.GetLabel()}";
                    btnText.raycastTarget = false; // Don't block button raycasts!
                }

                TeleportDoor doorToTeleport = _activeDoor;
                string chosenId = dest.targetDoorId;
                btnObj.onClick.AddListener(() =>
                {
                    Close();
                    if (doorToTeleport != null)
                    {
                        doorToTeleport.TeleportTo(chosenId);
                    }
                });

                _instantiatedButtons.Add(btnObj);
            }
        }

        public void Close()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }

            ClearButtons();
            _currentDestinations.Clear();

            // 1. RE-ENABLE PLAYER INPUT
            SetPlayerControlsEnabled(true);

            // 2. RE-ENABLE PLAYER ACTION MAP
#if ENABLE_INPUT_SYSTEM
            if (InputSystem.actions != null)
            {
                InputSystem.actions.FindActionMap("Player")?.Enable();
            }
#endif

            // Restore door prompt if player cancelled without teleporting and is still at door
            if (_activeDoor != null && _activeDoor.PlayerInRange)
            {
                _activeDoor.SetPromptVisible(true);
            }

            _activeDoor = null;
        }

        private void SetPlayerControlsEnabled(bool enabled)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var controller = player.GetComponent<TheLastKnight.Player.PlayerController>();
                if (controller != null)
                {
                    controller.enabled = enabled;
                    if (!enabled)
                    {
                        controller.ResetVelocity();
                    }
                }

                var handler = player.GetComponent<TheLastKnight.Input.PlayerInputHandler>();
                if (handler != null)
                {
                    handler.enabled = enabled;
                    if (enabled)
                    {
                        handler.EnablePlayerActions();
                    }
                    else
                    {
                        handler.DisablePlayerActions();
                    }
                }

                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null && !enabled)
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
    }
}
