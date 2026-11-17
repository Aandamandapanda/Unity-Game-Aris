#!/usr/bin/env python3
"""Offline real-player resource guards; does not replace Unity/Android Play Mode."""
import json
import math
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Assets"
RES = ROOT / "Resources"
MODEL_SUFFIXES = (".prefab", ".fbx", ".obj", ".asset", ".blend")


class OfflineResourceContractTest(unittest.TestCase):
    def test_weapon_and_vehicle_catalogs_resolve_embedded_models(self):
        weapon = json.loads((RES / "AarisWeapons/weapon_catalog.json").read_text())
        vehicle = json.loads((RES / "AarisVehicles/vehicle_catalog.json").read_text())
        self.assertGreaterEqual(len(weapon["weapons"]), 5)
        self.assertGreaterEqual(len(vehicle["vehicles"]), 6)
        for records in (weapon["weapons"], vehicle["vehicles"]):
            ids = [item["id"] for item in records]
            self.assertEqual(len(ids), len(set(ids)))
            for item in records:
                self.assertGreater(float(item["weight"]), 0)
                for key in ("modelResource", "wheelResource", "authoredModelResource",
                            "authoredWheelResource", "fallbackAuthoredModelResource"):
                    name = item.get(key)
                    if not name:
                        continue
                    with self.subTest(type=item["id"], resource=name):
                        self.assertNotIn("..", Path(name).parts)
                        matches = [RES / (name + ext) for ext in MODEL_SUFFIXES
                                   if (RES / (name + ext)).is_file()]
                        self.assertEqual(len(matches), 1, "Missing GameObject: " + name)
                        self.assertTrue(Path(str(matches[0]) + ".meta").is_file(),
                                        "Missing Unity import metadata: " + name)

    def test_loot_and_vehicle_candidates_have_map_wide_coverage(self):
        cases = (("AarisWeapons/loot_candidates.json", 500, 16),
                 ("AarisVehicles/vehicle_candidates.json", 70, 12))
        for resource, minimum, sector_minimum in cases:
            with self.subTest(data=resource):
                data = json.loads((RES / resource).read_text())
                self.assertEqual(data["schema"], 1)
                self.assertEqual(data["sizeMetres"], 2000)
                self.assertGreaterEqual(len(data["candidates"]), minimum)
                sectors = set()
                for point in data["candidates"]:
                    x, y, z = (float(point[k]) for k in ("x", "y", "z"))
                    self.assertTrue(all(math.isfinite(v) for v in (x, y, z)))
                    self.assertLessEqual(abs(x), 1000)
                    self.assertLessEqual(abs(z), 1000)
                    sectors.add((min(3, max(0, int((x + 1000) // 500))),
                                 min(3, max(0, int((z + 1000) // 500)))))
                self.assertGreaterEqual(len(sectors), sector_minimum)

    def test_native_scene_direct_game_script_guids_exist(self):
        scene = (ROOT / "TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity").read_text()
        refs = re.findall(r"m_Script:\s*\{[^}\n]*guid:\s*([0-9a-f]{32})", scene)
        self.assertTrue(refs, "No MonoBehaviours in native scene")
        known = set()
        for meta in ROOT.rglob("*.cs.meta"):
            match = re.search(r"(?m)^guid:\s*([a-f0-9]{32})", meta.read_text())
            if match:
                known.add(match.group(1))
        for guid in refs:
            self.assertIn(guid, known, "Unresolved game-script GUID")


if __name__ == "__main__":
    unittest.main()
