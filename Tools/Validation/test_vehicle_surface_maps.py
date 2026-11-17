#!/usr/bin/env python3
"""Offline car PBR transfer guard; native Unity shader test is separate."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets/AarisWorld/Scripts"
WORLD = (SCRIPTS / "AarisVehicleWorld.cs").read_text()
HELPER = (SCRIPTS / "AarisVehicleSurfaceMaps.cs").read_text()


class VehicleSurfaceFidelity(unittest.TestCase):
    def test_source_maps_retain_uv_scale_and_offset(self):
        self.assertIn("internal static Texture Transfer(", HELPER)
        self.assertIn("source.GetTexture(sourceProperty)", HELPER)
        self.assertIn("target.SetTexture(destinationProperty, texture);", HELPER)
        self.assertIn("source.GetTextureScale(sourceProperty)", HELPER)
        self.assertIn("source.GetTextureOffset(sourceProperty)", HELPER)
        self.assertIn("target.SetTextureScale(destinationProperty,", HELPER)
        self.assertIn("target.SetTextureOffset(destinationProperty,", HELPER)
        self.assertIn("if (texture == null) return null;", HELPER)

    def test_normal_map_and_occlusion_scalar_are_preserved(self):
        self.assertIn('"BumpScale"', WORLD.replace('"_BumpScale"', '"BumpScale"'))
        self.assertIn('"_BumpScale", 1f, 2f', WORLD)
        self.assertIn('"_OcclusionMap"', WORLD)
        self.assertIn('"_OcclusionStrength"', WORLD)
        self.assertIn('result.EnableKeyword("_OCCLUSIONMAP");', WORLD)
        self.assertIn('result.EnableKeyword("_NORMALMAP");', WORLD)
        self.assertIn('result.EnableKeyword("_METALLICSPECGLOSSMAP");', WORLD)
        self.assertIn("float.IsNaN(value) || float.IsInfinity(value)", HELPER)
        self.assertIn("Mathf.Clamp(value, 0f, maximum)", HELPER)

    def test_missing_base_map_falls_back_to_legacy_main_tex(self):
        self.assertIn('source.GetTexture("_BaseMap") != null', WORLD)
        self.assertIn('"_BaseMap" : "_MainTex"', WORLD)
        self.assertIn('AarisVehicleSurfaceMaps.Transfer(source, result, baseProperty, "_BaseMap");', WORLD)

    def test_authored_urp_pbr_assets_remain_untouched_and_shared(self):
        self.assertIn('source.shader.name == "Universal Render Pipeline/Lit"', WORLD)
        self.assertIn("return source;", WORLD)
        self.assertIn("authoredMaterialCache.TryGetValue(source, out Material cached)", WORLD)
        self.assertIn("authoredMaterialCache[source] = result;", WORLD)
        self.assertNotIn("private void Update()", HELPER)
        self.assertNotIn("new Material(", HELPER)
        self.assertNotIn("new Texture2D(", HELPER)
        bodies = list((ROOT / "Assets/Resources/AarisVehicles/Authored").glob(
            "PBR_*/*body_pbr.mat"))
        self.assertEqual(len(bodies), 5)
        for body in bodies:
            with self.subTest(body=body.name):
                source = body.read_text()
                self.assertIn("- _BaseMap:", source)
                self.assertIn("- _BumpMap:", source)
                self.assertIn("- _MetallicGlossMap:", source)
                self.assertIn("m_EnableInstancingVariants: 1", source)


if __name__ == "__main__":
    unittest.main()
