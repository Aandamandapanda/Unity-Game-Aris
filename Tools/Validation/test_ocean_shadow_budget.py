#!/usr/bin/env python3
"""Guard the one-time visual-only ocean shadow-pass optimization.

Source-level tests; they do not replace Unity Play Mode or Android profiling.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
RUNTIME = ROOT / "Assets/AarisWorld/Scripts/AarisMobileRuntime.cs"
SCENE = ROOT / "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity"


class OceanShadowBudgetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = RUNTIME.read_text(encoding="utf-8")

    def test_shadow_setup_runs_before_first_render_and_not_each_frame(self):
        source = self.source
        startup = source.split("private IEnumerator Start()", 1)[1].split(
            "yield return null;", 1
        )[0]
        self.assertIn("ConfigureOceanShadowBudget();", startup)
        self.assertLess(
            startup.index("ConfigureOceanShadowBudget();"),
            startup.index("ConfigureTerrainInstanceRenderer();"),
        )
        self.assertEqual(source.count("ConfigureOceanShadowBudget();"), 1)

    def test_ocean_does_not_submit_or_receive_shadows(self):
        helper = self.source.split(
            "private static void ConfigureOceanShadowBudget()", 1
        )[1].split("private void ConfigureTerrainInstanceRenderer()", 1)[0]
        for expected in (
            'GameObject.Find("Ocean_VisualOnly")',
            "ocean.GetComponent<MeshRenderer>()",
            "if (ocean == null) return;",
            "if (oceanRenderer == null) return;",
            "oceanRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;",
            "oceanRenderer.receiveShadows = false;",
        ):
            with self.subTest(expected=expected):
                self.assertIn(expected, helper)
        self.assertNotIn("material =", helper)
        self.assertNotIn("terrainData", helper)
        self.assertNotIn("Update()", helper)

    def test_authored_ocean_scene_is_retained(self):
        self.assertIn("m_Name: Ocean_VisualOnly", SCENE.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
