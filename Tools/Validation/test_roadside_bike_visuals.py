"""Standalone static mesh and runtime checks for original parked-bike scenery."""
import re
import unittest
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / "Assets/Resources/AarisVehicles/Authored"
SCRIPT = ROOT / "Assets/AarisWorld/Scripts/AarisRoadsideBikeScenery.cs"

class BikeVisuals(unittest.TestCase):
    def test_mesh_has_correct_rims_tyres_materials_and_bounds(self):
        lines = (ASSET / "AarisRoadBike.obj").read_text().splitlines()
        vertices = [tuple(map(float, l.split()[1:])) for l in lines if l.startswith("v ")]
        triangles = [l for l in lines if l.startswith("f ")]
        materials = {l.split()[1] for l in lines if l.startswith("usemtl ")}
        self.assertTrue(3000 <= len(vertices) <= 5000)
        self.assertTrue(6000 <= len(triangles) <= 8500)
        self.assertEqual(materials, {"Paint", "Tyre", "Rim", "Engine",
                                     "Seat", "Headlamp", "TailLamp", "Amber"})
        self.assertIn("mtllib AarisRoadBike.mtl", lines)
        self.assertTrue(all(len(f.split()) == 4 for f in triangles))
        for axis, (lower, upper) in enumerate(((-.6, .6), (0, 1.6), (-1.3, 1.3))):
            self.assertGreaterEqual(min(v[axis] for v in vertices), lower)
            self.assertLessEqual(max(v[axis] for v in vertices), upper)

    def test_unity_metadata_is_stable_unique(self):
        files = [ASSET / "AarisRoadBike.obj", ASSET / "AarisRoadBike.mtl", SCRIPT]
        ids = []
        for asset in files:
            self.assertTrue(asset.is_file())
            meta = Path(str(asset) + ".meta")
            match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta.read_text())
            self.assertIsNotNone(match)
            ids.append(match.group(1))
        self.assertEqual(len(ids), len(set(ids)))
        self.assertIn("Original", (ROOT / "Tools/Art/generate_road_bike.py").read_text())

    def test_mobile_streaming_is_bounded_and_ground_validated(self):
        code = SCRIPT.read_text()
        for marker in ("MaximumVisibleBikes = 3", "LoadRadius = 45f",
                       "UnloadRadius = 66f", "index++ % 6 == 2",
                       "nextPoll = now + .4f", "Physics.RaycastNonAlloc(",
                       "Physics.OverlapBoxNonAlloc(", "HasPotentialInactiveObstacle(",
                       "root.AddComponent<BoxCollider>()",
                       "Resources.Load<GameObject>(ModelResource)",
                       "Destroy(material);"):
            self.assertIn(marker, code)
        self.assertNotIn("AddComponent<Rigidbody>", code)
        self.assertNotIn("AddComponent<WheelCollider>", code)
        self.assertNotIn("AarisVehicleMotor", code)
        update = code.split("private void Update()", 1)[1].split(
            "private bool TryParkingSpot(", 1)[0]
        self.assertEqual(update.count("CreateBike("), 1)
        self.assertIn("if (visibleCount >= MaximumVisibleBikes) return;", update)

if __name__ == "__main__":
    unittest.main()
