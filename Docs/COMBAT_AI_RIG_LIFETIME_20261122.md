# 100-bot AI humanoid rig lifetime (2026-11-22)

Unity 6000.3.26f1 URP; offline battle royale.

## Root cause
A rival's first BuildVisual() permanently set visualBuilt=true. Leaving the
155m full-model band only did visualRoot.SetActive(false), retaining dormant
skinned meshes, Animator and PlayableGraph instances. Map traversal could
accumulate hidden humanoids without bound until the match ended.

## Implementation
- Existing 0.55s staggered visual tick; close full model and 485m distant LOD
  remain unchanged, as do weapons, player HP and AI combat contracts.
- Release only after 5 seconds continuously >190m, with no visible full rig.
  Inside the 485m band, require an active distant LOD first.
- Destroy the old animation PlayableGraph, reset its local animation and build
  state, and Destroy only the instantiated local visual hierarchy.
- Maximum one teardown per rendered frame. Android uses existing
  ReserveNativeTeardownFrame. Return inside 105m recreates the exact original
  rig using the same authored prefab and existing native build gating.
- This is hysteresis plus dwell time, not per-frame instantiate/destroy.

## Verification (Linux Unity Editor, not Android)
The native 29-test PhysX combat suite PASSED. The saved-scene 100-bot combat
soak spawned 100/100 opponents, passed every scope tier, two-way HP and stance
checks, and its additional travel test reported 7 retained rig instances
before the player departed versus 1 afterward. Returning to the selected
rival restored the same renderer count as before: 1 in this test fixture.
RESULT=PASS. No claims of Snapdragon 680 FPS or zero-GC are justified by
headless Editor timings; device memory/FPS capture is still required.

## Reproduction
Unity batchmode executeMethod:
Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run
Log file: /tmp/aaris-full-scene-combat-soak/result.txt

No native scene, prefab, material or previously dirty agent file was modified.
