using UnityEngine;
using UnityEngine.EventSystems;
using TheLastKnight.Audio;
using TheLastKnight.UI;

namespace TheLastKnight.Inventory
{
    public class BackdropClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            var inv = InventoryManager.Instance;
            if (inv != null && inv.CursorHeldItem != null)
            {
                var player = CharacterStatusUI.Instance != null ? CharacterStatusUI.Instance.GetPlayer() : null;
                Vector3 dropPos = player != null ? player.transform.position : Vector3.zero;

                if (eventData.button != PointerEventData.InputButton.Left && eventData.button != PointerEventData.InputButton.Right) return;
                inv.DropCursorItemToWorld(false, dropPos);
                AudioManager.Instance?.PlaySfx("click");
                return;
            }

            // If not holding an item, clicking backdrop closes window
            CharacterStatusUI.Instance?.Close();
        }
    }
}
