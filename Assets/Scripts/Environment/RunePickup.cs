using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(Collider2D))]
    public class RunePickup : MonoBehaviour
    {
        [Header("Rune Identity")]
        [Tooltip("รหัสของรูน (0 ถึง 3)")]
        public int runeId = 0;

        [Tooltip("ชื่อของรูน")]
        public string runeDisplayName = "Ancient Demon Rune";

        [Header("Floating Animation")]
        [SerializeField] private float _floatSpeed = 2.5f;
        [SerializeField] private float _floatHeight = 0.2f;

        [Header("Visual & Light")]
        [SerializeField] private Light2D _runeLight;
        [SerializeField] private Color _runeColor = new Color(1f, 0.25f, 0.2f, 1f);

        private Vector3 _startPosition;
        private bool _isCollected = false;

        private void Start()
        {
            _startPosition = transform.position;

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (_runeLight != null)
            {
                _runeLight.color = _runeColor;
            }

            // If already collected previously in another map/session, disable
            if (DemonRuneManager.Instance != null && DemonRuneManager.Instance.HasRune(runeId))
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isCollected) return;

            // Smooth floating animation
            float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
            transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isCollected) return;

            if (other.CompareTag("Player"))
            {
                _isCollected = true;

                if (DemonRuneManager.Instance != null)
                {
                    DemonRuneManager.Instance.CollectRune(runeId);
                }

                Debug.Log($"<color=orange>[RunePickup]</color> ผู้เล่นเก็บรูน: {runeDisplayName} (ID: {runeId})");
                gameObject.SetActive(false);
            }
        }
    }
}
