using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;

namespace TheLastKnight.Environment
{
    public class RuneChest : WorldInteractable
    {
        private void Awake() { prompt = "F  Open Pentagram chest"; }
        public override void Interact()
        {
            if (!GameManager.Instance.State.churchKey)
            {
                FloatingCombatText.Show(transform.position, "Requires the Moonstone Keeper's key", Color.yellow);
                return;
            }
            DemonRuneManager.Instance.CollectRune(0);
            prompt = "Pentagram collected";
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.color = new Color(0.65f, 0.65f, 0.65f);
        }
    }
}
