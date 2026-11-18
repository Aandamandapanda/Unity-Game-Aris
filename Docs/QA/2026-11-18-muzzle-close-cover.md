# Close-cover muzzle authority — 18 November 2026

When the shoulder camera can see past a thin wall but the muzzle starts inside
or beyond it, PhysX does not return that wall from the forward muzzle ray.
The old trigger could therefore damage a target behind close cover.

`AarisEquippedGun.TryFire` now checks the existing 0.24 m receiver-to-muzzle
offset before the pellet loop. The first external solid collider on that
segment owns the hit, impact position and normal. Open shots retain the
camera-aim/muzzle-path queries. The normal path reuses the existing hit buffer;
the exhaustive fallback on saturation remains intact. This adds one short
query per accepted shot, shared by shotgun pellets, with no Update work,
new buffer, asset or renderer.

## Native evidence

Unity **6000.3.26f1**, actual PhysX and the real `TryFire`, `FirstExternalHit`,
`ApplyDamage`, health, weapon spec and random implementation:

| Cases | Baseline `f370c060` | Candidate |
| --- | ---: | ---: |
| 10 scenarios at each of 1.30, 0.85 and 0.30 m firing heights | 18/30 pass | 30/30 pass |
| Inside/beyond close cover, saturated owner-collider query, eight-pellet shot | 12 failures | 0 failures |

Open lanes, ordinary forward cover, off-axis cover, triggers, Ignore Raycast
layers and owner colliders retain their expected behavior. Each accepted
trigger consumes exactly one cartridge, including the eight-pellet case.

The candidate runtime source SHA-256 was verified before execution:
`d819f694f6325c26d6c675b5ae12246bebf89b92e911b67d0b3a743151276259`.

This comparison ran in a disposable minimal project under `/tmp`, with
audio and optional owner-component dependencies replaced by inert test
doubles. Heights were tested by moving the owner; this is not animated
stance verification, a full-map Play Mode test, an APK build, a screenshot
quality assessment or Android performance profiling. The live game project
and interactive Editor were not used for these isolated physics runs.

The committed Editor audit can also run against the full game assemblies via
**Aaris > CI > Verify muzzle cannot fire through close cover**, or batch
`-executeMethod Aaris.MapReview.Editor.AarisMuzzleCoverQA.Run` without `-quit`.
It uses temporary objects in an additive scene, preserves the original scene,
never saves assets and exits only in batch mode. A fresh empty batch scene is
reused because Unity refuses an additive scene beside an untitled scene.
Report: the private temporary `aaris-muzzle-cover-qa/report.txt` file.

## Static regression

Passed weapon/loot contracts, 10,000 two-slot weapon transactions,
battle hardening, mobile hot-path checks and all Aaris C# syntax checks.
