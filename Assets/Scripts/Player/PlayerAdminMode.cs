using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Core;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TheLastKnight.Inventory;
using TheLastKnight.Input;
using TheLastKnight.Stats;

namespace TheLastKnight.Player
{
    /// <summary>Runtime developer panel. Hold G, then O, then D to toggle.</summary>
    public sealed class PlayerAdminMode : MonoBehaviour
    {
        private static readonly Vector2 InitialWindowSize = new Vector2(460f, 560f);
        private readonly List<InventoryItemData> _items = new List<InventoryItemData>();
        private Rect _windowRect = new Rect(24f, 24f, 460f, 560f);
        private Vector2 _itemScroll;
        private string _search = string.Empty;
        private string _statAmountText = "1";
        private string _itemAmountText = "1";
        private bool _isOpen;
        private bool _infiniteHealth;
        private bool _noCooldowns;
        private bool _statusImmunity;
        private int _toggleSequenceStep;
        private GUIStyle _rowStyle;

        public static bool IsAdminModeOpen { get; private set; }
        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToPlayer();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToPlayer();

        private static void AttachToPlayer()
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null && player.GetComponent<PlayerAdminMode>() == null)
                player.gameObject.AddComponent<PlayerAdminMode>();
        }

        private void Awake()
        {
            _windowRect.size = InitialWindowSize;
            _items.AddRange(ItemRegistry.GetAllItems());
        }

        private void OnDisable()
        {
            _toggleSequenceStep = 0;
            ApplyPermissions(false);
            if (_isOpen)
            {
                SetOpen(false);
            }
        }

        private void OnEnable() => ApplyPermissions();

        public void SetInfiniteHealth(bool enabled)
        {
            _infiniteHealth = enabled;
            ApplyPermissions();
        }

        private void ApplyPermissions(bool active = true)
        {
            var stats = GetComponent<PlayerStats>();
            var controller = GetComponent<PlayerController>();
            if (stats != null) stats.AdminInvincible = active && _infiniteHealth;
            if (controller != null)
            {
                controller.AdminNoCooldown = active && _noCooldowns;
                controller.AdminStatusImmunity = active && _statusImmunity;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) _toggleSequenceStep = 3;
        }

        private void OnDestroy()
        {
            if (_isOpen)
            {
                SetOpen(false);
            }
        }

        public void SetOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            IsAdminModeOpen = open;

            if (TheLastKnight.Core.GameManager.Instance != null)
            {
                if (_isOpen)
                {
                    TheLastKnight.Core.GameManager.Instance.SetInputBlocked(true);
                }
                else
                {
                    if (ShouldRestoreInput())
                    {
                        TheLastKnight.Core.GameManager.Instance.SetInputBlocked(false);
                    }
                }
            }
        }

        private bool ShouldRestoreInput()
        {
            if (TheLastKnight.UI.PauseMenuUI.Instance != null && TheLastKnight.UI.PauseMenuUI.Instance.IsOpen) return false;
            if (TheLastKnight.UI.CharacterStatusUI.Instance != null && TheLastKnight.UI.CharacterStatusUI.Instance.IsOpen) return false;
            var shop = FindAnyObjectByType<TheLastKnight.UI.ShopUI>();
            if (shop != null && shop.IsOpen) return false;
            return true;
        }

        private void Update()
        {
            HandleToggleInput();

            if (_isOpen)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                {
                    SetOpen(false);
                }
            }
        }

        private void HandleToggleInput()
        {
            var kb = Keyboard.current;
            if (kb == null || KeyRebindManager.IsRebinding)
            {
                _toggleSequenceStep = 3;
                return;
            }
            if (AdvanceToggleSequence(kb.gKey.isPressed, kb.gKey.wasPressedThisFrame,
                kb.oKey.isPressed, kb.oKey.wasPressedThisFrame, kb.dKey.isPressed, kb.dKey.wasPressedThisFrame))
                SetOpen(!_isOpen);
        }

        private bool AdvanceToggleSequence(bool gHeld, bool gPressed, bool oHeld, bool oPressed, bool dHeld, bool dPressed)
        {
            // After a completed/invalid chord, release every key before starting again.
            if (_toggleSequenceStep == 3)
            {
                if (!gHeld && !oHeld && !dHeld) _toggleSequenceStep = 0;
                return false;
            }
            switch (_toggleSequenceStep)
            {
                case 0:
                    if (gPressed && gHeld && !oHeld && !dHeld) _toggleSequenceStep = 1;
                    else if (gHeld || oHeld || dHeld) _toggleSequenceStep = 3;
                    break;
                case 1:
                    if (!gHeld || dHeld) _toggleSequenceStep = 3;
                    else if (oHeld && oPressed) _toggleSequenceStep = 2;
                    break;
                case 2:
                    if (!gHeld || !oHeld) _toggleSequenceStep = 3;
                    else if (dHeld && dPressed)
                    {
                        _toggleSequenceStep = 3;
                        return true;
                    }
                    break;
            }
            return false;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            Event currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.MouseDown)
            {
                if (!_windowRect.Contains(currentEvent.mousePosition))
                {
                    SetOpen(false);
                    currentEvent.Use();
                    return;
                }
            }

            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            _windowRect = GUI.Window(731942, _windowRect, DrawWindow, "PLAYER ADMIN  |  G > O > D");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(LocalizationManager.Translate("สิทธิ์แต่ละอย่างเปิดหรือปิดแยกกันได้"), GUI.skin.box);
            var stats = GetComponent<PlayerStats>();
            bool infiniteHealth = GUILayout.Toggle(_infiniteHealth, LocalizationManager.Translate("อมตะ (ไม่รับความเสียหาย)"));
            if (infiniteHealth != _infiniteHealth) SetInfiniteHealth(infiniteHealth);
            _noCooldowns = GUILayout.Toggle(_noCooldowns, LocalizationManager.Translate("ปิดคูลดาวน์สกิล / โจมตี / แดช"));
            _statusImmunity = GUILayout.Toggle(_statusImmunity, LocalizationManager.Translate("ต้านทานสตันและสถานะผิดปกติ"));
            ApplyPermissions();

            GUILayout.Space(8f);
            GUILayout.Label(LocalizationManager.Translate("เพิ่มเลเวล"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1", GUILayout.Width(70f)) && stats != null)
                stats.AddLevel();
            if (GUILayout.Button("+10", GUILayout.Width(70f)) && stats != null)
                stats.AddLevels(10);
            if (GUILayout.Button("+100", GUILayout.Width(70f)) && stats != null)
                stats.AddLevels(100);
            GUILayout.Label(stats != null ? LocalizationManager.Translate("เลเวล: " + stats.Level) : LocalizationManager.Translate("ไม่พบ PlayerStats"));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label(LocalizationManager.Translate("เพิ่ม Status Point"));
            GUILayout.BeginHorizontal();
            _statAmountText = GUILayout.TextField(_statAmountText, GUILayout.Width(90f));
            if (GUILayout.Button(LocalizationManager.Translate("เพิ่ม"), GUILayout.Width(80f)) && stats != null &&
                int.TryParse(_statAmountText, out int amount) && amount > 0)
                stats.AddStatPoints(amount);
            GUILayout.Label(stats != null ? LocalizationManager.Translate("แต้มคงเหลือ: " + stats.StatPoints) : LocalizationManager.Translate("ไม่พบ PlayerStats"));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label(LocalizationManager.Translate("เสกไอเทม  |  จำนวนต่อครั้ง"));
            _itemAmountText = GUILayout.TextField(_itemAmountText, GUILayout.Width(90f));
            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSeachTextField") ?? GUI.skin.textField);
            _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || (!string.IsNullOrWhiteSpace(_search) &&
                    !item.name.ToLowerInvariant().Contains(_search.ToLowerInvariant()) &&
                    !item.id.ToLowerInvariant().Contains(_search.ToLowerInvariant()))) continue;

                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.MinHeight(46f));
                var icon = item.Icon;
                if (icon != null) GUILayout.Label(icon.texture, GUILayout.Width(38f), GUILayout.Height(38f));
                else GUILayout.Label("□", GUILayout.Width(38f), GUILayout.Height(38f));
                GUILayout.BeginVertical();
                GUILayout.Label(LocalizationManager.Translate(item.name), _rowStyle);
                GUILayout.Label(item.id + "  ·  " + item.category, GUI.skin.label);
                GUILayout.EndVertical();
                if (GUILayout.Button(LocalizationManager.Translate("เสก"), GUILayout.Width(54f), GUILayout.Height(36f)))
                {
                    if (int.TryParse(_itemAmountText, out int count) && count > 0)
                    {
                        var inventory = InventoryManager.Instance;
                        int remaining = count;
                        while (inventory != null && remaining > 0)
                        {
                            int batchCount = Mathf.Min(remaining, Mathf.Max(1, item.maxStack));
                            var spawned = ItemRegistry.CreateItem(item.id, batchCount);
                            if (spawned == null) break;
                            int unadded = inventory.AddItem(spawned);
                            int accepted = batchCount - unadded;
                            if (accepted <= 0) break;
                            remaining -= accepted;
                        }
                        if (remaining > 0) Debug.LogWarning("Admin: inventory full; could not add " + remaining + " " + item.name + ".");
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }
    }
}
