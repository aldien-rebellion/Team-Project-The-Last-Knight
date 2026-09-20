using TheLastKnight.Core;
using TheLastKnight.UI;

namespace TheLastKnight.Environment
{
    public class ShadowMarketNPC : WorldInteractable
    {
        private void Awake() { prompt = "F  The Shadow Market"; }
        public override void Interact()
        {
            if (GameManager.Instance.InputBlocked) return;
            var shop = gameObject.GetComponent<ShopUI>();
            if (shop == null) shop = gameObject.AddComponent<ShopUI>();
            shop.Open();
        }
    }
}
