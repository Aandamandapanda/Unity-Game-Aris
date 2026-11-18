# Third-person near-wall camera (18 November 2026)

Before: player camera collision gave the lens a minimum 12 cm of travel
even when a wall was only 2 cm from the pivot. This could move the view
through a very close obstacle.

Fix: clamp travel between zero and the actual hit distance minus 8 cm.
If the already-existing second collision sweep corrects a SmoothDamp
position, clear the spring velocity to avoid repeated wall pushing.
No extra physics queries, render passes, allocations or camera modes.
Crouch/prone visibility hysteresis and vehicle/aircraft handoff remain.

QA: numerical close-wall regression and existing mobile hotpath /
jump-contact tests. Unity screenshots and Android performance are
independently required; these source checks do not prove device FPS.
