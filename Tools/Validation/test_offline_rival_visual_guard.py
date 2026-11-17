#!/usr/bin/env python3
import unittest
from pathlib import Path

SOURCE = (Path(__file__).resolve().parents[2] /
    "Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs").read_text("utf-8")

class RivalVisualGuard(unittest.TestCase):
    def test_fallback_prefabs_are_mesh_checked(self):
        body=SOURCE.split("internal void ResolveRivalAssets(",1)[1].split(
            "internal static bool HasRenderableRivalPrefab(",1)[0]
        self.assertGreaterEqual(body.count("!HasRenderableRivalPrefab(rivalPrefab)"),3)
        self.assertIn("rivalPrefab = null;",body)

    def test_non_renderable_parts_are_rejected(self):
        body=SOURCE.split("internal static bool HasRenderableRivalPrefab(",1)[1].split(
            "private static AnimationClip LoadRivalClip(",1)[0]
        for required in ("source.activeSelf", "!renderer.enabled",
            "renderer.forceRenderingOff", "!node.gameObject.activeSelf",
            "skinned.sharedMesh.vertexCount > 0",
            "filter.sharedMesh.vertexCount > 0"):
            self.assertIn(required,body)
        self.assertNotIn("Renderer.bounds",body)

    def test_runtime_checks_before_animation(self):
        body=SOURCE.split("private void BuildVisual()",1)[1].split(
            "private void BuildFallbackSilhouette()",1)[0]
        self.assertIn("HasRenderableRivalPrefab(visual)",body)
        self.assertIn("Destroy(visual);",body)
        self.assertLess(body.index("HasRenderableRivalPrefab(visual)"),
                        body.index("TryFitHumanoidModel("))
        fallback=SOURCE.split("private void BuildFallbackSilhouette()",1)[1].split(
            "private static bool TryFitHumanoidModel(",1)[0]
        self.assertIn("collider.enabled = false;",fallback)

if __name__ == "__main__":
    unittest.main()
