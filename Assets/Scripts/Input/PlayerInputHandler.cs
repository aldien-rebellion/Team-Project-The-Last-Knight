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
        private InputAction _nextAction;
        private InputAction _previousAction;
        private InputAction _useDrinkAction;

        private void Start()
        {
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
            if (_useDrinkAction == null) Debug.LogWarning("[PlayerInputHandler] 'UseDrink' action not found in 'InputSystem.actions'.");
            if (_nextAction == null) Debug.LogWarning("[PlayerInputHandler] 'Next' action not found in 'InputSystem.actions'.");
            if (_previousAction == null) Debug.LogWarning("[PlayerInputHandler] 'Previous' action not found in 'InputSystem.actions'.");
        }

        public Vector2 MoveInput => _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        public bool JumpTriggered => _jumpAction != null && _jumpAction.WasPressedThisFrame();
        public bool JumpHeld => _jumpAction != null && _jumpAction.IsPressed();
        public bool SprintHeld => (_sprintAction != null && _sprintAction.IsPressed()) ||
                                  (Mouse.current != null && Mouse.current.rightButton.isPressed);
        public bool DashTriggered => _dashAction != null && _dashAction.WasPressedThisFrame();
        public bool AttackTriggered => _attackAction != null && _attackAction.WasPressedThisFrame();
        public bool CounterTriggered => _counterAttackAction != null && _counterAttackAction.WasPressedThisFrame();
        public bool UseSkillTriggered => _useSkillAction != null && _useSkillAction.WasPressedThisFrame();
        public bool UseBuffTriggered => _useBuffAction != null && _useBuffAction.WasPressedThisFrame();
        public bool UseDrinkTriggered => _useDrinkAction != null && _useDrinkAction.WasPressedThisFrame();

        public float CycleSkillInput
        {
            get
            {
                if (_nextAction != null && _nextAction.WasPressedThisFrame())
                    return 1f;
                if (_previousAction != null && _previousAction.WasPressedThisFrame())
                    return -1f;
                return 0f;
            }
        }
    }
}
