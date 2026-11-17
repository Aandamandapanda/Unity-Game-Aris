#!/usr/bin/env python3
"""Offline URP material conversion regression; visual Android proof is separate."""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "Assets/AarisWorld/Scripts/AarisWeaponInventory.cs").read_text()


class WeaponVisualPbrTransfer(unittest.TestCase):
    def test_legacy_surface_maps_are_preserved_before_metallic_fallback(self):
        convert = SOURCE.split("private static Material ConvertedWeaponFinish(", 1)[1].split(
            "internal static Quaternion ImportedModelRotation(", 1)[0]
        for token in ('converted.SetTexture("_BumpMap", source.GetTexture("_BumpMap"))',
                      'source.GetFloat("_BumpScale"), .2f, 1.5f',
                      'converted.SetTexture("_OcclusionMap",',
                      'source.GetTexture("_OcclusionMap")',
                      'converted.EnableKeyword("_OCCLUSIONMAP")',
                      'source.GetFloat("_OcclusionStrength")',
                      'converted.EnableKeyword("_METALLICSPECGLOSSMAP")',
                      'converted.EnableKeyword("_NORMALMAP")'):
            self.assertIn(token, convert)
        self.assertLess(convert.index('source.GetTexture("_OcclusionMap")'),
                        convert.index('source.GetTexture("_MetallicGlossMap")'))
        self.assertIn("weaponFinishCache[source] = converted;", convert)
        self.assertIn('shaderName == "Universal Render Pipeline/Lit"', convert)
        self.assertNotIn("new Texture2D(", convert)

    def test_shared_finish_on_world_and_equipped_models(self):
        pickup = (ROOT / "Assets/AarisWorld/Scripts/AarisWeaponPickup.cs").read_text()
        self.assertIn("ApplyTextureAwareWeaponFinish(", pickup)
        self.assertIn("ApplyTextureAwareWeaponFinish(incoming.Spec, renderers);", SOURCE)
        self.assertIn("if (weaponFinishCache.TryGetValue(source, out Material textured)", SOURCE)


if __name__ == "__main__":
    unittest.main()
