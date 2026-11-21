# Android ADPF thermal-aware world streaming — 2026-11-21

Target: Unity 6000.3.26f1, URP, Snapdragon 680 / Adreno 610 (4–6 GB), offline world.

## Architectural change

The existing project already has a per-render-frame native admission gate, predictive vehicle import and authored additive scene streaming. CPU/physics work can still compete with GPU work while the phone is **thermally throttled**. RAM alone does not identify this condition. This patch adds one **Android OS thermal signal** to the existing multi-streamer work scheduler, not another unrelated quality tier.

- `AarisAndroidThermalGovernor` is created by the production `AarisMobileRuntime` only after the first rendered scene frame. On Android API 29+, it holds one OS `PowerManager` reference and samples `getCurrentThermalStatus()` every 5 seconds. No frame-by-frame JNI calls, allocations or native Java listener. The object and its JNI wrapper are released on scene teardown.
- Status `-1`: unsupported/unobserved, fall back to the existing unchanged scheduler.
- Status `0–1`: normal full predictive prefetch.
- Status `2` (moderate): still prefetch, but keep at most **one** merely speculative additive scene instead of two; every scene needed at the player's actual position ignores this cap.
- Status `3–6` (severe/critical/emergency/shutdown): stop initiating **speculative** future additive scenes and predicted-distance-only vehicle imports. Actual player-near scene activation, vehicle models, occupied/driven vehicles, weapon/loot, physics, terrain, gameplay/audio and all authored visuals retain their original paths.
- Require three consecutive cool samples (15 seconds) before re-enabling future prefetch to prevent a heat-border oscillation. A later moderate sample pauses the cooldown count without forgetting the hot status.
- Existing global `AarisMobileStreamingFrameGate` still owns one native model hierarchy/material slot and gives essential resource imports and physics priority; this change does not bypass its admission or introduce a second competing queue.
- Opt-in phone telemetry `AarisDiagnostics/current.jsonl` now exposes `androidThermalStatus`, `moderateThermalPressure`, and `speculativePrefetchSuspended`. These are OS-reported status, **not** measured temperature or cause of a specific hitch.

The source 2 km landscape and native four terrain tiles are not yet independently streamable: the AdditiveChunkStreamer policy only has a RAM effect after safe authored `.unity` chunk scenes are created, included in the Android build and validated. This work does **not** pretend to free persistent TerrainData RAM, produce HLOD proxies, or fix per-frame monolithic vehicle `Instantiate`.

## Nonnegotiable visual / gameplay checks

1. Never change render resolution, texture mips, shadows, terrain tiles, tree counts or visibility distances as a thermal response in this feature.
2. Near-scene check (`loadIsUrgent`) always wins regardless of thermal status. Nearby vehicle catalog import and real distance spawn are not gated by the speculative prefetch rule.
3. Keep native distant silhouettes when a chunk is unloaded; no untested hiding of houses, trees, rivers, bridges or important landmarks.
4. JNI failures/unsupported OEMs fail *open*: no speculative prefetch restriction. Never spam retry logs.
5. Offline native Android battery/thermal/FPS must be tested before claiming a sustained FPS improvement.

## Verification matrix

Run source + C# parser CI, plus additive scene, vehicle priority, shared native admission, Android low-memory and mobile hotpath regressions. For actual handset acceptance compare same camera/route on cool and hot device for 10+ minutes; record `p95FrameMs`, `p99FrameMs`, `framesOver50Ms`, `androidThermalStatus`, thermal behavior, PSS, dropped-frame count, native frame markers, visible tree/settlement silhouettes and first-use vehicle delay.

This is *one substantive thermal-aware scheduling subsystem*, not a proof that the entire map has been partitioned or that Adreno 610 can sustain 60 FPS.

## Public API references

- Android PowerManager thermal status: https://developer.android.com/reference/android/os/PowerManager
- ADPF Thermal API guidance: https://developer.android.com/games/optimize/adpf/thermal
- Unity asynchronous texture/mesh upload budgeting: https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/loading-texture-mesh-data-asynchronously/configure-asynchronous-upload-pipeline
- Unity Addressables memory management: https://docs.unity.cn/Packages/com.unity.addressables@3.0/manual/memory-assets.html
