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
        private TextMesh _prompt;
        protected virtual void OnEnable() { Active.Add(this); }
        protected virtual void OnDisable() { Active.Remove(this); }
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
                var go = new GameObject("Interaction prompt");
                go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up * 2f;
                _prompt = go.AddComponent<TextMesh>(); _prompt.fontSize = 40; _prompt.characterSize = 0.055f;
                _prompt.anchor = TextAnchor.MiddleCenter;
                go.GetComponent<MeshRenderer>().sortingOrder = 110;
            }
            _prompt.gameObject.SetActive(selected);
            _prompt.text = prompt;
            if (selected && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Interact();
        }
        public abstract void Interact();
    }
}
