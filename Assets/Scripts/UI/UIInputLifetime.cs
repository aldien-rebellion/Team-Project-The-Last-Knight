using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace TheLastKnight.UI
{
    // uGUI owns these actions independently of gameplay and UI Toolkit's global actions.
    [DisallowMultipleComponent]
    public sealed class UIInputLifetime : MonoBehaviour
    {
        private InputActionAsset _actions;

        public static void Configure(InputSystemUIInputModule module)
        {
            var owner = module.GetComponent<UIInputLifetime>();
            if (owner == null) owner = module.gameObject.AddComponent<UIInputLifetime>();
            if (owner._actions == null)
            {
                var source = InputSystem.actions;
                if (source == null)
                {
                    module.AssignDefaultActions();
                    source = module.actionsAsset;
                }
                owner._actions = Instantiate(source);
                owner._actions.name = "Runtime UI Input";
            }
            module.actionsAsset = owner._actions;
            owner._actions.FindActionMap("UI", true).Enable();
        }

        private void OnEnable()
        {
            if (_actions != null) _actions.FindActionMap("UI", true).Enable();
        }

        private void OnDisable()
        {
            if (_actions != null) _actions.Disable();
        }

        private void OnDestroy()
        {
            if (_actions == null) return;
            _actions.Disable();
            if (Application.isPlaying) Destroy(_actions);
            else DestroyImmediate(_actions);
        }
    }
}
