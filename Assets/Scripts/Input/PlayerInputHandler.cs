using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastKnight.Input
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInputHandler : MonoBehaviour
    {
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private InputAction _dashAction;
        private InputAction _attackAction;
        private InputAction _counterAttackAction;
        private InputAction _useSkillAction;
        private InputAction _useBuffAction;
        private InputAction _useExcaliburAction;
        private InputAction _nextAction;
        private InputAction _previousAction;
        private InputAction _useDrinkAction;

        private void Awake()
        {
            InitializeActions();
        }

        private void OnEnable()
        {
            InitializeActions();
            EnablePlayerActions();
        }

        private void Start()
        {
            InitializeActions();
            EnablePlayerActions();
        }

        private void InitializeActions()
        {
            if (_moveAction != null) return;
            if (InputSystem.actions == null)
            {
                Debug.LogError("[PlayerInputHandler] InputSystem.actions is null! Make sure the project-wide Input Actions asset is assigned.");
                return;
            }

            _moveAction = InputSystem.actions.FindAction("Move");
            _jumpAction = InputSystem.actions.FindAction("Jump");
            _sprintAction = InputSystem.actions.FindAction("Sprint");
            _dashAction = InputSystem.actions.FindAction("Dash");
            _attackAction = InputSystem.actions.FindAction("Attack");
            _counterAttackAction = InputSystem.actions.FindAction("CounterAttack");
            _useSkillAction = InputSystem.actions.FindAction("UseSkill");
            _useBuffAction = InputSystem.actions.FindAction("UseBuff");
            _useExcaliburAction = InputSystem.actions.FindAction("UseExcalibur");
            _useDrinkAction = InputSystem.actions.FindAction("UseDrink");
            _nextAction = InputSystem.actions.FindAction("Next");
            _previousAction = InputSystem.actions.FindAction("Previous");

            if (_moveAction == null) Debug.LogWarning("[PlayerInputHandler] 'Move' action not found in 'InputSystem.actions'.");
            if (_jumpAction == null) Debug.LogWarning("[PlayerInputHandler] 'Jump' action not found in 'InputSystem.actions'.");
            if (_sprintAction == null) Debug.LogWarning("[PlayerInputHandler] 'Sprint' action not found in 'InputSystem.actions'.");
            if (_dashAction == null) Debug.LogWarning("[PlayerInputHandler] 'Dash' action not found in 'InputSystem.actions'.");
            if (_attackAction == null) Debug.LogWarning("[PlayerInputHandler] 'Attack' action not found in 'InputSystem.actions'.");
            if (_counterAttackAction == null) Debug.LogWarning("[PlayerInputHandler] 'CounterAttack' action not found in 'InputSystem.actions'.");
            if (_useSkillAction == null) Debug.LogWarning("[PlayerInputHandler] 'UseSkill' action not found in 'InputSystem.actions'.");
            if (_useBuffAction == null) Debug.LogWarning("[PlayerInputHandler] 'UseBuff' action not found in 'InputSystem.actions'.");
            if (_useExcaliburAction == null) Debug.LogWarning("[PlayerInputHandler] 'UseExcalibur' action not found in 'InputSystem.actions'.");
            if (_useDrinkAction == null) Debug.LogWarning("[PlayerInputHandler] 'UseDrink' action not found in 'InputSystem.actions'.");
            if (_nextAction == null) Debug.LogWarning("[PlayerInputHandler] 'Next' action not found in 'InputSystem.actions'.");
            if (_previousAction == null) Debug.LogWarning("[PlayerInputHandler] 'Previous' action not found in 'InputSystem.actions'.");
        }

        public void EnablePlayerActions()
        {
            if (InputSystem.actions != null)
            {
                var playerMap = InputSystem.actions.FindActionMap("Player");
                if (playerMap != null && !playerMap.enabled)
                {
                    playerMap.Enable();
                }
            }

            _moveAction?.Enable();
            _jumpAction?.Enable();
            _sprintAction?.Enable();
            _dashAction?.Enable();
            _attackAction?.Enable();
            _counterAttackAction?.Enable();
            _useSkillAction?.Enable();
            _useBuffAction?.Enable();
            _useExcaliburAction?.Enable();
            _useDrinkAction?.Enable();
            _nextAction?.Enable();
            _previousAction?.Enable();
        }

        private void OnDisable()
        {
            DisablePlayerActions();
        }

        public void DisablePlayerActions()
        {
            if (InputSystem.actions != null)
            {
                var playerMap = InputSystem.actions.FindActionMap("Player");
                if (playerMap != null && playerMap.enabled)
                {
                    playerMap.Disable();
                }
            }

            _moveAction?.Disable();
            _jumpAction?.Disable();
            _sprintAction?.Disable();
            _dashAction?.Disable();
            _attackAction?.Disable();
            _counterAttackAction?.Disable();
            _useSkillAction?.Disable();
            _useBuffAction?.Disable();
            _useExcaliburAction?.Disable();
            _useDrinkAction?.Disable();
            _nextAction?.Disable();
            _previousAction?.Disable();
        }

        public Vector2 MoveInput => (enabled && _moveAction != null && _moveAction.enabled) ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        public bool JumpTriggered => enabled && _jumpAction != null && _jumpAction.enabled && _jumpAction.WasPressedThisFrame();
        public bool JumpHeld => enabled && _jumpAction != null && _jumpAction.enabled && _jumpAction.IsPressed();
        public bool SprintHeld => enabled && ((_sprintAction != null && _sprintAction.enabled && _sprintAction.IsPressed()) ||
                                  (Mouse.current != null && Mouse.current.rightButton.isPressed));
        public bool DashTriggered => enabled && _dashAction != null && _dashAction.enabled && _dashAction.WasPressedThisFrame();
        public bool AttackTriggered => enabled && _attackAction != null && _attackAction.enabled && _attackAction.WasPressedThisFrame();
        public bool CounterTriggered => enabled && _counterAttackAction != null && _counterAttackAction.enabled && _counterAttackAction.WasPressedThisFrame();
        public bool UseSkillTriggered => enabled && _useSkillAction != null && _useSkillAction.enabled && _useSkillAction.WasPressedThisFrame();
        public bool UseBuffTriggered => enabled && _useBuffAction != null && _useBuffAction.enabled && _useBuffAction.WasPressedThisFrame();
        public bool UseExcaliburTriggered => enabled && _useExcaliburAction != null && _useExcaliburAction.enabled && _useExcaliburAction.WasPressedThisFrame();
        public bool UseDrinkTriggered => enabled && _useDrinkAction != null && _useDrinkAction.enabled && _useDrinkAction.WasPressedThisFrame();

        public float CycleSkillInput
        {
            get
            {
                if (!enabled) return 0f;
                if (_nextAction != null && _nextAction.enabled && _nextAction.WasPressedThisFrame())
                    return 1f;
                if (_previousAction != null && _previousAction.enabled && _previousAction.WasPressedThisFrame())
                    return -1f;
                return 0f;
            }
        }
    }
}
