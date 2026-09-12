using UnityEngine;
using TheLastKnight.Camera;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class CastleRoom : MonoBehaviour
    {
        [Header("Room Identity")]
        [SerializeField] private string _roomName = "Castle Room";

        [Header("Camera & Isolation")]
        [SerializeField] private BoxCollider2D _cameraConfiner;
        [SerializeField] private GameObject _roomContent;
        [SerializeField] private GameObject _roomOccluder;

        private BoxCollider2D _triggerCollider;
        private static CastleRoom _currentActiveRoom;

        public string RoomName => _roomName;
        public BoxCollider2D CameraConfiner => _cameraConfiner;

        private void Awake()
        {
            _triggerCollider = GetComponent<BoxCollider2D>();
            _triggerCollider.isTrigger = true;

            // Ensure we have a confiner, default to trigger if not set
            if (_cameraConfiner == null)
            {
                _cameraConfiner = _triggerCollider;
            }
        }

        private void Start()
        {
            // Initial state: if player starts inside this room, activate it
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && _triggerCollider.bounds.Contains(player.transform.position))
            {
                ActivateRoom();
            }
            else if (_roomOccluder != null)
            {
                _roomOccluder.SetActive(true);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                ActivateRoom();
            }
        }

        public void ActivateRoom()
        {
            if (_currentActiveRoom != null && _currentActiveRoom != this)
            {
                _currentActiveRoom.DeactivateRoom();
            }

            _currentActiveRoom = this;

            // Update camera boundaries to this room
            CameraFollow2D cameraFollow = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.GetComponent<CameraFollow2D>() : null;
            if (cameraFollow != null && _cameraConfiner != null)
            {
                cameraFollow.SetBoundaries(_cameraConfiner);
            }

            // Remove occluder so room is fully visible
            if (_roomOccluder != null)
            {
                _roomOccluder.SetActive(false);
            }
        }

        public void DeactivateRoom()
        {
            // Re-apply occluder so outside rooms cannot be seen
            if (_roomOccluder != null)
            {
                _roomOccluder.SetActive(true);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_cameraConfiner != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(_cameraConfiner.bounds.center, _cameraConfiner.bounds.size);
            }
        }
    }
}
