#!/usr/bin/env python3
"""Source geometry regression for faceted roof lighting, not a Unity render."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/AarisWorld/Scripts/AarisModularHouse.cs"


class RoofShading(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.roof = SOURCE.read_text().split(
            "private static void PitchedRoof(", 1)[1].split(
            "private static void CombineStaticSurfaces(", 1)[0]

    def test_distinct_vertices_per_slope_and_gable(self):
        src = self.roof
        self.assertIn("Vector3[] vertices = new Vector3[faceCorners.Length];", src)
        self.assertIn("vertices[i] = corners[faceCorners[i]];", src)
        match = re.search(r"int\[\] faceCorners = \{(.*?)\};", src, re.S)
        values = [int(n) for n in re.findall(r"\b\d+\b", re.sub(r"//[^\n]*", "", match.group(1)))]
        self.assertEqual(values, [0,3,1,4,1,4,2,5,0,1,2,3,5,4])
        self.assertEqual(len(values), 14)
        self.assertIn("mesh.RecalculateNormals();", src)

    def test_six_properly_wound_triangles_and_single_renderer(self):
        src = self.roof
        match = re.search(r"mesh\.triangles = new\[\] \{(.*?)\};", src, re.S)
        indices = [int(n) for n in re.findall(r"\d+", match.group(1))]
        self.assertEqual(indices, [0,1,2,2,1,3,4,5,6,6,5,7,8,9,10,11,12,13])
        self.assertEqual(len(indices)//3, 6)
        self.assertEqual(src.count("AddComponent<MeshRenderer>()"), 1)
        self.assertNotIn("AddComponent<MeshCollider>()", src)
        self.assertIn("mesh.RecalculateBounds();", src)


if __name__ == "__main__":
    unittest.main(verbosity=2)
