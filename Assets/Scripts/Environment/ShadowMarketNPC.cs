using System.Collections;
using UnityEngine;
using TheLastKnight.Core;
using TheLastKnight.UI;

namespace TheLastKnight.Environment
{
    public class ShadowMarketNPC : WorldInteractable
    {
        private Animator _traderAnimator;
        private bool _openingShop;
        private bool _greetingShown;

        private void Awake()
        {
            prompt = "F  The Shadow Market";
            promptHeight = 3.5f;
            _traderAnimator = GetComponent<Animator>();
            if (_traderAnimator != null)
            {
                _traderAnimator.SetInteger("ActionIndex", 2);
                _traderAnimator.Play("Idle", 0, 0f);
            }

            // Keep the enlarged, bottom-pivoted sprite on the market walkway even
            // when an older serialized scene position is loaded.
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                const float walkwayTop = -4.47f;
                Vector3 position = transform.position;
                position.y += walkwayTop - spriteRenderer.bounds.min.y;
                transform.position = position;
            }
        }
        public override void Interact()
        {
            if (GameManager.Instance.InputBlocked || _openingShop) return;

            if (!_greetingShown)
            {
                _greetingShown = true;
                var dialogue = GameManager.Instance.GetComponent<StoryDialogueUI>();
                if (dialogue != null)
                {
                    _openingShop = true;
                    dialogue.Show("THE SHADOW MARKET", new[]
                    {
                        "พ่อค้าแห่งเงา\n\nยินดีต้อนรับ ท่านอัศวิน … ข้าเป็นพ่อค้าแห่งเงา",
                        "พ่อค้าแห่งเงา\n\nที่นี่มีทั้งสิ่งที่เจ้าตามหา และสิ่งที่เจ้าอาจยังไม่รู้ว่าต้องการ",
                        "พ่อค้าแห่งเงา\n\nมองดูให้ดี แล้วเลือกอย่างระวัง—ทุกสิ่งมีราคาเสมอ"
                    }, OnGreetingFinished);
                    return;
                }
            }
            else
            {
                var dialogue = GameManager.Instance.GetComponent<StoryDialogueUI>();
                if (dialogue != null)
                {
                    _openingShop = true;
                    dialogue.Show("THE SHADOW MARKET", new[]
                    {
                        "พ่อค้าแห่งเงา\n\nกลับมาแล้วหรือ ท่านอัศวิน… ข้ามีของดีรอเจ้าอยู่"
                    }, OnGreetingFinished);
                    return;
                }
            }

            StartCoroutine(OpenShopSequence());
        }

        private void OnGreetingFinished()
        {
            StartCoroutine(OpenShopSequence());
        }

        private IEnumerator OpenShopSequence()
        {
            _openingShop = true;
            GameManager.Instance.SetInputBlocked(true);

            if (_traderAnimator != null)
            {
                // Reset to the idle state so the reveal can be replayed every visit.
                _traderAnimator.SetInteger("ActionIndex", 2);
                _traderAnimator.Play("Idle", 0, 0f);
                yield return null;

                // Dialogue is the trader's cloak reveal animation.
                _traderAnimator.SetInteger("ActionIndex", 1);
                yield return new WaitForSecondsRealtime(0.95f);

                // Hold the open-goods pose while the shop is on screen.
                _traderAnimator.SetInteger("ActionIndex", 0);
                yield return null;
            }

            var shop = gameObject.GetComponent<ShopUI>();
            if (shop == null) shop = gameObject.AddComponent<ShopUI>();
            shop.Closed -= OnShopClosed;
            shop.Closed += OnShopClosed;
            shop.Open();
            _openingShop = false;
        }

        private void OnShopClosed()
        {
            if (_traderAnimator == null) return;
            GameManager.Instance.SetInputBlocked(true);
            StartCoroutine(CloseShopSequence());
        }

        private IEnumerator CloseShopSequence()
        {
            // Resume the reveal clip near its final frames so the trader folds the cloak.
            _traderAnimator.SetInteger("ActionIndex", 1);
            _traderAnimator.Play("Dialogue", 0, 0.7f);
            yield return null;

            while (_traderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Dialogue") &&
                   _traderAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                yield return null;
            }

            _traderAnimator.SetInteger("ActionIndex", 2);
            _traderAnimator.Play("Idle", 0, 0f);
            yield return null;
            GameManager.Instance.SetInputBlocked(false);
        }
    }
}
