# Melee ownership, cover and target selection — 18 November 2026

The old punch selected its closest damageable collider before checking cover,
then inspected only the first ray hit. Native tests reproduced three defects:

1. An owner's child collider could cancel an otherwise valid punch.
2. A target's sibling collider could cause the first-hit exemption to miss a
   solid wall farther along the same path and damage a protected target.
3. A hidden closest target could suppress a slightly farther exposed target
   inside the existing punch volume, range and forward hemisphere.

`AarisBattleActions` now checks exposure while choosing the closest candidate.
A reusable 32-hit ray buffer ignores the owner's colliders and all colliders
with the intended receiver, but checks every remaining hit for solid cover.
Buffer saturation falls back to a complete query. The normal path allocates
no query array; rays run only at the existing delayed punch contact, not in a
new per-frame update. Punch timing, reach, damage, overlap volume and one-hit
damage delivery remain unchanged.

## Native evidence

Real Unity **6000.3.26f1** and PhysX, all 34 actual game runtime scripts,
without runtime test doubles:

| Scenario group across Standing, Crouching and Prone fields | Baseline `b52f81ed` | Candidate |
| --- | ---: | ---: |
| All 10 scenarios per stance | 18/30 pass, exit 1 | 30/30 pass, exit 0 |
| Own blocker, sibling before wall, hidden nearest target, saturated own blockers | 12 failures | 0 failures |

Ordinary open hits, own collider followed by real cover, sibling-only paths,
triggers, Ignore Raycast layers and saturated paths containing a real wall
also passed. Hidden targets retained 200 HP; an eligible exposed target took
exactly one 20-HP hit. C# compiler errors: **0** before and after.

The candidate runtime source SHA-256 was checked before execution:
`1e3f8d4f9a09cb6bd2911a9ee7f661668bc3357da608b8dbfcff05df73e76d71`.

The audit ran in the existing disposable source-only Unity project under
`/tmp`, with the game packages except optional VFX Graph thumbnail imports.
It selected the real stance field and invoked the existing contact method
directly. It does not certify animation, delayed input timing, the native
map, a new APK, rendering quality or Android frame rate. The live project's
source assets and packages were not changed for native testing.

Repeat through **Aaris > CI > Verify punch ownership and complete cover checks**
or batch `-executeMethod Aaris.MapReview.Editor.AarisPunchCoverQA.Run` without
`-quit`. The Editor audit restores the original active scene, uses disposable
objects, saves no scene/assets, and exits only in batch mode. Private report:
the temporary `aaris-punch-cover-qa/report.txt` file.

Eleven existing regression scripts and C# syntax checks across all 74 Aaris
scripts also passed locally. GitHub CI is recorded on the associated PR.
