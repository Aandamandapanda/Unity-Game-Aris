#!/usr/bin/env python3
"""Portable material and single-LOD contracts for budget house details."""
import re
import struct
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "Assets/AarisWorld/Scripts/AarisHouseFacadeTrim.cs").read_text()
TEXTURE = ROOT / "Assets/Resources/AarisBuildings/ArchitecturalLimestone.png"
META = Path(str(TEXTURE) + ".meta")
GENERATOR = ROOT / "Tools/Art/generate_limestone_trim.py"


class FacadeTextureBudget(unittest.TestCase):
    def test_baked_original_stone_texture_is_small_RGB_with_import_metadata(self):
        blob = TEXTURE.read_bytes()
        self.assertTrue(blob.startswith(b"\x89PNG\r\n\x1a\n"))
        self.assertEqual(struct.unpack(">II",blob[16:24]),(128,128))
        self.assertEqual(blob[25],2, "Must be RGB without an alpha render pass")
        self.assertLess(len(blob),18000)
        self.assertIn("generate_limestone_trim.py",str(GENERATOR))
        self.assertIn("SEED = 0xA512026",GENERATOR.read_text())
        meta = META.read_text()
        self.assertRegex(meta,r"(?m)^guid: [a-f0-9]{32}$")
        self.assertIn("enableMipMap: 1",meta)
        self.assertIn("streamingMipmaps: 1",meta)
        self.assertIn("sRGBTexture: 1",meta)
        self.assertIn("wrapU: 0",meta)
        self.assertIn("wrapV: 0",meta)
        self.assertIn("buildTarget: Android",meta)
        self.assertIn("maxTextureSize: 256",meta)

    def test_stone_texture_is_shared_and_has_mandatory_color_fallback(self):
        self.assertIn('Resources.Load<Texture2D>(',SOURCE)
        self.assertIn('"AarisBuildings/ArchitecturalLimestone"',SOURCE)
        self.assertIn('if (stone != null)',SOURCE)
        self.assertIn('sharedStucco.SetTexture("_BaseMap", stone);',SOURCE)
        self.assertIn('sharedStucco = new Material(shader)',SOURCE)
        self.assertNotIn("new Texture2D(",SOURCE)
        self.assertNotIn("Texture2D.LoadImage",SOURCE)

    def test_tiled_physical_uv_not_stretched_over_whole_house(self):
        self.assertIn("const float MetresPerTile = .85f;",SOURCE)
        self.assertIn("Vector3.Distance(b, d) / MetresPerTile",SOURCE)
        self.assertIn("Vector3.Distance(a, b) / MetresPerTile",SOURCE)
        self.assertIn("uv.Add(new Vector2(along, across));",SOURCE)
        self.assertNotIn("uv.Add(Vector2.one);",SOURCE)

    def test_native_one_step_lod_culls_without_updates_or_colliders(self):
        self.assertIn("detail.AddComponent<LODGroup>()",SOURCE)
        self.assertIn("lod.fadeMode = LODFadeMode.None;",SOURCE)
        self.assertIn("lod.animateCrossFading = false;",SOURCE)
        self.assertIn("new LOD(.08f, new Renderer[] { renderer })",SOURCE)
        self.assertIn("lod.RecalculateBounds();",SOURCE)
        self.assertNotIn("void Update(",SOURCE)
        self.assertNotIn("AddComponent<BoxCollider>",SOURCE)
        self.assertNotIn("AddComponent<MeshCollider>",SOURCE)
        self.assertEqual(SOURCE.count("AddComponent<MeshRenderer>()"),1)
        self.assertIn("Destroy(ownedMesh);",SOURCE)
        self.assertIn("Destroy(sharedStucco);",SOURCE)


if __name__ == "__main__":
    unittest.main()
