"""Source contracts for opt-in native ForestLookout world-cell migration.

These do not replace a real Unity Editor compile, scene reopen or Android FPS QA.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
EDITOR = (ROOT / "Assets/AarisWorld/Editor/AarisForestLookoutPartitionPilot.cs")
RUNTIME = (ROOT / "Assets/AarisWorld/Scripts/AarisChunkFallbackVisibility.cs")


class ForestLookoutPilotSourceTests(unittest.TestCase):
    def test_editor_migration_is_privately_gated_and_targeted(self):
        s = EDITOR.read_text()
        self.assertIn('AARIS_WORLD_PARTITION_APPLY', s)
        self.assertIn('public static void Probe()', s)
        self.assertIn('public static void Apply()', s)
        self.assertIn('public static void VerifyAuthoredPilot()', s)
        self.assertIn('AARIS_FORESTLOOKOUT_PILOT_VERIFY_PASS', s)
        self.assertIn('Check(matches == 1, "physical-proxy-not-rebound");', s)
        self.assertIn('originalMesh.sharedMesh == proxy.sharedMesh', s)
        self.assertIn('ForestLookout_watchtower_01', s)
        self.assertIn('Aaris_ForestLookout_Detail.unity', s)
        self.assertIn('Check(district.transform.childCount == 1', s)
        self.assertIn('Check(matches == 1,', s)
        self.assertIn('EditorSceneManager.NewScene(', s)
        self.assertIn('SceneManager.MoveGameObjectToScene(c.tower, newCell);', s)
        self.assertNotIn('DestroyImmediate(c.tower)', s)

    def test_physics_bounds_are_never_discarded_or_approximated(self):
        s = EDITOR.read_text()
        for token in (
            'equivalent.sharedMesh = c.originalCollider.sharedMesh;',
            'equivalent.sharedMaterial = c.originalCollider.sharedMaterial;',
            'equivalent.cookingOptions = c.originalCollider.cookingOptions;',
            'equivalent.contactOffset = c.originalCollider.contactOffset;',
            'obstacleSlot.objectReferenceValue = equivalent;',
            'UnityEngine.Object.DestroyImmediate(c.originalCollider);',
        ):
            self.assertIn(token, s)
        self.assertLess(s.index('obstacleSlot.objectReferenceValue = equivalent;'),
                        s.index('UnityEngine.Object.DestroyImmediate(c.originalCollider);'))

    def test_last_lod_is_restored_with_no_render_quality_tuning(self):
        s = EDITOR.read_text()
        p = RUNTIME.read_text()
        self.assertIn('LOD[] lods = group.GetLODs();', s)
        self.assertIn('GameObject silhouette = UnityEngine.Object.Instantiate(', s)
        self.assertIn('silhouetteRenderers', p)
        self.assertIn('SceneManager.sceneLoaded += HandleSceneLoaded;', p)
        self.assertIn('SceneManager.sceneUnloaded += HandleSceneUnloaded;', p)
        self.assertIn('ShowSilhouette(!scene.IsValid() || !scene.isLoaded);', p)
        self.assertNotIn('void Update()', p)
        for forbidden in ('QualitySettings.', 'TerrainData.', 'renderScale',
                          'treeDistance', 'allowSceneActivation = false'):
            self.assertNotIn(forbidden, s + p)


if __name__ == "__main__":
    unittest.main(verbosity=2)
