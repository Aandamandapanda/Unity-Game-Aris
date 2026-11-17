import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/AarisWorld/Editor/AarisSoftwareRendererPreview.cs"

class SoftwareRendererPreviewTests(unittest.TestCase):
    def test_only_cpu_graphics_targeted(self):
        source = SOURCE.read_text(encoding="utf-8")
        for marker in ("llvmpipe", "softpipe", "SwiftShader", "IsSoftwareRenderer(renderer)", "Application.isBatchMode"):
            self.assertIn(marker, source)
        self.assertIn("!EditorApplication.isPlaying", source)
        self.assertIn("BuildPipeline.isBuildingPlayer", source)
        self.assertIn("IPreprocessBuildWithReport", source)
        self.assertIn("SuspendForBuild();", source)

    def test_clones_pipeline_and_restores_before_reload_or_quit(self):
        source = SOURCE.read_text(encoding="utf-8")
        for marker in ("UnityEngine.Object.Instantiate(current)", "HideFlags.HideAndDontSave",
                       "QualitySettings.renderPipeline = originalQualityPipeline",
                       "AssemblyReloadEvents.beforeAssemblyReload += Restore",
                       "EditorApplication.quitting += Restore"):
            self.assertIn(marker, source)
        for forbidden in ("AssetDatabase.SaveAssets", "EditorSceneManager.SaveScene",
                          "RenderSettings.skybox =", "SetDirty"):
            self.assertNotIn(forbidden, source)

    def test_cpu_preview_budget_is_bounded(self):
        source = SOURCE.read_text(encoding="utf-8")
        for marker in ("local.supportsHDR = false", "local.msaaSampleCount = 1",
                       "local.shadowDistance", "local.shadowCascadeCount = 1",
                       "local.mainLightShadowmapResolution"):
            self.assertIn(marker, source)

if __name__ == "__main__":
    unittest.main()
