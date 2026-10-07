using System.Collections;
using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.Combat;
using TheLastKnight.UI;

namespace TheLastKnight.Environment
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public class RedGate : WorldInteractable
    {
        [Header("Gate Configuration")]
        [Tooltip("The boss that must be defeated before this gate can be destroyed (e.g. Volcanox).")]
        [SerializeField] private EnemyStats _requiredBoss;

        [Tooltip("Message displayed when player tries to destroy the gate before defeating the boss.")]
        [SerializeField] private string _bossAliveMessage = "The portal is sealed by Volcanox's power!";

        [Tooltip("Audio SFX ID played when the gate is destroyed.")]
        [SerializeField] private string _destroySfx = "rune";

        private Animator _animator;
        private bool _isDestroyed = false;
        private static readonly int DestroyParam = Animator.StringToHash("Destroy");

        public bool IsDestroyed => _isDestroyed;
        protected override bool CanInteract => base.CanInteract && !_isDestroyed;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            prompt = "F  Destroy Gate";
            interactionRange = 3.5f;

            if (_requiredBoss == null)
            {
                ResolveSceneBoss();
            }
        }

        private void ResolveSceneBoss()
        {
            foreach (var candidate in FindObjectsByType<EnemyStats>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene &&
                    (candidate.gameObject.name.Contains("Volcanox") || candidate.gameObject.name.Contains("Boss")))
                {
                    _requiredBoss = candidate;
                    break;
                }
            }
        }

        public override void Interact()
        {
            if (_isDestroyed) return;

            if (_requiredBoss != null && !_requiredBoss.IsDead)
            {
                FloatingCombatText.Show(transform.position + Vector3.up * 2f, _bossAliveMessage, Color.red);
                return;
            }

            prompt = "";
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.State.victory = true;
                var storyUI = gm.GetComponent<StoryDialogueUI>();
                if (storyUI != null)
                {
                    storyUI.Ending();
                    return;
                }
            }

            // Fallback if StoryDialogueUI is not present
            BreakGate();
        }

        public void BreakGate()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            prompt = "";

            if (_animator != null)
            {
                _animator.SetTrigger(DestroyParam);
            }

            if (!string.IsNullOrEmpty(_destroySfx))
            {
                TheLastKnight.Audio.AudioManager.Instance?.PlaySfx(_destroySfx);
            }
        }
    }
}
