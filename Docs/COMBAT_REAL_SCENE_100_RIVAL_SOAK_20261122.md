# Real terrain-scene 100-rival combat soak — 2026-11-22

## What this certifies

The **actual saved `TerrainDemoScene.unity`** and production runtime `AarisOfflineBRDirector.TrySpawnSlot` were exercised in Unity 6000.3.26f1 Editor Play Mode under Linux/Xvfb. This is **not** the earlier synthetic flat-ground scheduler benchmark.

The Editor-only `AarisFullSceneCombatSoak.Run` method loads the native scene without saving or changing it, lets player/controller and asynchronously prewarmed rival resources initialize, lands the actual player, and captures a 200-frame zero-bot scene baseline. It then enables the production rival director and allows real terrain, slope, ocean cutoff, and collider clearance checks to spawn 100 rivals. To avoid waiting through the match timeline, `Time.timeScale` is temporarily 8x for **spawn setup only**, with a proportionately adjusted fixed timestep. Once spawns finish, the test restores normal game speed, places six *existing real rivals* near the player, and captures close-range visual/AI/combat frame samples for at least 1.3 seconds of game time. This does not duplicate or fabricate rivals.

The smoke gate requires: 100 successful real spawns, at least one active close-up rig, visible enemies, an actual incoming player HP-loss event, and no logged runtime exception/error. The test measures Editor Main Thread and GC counters using `ProfilerRecorder`, and exits with failure on a missing gate. It is batchmode-only, uses a temporary Play Mode scene, and does not touch the game's shipping scripts, prefabs, models, renderer settings, or visual quality.

## Verified native Unity results

| Metric | Run 1 | Run 2 |
|---|---:|---:|
| Spawned on saved 2 km terrain | **100 / 100** | **100 / 100** |
| Successful spawn pass | 1 | 1 |
| Alive after nearby combat | 99 | 99 |
| Visible opponents | 23 | 26 |
| High-quality close-range rigs | 9 | 3 |
| Distant shared LOD visuals | 14 | 23 |
| Genuine incoming player damage events | 18 | 9 |
| Baseline Main Thread mean | 16.787 ms | 16.745 ms |
| Combat Main Thread mean | 16.665 ms | 16.665 ms |
| Baseline GC Allocated In Frame mean | 155,374.4 B/frame | 171,443.2 B/frame |
| Combat GC Allocated In Frame mean | 160,886.6 B/frame | 176,447.9 B/frame |
| Combat measurement game time | 5.949 s | 4.716 s |
| Unity Editor exit | 0 | 0 |
| Script/runner result | PASS | PASS |

**Important measurement limit:** Both baseline and loaded Editor samples clustered near 16.7 ms; this is a 60 Hz headless Editor frame-pacing/measurement ceiling, **not evidence that 100 bots have zero CPU cost or that the phone achieves 60 FPS**. Likewise, Unity Editor allocation counts include the Editor, IMGUI, development diagnostics, scene streaming, and unrelated runtime components; they are not a game-only per-system allocation attribution.

The rival-CPU-only microbenchmark in `Docs/COMBAT_100_BOT_FRAME_BENCHMARK_20261122.md` remains the correct isolated scheduler comparison. The real-scene results instead establish that production terrain spawning, Humanoid models, distant shared mesh LODs, player health/damage callbacks, and combat can run together in the saved scene.

## How to reproduce

Run in a dedicated **licensed** Unity Codespace, without `-quit` (the verifier exits after returning from Play Mode):

```bash
export TMPDIR=/tmp
xvfb-run -a -s '-screen 0 1280x720x24' \
  /home/vscode/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity \
  -batchmode -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run \
  -logFile /tmp/aaris-full-scene-soak.log

cat /tmp/aaris-full-scene-combat-soak/result.txt
```

## What remains unverified

- A real Android device connected through ADB/Profiler, including 2 GB RAM, renderer/GPU temperatures, draw calls, GPU frame timing, asset-streaming peaks, 1% lows, and restart behavior.
- Production 100-bot **render** performance at full visibility; at most the local visual subset around the player is instantiated at full quality by design, and all 100 are not simultaneously on-screen.
- Device memory pressure and first-boot shader/import stutters. Editor scene load and code recompilation times are not directly transferable to Android.
- A causal diagnosis of the observed Editor GC allocations. Do **not** treat these numbers as proof of a specific game script leak.

The test is intentionally Editor-only and does not alter the APK.
