#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aaris.MapReview.Editor
{
    /// <summary>
    /// Conservative single-landmark additive streaming pilot for the native
    /// Unity Editor. Dry-run by default; Apply requires a private environment
    /// opt-in. Never partitions roads, terrain, buildings with unknown scripts,
    /// dynamic vehicle spawn points or vegetation. Uses existing authored meshes.
    /// </summary>
    public static class AarisForestLookoutPartitionPilot
    {
        private const string MainPath =
            "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity";
        private const string ChunkPath =
            "Assets/AarisWorld/Scenes/Chunks/Aaris_ForestLookout_Detail.unity";

        private sealed class Candidate
        {
            internal Scene bootstrap;
            internal GameObject district, tower, mobile;
            internal MeshCollider originalCollider;
            internal LODGroup lod;
            internal AarisNearbyColliders grid;
            internal SerializedObject gridSerialized;
            internal Renderer lastLod;
            internal int colliderArrayIndex;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("AARIS_WORLD_PILOT_UNSAFE " + message);
        }

        private static Candidate Inspect()
        {
            Check(File.Exists(MainPath), "persistent-scene-missing");
            Check(!File.Exists(ChunkPath), "pilot-scene-already-created");
            Scene original = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
            Check(original.IsValid() && original.isLoaded, "scene-open");
            GameObject district = GameObject.Find("ForestLookout");
            GameObject tower = GameObject.Find("ForestLookout_watchtower_01");
            GameObject mobile = GameObject.Find("MobileRuntime");
            GameObject player = GameObject.Find("AarisPlayer");
            Check(district != null && tower != null &&
                  mobile != null && player != null, "known-world-hierarchy");
            Check(tower.transform.parent == district.transform &&
                  district.scene == original && tower.scene == original,
                  "tower-not-direct-child");
            Check(mobile.GetComponent<AarisAdditiveChunkStreamer>() == null,
                  "duplicate-streamer");
            MeshCollider collider = tower.GetComponent<MeshCollider>();
            LODGroup group = tower.GetComponent<LODGroup>();
            Check(collider != null && collider.enabled && !collider.isTrigger &&
                  collider.sharedMesh != null && !collider.convex,
                  "exact-static-mesh-collider-required");
            Check(group != null, "missing-authored-lod-group");
            LOD[] lods = group.GetLODs();
            Check(lods != null && lods.Length >= 2, "lod-levels-missing");
            Renderer[] far = lods[lods.Length - 1].renderers;
            Check(far != null && far.Length == 1 && far[0] != null,
                  "one-last-lod-renderer-required");
            Renderer distant = far[0];
            Check(distant.transform.parent == tower.transform &&
                  distant is MeshRenderer &&
                  distant.GetComponent<MeshFilter>() != null &&
                  distant.GetComponent<MeshFilter>().sharedMesh != null,
                  "last-lod-must-be-direct-authored-mesh");
            // Reject gameplay components/collider children until the editor
            // migration explicitly supports their state and external links.
            foreach (Component c in tower.GetComponentsInChildren<Component>(true))
            {
                Check(c != null &&
                      (c is Transform || c is MeshRenderer || c is MeshFilter ||
                       c is MeshCollider || c is LODGroup),
                      "unsupported-component-on-tower");
                if (c is Collider)
                    Check(c == collider, "additional-collision-shape");
            }
            var grid = mobile.GetComponent<AarisNearbyColliders>();
            Check(grid != null, "persistent-physics-streamer-missing");
            var serialized = new SerializedObject(grid);
            SerializedProperty obstacles = serialized.FindProperty("obstacles");
            Check(obstacles != null && obstacles.isArray, "collider-array-missing");
            int matches = 0, foundIndex = -1;
            for (int i = 0; i < obstacles.arraySize; i++)
            {
                if (obstacles.GetArrayElementAtIndex(i).objectReferenceValue != collider)
                    continue;
                matches++;
                foundIndex = i;
            }
            Check(matches == 1, "one-physical-collider-reference-required");
            Check(district.transform.childCount == 1 && tower.transform.childCount == 2,
                  "pilot-content-changed");
            Check(player.transform != null && player.scene == original,
                  "player-not-persistent");
            return new Candidate {
                bootstrap = original, district = district, tower = tower,
                mobile = mobile, originalCollider = collider, lod = group,
                lastLod = distant, grid = grid,
                gridSerialized = serialized, colliderArrayIndex = foundIndex
            };
        }

        [MenuItem("Aaris/World/Probe ForestLookout Additive Pilot")]
        public static void Probe()
        {
            Candidate c = Inspect();
            var renderer = c.lastLod.GetComponent<MeshRenderer>();
            Debug.Log("AARIS_FORESTLOOKOUT_PILOT_PROBE_PASS " +
                "world_scene=" + c.bootstrap.path +
                " collider_mesh=" + c.originalCollider.sharedMesh.name +
                " lod_levels=" + c.lod.GetLODs().Length +
                " last_lod_mesh=" + c.lastLod.GetComponent<MeshFilter>().sharedMesh.name +
                " far_materials=" + renderer.sharedMaterials.Length +
                " collider_slot=" + c.colliderArrayIndex +
                " tower_at=" + c.tower.transform.position);
        }

        [MenuItem("Aaris/World/Apply ForestLookout Additive Pilot")]
        public static void Apply()
        {
            Check(Environment.GetEnvironmentVariable("AARIS_WORLD_PARTITION_APPLY") ==
                  "1", "explicit-environment-gate-required");
            Candidate c = Inspect();
            Transform source = c.tower.transform;
            Vector3 originalWorldPosition = source.position;
            var district = c.district.transform;

            // Persistent native collision proxy is mesh-identical and uses
            // the same local pose/scale as the original 55m collider stream.
            GameObject physical = new GameObject("__AarisForestLookout_PersistentCover");
            physical.transform.SetParent(district, false);
            physical.transform.localPosition = source.localPosition;
            physical.transform.localRotation = source.localRotation;
            physical.transform.localScale = source.localScale;
            physical.layer = c.tower.layer;
            MeshCollider equivalent = physical.AddComponent<MeshCollider>();
            equivalent.sharedMesh = c.originalCollider.sharedMesh;
            equivalent.sharedMaterial = c.originalCollider.sharedMaterial;
            equivalent.cookingOptions = c.originalCollider.cookingOptions;
            equivalent.convex = c.originalCollider.convex;
            equivalent.isTrigger = c.originalCollider.isTrigger;
            equivalent.contactOffset = c.originalCollider.contactOffset;
            equivalent.enabled = c.originalCollider.enabled;

            // Only the original authored last-LOD mesh/material. Do not
            // replace the near camera view with a lower-poly substitute.
            GameObject silhouette = UnityEngine.Object.Instantiate(
                c.lastLod.gameObject);
            silhouette.name = "__AarisForestLookout_FarSilhouette";
            silhouette.transform.SetParent(physical.transform, false);
            silhouette.transform.localPosition = c.lastLod.transform.localPosition;
            silhouette.transform.localRotation = c.lastLod.transform.localRotation;
            silhouette.transform.localScale = c.lastLod.transform.localScale;
            var farRenderer = silhouette.GetComponent<Renderer>();
            Check(farRenderer != null, "far-renderer-clone-failed");
            farRenderer.enabled = true;

            var fallback = physical.AddComponent<AarisChunkFallbackVisibility>();
            var fallbackSerialized = new SerializedObject(fallback);
            fallbackSerialized.FindProperty("chunkScenePath").stringValue = ChunkPath;
            var renderers = fallbackSerialized.FindProperty("silhouetteRenderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = farRenderer;
            fallbackSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Preserve the existing AarisNearbyColliders inspector authority.
            // Never duplicate live MeshColliders after an additive load.
            c.gridSerialized.Update();
            var obstacleSlot = c.gridSerialized.FindProperty("obstacles")
                .GetArrayElementAtIndex(c.colliderArrayIndex);
            Check(obstacleSlot.objectReferenceValue == c.originalCollider,
                  "collider-reference-raced");
            obstacleSlot.objectReferenceValue = equivalent;
            c.gridSerialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.DestroyImmediate(c.originalCollider);

            Directory.CreateDirectory(Path.GetDirectoryName(ChunkPath));
            Scene newCell = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            source.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(c.tower, newCell);
            Check(c.tower.scene == newCell &&
                  Vector3.Distance(originalWorldPosition, source.position) < 0.001f,
                  "world-transform-drift");

            var streamer = c.mobile.AddComponent<AarisAdditiveChunkStreamer>();
            var authored = new SerializedObject(streamer);
            authored.FindProperty("player").objectReferenceValue =
                GameObject.Find("AarisPlayer").transform;
            var cells = authored.FindProperty("chunks");
            cells.arraySize = 1;
            var cell = cells.GetArrayElementAtIndex(0);
            cell.FindPropertyRelative("scenePath").stringValue = ChunkPath;
            cell.FindPropertyRelative("center").vector2Value =
                new Vector2(originalWorldPosition.x, originalWorldPosition.z);
            cell.FindPropertyRelative("halfExtents").vector2Value =
                new Vector2(32f, 32f);
            authored.ApplyModifiedPropertiesWithoutUndo();

            var existingScenes = EditorBuildSettings.scenes;
            foreach (var s in existingScenes)
                Check(s.path != ChunkPath, "duplicate-build-scene");
            var newScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            Array.Copy(existingScenes, newScenes, existingScenes.Length);
            newScenes[newScenes.Length - 1] =
                new EditorBuildSettingsScene(ChunkPath, true);

            Check(EditorSceneManager.SaveScene(newCell, ChunkPath),
                  "save-additive-chunk-failed");
            Check(EditorSceneManager.SaveScene(c.bootstrap, MainPath),
                  "save-persistent-scene-failed");
            EditorBuildSettings.scenes = newScenes;
            SceneManager.SetActiveScene(c.bootstrap);
            EditorSceneManager.CloseScene(newCell, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AARIS_FORESTLOOKOUT_PILOT_APPLY_PASS " +
                "persistent_physics_proxy=1 last_lod_proxy=1 " +
                "streaming_cells=1 source_world_position=" + originalWorldPosition);
        }
    }
}
#endif
