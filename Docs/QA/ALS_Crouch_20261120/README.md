# Native Unity 6 stance validation — 20 November 2026

Branch source: `c299a96383178ddec966081ee234df2fd29e52f3` (PR #784).

Actual Editor Play Mode under Unity 6000.3.26f1, on isolated test worktree with the two imported CC0 Quaternius ALS crouch clips.

- `ALS_CC0_NATIVE_CLIPS_VERIFIED`: idle 2.933 seconds; forward 2.000 seconds; both non-legacy Humanoid clips
- `SUCCESS_16_ACTOR_MULTIANGLE_PORTRAITS`: 16 actual Play Mode JPEGs
- Crouch knee-height difference: 0.034–0.035 m; lateral knee deviations around 0.001–0.003 m
- Settled prone: ankle-to-terrain 0.105 m, knee-to-terrain 0.150 m, matching L/R
- Native C# compiler errors: zero

See `ALS_Unity_Real_Stances_3x3.jpg` for 9 of the 16 frames. **Important:** character and terrain render magenta in this isolated test scene because of imported shader/material state; the photos verify silhouettes and pose geometry, not production material appearance. The full locomotion and stance-transition sweeps are separate acceptance gates. Native resource probe itself printed success but its Editor process was stopped by a timeout rather than exiting normally. Do not treat that probe's exit status as a complete success.

No claim about real Android FPS/GC was made.
