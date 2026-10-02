using UnityEngine;
using TheLastKnight.Combat;
using TheLastKnight.AI;

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

        [SerializeField, Tooltip("Maximum height the character can step up without jumping.")]
        private float _stepOffset = 0.25f;

        [SerializeField, Tooltip("Layer mask for collision detection.")]
        private LayerMask _obstacleMask;

        private BoxCollider2D _boxCollider;
        private Rigidbody2D _rigidbody;
        private ContactFilter2D _contactFilter;
        private RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
        private bool _wasGroundedLastFrame;

        public bool IsGrounded { get; private set; }
        public bool HitWall { get; private set; }
        public bool HitCeiling { get; private set; }
        public bool IgnoreEnemies { get; set; }
        public bool IgnoreOneWayPlatforms { get; set; }
        public Collider2D IgnoredOneWayPlatform { get; set; }

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
            _rigidbody = GetComponent<Rigidbody2D>();

            // Ensure slopes and staircases up to 55 degrees can be climbed
            if (_maxSlopeAngle < 55f)
            {
                _maxSlopeAngle = 55f;
            }

            // Configure Rigidbody2D for custom kinematic collision handling
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.useFullKinematicContacts = true;
            _rigidbody.simulated = true;
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Setup contact filter
            _contactFilter.useTriggers = false;
            if (_obstacleMask == 0)
            {
                _obstacleMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
            }
            _contactFilter.SetLayerMask(_obstacleMask);
            _contactFilter.useLayerMask = true;
        }

        private void Start()
        {
            if (_maxSlopeAngle < 55f)
            {
                _maxSlopeAngle = 55f;
            }

            // Fallback obstacle mask
            if (_obstacleMask == 0)
            {
                _obstacleMask = Physics2D.GetLayerCollisionMask(gameObject.layer);
                _contactFilter.SetLayerMask(_obstacleMask);
            }
        }

        public void Move(Vector2 velocity, float deltaTime)
        {
            float hDelta = velocity.x * deltaTime;
            float vDelta = velocity.y * deltaTime;
            _wasGroundedLastFrame = IsGrounded;

            // Reset states before moving
            IsGrounded = false;
            HitWall = false;
            HitCeiling = false;

            // 1. Handle Horizontal (X) Movement & Slope Climbing / Step-Up
            float slopeOrStepY = 0f;
            if (Mathf.Abs(hDelta) > 0.0001f)
            {
                Vector2 hResult = HandleHorizontalMovement(hDelta);
                hDelta = hResult.x;
                slopeOrStepY = hResult.y;
            }

            // Apply horizontal and step-up movement immediately so vertical checks and slope snapping operate from new position
            _rigidbody.position += new Vector2(hDelta, slopeOrStepY);
            Physics2D.SyncTransforms();

            // 2. Handle Vertical (Y) Movement & Ground/Ceiling Checks
            float resolvedY = HandleVerticalMovement(vDelta);

            // 3. Slope Snapping: If walking downhill, snap down to slope surface to prevent launching into the air
            if (_wasGroundedLastFrame && velocity.y <= 0.001f && Mathf.Abs(hDelta) > 0.0001f && !IsGrounded)
            {
                resolvedY = HandleSlopeSnapping(resolvedY, hDelta);
            }

            // Apply final vertical movement to the Rigidbody
            _rigidbody.position += new Vector2(0f, resolvedY);
            Physics2D.SyncTransforms(); // Force transform to update immediately for LateUpdate camera

            // 4. Perform an extra post-move ground check to ensure IsGrounded state is accurate when stationary/sliding down slopes
            // NOTE: Never run ground check when character is moving upwards (jumping/rising)
            if (resolvedY <= 0.001f && slopeOrStepY <= 0.001f && vDelta <= 0.001f)
            {
                CheckGrounded();
            }
        }

        private Vector2 HandleHorizontalMovement(float xDist)
        {
            Vector2 result = new Vector2(xDist, 0f);
            Vector2 directionX = new Vector2(Mathf.Sign(xDist), 0);
            float castDistance = Mathf.Abs(xDist) + _skinWidth;

            int count = _rigidbody.Cast(directionX, _contactFilter, _hitBuffer, castDistance);
            RaycastHit2D closestHit = GetClosestValidHit(count, directionX, isHorizontal: true);

            // Step-up check: detect low obstacles, steps, or adjacent one-way platforms that can be stepped onto
            float stepUpY = 0f;
            if (IsGrounded || _wasGroundedLastFrame)
            {
                for (int i = 0; i < count; i++)
                {
                    var h = _hitBuffer[i];
                    if (h.collider != null && !h.collider.isTrigger)
                    {
                        if (IgnoreEnemies && IsEnemyCollider(h.collider)) continue;
                        if (IgnoreOneWayPlatforms && IsOneWayPlatform(h.collider)) continue;

                        float topY = GetPlatformTopY(h.collider, h.point);
                        float playerBottom = _boxCollider != null ? _boxCollider.bounds.min.y : transform.position.y;
                        float diff = topY - playerBottom;
                        if (diff > 0.001f && diff <= _stepOffset)
                        {
                            stepUpY = Mathf.Max(stepUpY, diff + _skinWidth);
                        }
                    }
                }

                if (stepUpY > 0.001f)
                {
                    // Ensure there is enough headroom before stepping up
                    int ceilCount = _rigidbody.Cast(Vector2.up, _contactFilter, _hitBuffer, stepUpY + _skinWidth);
                    RaycastHit2D ceilHit = GetClosestValidHit(ceilCount, Vector2.up);
                    if (ceilHit.collider != null)
                    {
                        stepUpY = 0f;
                    }
                }
            }

            if (closestHit.collider != null)
            {
                Vector2 normal = GetTrueSurfaceNormal(closestHit.collider, closestHit.point, closestHit.normal);
                float slopeAngle = Vector2.Angle(normal, Vector2.up);

                // Handle walkable slope climbing
                if ((IsGrounded || _wasGroundedLastFrame) && slopeAngle <= _maxSlopeAngle && normal.y > 0.001f)
                {
                    // Calculate climbing movement along the slope surface
                    float angleRad = slopeAngle * Mathf.Deg2Rad;
                    float absX = Mathf.Abs(xDist);
                    
                    // We project the horizontal movement onto the slope
                    float moveX = absX * Mathf.Cos(angleRad) * Mathf.Sign(xDist);
                    float moveY = absX * Mathf.Sin(angleRad);

                    Vector2 slopeDirection = new Vector2(normal.y, -normal.x) * Mathf.Sign(xDist);
                    int slopeCount = _rigidbody.Cast(slopeDirection, _contactFilter, _hitBuffer, absX + _skinWidth);
                    RaycastHit2D slopeHit = GetClosestValidHit(slopeCount, slopeDirection, isHorizontal: true, ignoreCollider: closestHit.collider);

                    if (slopeHit.collider != null)
                    {
                        float allowedDistance = Mathf.Max(0, slopeHit.distance - _skinWidth);
                        result = slopeDirection * allowedDistance;
                        HitWall = true;
                    }
                    else
                    {
                        result.x = moveX;
                        result.y = moveY;
                        IsGrounded = true;
                    }
                }
                else if (stepUpY > 0.001f)
                {
                    // Can step up over this low obstacle/step
                    result.x = xDist;
                    result.y = stepUpY;
                    IsGrounded = true;
                }
                else
                {
                    // It's a steep slope/wall - stop at skin width distance
                    float allowedDistance = Mathf.Max(0, closestHit.distance - _skinWidth);
                    result.x = directionX.x * allowedDistance;
                    result.y = 0f;
                    HitWall = true;
                }
            }
            else if (stepUpY > 0.001f)
            {
                result.y = stepUpY;
            }

            return result;
        }

        private float HandleVerticalMovement(float yDist)
        {
            float resolvedY = yDist;
            
            // Cast downward with enough distance (_skinWidth * 2f) to reliably detect ground when stationary or moving
            float castDistance = Mathf.Abs(yDist) + _skinWidth * 2f;
            Vector2 directionY = new Vector2(0, yDist > 0.0001f ? 1f : -1f);

            int count = _rigidbody.Cast(directionY, _contactFilter, _hitBuffer, castDistance);
            RaycastHit2D closestHit = GetClosestValidHit(count, directionY);

            if (closestHit.collider != null)
            {
                float allowedDistance = Mathf.Max(0, closestHit.distance - _skinWidth);
                
                if (directionY.y > 0)
                {
                    // Hit ceiling
                    resolvedY = allowedDistance;
                    HitCeiling = true;
                }
                else
                {
                    // Hit ground
                    if (IsOneWayPlatform(closestHit.collider))
                    {
                        Vector2 normal = GetTrueSurfaceNormal(closestHit.collider, closestHit.point, closestHit.normal);
                        float slopeAngle = Vector2.Angle(normal, Vector2.up);
                        if (slopeAngle <= 10f)
                        {
                            float platTop = GetPlatformTopY(closestHit.collider, closestHit.point);
                            float playerBottom = _boxCollider != null ? _boxCollider.bounds.min.y : transform.position.y;

                            // Only snap if feet are sunken below platform surface (e.g. landing frame / jump apex)
                            if (playerBottom < platTop - 0.001f)
                            {
                                float playerBottomOffset = _boxCollider != null ? _boxCollider.offset.y - _boxCollider.size.y * 0.5f : 0f;
                                float targetY = platTop + _skinWidth - playerBottomOffset;
                                resolvedY = targetY - transform.position.y;
                            }
                            else
                            {
                                resolvedY = -allowedDistance;
                            }
                        }
                        else
                        {
                            resolvedY = -allowedDistance;
                        }
                    }
                    else
                    {
                        resolvedY = -allowedDistance;
                    }
                    IsGrounded = true;
                }
            }

            return resolvedY;
        }

        private float HandleSlopeSnapping(float currentY, float hDelta)
        {
            float snapDistance = Mathf.Abs(hDelta) * Mathf.Tan(_maxSlopeAngle * Mathf.Deg2Rad) + _skinWidth * 2f;
            int count = _rigidbody.Cast(Vector2.down, _contactFilter, _hitBuffer, snapDistance);
            RaycastHit2D closestHit = GetClosestValidHit(count, Vector2.down);

            if (closestHit.collider != null)
            {
                Vector2 normal = GetTrueSurfaceNormal(closestHit.collider, closestHit.point, closestHit.normal);
                float slopeAngle = Vector2.Angle(normal, Vector2.up);
                if (slopeAngle <= _maxSlopeAngle && normal.y > 0.001f)
                {
                    float allowedDistance = Mathf.Max(0, closestHit.distance - _skinWidth);
                    currentY = -allowedDistance;
                    IsGrounded = true;
                }
            }

            return currentY;
        }

        private void CheckGrounded()
        {
            if (IsGrounded) return;

            // Small downward cast to detect if the character is standing on the ground
            int count = _rigidbody.Cast(Vector2.down, _contactFilter, _hitBuffer, _skinWidth * 2f);
            RaycastHit2D closestHit = GetClosestValidHit(count, Vector2.down);

            if (closestHit.collider != null)
            {
                Vector2 normal = GetTrueSurfaceNormal(closestHit.collider, closestHit.point, closestHit.normal);
                float slopeAngle = Vector2.Angle(normal, Vector2.up);
                if (slopeAngle <= _maxSlopeAngle)
                {
                    IsGrounded = true;
                }
            }
        }

        private RaycastHit2D GetClosestValidHit(int count, Vector2 direction, bool isHorizontal = false, Collider2D ignoreCollider = null)
        {
            RaycastHit2D closestHit = default;

            for (int i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (ignoreCollider != null && hit.collider == ignoreCollider) continue;

                if (Vector2.Dot(hit.normal, direction) < -0.001f)
                {
                    if (IgnoreEnemies && IsEnemyCollider(hit.collider))
                    {
                        continue;
                    }

                    if (IsOneWayPlatform(hit.collider))
                    {
                        // 1. If IgnoreOneWayPlatforms is active (user holding S+A, S+D):
                        // Pass completely through the platform/stairs!
                        if (IgnoreOneWayPlatforms)
                        {
                            continue;
                        }

                        // Specific platform being dropped through (S+Space):
                        if (IgnoredOneWayPlatform != null && (hit.collider == IgnoredOneWayPlatform || hit.collider.transform.IsChildOf(IgnoredOneWayPlatform.transform)))
                        {
                            continue;
                        }

                        // 2. One-way platforms never block upward movement (jumping through from below)
                        if (direction.y > 0.001f)
                        {
                            continue;
                        }

                        // 3. Compute true surface normal and slope angle
                        Vector2 trueNormal = GetTrueSurfaceNormal(hit.collider, hit.point, hit.normal);
                        float slopeAngle = Vector2.Angle(trueNormal, Vector2.up);
                        bool isWalkableSlope = slopeAngle <= _maxSlopeAngle && trueNormal.y > 0.001f;

                        // 4. Horizontal movement:
                        // Walkable slopes (like stairs/ramps) can be climbed.
                        // Non-slope one-way platforms (horizontal shelves, hanging platforms) NEVER block horizontal movement from sides!
                        if (isHorizontal)
                        {
                            if (!isWalkableSlope)
                            {
                                continue;
                            }
                        }

                        // 5. When moving downward or checking ground:
                        // Surface normal must face upwards (not a ceiling or vertical wall)
                        if (direction.y < -0.001f)
                        {
                            if (trueNormal.y <= 0.001f)
                            {
                                continue;
                            }

                            // For flat / near-flat platforms:
                            // Check if player's feet are above or within landing tolerance of the platform top
                            if (slopeAngle <= 10f)
                            {
                                float playerBottom = _boxCollider != null ? _boxCollider.bounds.min.y : transform.position.y;
                                float platformTopY = GetPlatformTopY(hit.collider, hit.point);
                                
                                // Landing tolerance: 0.20m (20 cm)
                                // If player jumps from below and is still more than 20cm below the top, fall through.
                                // If player's feet are within 20cm of the top or above it (apex of double jump, air dash, falling from above):
                                // Land on the platform!
                                if (playerBottom < platformTopY - 0.20f)
                                {
                                    continue;
                                }
                            }
                        }
                    }

                    // Touching the floor is not an obstruction when moving along
                    // it or away from it. Only keep surfaces opposing movement.
                    if (closestHit.collider == null || hit.distance < closestHit.distance) closestHit = hit;
                }
            }

            return closestHit;
        }

        public static float GetPlatformTopY(Collider2D col, Vector2 hitPoint)
        {
            if (col == null) return hitPoint.y;
            if (col is BoxCollider2D box)
            {
                return box.bounds.max.y;
            }
            if (col is CompositeCollider2D comp)
            {
                return comp.bounds.max.y;
            }
            if (col is PolygonCollider2D poly)
            {
                return poly.bounds.max.y;
            }
            if (col is EdgeCollider2D edge)
            {
                int count = edge.pointCount;
                for (int i = 0; i < count - 1; i++)
                {
                    Vector2 p1 = edge.transform.TransformPoint(edge.points[i] + edge.offset);
                    Vector2 p2 = edge.transform.TransformPoint(edge.points[i + 1] + edge.offset);
                    float minX = Mathf.Min(p1.x, p2.x);
                    float maxX = Mathf.Max(p1.x, p2.x);
                    if (hitPoint.x >= minX - 0.05f && hitPoint.x <= maxX + 0.05f)
                    {
                        float t = Mathf.InverseLerp(p1.x, p2.x, hitPoint.x);
                        return Mathf.Lerp(p1.y, p2.y, t);
                    }
                }
                return hitPoint.y;
            }
            return col.bounds.max.y;
        }

        public static Vector2 GetTrueSurfaceNormal(Collider2D col, Vector2 hitPoint, Vector2 fallbackNormal)
        {
            if (col is EdgeCollider2D edge)
            {
                int count = edge.pointCount;
                Vector2 bestNormal = fallbackNormal;
                float minDistanceSq = float.MaxValue;
                for (int i = 0; i < count - 1; i++)
                {
                    Vector2 p1 = edge.transform.TransformPoint(edge.points[i] + edge.offset);
                    Vector2 p2 = edge.transform.TransformPoint(edge.points[i + 1] + edge.offset);
                    Vector2 seg = p2 - p1;
                    float segLenSq = seg.sqrMagnitude;
                    if (segLenSq < 0.0001f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(hitPoint - p1, seg) / segLenSq);
                    Vector2 proj = p1 + t * seg;
                    float distSq = (hitPoint - proj).sqrMagnitude;
                    if (distSq < minDistanceSq)
                    {
                        minDistanceSq = distSq;
                        Vector2 n = new Vector2(-seg.y, seg.x).normalized;
                        if (n.y < 0) n = -n;
                        bestNormal = n;
                    }
                }
                return bestNormal;
            }
            return fallbackNormal;
        }

        public static bool IsOneWayPlatform(Collider2D col)
        {
            if (col == null) return false;
            if (col.GetComponent<TheLastKnight.Environment.OneWayPlatform2D>() != null || 
                col.GetComponentInParent<TheLastKnight.Environment.OneWayPlatform2D>() != null)
            {
                return true;
            }
            var effector = col.GetComponent<PlatformEffector2D>();
            if (effector == null) effector = col.GetComponentInParent<PlatformEffector2D>();
            if (effector != null && effector.useOneWay)
            {
                return true;
            }
            return false;
        }

        public static bool IsEnemyCollider(Collider2D col)
        {
            if (col == null) return false;

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer != -1 && col.gameObject.layer == enemyLayer) return true;
            try
            {
                if (col.CompareTag("Enemy")) return true;
            }
            catch (UnityException) { }

            // Check enemy components while ensuring player is never treated as enemy
            if (col.GetComponentInParent<EnemyStats>() != null) return true;
            if (col.GetComponentInParent<EnemyController>() != null) return true;
            if (col.GetComponentInParent<SlimeController>() != null) return true;
            if (col.GetComponentInParent<IDamageable>() != null)
            {
                if (col.GetComponentInParent<TheLastKnight.Player.PlayerController>() == null &&
                    col.GetComponentInParent<TheLastKnight.Stats.PlayerStats>() == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
