# Live-scene guns, Red-Lock optics and 200-HP HUD — Unity verification (2026-11-22)

## Scope

This test extends `AarisFullSceneCombatSoak.Run` (Editor-only batchmode test) in Unity **6000.3.26f1**. It uses the original authored `TerrainDemoScene.unity`, the saved player camera and CharacterController, the actual `AarisWeaponInventory` and bundled FBX gun models, `AarisEquippedGun.TryFire`, the active `AarisCombatFeedbackHUD` and an actual `AarisOfflineRival` chosen from the 100 successfully spawned bots.

The test first runs the existing **100 real terrain spawn + incoming damage + real Humanoid visuals** soak. For deterministic shot/stance tests, it *temporarily* moves the saved player and an existing live rival into a clear, out-of-map physical calibration area **inside Play Mode**, with a disposable grounded-floor collider for prone/crouch checks. This does not edit or save the map, build, or any shipping component. It deliberately tests real aiming/damage authority without random trees or houses intersecting the fixture.

## Verified checks

- **100/100 actual terrain spawns** in first pass, real local/LOD visuals, genuine player incoming damage.
- **200 HP HUD:** full text `HP 200 / 200` and full 220-unit health bar.
- **Four authored tiers** using real gun model and inventory exchange:
  - Shotgun, **0 / NO SCOPE ADS**;
  - SMG, **1 / NORMAL ADS**;
  - Assault Rifle, **2 / 2X ADS** and visible 2x optic;
  - Sniper, **4 / 4X ADS** and visible 4x optic.
- For every tier, the production HUD marks a live rival red, `AarisEquippedGun.TryFire` consumes exactly one magazine round, the *actual rival's health* decreases via PhysX hit routing, and the outgoing damage-number pool advances. These are real shots, not direct synthetic damage calls.
- **Stance-aware 2x scope:** Standing, Crouching and Prone each transitions through real `AarisBattleActions.TrySetStance`, uses the stance-specific camera height, obtains Red-Lock and produces a physical HP-damaging shot. Standing stance is restored after prone.
- **Solid-wall negative test:** a temporary BoxCollider between the player and rival turns the reticle white and prevents target HP damage, even though a shot still consumes ammunition.
- **Incoming HUD test:** 25 HP damage updates text to `HP 175 / 200`, shrinks the health bar to 87.5% of full width, and activates the red hit flash.

## Run observations

| Check | Initial four-tier run | Extended four-tier + 3-stance run |
| --- | --- | --- |
| Saved-scene real spawns | 100/100 | 100/100 |
| Real player-damage events from bots | 13 | 10 |
| Live close-range humanoids | 6 | 7 |
| Distant LODs | 10 | 28 |
| Real scoped-weapon shots | 4/4 PASS | 4/4 PASS |
| Wall-occluded shot suppressed | PASS | PASS |
| 200→175 HP HUD state | PASS | PASS |
| Standing, crouch, prone 2x shots | Not included | 3/3 PASS |
| Unity test result | PASS | PASS |

The extended run measured 200 baseline scene frames and 200 loaded frames. It observed mean `GC Allocated In Frame` of **183,302 B/frame** before bots and **175,523 B/frame** during measured combat, including the Editor's allocations and other scene work. These counts are **not** causal attribution to bots or weapons; note that some unrelated scene streaming can be heavy. Frame timing clustered near **16.7 ms**, consistent with headless Editor frame pacing, and cannot establish mobile FPS.

## Reproduce

Run in a dedicated licensed Unity Codespace (no `-quit`; the test exits itself):

```bash
export TMPDIR=/tmp
xvfb-run -a -s '-screen 0 1280x720x24' \
  /home/vscode/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity \
  -batchmode -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run \
  -logFile /tmp/aaris-scope-hp-scene.log

cat /tmp/aaris-full-scene-combat-soak/result.txt
```

## Limits

This is a **Unity Editor/PhysX test**, not an on-device FPS, GPU, 1%-low or thermal benchmark. It verifies a deterministic open-air shot scenario *after* spawning on the real map, rather than asserting that every outdoor firing angle near random terrain is unobstructed. It does not prove a polished screenshot of a reticle, Android UI safe area, or zero first-launch stutter.

The source addition is strictly under `Assets/AarisWorld/Editor/`. Shipping player/bot/gun scripts, authored scenes, prefabs, assets and quality settings are unchanged. All Play Mode fixtures are temporary.
