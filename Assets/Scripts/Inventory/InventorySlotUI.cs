using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using TheLastKnight.Audio;
using TheLastKnight.Stats;

namespace TheLastKnight.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public SlotType slotType;
        public int slotIndex;

        public Image iconImage;
        public TextMeshProUGUI countText;
        public Image highlightImage;
        public Outline highlightOutline;

        public void SetHighlight(bool active)
        {
            if (highlightImage != null)
            {
                highlightImage.color = active ? new Color(1f, 0.95f, 0.65f, 0.35f) : new Color(1f, 1f, 1f, 0f);
            }
            if (highlightOutline != null)
            {
                highlightOutline.enabled = active;
            }
        }

        public void UpdateDisplay(InventoryItemData item)
        {
            if (item != null && item.count > 0 && item.Icon != null)
            {
                if (iconImage != null)
                {
                    iconImage.sprite = item.Icon;
                    iconImage.color = Color.white;
                }
                if (countText != null)
                {
                    countText.text = item.count > 1 ? item.count.ToString() : "";
                }
            }
            else
            {
                if (iconImage != null)
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0f);
                }
                if (countText != null)
                {
                    countText.text = "";
                }
            }
        }

        private bool _dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            var inventory = InventoryManager.Instance;
            if (inventory == null || inventory.CursorHeldItem != null || inventory.GetSlot(slotType, slotIndex) == null) return;

            _dragging = true;
            eventData.eligibleForClick = false;
            inventory.HandleLeftClick(slotType, slotIndex, false, FindAnyObjectByType<PlayerStats>());
            UI.CharacterStatusUI.Instance?.HideTooltip();
        }

        // Required by the EventSystem to recognize this object as a drag source.
        // CharacterStatusUI renders the held stack at the mouse position.
        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            eventData.eligibleForClick = false;
            var inventory = InventoryManager.Instance;
            if (inventory == null || inventory.CursorHeldItem == null) return;

            var hit = eventData.pointerCurrentRaycast.gameObject;
            var target = hit != null ? hit.GetComponentInParent<InventorySlotUI>() : null;
            if (target != null)
            {
                inventory.HandleLeftClick(target.slotType, target.slotIndex, false, FindAnyObjectByType<PlayerStats>());
                AudioManager.Instance?.PlaySfx("click");
            }
            else if (hit != null && hit.GetComponentInParent<BackdropClickHandler>() != null)
            {
                var player = UI.CharacterStatusUI.Instance != null ? UI.CharacterStatusUI.Instance.GetPlayer() : null;
                inventory.DropCursorItemToWorld(false, player != null ? player.transform.position : Vector3.zero);
            }
            // Releasing over the window keeps the stack held, ready for click-to-place.
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            var player = FindAnyObjectByType<PlayerStats>();
            bool isShift = Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                inv.HandleLeftClick(slotType, slotIndex, isShift, player);
                AudioManager.Instance?.PlaySfx("click");
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                var item = inv.GetSlot(slotType, slotIndex);
                if (inv.CursorHeldItem == null && item != null &&
                    string.Equals(item.id, "earth_spellbook", System.StringComparison.OrdinalIgnoreCase) &&
                    UI.CharacterStatusUI.Instance != null)
                {
                    UI.CharacterStatusUI.Instance.ShowItemUseMenu(slotType, slotIndex, eventData.position);
                    AudioManager.Instance?.PlaySfx("click");
                    return;
                }
                inv.HandleRightClick(slotType, slotIndex, player);
                AudioManager.Instance?.PlaySfx("click");
            }

            // Refresh tooltip & highlight after click
            ShowSlotTooltip();
            SetHighlight(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlight(true);
            ShowSlotTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
            UI.CharacterStatusUI.Instance?.HideTooltip();
        }

        private void ShowSlotTooltip()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            // If cursor is holding an item, don't obstruct view
            if (inv.CursorHeldItem != null)
            {
                UI.CharacterStatusUI.Instance?.HideTooltip();
                return;
            }

            var item = inv.GetSlot(slotType, slotIndex);
            if (item != null && item.count > 0)
            {
                string subtitle = slotType == SlotType.QuickSlot
                    ? (slotIndex == 0 ? $"{item.typeName} • Ready for [Q]" : $"{item.typeName} • Quick Slot {slotIndex + 1}")
                    : item.typeName;

                string desc = item.description;
                if (item.count > 1)
                {
                    desc += $"\n<color=#FFD56B>Current Stack: {item.count} / {item.maxStack}</color>";
                }

                string hint = "<color=#98E498>[L-Click: Pick / Place]</color>   <color=#85C1E9>[R-Click: Split / Place 1]</color>\n<color=#F9E79F>[Shift + L-Click: Quick Move]</color>";

                if (string.Equals(item.id, "earth_spellbook", System.StringComparison.OrdinalIgnoreCase))
                    hint = "<color=#98E498>[L-Click: Pick / Place]</color>   <color=#85C1E9>[R-Click: ใช้]</color>\n<color=#F9E79F>[Shift + L-Click: Quick Move]</color>";

                UI.CharacterStatusUI.Instance?.ShowTooltip(item.name, subtitle, desc, hint);
            }
            else
            {
                if (slotType == SlotType.QuickSlot)
                {
                    string subtitle = slotIndex == 0 ? "Hotbar Priority 1" : $"Hotbar Priority {slotIndex + 1}";
                    string desc = slotIndex == 0
                        ? "Active [Q] hotkey item slot. Drag consumables here to use with [Q] in battle."
                        : "Backup quick slot. Items advance automatically when earlier slots run out.";
                    string hint = "<color=#F9E79F>[Drag item here to assign]</color>";
                    UI.CharacterStatusUI.Instance?.ShowTooltip($"Quick Slot {slotIndex + 1} (Empty)", subtitle, desc, hint);
                }
                else
                {
                    string hint = "<color=#A0A0A0>[Drag item here to store]</color>";
                    UI.CharacterStatusUI.Instance?.ShowTooltip("Empty Inventory Slot", $"Bag Slot {slotIndex + 1}", "Slot is empty and ready to store items.\nDrag items here to organize.", hint);
                }
            }
        }
    }
}
