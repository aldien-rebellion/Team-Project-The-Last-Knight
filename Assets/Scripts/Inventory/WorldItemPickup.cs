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
        private float _spawnTime;
        private bool _isSettled;
        private Vector2 _velocity;
        private const float GroundClearance = 0.01f;
        // Match the usual 32 px loot icons at 100 pixels per unit and 1.1 scale.
        private const float MaxIconWorldSize = 0.352f;
        private const float DroppedIconSizeMultiplier = 2f;
        private const float PickupWorldRadius = 0.495f;
        private const float Gravity = -14f;
        private const float PickupDelay = 0.5f;
        private static float _nextInventoryFullNoticeTime;
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

        public static Vector3 GetDropPosition(Transform source)
        {
            // Monster artwork can place the root pivot below the map. Use the body feet.
            // Read local geometry because EnemyController disables colliders before loot callbacks.
            foreach (var collider in source.GetComponents<Collider2D>())
            {
                if (collider.isTrigger) continue;
                Vector2 feet;
                if (collider is CapsuleCollider2D capsule)
                    feet = capsule.offset - Vector2.up * (capsule.size.y * 0.5f);
                else if (collider is BoxCollider2D box)
                    feet = box.offset - Vector2.up * (box.size.y * 0.5f);
                else if (collider is CircleCollider2D circle)
                    feet = circle.offset - Vector2.up * circle.radius;
                else if (collider.enabled && collider.gameObject.activeInHierarchy)
                    return new Vector3(collider.bounds.center.x, collider.bounds.min.y, source.position.z);
                else
                    continue;
                return source.TransformPoint(feet);
            }
            return source.position;
        }

        public static WorldItemPickup Spawn(InventoryItemData item, Vector3 position)
        {
            if (item == null || item.count <= 0) return null;

            var go = new GameObject($"Pickup_{item.id}");
            go.transform.position = position + Vector3.up * 0.5f;

            var pickup = go.AddComponent<WorldItemPickup>();
            pickup.Initialize(item, position.y);
            int rune = TheLastKnight.Environment.DemonRuneManager.ItemIndex(item.id);
            if (rune >= 0 && TheLastKnight.Environment.DemonRuneManager.Instance != null)
                TheLastKnight.Environment.DemonRuneManager.Instance.TrackDrop(rune, position);
            return pickup;
        }

        public void Initialize(InventoryItemData item, float floorLevel)
        {
            _itemData = item.Clone();
            _spawnTime = Time.time;

            _collected = false;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null) _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = item.Icon;
            _spriteRenderer.sortingOrder = 50;

            float iconScale = 1.1f;
            if (_spriteRenderer.sprite != null)
            {
                Vector3 iconSize = _spriteRenderer.sprite.bounds.size;
                float longestSide = Mathf.Max(iconSize.x, iconSize.y);
                if (longestSide > 0f)
                    iconScale = Mathf.Min(iconScale, MaxIconWorldSize / longestSide);
            }
            // Enlarge every world drop after normalizing its original icon size.
            iconScale *= DroppedIconSizeMultiplier;
            transform.localScale = Vector3.one * iconScale;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            // Keep the pickup reach consistent when only the picture needs shrinking.
            col.radius = PickupWorldRadius / iconScale;

            // Resolve an overlapping spawn using the visual bottom, not the pickup trigger.
            float bottomOffset = transform.position.y - _spriteRenderer.bounds.min.y;
            if (TryFindGround(transform.position.x, Mathf.Max(transform.position.y, floorLevel) + 0.5f,
                bottomOffset + 1f, out float groundY) && transform.position.y - bottomOffset < groundY)
            {
                transform.position = new Vector3(transform.position.x,
                    groundY + bottomOffset + GroundClearance, transform.position.z);
            }

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
                SimulateFall(Time.deltaTime);
            }
            UpdateOwnerSeparation();
        }

        private void SimulateFall(float deltaTime)
        {
            float bottomOffset = transform.position.y - _spriteRenderer.bounds.min.y;
            Vector3 previous = transform.position;
            _velocity.y += Gravity * deltaTime;
            Vector3 next = previous + (Vector3)(_velocity * deltaTime);
            float originY = previous.y + GroundClearance;
            float distance = originY - (next.y - bottomOffset) + GroundClearance;

            // Sweep at the landing X so slopes, ledges and long falls use the actual surface.
            if (_velocity.y < 0f && TryFindGround(next.x, originY, distance, out float groundY)
                && next.y - bottomOffset <= groundY + GroundClearance)
            {
                next.y = groundY + bottomOffset + GroundClearance;
                _velocity = Vector2.zero;
                _isSettled = true;
            }
            transform.position = next;
        }

        private bool TryFindGround(float x, float originY, float distance, out float groundY)
        {
            groundY = float.NegativeInfinity;
            float halfWidth = _spriteRenderer.bounds.extents.x;
            // Check both edges too, keeping the whole icon above sloped terrain.
            for (int i = -1; i <= 1; i++)
            {
                var origin = new Vector2(x + i * halfWidth, originY);
                // A ray starting inside solid ground has no usable surface normal.
                // Query overlaps explicitly so this also works when start-in-collider hits are disabled.
                foreach (var collider in Physics2D.OverlapPointAll(origin))
                {
                    if (!IsGroundCollider(collider)) continue;
                    var bounds = collider.bounds;
                    var surfaceOrigin = new Vector2(origin.x, bounds.max.y + GroundClearance);
                    foreach (var surface in Physics2D.RaycastAll(surfaceOrigin, Vector2.down,
                        bounds.size.y + GroundClearance * 2f))
                    {
                        if (surface.collider != collider || surface.fraction <= 0f || surface.normal.y <= 0.01f) continue;
                        groundY = Mathf.Max(groundY, surface.point.y);
                        break;
                    }
                }
                foreach (var hit in Physics2D.RaycastAll(origin, Vector2.down, distance))
                {
                    var collider = hit.collider;
                    if (!IsGroundCollider(collider) || hit.fraction <= 0f || hit.normal.y <= 0.01f) continue;
                    groundY = Mathf.Max(groundY, hit.point.y);
                    break;
                }
            }
            return !float.IsNegativeInfinity(groundY);
        }

        private static bool IsGroundCollider(Collider2D collider)
        {
            return !collider.isTrigger
                && collider.GetComponentInParent<PlayerStats>() == null
                && collider.GetComponentInParent<EnemyStats>() == null
                && collider.GetComponentInParent<WorldItemPickup>() == null;
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
            var inventory = InventoryManager.Instance;
            if (inventory == null) return;
            int remaining = inventory.AddItem(_itemData);

            // Failed and partial pickups used to be silent when no slot could accept more.
            if (remaining > 0 && Time.unscaledTime >= _nextInventoryFullNoticeTime)
            {
                _nextInventoryFullNoticeTime = Time.unscaledTime + 2f;
                FloatingCombatText.Show(player.transform.position + Vector3.up,
                    "กระเป๋าเต็ม", new Color(1f, 0.65f, 0.25f));
            }

            int pickedUp = initialCount - remaining;
            if (pickedUp > 0)
            {
                int rune = TheLastKnight.Environment.DemonRuneManager.ItemIndex(_itemData.id);
                if (rune >= 0) TheLastKnight.Environment.DemonRuneManager.Instance?.PickedUp(rune);
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
