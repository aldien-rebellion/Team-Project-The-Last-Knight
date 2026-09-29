using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(MedusaAura))]
    public class MedusaSavePoint : WorldInteractable
    {
        public bool grantsCityRune;
        private void Awake() { prompt = "F  Rest at Medusa / Save"; }
        public override void Interact()
        {
            if (grantsCityRune) DemonRuneManager.Instance.DropRune(1, transform.position);
            bool saved = GameManager.Instance.SaveAt(GameManager.Instance.Player.transform.position, out string error);
            FloatingCombatText.Show(transform.position, saved ? "Restored • Game saved" : error, saved ? Color.green : Color.yellow);
        }
    }
}
