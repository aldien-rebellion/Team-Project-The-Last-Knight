using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(BoxCollider2D))]
public class ScenePortal : MonoBehaviour
{
    public string targetSceneName;
    public GameObject popupUI;
    
    public static string lastPortalUsed = "";
    private bool playerInRange = false;

    void Start()
    {
        if (popupUI != null)
        {
            popupUI.SetActive(false);
        }

        // Move player to this portal if they just came from the opposite side
        if (!string.IsNullOrEmpty(lastPortalUsed))
        {
            if ((this.gameObject.name == "Portal_Left" && lastPortalUsed == "Portal_Right") ||
                (this.gameObject.name == "Portal_Right" && lastPortalUsed == "Portal_Left"))
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    // Move slightly in front of the portal so they don't immediately trigger it again
                    float offset = (this.gameObject.name == "Portal_Left") ? 3f : -3f;
                    Vector3 newPos = this.transform.position + new Vector3(offset, 0, 0);
                    player.transform.position = newPos;
                    lastPortalUsed = ""; // Clear after spawning
                }
            }
        }
    }

    void Update()
    {
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

        if (playerInRange && fPressed)
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                lastPortalUsed = this.gameObject.name; // Remember which portal we entered
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("Target scene name is empty!");
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
}
