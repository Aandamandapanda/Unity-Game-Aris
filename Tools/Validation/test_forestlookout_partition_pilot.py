"""Source contracts for opt-in native ForestLookout world-cell migration.

These do not replace a real Unity Editor compile, scene reopen or Android FPS QA.
"""
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Validation"))
from world_partition_readiness import WorldScene
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
        self.assertIn('persistent-collider-source-mesh-identity-changed', s)
        self.assertIn('"6d0fe0e15b4c57e9ba9d921e437cc280"', s)
        self.assertIn('OriginalColliderLocalId = 4300000L;', s)
        self.assertIn('sourceGuid == OriginalColliderGuid', s)
        self.assertIn('sourceLocalId == OriginalColliderLocalId', s)
        self.assertIn('authored-collision-mesh-changed-before-migration', s)
        self.assertIn('colliderGuid == OriginalColliderGuid', s)
        self.assertIn('colliderLocalId == OriginalColliderLocalId', s)
        self.assertNotIn('originalMesh.sharedMesh == proxy.sharedMesh', s)
        self.assertIn('ForestLookout_watchtower_01', s)
        self.assertIn('Aaris_ForestLookout_Detail.unity', s)
        self.assertIn('Check(district.transform.childCount == 1', s)
        self.assertIn('Check(matches == 1,', s)
        self.assertIn('EditorSceneManager.NewScene(', s)
        self.assertIn('SceneManager.MoveGameObjectToScene(c.tower, newCell);', s)
        self.assertNotIn('DestroyImmediate(c.tower)', s)

    def test_original_scene_has_one_safe_extractable_source_hierarchy(self):
        """Prevent a stale pilot from silently moving a changed landmark."""
        source = (ROOT / "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity")
        scene = WorldScene(source.read_text())
        district = scene.find_unique("ForestLookout")
        tower = scene.find_unique("ForestLookout_watchtower_01")
        self.assertEqual(scene.children[district], [tower])
        self.assertEqual(len(scene.children[tower]), 2)
        self.assertEqual(len(scene.descendants(district)), 4)
        self.assertEqual(
            scene.component_types[tower], {33, 23, 64, 205}
        )
        self.assertTrue(all(scene.component_types[child] == {33, 23}
                            for child in scene.children[tower]))
        outgoing, incoming = scene.semantic_cross_refs(scene.descendants(district))
        self.assertEqual(outgoing, set())
        self.assertEqual(len(incoming), 1)
        source_component, destination_component = next(iter(incoming))
        self.assertEqual(
            scene.names[scene.owner[source_component]], "MobileRuntime"
        )
        self.assertEqual(scene.blocks[source_component].kind, 114)
        self.assertEqual(scene.blocks[destination_component].kind, 64)
        collider_yaml = scene.blocks[destination_component].body
        self.assertIn("m_Mesh: {fileID: 4300000, "
                      "guid: 6d0fe0e15b4c57e9ba9d921e437cc280", collider_yaml)
        lod_components = [
            uid for uid, block in scene.blocks.items()
            if block.kind == 205 and scene.owner.get(uid) == tower
        ]
        self.assertEqual(len(lod_components), 1)
        self.assertEqual(
            scene.blocks[lod_components[0]].body.count("screenRelativeHeight:"), 3
        )

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
