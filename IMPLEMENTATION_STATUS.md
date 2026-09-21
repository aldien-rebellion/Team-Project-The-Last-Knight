# PLAN.md implementation evidence

Updated 2026-09-21. The goal is **active**, not complete.

## Current scope

- Implement the AI-assigned work in PLAN.md.
- Use `Assets/sprites/Player/player-behind.png` at DemonCastleEntrance.
- Use `Assets/sprites/Environment/Materials/medusa Statue.png` for save points.
- Do not produce a standalone executable (latest user instruction).
- Audio sourcing and licensing credits remain assigned to the human teammate (M4.1A).

## Verified evidence

- Gate scene serializes `player-behind_0`; runtime uses it with both player input and controller disabled.
- All four save-point scenes (CityCenter, OutdoorMarket, SuburbToForest, DemonCastle) serialize the supplied Medusa sprite.
- Screenshots inspected in `Captures/gate-player-behind-verified.png` and `Captures/medusa-statue-verified.png`.
- Gate's return button loads SuburbToForest; an unowned rune is rejected.
- Legacy gate prompt colliders are disabled to prevent old trigger callbacks displaying overlapping UI.
- Four obsolete entrance test rune pickups are disabled in the saved scene.
- Main menu, Easy new game and intro were exercised previously in Play Mode. Completed potion drinking changed HP 40 to 90 and potions 3 to 2.
- Five EditMode tests passed after the latest code changes, job `dafb4b5f7f964fd59099a8e9a00b568e`: sword hit deduplication/death, complete save round-trip, corrupted-primary backup recovery, invalid-save rejection without overwriting a good save, and corrupt-save rejection without backup.
- Save tests use unique temporary directories. Existing persistent player save and backup were detected and must not be overwritten by QA.
- Latest compilation and sprite Play Mode checks had zero console errors. Earlier material batching warnings are not proof of full-playthrough cleanliness.

## Implemented but not fully accepted yet

Combat/parry/stamina, rewards/potions/shop, persistent state/save/respawn, progression sources, map placements, gate interaction, audio manager, narrative dialogue, difficulty and main menu have implementation code. This is not evidence that every PLAN.md acceptance criterion is satisfied.

## Remaining acceptance work

1. Exercise complete route and real acquisition sources: church boss/key/chest, Medusa rune/save, market purchases, fox rune, four gate insertions and final boss ending. Check traversal, floor placement, arena locks, enemy AI, camera bounds and return paths.
2. Verify stamina costs, aura regeneration, interrupted drinking, parry cancellation/stagger/critical timing, difficulty damage and helper visibility in Play Mode. Newly added Easy-only enemy level labels need visual checking. Simultaneous action inputs now use a single priority chain.
3. Verify save/Continue/respawn through gameplay UI while isolating QA from the existing user save. File-level save tests passed, but they do not prove scene restoration.
4. Complete audio catalog setup and verify playback using test clips without sourcing human-assigned audio. Main menu lacked an AudioListener; its scene still needs updating (the scene factory now includes one).
5. Check floating damage prefab requirement, final-boss ending visuals/credits, shop layout, gate completion animation and settings sliders against PLAN.md.
6. Run appropriate regression checks and final requirement-by-requirement audit; only then finalize branch integration.
7. Git pushes remain unapproved: automatic review rejected the earlier push to the existing GitHub remote because destination trust/privacy were unverified. An explicit approval question was sent; no answer is present in this task's current context. Do not retry a push without resolving this.

The branch is `feature/map-bosses-placement`. Earlier implementation commits are merged locally into main; current map integration and QA fixes are still on the feature branch. Preserve the existing URP camera component change in MainMenu.
