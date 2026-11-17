# Aaris original off-road buggy and automotive finishes (2026-11-17)

A new seventh drivable family, Buggy, uses an entirely original locally
generated open-cockpit body (828 vertices, 1368 triangles, seven surface
groups) combined with the existing verified wheel-truck FBX. The chassis
contains bucket seats, tubular roll cage, thin wind deflector, fenders,
mechanical bracing, front/rear lamps and side mirrors. Rebuild exactly with
python3 Tools/Art/generate_aaris_buggy.py. No third-party game mesh is copied.

Catalog parameters: AWD, 1130 kg, speed cap 25 m/s and modestly softer
suspension with off-road grip. The existing WheelCollider physics, bounded
streaming, clearance checks and 44 planned world slots remain authoritative.
No additional vehicle is guaranteed to spawn at an unverified floor.

The Buggy's resource fallback uses local CC0 truck-flat and wheel-truck
meshes if native authored OBJ import fails. Explicit Unity importer checks
were extended for the new chassis. Shared URP materials provide dark
interiors, separate bare metal, tinted glass and paint. Dedicated paint
supports URP clear coat; texture atlases, wheels and windows do not receive
the paint shader override.

These changes have offline tests. Android framerate, native Unity model
import, wheel alignment, camera and driving feel require real Play Mode
and device testing before claiming production parity with commercial games.
