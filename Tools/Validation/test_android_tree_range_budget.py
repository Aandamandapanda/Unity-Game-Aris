#!/usr/bin/env python3
"""Regression guard for reversible low-end Android terrain draw-distance caps.

Not an Android profiler or Unity Play Mode test.
"""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Assets/AarisWorld/Scripts/AarisMobileRuntime.cs"
SCENE = ROOT / "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity"


class TerrainDrawRangeBudget(unittest.TestCase):
    def test_source_terrain_is_untouched(self):
        data = SCENE.read_text()
        self.assertEqual(re.findall(r"(?m)^  m_TreeDistance: (\d+)$", data), ["850"] * 4)

    def test_android_only_constrained_gpu_caps_draw_distance(self):
        code = RUNTIME.read_text()
        setup = code.split("private void ConfigureLowMemoryVegetation()", 1)[1]
        setup = setup.split("private static float EffectiveAndroidTargetFps(", 1)[0]
        self.assertIn("originalTreeDistances[i] = tile.treeDistance;", setup)
        self.assertIn("? 450f : 600f;", setup)
        self.assertIn("Mathf.Min(originalTreeDistances[i],", setup)
        self.assertIn("tile.treeDistance = appliedTreeDistances[i];", setup)
        self.assertIn("if (constrainedGraphics)", code)
        self.assertIn("ConfigureLowMemoryVegetation();", code)

    def test_cleanup_restores_only_unchanged_instance_state(self):
        code = RUNTIME.read_text()
        end = code.split("private void OnDestroy()", 1)[1]
        self.assertIn("Mathf.Approximately(tile.treeDistance, appliedTreeDistances[i])", end)
        self.assertIn("tile.treeDistance = originalTreeDistances[i];", end)


if __name__ == "__main__":
    unittest.main(verbosity=2)
