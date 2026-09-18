using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public class ScenePortal : MonoBehaviour
{
    [Header("Destination")]
    public string targetSceneName;
    public string targetPortalName = "";

    [Header("Spawn Settings")]
    public Vector3 spawnOffset = Vector3.zero;

    [Header("Invisibility")]
    [Tooltip("ซ่อน Sprite พอร์ทัลตอนเล่นเกม (Player View)")]
    [SerializeField] private bool _isInvisible = true;

    [Header("UI")]
    public GameObject popupUI;

    public static string lastSceneLoaded = "";
    public static string lastPortalUsed = "";
    public static string targetPortalExpected = "";
    public static float lastTeleportTime = -99f;

    private bool playerInRange = false;
    private SpriteRenderer _spriteRenderer;
    private BoxCollider2D _boxCollider;

    public bool IsInvisible
    {
        get { return _isInvisible; }
        set
        {
            _isInvisible = value;
            ApplyVisibility();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastSceneLoaded = "";
        lastPortalUsed = "";
        targetPortalExpected = "";
        lastTeleportTime = -99f;
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _boxCollider = GetComponent<BoxCollider2D>();
        if (_boxCollider != null)
        {
            _boxCollider.isTrigger = true;
        }
        ApplyVisibility();
    }

    private void OnValidate()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
        ApplyVisibility();
    }

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
            _spriteRenderer.color = _isInvisible ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
        }
    }

    void Start()
    {
        ApplyVisibility();

        if (popupUI != null)
        {
            popupUI.SetActive(false);
        }

        // Handle spawning player
        if (!string.IsNullOrEmpty(lastPortalUsed) || !string.IsNullOrEmpty(lastSceneLoaded) || !string.IsNullOrEmpty(targetPortalExpected))
        {
            bool isMatchingPortal = false;

            // 1. Explicit expected portal name match
            if (!string.IsNullOrEmpty(targetPortalExpected) && string.Equals(this.gameObject.name, targetPortalExpected, System.StringComparison.OrdinalIgnoreCase))
            {
                isMatchingPortal = true;
            }
            // 2. Exact match if this portal points back to the scene we just came from
            else if (string.IsNullOrEmpty(targetPortalExpected) && !string.IsNullOrEmpty(lastSceneLoaded) && string.Equals(this.targetSceneName, lastSceneLoaded, System.StringComparison.OrdinalIgnoreCase))
            {
                isMatchingPortal = true;
            }
            // 3. Classic opposite name match: Left <-> Right
            else if ((this.gameObject.name == "Portal_Left" && lastPortalUsed == "Portal_Right") ||
                     (this.gameObject.name == "Portal_Right" && lastPortalUsed == "Portal_Left"))
            {
                isMatchingPortal = true;
            }

            if (isMatchingPortal)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    Vector3 offset = spawnOffset;
                    if (offset == Vector3.zero)
                    {
                        if (this.gameObject.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            offset = new Vector3(3f, 0f, 0f);
                        }
                        else if (this.gameObject.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            offset = new Vector3(-3f, 0f, 0f);
                        }
                        else
                        {
                            offset = new Vector3(2f, 0f, 0f);
                        }
                    }

                    Vector3 newPos = this.transform.position + offset;

                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.position = newPos;
                        rb.linearVelocity = Vector2.zero;
                    }

                    var pCtrl = player.GetComponent<TheLastKnight.Player.PlayerController>();
                    if (pCtrl != null)
                    {
                        pCtrl.ResetVelocity();
                    }

                    player.transform.position = newPos;
                    Physics2D.SyncTransforms();

                    var cam = UnityEngine.Camera.main;
                    if (cam != null)
                    {
                        var follow = cam.GetComponent<TheLastKnight.Camera.CameraFollow2D>();
                        if (follow != null)
                        {
                            follow.SnapTo(newPos);
                        }
                        else
                        {
                            cam.transform.position = new Vector3(newPos.x, newPos.y, cam.transform.position.z);
                        }
                    }

                    lastPortalUsed = "";
                    lastSceneLoaded = "";
                    targetPortalExpected = "";
                }
            }
        }
    }

    private GameObject _player;
    private Collider2D _playerCollider;

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

    private void CheckPlayerOverlap()
    {
        if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider2D>();
        if (_boxCollider == null || !_boxCollider.enabled) return;

        if (_player == null || _playerCollider == null)
        {
            CachePlayer();
        }

        if (_playerCollider != null && _boxCollider.bounds.Intersects(_playerCollider.bounds))
        {
            playerInRange = true;
            if (popupUI != null)
            {
                popupUI.SetActive(true);
            }
        }
    }

    void Update()
    {
        if (!playerInRange)
        {
            CheckPlayerOverlap();
        }

        if (!playerInRange) return;

        if (Time.time - lastTeleportTime < 0.5f && Time.time >= lastTeleportTime) return;

        bool interactPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
        {
            interactPressed = true;
        }
        if (Gamepad.current != null && (Gamepad.current.buttonNorth.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame))
        {
            interactPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E))
        {
            interactPressed = true;
        }
#endif

        if (interactPressed)
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                lastTeleportTime = Time.time;
                lastSceneLoaded = SceneManager.GetActiveScene().name;
                lastPortalUsed = this.gameObject.name;
                targetPortalExpected = this.targetPortalName;

                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning($"[ScenePortal] Target scene name is empty on {gameObject.name}!");
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (popupUI != null)
            {
                popupUI.SetActive(true);
            }
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (!playerInRange)
            {
                playerInRange = true;
                if (popupUI != null)
                {
                    popupUI.SetActive(true);
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (popupUI != null)
            {
                popupUI.SetActive(false);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isInvisible ? new Color(0f, 0.8f, 1f, 0.4f) : new Color(0f, 1f, 0.5f, 0.6f);
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, Vector3.Scale(col.size, transform.lossyScale));
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }

        // Draw arrow or spawn indicator
        Vector3 offset = spawnOffset;
        if (offset == Vector3.zero)
        {
            offset = gameObject.name.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0 ? new Vector3(3f, 0f, 0f) : new Vector3(-3f, 0f, 0f);
        }
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + offset);
        Gizmos.DrawWireSphere(transform.position + offset, 0.3f);
    }
}
