"""Regression contracts for the non-serialized Linux terrain preview."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SHADER = (ROOT / "Assets/AarisWorld/Surfaces/AarisGlcoreTerrainPreview.shader").read_text()
EDITOR = (ROOT / "Assets/AarisWorld/Editor/AarisGlcoreTerrainPreview.cs").read_text()


class GlcoreTerrainPreviewTests(unittest.TestCase):
    def test_all_painted_splats_and_controls_exist(self):
        self.assertIn("#pragma only_renderers glcore", SHADER)
        self.assertIn('Shader "Hidden/Aaris/GlcoreTerrainPreview"', SHADER)
        for i in range(3):
            name = "_Control" + str(i) if i else "_Control"
            self.assertIn("TEXTURE2D(" + name + ")", SHADER)
        for i in range(10):
            self.assertIn("TEXTURE2D(_Splat%d)" % i, SHADER)
            self.assertIn("_Splat%d_ST" % i, SHADER)
        self.assertIn("albedo /= total;", SHADER)

    def test_each_tile_has_isolated_unsaved_material(self):
        for marker in ("new Material(previewMaterial)", "HideFlags.HideAndDontSave",
                       "data.alphamapTextures", "data.terrainLayers",
                       "BindExtraLayers(local, terrain.terrainData)",
                       "snapshot.preview"):
            self.assertIn(marker, EDITOR)
        self.assertEqual(EDITOR.count("DestroyImmediate(snapshot.preview)"), 2)

    def test_android_and_saved_scene_remain_untouched(self):
        self.assertIn("#if UNITY_EDITOR_LINUX", EDITOR)
        self.assertIn("EditorSceneManager.sceneSaving += OnSceneSaving;", EDITOR)
        self.assertIn("RestoreScene(scene);", EDITOR)
        self.assertNotIn("AssetDatabase.SaveAssets(", EDITOR)
        self.assertNotIn("EditorSceneManager.SaveScene(", EDITOR)


if __name__ == "__main__":
    unittest.main()
