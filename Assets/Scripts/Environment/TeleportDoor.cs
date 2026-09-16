using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheLastKnight.Environment
{
    [System.Serializable]
    public class DoorDestination
    {
        [Tooltip("ID ของประตูปลายทาง (เช่น DemonCastle_2)")]
        public string targetDoorId = "";

        [Tooltip("ข้อความที่แสดงบนปุ่ม (เช่น 'ชั้น 2 ห้องทดลอง' หากเว้นว่างจะแสดงชื่อ ID)")]
        public string displayName = "";

        public DoorDestination() { }

        public DoorDestination(string targetId, string name = "")
        {
            targetDoorId = targetId;
            displayName = name;
        }

        public string GetLabel()
        {
            return !string.IsNullOrWhiteSpace(displayName) ? displayName : targetDoorId;
        }
    }

    [RequireComponent(typeof(BoxCollider2D))]
    public class TeleportDoor : MonoBehaviour
    {
        [Header("Door Identity")]
        [Tooltip("ชื่อเฉพาะของประตูนี้")]
        public string doorId = "";

        [Header("Destinations")]
        [Tooltip("รายการจุดหมายปลายทางที่เลือกไปได้ในฉากนี้")]
        public List<DoorDestination> customDestinations = new List<DoorDestination>();

        [HideInInspector]
        public List<string> targetDoorIds = new List<string>();

        [Header("Invisibility")]
        [Tooltip("ซ่อน Sprite ประตูตอนเล่นเกม")]
        [SerializeField] private bool _isInvisible = false;

        [Header("Spawn Settings")]
        [Tooltip("ระยะเยื้องจุดเกิดเมื่อผู้เล่นวาร์ปมาถึงประตูนี้")]
        [SerializeField] private Vector2 _spawnOffset = new Vector2(1.2f, 0f);

        [Header("Interaction Settings")]
        [SerializeField] private KeyCode _interactKey = KeyCode.F;
        [SerializeField] private GameObject _promptUI;
        [SerializeField] private TeleportDoorUI _selectionMenuUI;

        [Header("Room Confiner (Optional)")]
        [SerializeField] private CastleRoom _targetRoom;

        public static float LastTeleportTime = -99f;
        private static readonly List<TeleportDoor> s_ActiveDoors = new List<TeleportDoor>();

        private bool _playerInRange = false;
        private GameObject _player;
        private Collider2D _playerCollider;
        private BoxCollider2D _doorCollider;
        private SpriteRenderer _spriteRenderer;

        public bool IsInvisible
        {
            get => _isInvisible;
            set
            {
                _isInvisible = value;
                ApplyVisibility();
            }
        }

        public bool PlayerInRange => _playerInRange;

        public void SetPromptVisible(bool visible)
        {
            if (_promptUI != null)
            {
                _promptUI.SetActive(visible);
            }
        }

        public Vector3 GetSpawnPosition()
        {
            return transform.position + new Vector3(_spawnOffset.x, _spawnOffset.y, 0f);
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _doorCollider = GetComponent<BoxCollider2D>();
            if (_doorCollider != null)
            {
                _doorCollider.isTrigger = true;
            }

            if (_promptUI != null)
            {
                _promptUI.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!s_ActiveDoors.Contains(this))
            {
                s_ActiveDoors.Add(this);
            }

            ApplyVisibility();
        }

        private void OnDisable()
        {
            s_ActiveDoors.Remove(this);
        }

        private void Start()
        {
            ApplyVisibility();
            CachePlayer();
        }

        private void CachePlayer()
        {
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player");
                if (_player != null)
                {
                    _playerCollider = _player.GetComponent<Collider2D>();
                }
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            UnityEditor.EditorApplication.delayCall += EnsureUniqueId;
        }

        private void OnValidate()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            ApplyVisibility();

            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += EnsureUniqueId;
                UnityEditor.EditorApplication.delayCall += SyncAllDoorConnections;
            }
        }

        public void EnsureUniqueId()
        {
            if (this == null || gameObject == null) return;
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;

            string sceneName = gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName)) return;

            var allDoors = UnityEngine.Object.FindObjectsByType<TeleportDoor>(FindObjectsInactive.Include);

            bool isDuplicate = false;
            foreach (var d in allDoors)
            {
                if (d != null && d != this && !string.IsNullOrEmpty(d.doorId) && string.Equals(d.doorId, doorId, StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (string.IsNullOrEmpty(doorId) || doorId == "Map_1" || isDuplicate)
            {
                int maxIndex = 0;
                foreach (var d in allDoors)
                {
                    if (d == null || d == this) continue;
                    if (!string.IsNullOrEmpty(d.doorId) && d.doorId.StartsWith(sceneName + "_"))
                    {
                        string numStr = d.doorId.Substring((sceneName + "_").Length);
                        if (int.TryParse(numStr, out int num) && num > maxIndex)
                        {
                            maxIndex = num;
                        }
                    }
                }

                string newId = $"{sceneName}_{maxIndex + 1}";
                doorId = newId;
                gameObject.name = newId;
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
        }
#else
        private void OnValidate()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }
            ApplyVisibility();
        }
#endif

        public void ApplyVisibility()
        {
            if (_spriteRenderer == null) return;

            if (Application.isPlaying)
            {
                _spriteRenderer.enabled = !_isInvisible;
            }
            else
            {
                _spriteRenderer.enabled = true;
                _spriteRenderer.color = _isInvisible ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            }
        }

        public static string GetReverseLabel(string forwardLabel, string defaultId)
        {
            if (string.IsNullOrWhiteSpace(forwardLabel)) return defaultId;
            string trimmed = forwardLabel.Trim();
            if (trimmed == "ขึ้น") return "ลง";
            if (trimmed == "ลง") return "ขึ้น";
            if (trimmed == "ไป") return "กลับ";
            if (trimmed == "กลับ") return "ไป";
            if (trimmed == "เข้า") return "ออก";
            if (trimmed == "ออก") return "เข้า";
            if (trimmed == "ซ้าย") return "ขวา";
            if (trimmed == "ขวา") return "ซ้าย";
            return defaultId;
        }

        public static void SyncAllDoorConnections()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

            var allDoors = UnityEngine.Object.FindObjectsByType<TeleportDoor>(FindObjectsInactive.Include);
            if (allDoors == null || allDoors.Length == 0) return;

            var doorMap = new Dictionary<string, TeleportDoor>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in allDoors)
            {
                if (d == null) continue;
                if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(d)) continue;
                string id = !string.IsNullOrEmpty(d.doorId) ? d.doorId.Trim() : d.gameObject.name.Trim();
                if (string.IsNullOrEmpty(id) || string.Equals(id, "Map_1", StringComparison.OrdinalIgnoreCase)) continue;
                doorMap[id] = d;
            }

            // Step 1: Clean and migrate legacy connections into customDestinations
            foreach (var kvp in doorMap)
            {
                string doorIdA = kvp.Key;
                TeleportDoor doorA = kvp.Value;

                if (doorA.customDestinations == null)
                    doorA.customDestinations = new List<DoorDestination>();

                doorA.customDestinations.RemoveAll(cd =>
                    cd == null ||
                    string.IsNullOrWhiteSpace(cd.targetDoorId) ||
                    string.Equals(cd.targetDoorId.Trim(), "Map_1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cd.targetDoorId.Trim(), doorIdA, StringComparison.OrdinalIgnoreCase)
                );

                // Migrate legacy targetDoorIds
                if (doorA.targetDoorIds != null && doorA.targetDoorIds.Count > 0)
                {
                    foreach (var t in doorA.targetDoorIds)
                    {
                        if (!string.IsNullOrWhiteSpace(t) &&
                            !string.Equals(t.Trim(), "Map_1", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(t.Trim(), doorIdA, StringComparison.OrdinalIgnoreCase))
                        {
                            string tid = t.Trim();
                            if (!doorA.customDestinations.Exists(x => string.Equals(x.targetDoorId, tid, StringComparison.OrdinalIgnoreCase)))
                            {
                                doorA.customDestinations.Add(new DoorDestination(tid, tid));
                                UnityEditor.EditorUtility.SetDirty(doorA);
                            }
                        }
                    }
                    doorA.targetDoorIds.Clear();
                }
            }

            // Step 2: Bidirectional sync between all doors in the scene
            bool anyChanged = false;
            foreach (var kvp in doorMap)
            {
                string doorIdA = kvp.Key;
                TeleportDoor doorA = kvp.Value;

                for (int i = 0; i < doorA.customDestinations.Count; i++)
                {
                    var dest = doorA.customDestinations[i];
                    string targetId = dest.targetDoorId.Trim();

                    if (doorMap.TryGetValue(targetId, out TeleportDoor doorB))
                    {
                        var existingInB = doorB.customDestinations.Find(x => string.Equals(x.targetDoorId, doorIdA, StringComparison.OrdinalIgnoreCase));
                        if (existingInB == null)
                        {
                            string reverseName = GetReverseLabel(dest.displayName, doorIdA);
                            doorB.customDestinations.Add(new DoorDestination(doorIdA, reverseName));
                            UnityEditor.EditorUtility.SetDirty(doorB);
                            anyChanged = true;
                        }
                    }
                }
            }

            if (anyChanged && !Application.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }
#endif
        }

        public List<DoorDestination> GetFormattedDestinations()
        {
            if (customDestinations == null)
                customDestinations = new List<DoorDestination>();

            var result = new List<DoorDestination>();
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in customDestinations)
            {
                if (d != null && !string.IsNullOrWhiteSpace(d.targetDoorId) &&
                    !string.Equals(d.targetDoorId, doorId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(d.targetDoorId, "Map_1", StringComparison.OrdinalIgnoreCase))
                {
                    string id = d.targetDoorId.Trim();
                    if (!addedIds.Contains(id))
                    {
                        result.Add(d);
                        addedIds.Add(id);
                    }
                }
            }

            return result;
        }

        public List<string> GetAllConnectedDestinations()
        {
            var formatted = GetFormattedDestinations();
            var list = new List<string>(formatted.Count);
            foreach (var f in formatted)
            {
                list.Add(f.targetDoorId);
            }
            return list;
        }

        private void Update()
        {
            if (!_playerInRange)
            {
                CheckPlayerOverlap();
            }

            if (!_playerInRange) return;

            // Don't interact if selection menu is currently open
            if (_selectionMenuUI != null && _selectionMenuUI.IsOpen) return;

            if (IsInteractPressed())
            {
                HandleInteraction();
            }
        }

        private void CheckPlayerOverlap()
        {
            if (_doorCollider == null) _doorCollider = GetComponent<BoxCollider2D>();
            if (_doorCollider == null) return;

            if (_player == null || _playerCollider == null)
            {
                CachePlayer();
            }

            if (_playerCollider != null && _doorCollider.bounds.Intersects(_playerCollider.bounds))
            {
                _playerInRange = true;
                if (_promptUI != null && (_selectionMenuUI == null || !_selectionMenuUI.IsOpen))
                {
                    _promptUI.SetActive(true);
                }
            }
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (_interactKey == KeyCode.F && Keyboard.current.fKey.wasPressedThisFrame) return true;
                if (_interactKey == KeyCode.E && Keyboard.current.eKey.wasPressedThisFrame) return true;
                if (Keyboard.current.fKey.wasPressedThisFrame) return true;
            }
#endif
            try
            {
                if (UnityEngine.Input.GetKeyDown(_interactKey))
                    return true;
                if (UnityEngine.Input.GetKeyDown(KeyCode.F))
                    return true;
            }
            catch {}
            return false;
        }

        private void HandleInteraction()
        {
            if (Time.time - LastTeleportTime < 0.5f) return;

            var dests = GetFormattedDestinations();

            if (dests.Count == 0)
            {
                Debug.LogWarning($"[TeleportDoor] ประตู '{doorId}' ไม่มีจุดหมายปลายทางที่เชื่อมต่ออยู่!");
                return;
            }

            // If only 1 destination -> warp immediately without asking
            if (dests.Count == 1)
            {
                TeleportTo(dests[0].targetDoorId);
            }
            else
            {
                // If multiple destinations -> open selection menu
                if (_selectionMenuUI != null)
                {
                    if (_promptUI != null) _promptUI.SetActive(false);
                    _selectionMenuUI.Open(this, dests);
                }
                else
                {
                    TeleportTo(dests[0].targetDoorId);
                }
            }
        }

        public void TeleportTo(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return;
            if (Time.time - LastTeleportTime < 0.3f) return;

            LastTeleportTime = Time.time;

            if (_promptUI != null) _promptUI.SetActive(false);
            if (_selectionMenuUI != null && _selectionMenuUI.IsOpen) _selectionMenuUI.Close();

            TeleportDoor localTargetDoor = FindLocalDoor(targetId);
            if (localTargetDoor == null)
            {
                Debug.LogWarning($"[TeleportDoor] ไม่พบประตู '{targetId}' ในฉากปัจจุบัน!");
                return;
            }

            GameObject player = _player != null ? _player : GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 spawnPos = localTargetDoor.GetSpawnPosition();

            // Move Rigidbody2D and Transform
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = spawnPos;
                rb.linearVelocity = Vector2.zero;
            }

            var pCtrl = player.GetComponent<TheLastKnight.Player.PlayerController>();
            if (pCtrl != null)
            {
                pCtrl.ResetVelocity();
            }

            player.transform.position = spawnPos;
            Physics2D.SyncTransforms();

            // Snap camera
            var cam = UnityEngine.Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<TheLastKnight.Camera.CameraFollow2D>();
                if (follow != null)
                {
                    follow.SnapTo(spawnPos);
                }
                else
                {
                    cam.transform.position = new Vector3(spawnPos.x, spawnPos.y, cam.transform.position.z);
                }
            }

            // Confiner activation
            if (localTargetDoor._targetRoom != null)
            {
                localTargetDoor._targetRoom.ActivateRoom();
            }
            else
            {
                var rooms = UnityEngine.Object.FindObjectsByType<CastleRoom>(FindObjectsInactive.Include);
                foreach (var room in rooms)
                {
                    var col = room.GetComponent<BoxCollider2D>();
                    if (col != null && col.bounds.Contains(spawnPos))
                    {
                        room.ActivateRoom();
                        break;
                    }
                }
            }
        }

        public static TeleportDoor FindLocalDoor(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return null;

            for (int i = 0; i < s_ActiveDoors.Count; i++)
            {
                if (s_ActiveDoors[i] != null && (
                    string.Equals(s_ActiveDoors[i].doorId, targetId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s_ActiveDoors[i].gameObject.name, targetId, StringComparison.OrdinalIgnoreCase)))
                {
                    return s_ActiveDoors[i];
                }
            }

            var allDoors = UnityEngine.Object.FindObjectsByType<TeleportDoor>(FindObjectsInactive.Include);
            for (int i = 0; i < allDoors.Length; i++)
            {
                if (allDoors[i] != null && (
                    string.Equals(allDoors[i].doorId, targetId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(allDoors[i].gameObject.name, targetId, StringComparison.OrdinalIgnoreCase)))
                {
                    if (allDoors[i].isActiveAndEnabled && !s_ActiveDoors.Contains(allDoors[i]))
                    {
                        s_ActiveDoors.Add(allDoors[i]);
                    }
                    return allDoors[i];
                }
            }

            return null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _player = other.gameObject;
                _playerCollider = other;
                _playerInRange = true;

                if (_promptUI != null && (_selectionMenuUI == null || !_selectionMenuUI.IsOpen))
                {
                    _promptUI.SetActive(true);
                }
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                if (!_playerInRange)
                {
                    _player = other.gameObject;
                    _playerCollider = other;
                    _playerInRange = true;

                    if (_promptUI != null && (_selectionMenuUI == null || !_selectionMenuUI.IsOpen))
                    {
                        _promptUI.SetActive(true);
                    }
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = false;

                if (_promptUI != null)
                {
                    _promptUI.SetActive(false);
                }

                if (_selectionMenuUI != null && _selectionMenuUI.IsOpen)
                {
                    _selectionMenuUI.Close();
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_isInvisible)
            {
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
                var col = GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
                }
                else
                {
                    Gizmos.DrawWireCube(transform.position, Vector3.one);
                }
            }
        }
    }
}
