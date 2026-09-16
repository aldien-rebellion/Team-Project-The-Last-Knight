using UnityEngine;
using TheLastKnight.Camera;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class RoomDoorway : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField] private Transform _targetSpawnPoint;
        [SerializeField] private CastleRoom _targetRoom;

        [Header("Doorway Settings")]
        [SerializeField] private bool _autoTransitionOnEnter = true;
        [SerializeField] private KeyCode _interactKey = KeyCode.F;
        [SerializeField] private GameObject _promptUI;

        private bool _playerInRange = false;
        private GameObject _player;

        public Transform TargetSpawnPoint
        {
            get => _targetSpawnPoint;
            set => _targetSpawnPoint = value;
        }

        public CastleRoom TargetRoom
        {
            get => _targetRoom;
            set => _targetRoom = value;
        }

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            if (_promptUI != null)
            {
                _promptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_autoTransitionOnEnter && _playerInRange)
            {
                if (UnityEngine.Input.GetKeyDown(_interactKey))
                {
                    PerformTransition();
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _player = other.gameObject;
                _playerInRange = true;

                if (_autoTransitionOnEnter)
                {
                    PerformTransition();
                }
                else if (_promptUI != null)
                {
                    _promptUI.SetActive(true);
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
            }
        }

        private void PerformTransition()
        {
            if (_player == null || _targetSpawnPoint == null) return;

            // Teleport player
            _player.transform.position = _targetSpawnPoint.position;

            // Activate new room & confiner
            if (_targetRoom != null)
            {
                _targetRoom.ActivateRoom();
            }

            if (_promptUI != null)
            {
                _promptUI.SetActive(false);
            }
        }
    }
}
