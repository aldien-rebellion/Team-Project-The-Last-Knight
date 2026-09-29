using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TheLastKnight.Inventory;
using TheLastKnight.Stats;

namespace TheLastKnight.Player
{
    /// <summary>Runtime developer panel for the local player. Toggle with F3 + G.</summary>
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
        private GUIStyle _rowStyle;

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

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.isPressed && keyboard.gKey.wasPressedThisFrame)
                _isOpen = !_isOpen;

            var stats = GetComponent<PlayerStats>();
            var controller = GetComponent<PlayerController>();
            if (stats != null) stats.AdminInvincible = _infiniteHealth;
            if (controller != null)
            {
                controller.AdminNoCooldown = _noCooldowns;
                controller.AdminStatusImmunity = _statusImmunity;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;
            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            _windowRect = GUI.Window(731942, _windowRect, DrawWindow, "PLAYER ADMIN  |  F3 + G");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("สิทธิ์แต่ละอย่างเปิดหรือปิดแยกกันได้", GUI.skin.box);
            var stats = GetComponent<PlayerStats>();
            var controller = GetComponent<PlayerController>();
            _infiniteHealth = GUILayout.Toggle(_infiniteHealth, "อมตะ (ไม่รับความเสียหาย)");
            _noCooldowns = GUILayout.Toggle(_noCooldowns, "ปิดคูลดาวน์สกิล / โจมตี / แดช");
            _statusImmunity = GUILayout.Toggle(_statusImmunity, "ต้านทานสตันและสถานะผิดปกติ");

            GUILayout.Space(8f);
            GUILayout.Label("เพิ่ม Status Point");
            GUILayout.BeginHorizontal();
            _statAmountText = GUILayout.TextField(_statAmountText, GUILayout.Width(90f));
            if (GUILayout.Button("เพิ่ม", GUILayout.Width(80f)) && stats != null &&
                int.TryParse(_statAmountText, out int amount) && amount > 0)
                stats.AddStatPoints(amount);
            GUILayout.Label(stats != null ? "คงเหลือ: " + stats.StatPoints : "ไม่พบ PlayerStats");
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("เสกไอเทม  |  จำนวนต่อครั้ง");
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
                GUILayout.Label(item.name, _rowStyle);
                GUILayout.Label(item.id + "  ·  " + item.category, GUI.skin.label);
                GUILayout.EndVertical();
                if (GUILayout.Button("เสก", GUILayout.Width(54f), GUILayout.Height(36f)))
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
