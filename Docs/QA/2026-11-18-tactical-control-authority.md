# Tactical pause and control authority — 18 November 2026

Native tests reproduced tactical callbacks accepting commands after gameplay
control had stopped. The old code could start/finish a medkit at timeScale 0,
consume grenades with a disabled/inactive motor, start wall aiming while
paused, and retain a healing channel or preview after focus/pause callbacks.

`AarisOfflineTacticalHUD` now shares a field-only control guard between input,
grenade release and medkit start/completion. It requires an active component,
positive timeScale, an active motor and a living health receiver when present.
An interrupted channel stops before spending its medkit. Focus loss,
application pause and disable cancel healing and held wall preview.
`AarisOfflineGlooWall.CanUse` also rejects a paused or inactive motor before
placement queries. No new per-frame component lookup, query, renderer or
visual asset is added; the existing optional-component refresh stays bounded.

## Native comparison

Unity **6000.3.26f1**, actual game classes and all **34 runtime source files**,
without runtime test doubles, in the existing source-only temporary project:

| Result | Baseline `5111e8c` | Candidate |
| --- | ---: | ---: |
| Control-state scenarios | 12/30 pass | 30/30 pass |
| Failed cases | 18 | 0 |
| Unity process exit | 1 | 0 |
| C# compiler errors | 0 | 0 |

Coverage includes paused, disabled, inactive, disabled-motor and dead-owner
callbacks; medkit completion and item conservation; focus/pause cancellation;
benign resume notifications; cooldown/empty grenade rejection; valid healing
and wall eligibility. The valid heal restores 75 HP and spends one medkit.
The deadline is explicitly advanced in the fixture rather than waiting three
real seconds. Lifecycle callbacks are invoked directly. This is Edit Mode
state/engine evidence, not Android lifecycle, full-map Play Mode, grenade
flight, wall-placement physics, rendering or device FPS certification.

Changed runtime SHA-256 values were checked before execution:

- Tactical HUD: `5b35f10b6346148d6275a21a4534e6957f0ba5d293bdbd8039d7863d3b9ce3e1`
- Gloo Wall: `254f6b37907ad402fdba5f2f72429519bbb3b4ee7870b844fc3e845d20bd3925`

The isolated project excludes optional VFX Graph thumbnail imports. No live
source asset or game package changed for the native test. The probe used low
CPU priority and resource guards that could terminate only its own process
group, retaining headroom for the separate pinned Android build.

Repeat through **Aaris > CI > Verify tactical pause and control authority**,
or batch `-executeMethod Aaris.MapReview.Editor.AarisTacticalInputQA.Run`
without `-quit`. The audit restores timeScale and the active scene, saves no
assets, uses temporary actors, and exits only in batch mode. Failed baseline
throws are cleaned up only at the isolated QA position. The private temporary
report is `aaris-tactical-input-qa/report.txt`.

Eleven existing regression scripts, all 75 Aaris C# syntax checks and workflow
YAML parsing passed locally. Tactical authority contracts are now included
in the existing mobile gameplay CI workflow.
