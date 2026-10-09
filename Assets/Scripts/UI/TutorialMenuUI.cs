using System;
using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Core;
using TheLastKnight.Input;
using TheLastKnight.Player;
using TheLastKnight.Stats;

namespace TheLastKnight.UI
{
    public static class TutorialMenuUI
    {
        public const int PageCount = 5;
        private static readonly Color Gold = new Color(0.9f, 0.77f, 0.48f);
        private static string Txt(string thai, string english) => LocalizationManager.Current == GameLanguage.English ? english : thai;
        private static string Key(string action, int index = 0) => KeyRebindManager.GetCurrentBindingDisplay(action, index);
        private static string Keys(string action, int first, int second) => Key(action, first) + " / " + Key(action, second);

        public static GameObject Build(int page, Action<int> changePage, Action back)
        {
            page = Mathf.Clamp(page, 0, PageCount - 1);
            var panel = RuntimeUI.Panel(LocalizationManager.Get("TUTORIAL_TITLE"), out var content, 600);
            panel.name = "TutorialMenu";
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.05f, 0.04f);
            contentRect.anchorMax = new Vector2(0.95f, 0.96f);
            content.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            RuntimeUI.Label(content, LocalizationManager.Get("TUTORIAL_SUBTITLE"), 16);

            var split = new GameObject("TutorialBody", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            split.transform.SetParent(content, false);
            split.GetComponent<LayoutElement>().preferredHeight = 480f;
            split.GetComponent<LayoutElement>().minHeight = 200f;
            split.GetComponent<LayoutElement>().flexibleHeight = 1f;
            var splitLayout = split.GetComponent<HorizontalLayoutGroup>();
            splitLayout.spacing = 16f;
            splitLayout.childControlWidth = splitLayout.childControlHeight = true;
            splitLayout.childForceExpandWidth = false;
            splitLayout.childForceExpandHeight = true;
            var nav = new GameObject("Topics", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            nav.transform.SetParent(split.transform, false);
            nav.GetComponent<LayoutElement>().preferredWidth = nav.GetComponent<LayoutElement>().minWidth = 210f;
            nav.GetComponent<LayoutElement>().layoutPriority = 1;
            nav.GetComponent<LayoutElement>().flexibleWidth = 0f;
            var navLayout = nav.GetComponent<VerticalLayoutGroup>();
            navLayout.spacing = 10f;
            navLayout.childControlWidth = navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = false;
            string[] topics = {
                Txt("1. การเคลื่อนที่", "1. Movement"), Txt("2. ต่อสู้และหลบ", "2. Combat & evasion"),
                Txt("3. HP และ Stamina", "3. HP & Stamina"), Txt("4. ทักษะและสเตตัส", "4. Skills & stats"),
                Txt("5. สำรวจและบันทึก", "5. Explore & save")
            };
            for (int i = 0; i < PageCount; i++)
            {
                int targetPage = i;
                var button = RuntimeUI.Button(nav.transform, topics[i], () => changePage(targetPage));
                button.name = "TutorialTopic" + i;
                button.GetComponentInChildren<Text>().fontSize = 18;
                if (i == page)
                {
                    var colors = button.colors;
                    colors.normalColor = new Color(0.34f, 0.28f, 0.16f);
                    button.colors = colors;
                    button.GetComponentInChildren<Text>().color = Gold;
                }
            }
            Paragraph(nav.transform, Txt("กด Esc หรือย้อนกลับ\nเพื่อกลับหน้าตั้งค่า", "Esc or Back returns\nto Settings."), 16);
            var rows = RuntimeUI.ScrollContent(split.transform, 480f);
            var scroll = rows.GetComponentInParent<ScrollRect>();
            scroll.name = "TutorialScroll";
            var scrollElement = scroll.GetComponent<LayoutElement>();
            scrollElement.minHeight = 0f;
            scrollElement.minWidth = scrollElement.preferredWidth = 0f;
            scrollElement.flexibleWidth = 1f;
            Heading(rows, topics[page]);
            switch (page)
            {
                case 0: Movement(rows); break;
                case 1: Combat(rows); break;
                case 2: ResourcesPage(rows); break;
                case 3: Skills(rows); break;
                case 4: Exploration(rows); break;
            }
            var backButton = RuntimeUI.Button(content, LocalizationManager.Get("BTN_BACK"), () => back());
            backButton.name = "TutorialBack";
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(nav.transform.GetChild(page).gameObject);
            return panel;
        }

        private static void Movement(Transform rows)
        {
            Screenshot(rows, "GameplayHud", Txt("ภาพขณะเล่น: HP / Stamina อยู่มุมซ้ายบน ช่องใช้ไอเทมอยู่มุมขวาล่าง", "Gameplay: HP / Stamina at the top left; the quick item is at the bottom right."));
            Control(rows, Txt("เดินซ้าย / ขวา", "Walk left / right"), Keys("Move", 6, 7) + "  |  " + Keys("Move", 8, 9));
            Control(rows, Txt("พุ่งตัว / วิ่งค้าง", "Dash / hold to sprint"), Keys("Dash", 0, 2) + "  |  " + Keys("Sprint", 0, 1));
            Control(rows, Txt("กระโดด", "Jump"), Key("Jump"));
            Control(rows, Txt("ลงจากพื้นทางเดียว", "Drop through one-way platform"), Keys("Move", 4, 5) + " + " + Key("Jump"));
            Tip(rows, Txt("วิ่งโดยไม่ต้องกดทิศทาง", "Sprint without a direction key"),
                Txt($"กด {Keys("Sprint", 0, 1)} ค้างเพื่อวิ่งไปทางที่หัน ใช้ปุ่มเดินเพื่อเปลี่ยนทิศ เมื่อ Stamina หมดจะเดินต่อขณะค้างปุ่ม แม้พลังฟื้นก็ยังเดิน ต้องปล่อยแล้วกดใหม่จึงกลับไปวิ่งได้",
                    $"Hold {Keys("Sprint", 0, 1)} to run in your facing direction. Movement keys steer. When stamina runs out, holding the button keeps you walking. Recovery does not restart sprinting: release and press again."));
            Tip(rows, Txt("คุมความสูงและข้ามช่องว่าง", "Control jump height and cross gaps"),
                Txt($"แตะ {Key("Jump")} เพื่อกระโดดสั้น หรือค้างเพื่อกระโดดสูงขึ้น ใช้พุ่งกลางอากาศเพื่อข้ามช่องว่าง พุ่งกลางอากาศได้หนึ่งครั้งก่อนลงพื้น กระโดดสองชั้นปลดล็อกเมื่อ AGI ถึงเกณฑ์ (ค่าเริ่มต้น {PlayerStats.DoubleJumpAgiThreshold})",
                    $"Tap {Key("Jump")} for a short jump; hold for more height. Air dash to cross gaps. You get one air dash until landing. Double jump unlocks at the AGI threshold (default {PlayerStats.DoubleJumpAgiThreshold})."));
            Tip(rows, Txt("ผ่านพื้นทางเดียว", "Pass through one-way platforms"),
                Txt($"กดลง ({Keys("Move", 4, 5)}) + กระโดดขณะยืนบนพื้นทางเดียวเพื่อลงด้านล่าง หรือกดลงพร้อมปุ่มเดินซ้าย/ขวาเพื่อผ่านพื้นชนิดนี้ พื้นทึบปกติผ่านไม่ได้",
                    $"Press down ({Keys("Move", 4, 5)}) + Jump while standing on a one-way platform to drop through it. Down + left/right also bypasses these platforms. Solid floors stay solid."));
        }

        private static void Combat(Transform rows)
        {
            Screenshot(rows, "ParryTiming", Txt("วงเตือนของศัตรูหดเข้าหาวงเป้า ใช้จังหวะนี้เตรียมปัดป้อง", "The enemy warning ring contracts toward the target ring: prepare your parry."));
            Control(rows, Txt("โจมตี / ปัดป้องด้วยจังหวะโจมตี", "Attack / timed attack parry"), Keys("Attack", 1, 5));
            Control(rows, Txt("พุ่งหลบ", "Dash to evade"), Keys("Dash", 0, 2));
            Tip(rows, Txt("ปัดป้องแล้วสวนกลับ", "Parry, then counterattack"),
                Txt("เข้าใกล้ศัตรู แล้วโจมตีตอนใกล้จบการเตรียมโจมตีของมัน เมื่อวงหดใกล้วงเป้าจะเป็นจังหวะที่เหมาะ หากสำเร็จจะขึ้น PARRY และศัตรูชะงัก โจมตีซ้ำในช่วงชะงักเพื่อทำคริติคอล ในระดับยากวงเตือนจะถูกซ่อน ต้องอ่านท่าศัตรูแทน",
                    "Get close and attack near the end of the enemy's windup, as the ring approaches its target. Success shows PARRY and staggers the enemy. Follow up during stagger for a critical hit. Hard hides warning rings, so read the enemy animation."));
            Tip(rows, Txt("หลบอย่างมีจังหวะ", "Time your evasion"),
                Txt("พุ่งมีช่วงอมตะสั้น ๆ และผ่านตัวศัตรูได้ แต่ไม่ใช่อมตะตลอดท่า อย่าพุ่งติดกันจน Stamina หมด เก็บพลังไว้สำหรับหลบครั้งถัดไป และเลือกตำแหน่งที่ลงแล้วไม่ติดศัตรูอีกตัว",
                    "Dash has a brief invincibility window and can pass through enemies; the whole animation is not invincible. Avoid exhausting stamina. Save enough for another escape and choose a landing spot away from other enemies."));
            Tip(rows, Txt("เริ่มจากจังหวะง่าย", "Practice one enemy at a time"),
                Txt("ฝึกกับศัตรูทีละตัว: รอให้เตรียมโจมตี → ปัดป้องหรือพุ่งผ่าน → โจมตีเมื่อมันพักท่า การตีรัวโดยไม่ดู Stamina ทำให้ขาดพลังหลบและถูกสวนได้ง่าย",
                    "Practice on one enemy: wait for windup → parry or dash through → attack during recovery. Spamming attacks without checking stamina leaves you unable to evade."));
        }

        private static void ResourcesPage(Transform rows)
        {
            Screenshot(rows, "MedusaSavePoint", Txt("พักใกล้รูปปั้นเมดูซ่า และเลือกพักผ่อน/บันทึกเพื่อสร้างจุดกลับ", "Rest near a Medusa statue and choose Rest / Save to set a recall point."));
            Control(rows, Txt("ใช้ไอเทมช่องลัด", "Use quick item"), Key("UseDrink"));
            Tip(rows, Txt("HP และ Stamina ทำหน้าที่ต่างกัน", "HP and stamina have different jobs"),
                Txt("HP หมดจะตาย ส่วน Stamina ใช้กับการโจมตี กระโดด พุ่ง วิ่ง และบางทักษะ เมื่อพลังไม่พอจะใช้ท่านั้นไม่ได้ หยุดวิ่งแล้วเดินหรือยืนพักเพื่อให้ Stamina ฟื้น ยารักษาไม่ได้ทดแทนพลังสำหรับหลบ",
                    "Zero HP means death. Stamina fuels attacks, jumps, dashes, sprinting and some skills. Without enough stamina an action cannot start. Walk or stand still to recover it. Healing does not replace stamina for evasion."));
            Tip(rows, Txt("ฟื้นฟูที่รูปปั้น", "Recover at a statue"),
                Txt($"ในพื้นที่ฟื้นฟู Stamina ฟื้นเร็วขึ้น การฟื้น HP ต้องยืนหรือเดินและไม่โดนโจมตีอย่างน้อย {PlayerStats.HPRegenDelay:0.#} วินาที ฟื้นแบบออร่าได้ถึง {PlayerStats.MaxAuraHPPercent * 100f:0}% ของ HP สูงสุด เลือกพักผ่อน/บันทึกเพื่อเติม Stamina เต็มและฟื้น HP อย่างน้อยถึงเกณฑ์ออร่า ระดับยากฟื้นช้าลง",
                    $"The aura accelerates stamina recovery. HP recovery requires walking or idling and {PlayerStats.HPRegenDelay:0.#} seconds without damage; aura healing caps at {PlayerStats.MaxAuraHPPercent * 100f:0}% of maximum HP. Rest / Save fills stamina and restores HP up to at least the aura threshold. Hard reduces recovery speed."));
            Tip(rows, Txt("เตรียมไอเทมก่อนสู้", "Prepare items before combat"),
                Txt($"เปิดสเตตัสด้วย {Key("ToggleStatus")} แล้วย้ายยาหรือไอเทมที่ใช้ได้ไปช่องลัด ปุ่ม {Key("UseDrink")} ใช้ช่องแรกที่มีไอเทม หรือคลิกช่องไอเทมบน HUD การใช้ไอเทมมีท่าและอาจถูกยกเลิกเมื่อเคลื่อนที่ จึงควรถอยไปจุดปลอดภัยก่อนใช้",
                    $"Open Status with {Key("ToggleStatus")} and put a usable item in a quick slot. {Key("UseDrink")} uses the first occupied slot; you can also click the HUD item. Item use has an animation and movement can cancel it, so create a safe opening first."));
        }

        private static void Skills(Transform rows)
        {
            Control(rows, Txt("พายุดาบ", "Carnage Burst"), Key("UseSkill"));
            Control(rows, Txt("บัฟพลังโจมตี", "Attack buff"), Key("UseBuff"));
            Control(rows, Txt("ดาบศักดิ์สิทธิ์", "Excalibur"), Key("UseExcalibur"));
            Control(rows, Txt("สเตตัสและกระเป๋า", "Status & inventory"), Key("ToggleStatus"));
            Tip(rows, Txt("ปลดล็อกตามเลเวล", "Unlock skills by level"),
                Txt($"พายุดาบปลดล็อกเลเวล {PlayerController.Skill1UnlockLevel} ใช้โจมตีระยะใกล้ บัฟปลดล็อกเลเวล {PlayerController.Skill2UnlockLevel} เพิ่มพลังโจมตี {PlayerStats.Skill2BuffMultiplier * 100f:0}% นาน {PlayerStats.Skill2BuffDuration:0} วินาที ดาบศักดิ์สิทธิ์ปลดล็อกเลเวล {PlayerController.Skill3UnlockLevel} ใช้ยิงไปทางที่หัน แต่ต้องเผื่อเวลาร่ายและ Stamina",
                    $"Carnage Burst unlocks at level {PlayerController.Skill1UnlockLevel} for close-range damage. The buff unlocks at level {PlayerController.Skill2UnlockLevel}: +{PlayerStats.Skill2BuffMultiplier * 100f:0}% attack for {PlayerStats.Skill2BuffDuration:0} seconds. Excalibur unlocks at level {PlayerController.Skill3UnlockLevel}; aim with your facing direction and allow for cast time and stamina."));
            Tip(rows, Txt("ใช้ทักษะตอนมีช่องว่าง", "Cast when you have an opening"),
                Txt("บัฟและท่าร่ายทำให้หยุดเคลื่อนที่ชั่วคราว ควรใช้ก่อนเข้าโจมตีหรือหลังศัตรูชะงัก ตรวจคูลดาวน์ก่อนกดซ้ำ การยกเลิกบัฟหรือดาบศักดิ์สิทธิ์ลดคูลดาวน์ที่เหลือครึ่งหนึ่ง แต่ยังต้องรอให้พร้อมก่อนใช้ใหม่",
                    "Buffs and casting temporarily stop movement. Cast before engaging or after staggering an enemy. Check cooldowns before pressing again. Cancelling the buff or Excalibur halves its remaining cooldown; it does not make the skill immediately ready."));
            Tip(rows, Txt("เลือกเพิ่มค่าสเตตัสให้เหมาะ", "Choose stats for your play style"),
                Txt("STR เพิ่มพลังโจมตี • VIT เพิ่ม HP และ Stamina • DEX ช่วยคริติคอล • AGI เพิ่มความเร็วโจมตี วิ่ง และพุ่ง รวมถึงปลดล็อกกระโดดสองชั้น อ่านคำอธิบายค่าสเตตัสก่อนใช้แต้ม และจัดอุปกรณ์ในกระเป๋าให้พร้อมก่อนออกเดินทาง",
                    "STR improves attack; VIT adds HP and stamina; DEX helps critical hits; AGI improves attack, sprint and dash speed and unlocks double jump. Read each stat description before spending points and equip items before setting out."));
        }

        private static void Exploration(Transform rows)
        {
            Control(rows, Txt("โต้ตอบกับสิ่งของ / ประตู / รูปปั้น", "Interact with objects / doors / statues"), Key("Interact"));
            Control(rows, Txt("เปิดแผนที่ / สลับมุมมอง", "Map / switch map view"), Key("ToggleMap") + "  |  " + Key("SwitchMapView"));
            Control(rows, Txt("กลับจุดบันทึก", "Recall to save point"), Key("Recall"));
            Tip(rows, Txt("บันทึกก่อนเสี่ยง", "Save before taking risks"),
                Txt($"เดินเข้าใกล้รูปปั้นเมดูซ่า ใช้ลูกกลิ้งเลือกเมนู แล้วกด {Key("Interact")} หรือคลิกเพื่อยืนยัน พักผ่อน/บันทึกสร้างจุดกลับล่าสุด การอยู่ในออร่าหรือเปิดเมนูเฉย ๆ ยังไม่ใช่การบันทึก",
                    $"Approach a Medusa statue, select an option with the mouse wheel, then press {Key("Interact")} or click to confirm. Rest / Save sets your latest return point. Being in its aura or opening a menu alone does not save."));
            Tip(rows, Txt("วาร์ปกลับอย่างปลอดภัย", "Recall safely"),
                Txt($"หลังบันทึกที่รูปปั้นแล้ว ยืนนิ่งและกด {Key("Recall")} รอ {PlayerRecall.ChannelDuration:0} วินาที การเคลื่อนที่ โจมตี ใช้ท่า หรือโดนดาเมจจะขัดจังหวะ ระหว่างสู้บอสที่ล็อกสนามจะวาร์ปกลับไม่ได้ จึงควรหลบไปจุดปลอดภัยก่อนเริ่ม",
                    $"After saving at a statue, stand still and press {Key("Recall")}, then wait {PlayerRecall.ChannelDuration:0} seconds. Movement, attacks, actions or damage interrupt recall. Locked boss arenas prevent it, so get to safety before starting."));
            Tip(rows, Txt("ใช้แผนที่และอ่านคำใบ้", "Use the map and read prompts"),
                Txt("ตรวจเส้นทางในแผนที่ สำรวจประตู หีบ และจุดโต้ตอบที่มีข้อความขึ้น บางพื้นที่ต้องใช้รูนหรือความสามารถที่ปลดล็อกภายหลัง หากผ่านช่องว่างไม่ได้ ให้กลับมาเมื่อมีทักษะเคลื่อนที่พร้อม อย่าลืมตรวจความยาก: ระดับยากซ่อนแถบพลังและคำเตือนศัตรู",
                    "Check routes on the map and explore doors, chests and interaction prompts. Some areas need runes or abilities unlocked later. Revisit gaps when your movement abilities are ready. Hard hides enemy health bars and attack warnings."));
            Screenshot(rows, "MedusaSavePoint", Txt("รูปปั้นเมดูซ่า: จุดพัก ฟื้นฟู และบันทึกการเดินทาง", "Medusa statue: rest, recover and save your journey."));
        }

        private static Text Paragraph(Transform parent, string text, int size = 18)
        {
            var label = RuntimeUI.Label(parent, text, size);
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.GetComponent<LayoutElement>().preferredHeight = -1f;
            return label;
        }

        private static void Heading(Transform parent, string text)
        {
            var label = Paragraph(parent, text, 22);
            label.color = Gold;
        }

        private static void Tip(Transform parent, string title, string body)
        {
            var card = new GameObject("TutorialTip", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = new Color(0.1f, 0.14f, 0.21f);
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Heading(card.transform, title);
            Paragraph(card.transform, body);
        }

        private static void Control(Transform parent, string title, string keys)
        {
            var row = new GameObject("TutorialControl", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = new Color(0.1f, 0.14f, 0.21f);
            row.GetComponent<LayoutElement>().minHeight = 46f;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 8f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            var titleLabel = Paragraph(row.transform, title, 17);
            titleLabel.alignment = TextAnchor.MiddleLeft;
            titleLabel.GetComponent<LayoutElement>().minWidth = titleLabel.GetComponent<LayoutElement>().preferredWidth = 0;
            titleLabel.GetComponent<LayoutElement>().flexibleWidth = 1.5f;
            var keyLabel = Paragraph(row.transform, keys, 17);
            keyLabel.color = Gold;
            keyLabel.alignment = TextAnchor.MiddleRight;
            keyLabel.GetComponent<LayoutElement>().minWidth = keyLabel.GetComponent<LayoutElement>().preferredWidth = 0;
            keyLabel.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void Screenshot(Transform parent, string resource, string caption)
        {
            var texture = Resources.Load<Texture2D>("Tutorial/" + resource);
            if (texture == null) return;
            var frame = new GameObject("TutorialImage_" + resource, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            frame.transform.SetParent(parent, false);
            frame.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f);
            frame.GetComponent<LayoutElement>().preferredHeight = 250f;
            var image = new GameObject("Screenshot", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            image.transform.SetParent(frame.transform, false);
            image.GetComponent<RawImage>().texture = texture;
            image.GetComponent<RawImage>().raycastTarget = false;
            var fitter = image.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)texture.width / texture.height;
            Paragraph(parent, caption, 16).color = new Color(0.75f, 0.82f, 0.92f);
        }
    }
}
