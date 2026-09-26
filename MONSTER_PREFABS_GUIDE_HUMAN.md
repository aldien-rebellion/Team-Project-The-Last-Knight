# คู่มือการสร้าง Monster ใน Prefabs และระบบต่อสู้ [ฉบับคนอ่าน / Human Developer Edition]

> [!NOTE]
> **เอกสารคู่ขนาน:** สำหรับ AI / LLM Agents ดูข้อกำหนดเชิงเทคนิคและ Schema มาตรฐานได้ที่ [`MONSTER_PREFABS_GUIDE_AI.md`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/MONSTER_PREFABS_GUIDE_AI.md)

คู่มือนี้สรุปขั้นตอนการสร้าง, ตั้งค่า Prefab ของมอนสเตอร์, ระบบ Level Scaling, ระบบสกิล, ระบบ Parry, และระบบเกิดใหม่ (Respawn & Leash) ที่สามารถระบุและคำนวณค่าสเตตัสผ่านหน้าต่าง Unity Inspector ได้โดยตรง สำหรับทีมพัฒนาและผู้ออกแบบเกม **The Last Knight**

---

## 1. ตำแหน่งโฟลเดอร์สำคัญ (Key Directories)

* **Prefab ของมอนสเตอร์ทั้งหมด:** `Assets/Prefabs/Enemies/`
  *(เช่น `Skeleton.prefab`, `Goblin.prefab`, `Minotaur_1.prefab`, `UndeadExecutioner.prefab`)*
* **Prefab กระสุน / เวทมนตร์:** `Assets/Prefabs/Enemies/Projectiles/`
  *(เช่น `Goblin_Bomb.prefab`, `Skeleton_Sword.prefab`, `Dragon_FireBall.prefab`)*
* **Animations & Controllers:** `Assets/Animations/Enemies/`
* **สคริปต์ควบคุมระบบ:**
  * [`Assets/Scripts/Combat/EnemyStats.cs`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Scripts/Combat/EnemyStats.cs) (ระบบสเตตัส, เลเวล, และการคำนวณอัตโนมัติ)
  * [`Assets/Scripts/AI/EnemyController.cs`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Scripts/AI/EnemyController.cs) (ระบบ AI การเคลื่อนที่และโจมตี)
  * [`Assets/Editor/EnemyPrefabBuilder.cs`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Editor/EnemyPrefabBuilder.cs) (เครื่องมือสร้างและตั้งค่า Prefab อัตโนมัติ)

---

## 2. โครงสร้างของ Monster Prefab (Prefab Hierarchy & Components)

มอนสเตอร์ 1 ตัวใน Prefab ควรมีโครงสร้าง Hierarchy และ Components ดังต่อไปนี้:

```text
[Monster_Name] (Root GameObject)
 ├── SpriteRenderer
 ├── Animator
 ├── Rigidbody2D
 ├── CapsuleCollider2D (หรือ BoxCollider2D สำหรับ Hurtbox ลำตัว)
 ├── EnemyStats
 ├── EnemyController
 ├── HealthCanvas (Child GameObject - World Space Canvas แสดงหลอดเลือด)
 └── ContactHitbox (Child GameObject - Trigger Collider สำหรับชนทำดาเมจผู้เล่น)
```

### รายละเอียด Components บน Root GameObject:
1. **`SpriteRenderer`**:
   * Sorting Layer: `Default` หรือเลเยอร์ตัวละคร
   * Order in Layer: `2` (อยู่ด้านหน้าฉากหลัง)
2. **`Animator`**:
   * Controller: ใส่ Controller จาก `Assets/Animations/Enemies/<ชื่อมอนสเตอร์>/<ชื่อมอนสเตอร์>Controller.controller`
3. **`Rigidbody2D`**:
   * Body Type: `Dynamic`
   * Collision Detection: `Continuous`
   * Constraints: ติ๊กถูก **Freeze Rotation Z**
   * Gravity Scale: `2.5` (สำหรับมอนสเตอร์เดินดิน) หรือ `0` (สำหรับมอนสเตอร์บินได้ เช่น `FlyingEye`)
4. **`CapsuleCollider2D`**:
   * ใช้เป็น Hitbox ลำตัวสำหรับรับความเสียหาย (รับการโจมตีจากผู้เล่น)
   * ขนาดและ Offset ปรับให้พอดีกับลำตัวมอนสเตอร์
5. **`EnemyStats`**:
   * จัดการ Level, HP, Defense, Attack Power และรางวัล EXP / Gold
6. **`EnemyController`**:
   * จัดการ AI พฤติกรรม: Patrol Speed, Chase Speed, Detection Range, Melee Range และ Prefab กระสุน/เวทมนตร์

---

## 3. ระบบการระบุ Level และการคำนวณสเตตัสใน Inspector

ใน Component **`EnemyStats`** ได้รับการออกแบบให้สามารถระบุ Level ได้โดยตรงผ่านหน้าต่าง Unity Inspector และจะคำนวณค่าสเตตัสให้อัตโนมัติทันที

### การตั้งค่าใน Inspector:
1. เปิด Prefab ของมอนสเตอร์ หรือเลือกตัวมอนสเตอร์ใน Scene
2. ที่ Component **`EnemyStats`** จะพบส่วนหัวข้อ **`Level & Scaling Configuration`**:
   * **`Level`**: กำหนดระดับเลเวลของมอนสเตอร์ (ค่าเริ่มต้นตั้งแต่ `1` ขึ้นไป)
   * **`Use Level Scaling`**: ติ๊กเปิดใช้งานระบบคำนวณสเตตัสตามเลเวลอัตโนมัติ
3. เมื่อพิมพ์เปลี่ยนตัวเลขในช่อง **`Level`** หรือคลิกขวาที่ชื่อคอมโพเนนต์แล้วเลือก **`Apply Level Scaling Formula`**:
   * Unity Editor จะเรียกฟังก์ชัน `OnValidate()` และอัปเดตค่า `Max Health`, `Attack Power`, `Defense`, `EXP Reward` และ `Gold Reward` ให้สอดคล้องกับเลเวลทันที

---

## 4. สูตรความสัมพันธ์ระหว่าง Level กับความเก่ง (Scaling Formulas)

ระบบใช้สูตรตามที่กำหนดไว้ดังนี้:

| ค่าสเตตัส (Stat) | สูตรการคำนวณ (Formula) | คำอธิบาย |
| :--- | :--- | :--- |
| **Max HP** | $\text{Level} \times 100$ | เลือดสูงสุดของมอนสเตอร์ เพิ่มขึ้น 100 ต่อ 1 เลเวล |
| **ATK (Attack Power)** | $\text{Level} \times 10$ | พลังโจมตีพื้นฐาน เพิ่มขึ้น 10 ต่อ 1 เลเวล |
| **DEF (Defense)** | $\text{Level} \times 1$ | พลังป้องกัน เพิ่มขึ้น 1 ต่อ 1 เลเวล |
| **EXP Reward** | $\text{Random}(20\% - 30\% \text{ of } [\text{Level} \times 100])$ | สุ่มค่าระหว่าง $\text{Level} \times 20$ ถึง $\text{Level} \times 30$ |
| **Gold Reward** | $\text{Random}(20\% - 30\% \text{ of } [\text{Level} \times 100])$ | สุ่มค่าระหว่าง $\text{Level} \times 20$ ถึง $\text{Level} \times 30$ |

> [!NOTE]
> เมื่อมอนสเตอร์ตายในเกม (`Die()`) หากเปิดใช้งาน `Use Level Scaling` ระบบจะทำการสุ่มค่า (`RollRewards()`) ใหม่อีกครั้ง ทำให้มอนสเตอร์เลเวลเดียวกันแต่ละตัวดรอป Gold และ EXP ไม่เท่ากัน อยู่ในช่วง 20% - 30% อย่างสมจริง

---

## 5. ตารางตัวอย่างสเตตัสมอนสเตอร์ตาม Level (Lv. 1 - 10)

| Level (Lv.) | Max HP | ATK | DEF | EXP Reward (สุ่ม) | Gold Reward (สุ่ม) |
| :---: | :---: | :---: | :---: | :---: | :---: |
| **Lv. 1** | 100 | 10 | 1 | 20 - 30 | 20 - 30 |
| **Lv. 2** | 200 | 20 | 2 | 40 - 60 | 40 - 60 |
| **Lv. 3** | 300 | 30 | 3 | 60 - 90 | 60 - 90 |
| **Lv. 4** | 400 | 40 | 4 | 80 - 120 | 80 - 120 |
| **Lv. 5** | 500 | 50 | 5 | 100 - 150 | 100 - 150 |
| **Lv. 6** | 600 | 60 | 6 | 120 - 180 | 120 - 180 |
| **Lv. 7** | 700 | 70 | 7 | 140 - 210 | 140 - 210 |
| **Lv. 8** | 800 | 80 | 8 | 160 - 240 | 160 - 240 |
| **Lv. 9** | 900 | 90 | 9 | 180 - 270 | 180 - 270 |
| **Lv. 10** | 1,000 | 100 | 10 | 200 - 300 | 200 - 300 |

---

## 6. ขั้นตอนการสร้าง Monster Prefab ใหม่ทีละสเต็ป (Step-by-Step Creation)

### วิธีที่ 1: สร้าง Prefab ใหม่จากหน้าต่าง Scene
1. คลิกขวาในแถบ **Hierarchy** ➔ เลือก **Create Empty** และตั้งชื่อมอนสเตอร์ (เช่น `Goblin_Lv3`)
2. ใส่ Component:
   * กด **Add Component** ➔ ค้นหา `SpriteRenderer` (เลือก Sprite ท่า Idle เฟรมแรก)
   * กด **Add Component** ➔ ค้นหา `Animator` (ใส่ Animator Controller ของมอนสเตอร์ตัวนั้น)
   * กด **Add Component** ➔ ค้นหา `Rigidbody2D` (ปรับ Gravity และ Freeze Rotation Z)
   * กด **Add Component** ➔ ค้นหา `CapsuleCollider2D` (ปรับขนาดให้คลุมลำตัว)
   * กด **Add Component** ➔ ค้นหา `EnemyStats`
   * กด **Add Component** ➔ ค้นหา `EnemyController`
3. ตั้งค่าสเตตัสใน `EnemyStats`:
   * ติ๊กเปิด **`Use Level Scaling`**
   * ใส่ระดับ **`Level`** ที่ต้องการ (เช่น `3`) สเตตัส HP, ATK, DEF, EXP, Gold จะเปลี่ยนเป็นค่าตามสูตรทันที
4. ลาก GameObject จาก **Hierarchy** ไปปล่อยในโฟลเดอร์ `Assets/Prefabs/Enemies/` เพื่อเซฟเป็น Prefab

### วิธีที่ 2: แก้ไข Prefab ที่มีอยู่แล้ว
1. ไปที่ `Assets/Prefabs/Enemies/` ในหน้าต่าง **Project**
2. ดับเบิ้ลคลิกเปิด Prefab ที่ต้องการแก้ไข (เช่น `Skeleton.prefab`)
3. ในแถบ Inspector ที่ `EnemyStats` ให้ติ๊ก **`Use Level Scaling`** และปรับ **`Level`** ตามต้องการ
4. กดเซฟ Prefab หรือกลับสู่ Scene ปกติ

---

## 7. การเรียกใช้งานผ่านโค้ด C# (Scripting API)

หากต้องการเปลี่ยนเลเวลมอนสเตอร์ผ่านโค้ดในระหว่างเล่นเกม หรือตอน Spawn:

```csharp
using TheLastKnight.Combat;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _monsterPrefab;

    public void SpawnMonsterAtLevel(Vector3 position, int targetLevel)
    {
        GameObject enemyObj = Instantiate(_monsterPrefab, position, Quaternion.identity);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        
        if (stats != null)
        {
            // กำหนด Level และสั่งให้คำนวณสเตตัสใหม่ตามสูตร
            stats.SetLevel(targetLevel, autoRecalculate: true);
        }
    }
}
```

---

## 8. ระบบการออกท่าทาง อนิเมชัน และสกิลของมอนสเตอร์ (Basic Attack & Skill System)

เป้าหมายหลักคือ **ให้มอนสเตอร์แต่ละตัวสามารถออกอนิเมชันได้ครบทุกท่าตามที่ออกแบบไว้ใน Animation Controller** โดยแบ่งหมวดหมู่ท่าทางออกเป็น 2 ประเภทหลัก:

### 1. Basic Attack (ท่าโจมตีพื้นฐาน)
* **เงื่อนไข:** มอนสเตอร์ทุกตัวต้องมีอย่างน้อย 1 ท่า (ส่วนใหญ่ 1 ท่า)
* **ดาเมจ:** ยึดตามค่าสเตตัสพื้นฐานเสมอ คือ **$\text{Damage} = \text{ATK} \times 1.0$**
* **คูลดาวน์:** สั้นและต่อเนื่อง (เช่น 1.2 – 2.0 วินาที) เพื่อใช้กดดันผู้เล่นในระยะปกติ

### 2. Skills (สกิลโจมตีพิเศษ)
* **เงื่อนไข:** นำท่าโจมตีอื่นๆ ที่เหลือใน Animation Controller ทั้งหมดมาตั้งค่าเป็นสกิล (Special Attacks / Spells)
* **การปรับแต่งในหน้าต่าง Inspector:** ผู้ใช้สามารถปรับค่าของแต่ละสกิลได้อิสระรายตัว:
  * **`Animation Trigger / State`**: ระบุชื่อท่าจาก Controller (เช่น `Attack3`, `Skill1`, `Summon`, `Dash`)
  * **`Damage Multiplier`**: ตัวคูณดาเมจจากค่า ATK (เช่น `1.5x`, `2.0x`, `2.5x`)
  * **`Cooldown`**: ระยะเวลาคูลดาวน์เป็นวินาที (เช่น `5s`, `8s`, `12s`)
  * **`Min / Max Range`**: ระยะตรวจจับที่เหมาะสมสำหรับออกท่า (ระยะประชิด 0-1.8m หรือระยะไกล 3-8m)
  * **`Projectile / Spell Prefab`**: วัตถุกระสุนหรือเวทมนตร์ (ถ้าเป็นท่าโจมตีระยะไกล)
  * **`Status Effect`**: สถานะผิดปกติที่แนบมากับการโจมตี (เช่น Stun, Burning, Frozen)

```mermaid
flowchart TD
    A["ตรวจพบผู้เล่น (In Detection Range)"] --> B{"ผู้เล่นอยู่ในระยะโจมตี?"}
    B -- ไม่ใช่ --> C["วิ่งไล่ล่า (Chase)"]
    B -- ใช่ --> D{"มี Skill ใดพร้อมใช้งาน?<br>(Cooldown หมด & อยู่ใน Range)"}
    D -- มีสกิลพร้อม --> E["ร่าย Skill พิเศษ<br>(Damage = ATK × Multiplier, เริ่มนับ Cooldown สกิล)"]
    D -- ไม่มีสกิลพร้อม --> F{"Basic Attack พร้อมไหม?<br>(CD หมด & อยู่ใน Melee Range)"}
    F -- พร้อม --> G["ออกท่า Basic Attack<br>(Damage = ATK × 1.0)"]
    F -- ยังติด CD --> H["คุมเชิง / รอจังหวะ (Wait / Move)"]
```

---

## 9. ตารางการตั้งค่าและปรับจูนมอนสเตอร์รายตัว (Individual Monster Tuning Table)

ตารางนี้รวบรวมมอนสเตอร์ทั้งหมด 30 ชนิดในโปรเจกต์ พร้อมจำแนกท่า Basic Attack และ Skills เพื่อเป็นแนวทางให้ผู้ใช้ปรับแต่งตัวคูณดาเมจและคูลดาวน์รายตัวใน Inspector:

### กลุ่มที่ 1: มอนสเตอร์ที่มีหลายท่าโจมตีและมีสกิลพิเศษ (15 ชนิด)

| มอนสเตอร์ | Basic Attack (1.0x ATK) | ท่า Skills (ชื่อท่า, ตัวคูณดาเมจแนะนำ, คูลดาวน์แนะนำ, รูปแบบ) |
| :--- | :--- | :--- |
| **ForestMushroom** | `Attack` (โขกเห็ด 1.0x, CD 1.5s) | • `AttackWithStun` (1.5x ATK, CD 6.0s, ระยะ 1.5m, ติดสถานะ Stun 1.5 วินาที) |
| **Goblin** | `Attack` (ฟันมีดสั้น 1.0x, CD 1.4s) | • `Attack3` (1.6x ATK, CD 5.0s, ระยะ 3.0-7.0m, ขว้างระเบิด `Goblin_Bomb`) |
| **Skeleton** | `Attack` (ฟันดาบ 1.0x, CD 1.5s) | • `Attack3` (1.4x ATK, CD 4.5s, ระยะ 3.0-6.5m, ปาดาบพุ่ง `Skeleton_Sword`)<br>• `Shield` (CD 8.0s, ยกโล่ป้องกันลดดาเมจ 50% ชั่วคราว) |
| **FlyingEye** | `Attack` (พุ่งกัด 1.0x, CD 1.3s) | • `Attack3` (1.5x ATK, CD 5.0s, ระยะ 2.5-7.5m, ยิงกระสุน `FlyingEye_Projectile`) |
| **FantasyMushroom** | `Attack` (กระแทก 1.0x, CD 1.5s) | • `Attack3` (1.4x ATK, CD 5.0s, ระยะ 3.0-7.0m, พ่นสปอร์ `FantasyMushroom_Projectile`) |
| **UndeadExecutioner** *(Mini-Boss)* | `Attack` (ฟันขวานยักษ์ 1.0x, CD 2.0s) | • `Skill1` (2.0x ATK, CD 7.0s, ระยะ 2.2m, หมุนขวานฟันรอบตัวกวาดล้าง)<br>• `Summon` (CD 15.0s, ปักขวานร่ายเวทรอบตัว / เรียกวิญญาณ) |
| **MoonstoneKeeper** | `Attack1` (ต่อยหมัดชุด 1.0x, CD 1.5s) | • `Attack2` (1.8x ATK, CD 6.0s, ระยะ 2.0m, ทุบพื้นสร้างแรงกระแทกหนัก)<br>• `Dash` (1.3x ATK, CD 5.0s, ระยะ 4.0-7.0m, พุ่งทะลวงเข้าใส่ผู้เล่น) |
| **MechaStoneGolem** | `Melee` (ทุบหมัดหิน 1.0x, CD 2.0s) | • `Shoot` (1.5x ATK, CD 5.0s, ระยะ 3.0-8.0m, ยิงหมัดจรวด `Golem_ArmProjectile`)<br>• `LaserCast` (2.5x ATK, CD 10.0s, ระยะ 3.0-9.0m, ยิงลำแสงเลเซอร์ `Golem_Laser`)<br>• `ShieldCast` (CD 12.0s, กางเกราะหินสะท้อน/ลดดาเมจ) |
| **BlueSlime** | `Attack_1` (กระโดดโหม่ง 1.0x, CD 1.2s) | • `Attack_2` (1.3x ATK, CD 3.5s, ระยะ 1.8m, กระโดดสองจังหวะ)<br>• `Attack_3` (1.6x ATK, CD 6.0s, ระยะ 2.2m, พองตัวทับสร้างคลื่น)<br>• `Run+Attack` (1.2x ATK, CD 5.0s, ระยะ 3.0-6.0m, พุ่งสไลด์ชน) |
| **Satyr** | `Attack1` (แทงหอก 1.0x, CD 1.4s) | • `Attack2` (1.4x ATK, CD 4.0s, ฟันเฉือนแนวขวาง)<br>• `Skill1` (1.8x ATK, CD 7.0s, กระโดดถีบสองเท้า)<br>• `Cast / Skill2` (2.0x ATK, CD 9.0s, ร่ายเวทศรธรรมชาติ) |
| **Necromancer** | `Attack1` (ยิงกระสุนวิญญาณ 1.0x, CD 1.5s) | • `Attack2` (1.6x ATK, CD 5.0s, ระยะ 3.0-8.0m, คลื่นพลังมืด)<br>• `Attack3` (2.2x ATK, CD 9.0s, ระยะ 2.0-7.0m, ลำแสงระเบิดวิญญาณ) |
| **SkeletonKnight** | `FwdSwing` (ฟันดาบใหญ่ 1.0x, CD 1.6s) | • `DownSwing` (1.6x ATK, CD 5.0s, ผ่าดาบลงพื้นอย่างแรง)<br>• `SideSwing` (1.3x ATK, CD 4.0s, ฟันกวาดมุมกว้าง)<br>• `FullCombo` (2.2x ATK, CD 9.0s, คอมโบดาบต่อเนื่อง 3 จังหวะ) |
| **DemonBoss** *(Boss)* | `Attack_01` (ตบกรงเล็บ 1.0x, CD 1.8s) | • `Attack_02` (1.8x ATK, CD 5.5s, กวาดกรงเล็บคู่ดาเมจหนัก)<br>• `Jump` (1.6x ATK, CD 8.0s, ระยะ 4.0-8.0m, กระโดดทับแผ่นดินไหว)<br>• `Shout` (CD 12.0s, คำรามข่มขวัญผลักผู้เล่นถอยหลัง/Stun) |
| **BringerOfDeath** | `Attack` (ฟันเคียวมรณะ 1.0x, CD 1.8s) | • `Cast / Spell` (2.0x ATK, CD 8.0s, ระยะ 2.0-8.0m, เสาเพลิงนรกใต้เท้า `BringerOfDeath_Spell`) |
| **Fox** | `Attack` (กระโจนข่วน 1.0x, CD 1.3s) | • `Disappear / Appear` (1.5x ATK, CD 6.0s, ระยะ 3.0-7.0m, หายตัววาร์ปมาโผล่โจมตีด้านหลัง) |

---

### กลุ่มที่ 2: มอนสเตอร์ที่มีท่าโจมตีเดี่ยว หรือยิงกระสุนประจำตัว (15 ชนิด)

| มอนสเตอร์ | ท่าโจมตีหลัก (1.0x ATK) | สกิล / คุณสมบัติเพิ่มเติม |
| :--- | :--- | :--- |
| **ArchDemon** | `BasicAtk` (ฟันพลังอสูร 1.0x, CD 1.8s) | ท่าฟันเดี่ยวหนักแน่น |
| **Demon** | `Attack` (ฟันกรงเล็บปีศาจ 1.0x, CD 1.5s) | โจมตีประชิดรวดเร็ว |
| **DemonKin** | `BasicAtk` (ฟันดาบคู่ 1.0x, CD 1.4s) | โจมตีประชิดมาตรฐาน |
| **Dragon** | `Attack` (กัด 1.0x, CD 1.6s) | • สกิลยิงลูกไฟ `Dragon_FireBall` (1.5x ATK, CD 4.5s, ระยะ 3.0-8.0m) |
| **FireWorm** | `Attack` (พ่นกัด 1.0x, CD 1.4s) | • สกิลยิงลูกไฟ `FireWorm_FireBall` (1.4x ATK, CD 4.0s, ระยะ 2.5-7.0m) |
| **Jinn** | `Attack` (ตบพลังภูติ 1.0x, CD 1.5s) | • สกิลเวทพื้นดิน `Jinn_Magic` (1.8x ATK, CD 6.0s, เสาเวทพุ่งขึ้นจากใต้ดิน) |
| **Lizard** | `Attack` (กัดฟาดหาง 1.0x, CD 1.4s) | โจมตีประชิดตัว |
| **Minotaur_1** | `Attack` (ฟันขวานศึก 1.0x, CD 1.8s) | กวาดขวานประชิดตัวแรง |
| **Minotaur_2** | `Attack` (ทุบกระบอง 1.0x, CD 1.8s) | ทุบกระบองประชิดตัว |
| **Minotaur_3** | `Attack` (ฟาดค้อน 1.0x, CD 1.8s) | ฟาดค้อนหนัก |
| **Reaper** | `HostileAttack` (ฟันเคียว 1.0x, CD 1.6s) | ท่าฟันเคียวกวาดกว้าง |
| **ShadowDemonDragon** *(Boss)* | `Attack_Left / Attack_Right` (1.0x, CD 2.0s) | ท่ากัดและตบตามทิศทางซ้าย-ขวา |
| **Skullwolf** | `Attack` (กระโจนกัด 1.0x, CD 1.2s) | เคลื่อนที่เร็ว กัดประชิดต่อเนื่อง |
| **Small_dragon** | `Attack` (พุ่งชน 1.0x, CD 1.3s) | • สกิลยิงลูกไฟ `SmallDragon_FireBall` (1.3x ATK, CD 4.0s, ระยะไกล) |
| **Trader_1** | *(ไม่มีท่าโจมตี)* | เป็น NPC พ่อค้า (`Approval`, `Dialogue`, `Idle`) |

---

## 10. วิธีการปรับแต่งในหน้าต่าง Unity Inspector (How to Tune in Inspector)

เมื่อเปิด Prefab ของมอนสเตอร์ใน `Assets/Prefabs/Enemies/`:

1. ไปที่ Component **`EnemyController`**
2. ในหัวข้อ **`Basic Attack`**:
   * ตรวจสอบว่า `Damage Multiplier` เป็น `1.0`
   * ปรับ `Cooldown` สำหรับจังหวะฟันธรรมดา (แนะนำ 1.2 – 2.0 วินาที)
   * ปรับ `Max Range` ตามระยะของ Sprite อาวุธ
3. ในหัวข้อ **`Special Skills`** (รายการ Array/List):
   * สามารถกดปุ่ม **`+`** เพื่อเพิ่มสกิล หรือปรับแต่งค่าของสกิลที่มีอยู่:
     * **`Damage Multiplier`**: กำหนดความแรง เช่น `1.5` หมายถึงแรงขึ้น 50% ของค่า ATK
     * **`Cooldown`**: กำหนดเวลาคูลดาวน์ ยิ่งท่าแรงควรตั้งคูลดาวน์ให้สูง (เช่น 6.0 – 10.0 วินาที)
     * **`Max Range`**: ถ้าเป็นท่ายิงไกล ให้ตั้งค่าระยะระหว่าง 6.0 – 8.0 เมตร
4. **การคำนวณร่วมกับ Level:**
   * สกิลทั้งหมดจะคำนวณความเสียหายโดยอิงจาก `EnemyStats.AttackPower`
   * เช่น มอนสเตอร์ **Lv. 5** มี $\text{ATK} = 50$:
     * ท่า Basic Attack $(1.0\times)$ ทำดาเมจ $= 50$
     * ท่า Skill $(2.0\times)$ ทำดาเมจ $= 100$
     * ท่า Laser $(2.5\times)$ ทำดาเมจ $= 125$

---

## 11. ระบบความต่างของเลเวล (Level Difference Penalty / Bonus System)

เพื่อสร้างความท้าทาย ป้องกันไม่ให้ผู้เล่นข้ามไปตีมอนสเตอร์เลเวลสูงเกินไปได้ง่ายๆ และป้องกันไม่ให้ผู้เล่นย้อนกลับมาฟาร์มมอนสเตอร์เลเวลต่ำแบบไร้ความเสี่ยง ระบบจึงมีการคำนวณอัตราทดตามผลต่างของเลเวล ($\Delta \text{Level}$) ดังนี้:

### 1. กรณีมอนสเตอร์เลเวลสูงกว่าผู้เล่น (Under-leveled Damage Penalty)
เมื่อผู้เล่นโจมตีมอนสเตอร์ที่มีเลเวลสูงกว่า ดาเมจสุทธิสุดท้ายที่ทำได้หลังหักลบเกราะ (Final Damage) จะถูกลดทอนลงตามระดับขั้น:

| ผลต่างของเลเวล ($\text{Monster Lv} - \text{Player Lv}$) | อัตราลดทอนดาเมจผู้เล่น | ดาเมจสุทธิที่ทำได้จริง |
| :---: | :---: | :---: |
| **ความต่างน้อยกว่า 5 เลเวล** | ไม่ลดดาเมจ ($0\%$) | **$100\%$** (ดาเมจปกติ) |
| **มอนสเตอร์เลเวล > ผู้เล่น + 5** | ดาเมจสุดท้าย **$-10\%$** | **$90\%$** |
| **มอนสเตอร์เลเวล > ผู้เล่น + 10** | ดาเมจสุดท้าย **$-20\%$** | **$80\%$** |
| **มอนสเตอร์เลเวล > ผู้เล่น + 15** | ดาเมจสุดท้าย **$-30\%$** | **$70\%$** |
| **มอนสเตอร์เลเวล > ผู้เล่น + 20 ขึ้นไป** | ดาเมจสุดท้าย **$-40\%$** | **$60\%$** |

> [!WARNING]
> การลดทอนนี้คำนวณที่ **"ดาเมจสุดท้าย" (Final Damage)** หมายความว่าคำนวณหลังหักลบค่าพลังป้องกัน (`DEF`) ของมอนสเตอร์แล้ว แต่จะยังมีค่าดาเมจขั้นต่ำอย่างน้อย 1 เสมอ (`Mathf.Max(1f, actualDamage)`)

---

### 2. กรณีผู้เล่นเลเวลสูงกว่ามอนสเตอร์ (Over-leveled EXP Penalty)
เมื่อผู้เล่นมีเลเวลสูงกว่ามอนสเตอร์เกินเกณฑ์ที่กำหนด ค่าประสบการณ์ (`EXP Reward`) ที่ได้รับเมื่อมอนสเตอร์ตายจะถูกหักลดทอนโทษ (EXP Penalty) เพื่อผลักดันให้ผู้เล่นเดินทางไปผจญภัยในพื้นที่ใหม่:

| ผลต่างของเลเวล ($\text{Player Lv} - \text{Monster Lv}$) | อัตราหักลดโทษ EXP (Penalty) | ค่า EXP สุทธิที่ได้รับจริง |
| :---: | :---: | :---: |
| **ความต่างน้อยกว่า 5 เลเวล** | ไม่หัก EXP ($0\%$) | **$100\%$** (เต็มจำนวน) |
| **ผู้เล่นเลเวล > มอนสเตอร์ + 5** | หักลด EXP Penalty **$-10\%$** | **$90\%$** ของค่าที่สุ่มได้ |
| **ผู้เล่นเลเวล > มอนสเตอร์ + 10 ขึ้นไป** | หักลด EXP Penalty **$-20\%$** | **$80\%$** ของค่าที่สุ่มได้ |

*(หมายเหตุ: สามารถปรับแต่งค่าเปอร์เซ็นต์หักลดโทษเพิ่มเติมได้ในสคริปต์ `EnemyStats.cs` หากต้องการเพิ่มความเข้มงวดในการฟาร์ม เช่น ลดเหลือ 50% หรือ 10%)*

---

### 3. ตัวอย่างการทำงานในโค้ด C# (`EnemyStats.cs`)

```csharp
// คำนวณลดทอนดาเมจเมื่อมอนสเตอร์เลเวลสูงกว่าผู้เล่น
public void TakeDamage(DamageData damageData)
{
    if (IsDead) return;

    float actualDamage = Mathf.Max(1f, damageData.amount - _defense);

    var player = FindAnyObjectByType<TheLastKnight.Stats.PlayerStats>();
    if (player != null && _useLevelScaling)
    {
        int levelDiff = _level - player.Level;
        if (levelDiff >= 20) actualDamage *= 0.60f;      // -40%
        else if (levelDiff >= 15) actualDamage *= 0.70f; // -30%
        else if (levelDiff >= 10) actualDamage *= 0.80f; // -20%
        else if (levelDiff >= 5)  actualDamage *= 0.90f; // -10%

        actualDamage = Mathf.Max(1f, actualDamage);
    }

    CurrentHealth = Mathf.Max(0f, CurrentHealth - actualDamage);
    // ...
}

// คำนวณหักลดโทษ EXP เมื่อผู้เล่นเลเวลสูงกว่ามอนสเตอร์
private void Die()
{
    // ...
    var player = FindAnyObjectByType<TheLastKnight.Stats.PlayerStats>();
    if (player != null)
    {
        int finalExp = _expReward;
        if (_useLevelScaling)
        {
            int playerAdvantage = player.Level - _level;
            if (playerAdvantage >= 10) finalExp = Mathf.Max(1, Mathf.RoundToInt(_expReward * 0.80f)); // ลด 20%
            else if (playerAdvantage >= 5) finalExp = Mathf.Max(1, Mathf.RoundToInt(_expReward * 0.90f)); // ลด 10%
        }

        player.AddGold(_goldReward);
        player.AddEXP(finalExp);
        FloatingCombatText.Show(transform.position, $"+{_goldReward} Gold / +{finalExp} EXP", Color.yellow);
    }
}
```

---

## 12. ระบบการเกิดใหม่ของมอนสเตอร์ (Monster Respawn System)

เพื่อรองรับเกมเพลย์ที่มีการฟาร์มมอนสเตอร์ หรือมอนสเตอร์ประจำจุดในแมพที่ต้องการให้เกิดใหม่ได้ ระบบได้เตรียมฟังก์ชันการเกิดใหม่ (Respawn) ในตัว Component [`EnemyController`](file:///c:/Users/aldie/Team-Project-The-Last-Knight/Assets/Scripts/AI/EnemyController.cs) ไว้ดังนี้:

### 1. การตั้งค่าในหน้าต่าง Inspector
เปิด Prefab หรือเลือกมอนสเตอร์ตัวที่อยู่ใน Scene ไปที่ Component **`EnemyController`** ในหัวข้อ **`Death & Respawn Settings`**:

* **`Can Respawn`** (ติ๊กเปิด/ปิด):
  * **ปิด (False):** มอนสเตอร์จะตายแล้วหายไปถาวร (`Destroy(gameObject)`) เหมาะสำหรับบอส มินิบอส หรืออีเวนต์ครั้งเดียว
  * **เปิด (True):** มอนสเตอร์จะสามารถเกิดใหม่ได้ตามเวลาและเงื่อนไขที่กำหนด
* **`Respawn Time`** (วินาที):
  * กำหนดระยะเวลาที่มอนสเตอร์ต้องรอหลังตาย ก่อนจะพยายามเกิดใหม่ (เช่น `15.0` หรือ `30.0` วินาที)
* **`Death Destroy Delay`** (วินาที):
  * เวลาหน่วงสำหรับเล่นอนิเมชันตอนตายให้จบ (ค่าเริ่มต้น `1.5` วินาที)

---

### 2. เงื่อนไขระยะการมองเห็นของผู้เล่น (Player Proximity & Sight Check)

> [!IMPORTANT]
> **กฎการเกิดใหม่:** มอนสเตอร์จะไม่เกิดเด็ดขาด หากผู้เล่นยังคงยืนอยู่ใกล้จุดเกิดในระยะที่เท่ากับระยะการมองเห็นของมอนสเตอร์ตัวนั้น (`Detection Range`)

* **หลักการทำงาน:**
  1. เมื่อเวลาคูลดาวน์ `Respawn Time` หมดลง ระบบจะทำการวัดระยะห่างระหว่างตำแหน่งผู้เล่น (`Player`) กับจุดเกิดเดิมของมอนสเตอร์ (`Spawn Position`)
  2. หากระยะห่าง **$\le \text{Detection Range}$** (ผู้เล่นยังยืนอยู่ในรัศมีสายตา):
     * มอนสเตอร์จะ **"ยังไม่เกิด"** และรอตรวจเช็คใหม่อย่างต่อเนื่องทุก 0.5 วินาที
  3. เมื่อผู้เล่นเดินออกห่างจนระยะ **$> \text{Detection Range}$** (พ้นสายตามอนสเตอร์แล้ว):
     * มอนสเตอร์จึงจะทำการเกิดใหม่ (Respawn) ทันที เพื่อป้องกันไม่ให้มอนสเตอร์โผล่มาทับหรือจ๊ะเอ๋ใส่ผู้เล่นต่อหน้าต่อตา

---

### 3. ลำดับขั้นตอนการทำงาน (Respawn Lifecycle)

```mermaid
sequenceDiagram
    participant Player as ผู้เล่น
    participant Enemy as มอนสเตอร์ (EnemyController)
    participant Stats as สเตตัส (EnemyStats)

    Enemy->>Enemy: ถูกกำจัด (HP <= 0)
    Enemy->>Enemy: เล่นอนิเมชันตาย (IsDead = true) & ปิด Colliders
    Note over Enemy: รอเวลา Death Destroy Delay (1.5s)
    Enemy->>Enemy: ซ่อนภาพ (SpriteRenderer / HealthBar = false)
    Note over Enemy: นับเวลาถอยหลังตาม Respawn Time (เช่น 30s)
    
    loop ตรวจสอบทุก 0.5 วินาที
        Enemy->>Player: เช็คระยะ Vector2.Distance(Player, SpawnPoint)
        alt ระยะ <= Detection Range (ผู้เล่นยังอยู่ใกล้)
            Enemy-->>Enemy: พักรอ ยังไม่เกิด
        else ระยะ > Detection Range (ผู้เล่นเดินออกไปแล้ว)
            Enemy-->>Enemy: ผ่านเงื่อนไข! เริ่มกระบวนการเกิดใหม่
        end
    end

    Enemy->>Enemy: ย้ายกลับตำแหน่งจุดเกิดเดิม (Spawn Position)
    Enemy->>Enemy: รีเซ็ตความเร็ว & เปิด Colliders / Rigidbody
    Enemy->>Stats: ฟื้นฟูเลือดเต็มหลอด (Revive / HP เต็ม)
    Enemy->>Enemy: เล่นอนิเมชัน Idle & เปิดแสดงผล Sprite / Canvas
    Enemy->>Enemy: พร้อมตรวจจับและต่อสู้อีกครั้ง!
```

---

### 4. การแสดงรัศมีตรวจสอบใน Scene View (Editor Gizmos)
* เมื่อคลิกเลือกมอนสเตอร์ตัวที่มีการเปิด **`Can Respawn = True`** ในหน้าต่าง Scene:
* ตัว Editor จะวาดวงกลมเส้นสีฟ้าใส (**Cyan Wire Sphere**) แสดงรัศมีระยะการมองเห็น (`Detection Range`) ล้อมรอบจุดเกิดให้เห็นได้ทันที ทำให้จัดวางตำแหน่งจุดเกิดและประเมินระยะทางได้อย่างแม่นยำ

---

## 13. ระบบลาดตระเวนรอบจุดเกิดและการรีเซ็ตมอนสเตอร์ (Patrol, Leash & HP Reset System)

สำหรับมอนสเตอร์ทั่วไปประจำฉากที่ไม่ใช่บอส ระบบได้ออกแบบพฤติกรรม AI ให้รักษาอาณาเขตของตนเอง ไม่วิ่งเตลิดข้ามฉากตามผู้เล่นไปเรื่อยๆ และป้องกันไม่ให้ผู้เล่นใช้เทคนิคตอดดาเมจทีละนิดแล้ววิ่งหนี:

### 1. ลำดับพฤติกรรม 3 เฟส (AI 3-Phase Behavior)

1. **เฟสลาดตระเวนรอบจุดเกิด (Patrol Phase):**
   * มอนสเตอร์จะเดินลาดตระเวนระยะสั้นๆ รอบจุดเกิดของตนเอง (`Spawn Position`) ในระยะ **`Patrol Distance`** (ประมาณ 2–3 เมตร)
   * จุดศูนย์กลางการลาดตระเวนจะถูกล็อคไว้ที่จุดเกิดเดิมเสมอ ทำให้มอนสเตอร์ประจำอยู่ตามโซนที่จัดวางไว้
2. **เฟสจู่โจม (Engage & Chase Phase):**
   * เมื่อผู้เล่นก้าวเข้ามาในระยะสายตา (**`Detection Range`**) มอนสเตอร์จะสลับเข้าสู่สถานะวิ่งไล่ล่า (`Chase`) และเข้าโจมตีด้วย Basic Attack หรือ Skills
3. **เฟสหยุดไล่ตามและกลับจุดเกิด (Leash & Return Phase):**
   * **เงื่อนไขสำคัญ:** หากผู้เล่นวิ่งหนีออกไปไกลเกิน **2 เท่าของระยะสายตามอนสเตอร์ตัวนั้นจากจุดเกิด** ($\text{Distance} > \text{Detection Range} \times 2$) หรือตัวมอนสเตอร์ถูกล่อออกไปไกลเกินขอบเขตนี้:
     * มอนสเตอร์จะ **"หยุดไล่ตามทันที"**
     * สลับเข้าสู่สถานะ `ReturningToSpawn` แล้วหันหลังวิ่งกลับไปยังจุดเกิดเดิม (`Spawn Position`)
     * **การฟื้นฟู HP:** เมื่อเดินกลับถึงจุดเกิด มอนสเตอร์จะ **"ฟื้นฟูเลือดกลับมาเต็มหลอด (Full HP)"** ทันที พร้อมแสดงข้อความ `Full HP` สีเขียว แล้วกลับเข้าสู่เฟสลาดตระเวนตามปกติ

```mermaid
flowchart LR
    A["จุดเกิด (Spawn Point)<br>เดินลาดตระเวนระยะสั้นๆ"] -- "พบผู้เล่นในระยะสายตา<br>(<= Detection Range)" --> B["วิ่งไล่ล่าและโจมตี<br>(Chase / Attack)"]
    B -- "ผู้เล่นหนีออกไปไกลเกิน 2 เท่า<br>(> 2x Detection Range)" --> C["หยุดไล่ตาม (Leash)<br>เดินกลับจุดเกิดเดิม"]
    C -- "ถึงจุดเกิดแล้ว" --> D["ฟื้นฟูเลือดเต็มหลอด (Full HP)<br>กลับสู่สถานะลาดตระเวน"]
    D --> A
```

---

### 2. ข้อยกเว้นสำหรับบอส (Boss Monsters)

* ใน Component **`EnemyController`** จะมีตัวเลือก **`Is Boss`**:
  * **มอนสเตอร์ทั่วไป (`Is Boss = False`):** มีระบบหลุดระยะและวิ่งกลับจุดเกิดพร้อมฟื้นเลือดตามเงื่อนไข $2\times \text{Detection Range}$
  * **บอสมินิบอส (`Is Boss = True` เช่น `DemonBoss`, `ShadowDemonDragon`):** จะ **ไม่มีการหลุดระยะ (No Leash)** มอนสเตอร์จะไม่วิ่งกลับจุดเกิดหรือรีเซ็ตเลือดกลางคัน เพื่อรักษาการต่อสู้ในห้องบอสให้ดำเนินไปจนจบ

---

### 3. การตั้งค่าในหน้าต่าง Inspector (`EnemyController`)

| ตัวแปรใน Inspector | ค่าแนะนำ | คำอธิบาย |
| :--- | :---: | :--- |
| **`Is Boss`** | `False` (ทั่วไป) / `True` (บอส) | เลือกว่าเป็นบอสหรือไม่ (ถ้าเป็นบอสจะไม่ Leash) |
| **`Patrol Distance`** | `2.5 - 3.5` เมตร | ระยะเดินลาดตระเวนสั้นๆ ซ้าย-ขวารอบจุดเกิด |
| **`Detection Range`** | `6.0 - 8.0` เมตร | ระยะสายตามองเห็นผู้เล่น |
| **`Leash Range Multiplier`** | `2.0` (2 เท่า) | ตัวคูณคำนวณระยะหลุดการต่อสู้ ($\text{Detection Range} \times 2$) |

---

### 4. เส้นจำลองภาพใน Scene View (Visual Gizmos)
เมื่อคลิกเลือกตัวมอนสเตอร์ใน Unity Scene View จะแสดงเส้นไกด์ไลน์ 3 ชั้น:
1. **เส้นสีเขียว (Green Line):** ระยะเดินลาดตระเวนสั้นๆ รอบจุดเกิด (`Patrol Distance`)
2. **วงกลมสีเหลือง (Yellow Sphere):** รัศมีระยะสายตาตรวจจับผู้เล่น (`Detection Range`)
3. **วงกลมสีแดง (Red Sphere):** ขอบเขตระยะหลุดการต่อสู้ ($2\times \text{Detection Range}$) ถ้าผู้เล่นหลุดพ้นวงนี้ มอนสเตอร์จะเลิกตามและกลับจุดเกิดทันที

---

## 14. ระบบ Parry มอนสเตอร์ และการตัดสินใจเลือกท่าร่วมกับผู้สร้าง (Monster Parry System & Per-Monster Consultation)

เป้าหมายสูงสุดของการพัฒนาอนิเมชันคือ **"ให้มอนสเตอร์ทุกตัวมีโอกาสออกทุกท่าอนิเมชันที่มีใน Asset Pack"** พร้อมทั้งผสานเข้ากับระบบ Parry เพื่อสร้างจังหวะเกมเพลย์ Action ที่ลึกซึ้ง ท้าทาย และเป็นธรรมต่อผู้เล่น

---

### 1. กฎและเงื่อนไขของระบบ Parry (Core Parry Rules)

```mermaid
flowchart TD
    A["มอนสเตอร์ตัดสินใจโจมตี"] --> B{"มอนสเตอร์มีท่า Skill หรือไม่?"}
    
    B -- "มี Skills" --> C{"ท่าที่ออกเป็น Skill หรือ Basic?"}
    C -- "ออกท่า Skill" --> D{"Skill นี้ตั้งค่า isParryable = True หรือไม่?"}
    D -- "ใช่ (Parryable Skill)" --> E["★ แสดงวง Timing Ring สีเหลือง<br>เริ่มนับ Windup Window (0.6s)"]
    D -- "ไม่ใช่" --> F["โจมตีปกติ (ไม่มีวง Parry)"]
    C -- "ออกท่า Basic Attack" --> F
    
    B -- "ไม่มี Skill (มีแต่ Basic Attack)" --> G{"คูลดาวน์ Parry หมดหรือยัง?<br>(Cooldown >= 4.0 วินาที)"}
    G -- "หมดแล้ว (พร้อมขึ้นวง)" --> H["★ แสดงวง Timing Ring สีเหลือง<br>รีเซ็ตเวลาคูลดาวน์ Parry 4 วินาที"]
    G -- "ยังติดคูลดาวน์ (< 4s)" --> I["โจมตี Basic Attack ธรรมดา<br>(ไม่มีวง Parry)"]
    
    E --> J{"ผู้เล่นกดโจมตีสวนในจังหวะ Perfect Window?"}
    H --> J
    J -- "Parry สำเร็จ" --> K["• มอนสเตอร์ Stagger ชะงัก 1.5s<br>• ติดสถานะ Stunned<br>• ยกเลิกท่าโจมตีทันที (CancelAttack)<br>• เล่น SFX 'parry' และขึ้นตัวหนังสือ PARRY<br>• ผู้เล่นได้อมตะชั่วขณะ และการันตี Critical Hit"]
    J -- "ไม่กด / ผิดจังหวะ" --> L["มอนสเตอร์ปล่อยการโจมตีสำเร็จ<br>ผู้เล่นได้รับดาเมจเต็มตามสูตร"]
```

#### กฎข้อที่ 1: มอนสเตอร์ที่มีท่าสกิล (Monsters with Skills)
* มอนสเตอร์แต่ละตัวจะต้องมี **อย่างน้อย 1 สกิลที่ผู้เล่นสามารถ Parry ได้** (`isParryable = true`)
* เมื่อเริ่มใช้สกิลนี้ วงกลมสีเหลือง (Parry Timing Ring) จะหดตัวเข้าสู่จุดศูนย์กลางเพื่อบอกจังหวะให้ผู้เล่นกดสวน
* สกิลอื่นๆ ที่เหลือ หรือท่าโจมตีพื้นฐาน (Basic Attack) ของมอนสเตอร์กลุ่มนี้ **จะไม่ขึ้นวง Parry** เพื่อให้ผู้เล่นต้องสังเกตท่าทางและรอจังหวะสวนเฉพาะท่าไม้ตาย

#### กฎข้อที่ 2: มอนสเตอร์ที่ไม่มีสกิล (Monsters without Skills - Basic Attack Only)
* สำหรับมอนสเตอร์ที่มีเฉพาะท่าฟันดาบ/โจมตีปกติเพียงท่าเดียว ไม่มีสกิลยิงเวทหรือท่ากระโดดทุบ
* วง Parry จะปรากฏขึ้นกับการโจมตีปกติ (Basic Attack)
* **เงื่อนไขคูลดาวน์ขั้นต่ำ:** การขึ้นวง Parry ของท่า Basic Attack จะมีคูลดาวน์ **อย่างน้อย 4 วินาที** (`_basicParryCooldown >= 4.0f`)
  * การฟันครั้งแรก -> แสดงวง Parry ให้ผู้เล่นสวนได้
  * หากผู้เล่นไม่สวน และมอนสเตอร์ฟันซ้ำภายใน 4 วินาที -> **จะไม่มีวง Parry ขึ้น** (ผู้เล่นต้องใช้การกลิ้งหลบหรือยกโล่บล็อกแทน) จนกว่าจะพ้นระยะเวลา 4 วินาที

---

### 2. โครงสร้างข้อมูลและโค้ด C# (C# Implementation Architecture)

#### 1) คลาส `EnemySkill` (`Assets/Scripts/Combat/EnemySkill.cs`)
กำหนดโครงสร้างข้อมูลของแต่ละสกิลเพื่อให้สามารถปรับแต่งใน Inspector ได้แบบอิสระ:
```csharp
[System.Serializable]
public class EnemySkill
{
    public string skillName = "Heavy Smash";      // ชื่อเรียกสกิล
    public string animationName = "Attack3";        // ชื่อ State หรือ Trigger ใน Animator
    public int actionIndex = -1;                   // ActionIndex ใน Controller (-1 = ไม่ใช้)
    [Range(0.5f, 5.0f)]
    public float damageMultiplier = 1.5f;          // ตัวคูณดาเมจจากค่า ATK (เช่น 1.5x, 2.0x)
    public float cooldown = 6.0f;                  // คูลดาวน์เป็นวินาที
    public float minRange = 0f;                    // ระยะใกล้สุดที่ร่ายได้
    public float maxRange = 2.0f;                  // ระยะไกลสุดที่ร่ายได้
    public bool isParryable = true;                // สกิลนี้เปิดให้ Parry หรือไม่
    public GameObject projectilePrefab;            // วัตถุกระสุน (ถ้ามี)
    public GameObject groundSpellPrefab;           // วัตถุเวทมนตร์บนพื้น (ถ้ามี)
}
```

#### 2) ตัวแปรควบคุมใน `EnemyController` (`Assets/Scripts/AI/EnemyController.cs`)
* `_basicAttackMultiplier`: ล็อกไว้ที่ `1.0f` ตามสูตรพื้นฐาน ($\text{Damage} = \text{ATK} \times 1.0$)
* `_basicAttackCanParry`: เปิดการ Parry บนท่าฟันธรรมดาสำหรับตัวที่ไม่มีสกิล
* `_basicParryCooldown`: ตั้งค่าขั้นต่ำ `4.0f` วินาที
* `_skills`: รายการอาเรย์ `EnemySkill[]` รองรับการเพิ่มสกิลได้ไม่จำกัด

---

### 3. ตารางแจกแจงแม่บทมอนสเตอร์ 30 ชนิด และการแมปท่าอนิเมชัน (Complete 30-Monster Master Animation & Parry Mapping Table)

ตารางนี้รวบรวม **ทุกอนิเมชันคลิปที่มีอยู่ใน Asset Pack** ของมอนสเตอร์ทั้ง 30 ชนิด พร้อมข้อเสนอแนะในการจัดกลุ่มท่าโจมตี เพื่อให้ผู้สร้าง (Creator) ใช้ตรวจสอบและตัดสินใจยืนยันรายตัว:

| # | ชื่อมอนสเตอร์ | คลิปอนิเมชันทั้งหมดใน Asset | Basic Attack (1.0x ATK) | ท่า Skills (Multiplier, CD, รูปแบบ) | ท่าที่ Parry ได้ (Parryable) | สถานะการตัดสินใจ |
| :-: | :--- | :--- | :--- | :--- | :--- | :-: |
| 1 | **ForestMushroom** | Attack, AttackWithStun, Die, Hit, Idle, Run, Stun | `Attack` (โขก 1.0x) | • `AttackWithStun` (1.5x, CD 6s, ติด Stun) | `AttackWithStun` | รอผู้ใช้ยืนยัน |
| 2 | **Goblin** | Attack, Attack3, Death, Idle, Run, TakeHit | `Attack` (แทงมีด 1.0x) | • `Attack3` (1.6x, CD 5s, ขว้างระเบิด) | `Attack` หรือ `Attack3` | รอผู้ใช้ตัดสินใจ |
| 3 | **Skeleton** | Attack, Attack3, Death, Idle, Shield, TakeHit, Walk | `Attack` (ฟันดาบ 1.0x) | • `Attack3` (1.4x, CD 4.5s, ปาดาบ)<br>• `Shield` (CD 8s, ยกโล่กัน) | `Attack` (หรือฟันดาบปา) | รอผู้ใช้ตัดสินใจ |
| 4 | **FlyingEye** | Attack, Attack3, Death, Flight, TakeHit | `Attack` (พุ่งกัด 1.0x) | • `Attack3` (1.5x, CD 5s, ยิงลูกตามนตรา) | `Attack` หรือ `Attack3` | รอผู้ใช้ตัดสินใจ |
| 5 | **FantasyMushroom** | Attack, Attack3, Death, Idle, Run, TakeHit | `Attack` (กระแทก 1.0x) | • `Attack3` (1.4x, CD 5s, ยิงสปอร์) | `Attack` หรือ `Attack3` | รอผู้ใช้ตัดสินใจ |
| 6 | **UndeadExecutioner** *(Mini-Boss)* | Attack, Death, Idle, Idle2, Skill1, Summon | `Attack` (ฟันขวาน 1.0x) | • `Skill1` (2.0x, CD 7s, หมุนขวาน)<br>• `Summon` (CD 15s, ปักขวานเสกเวท) | `Skill1` (หมุนขวานยักษ์) | รอผู้ใช้ยืนยัน |
| 7 | **MoonstoneKeeper** | Attack1, Attack2, Dash, Death, Hit, Idle, JumpLoop, JumpStart, Land, Run, RunAlt, Walk | `Attack1` (หมัดฮุก 1.0x) | • `Attack2` (1.8x, CD 6s, ทุบพื้น)<br>• `Dash` (1.3x, CD 5s, พุ่งชน) | `Attack2` (ทุบพื้น) | รอผู้ใช้ยืนยัน |
| 8 | **MechaStoneGolem** | Death, Glow, Idle, Immune, LaserCast, Melee, ShieldCast, Shoot | `Melee` (ทุบหมัดหิน 1.0x) | • `Shoot` (1.5x, CD 5s, หมัดจรวด)<br>• `LaserCast` (2.5x, CD 10s, เลเซอร์)<br>• `ShieldCast` (CD 12s, เกราะหิน) | `Melee` หรือ `Shoot` | รอผู้ใช้ตัดสินใจ |
| 9 | **BlueSlime** | Walk, Attack_1, Attack_2, Attack_3, Dead, Hurt, Idle, Jump, Run+Attack, Run | `Attack_1` (กระโดดชน 1.0x) | • `Attack_2` (1.3x, CD 3.5s)<br>• `Attack_3` (1.6x, CD 6s, ทับแผ่นดิน)<br>• `Run+Attack` (1.2x, CD 5s, สไลด์ชน) | `Attack_3` (หรือ Basic CD 4s) | รอผู้ใช้ตัดสินใจ |
| 10 | **Satyr** | Attack1, Attack2, Cast, Death, Defend, Hurt, Idle, Jump, Run, Skill1, Skill2 | `Attack1` (แทงหอก 1.0x) | • `Attack2` (1.4x, CD 4s, ฟันเฉือน)<br>• `Skill1` (1.8x, CD 7s, กระโดดถีบ)<br>• `Skill2 / Cast` (2.0x, CD 9s, เวท) | `Skill1` (กระโดดถีบ) | รอผู้ใช้ยืนยัน |
| 11 | **Necromancer** | Attack1, Attack2, Attack3, Death, Hurt, Idle, Move | `Attack1` (ยิงกระสุนมืด 1.0x) | • `Attack2` (1.6x, CD 5s, คลื่นวิญญาณ)<br>• `Attack3` (2.2x, CD 9s, ลำแสงระเบิด) | `Attack3` (ท่าชาร์จใหญ่) | รอผู้ใช้ยืนยัน |
| 12 | **SkeletonKnight** | Death, DownSwing, FullCombo, FwdSwing, Hurt, Idle, SideSwing, Walk | `FwdSwing` (ฟันหน้า 1.0x) | • `DownSwing` (1.6x, CD 5s, ฟันผ่าลง)<br>• `SideSwing` (1.3x, CD 4s, ฟันกวาด)<br>• `FullCombo` (2.2x, CD 9s, คอมโบ 3 ฮิต) | `DownSwing` (ผ่าดาบหนัก) | รอผู้ใช้ยืนยัน |
| 13 | **DemonBoss** *(Boss)* | Appear, Attack_01, Attack_02, Death, Hit, Idle, Jump, Run, Shout, Walk | `Attack_01` (ตบกรงเล็บ 1.0x) | • `Attack_02` (1.8x, CD 5.5s, กรงเล็บคู่)<br>• `Jump` (1.6x, CD 8s, โดดทับ)<br>• `Shout` (CD 12s, คำรามผลัก) | `Attack_02` (กรงเล็บคู่) | รอผู้ใช้ยืนยัน |
| 14 | **BringerOfDeath** | Attack, Cast, Death, Hurt, Idle, Spell, Walk | `Attack` (ฟันเคียว 1.0x) | • `Cast / Spell` (2.0x, CD 8s, เสาเพลิงนรก) | `Attack` (จังหวะฟันเคียว) | รอผู้ใช้ยืนยัน |
| 15 | **Fox** | Appear, Attack, Death, Disappear, Hit, Idle, Run | `Attack` (ข่วนกรงเล็บ 1.0x) | • `Disappear / Appear` (1.5x, CD 6s, วาร์ปโจมตี) | `Appear / Attack` | รอผู้ใช้ยืนยัน |
| 16 | **ArchDemon** | BasicAtk, Death, Hurt, Idle | `BasicAtk` (1.0x) | *(ไม่มีสกิล)* | `BasicAtk` (CD >= 4s) | ตามกฎ Basic Parry |
| 17 | **Demon** | Attack, Death, Hurt, Idle, Walk | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 18 | **DemonKin** | BasicAtk, Death, Hurt, Idle, Walk | `BasicAtk` (1.0x) | *(ไม่มีสกิล)* | `BasicAtk` (CD >= 4s) | ตามกฎ Basic Parry |
| 19 | **Dragon** | Attack, Death, Hurt, Idle, Walk | `Attack` (กัด 1.0x) | • `Dragon_FireBall` (1.5x, CD 4.5s) | `Attack` (กัด) | รอผู้ใช้ยืนยัน |
| 20 | **FireWorm** | Attack, Death, GetHit, Idle, Walk | `Attack` (กัด 1.0x) | • `FireWorm_FireBall` (1.4x, CD 4s) | `Attack` (กัด) | รอผู้ใช้ยืนยัน |
| 21 | **Jinn** | Attack, Death, Flight, Hurt, Idle | `Attack` (ตบพลัง 1.0x) | • `Jinn_Magic` (1.8x, CD 6s, เสาเวทพื้น) | `Attack` (ตบพลัง) | รอผู้ใช้ยืนยัน |
| 22 | **Lizard** | Attack, Death, Hurt, Idle, Walk | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 23 | **Minotaur_1** | Attack, Dead, Hurt, Idle, Walk | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 24 | **Minotaur_2** | Attack, Dead, Hurt, Idle, Walk | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 25 | **Minotaur_3** | Attack, Dead, Hurt, Idle, Walk | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 26 | **Reaper** | HostileAttack, HostileIdle, HostileRunning, PassiveIdle, PassiveRunning, WieldWeapon, HolsterWeapon | `HostileAttack` (1.0x) | *(ไม่มีสกิล)* | `HostileAttack` (CD >= 4s) | ตามกฎ Basic Parry |
| 27 | **ShadowDemonDragon** *(Boss)* | Attack_Left, Attack_Right, Death_Left, Death_Right, Hit_Left, Hit_Right, Idle_Left, Idle_Right, Walk_Left, Walk_Right | `Attack_Left/Right` (1.0x) | สลับทิศทางการโจมตีซ้าย-ขวา | `Attack_Left / Right` (CD 4s) | รอผู้ใช้ยืนยัน |
| 28 | **Skullwolf** | Attack, Death, Hurt, Idle | `Attack` (1.0x) | *(ไม่มีสกิล)* | `Attack` (CD >= 4s) | ตามกฎ Basic Parry |
| 29 | **Small_dragon** | Attack, Death, Hurt, Idle, Walk | `Attack` (พุ่งชน 1.0x) | • `SmallDragon_FireBall` (1.3x, CD 4s) | `Attack` (พุ่งชน) | รอผู้ใช้ยืนยัน |
| 30 | **Trader_1** | Approval, Dialogue, Idle, Idle_2, Idle_3 | *(ไม่มีท่าโจมตี)* | NPC พ่อค้า ไม่ต่อสู้ | *(ไม่สามารถ Parry)* | NPC ปลอดภัย |

---

### 4. แผนงานการปรึกษาและปรับจูนร่วมกับผู้สร้างทีละตัว (Creator Consultation Workflow)

เนื่องจากเกมเพลย์และ Timing ของแต่ละท่าขึ้นอยู่กับความรู้สึกในการเล่นจริง (Game Feel) ขั้นตอนการร่วมตัดสินใจจะแบ่งออกเป็น 3 กลุ่มตามลำดับ:

1. **กลุ่มที่ 1 (Focus Batch 1 - มอนสเตอร์ยอดนิยม 5 ตัวแรก):**
   * `ForestMushroom`, `Goblin`, `Skeleton`, `UndeadExecutioner`, `BlueSlime`
   * ผู้สร้างร่วมตัดสินใจ: ท่าไม้ตายใดที่อยากให้เป็นท่า Parry หลัก, ความเร็วคูลดาวน์ และตัวคูณดาเมจ
2. **กลุ่มที่ 2 (Focus Batch 2 - มอนสเตอร์สายเวทและมอนสเตอร์ร่างใหญ่):**
   * `MoonstoneKeeper`, `MechaStoneGolem`, `Satyr`, `Necromancer`, `SkeletonKnight`, `DemonBoss`, `BringerOfDeath`, `Fox`
   * ผู้สร้างร่วมตัดสินใจ: การใช้ท่ายิงกระสุน, ระยะเวทมนตร์บนพื้น, และคอมโบต่อเนื่อง
3. **กลุ่มที่ 3 (Focus Batch 3 - มอนสเตอร์สายโจมตีเดี่ยว):**
   * ยืนยันการใช้กฎ Basic Attack Parry Cooldown 4 วินาที และความเร็วแอนิเมชัน





