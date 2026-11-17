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
            "private void ConfigureOceanShadowBudget()", 1
        )[1].split("private void ConfigureTerrainInstanceRenderer()", 1)[0]
        for expected in (
            'GameObject.Find("Ocean_VisualOnly")',
            "oceanShadowRenderer = ocean.GetComponent<MeshRenderer>()",
            "if (ocean == null) return;",
            "if (oceanShadowRenderer == null) return;",
            "oceanShadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;",
            "oceanShadowRenderer.receiveShadows = false;",
        ):
            with self.subTest(expected=expected):
                self.assertIn(expected, helper)
        self.assertNotIn("material =", helper)
        self.assertNotIn("terrainData", helper)
        self.assertNotIn("Update()", helper)

    def test_shadow_settings_are_restored_only_if_still_owned(self):
        cleanup = self.source.split("private void OnDestroy()", 1)[1].split(
            "private void OnLog(", 1
        )[0]
        for expected in (
            "if (oceanShadowRenderer != null)",
            "if (changedOceanCasting &&",
            "oceanShadowRenderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off",
            "oceanShadowRenderer.shadowCastingMode = originalOceanCasting;",
            "if (changedOceanReceiving && !oceanShadowRenderer.receiveShadows)",
            "oceanShadowRenderer.receiveShadows = originalOceanReceiving;",
        ):
            with self.subTest(expected=expected):
                self.assertIn(expected, cleanup)

    def test_authored_ocean_scene_is_retained(self):
        self.assertIn("m_Name: Ocean_VisualOnly", SCENE.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
