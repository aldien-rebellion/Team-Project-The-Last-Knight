using UnityEngine;

namespace TheLastKnight.Physics
{
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public class KinematicCharacterController2D : MonoBehaviour
    {
        [Header("Collision Settings")]
        [SerializeField, Tooltip("Skin width to prevent getting stuck in colliders.")]
        private float _skinWidth = 0.015f;

        [SerializeField, Tooltip("Maximum angle of a slope the character can climb (in degrees).")]
        private float _maxSlopeAngle = 45f;

        [SerializeField, Tooltip("Layer mask for collision detection.")]
        private LayerMask _obstacleMask;

        private BoxCollider2D _boxCollider;
        private Rigidbody2D _rigidbody;
        private ContactFilter2D _contactFilter;
        private RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];

        public bool IsGrounded { get; private set; }
        public bool HitWall { get; private set; }
        public bool HitCeiling { get; private set; }

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();

            // Configure Rigidbody2D for custom kinematic collision handling
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.useFullKinematicContacts = true;
            _rigidbody.simulated = true;

            // Setup contact filter
            _contactFilter.useTriggers = false;
            _contactFilter.SetLayerMask(_obstacleMask);
            _contactFilter.useLayerMask = true;
        }

        private void Start()
        {
            // If obstacle mask wasn't assigned in the inspector, fallback to colliding with everything except the character's own layer
            if (_obstacleMask == 0)
            {
                _obstacleMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
                _contactFilter.SetLayerMask(_obstacleMask);
            }
        }

        public void Move(Vector2 velocity, float deltaTime)
        {
            Vector2 deltaPosition = velocity * deltaTime;

            // Reset states before moving
            IsGrounded = false;
            HitWall = false;
            HitCeiling = false;

            // 1. Handle Horizontal (X) Movement & Slope Climbing
            if (Mathf.Abs(deltaPosition.x) > 0.0001f)
            {
                deltaPosition = HandleHorizontalMovement(deltaPosition);
            }

            // 2. Handle Vertical (Y) Movement & Ground/Ceiling Checks
            deltaPosition = HandleVerticalMovement(deltaPosition);

            // 3. Apply final resolved movement to the Rigidbody
            _rigidbody.position += deltaPosition;

            // 4. Perform an extra post-move ground check to ensure IsGrounded state is accurate when stationary/sliding down slopes
            CheckGrounded();
        }

        private Vector2 HandleHorizontalMovement(Vector2 deltaPosition)
        {
            float xDist = deltaPosition.x;
            Vector2 directionX = new Vector2(Mathf.Sign(xDist), 0);
            float castDistance = Mathf.Abs(xDist) + _skinWidth;

            int count = _rigidbody.Cast(directionX, _contactFilter, _hitBuffer, castDistance);
            RaycastHit2D closestHit = GetClosestValidHit(count);

            if (closestHit.collider != null)
            {
                float slopeAngle = Vector2.Angle(closestHit.normal, Vector2.up);

                // Handle walkable slope climbing
                if (slopeAngle <= _maxSlopeAngle && closestHit.normal.y > 0.001f)
                {
                    // Calculate climbing movement along the slope surface
                    float angleRad = slopeAngle * Mathf.Deg2Rad;
                    float absX = Mathf.Abs(xDist);
                    
                    // We project the horizontal movement onto the slope
                    // x_slope = absX * cos(angle), y_slope = absX * sin(angle)
                    float moveX = absX * Mathf.Cos(angleRad) * Mathf.Sign(xDist);
                    float moveY = absX * Mathf.Sin(angleRad);

                    // Ensure we don't penetrate the slope collider
                    // Cast along the slope direction to verify if there's an obstacle on the slope
                    Vector2 slopeDirection = new Vector2(closestHit.normal.y, -closestHit.normal.x) * Mathf.Sign(xDist);
                    int slopeCount = _rigidbody.Cast(slopeDirection, _contactFilter, _hitBuffer, absX + _skinWidth);
                    RaycastHit2D slopeHit = GetClosestValidHit(slopeCount);

                    if (slopeHit.collider != null)
                    {
                        float allowedDistance = Mathf.Max(0, slopeHit.distance - _skinWidth);
                        deltaPosition = slopeDirection * allowedDistance;
                        HitWall = true;
                    }
                    else
                    {
                        deltaPosition.x = moveX;
                        deltaPosition.y = moveY;
                        IsGrounded = true;
                    }
                }
                else
                {
                    // It's a steep slope/wall - stop at skin width distance
                    float allowedDistance = Mathf.Max(0, closestHit.distance - _skinWidth);
                    deltaPosition.x = directionX.x * allowedDistance;
                    HitWall = true;
                }
            }

            return deltaPosition;
        }

        private Vector2 HandleVerticalMovement(Vector2 deltaPosition)
        {
            float yDist = deltaPosition.y;
            
            // If we are stationary or moving very slightly, still run checks
            float castDistance = Mathf.Abs(yDist) + _skinWidth;
            Vector2 directionY = new Vector2(0, Mathf.Sign(yDist == 0 ? -1f : yDist));

            int count = _rigidbody.Cast(directionY, _contactFilter, _hitBuffer, castDistance);
            RaycastHit2D closestHit = GetClosestValidHit(count);

            if (closestHit.collider != null)
            {
                float allowedDistance = Mathf.Max(0, closestHit.distance - _skinWidth);
                
                if (directionY.y > 0)
                {
                    // Hit ceiling
                    deltaPosition.y = allowedDistance;
                    HitCeiling = true;
                }
                else
                {
                    // Hit ground
                    deltaPosition.y = -allowedDistance;
                    IsGrounded = true;
                }
            }

            return deltaPosition;
        }

        private void CheckGrounded()
        {
            if (IsGrounded) return;

            // Small downward cast to detect if the character is standing on the ground
            int count = _rigidbody.Cast(Vector2.down, _contactFilter, _hitBuffer, _skinWidth * 2f);
            RaycastHit2D closestHit = GetClosestValidHit(count);

            if (closestHit.collider != null)
            {
                float slopeAngle = Vector2.Angle(closestHit.normal, Vector2.up);
                if (slopeAngle <= _maxSlopeAngle)
                {
                    IsGrounded = true;
                }
            }
        }

        private RaycastHit2D GetClosestValidHit(int count)
        {
            RaycastHit2D closestHit = default;

            for (int i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider != null && !hit.collider.isTrigger)
                {
                    // Since Rigidbody.Cast returns ordered list, the first non-trigger hit is the closest
                    return hit;
                }
            }

            return closestHit;
        }
    }
}
