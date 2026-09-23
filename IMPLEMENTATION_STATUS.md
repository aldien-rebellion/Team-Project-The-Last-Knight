# PLAN.md implementation evidence

Updated 2026-09-23. The requested local commit handoff is complete; full PLAN.md acceptance remains outstanding.

## Local commit handoff (2026-09-23)

- Player-behind at the gate and Medusa save-point artwork are already committed. This handoff records the pending slope fix and optional full-route mode in `PlanRuntimeChecks.Run(true)` on `feature/map-bosses-placement`.
- Kinematic collision selection now ignores contacts that do not oppose movement and selects the nearest opposing hit. Existing `SlopeMovementTests` passed 2/2 with no skips (job `5dae55df0ad64db0a15d0e2955dc1c2b`): walking along a shallow slope and stopping at a wall. `git diff --check` passed.
- Physical route report `Captures/RuntimeQA/20260923-021111/report.txt` verifies Church boss/key/upstairs Rune 1, City Medusa Rune 2 and isolated save, market Rune 4 purchase, and arrival in SuburbToForest. It timed out at forest x=-125.5; it does not verify the full route after the slope fix.
- The later route report `Captures/RuntimeQA/20260923-024320/report.txt` was aborted on leaving Play Mode and contains Input System assertions. Both reports confirm the existing user save and backup remained unchanged.
- Unity was idle with compilation complete at handoff. The current Console still contains Input System `Assertion failed` entries during Play Mode state changes; zero-console-error acceptance is not established. No full playthrough was rerun for this commit-only request.

| PLAN.md scope | Handoff status |
|---|---|
| M1, M2 | Implemented; prior component integration checks passed. Full acceptance audit remains. |
| M3.1-M3.3 | Placements, rune sources and gate implemented; player-behind complete. Physical route verified through market and forest arrival; complete route remains. |
| M4.1A | Human teammate audio sourcing, licensing and clip assignment remain. |
| M4.1B, M4.2, M4.3 | Audio code, Medusa save/respawn and story implemented with prior integration evidence; final acceptance remains. |
| M5.1-M5.2 | Difficulty and main menu implemented with prior integration evidence; final acceptance remains. |
| M6.1 | Open: rerun complete physical route after slope fix, finish visual QA, and resolve/recheck Console errors. |
| M6.2 | Not built, following the earlier instruction not to produce an executable. |
| M0 / branch integration | Local feature-branch commit only; main merge and remote push are not completed by this handoff. |

## Current scope

- Implement the AI-assigned work in PLAN.md.
- Use `Assets/sprites/Player/player-behind.png` at DemonCastleEntrance.
- Use `Assets/sprites/Environment/Materials/medusa Statue.png` for save points.
- Do not produce a standalone executable (latest user instruction).
- Audio sourcing and licensing credits remain assigned to the human teammate (M4.1A).

## Verified evidence

- Gate scene serializes `player-behind_0`; runtime uses it with both player input and controller disabled.
- All four save-point scenes (CityCenter, OutdoorMarket, SuburbToForest, DemonCastle) serialize the supplied Medusa sprite.
- Gate and Medusa screenshots were inspected earlier; those capture files were subsequently removed, so they are historical evidence only.
- Gate's return button loads SuburbToForest; an unowned rune is rejected.
- Legacy gate prompt colliders are disabled to prevent old trigger callbacks displaying overlapping UI.
- Four obsolete entrance test rune pickups are disabled in the saved scene.
- Main menu, Easy new game and intro were exercised previously in Play Mode. Completed potion drinking changed HP 40 to 90 and potions 3 to 2.
- Five EditMode tests passed after the latest code changes, job `dafb4b5f7f964fd59099a8e9a00b568e`: sword hit deduplication/death, complete save round-trip, corrupted-primary backup recovery, invalid-save rejection without overwriting a good save, and corrupt-save rejection without backup.
- Save tests use unique temporary directories. Existing persistent player save and backup were detected and must not be overwritten by QA.
- Latest compilation and sprite Play Mode checks had zero console errors. Earlier material batching warnings are not proof of full-playthrough cleanliness.

## Implemented but not fully accepted yet

### Runtime integration verification (2026-09-21)

- 61 checks passed in `Captures/RuntimeQA/20260921-090738/report.txt`. This is component-driven integration, not a physical full-map playthrough.
- Q drinking and F saving used synthetic keyboard input through the focused Game View. Checks covered interrupted drinking, stamina rejection, measured regeneration, damage multipliers, helper visibility, actual enemy windup/parry/stagger/critical, rewards, shop pricing, checkpoint death/respawn, Continue, all four gate sockets and final-story triggering.
- Arena trigger entry enabled barriers, denied travel, and boss death released them. The runner waits for physics processing and gate scene completion instead of assuming editor wall time equals game time.
- Existing real save and backup contents remained unchanged. QA saves use an editor-only path override.
- Temporary in-memory clips verified music/SFX gains and crossfade. Resource AudioCatalog now provides human assignment slots, FloatingCombatText has a resource prefab, and MainMenu has one AudioListener.
- World prompts, floating damage and parry rings now use the InGame_UI sorting layer. Boss death restores the current scene's music. Portals respect input/arena locks before changing arrival state and use F rather than the E skill key.
- Latest AnimationTests passed 105/105, no skips or inconclusive tests (job `14c6dfea71ee42e19afd218cd6050a2e`, 2026-09-22); console returned zero errors. Corrected stale walk expectations to the authored eight poses at 6 fps with pose seven held. Restored missing jump takeoff frame and Run-to-Jump transition. Player prefab now has its default idle sprite and controller; asset tests inspect the prefab independently of the test runner's temporary scene.
- Runtime integration passed 66 checks in `Captures/RuntimeQA/20260921-092657/report.txt`, including synthetic D/Shift movement, Space lift, takeoff animation and physical landing in CityCenter. This proves a short locomotion segment, not full-map traversal. Existing user save and backup remained unchanged.

Combat/parry/stamina, rewards/potions/shop, persistent state/save/respawn, progression sources, map placements, gate interaction, audio manager, narrative dialogue, difficulty and main menu have implementation code. This is not evidence that every PLAN.md acceptance criterion is satisfied.

### Ending artwork provenance

`Assets/Resources/RebuiltMoa.png` was created with the built-in imagegen tool using the supplied player-behind and citycinter3 images as visual references. Final prompt:

> Use case: stylized-concept. Asset: landscape 16:9 victory cutscene illustration for The Last Knight Unity game. Reference 1 defines Arthur: back-facing silver armored knight, worn crimson cape, sword lowered at left. Reference 2 defines kingdom Moa architecture and painterly dark-fantasy game style. Create a NEW composition: Arthur stands in the lower left foreground on a castle stone terrace, back toward viewer, looking over the REBUILT peaceful kingdom at golden dawn. Beyond him a broad valley town of restored timber-and-stone houses with red roofs, an intact Gothic clocktower, busy tiny market silhouettes, trees and distant hills. Hopeful warm light after victory, detailed painted 2D game art matching town reference. Arthur occupies about one quarter of image height, skyline dominates. Keep center and right composition readable for later credits overlay. No writing, no logos, no watermark. All architecture repaired, no fires, no battle. Full bleed.

## Remaining acceptance work

### Physical route QA, 2026-09-22

- `PlanTraversalProbe` drives keyboard movement and timed mouse attacks with no teleportation, direct damage, healing or loot grants. It temporarily clones editor input settings in memory to allow the test to run while Unity is unfocused, and restores the original settings on stop.
- Found and fixed Skeleton Attack/Attack3 states lacking exit transitions. This trapped the AI in the attack animation after its first strike. Both now return to Idle after the clip.
- After the fix, the physical CityCenter-to-Church route passed: five enemies defeated, 40 gold earned, HP 100 at Church arrival `(42.5, -4.54)`. Traversal logs are under `Captures/TraversalQA`. Camera and HUD inspected during combat and arrival.
- The first Church boss attempt ended in death because the probe spent attack cooldowns on late stagger hits, missing subsequent parries. A focused Church run, reserving swings for each windup, defeated MoonstoneKeeper with HP 100, earned ChurchKey and 100 gold, and released the barriers. Arthur traversed the stairs, used F at Church_1 to arrive upstairs, walked to x=76.4, and used F to open the chest and acquire Rune 1. Inspected `Captures/church-rune1-physical-route.png`. The latest console check showed repeated Unity PlayerLoop recursion errors during tool-driven sampling; this run does not establish the zero-console-error acceptance criterion. This focused run started in Church, so a single uninterrupted complete route is still outstanding.
- Workspace now includes user commits through `5e99876` merging main into the feature branch; preserve those changes and re-audit integration before finalizing.

1. Exercise complete route and real acquisition sources: church boss/key/chest, Medusa rune/save, market purchases, fox rune, four gate insertions and final boss ending. Check traversal, floor placement, arena locks, enemy AI, camera bounds and return paths.
2. Visually inspect helper labels, parry rings, floating damage, shop layout, gate animation and settings sliders. Runtime behavior passed the integration checks above.
3. Ending now has rebuilt-Moa artwork, two timed epilogue pages, and a 24-second masked credit roll using unscaled time. Epilogue and sampled midpoint visually inspected in `Captures/ending-epilogue.png` and `Captures/ending-credits-midpoint.png`. Resuming the sampled roll automatically returned to MainMenu with input unblocked and timeScale 1. Updated final-boss/Skip regression passed in the 61-check run `Captures/RuntimeQA/20260921-091947/report.txt`, with zero console errors in that run. Earlier manual tool-driven sampling produced editor PlayerLoop recursion and null errors; these were not reproduced in the clean regression.
4. Audio sourcing and catalog clip assignment remain human work; audio code and assignment slots are verified.
5. Complete traversal and visual QA without treating direct component calls and direct boss damage in the integration suite as a full playthrough.
6. Run appropriate regression checks and final requirement-by-requirement audit; only then finalize branch integration.
7. Git pushes remain unapproved: automatic review rejected the earlier push to the existing GitHub remote because destination trust/privacy were unverified. An explicit approval question was sent; no answer is present in this task's current context. Do not retry a push without resolving this.

The branch is `feature/map-bosses-placement`. Earlier implementation commits are merged locally into main; current map integration and QA fixes are still on the feature branch. Preserve the existing URP camera component change in MainMenu.
