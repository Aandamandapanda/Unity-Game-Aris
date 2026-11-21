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
        // The original project's serialized Collider.m_Mesh identity. A
        // changed source requires re-review before the pilot can mutate assets.
        private const string OriginalColliderGuid =
            "6d0fe0e15b4c57e9ba9d921e437cc280";
        private const long OriginalColliderLocalId = 4300000L;

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
            string sourceGuid;
            long sourceLocalId;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                      collider.sharedMesh, out sourceGuid, out sourceLocalId) &&
                  sourceGuid == OriginalColliderGuid &&
                  sourceLocalId == OriginalColliderLocalId,
                  "authored-collision-mesh-changed-before-migration");
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

        private static string BackupBeforeAssetMutation()
        {
            const string buildSettings = "ProjectSettings/EditorBuildSettings.asset";
            Check(File.Exists(MainPath) && File.Exists(buildSettings),
                  "snapshot-source-missing");
            // A native Unity scene save changes fileIDs and references. Retain
            // exact original bytes under /tmp so an unexpected import/scene
            // error can be investigated and rolled back explicitly, without
            // overwriting unrelated dirty work inside the live repository.
            string directory = Path.Combine(Path.GetTempPath(),
                "aaris-forestlookout-backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") +
                "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string originalCopy = Path.Combine(directory, "TerrainDemoScene.unity");
            string settingsCopy = Path.Combine(directory, "EditorBuildSettings.asset");
            File.Copy(MainPath, originalCopy);
            File.Copy(buildSettings, settingsCopy);
            Check(new FileInfo(originalCopy).Length == new FileInfo(MainPath).Length &&
                  new FileInfo(settingsCopy).Length ==
                    new FileInfo(buildSettings).Length,
                  "snapshot-copy-incomplete");
            File.WriteAllText(Path.Combine(directory, "RESTORE_MANUALLY.txt"),
                "These are exact pre-migration Unity files. Do not overwrite the live " +
                "workspace automatically. Stop Unity and reconcile other agents' " +
                "changes before selectively restoring either file." + Environment.NewLine);
            return directory;
        }

        [MenuItem("Aaris/World/Apply ForestLookout Additive Pilot")]
        public static void Apply()
        {
            Check(Environment.GetEnvironmentVariable("AARIS_WORLD_PARTITION_APPLY") ==
                  "1", "explicit-environment-gate-required");
            Candidate c = Inspect();
            string recoveryFolder = BackupBeforeAssetMutation();
            Debug.Log("AARIS_FORESTLOOKOUT_PILOT_PREMIGRATION_BACKUP " + recoveryFolder);
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
            equivalent.includeLayers = c.originalCollider.includeLayers;
            equivalent.excludeLayers = c.originalCollider.excludeLayers;
            equivalent.layerOverridePriority = c.originalCollider.layerOverridePriority;
            equivalent.providesContacts = c.originalCollider.providesContacts;
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

        // Batchmode entrypoint: one Editor process owns the entire pilot
        // migration + asset reopen verification. No second Unity process can
        // race to import a half-authored world between Apply and Verify.
        [MenuItem("Aaris/World/Apply and Verify ForestLookout Additive Pilot")]
        public static void ApplyAndVerify()
        {
            Apply();
            VerifyAuthoredPilot();
            Debug.Log("AARIS_FORESTLOOKOUT_PILOT_APPLY_VERIFY_COMPLETE");
        }

        [MenuItem("Aaris/World/Verify ForestLookout Additive Pilot")]
        public static void VerifyAuthoredPilot()
        {
            Check(File.Exists(ChunkPath), "pilot-scene-not-saved");
            Scene bootstrap = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
            GameObject district = GameObject.Find("ForestLookout");
            GameObject player = GameObject.Find("AarisPlayer");
            GameObject mobile = GameObject.Find("MobileRuntime");
            Check(district != null && mobile != null && player != null,
                  "persistent-world-missing");
            Transform root = district.transform.Find("__AarisForestLookout_PersistentCover");
            Check(root != null && root.gameObject.scene == bootstrap,
                  "persistent-cover-missing");
            MeshCollider proxy = root.GetComponent<MeshCollider>();
            var fallback = root.GetComponent<AarisChunkFallbackVisibility>();
            Check(proxy != null && proxy.sharedMesh != null && proxy.enabled &&
                  fallback != null && !proxy.convex && !proxy.isTrigger,
                  "persistent-physics-and-far-lod-missing");
            var fallbackData = new SerializedObject(fallback);
            Check(fallbackData.FindProperty("chunkScenePath").stringValue == ChunkPath,
                  "far-silhouette-scene-path-mismatch");
            var farData = fallbackData.FindProperty("silhouetteRenderers");
            Check(farData.arraySize == 1 &&
                  farData.GetArrayElementAtIndex(0).objectReferenceValue != null,
                  "far-mesh-reference-missing");

            var nearby = mobile.GetComponent<AarisNearbyColliders>();
            Check(nearby != null, "nearby-collider-index-missing");
            var nearbyData = new SerializedObject(nearby);
            var obstacles = nearbyData.FindProperty("obstacles");
            int matches = 0;
            for (int i = 0; i < obstacles.arraySize; i++)
                if (obstacles.GetArrayElementAtIndex(i).objectReferenceValue == proxy)
                    matches++;
            Check(matches == 1, "physical-proxy-not-rebound");
            var streamer = mobile.GetComponent<AarisAdditiveChunkStreamer>();
            Check(streamer != null, "additive-streamer-not-serialized");
            var streamerData = new SerializedObject(streamer);
            var chunks = streamerData.FindProperty("chunks");
            Check(chunks.arraySize == 1 &&
                  chunks.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("scenePath").stringValue == ChunkPath &&
                  streamerData.FindProperty("player").objectReferenceValue ==
                    player.transform, "additive-authority-not-wired");

            int buildSceneMatches = 0;
            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path == ChunkPath && entry.enabled) buildSceneMatches++;
            Check(buildSceneMatches == 1, "additive-scene-build-configuration");

            Scene cell = EditorSceneManager.OpenScene(ChunkPath, OpenSceneMode.Additive);
            GameObject originalTower = GameObject.Find("ForestLookout_watchtower_01");
            Check(cell.IsValid() && cell.isLoaded && originalTower != null &&
                  originalTower.scene == cell && originalTower.transform.parent == null,
                  "authored-tower-not-in-cell");
            Check(originalTower.GetComponent<MeshCollider>() == null,
                  "duplicated-mesh-collision");
            LODGroup authoredLod = originalTower.GetComponent<LODGroup>();
            Check(authoredLod != null && authoredLod.GetLODs().Length >= 2,
                  "authored-full-detail-lod-dropped");
            // The authored visual MeshFilter may deliberately differ from the
            // simplified physical MeshCollider mesh. Never compare those two
            // unrelated assets: check the collider's actual serialized GUID and
            // local subasset ID from the original persistent scene instead.
            string colliderGuid;
            long colliderLocalId;
            Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                      proxy.sharedMesh, out colliderGuid, out colliderLocalId) &&
                  colliderGuid == OriginalColliderGuid &&
                  colliderLocalId == OriginalColliderLocalId,
                  "persistent-collider-source-mesh-identity-changed");
            MeshFilter originalMesh = originalTower.GetComponent<MeshFilter>();
            Check(originalMesh != null && originalMesh.sharedMesh != null,
                  "authored-tower-visual-mesh-missing");
            MeshFilter lastLodMesh = authoredLod.GetLODs()[
                authoredLod.GetLODs().Length - 1].renderers[0]
                    .GetComponent<MeshFilter>();
            var proxyRenderer = farData.GetArrayElementAtIndex(0)
                .objectReferenceValue as MeshRenderer;
            Check(proxyRenderer != null &&
                  proxyRenderer.GetComponent<MeshFilter>().sharedMesh ==
                    lastLodMesh.sharedMesh,
                  "persistent-silhouette-mesh-not-identical");
            // Native mesh identity alone cannot prove that users will see the
            // same authored landmark. Keep materials, world pose and uniform
            // / nonuniform scale aligned with the original last LOD renderer.
            Renderer authoredFar = authoredLod.GetLODs()[
                authoredLod.GetLODs().Length - 1].renderers[0];
            Check(Vector3.Distance(root.position,
                      originalTower.transform.position) < 0.001f &&
                  Quaternion.Angle(root.rotation,
                      originalTower.transform.rotation) < 0.01f &&
                  Vector3.Distance(root.lossyScale,
                      originalTower.transform.lossyScale) < 0.001f,
                  "persistent-proxy-world-pose-drift");
            Check(Vector3.Distance(proxyRenderer.transform.position,
                      authoredFar.transform.position) < 0.001f &&
                  Quaternion.Angle(proxyRenderer.transform.rotation,
                      authoredFar.transform.rotation) < 0.01f &&
                  Vector3.Distance(proxyRenderer.transform.lossyScale,
                      authoredFar.transform.lossyScale) < 0.001f,
                  "last-lod-silhouette-world-pose-drift");
            Material[] proxyMaterials = proxyRenderer.sharedMaterials;
            Material[] authoredMaterials = authoredFar.sharedMaterials;
            Check(proxyMaterials.Length == authoredMaterials.Length,
                  "last-lod-material-slot-count-changed");
            for (int materialIndex = 0;
                 materialIndex < authoredMaterials.Length; materialIndex++)
                Check(proxyMaterials[materialIndex] == authoredMaterials[materialIndex],
                      "last-lod-material-identity-changed");
            EditorSceneManager.CloseScene(cell, true);
            Check(bootstrap.IsValid(), "persistent-scene-closed");
            Debug.Log("AARIS_FORESTLOOKOUT_PILOT_VERIFY_PASS " +
                "exact_colliders=1 original_lods=1 far_silhouettes=1 " +
                "additive_build_scenes=1");
        }
    }
}
#endif
