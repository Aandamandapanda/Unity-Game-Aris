#!/usr/bin/env python3
"""Guard the visual-only textured gun URP fallback; no weapon mechanics changed."""
from pathlib import Path
import unittest
R=Path(__file__).resolve().parents[2]
P=R/"Assets/AarisWorld/Scripts/AarisWeaponInventory.cs"
class GunPbrImport(unittest.TestCase):
 def test_urp_conversion_preserves_authored_maps(self):
  src=P.read_text()
  method=src.split("private static Material ConvertedWeaponFinish(",1)[1].split(
   "internal static Quaternion ImportedModelRotation(",1)[0]
  for value in ('if (HasAuthoredTexture(source))',
    'Shader.Find("Universal Render Pipeline/Lit")',
    'shaderName == "Universal Render Pipeline/Lit"',
    'source.GetTextureScale(texProperty)',
    'source.GetTextureOffset(texProperty)',
    'converted.SetTexture("_BumpMap"',
    'converted.EnableKeyword("_NORMALMAP")',
    'converted.SetTexture("_MetallicGlossMap"',
    'converted.EnableKeyword("_METALLICSPECGLOSSMAP")',
    'weaponFinishCache[source] = converted;',
    'return converted;'):
   self.assertIn(value,method)
  self.assertNotIn('renderer.material',method)
  self.assertNotIn('new GameObject',method)
 def test_preserves_weapon_logic_and_single_cache_per_material(self):
  src=P.read_text()
  self.assertIn("private static readonly Dictionary<Material, Material> weaponFinishCache",src)
  for name in ("Reload()","DropCurrent()","SwapWeapons()"):
   self.assertIn(name,src)
  self.assertIn("ApplyTextureAwareWeaponFinish(",src)
if __name__=="__main__":
 unittest.main()
