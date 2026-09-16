# The Last Knight: แผนงานและกำหนดส่งมอบ (September 2026 Sprint & Deadline Plan)

> **ช่วงเวลาดำเนินการ:** 16 กันยายน 2026 – 30 กันยายน 2026 (ระยะเวลา 14 วัน)  
> **เป้าหมายสูงสุดประจำเดือน (Final Deliverable):**  
> **"The Last Knight - First Playable Vertical Slice (v0.1) Windows Standalone Build"**  
> **Final Deadline:** **30 กันยายน 2026 เวลา 18:00 น.**

---

## 1. การจัดการ Asset มอนสเตอร์ในโปรเจกต์ (Assets/sprites/Monsters)

จากการสำรวจ Asset มอนสเตอร์ที่มีอยู่ในโฟลเดอร์ Assets/sprites/Monsters พบว่ามีจำนวนมาก เพื่อไม่ให้ทีมงานโหลดงานเกินไปในช่วง 14 วันนี้ จึงจัดกลุ่มและวางลำดับความสำคัญ (Prioritization) ตามประเภทของฉากและระดับความยาก ดังนี้:

### 👾 ลำดับขั้นการนำมอนสเตอร์เข้าสู่เกม (Monster Tier & Distribution)

| ระดับ (Tier) | รายชื่อ Asset มอนสเตอร์ | แมพที่เหมาะสม | รูปแบบการโจมตี / พฤติกรรม | เฟสที่นำเข้า |
| :--- | :--- | :--- | :--- | :---: |
| **Tier 1: Basic Mobs** *(ศัตรูพื้นฐาน)* | • BlueSlime (มี Controller แล้ว)<br>• Forest_Monsters_FREE/Mushroom<br>• Monsters_Creatures_Fantasy (Goblin / Skeleton) | • CityCenter<br>• SuburbToForest | เดินลาดตระเวน (Patrol), กระโดดชน, ฟันระยะประชิดเบาๆ | **Sprint 2** (21-24 ก.ย.) |
| **Tier 2: Ranged / Hazard** *(ศัตรูโจมตีไกล/ป่วน)* | • Fire Worm (พ่นลูกไฟ)<br>• Skullwolf (วิ่งเร็วพุ่งชน)<br>• Monsters_Creatures_Fantasy/Flying eye | • OutdoorMarket<br>• SuburbToForest | ยิงกระสุน/ลูกไฟวิถีตรง, บินโฉบ, วิ่งชาร์จ | **Sprint 2 - 3** (23-26 ก.ย.) |
| **Tier 3: Elite Mobs** *(ศัตรูระดับสูง/มินิบอส)* | • Mecha-stone Golem 0.1<br>• craftpix-net-170637-minotaur<br>• craftpix-net-803217-knight | • Church<br>• ทางเข้า DemonCastle | เลือดเยอะ, ซูเปอร์อาร์เมอร์ (ไม่กระตุกง่าย), ฟันกวาดรุนแรง | **Sprint 3** (25-27 ก.ย.) |
| **Tier 4: Boss** *(บอสประจำฉาก)* | • Undead executioner (บอสประจำสุสาน/โบสถ์)<br>• Bringer-Of-Death (บอสใหญ่ปราสาท)<br>• Shadow_Demon_Dragon (มีไฟล์เสียง Audio พร้อม) | • DemonCastle<br>• Church | มี Pattern ชัดเจน: โจมตี 2-3 ท่า, จังหวะเรียกเวท/เสกดาบ, มีหลอดเลือดบอส | **Sprint 3** (เลือกทำ 1 ตัวหลักก่อน) |

---

## 2. แผนพัฒนาระบบเสียงและดนตรี (Audio & Sound System Plan)

ปัจจุบันในโปรเจกต์มีไฟล์เสียงเฉพาะในแพ็กมังกร Shadow_Demon_Dragon_Asset_Pack/Audio/ เท่านั้น ยังขาดระบบจัดการเสียง (Audio Manager) รวมถึง SFX ของตัวละครผู้เล่น, UI และดนตรีประกอบฉาก (BGM)

### 🔊 สถาปัตยกรรมระบบเสียง (Audio Architecture)
- **AudioManager.cs**: ทำหน้าที่เป็น Singleton กลาง ควบคุม Channel เสียง 3 กลุ่ม:
  1. **Master Volume**: คุมระดับเสียงรวม
  2. **BGM (Background Music)**: เล่นเพลงวนลูปตามฉาก (Fade-in / Fade-out ขณะเปลี่ยนฉาก)
  3. **SFX (Sound Effects)**: เล่นเสียงเอฟเฟกต์แบบ One-shot (รองรับ 2D/3D Positional Audio)

### 🎼 รายการเสียงที่ต้องจัดหาและใส่ในเกม (Audio Asset Checklist)

#### ก. เสียงตัวละคร Arthur (Player SFX)
- [ ] เสียงก้าวเท้า (Footsteps) บนพื้นดิน / พื้นหิน
- [ ] เสียงกระโดด (Jump) และเสียงตกกระทบพื้น (Land)
- [ ] เสียงฟันดาบปกติ (Sword Slash / Whoosh)
- [ ] เสียงดาบฟันโดนศัตรู (Impact / Flesh Hit)
- [ ] เสียงใช้สกิล Excalibur (Magic Burst / Divine Blade beam)
- [ ] เสียงพุ่งแดช (Dash Wind Whoosh)
- [ ] เสียงโดนโจมตี / ร้องเจ็บ (Player Hurt) และเสียงตัวละครตาย (Death)

#### ข. เสียงศัตรู (Enemy & Monster SFX)
- [ ] เสียงสไลม์เดิน/กระโดด (Squish / Bounce)
- [ ] เสียงสไลม์โดนฟัน / แตกตาย
- [ ] เสียงมอนสเตอร์โจมตี (Claw slash / Fireball cast)
- [ ] เสียงบอสคำราม / เสียงโจมตีหนัก (Bringer of Death / Undead Executioner)
- [ ] *หมายเหตุ:* มังกรมีเสียงพร้อมแล้ว (dragon_attack, dragon_hit, dragon_death, dragon_footstep)

#### ค. เสียงหน้าจอและระบบ (UI & System SFX)
- [ ] เสียงกดปุ่มเมนู / UI Hover & Click
- [ ] เสียงกดเพิ่มแต้มสเตตัส (Stat Point Allocate Ding)
- [ ] เสียงเลเวลอัป (Level Up Fanfare)
- [ ] เสียงประตูปราสาท / วาร์ปข้ามแมพ (Portal / Door creak)
- [ ] เสียง Game Over

#### ง. เพลงประกอบฉาก (Background Music - BGM)
- [ ] **Town / Market BGM:** เพลงบรรยากาศเมือง ดาร์กแฟนตาซี สบายๆ แต่หม่นหมอง (CityCenter, OutdoorMarket)
- [ ] **Forest BGM:** ดนตรีแนวลึกลับ ป่าทึบ อันตราย (SuburbToForest)
- [ ] **Castle / Church BGM:** ดนตรีกอทิก (Gothic), เสียงออร์แกน/คอรัสหลอน (Church, DemonCastle)
- [ ] **Boss Fight BGM:** ดนตรีจังหวะเร็ว ระทึกขวัญ ตื่นเต้น

---

## 3. ตารางกำหนดการและเป้าหมายย่อย (Sprint Milestones & Deadlines)

### 🚩 Milestone 1: Map Connection & Audio/Monster Preparation
> ⏰ **Deadline ย่อย:** **20 กันยายน 2026 (23:59 น.)** *(เหลือ 4 วัน)*
* **ระบบฉาก:**
  - เชื่อมโยง 5 แมพ (CityCenter, OutdoorMarket, SuburbToForest, DemonCastle, Church) ด้วย ScenePortal และ RoomDoorway
  - ล็อกขอบเขตกล้อง CameraFollow2D ไม่ให้หลุดนอกฉาก
* **มอนสเตอร์ & กราฟิก:**
  - วาง Background และจุดเกิดของมอนสเตอร์ในแมพป่าชานเมือง
  - เตรียม Sprite Sheet และตัด Sprite สำหรับ Basic Mobs (Mushroom / Skeleton)
* **ระบบเสียง:**
  - รวบรวมไฟล์เสียงฟรีปลอดลิขสิทธิ์ (Itch.io, Freesound, Sonniss) ตาม Audio Checklist ให้ครบชุด
* **เกณฑ์ตรวจรับงาน (DoD):** เดินสำรวจได้ครบทั้ง 5 แมพ ประตูเชื่อมกันได้ถูกต้อง และมีโฟลเดอร์ Assets/Audio/ ที่เตรียมไฟล์เสียงพร้อมใช้งาน

---

### 🚩 Milestone 2: Combat System, Tier 1 Mobs & Sound Integration
> ⏰ **Deadline ย่อย:** **24 กันยายน 2026 (23:59 น.)** *(เหลือ 4 วัน)*
* **ระบบต่อสู้ & มอนสเตอร์:**
  - บรรจุ Slime และ Skeleton/Mushroom ลงในแมพ
  - วางระบบ Hitbox / Hurtbox / Damage Receiver ให้ฟันมอนสเตอร์ตายและดรอป EXP ได้
  - ระบบ i-frame (อมตะชั่วขณะหลังโดนตี) และ Knockback ของ Arthur
* **ระบบเสียง:**
  - สร้างสคริปต์ AudioManager.cs ควบคุม BGM และ SFX
  - เชื่อมต่อ Player SFX: ฟันดาบ, กระโดด, วิ่ง, แดช, สกิล Excalibur
  - เชื่อมต่อ Enemy SFX: เสียงสไลม์กระโดด, เสียงโดนฟัน
* **เกณฑ์ตรวจรับงาน (DoD):** ผู้เล่นฟันศัตรูได้ มีเสียงฟันและเสียงโดนดาเมจชัดเจน ศัตรูเลือดหมดแล้วตายและได้ EXP

---

### 🚩 Milestone 3: Boss/Elite Mobs, Progression, UI & Atmosphere
> ⏰ **Deadline ย่อย:** **27 กันยายน 2026 (23:59 น.)** *(เหลือ 3 วัน)*
* **มอนสเตอร์ขั้นสูง & บอส:**
  - นำเข้าศัตรูระดับ Elite (Minotaur หรือ Golem) และเลือก 1 บอสสำหรับ DemonCastle (เช่น Undead Executioner หรือ Bringer-Of-Death)
  - กำหนด Attack Pattern และห้องบอส (Boss Arena พร้อมประตูปิดกั้น)
* **UI & ระบบความก้าวหน้า:**
  - อัปเดต HUDController แสดงเลือด HP, หลอด EXP, ไอคอน Skill
  - เชื่อมหน้าต่างอัปสเตตัส (STR, VIT, DEX, AGI) ให้กดบวกแต้มแล้วส่งผลกับตัวละครทันที
  - สร้างจุดเซฟ / Checkpoint และหน้าต่าง Game Over
* **ระบบเสียง:**
  - ใส่ BGM ประจำแต่ละแมพ (เมือง, ป่า, ปราสาท, เพลงสู้บอส)
  - ใส่เสียง UI Click, Level Up Fanfare, Game Over Sound
* **เกณฑ์ตรวจรับงาน (DoD):** มีลูปเกมครบ: เกิด -> เดินสำรวจ -> สู้มอนสเตอร์ -> เลเวลอัปเพิ่มสเตตัส -> ปะทะบอสในห้องบอส มีเพลงและเสียงสมบูรณ์

---

### 🚩 Milestone 4: QA Polish, Bug Fixing & Standalone Build
> ⏰ **FINAL DEADLINE:** **30 กันยายน 2026 (18:00 น.)** *(เหลือ 3 วัน)*
* **ตรวจสอบและขัดเกลา (Polish):**
  - Full Playthrough Test ตั้งแต่แมพแรกจนจบห้องบอส
  - มิกซ์เสียง (Audio Balance): ปรับระดับความดังของ BGM ไม่ให้กลบเสียง SFX
  - ปรับสมดุลค่าดาเมจ ความยาก-ง่ายของเกม (Balancing)
  - แก้ไขบั๊กอนิเมชันกระตุก, บั๊กติดซอกกำแพง, บั๊กกล้องสั่น
* **ส่งมอบงาน (Final Build):**
  - สร้าง Windows Standalone Build (.exe)
  - อัปโหลดไฟล์ Zip และทำ Release Note สรุปผลงานของโปรเจกต์
* **เกณฑ์ตรวจรับงาน (DoD):** ไฟล์ .exe สามารถดาวน์โหลดไปเปิดเล่นบนเครื่องคอมพิวเตอร์อื่นได้โดยไม่มี Error หรือ Crash

---

## 4. ตารางสรุปภาพรวม (Summary Roadmap)

| รหัส | เฟสงาน (Milestone) | วันที่ | สิ่งที่ต้องส่งมอบ (Key Deliverables) | สถานะ |
| :---: | :--- | :---: | :--- | :---: |
| **M1** | Map Traversal & Asset Prep | 17 - 20 ก.ย. | เชื่อม 5 แมพ, สไปรต์มอนสเตอร์พร้อม, ไฟล์เสียงพร้อมในโฟลเดอร์ | 🟡 เริ่มต้น |
| **M2** | Combat, Mobs & AudioManager | 21 - 24 ก.ย. | สไลม์ + มอนป่า, ระบบดาเมจ, สคริปต์ AudioManager, SFX ตัวละคร | ⚪ รอคิว |
| **M3** | Boss, UI Stats & Full Audio | 25 - 27 ก.ย. | บอส 1 ตัว, อัปเวล/อัปสเตตัสได้, หลอดเลือด/EXP, BGM ทุกฉาก | ⚪ รอคิว |
| **M4** | QA, Balance & Standalone Build | 28 - 30 ก.ย. | **ไฟล์ Build Windows Standalone (.exe) พร้อมเล่น** (30 ก.ย. 18:00) | ⚪ รอคิว |

---

## 5. กฎเหล็กของทีมสำหรับโค้งสุดท้าย 14 วัน (Team Rules)
1. **Feature Freeze วันที่ 27 ก.ย. 23:59 น.:** ห้ามเพิ่มศัตรูใหม่หรือแมพใหม่หลังวันนี้เด็ดขาด ให้ใช้เวลา 3 วันสุดท้ายเพื่อแก้บั๊กและบาลานซ์เสียงกับเกมเพลย์เท่านั้น
2. **Commit & Pull Request ชัดเจน:** แยก Branch ตาม Milestone เช่น eature/audio-manager, eature/monster-mushroom และห้าม Push ทับ Scene ไฟล์ของเพื่อนตรงๆ
3. **Daily Check-in:** สรุปความคืบหน้ารายวันทุกเย็น 18:00 น. เพื่อดูว่ามีจุดไหนติดขัดและต้องช่วยกันเคลียร์ทันที
