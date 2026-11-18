# Driving input suspension — 18 November 2026

A disabled vehicle motor zeroed its own axes but left its driver's held axes
and HUD arrow flags intact. Re-enabling the car between two Updates could
therefore restore throttle without a new press. Paused callbacks could also
write steering/throttle and leave the old HUD state queued across resume.

The motor now notifies its owning session immediately on disable. The session
cancels both its virtual axes and its bound HUD; the HUD verifies that the
request belongs to its current session. Pause, focus loss and session disable
use the same cancellation. Invalid setters neutralize the motor, while fresh
valid input retains finite-value checks and clamping. A paused DRIVE/EXIT
callback rejects ownership changes. Disabled occupied cars hide the driving
arrows but retain EXIT availability; the existing floor/route exit checks
remain unchanged. No automatic exit or driver release was introduced.

The binding is cached during HUD configuration. Normal control checks add no
Find/GetComponent call, physics query, asset, renderer or new Update method.
The latest imported-tyre symmetry and speedometer text-cache improvements
are preserved in the integration based on `0d439157`.

## Native evidence

Unity **6000.3.26f1**, all 34 real runtime sources, no runtime test doubles:

| Result | Baseline `c839550d` | Final integrated candidate |
| --- | ---: | ---: |
| Completed control-state cases | 24 | 24 |
| Passed cases | 8 | 24 |
| Failed cases | 16 | 0 |
| C# compiler errors | 0 | 0 |
| Candidate Unity process exit | — | 0 |

The baseline produced its complete failing report before its resource guard
stopped the process during shutdown; no normal baseline exit is claimed.
Both the initial candidate and the final candidate including the newer
speedometer changes passed all 24 cases and exited 0.

Cases cover motor/session/HUD pause and disable, cached input clearing with
no world reference, suspension and same-frame re-enable, focus/app pause,
releases during pause, fresh-press recovery, valid forward/reverse/clamped
and nonfinite input, and ownership isolation from another vehicle's disable.
The HUD case verifies that EXIT remains visible for a disabled occupied car.

Runtime SHA-256 fingerprints verified before final execution:

- Session: `76881455375e50e24fc78974e0bb1a6d1734c0dcb9fa405a5bbf5e421dae198f`
- Motor: `8801c2ee125e473cc3347c81978958454f37601e866e726a74e2a27dc0e65b22`
- HUD: `f993be008b3e312ee83ffaf624682aa14cd2c12a76c7fda28c3e8453286d3d18`

The existing disposable source-only project excludes optional VFX Graph
thumbnail imports. Fixtures wire the real components' stored references and
invoke lifecycle/Update callbacks directly in Edit Mode. They do not simulate
WheelCollider motion, real pointer dispatch, a supported physical car exit,
the native map, a new APK or Android FPS. Low-priority probes have guards
which can stop only their own process group, preserving other build work.

Repeat through **Aaris > CI > Verify driving input cannot survive suspension**
or synchronous batch `-executeMethod Aaris.MapReview.Editor.AarisDrivingInputQA.Run`.
The audit preserves the original active scene and timeScale, saves no assets,
uses disposable actors and exits only in batch mode. Private temporary report:
`aaris-driving-input-qa/report.txt`.

Seventeen existing regressions and C# syntax checks on all 76 Aaris scripts
passed locally. Lifecycle source contracts now require clearing both the
session and bound HUD while preserving safe-exit behavior and input releases.
