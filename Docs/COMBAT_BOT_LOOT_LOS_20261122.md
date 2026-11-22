# 100-bot combat: floor-loot cover fix (2026-11-22)

## Root cause
Player pickup tested physical LOS, but AI rival pickup only checked a 2.15m distance. As a result, a bot could reserve and consume a gun through a house wall.

## Change
AarisWeaponLootWorld uses one shared HasClearPickupPath(looter, position) for player and bot. The bot's Transform is passed through AarisOfflineBRDirector into TryTakeForRival. RaycastNonAlloc reuses existing buffers; the ray ignores only the actual looter's own collider hierarchy. The wall check runs before pickup reservation, consumption or visual teardown, on the already staggered bot loot tick. No extra Update, enemy scan or prefab exists.

## Unity 6000.3.26f1 evidence
* Real PhysX QA: 29 PASS, process exit 0, C# compiler errors 0. New cases: open floor weapon, blocked wall, and wall removed.
* Full native terrain scene soak: 100/100 rival spawn; four scope tiers (0, 1, 2, 4), actual firing/hit popup, stance-specific Red-Lock (standing/crouching/prone), blocked-wall damage, HP 200/200 and HP 175/200 hit flash; RESULT=PASS, exit 0.
* The 100-bot scenario was executed in a Linux Unity Editor, not on an Android device. No Snapdragon 680 FPS guarantee or zero-GC claim: the existing full-scene profiler still reports managed allocations and requires device profiling.
* The bot pickup regression directly exercises the shared physics helper; a separate integration code check confirms TryTakeForRival gates reservation through that helper.

This branch does not rewrite native scenes/prefabs, spawn counts, meshes, URP assets, or unrelated dirty agent files.
