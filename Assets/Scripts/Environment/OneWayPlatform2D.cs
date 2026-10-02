using UnityEngine;

namespace TheLastKnight.Environment
{
    /// <summary>
    /// Marks a Collider2D as a One-Way Platform.
    /// Characters can jump up through it from below and land on top.
    /// Can also be dropped down through using Down + Jump.
    /// </summary>
    [AddComponentMenu("The Last Knight/Environment/One-Way Platform 2D")]
    [RequireComponent(typeof(Collider2D))]
    public class OneWayPlatform2D : MonoBehaviour
    {
        [Tooltip("Surface top offset tolerance for landing detection.")]
        [SerializeField] private float _topTolerance = 0.15f;

        private Collider2D _collider;

        public Collider2D Collider => _collider != null ? _collider : (_collider = GetComponent<Collider2D>());

        public float TopY => Collider != null ? Collider.bounds.max.y : transform.position.y;

        public float TopTolerance => _topTolerance;

        /// <summary>
        /// Checks if the character's feet (bottom of character collider) is above or at the top surface of this platform.
        /// </summary>
        public bool IsAbovePlatform(Collider2D characterCollider)
        {
            if (Collider == null || characterCollider == null) return false;
            return characterCollider.bounds.min.y >= (Collider.bounds.max.y - _topTolerance);
        }

        /// <summary>
        /// Checks if a world Y coordinate is above or at the top surface of this platform.
        /// </summary>
        public bool IsAbovePlatform(float feetY)
        {
            if (Collider == null) return false;
            return feetY >= (Collider.bounds.max.y - _topTolerance);
        }
    }
}
