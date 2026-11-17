#!/usr/bin/env python3
"""Offline baked, mobile-safe architecture normal-map validation."""
from pathlib import Path
import re,struct,hashlib,subprocess,sys,unittest
R=Path(__file__).resolve().parents[2]
BASE=R/"Assets/Resources/AarisBuildings/ArchitecturalLimestoneNormal.png"
SCRIPT=R/"Assets/AarisWorld/Scripts/AarisHouseFacadeTrim.cs"
GEN=R/"Tools/Art/generate_limestone_trim.py"
class HouseNormalAssets(unittest.TestCase):
 def test_shared_rgb_normal_map_and_import_budget(self):
  raw=BASE.read_bytes()
  self.assertTrue(raw.startswith(b"\x89PNG\r\n\x1a\n"))
  self.assertEqual(struct.unpack(">II",raw[16:24]),(128,128))
  self.assertEqual(raw[25],2)
  self.assertLess(len(raw),18000)
  meta=Path(str(BASE)+".meta").read_text()
  for v in ("textureType: 1","sRGBTexture: 0","enableMipMap: 1",
            "streamingMipmaps: 1","buildTarget: Android","maxTextureSize: 256"):
   self.assertIn(v,meta)
  self.assertRegex(meta,r"(?m)^guid: [0-9a-f]{32}$")
 def test_urp_normal_is_optional_and_shared_across_homes(self):
  src=SCRIPT.read_text()
  self.assertIn('"AarisBuildings/ArchitecturalLimestoneNormal"',src)
  self.assertIn('sharedStucco.SetTexture("_BumpMap", stoneNormal);',src)
  self.assertIn('sharedStucco.EnableKeyword("_NORMALMAP");',src)
  self.assertIn('if (stoneNormal != null && sharedStucco.HasProperty("_BumpMap"))',src)
  self.assertNotIn("new Texture2D(",src)
  self.assertEqual(src.count("new Material(shader)"),1)
 def test_reproducible_baked_normal(self):
  before=hashlib.sha256(BASE.read_bytes()).hexdigest()
  subprocess.run([sys.executable,str(GEN)],check=True,capture_output=True)
  self.assertEqual(before,hashlib.sha256(BASE.read_bytes()).hexdigest())
if __name__=="__main__":unittest.main()
