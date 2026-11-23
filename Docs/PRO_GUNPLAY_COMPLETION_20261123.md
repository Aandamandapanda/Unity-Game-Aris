# Gunplay and death-crate completion — 23 November 2026

Canonical base: 17a304097c5725349ac24de88ff1303bfb5955c1.

The current main already contained firing feedback, pooled independent casings,
per-weapon headshots and transactional death-crate pickup. This pass completes
the missing pistol profile and fixes presentation/ground-query gaps.

## Behavior

- Every accepted player shot keeps hitscan as the sole damage authority. The
  thin cosmetic tracer starts at the animated rendered barrel and ends at the
  physical hit. A short shot has nonzero streak geometry on its first frame.
- Bot flames share the player's additive transparent material. At most two
  nearby bot lights are active, with no shadow maps; distant combat stays logical.
- Player and bot casings exclude the shooter's body and attachments from their
  one-time ground query. A retained buffer expands for dense collision results.
  Existing ballistic gravity, spin, bounce and independently pooled shells stay.
- Original compact pistol: native mesh, two shared URP materials and prefab;
  0.235 m presentation, 0.86 kg handling, semi-auto 360 RPM, 15/60 rounds,
  27 body damage, 1.8x headshots, 0.85 degree recoil, 1.65 s reload. The two
  hands support the short grip rather than an imaginary rifle fore-end.
  Its sound reuses the bundled AR clip with adjusted pitch/volume.
- Existing AR, battle rifle, SMG, sniper, shotgun and DMR profiles remain.
  Shotgun damage is per pellet. No commercial game's secret balance is claimed.
- Death crates retain actual magazines, reserve rounds and remaining medkits.
  Nearby PICK ignores own character colliders in every stance, uses wall
  line of sight and the existing safe two-slot gun exchange.
  Up to 100 loot records persist, with 16 nearest streamed box/beam visuals.

## Public technical references

These are implementation references, not evidence of proprietary PUBG,
Free Fire or Call of Duty internals.

- Unity ObjectPool: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/pool/objectpool1
- Unity FPS Creator Kit: https://connect-prd-cdn.unity.com/20200317/7a981784-430a-4a3c-8423-302fe220e6d7/Creator%20Kit%20FPS%20_%20Manual.pdf
- Unity RaycastNonAlloc: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/physics/raycastnonalloc
- Unity URP light limits: https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/light-limits-in-urp
- Epic Lyra inventory and equipment: https://dev.epicgames.com/documentation/en-us/unreal-engine/lyra-inventory-and-equipment-in-unreal-engine

## Validation

Native entry points: AarisCompactPistolBake.Bake, AarisProGunplayQA.Run,
AarisFullSceneCombatSoak.Run and AarisWeaponImportValidation.Run.
The new Play Mode QA exercises seven real models and HP profiles, dense own
colliders at standing/crouched/prone heights, first-frame traces, bounded bot
lights, exact crate ammo transfer and a real blocking wall. The full-scene
soak additionally equips/fires the pistol on the saved game's real actors.

Native reports and images remain private under /tmp during QA. Linux Editor
checks do not certify Android frame time, thermal behavior or device audio.
The casing floor is a sampled horizontal plane, not full rigid-body collision
against arbitrary moving geometry. Cosmetic tracer distance stays capped.

Verified on Unity 6000.3.26f1: 44 native Play Mode checks passed, including the real
health/death callback chain and duplicate-fatality protection. Seven native
models imported and scaled correctly; three URP pistol views were inspected.
The saved-map soak ran 100/100 live rivals for 8 seconds (400 measured frames),
with 19 real encounter damage events; all five scope/weapon shots passed.
Eight static suites passed on the final source. C# compiler errors: zero.
