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

The full authored `TerrainDemoScene.unity` verification uses real terrain, 100 spawned rivals, Humanoid visuals, distant LODs, actual player damage, all four scope tiers, 3 stance-aware shots, 200 HP feedback and solid-wall shot blocking.

**Verified full-scene result after the production AI change:** Unity Editor 6000.3.26f1 exited `0` with `RESULT=PASS`; **100/100 real terrain rivals** spawned on first pass, all 100 alive at the measurement boundary, **7 nearby Humanoid rigs**, **20 distant LOD visuals** and **13 genuine player HP-damage events**. Shotgun/SMG/AssaultRifle/Sniper optical tiers **4/4 PASS**; standing/crouching/prone 2x shots **3/3 PASS**; physical wall stopped Red-Lock and HP damage; HP 200→175 HUD and hit flash PASS. The 200-frame headless Editor timings were paced near 16.7 ms and do not represent on-device FPS.

**Verified final source QA:** On the final branch, `AarisBotCombatIntegrationQA.Run` exited Unity `0` with `RESULT=PASS`, 0 compiler errors, and **`PASS Live bot think chooses and sees wall-clear rival`** in addition to the direct alternative-target test and existing 100-bot/player-damage assertions.

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
