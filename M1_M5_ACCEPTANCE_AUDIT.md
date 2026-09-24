# M1–M5 acceptance audit

Date: 2026-09-23. Baseline: local commit `4a41463`.

## Connectivity recovery and fresh tests

Later on 2026-09-23, direct HTTP initialization at the configured `http://127.0.0.1:8090/mcp` succeeded. A new MCP session can read the project instance, editor state and Console, and execute tests. The built-in connector's earlier HTTP 404 does not establish that the server endpoint is absent: a fresh direct session works. The built-in connector itself has not been repaired; the direct session is the working fallback.

Fresh `AnimationTests` EditMode run passed **107/107**, zero failed/skipped, job `d07dbc6bb18b4ea98a39b918a755983c`. Console retrieval also works; its latest sampled errors were Unity AI `NoSubscription` exceptions. These results supersede the live-access blocker below, but do not establish a clean gameplay Console or complete physical-route acceptance. No Unity restart, package/configuration changes or real-save writes were needed for recovery.

This audit compares PLAN.md and PLAN_HUMAN.md with current source, serialized scenes, and existing QA reports. **Final acceptance is not granted.** Unity MCP failed its initialization with HTTP 404 on both editor-state and Console requests. No fresh runtime tests, Console verification or screenshots were possible in this audit. Unity processes are present; they were not stopped or restarted. Reconnection has been requested.

## Evidence and limits

- `Captures/RuntimeQA/20260921-092657/report.txt`: inspected existing report, 66 component integration checks passed. It uses direct component calls and direct damage for several scenarios, so it cannot establish full physical progression.
- `Captures/RuntimeQA/20260923-021111/report.txt`: existing physical route passed Church boss/key/chest, City Medusa and market purchase, then timed out in SuburbToForest. It predates verification of the slope fix.
- `Captures/RuntimeQA/20260923-024320/report.txt`: existing later route aborted with Input System assertions. No complete route success follows from it.
- Prior handoff ran the existing slope tests: 2/2 passed, job `5dae55df0ad64db0a15d0e2955dc1c2b`. These only cover shallow-slope movement and wall blocking.
- Previous animation and save test results recorded in IMPLEMENTATION_STATUS.md are historical, not fresh test results from this audit.

## Requirement-by-requirement assessment

| Task | Current evidence | Acceptance still needed |
|---|---|---|
| M1.1 Attack | PlayerController uses overlap hitbox, IDamageable/DamageData, DEX crit, per-swing deduplication and popup. EnemyStats emits death; EnemyController selects death animation. Existing combat test and physical combat evidence support the flow. | Fresh attack/HP/death animation check against actual map enemies. |
| M1.2 Parry | ParryReceiver defines 0.6s windup, last 0.2s plus 0.1s tolerance, 1.5s stagger, yellow rings and Hard visibility. Player attack grants invulnerability and guaranteed stagger crit. Prior integration checks exercise these behaviors. | Fresh timing/visual test across difficulties. Metallic SFX cannot be accepted with empty audio slots. |
| M1.3 Stamina | Max 100; sprint 15/s, attack 15, dash 20, E 25, T 50. Idle/walk regen 20/s, aura 40/s, Hard multiplier 0.5. MedusaAura configures radius 4. HUD places green STM below HP and binds percentage. | Fresh input rejection, actual world aura radius and rendered HUD check. Prior suite measured 20/40/20 rates (last sample Hard near statue), not every state/cost combination. |
| M2.1 Rewards | EnemyStats defaults to 8 gold/15 EXP, awards once on death and displays popup; HUD reads gold. Prior route earned combat gold. | Fresh reward/HUD/popup check and boss reward values in gameplay. |
| M2.2 Potion | Player prefab and code specify 2.5s; Q input, Drinking state, +50 HP, one potion consumed on completion, damage interruption; inventory starts 3 and caps at 5. Prior report verifies completion/interruption. | Fresh animation/input check; prior capped-heal test alone does not prove exact +50 (earlier 40-to-90 evidence is historical). |
| M2.3 Shop | F world interaction, modal, 150 rune/50 heal/100 stat prices, duplicate/capacity rejection and close behavior exist. Prior integration tests prices/stats; physical route bought Rune 4. | Fresh F/open/purchase/close and visual layout checks, including full inventory rejection. |
| M3.1 Maps | Serialized maps name MoonstoneKeeper in Church, BlueSlime/Skeleton in City, DemonBoss in Market/Castle, Fox/FireWorm in Forest. Church key and final-boss reward flags exist. | Placement/reference inspection alone does not prove functional AI in all five maps; forest/final boss physical verification remains. |
| M3.2 Runes | ChurchKey-gated chest collects index 0; City Medusa index 1; marked Fox death index 2; shop index 3; state lives in GameManager. Prior integration collected all four. | Complete physical source acquisition after slope fix; prior route only proves runes 1, 2, 4. |
| M3.3 Gate VN | Back sprite is serialized; input/controller disabled via GameManager; fixed camera, return button, owned-rune buttons, lit sockets, shake and castle load exist. PLAN permits click as an alternative to dragging. | Fresh view, return path and four-button gate animation/load. Prior integration directly invoked insertion. |
| M4.1A Human audio | `Assets/Audio` is absent and all 17 AudioCatalog slots have null clips. | **Incomplete, human-owned:** supply audio, licensing credits and catalog assignments. No audio was sourced or changed. |
| M4.1B Audio code | Persistent AudioManager has two BGM sources for crossfade and separate SFX gain; master/music/effects settings. Player/enemy/parry/UI event calls exist. Prior temporary-clip tests passed. | Fresh clean Console and clip-backed playback/crossfade. Temporary clips do not prove production audio delivery. |
| M4.2 Save/respawn | F rests and saves through SaveAt; atomic temporary write/replace plus backup; state captures stats/scene/position/runes/inventory; death blocks input and offers respawn/menu. Prior isolated tests cover round-trip/respawn/Continue. | Fresh save/respawn and death-menu interaction, preserving real saves. Current Input System assertion history prevents clean acceptance. |
| M4.3 Story | City new-game intro, Space/click advancement and Skip; final reward triggers rebuilt-Moa ending, timed epilogue, 24s credits and menu return. | Fresh trigger and rendered sequence inspection. Old report verifies ending trigger/Skip, not a newly completed final encounter. |
| M5.1 Difficulty | Damage multipliers match 1/1.3/1.6 and 1/0.7/0.4. Enemy levels/health bars/parry visibility and STM half-regeneration exist. User confirmed HP regen (2% MaxHP/s for Easy/Normal, 1% MaxHP/s for Hard after 5s without damage in Idle/Walk) and UI scope (hide enemy HP bar and parry ring on Hard, hide enemy level on Normal/Hard). All 10 difficulty unit tests pass. | Complete. Ready for full runtime route integration check. |
| M5.2 Menu | MainMenuController builds Play/difficulty/Continue/settings/Exit; Continue checks save; sliders bind audio and save preferences. Existing report tests Continue, but calls NewGame directly. | Fresh clicks through all three difficulty buttons, slider interaction/visuals, missing-save Continue behavior. Exit is wired to Application.Quit; standalone behavior is outside this no-build audit. |

## Next acceptance actions

1. Restore Unity MCP connectivity; inspect fresh compilation/Console state without confusing old errors with new ones.
2. Run existing EditMode tests and isolated runtime integration. Preserve real save and backup contents.
3. Check uncovered menu, HUD, parry and story visuals, then the physical forest/rune/final-boss route after the slope fix.
4. Resolve HP regeneration rules with the user. Keep potion +50 and full Medusa rest requirements intact.
5. Human teammate completes audio sourcing/credits/assignments. No executable, push, merge or gameplay changes were made during this audit.
