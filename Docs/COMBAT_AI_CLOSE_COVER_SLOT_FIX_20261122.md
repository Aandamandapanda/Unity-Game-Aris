# Close-cover AI attacker-selection fix — 2026-11-22

## Production defect

Before this change, a rival’s long vision ray in `AarisOfflineRival.TryGetSightHit` started **0.48 m in front of the body**, at eye height. A thin wall inside that first 0.48 m could be invisible to the long ray. The final firing path already checked a short body-to-muzzle ray, but **`HasSightTo` did not**.

Consequently, a rival behind immediate cover could reserve one of the game's **two concurrent player-attack slots**, then have every firing attempt rejected. This starved other visible rivals and caused AI to appear idle or inconsistent behind walls. The bug also affected whether a bot considered another rival visible for thinking and pursuit.

## Fix

In production `Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs`, `HasSightTo(Transform victim)` now rejects obstructed body-to-muzzle paths through the existing `MuzzlePathClear(victim)` before using the long target ray. The checker uses the bot's existing `RaycastHit[]` scratch and `Physics.RaycastNonAlloc`; **no managed allocation, new model, object, persistent data structure or rendering change** was introduced.

The existing short-path check at the actual shot is retained, so new cover appearing between target selection and firing still blocks the bullet. The two-attacker cap, 100-bot roster, distance checks, line-of-sight semantics and weapon damage remain the same.

This introduces a small additional **short-ray physics query for bots considered for line of sight**; the precise Android CPU cost remains unmeasured. The change is not claimed to fix overall Android device FPS.

## Verified 100-bot PhysX regression

Run on Unity Editor **6000.3.26f1** using `Aaris.MapReview.Editor.AarisBotCombatIntegrationQA.Run`:

- `PASS`: exactly 100 logical bot slots.
- `PASS`: normal nearby attackers limited to two, distant/invisible bots excluded.
- `PASS`: close 48-cm muzzle wall blocks a bot's actual shot.
- **New `PASS`: that wall also blocks tactical LOS and prevents claiming either limited player attacker slot.**
- **New `PASS`: the staged scheduler chooses the other two visible nearby bots instead of the covered rival.**
- **New `PASS`: removing the wall restores the rival's eligibility.**
- `PASS`: authoritative two-way player/bot damage, 1.75x headshot, 2x Red-Lock shot, ammo consumption, and blocked red-lock behind solid cover.
- Native Unity exit code `0`; **0 C# compilation errors**.

## Verified real saved terrain scene

Run on `Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity` using `Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run`, with the production change applied:

- **100/100 real terrain rivals spawned in one pass**; 99 remained alive at the combat measurement boundary.
- 7 close-range humanoid rigs, 19 distant LOD visuals and **12 genuine player-damage events** were recorded.
- All four authored scopes (No Scope Shotgun, Normal SMG, 2x Assault Rifle, 4x Sniper) equipped, red-locked, fired, spent ammo and damaged a real rival.
- Standing, crouching and prone 2x scoped real shots passed.
- Cover blocked Red-Lock and bullet damage; HP 200→175 text/bar and incoming hit flash passed.
- Native result `RESULT=PASS` on a loaded-sample interval of approximately **4.694 seconds**. Unity executable exit code is verified separately.

The Editor frame duration was paced near 16.7 ms, and measured GC included unrelated scene systems. Neither number establishes Android FPS, phone thermal behavior, first-launch stutter or game-only GC. No terrain scene, prefabs, animations, FBX files or URP quality settings were changed.

## 100-bot logical CPU regression after the fix

The same validated Editor-only `AarisBotFrameBenchmark.Run` was repeated with the production fix applied. Result: **PASS**, Unity exit `0`, 100/100 actors moved, 1.300 game seconds, 0 compilation errors. The measurements were:

| Metric | No rivals | 100 logical rivals |
| --- | ---: | ---: |
| Mean Main Thread | 0.222 ms | 0.347 ms |
| Main Thread p95 | 0.257 ms | 0.397 ms |
| Mean GC Allocated In Frame | 704.0 B/frame | 705.4 B/frame |

This particular test's 100-rival minus zero-rival mean is **0.125 ms/frame**. Earlier independently measured editor baselines on the same project ranged around 0.131–0.133 ms/frame extra logic time, but machine load and the randomized simulation differ, so this is **not a proven speedup**. It shows no obvious gross regression in the isolated workload. The benchmark models 3 nearby and 97 distant logical bot instances, not 100 visible high-quality animated characters, so its graphics and Android performance claims remain strictly limited.

## Reproduce

```bash
export TMPDIR=/tmp
UNITY=/home/vscode/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity

xvfb-run -a -s '-screen 0 1280x720x24' "$UNITY" \
  -batchmode -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisBotCombatIntegrationQA.Run \
  -logFile /tmp/aaris-ai-wall-selection.log
cat /tmp/aaris-bot-combat-qa/report.txt

xvfb-run -a -s '-screen 0 1280x720x24' "$UNITY" \
  -batchmode -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run \
  -logFile /tmp/aaris-ai-wall-real-terrain.log
cat /tmp/aaris-full-scene-combat-soak/result.txt
```

Do not use `-quit` with the full-scene test; it exits Play Mode and batchmode itself. These QA tests are in Editor-only source and do not add release APK overhead. A connected Android development profiler is still necessary to make end-to-end mobile FPS claims.
