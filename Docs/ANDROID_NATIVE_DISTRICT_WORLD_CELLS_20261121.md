# Native Unity 6 district world cells — 21 Nov 2026

## Scope and authoritative observations

Unity 6000.3.26f1 / URP Android offline single-player, canonical 2 km world.
Read-only `WorldScene` source scan and live **Unity Editor Probe exit 0** found
12 remaining districts with **93 authored LODGroup + MeshCollider building roots**.
An existing separately authored ForestLookout additive scene is retained, for a
**13-cell detail streamer** after migration. This is not a claim that the
four TerrainData tiles, vegetation, road network, vehicle systems, plane,
interactive homes, or all other authored content have been partitioned.

District cells: CanalMarket, ClockTowerTown, CoastalHamlet, EasternPort,
FreightDistrict, HighlandObservatory, LakesideHamlet, NorthernRidgeTown,
OldVillage, SoutheastLighthouse, SouthernAirfield, ValleySettlement.

## Native serialization, not just GameObject.SetActive

The Editor batch tool must run in an **isolated project snapshot**, with
`AARIS_WORLD_PARTITION_APPLY=1`. Probe is read-only by default. Each migrated
building must satisfy exact native hierarchy contracts: original MeshCollider,
non-convex static collision mesh, LODGroup with authored far mesh, no unknown
MonoBehaviours or additional collision, one persistent external reference in
`AarisNearbyColliders.obstacles`.

For each building the tool saves the original renderer/LOD hierarchy **unchanged**
into a separately build-included additive `.unity` scene. It retains:
(1) an exact-world-pose MeshCollider component using the **same mesh**,
PhysicMaterial, contact offset, layer/filter overrides and enable state, with the
original `AarisNearbyColliders` slot rebound to the new persistent object;
(2) a persistent original last-LOD mesh/material silhouette at the exact
original world pose, visible while the full-detail additive scene is unloaded.
An existing event-driven `AarisChunkFallbackVisibility` toggles the silhouette
only when the corresponding actual scene is loaded/unloaded; no per-frame
object search or runtime mesh generation.

Always persistent: 4 TerrainData surfaces, roads/bridges, river and
CanalWater_VisualOnly, EarthPort Port_Sea_Pier with exact physical collider,
SouthernAirfield runway and centre-line meshes, loot/guns, vehicles, player,
plane, gameplay controllers, and all other unrelated native scene roots.

Each cell uses its **actual renderer bounds** to populate the existing
`AarisAdditiveChunkStreamer` manifest and adds its exact scene path to
`EditorBuildSettings`. The native streaming engine already has:
- predicted camera/player lookahead, true-position urgent admission,
- overlapping add/remove hysteresis (90 m entry / 170 m release margins),
- one shared native asset/scene load scheduling gate, physics/import fences,
- Android OS thermal-aware restriction of speculative-only IO,
- guarded actual-position scene priority even in severe thermal pressure.

The scene manifest is now also included in Android APK builds via #904,
which rejects a build missing registered scene assets.

## Native verification / release gates

Before publishing scenes, demand a real Editor `ApplyAndVerify` exit code 0,
actual `AARIS_DISTRICT_APPLY_VERIFY_COMPLETE` log, and explicit checks
that all 12 scenes exist with real `.meta` GUIDs. Reopen every scene, confirm
all 93 moved LODGroups with original far mesh/material identity and
world-space pose, exact physics mesh/material/one authoritative index entry,
and only the intended cross-scene references. Run
`Tools/Validation/test_native_district_world_partition.py` and existing
world/additive-streaming/Android build-source suites.

Only after publishing **real native `.unity` files** and passing PR CI merge
to `main`; never merge an Editor-only authoring script as if the map were
already partitioned. The existing dirty Codespace working branch must remain
untouched: no new git worktree, reset, stash, checkout, force push or broad
cleanup. Generated Unity Editor files belong to an isolated `/tmp` copy.

To validate device impact on Snapdragon 680 / Adreno 610 with 4–6GB RAM,
A/B benchmark the **same camera path** on cool and heat-soaked phones and record
p50/p95/p99 frame time, frame spikes >50ms, PSS resident memory, thermal
status/headroom and loading failures. Visually compare day/dusk, camera
height/overhead and building silhouettes at loaded/unloaded borders. Check
vehicle parking/exit, indoors/loot, Bot line of sight, collision, audio,
shader/material rendering, lightmaps and scene-entry pop. Release only after
no missing objects and no meaningful visual quality regression.

## Honest limits

Native scene splitting lowers baseline resident GameObjects and eventually
allows full-detail asset release, but persistent exact colliders and authored
last LOD references keep some shared mesh/material textures resident. No phone
FPS figure or guaranteed RAM saving can be inferred from a source-only CI pass.
The global four terrain tiles are still always resident; a later terrain-cell
conversion needs separate height/seam/collider/vegetation/physics safety QA
and should not be silently included in this migration.
