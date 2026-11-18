# Android car-stream frame pacing — 2026-11-18

## What changed
The offline 44-plan vehicle streamer already caps nearby cars at ten and
checks location/collision before instantiation. Previously, during a single
300 ms poll, a stalling Android device could still attempt eight expensive
placements and create two full physics/mesh cars.

When Unity's smooth frame delta exceeds 1/45 second on an Android **player**,
each poll now attempts at most three placements and successfully builds at most
one car. Faster Android devices, Unity Editor and desktop keep the previous
two-car / eight-probe budget. Near-first loading and safe old-car eviction are
unchanged. The 105 m load radius, 150 m FBX prewarm, original car scale,
body materials, wheel geometry and URP quality remain unchanged. This change
does not create textures, renderer instances, dynamic allocations or extra
per-frame lookups.

## QA and outstanding proof
Run `python3 Tools/Validation/test_vehicle_adaptive_frame_pacing.py`,
`python3 Tools/Validation/test_vehicle_streaming_priority.py`,
and `python3 Tools/Validation/check_mobile_hotpaths.py`.
Unit tests model budget bounds; they do not measure phone FPS. The attempted
native Unity car-photo capture timed out during large initial Unity asset
import, and no live Play Mode/Android screenshot or frame timing was produced.
Avoid claiming that the low-poly imported car art is now photorealistic.
