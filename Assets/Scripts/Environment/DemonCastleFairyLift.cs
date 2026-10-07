using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastKnight.Core;
using TheLastKnight.Input;
using TheLastKnight.Player;
using TheLastKnight.Physics;
using TheLastKnight.Stats;

namespace TheLastKnight.Environment
{
    /// <summary>Scene-owned, interruptible fairy lift from the grand hall's central shelf.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DemonCastleFairyLift : MonoBehaviour
    {
        [SerializeField] private Collider2D _sourceShelf;
        [SerializeField] private Collider2D _leftLanding;
        [SerializeField] private Collider2D _rightLanding;
        [SerializeField] private RuntimeAnimatorController _fairyAnimation;
        [SerializeField] private Sprite[] _firstFrames = new Sprite[3];
        [SerializeField] private Sprite _dustSprite;
        [SerializeField] private Rect _interior = new Rect(-110.1f, 27.4f, 38f, 19.1f);
        [SerializeField] private float _idleSeconds = 2f;
        [SerializeField] private float _approachSeconds = 0.9f;
        [SerializeField] private float _flightSpeed = 3.5f;
        [SerializeField] private float _lingerSeconds = 2f;
        [SerializeField] private float _fairyScale = 2.2f;

        public enum LiftPhase { Waiting, Approaching, Carrying, Lingering }
        public LiftPhase Phase { get; private set; }
        public int LastLandingIndex { get; private set; } = -1;
        public int LeftColor { get; private set; }
        public int RightColor { get; private set; }
        public float IdleElapsed => _idleElapsed;
        public int ActiveFairyCount => (_fairies[0] != null ? 1 : 0) + (_fairies[1] != null ? 1 : 0);
        private PlayerController _player;
        private PlayerInputHandler _input;
        private PlayerStats _stats;
        private KinematicCharacterController2D _motor;
        private BoxCollider2D _body;
        private Rigidbody2D _rigidbody;
        private readonly GameObject[] _fairies = new GameObject[2];
        private readonly SpriteRenderer[] _renderers = new SpriteRenderer[2];
        private readonly Vector2[] _spawn = new Vector2[2];
        private List<Vector2> _route;
        private int _waypoint;
        private float _idleElapsed, _elapsed, _startHP;
        private Vector2 _expectedPosition, _idlePosition;
        private bool _hasIdlePosition;
        private readonly Color[] _dustColors = { new Color(0.3f, 1f, 1f), new Color(1f, 0.75f, 0.3f), new Color(0.5f, 1f, 0.45f) };

        private bool BindPlayer()
        {
            if (_player != null) return true;
            var manager = GameManager.Instance;
            _player = manager != null && manager.Player != null ? manager.Player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
            if (_player == null) return false;
            _input = _player.GetComponent<PlayerInputHandler>();
            _stats = _player.GetComponent<PlayerStats>();
            _motor = _player.GetComponent<KinematicCharacterController2D>();
            _body = _player.GetComponent<BoxCollider2D>();
            _rigidbody = _player.GetComponent<Rigidbody2D>();
            return _body != null && _motor != null && _stats != null;
        }

        private bool InputRequested()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0.01f)) return true;
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            if (map != null)
                foreach (var action in map.actions)
                    if (action.enabled && (action.IsPressed() || action.WasPerformedThisFrame())) return true;
            return _input != null && (_input.MoveInput.sqrMagnitude > 0.01f || _input.JumpHeld || _input.SprintHeld ||
                _input.DashTriggered || _input.AttackTriggered || _input.CounterTriggered || _input.UseSkillTriggered ||
                _input.UseBuffTriggered || _input.UseExcaliburTriggered || _input.UseDrinkTriggered || Mathf.Abs(_input.CycleSkillInput) > 0f);
        }

        private bool Available()
        {
            var manager = GameManager.Instance;
            var recall = _player.GetComponent<PlayerRecall>();
            return _stats != null && !_stats.IsDead && _player.isActiveAndEnabled && _motor.isActiveAndEnabled &&
                (manager == null || !manager.InputBlocked) && (recall == null || !recall.IsRecalling) &&
                _player.CurrentState == PlayerState.Idle;
        }

        private bool StandingOnSource()
        {
            if (_sourceShelf == null || !_sourceShelf.enabled || !_motor.IsGrounded) return false;
            var b = _body.bounds;
            var shelf = _sourceShelf.bounds;
            return b.center.x >= shelf.min.x && b.center.x <= shelf.max.x && Mathf.Abs(b.min.y - shelf.max.y) < 0.06f &&
                _player.Velocity.sqrMagnitude < 0.01f;
        }

        private void Update()
        {
            if (!BindPlayer() || Time.deltaTime <= 0f) return;
            if (Phase == LiftPhase.Lingering)
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= _lingerSeconds) FinishFairies(true);
                return;
            }
            bool interrupted = !Available() || InputRequested();
            if (Phase != LiftPhase.Waiting)
            {
                interrupted |= _stats.CurrentHP < _startHP || Vector2.Distance(_player.transform.position, _expectedPosition) > 0.035f;
                if (interrupted) { CancelLift(); return; }
                if (Phase == LiftPhase.Approaching) Approach();
                else Carry();
                return;
            }
            if (interrupted || !StandingOnSource()) { ResetIdle(); return; }
            var position = (Vector2)_player.transform.position;
            if (!_hasIdlePosition) { _idlePosition = position; _hasIdlePosition = true; }
            if (Vector2.Distance(position, _idlePosition) > 0.02f) { ResetIdle(); return; }
            _idleElapsed += Time.deltaTime;
            if (_idleElapsed >= _idleSeconds)
            {
                if (!TryStartLift()) ResetIdle();
            }
        }

        private void ResetIdle() { _idleElapsed = 0f; _hasIdlePosition = false; }

        private Vector2 Shoulder(int side, Vector2 playerPosition)
        {
            var bounds = _body.bounds;
            Vector2 centerOffset = (Vector2)bounds.center - (Vector2)_player.transform.position;
            return playerPosition + centerOffset + new Vector2((side == 0 ? -1f : 1f) * (bounds.extents.x + 0.22f), bounds.extents.y * 0.65f);
        }

        private Vector2 FairySize => Vector2.one * (0.32f * _fairyScale);

        private bool IsObstacle(Collider2D collider)
        {
            return collider != null && collider.enabled && !collider.isTrigger && !collider.transform.IsChildOf(_player.transform);
        }

        private bool FreeBox(Vector2 center, Vector2 size)
        {
            var half = size * 0.5f;
            if (center.x - half.x < _interior.xMin || center.x + half.x > _interior.xMax ||
                center.y - half.y < _interior.yMin || center.y + half.y > _interior.yMax) return false;
            var boxBounds = new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 1f));
            // Physics queries include a contact halo. Standing at the motor's skin
            // width above a shelf is clear space, even when that halo touches it.
            foreach (var collider in Physics2D.OverlapBoxAll(center, size, 0f))
                if (IsObstacle(collider) && collider.bounds.Intersects(boxBounds)) return false;
            return true;
        }

        private bool FreeSweep(Vector2 from, Vector2 to, Vector2 size)
        {
            if (!FreeBox(from, size) || !FreeBox(to, size)) return false;
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return true;
            foreach (var hit in Physics2D.BoxCastAll(from, size, 0f, delta.normalized, delta.magnitude))
                if (IsObstacle(hit.collider) && !(delta.y >= 0f && from.y - size.y * 0.5f >= hit.collider.bounds.max.y)) return false;
            return true;
        }

        private bool SafeTravel(Vector2 from, Vector2 to)
        {
            var offset = (Vector2)_body.bounds.center - (Vector2)_player.transform.position;
            if (!FreeSweep(from + offset, to + offset, (Vector2)_body.bounds.size)) return false;
            for (int i = 0; i < 2; i++)
                if (!FreeSweep(Shoulder(i, from), Shoulder(i, to), FairySize)) return false;
            return true;
        }

        public bool TryBuildRoute(int landingIndex, out List<Vector2> route)
        {
            route = null;
            if (!BindPlayer()) return false;
            var platform = landingIndex == 0 ? _leftLanding : _rightLanding;
            if (platform == null || !platform.enabled) return false;
            var bounds = _body.bounds;
            var shelf = platform.bounds;
            float bottomOffset = bounds.min.y - _player.transform.position.y;
            float inset = bounds.extents.x + 0.85f;
            var destination = new Vector2(landingIndex == 0 ? shelf.max.x - inset : shelf.min.x + inset, shelf.max.y + 0.045f - bottomOffset);
            var start = (Vector2)_player.transform.position;
            // Lift in the open centre, cross above both shelf tops, then gently settle.
            float cruiseY = Mathf.Max(_leftLanding.bounds.max.y, _rightLanding.bounds.max.y) + 0.16f - bottomOffset;
            float liftX = _sourceShelf.bounds.center.x;
            var points = new List<Vector2> { new Vector2(liftX, start.y), new Vector2(liftX, cruiseY), new Vector2(destination.x, cruiseY), destination };
            var previous = start;
            foreach (var point in points) { if (!SafeTravel(previous, point)) return false; previous = point; }
            route = points;
            return true;
        }

        private bool FindSpawn(int side, out Vector2 position)
        {
            var shoulder = Shoulder(side, _player.transform.position);
            for (int height = 0; height < 5; height++)
                for (int spread = 0; spread < 4; spread++)
                {
                    var candidate = shoulder + new Vector2((side == 0 ? -1f : 1f) * (0.55f + spread * 0.2f), 1.25f + height * 0.25f);
                    if (!FreeSweep(candidate, shoulder, FairySize)) continue;
                    position = candidate;
                    return true;
                }
            position = Vector2.zero;
            return false;
        }

        public bool TryStartLift()
        {
            if (Phase != LiftPhase.Waiting || !BindPlayer() || !Available() || InputRequested() || !StandingOnSource() ||
                _fairyAnimation == null || _firstFrames.Length < 3) return false;
            for (int i = 0; i < 3; i++) if (_firstFrames[i] == null) return false;
            int landing = Random.Range(0, 2);
            if (!TryBuildRoute(landing, out _route) || !FindSpawn(0, out _spawn[0]) || !FindSpawn(1, out _spawn[1])) return false;
            LastLandingIndex = landing;
            LeftColor = Random.Range(0, 3);
            // Draw two different fairies from the three-colour cast on every visit.
            RightColor = (LeftColor + Random.Range(1, 3)) % 3;
            _expectedPosition = _player.transform.position;
            _startHP = _stats.CurrentHP;
            _elapsed = 0f;
            Phase = LiftPhase.Approaching;
            for (int i = 0; i < 2; i++) CreateFairy(i, i == 0 ? LeftColor : RightColor);
            ResetIdle();
            return true;
        }

        private void CreateFairy(int side, int color)
        {
            var fairy = new GameObject(side == 0 ? "Lift Fairy Left" : "Lift Fairy Right");
            fairy.transform.SetParent(transform, false);
            fairy.transform.position = new Vector3(_spawn[side].x, _spawn[side].y, _player.transform.position.z);
            fairy.transform.localScale = Vector3.one * _fairyScale;
            var renderer = fairy.AddComponent<SpriteRenderer>();
            renderer.sprite = _firstFrames[color];
            renderer.flipX = side == 1;
            var playerSprite = _player.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = playerSprite.sortingLayerID;
            renderer.sortingOrder = playerSprite.sortingOrder + 3;
            var animator = fairy.AddComponent<Animator>();
            animator.runtimeAnimatorController = _fairyAnimation;
            animator.SetInteger("ActionIndex", color);
            animator.Play("Fly_" + (color + 1), 0, Random.value);
            _fairies[side] = fairy;
            _renderers[side] = renderer;
            Dust(side);
        }

        private void Dust(int side)
        {
            var renderer = _renderers[side];
            if (renderer == null) return;
            FairyPixieDust.Burst(renderer.transform.position, _dustSprite, _dustColors[side == 0 ? LeftColor : RightColor], renderer.sortingLayerID, renderer.sortingOrder + 1);
        }

        private void Approach()
        {
            if (!StandingOnSource()) { CancelLift(); return; }
            _elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, _elapsed / _approachSeconds);
            for (int i = 0; i < 2; i++)
            {
                var next = Vector2.Lerp(_spawn[i], Shoulder(i, _expectedPosition), progress);
                if (!FreeSweep(_fairies[i].transform.position, next, FairySize)) { CancelLift(); return; }
                _fairies[i].transform.position = new Vector3(next.x, next.y, _player.transform.position.z);
            }
            if (_elapsed < _approachSeconds) return;
            if (!_player.TryBeginFairyCarry(this)) { CancelLift(); return; }
            Phase = LiftPhase.Carrying;
            _waypoint = 0;
        }

        private void Carry()
        {
            if (!_player.IsFairyCarried) { CancelLift(); return; }
            var target = _route[_waypoint];
            var next = Vector2.MoveTowards(_expectedPosition, target, _flightSpeed * Time.deltaTime);
            if (!SafeTravel(_expectedPosition, next)) { CancelLift(); return; }
            _rigidbody.position = next;
            _player.transform.position = new Vector3(next.x, next.y, _player.transform.position.z);
            Physics2D.SyncTransforms();
            _expectedPosition = next;
            for (int i = 0; i < 2; i++)
            {
                var shoulder = Shoulder(i, next);
                _fairies[i].transform.position = new Vector3(shoulder.x, shoulder.y, _player.transform.position.z);
            }
            if (Vector2.Distance(next, target) > 0.001f) return;
            if (++_waypoint < _route.Count) return;
            _player.EndFairyCarry(this);
            _motor.Move(Vector2.down, 0.05f);
            Phase = LiftPhase.Lingering;
            _elapsed = 0f;
        }

        public void CancelLift()
        {
            if (_player != null) _player.EndFairyCarry(this);
            FinishFairies(true);
        }

        private void FinishFairies(bool dust)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_fairies[i] == null) continue;
                if (dust) Dust(i);
                _fairies[i].SetActive(false);
                Destroy(_fairies[i]);
                _fairies[i] = null;
                _renderers[i] = null;
            }
            Phase = LiftPhase.Waiting;
            ResetIdle();
        }

        private void OnDisable()
        {
            if (_player != null) _player.EndFairyCarry(this);
            FinishFairies(false);
        }
    }
}
