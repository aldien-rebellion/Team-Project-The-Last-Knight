using UnityEngine;
using TheLastKnight.Combat;
using TheLastKnight.Audio;
using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    [RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
    public class WorldItemPickup : MonoBehaviour
    {
        [SerializeField] private InventoryItemData _itemData;
        public InventoryItemData ItemData => _itemData;

        private SpriteRenderer _spriteRenderer;
        private Vector3 _basePosition;
        private float _spawnTime;
        private bool _isSettled;
        private Vector2 _velocity;
        private float _floorY;
        private const float Gravity = -14f;
        private const float PickupDelay = 0.5f;
        private bool _collected;
        private PlayerStats _dropOwner;
        private Collider2D[] _ownerColliders;
        private bool _waitForOwnerSeparation;
        private bool _playerDropped;

        public void ConfigurePlayerDrop(PlayerStats owner)
        {
            if (owner == null) return;
            _dropOwner = owner;
            _ownerColliders = owner.GetComponentsInChildren<Collider2D>();
            _waitForOwnerSeparation = true;
            _playerDropped = true;
            var controller = owner.GetComponent<TheLastKnight.Player.PlayerController>();
            float direction = controller == null || controller.IsFacingRight ? 1f : -1f;
            _velocity = new Vector2(direction * 4.5f, 3.5f);
        }

        private void UpdateOwnerSeparation()
        {
            // Do not re-arm during the throw: the owner may still be under its landing point.
            if (!_waitForOwnerSeparation || !_isSettled) return;
            if (_dropOwner != null)
            {
                var pickupBounds = GetComponent<CircleCollider2D>().bounds;
                pickupBounds.Expand(0.2f);
                foreach (var ownerCollider in _ownerColliders)
                    if (ownerCollider != null && ownerCollider.enabled && ownerCollider.gameObject.activeInHierarchy &&
                        pickupBounds.Intersects(ownerCollider.bounds)) return;
            }
            _waitForOwnerSeparation = false;
        }

        private void Start()
        {
            // Scene-placed prefab instances resolve callbacks from the catalog too.
            if (_spriteRenderer != null || _itemData == null || string.IsNullOrEmpty(_itemData.id)) return;
            var item = ItemRegistry.CreateItem(_itemData.id, _itemData.count);
            if (item != null) Initialize(item, transform.position.y);
        }

        public static WorldItemPickup Spawn(InventoryItemData item, Vector3 position)
        {
            if (item == null || item.count <= 0) return null;

            var go = new GameObject($"Pickup_{item.id}");
            // Raycast down slightly to find ground level
            float floorLevel = position.y;
            foreach (var hit in Physics2D.RaycastAll(position + Vector3.up * 0.2f, Vector2.down, 4f))
            {
                if (hit.collider.isTrigger || hit.collider.GetComponentInParent<PlayerStats>() != null) continue;
                floorLevel = hit.point.y + 0.35f;
                break;
            }

            go.transform.position = position + Vector3.up * 0.5f;

            var pickup = go.AddComponent<WorldItemPickup>();
            pickup.Initialize(item, floorLevel);
            return pickup;
        }

        public void Initialize(InventoryItemData item, float floorLevel)
        {
            _itemData = item.Clone();
            _spawnTime = Time.time;
            _floorY = floorLevel;

            _collected = false;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null) _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = item.Icon;
            _spriteRenderer.sortingOrder = 50;

            transform.localScale = Vector3.one * 1.1f;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.45f;

            // Initial pop velocity
            float randomX = UnityEngine.Random.Range(-1.6f, 1.6f);
            float randomY = UnityEngine.Random.Range(2.5f, 4.0f);
            _velocity = new Vector2(randomX, randomY);
            _isSettled = false;
        }

        private void Update()
        {
            if (_itemData == null || string.IsNullOrEmpty(_itemData.id) || _itemData.count <= 0) return;
            if (!_isSettled)
            {
                _velocity.y += Gravity * Time.deltaTime;
                transform.position += (Vector3)(_velocity * Time.deltaTime);

                if (transform.position.y <= _floorY && _velocity.y < 0)
                {
                    transform.position = new Vector3(transform.position.x, _floorY, transform.position.z);
                    _basePosition = transform.position;
                    _isSettled = true;
                }
            }
            else
            {
                // Smooth Minecraft-style bobbing sine wave
                float bob = Mathf.Sin((Time.time - _spawnTime) * 3f) * 0.12f;
                transform.position = new Vector3(_basePosition.x, _basePosition.y + bob, _basePosition.z);
            }
            UpdateOwnerSeparation();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryPickup(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryPickup(other);
        }

        private void TryPickup(Collider2D other)
        {
            if (_collected || Time.time < _spawnTime + PickupDelay) return;
            if (_itemData == null || string.IsNullOrEmpty(_itemData.id) || _itemData.count <= 0) return;

            var player = other.GetComponent<PlayerStats>();
            if (player == null) player = other.GetComponentInParent<PlayerStats>();
            if (player == null && other.CompareTag("Player"))
            {
                player = FindAnyObjectByType<PlayerStats>();
            }

            if (player == null || player.IsDead) return;
            if (_playerDropped && !_isSettled) return;
            if (_waitForOwnerSeparation && player == _dropOwner) return;

            int initialCount = _itemData.count;
            int remaining = InventoryManager.Instance != null
                ? InventoryManager.Instance.AddItem(_itemData)
                : _itemData.count;

            int pickedUp = initialCount - remaining;
            if (pickedUp > 0)
            {
                AudioManager.Instance?.PlaySfx("click");
                FloatingCombatText.Show(transform.position + Vector3.up * 0.3f, $"+{pickedUp} {_itemData.name}", new Color(1f, 0.88f, 0.4f));

                if (remaining <= 0)
                {
                    _collected = true;
                    Destroy(gameObject);
                }
                else
                {
                    _itemData.count = remaining;
                }
            }
        }
    }
}
