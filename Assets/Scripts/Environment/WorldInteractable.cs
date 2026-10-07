using System.Collections.Generic;
using UnityEngine;
using TheLastKnight.Core;

namespace TheLastKnight.Environment
{
    public abstract class WorldInteractable : MonoBehaviour
    {
        private static readonly List<WorldInteractable> Active = new List<WorldInteractable>();
        private static int _selectionFrame = -1;
        private static int _interactionFrame = -1;
        private static WorldInteractable _selected;
        public float interactionRange = 2.5f;
        public string prompt = "F  Interact";
        protected float promptHeight = 2f;
        protected virtual TextAnchor PromptAnchor => TextAnchor.MiddleCenter;
        protected virtual void UpdateSelection(bool selected) { }
        protected virtual bool UsesWorldPrompt => true;
        private TextMesh _prompt;
        protected virtual bool CanInteract => interactionRange >= 0f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Keep enabled objects when Play Mode uses no scene reload.
            Active.RemoveAll(item => item == null);
            _selectionFrame = _interactionFrame = -1;
            _selected = null;
        }
        protected virtual void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        protected virtual void OnDisable()
        {
            Active.Remove(this);
            if (_prompt != null) _prompt.gameObject.SetActive(false);
        }
        protected virtual void OnDestroy()
        {
            if (_prompt != null) Destroy(_prompt.gameObject);
        }
        private static WorldInteractable FindClosest(Transform player)
        {
            WorldInteractable closest = null;
            float distance = float.PositiveInfinity;
            var playerCollider = player.GetComponent<Collider2D>();
            foreach (var item in Active)
            {
                if (item == null || !item.isActiveAndEnabled || !item.CanInteract) continue;
                Vector2 position = playerCollider != null && playerCollider.enabled
                    ? playerCollider.ClosestPoint(item.transform.position) : (Vector2)player.position;
                float candidate = Vector2.Distance(item.transform.position, position);
                if (candidate <= item.interactionRange && candidate < distance) { distance = candidate; closest = item; }
            }
            return closest;
        }
        private void LateUpdate()
        {
            var manager = GameManager.Instance;
            if (_selectionFrame != Time.frameCount)
            {
                _selectionFrame = Time.frameCount;
                _selected = manager != null && manager.Player != null && !manager.InputBlocked && !manager.Player.IsDead
                    ? FindClosest(manager.Player.transform) : null;
            }
            bool selected = _selected == this && manager != null && !manager.InputBlocked &&
                manager.Player != null && !manager.Player.IsDead && CanInteract;
            UpdateSelection(selected);
            if (!UsesWorldPrompt)
            {
                TryInteract(selected);
                return;
            }
            if (_prompt == null)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child.name == "Interaction prompt") Destroy(child.gameObject);
                }
                var go = new GameObject("Interaction prompt");
                _prompt = go.AddComponent<TextMesh>(); _prompt.fontSize = 40; _prompt.characterSize = 0.055f;
                _prompt.anchor = PromptAnchor;
                go.GetComponent<MeshRenderer>().sortingOrder = 110;
                go.GetComponent<MeshRenderer>().sortingLayerName = "InGame_UI";
            }
            // Use a world-space offset from the interactable pivot. Sprite bounds
            // include transparent padding in some sheets and can place labels too high.
            float promptY = transform.position.y + promptHeight;
            _prompt.transform.position = new Vector3(transform.position.x, promptY, transform.position.z);
            _prompt.gameObject.SetActive(selected);
            TheLastKnight.UI.LocalizedText.Set(_prompt, prompt);
            TryInteract(selected);
        }
        private void TryInteract(bool selected)
        {
            if (!selected || _interactionFrame == Time.frameCount ||
                !TheLastKnight.Input.KeyRebindManager.WasPressedThisFrame("Interact")) return;
            // Opening a chest changes its range immediately. Other objects must
            // not receive the same press when the closest target changes.
            _interactionFrame = Time.frameCount;
            Interact();
        }
        public abstract void Interact();
    }
}
