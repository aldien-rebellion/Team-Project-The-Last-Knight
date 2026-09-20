using UnityEngine;
using TheLastKnight.Stats;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(CircleCollider2D))]
    public class MedusaAura : MonoBehaviour
    {
        private readonly System.Collections.Generic.HashSet<PlayerStats> _players = new System.Collections.Generic.HashSet<PlayerStats>();
        private void Awake()
        {
            var area = GetComponent<CircleCollider2D>();
            area.isTrigger = true;
            area.radius = 4f;
        }
        private void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerStats>();
            if (player == null) return;
            _players.Add(player);
            player.SetRegenAura(this, true);
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerStats>();
            if (player == null) return;
            _players.Remove(player);
            player.SetRegenAura(this, false);
        }
        private void OnDisable()
        {
            foreach (var player in _players) if (player != null) player.SetRegenAura(this, false);
            _players.Clear();
        }
    }
}
