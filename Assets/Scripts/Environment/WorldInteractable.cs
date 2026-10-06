using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheLastKnight.Core;

namespace TheLastKnight.Environment
{
    public abstract class WorldInteractable : MonoBehaviour
    {
        private static readonly List<WorldInteractable> Active = new List<WorldInteractable>();
        public float interactionRange = 2.5f;
        public string prompt = "F  Interact";
        protected float promptHeight = 2f;
        private TextMesh _prompt;
        protected virtual void OnEnable() { Active.Add(this); }
        protected virtual void OnDisable()
        {
            Active.Remove(this);
            if (_prompt != null) _prompt.gameObject.SetActive(false);
        }
        protected virtual void OnDestroy()
        {
            if (_prompt != null) Destroy(_prompt.gameObject);
        }
        private void Update()
        {
            var manager = GameManager.Instance;
            if (manager == null || manager.Player == null) return;
            WorldInteractable closest = null;
            float distance = float.PositiveInfinity;
            foreach (var item in Active)
            {
                if (item == null) continue;
                float candidate = Vector2.Distance(item.transform.position, manager.Player.transform.position);
                if (candidate <= item.interactionRange && candidate < distance) { distance = candidate; closest = item; }
            }
            bool selected = closest == this && !manager.InputBlocked && !manager.Player.IsDead;
            if (_prompt == null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child.name == "Interaction prompt") Destroy(child.gameObject);
                }
                var go = new GameObject("Interaction prompt");
                _prompt = go.AddComponent<TextMesh>(); _prompt.fontSize = 40; _prompt.characterSize = 0.055f;
                _prompt.anchor = TextAnchor.MiddleCenter;
                go.GetComponent<MeshRenderer>().sortingOrder = 110;
                go.GetComponent<MeshRenderer>().sortingLayerName = "InGame_UI";
            }
            // Use a world-space offset from the interactable pivot. Sprite bounds
            // include transparent padding in some sheets and can place labels too high.
            float promptY = transform.position.y + promptHeight;
            _prompt.transform.position = new Vector3(transform.position.x, promptY, transform.position.z);
            _prompt.gameObject.SetActive(selected);
            TheLastKnight.UI.LocalizedText.Set(_prompt, prompt);
            bool fPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            if (selected && fPressed) Interact();
        }
        public abstract void Interact();
    }
}
