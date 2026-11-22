# Smarter rival-versus-rival target selection — 2026-11-22

## Production problem

The offline 100-bot authority originally used a nearest-distance-only rival hunt. When the closest rival was behind a wall, the hunter continued aiming/chasing that rival even when another nearby opponent had a clear physical line of sight. The final shot correctly respected collision, but the bot could appear unaware of a better target.

## Production improvement

Two production changes in `Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs`:

1. The director provides `TryFindVisibleRivalAlternative`, which searches a **42 m combat range** for the three closest alternatives **without allocating an array, list, LINQ query or object**. It performs at most **three** physical candidate visibility checks (each reusing the bot's existing NonAlloc ray buffers) and returns the nearest visible alternative. The original nearest-only `FindRivalOpponent` remains intact for general hunt/pathfinding.
2. During the existing staggered AI think tick, if a rival's nearest bot target is **within combat range but actually occluded** and the player is not the target, the bot tries that bounded fallback. The selected target is revalidated using its real CharacterController capsule before setting `canSee`. If none of the three options has clear sight, the bot continues pursuing the nearest target, retaining the original cover-navigation behavior.

**Performance guardrails:** the fallback runs only for an occluded **bot-versus-bot** target already within 42 m, not for distant bots, open-sight engagements, or the player's two-reserved-attacker selection. Max three LOS candidate checks per triggered attempt; the 100-bot roster count, rendering, map, scope aim and hit damage are untouched. Any extra Physics workload has not been quantified on the target Android device.

## Isolated Unity regression

`AarisBotCombatIntegrationQA.Run` uses three actual components from its existing 100-rival synthetic roster as a physical scenario:

- The hunter and two other rivals are placed in a clear test area.
- The nearest rival is behind a BoxCollider wall, while the slightly farther rival is unobstructed.
- The original nearest-distance finder first picks the covered rival.
- The new method picks the visible alternate and resolves its correct `AarisOfflineHealth`.
- Removing the wall makes the nearest rival visible again.
- The extended test also directly invokes the actual hunter `Update` to verify it adopts the visible target and reports `canSee` after physical target/capsule revalidation.

The existing attacker-two-slot, player↔bot damage, scoped bullet, red-lock, cover and headshot checks remain in the same regression suite.

## Performance baseline after change

The existing native Unity 6000.3.26f1 Editor-only `AarisBotFrameBenchmark.Run` completed with **100/100 logical bots moved** and exit `0`, after a 1.300-second game-time load sample.

| Metric | 0 rivals | 100 logical rivals |
| --- | ---: | ---: |
| Main Thread mean | 0.204 ms | 0.348 ms |
| Main Thread p95 | 0.247 ms | 0.401 ms |
| GC Allocated In Frame mean | 704.0 B/frame | 705.4 B/frame |

The observed 100-bot minus zero-bot delta was 0.144 ms in this run. Previous editor measurements fluctuated; these results do not isolate the additional fallback work, which is infrequent in a synthetic flat benchmark. Do not infer an Android FPS number, zero device lag or a speedup from these data.

## Full-scene and device limits

The full authored `TerrainDemoScene.unity` verification uses real terrain, 100 spawned rivals, Humanoid visuals, distant LODs, actual player damage, all four scope tiers, 3 stance-aware shots, 200 HP feedback and solid-wall shot blocking. Its result is recorded separately after execution.

The benchmark/QA additions are Editor-only. No graphics-quality reduction, mesh replacement, terrain rewrite, new git worktree, or changes to other agents' pending assets are part of this upgrade. Android 2 GB devices still need on-device profiling and heat/first-start hitch checks.

## Reproduce

```bash
export TMPDIR=/tmp
UNITY=/home/vscode/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity
xvfb-run -a -s '-screen 0 1280x720x24' "$UNITY" -batchmode \
  -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisBotCombatIntegrationQA.Run \
  -logFile /tmp/aaris-rival-fallback-regression.log
cat /tmp/aaris-bot-combat-qa/report.txt

xvfb-run -a -s '-screen 0 1280x720x24' "$UNITY" -batchmode \
  -projectPath /workspaces/Unity-Game-Aris \
  -executeMethod Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run \
  -logFile /tmp/aaris-rival-fallback-full-scene.log
cat /tmp/aaris-full-scene-combat-soak/result.txt
```

Both test entry points exit the Unity Editor themselves; omit `-quit`.
