# Real-scene 100-bot combat verification — 2026-11-22

## Scope and baseline

Canonical repository `sundamunda289-art/Unity-Game-Aris`, `main` baseline `0e488388e7bc53f1fc2f0f86393f1bf8b628a872`; Unity **6000.3.26f1**. Real Play Mode test scene: `Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity`. The source already contained the 100-bot, four-tier optic, 200-HP HUD, aim-assist and bidirectional physical damage features before this verification. **This document records observed execution; it does not represent a new implementation of those features.**

Existing connected flows:

- `AarisWeaponCatalog` and `Resources/AarisWeapons/weapon_catalog.json`: shotgun scope 0, SMG scope 1, assault/battle rifles scope 2, sniper/DMR scope 4; six configured weapon definitions.
- `AarisWeaponLootWorld`, `AarisWeaponInventory`, `AarisEquippedGun`: physical floor/hand weapon state, chamber/magazine/reload transitions, camera-assisted muzzle-validated hitscan, head/body receiver damage, pooled firing/impact visuals.
- `AarisOfflineBRDirector`: 100 rival slots, a staged player-attacker selection (12 candidates per frame, max two selected attackers), visibility/LOS gating, bot-versus-bot combat, cover, medkits, loot and distance-based simulation/visual LOD.
- `AarisOfflineHealth`, `AarisCombatFeedbackHUD`: shared damage receiver, 200-HP default, headshot/helmet logic, red enemy reticle, optic labels/ring, hit flash/direction, cached floating-damage and HP labels.

## Live tests executed on original Codespace

- **Combat Python tests:** `python3 -m unittest discover -s Tools/Validation -p 'test_combat*.py'` — **36/36 passed**, exit 0.
- **Native C# source-only compile:** `bash Tools/Codespaces/native_source_compile_sandbox.sh` — 97 game source scripts, Unity Editor exit 0, 0 C# errors, sentinel executed; source-only copy under `/tmp`, not a full shipping asset build.
- **Original authored scene 25-second soak:** `AARIS_FULLSCENE_SOAK_SECONDS=25` with `Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run`, using the installed Unity Linux Editor in batchmode and `xvfb-run` — Editor exit 0, report `RESULT=PASS`, **100/100 bots spawned and initially active**, 1,419 loaded frames over **25.000 game seconds**, 74 incoming player damage events, and zero parsed C# compile errors.
- The 25-second scene QA passed actual **No Scope, Normal, 2X and 4X** red-lock and physical shot tests, standing/crouching/prone 2X physical shots, **wall obstruction preventing both lock and player damage**, initial `HP 200 / 200` health bar, later `HP 175 / 200` (87.5% health bar) and incoming hit flash. Peak visual sampling recorded 6 nearby humanoid rigs with 21 visible rival bodies.
- **Default short PlayMode QA:** 100/100 spawned, 4/4 optic checks and bidirectional HP tests passed. The existing loaded-revisit scenario reported `PASS distant rig and PlayableGraph retired: 6 -> 1`, `PASS original humanoid rig restored with 1 renderers`, `RESULT=PASS`.

## Measured headless Editor profiling (not phone data)

| Measure | 0-bot baseline | 100-bot 25-second loaded phase |
| --- | ---: | ---: |
| Frame mean (ms) | 16.731 | 16.667 |
| Frame p95 (ms) | 16.737 | 16.739 |
| Main thread mean (ms) | 16.792 | 16.664 |
| GC allocated in frame mean (bytes) | 174,891.4 | 163,521.8 |

This **does not** establish Snapdragon 680 / Adreno 610 frame rate, battery or thermal stability. The nonzero GC readings also preclude claiming absolute zero-GC play. Dense physics buffer saturation retains a rare correctness-first `RaycastAll` fallback; source-code caching/pooling is not evidence of zero managed allocations in all cases.

## Other validation and preservation

The full local Python suite ran **925 tests; 1 failed, 2 skipped**. The single failure (`test_cc0_motion_provenance_and_guid_pairs`) expects `hasTranslationDoF: 1` in five humanoid motion FBX import metadata files; the live Codespace has **pre-existing uncommitted** local `hasTranslationDoF: 0` while those five committed `main` files use `1`. That failure is an uncommitted animation-importer discrepancy, **not a passing full-suite claim** and not evidence of a gun-damage failure. Do not silently overwrite or stage these local user/agent edits.

The original workspace contained unrelated modified and untracked importer, material and package files; before/after the 25-second PlayMode test, `git status --porcelain` was unchanged. All test diagnostics were directed to `/tmp`. No new worktree was made. Unity Personal license files, credentials and private logs were not put into version control.

## Remaining acceptance gates

- Measure real Android player visibility, gameplay and frame-time (including p95/p99), CPU/GPU/thermal pressure and GC on Snapdragon 680 / Adreno 610; Linux Editor metrics cannot substitute.
- Resolve the importer metadata conflict only after animation visual/retarget checks across standing, crouching, prone and all stance transitions.
- A full Android APK build/release has not been verified by the tests above.

This QA artifact changes **documentation only**: no production C#, .unity, .prefab, .fbx.meta or .mat assets are modified here.
