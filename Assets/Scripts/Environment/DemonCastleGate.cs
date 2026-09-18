using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class DemonCastleGate : MonoBehaviour
    {
        [Header("Target Scene")]
        public string targetSceneName = "DemonCastle";

        [Header("Visual Elements")]
        [Tooltip("ภาพหรือ GameObject ของประตูตอนปิด")]
        public GameObject closedVisual;

        [Tooltip("ภาพหรือ GameObject ของประตูตอนเปิด")]
        public GameObject openedVisual;

        [Header("Pillar Rune Sockets (When Gate is Closed)")]
        public GameObject[] socketObjects = new GameObject[4];
        public SpriteRenderer[] socketRenderers = new SpriteRenderer[4];
        public Sprite[] unlitRuneSprites = new Sprite[4];
        public Sprite[] litRuneSprites = new Sprite[4];

        [Header("Gate State")]
        public bool isUnlocked = false;

        [Header("UI References")]
        public GameObject promptCanvas;
        public Text promptTitleText;
        public Text promptActionText;
        public Text runeSlotsText;

        private bool _playerInRange = false;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (promptCanvas != null)
            {
                promptCanvas.SetActive(false);
            }
        }

        private void Start()
        {
            UpdateVisuals();

            if (DemonRuneManager.Instance != null)
            {
                DemonRuneManager.Instance.OnRunesChanged += OnRunesChanged;
            }
        }

        private void OnDestroy()
        {
            if (DemonRuneManager.Instance != null)
            {
                DemonRuneManager.Instance.OnRunesChanged -= OnRunesChanged;
            }
        }

        private void OnRunesChanged()
        {
            UpdateVisuals();
            if (_playerInRange)
            {
                UpdatePromptUI();
            }
        }

        public void UpdateVisuals()
        {
            if (closedVisual != null)
            {
                closedVisual.SetActive(!isUnlocked);
            }

            if (openedVisual != null)
            {
                openedVisual.SetActive(isUnlocked);
            }

            if (socketObjects != null)
            {
                for (int i = 0; i < socketObjects.Length; i++)
                {
                    if (socketObjects[i] == null) continue;

                    if (isUnlocked)
                    {
                        socketObjects[i].SetActive(false);
                    }
                    else
                    {
                        socketObjects[i].SetActive(true);
                        bool hasRune = DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasRune(i);

                        if (socketRenderers != null && i < socketRenderers.Length && socketRenderers[i] != null)
                        {
                            if (hasRune && litRuneSprites != null && i < litRuneSprites.Length && litRuneSprites[i] != null)
                            {
                                socketRenderers[i].sprite = litRuneSprites[i];
                            }
                            else if (unlitRuneSprites != null && i < unlitRuneSprites.Length && unlitRuneSprites[i] != null)
                            {
                                socketRenderers[i].sprite = unlitRuneSprites[i];
                            }
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (!_playerInRange) return;

            bool fPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                fPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.F))
            {
                fPressed = true;
            }
#endif

            if (fPressed)
            {
                HandleGateInteraction();
            }
        }

        private void HandleGateInteraction()
        {
            if (!isUnlocked)
            {
                bool hasAll = DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasAllRunes;
                if (hasAll)
                {
                    // Unlock the gate!
                    isUnlocked = true;
                    UpdateVisuals();
                    UpdatePromptUI();
                    Debug.Log("<color=green>[DemonCastleGate]</color> ประตูปลดผนึกสำเร็จ! บานประตูเปิดออกสู่ความมืดมิดภายใน");
                }
                else
                {
                    int count = DemonRuneManager.Instance != null ? DemonRuneManager.Instance.CollectedCount : 0;
                    Debug.LogWarning($"[DemonCastleGate] ประตูยังถูกปิดผนึก! ต้องการรูน 4 ชิ้น (มีเพียง: {count}/4)");
                }
            }
            else
            {
                // Already unlocked -> Enter Demon Castle!
                if (!string.IsNullOrEmpty(targetSceneName))
                {
                    ScenePortal.lastPortalUsed = "Portal_Right"; // Remember which side we entered from
                    ScenePortal.lastSceneLoaded = "DemonCastleEntrance";
                    SceneManager.LoadScene(targetSceneName);
                }
                else
                {
                    Debug.LogWarning("[DemonCastleGate] Target scene name is empty!");
                }
            }
        }

        public void UpdatePromptUI()
        {
            if (promptCanvas == null) return;

            bool hasAll = DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasAllRunes;
            int count = DemonRuneManager.Instance != null ? DemonRuneManager.Instance.CollectedCount : 0;

            if (!isUnlocked)
            {
                if (promptTitleText != null)
                {
                    promptTitleText.text = "ผนึกโบราณปราสาทปีศาจ";
                }

                if (runeSlotsText != null)
                {
                    string slots = DemonRuneManager.Instance != null ? DemonRuneManager.Instance.GetRuneSlotVisualText() : $"({count}/4)";
                    runeSlotsText.text = $"ช่องใส่รูน: {slots}";
                }

                if (promptActionText != null)
                {
                    if (hasAll)
                    {
                        promptActionText.text = "<color=#FFFF00>กด [F] ปลดผนึกประตูด้วยรูนทั้ง 4</color>";
                    }
                    else
                    {
                        promptActionText.text = "<color=#FF6666>ต้องการรูน 4 ชิ้นเพื่อเปิดประตู</color>";
                    }
                }
            }
            else
            {
                if (promptTitleText != null)
                {
                    promptTitleText.text = "ทางเข้าปราสาทปีศาจ";
                }

                if (runeSlotsText != null)
                {
                    runeSlotsText.text = "<color=#66FF66>ประตูเปิดแล้ว รูนทั้ง 4 ปลดผนึกสมบูรณ์</color>";
                }

                if (promptActionText != null)
                {
                    promptActionText.text = "<color=#FFFF00>กด [F] เข้าสู่ปราสาทปีศาจ</color>";
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
                if (promptCanvas != null)
                {
                    promptCanvas.SetActive(true);
                }
                UpdatePromptUI();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = false;
                if (promptCanvas != null)
                {
                    promptCanvas.SetActive(false);
                }
            }
        }
    }
}
