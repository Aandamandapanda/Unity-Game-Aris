# 100-rival scheduler CPU baseline — 2026-11-22

This is a **Unity Editor / Linux Xvfb logic-only measurement**, **not** an Android or full-game FPS result. Do not quote these timings as Snapdragon 680 performance.

## Test scope

- Unity 6000.3.26f1, Codespace Linux (4 logical CPUs), Xvfb software graphics.
- The authored game scene is **not** saved or changed. The benchmark creates an empty transient Play Mode scene and cleans up when Play Mode exits.
- **Control:** 200 sampled frames with 0 rivals, after 35 warmup frames.
- **Load:** 100 instances of the real `AarisOfflineRival` MonoBehaviour, CharacterController and health receiver, three within nearby-player selection range, remaining bots distributed on a flat physical floor; 55 warmup frames followed by at least 1.3 seconds of game time.
- The actual rival Update, physics movement, distance-based scheduling, AI think checks and opponent search are exercised. It does **not** load Humanoid rigs, combat meshes, terrain streaming, weapon VFX, shader workloads or real Android CPU/GPU drivers.
- Metrics use Unity `ProfilerRecorder` for Main Thread and GC Allocated In Frame; Editor and harness overhead are included.

## Verified results

| Metric | Run A: 0 bots | Run A: 100 bots | Run B: 0 bots | Run B: 100 bots |
| --- | ---: | ---: | ---: | ---: |
| Main-thread mean (ms) | 0.216 | 0.347 | 0.208 | 0.341 |
| Main-thread p95 (ms) | 0.256 | 0.388 | 0.248 | 0.395 |
| GC allocated per frame mean (bytes) | 704.0 | 705.4 | 704.0 | 705.4 |
| Bots moving | n/a | 100 / 100 | n/a | 100 / 100 |
| Sampled load game seconds | n/a | 1.300 | n/a | 1.300 |

**Additional mean main-thread cost:** 0.131 ms/frame in Run A, 0.133 ms/frame in Run B. This is an Editor microbenchmark for logical AI only, not a gameplay frame-time budget. Nonzero allocation values include the Editor and the harness; do not infer that game-wide VFX, physics, or GC are zero-allocation.

The initial short-sample attempt was intentionally rejected as an adequate far-bot simulation measurement: only 16/100 actors moved because game time had not advanced enough for the 0.35-second far-AI update interval. The final test requires 1.3 seconds of game time and records 100/100 moving.

## Reproduce in a dedicated CI Codespace

```bash
export TMPDIR=/tmp
xvfb-run -a -s '-screen 0 1280x720x24' \
  /home/vscode/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity \
  -batchmode -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisBotFrameBenchmark.Run \
  -logFile /tmp/aaris-bot-framebench.log
cat /tmp/aaris-bot-frame-benchmark/result.txt
```

The menu method explicitly blocks interactive Editor sessions because it creates a temporary single scene.

## Required before a mobile FPS claim

Profile a **Development Build on the target Android phone** with real terrain, high-quality models, 100 spawned bots and heavy concurrent firing. Capture CPU Main Thread, rendering, physics, GC Alloc, 1% low FPS, 95th/99th frame time, device temperature and first-launch vs warm-launch behavior. A connected phone/ADB or target-device profiler session is necessary; this Codespace's Android device link was offline on 2026-11-22.

The benchmark lives in `Assets/AarisWorld/Editor/AarisBotFrameBenchmark.cs` and has no shipping runtime component.
