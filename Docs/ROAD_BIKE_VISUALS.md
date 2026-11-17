# Original Roadside Motorcycle Visuals

The authorable AarisRoadBike.obj is reproducibly generated with:
python3 Tools/Art/generate_road_bike.py

The mesh is original project geometry (no downloaded game assets).
It has independent tyre profiles, alloy rims, chassis bars, forks, fuel
tank, engine, saddle, handlebars, and distinct head/tail/indicator surfaces.
Eight shared URP Lit surface materials and three extra paint variants control body paint, rubber, alloy, trim and
lamp appearance. There are no network requests at game runtime.

The AarisRoadsideBikeScenery scene boot component places **parked scenery**,
not rideable motorcycles. It samples 17 reviewed roadside candidates
from the same existing 2km map, checks real floor normal and height,
tests box clearance and inactive author colliders, and streams at most
three visible motorcycles with one spawn attempt each 400ms poll.
Distance hysteresis is 45m entry / 66m exit.

A parked bike must not be presented as a four-wheel driveable car. A
future rideable motorcycle requires a proper two-wheel steering/balance,
suspension and rider animation system with device-level handling tests.

Static tests check source/topology, not live graphics. Native Unity OBJ
import, URP shading, placement, and Android-device FPS require separate
validation before claiming visual or performance parity with any title.
