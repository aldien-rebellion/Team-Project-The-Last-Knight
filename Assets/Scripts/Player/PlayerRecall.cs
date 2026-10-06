using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.Input;
using TheLastKnight.Stats;
using TheLastKnight.UI;

namespace TheLastKnight.Player
{
    /// <summary>Two-second, freely interruptible recall to the latest saved Medusa point.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerRecall : MonoBehaviour
    {
        public const float ChannelDuration = 2f;
        public bool IsRecalling { get; private set; }
        public float Progress => IsRecalling ? Mathf.Clamp01(_elapsed / ChannelDuration) : 0f;
        private PlayerController _controller;
        private PlayerInputHandler _input;
        private PlayerStats _stats;
        private Vector3 _origin;
        private float _elapsed, _startHP;
        private GameObject _effect, _canvas;
        private Material _material;
        private LineRenderer _ring, _orbit, _beam;
        private readonly Vector3[] _points = new Vector3[65];
        private Image _fill;
        private Text _label;
        private float _radius, _height;
        private static readonly Color Cyan = new Color(0.25f, 0.9f, 1f);

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _stats = GetComponent<PlayerStats>();
            _input = GetComponent<PlayerInputHandler>();
            if (_input == null) _input = FindAnyObjectByType<PlayerInputHandler>();
        }

        private bool ActionRequested() => _input != null && (_input.MoveInput.sqrMagnitude > 0.01f ||
            _input.JumpHeld || _input.DashTriggered || _input.AttackTriggered || _input.CounterTriggered ||
            _input.UseSkillTriggered || _input.UseBuffTriggered || _input.UseExcaliburTriggered || _input.UseDrinkTriggered);

        private bool CanChannel()
        {
            var manager = GameManager.Instance;
            return manager != null && manager.Player == _stats && manager.CanRecall &&
                _controller != null && _controller.isActiveAndEnabled &&
                _controller.CurrentState == PlayerState.Idle && _controller.Velocity.sqrMagnitude < 0.01f;
        }

        public bool TryBeginRecall()
        {
            if (IsRecalling || !CanChannel() || ActionRequested()) return false;
            _origin = transform.position;
            _startHP = _stats.CurrentHP;
            _elapsed = 0f;
            IsRecalling = true;
            BuildVisuals();
            AnimateVisuals();
            return true;
        }

        private void LateUpdate()
        {
            if (IsRecalling)
            {
                // Check interruption before the timer, including on the completion frame.
                if (!CanChannel() || ActionRequested() || _stats.CurrentHP < _startHP ||
                    (transform.position - _origin).sqrMagnitude > 0.0001f)
                {
                    CancelRecall();
                    return;
                }
                _elapsed += Time.deltaTime;
                if (_elapsed >= ChannelDuration)
                {
                    CancelRecall();
                    GameManager.Instance.RecallToLastSave();
                    return;
                }
                AnimateVisuals();
            }
            else if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                if (!TryBeginRecall() && GameManager.Instance != null && !GameManager.Instance.InputBlocked &&
                    !_stats.IsDead && !GameManager.Instance.CanRecall)
                    TheLastKnight.Combat.FloatingCombatText.Show(transform.position,
                        GameManager.Instance.ArenaLocked ? "ไม่สามารถวาร์ประหว่างสู้บอส" : "ต้องบันทึกที่รูปปั้นเมดูซ่าก่อน", Color.yellow);
            }
        }

        public void CancelRecall()
        {
            IsRecalling = false;
            _elapsed = 0f;
            // Hide immediately, even though Destroy is deferred until the frame ends.
            if (_effect != null) { _effect.SetActive(false); Destroy(_effect); }
            if (_canvas != null) { _canvas.SetActive(false); Destroy(_canvas); }
            if (_material != null) Destroy(_material);
            _effect = _canvas = null;
            _material = null;
        }

        private void OnDisable() => CancelRecall();

        private LineRenderer Line(string name, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_effect.transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = _material;
            line.widthMultiplier = width;
            line.startColor = line.endColor = Cyan;
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) { line.sortingLayerID = sprite.sortingLayerID; line.sortingOrder = sprite.sortingOrder + order; }
            return line;
        }

        private void BuildVisuals()
        {
            var collider = GetComponent<Collider2D>();
            var bounds = collider != null ? collider.bounds : new Bounds(transform.position, new Vector3(1f, 2f, 0f));
            _radius = Mathf.Max(0.65f, bounds.extents.x * 1.7f);
            _height = Mathf.Max(1.8f, bounds.size.y * 1.3f);
            _effect = new GameObject("Recall • Arcane circle");
            _effect.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.05f, transform.position.z);
            _material = new Material(Shader.Find("Sprites/Default"));
            _ring = Line("Ground circle", 0.055f, 2);
            _orbit = Line("Rising spiral", 0.04f, 3);
            _beam = Line("Recall pillar", _radius * 1.1f, -1);
            _beam.positionCount = 2;
            _beam.SetPosition(0, Vector3.zero);
            _beam.SetPosition(1, Vector3.up * _height * 1.8f);

            _canvas = new GameObject("Recall progress", typeof(Canvas), typeof(CanvasScaler));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.GetComponent<Canvas>().sortingOrder = 85;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var background = new GameObject("Channel bar", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(_canvas.transform, false);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.13f);
            rect.sizeDelta = new Vector2(320f, 12f);
            var image = background.GetComponent<Image>();
            image.color = new Color(0.015f, 0.035f, 0.09f, 0.95f);
            image.raycastTarget = false;
            var fill = new GameObject("Progress", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            _fill = fill.GetComponent<Image>();
            _fill.color = Cyan; _fill.raycastTarget = false;
            _label = RuntimeUI.Label(background.transform, "", 20, Cyan, false);
            _label.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            _label.rectTransform.sizeDelta = new Vector2(440f, 36f);
            var hint = RuntimeUI.Label(background.transform, "ขยับหรือใช้ท่าเพื่อยกเลิก", 16);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -25f);
            hint.rectTransform.sizeDelta = new Vector2(440f, 30f);
        }

        private void AnimateVisuals()
        {
            float t = Progress;
            for (int i = 0; i < _points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (_points.Length - 1);
                float radius = _radius * (1f + 0.06f * Mathf.Sin(_elapsed * 12f));
                _points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.22f, 0f);
            }
            _ring.positionCount = _points.Length;
            _ring.SetPositions(_points);
            for (int i = 0; i < _points.Length; i++)
            {
                float p = i / (float)(_points.Length - 1);
                float angle = p * Mathf.PI * 6f - _elapsed * 7f;
                _points[i] = new Vector3(Mathf.Cos(angle) * _radius * (1f - p * 0.4f),
                    p * _height + Mathf.Sin(angle) * 0.12f, 0f);
            }
            _orbit.positionCount = _points.Length;
            _orbit.SetPositions(_points);
            _beam.startColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.08f + t * 0.16f);
            _beam.endColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
            var rect = _fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(t, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _label.text = $"กำลังกลับจุดเซฟ  {Mathf.Max(0f, ChannelDuration - _elapsed):0.0} วิ";
        }
    }
}
