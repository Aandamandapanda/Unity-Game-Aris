#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aaris.MapReview.Editor
{
    /// <summary>
    /// Converts the static, single-mesh-collider / authored-LOD buildings in
    /// twelve settlement districts into native additive Unity scene assets.
    /// Leaves exact original collision meshes and last-LOD silhouettes in the
    /// persistent island. Roads, runways, grass, rivers, terrain and interactive
    /// systems are never moved. Fails closed on unfamiliar hierarchy/components.
    ///
    /// Run only on an isolated project clone and opt in with
    /// AARIS_WORLD_PARTITION_APPLY=1. No runtime hierarchy cloning or SetActive.
    /// </summary>
    public static class AarisDistrictWorldPartitionBatch
    {
        private const string MainScene = "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity";
        private const string ChunkFolder = "Assets/AarisWorld/Scenes/Chunks/";
        private const string Prefix = "Aaris_District_";
        private static readonly string[] Districts = {
            "CanalMarket", "ClockTowerTown", "CoastalHamlet", "EasternPort",
            "FreightDistrict", "HighlandObservatory", "LakesideHamlet",
            "NorthernRidgeTown", "OldVillage", "SoutheastLighthouse",
            "SouthernAirfield", "ValleySettlement"
        };

        private sealed class Building
        {
            internal Transform source;
            internal MeshCollider physics;
            internal Renderer farRenderer;
            internal int obstacleIndex;
        }

        private sealed class District
        {
            internal string name, path;
            internal Transform persistentRoot;
            internal readonly List<Building> buildings = new List<Building>();
            internal Bounds worldBounds;
        }

        private static void Check(bool valid, string failure)
        {
            if (!valid) throw new InvalidOperationException(
                "AARIS_DISTRICT_CHUNK_UNSAFE " + failure);
        }

        private static string PathFor(string name)
        {
            return ChunkFolder + Prefix + name + "_Detail.unity";
        }

        private static List<District> Inspect(out Scene bootstrap,
                                              out GameObject mobile,
                                              out GameObject player,
                                              out AarisNearbyColliders nearby,
                                              out AarisAdditiveChunkStreamer streamer)
        {
            Check(File.Exists(MainScene), "main-scene-missing");
            bootstrap = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            Check(bootstrap.IsValid() && bootstrap.isLoaded, "main-scene-invalid");
            mobile = GameObject.Find("MobileRuntime");
            player = GameObject.Find("AarisPlayer");
            Check(mobile != null && player != null, "runtime-player-missing");
            nearby = mobile.GetComponent<AarisNearbyColliders>();
            streamer = mobile.GetComponent<AarisAdditiveChunkStreamer>();
            Check(nearby != null && streamer != null, "native-physics-streamer-missing");
            var serialized = new SerializedObject(nearby);
            SerializedProperty obstacles = serialized.FindProperty("obstacles");
            Check(obstacles != null && obstacles.isArray, "physics-index-invalid");
            var existing = new SerializedObject(streamer);
            var cells = existing.FindProperty("chunks");
            Check(cells != null && cells.isArray && cells.arraySize >= 1,
                  "forestlookout-chunk-must-remain");
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < cells.arraySize; i++)
                seenPaths.Add(cells.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("scenePath").stringValue);
            var results = new List<District>(Districts.Length);
            var seenColliders = new HashSet<MeshCollider>();
            foreach (string name in Districts)
            {
                string chunk = PathFor(name);
                Check(!File.Exists(chunk) && !seenPaths.Contains(chunk),
                      "chunk-already-authored-" + name);
                var root = GameObject.Find(name);
                Check(root != null && root.scene == bootstrap,
                      "district-root-missing-" + name);
                var district = new District {
                    name = name, path = chunk, persistentRoot = root.transform
                };
                bool firstBounds = true;
                foreach (Transform child in root.transform)
                {
                    var collider = child.GetComponent<MeshCollider>();
                    var lod = child.GetComponent<LODGroup>();
                    if (collider == null && lod == null)
                    {
                        // Airfield runways and ground markings stay persistent.
                        Check(name == "SouthernAirfield" &&
                              child.GetComponentsInChildren<Collider>(true).Length == 0 &&
                              child.GetComponentsInChildren<LODGroup>(true).Length == 0,
                              "unsupported-unpartitioned-geometry-" + child.name);
                        continue;
                    }
                    Check(collider != null && lod != null &&
                          collider.sharedMesh != null && !collider.convex &&
                          !collider.isTrigger, "collider-or-lod-" + child.name);
                    foreach (Component component in child.GetComponentsInChildren<Component>(true))
                        Check(component != null &&
                              (component is Transform || component is MeshFilter ||
                               component is MeshRenderer || component is MeshCollider ||
                               component is LODGroup), "unexpected-gameplay-component-" + child.name);
                    Check(child.GetComponentsInChildren<Collider>(true).Length == 1,
                          "more-than-one-physics-shape-" + child.name);
                    LOD[] levels = lod.GetLODs();
                    Check(levels != null && levels.Length >= 2,
                          "lod-levels-missing-" + child.name);
                    Renderer[] renderers = levels[levels.Length - 1].renderers;
                    Check(renderers != null && renderers.Length == 1 &&
                          renderers[0] != null && renderers[0] is MeshRenderer &&
                          renderers[0].transform.parent == child &&
                          renderers[0].GetComponent<MeshFilter>() != null &&
                          renderers[0].GetComponent<MeshFilter>().sharedMesh != null,
                          "far-lod-must-be-original-mesh-" + child.name);
                    int found = -1, matches = 0;
                    for (int i = 0; i < obstacles.arraySize; i++)
                    {
                        if (obstacles.GetArrayElementAtIndex(i).objectReferenceValue != collider)
                            continue;
                        found = i;
                        matches++;
                    }
                    Check(matches == 1 && seenColliders.Add(collider),
                          "physics-index-not-one-to-one-" + child.name);
                    var building = new Building {
                        source = child, physics = collider,
                        farRenderer = renderers[0], obstacleIndex = found
                    };
                    district.buildings.Add(building);
                    // True authored mesh bounds, never guessed radial cells.
                    foreach (Renderer visual in child.GetComponentsInChildren<Renderer>(true))
                    {
                        if (visual == null) continue;
                        if (firstBounds) { district.worldBounds = visual.bounds; firstBounds = false; }
                        else district.worldBounds.Encapsulate(visual.bounds);
                    }
                }
                Check(district.buildings.Count > 0 && !firstBounds,
                      "no-authored-buildings-" + name);
                results.Add(district);
            }
            return results;
        }

        [MenuItem("Aaris/World/Probe Native District Chunks (12)")]
        public static void Probe()
        {
            Scene bootstrap; GameObject mobile, player;
            AarisNearbyColliders nearby; AarisAdditiveChunkStreamer streamer;
            List<District> districts = Inspect(out bootstrap, out mobile, out player,
                                               out nearby, out streamer);
            int buildings = 0;
            foreach (District district in districts)
            {
                buildings += district.buildings.Count;
                Debug.Log("AARIS_DISTRICT_PROBE " + district.name +
                          " objects=" + district.buildings.Count +
                          " bounds=" + district.worldBounds);
            }
            Debug.Log("AARIS_DISTRICT_PROBE_PASS districts=" + districts.Count +
                      " buildings=" + buildings);
        }

        private static string BackUpScene()
        {
            const string settings = "ProjectSettings/EditorBuildSettings.asset";
            Check(File.Exists(MainScene) && File.Exists(settings),
                  "scene-or-build-settings-missing");
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "aaris-district-pre-migration-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
                "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.Copy(MainScene, System.IO.Path.Combine(directory, "main.unity"));
            File.Copy(settings, System.IO.Path.Combine(directory, "EditorBuildSettings.asset"));
            return directory;
        }

        private static void CopyCollider(MeshCollider source, MeshCollider destination)
        {
            destination.sharedMesh = source.sharedMesh;
            destination.sharedMaterial = source.sharedMaterial;
            destination.cookingOptions = source.cookingOptions;
            destination.convex = source.convex;
            destination.isTrigger = source.isTrigger;
            destination.contactOffset = source.contactOffset;
            destination.includeLayers = source.includeLayers;
            destination.excludeLayers = source.excludeLayers;
            destination.layerOverridePriority = source.layerOverridePriority;
            destination.providesContacts = source.providesContacts;
            destination.enabled = source.enabled;
        }

        [MenuItem("Aaris/World/Apply Native District Chunks (12)")]
        public static void Apply()
        {
            Check(Environment.GetEnvironmentVariable("AARIS_WORLD_PARTITION_APPLY") == "1",
                  "explicit-apply-gate-required");
            Scene bootstrap; GameObject mobile, player;
            AarisNearbyColliders nearby; AarisAdditiveChunkStreamer streamer;
            List<District> districts = Inspect(out bootstrap, out mobile, out player,
                                               out nearby, out streamer);
            string backup = BackUpScene();
            Debug.Log("AARIS_DISTRICT_PREMIGRATION_BACKUP " + backup);
            var obstacleData = new SerializedObject(nearby);
            var obstacleArray = obstacleData.FindProperty("obstacles");
            var chunkData = new SerializedObject(streamer);
            var cells = chunkData.FindProperty("chunks");
            int previousCellCount = cells.arraySize;
            cells.arraySize += districts.Count;
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int moved = 0;
            for (int d = 0; d < districts.Count; d++)
            {
                District district = districts[d];
                Scene cellScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                foreach (Building building in district.buildings)
                {
                    Transform original = building.source;
                    Vector3 originalWorldPosition = original.position;
                    Quaternion originalWorldRotation = original.rotation;
                    Vector3 originalLossyScale = original.lossyScale;
                    var cover = new GameObject("__AarisPersistentCover_" + original.name);
                    cover.transform.SetParent(district.persistentRoot, false);
                    cover.transform.localPosition = original.localPosition;
                    cover.transform.localRotation = original.localRotation;
                    cover.transform.localScale = original.localScale;
                    cover.layer = original.gameObject.layer;
                    GameObjectUtility.SetStaticEditorFlags(cover,
                        GameObjectUtility.GetStaticEditorFlags(original.gameObject));
                    var persistentCollider = cover.AddComponent<MeshCollider>();
                    CopyCollider(building.physics, persistentCollider);
                    var silhouette = UnityEngine.Object.Instantiate(building.farRenderer.gameObject);
                    silhouette.name = "__AarisFarSilhouette_" + original.name;
                    silhouette.transform.SetParent(cover.transform, false);
                    silhouette.transform.localPosition = building.farRenderer.transform.localPosition;
                    silhouette.transform.localRotation = building.farRenderer.transform.localRotation;
                    silhouette.transform.localScale = building.farRenderer.transform.localScale;
                    Renderer farRenderer = silhouette.GetComponent<Renderer>();
                    Check(farRenderer != null, "silhouette-clone-failed");
                    farRenderer.enabled = true;
                    var fallback = cover.AddComponent<AarisChunkFallbackVisibility>();
                    var fallbackData = new SerializedObject(fallback);
                    fallbackData.FindProperty("chunkScenePath").stringValue = district.path;
                    var renderers = fallbackData.FindProperty("silhouetteRenderers");
                    renderers.arraySize = 1;
                    renderers.GetArrayElementAtIndex(0).objectReferenceValue = farRenderer;
                    fallbackData.ApplyModifiedPropertiesWithoutUndo();

                    obstacleData.Update();
                    var slot = obstacleArray.GetArrayElementAtIndex(building.obstacleIndex);
                    Check(slot.objectReferenceValue == building.physics,
                          "physics-index-changed-while-migrating");
                    slot.objectReferenceValue = persistentCollider;
                    obstacleData.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(building.physics);
                    original.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(original.gameObject, cellScene);
                    Check(original.gameObject.scene == cellScene &&
                          Vector3.Distance(originalWorldPosition, original.position) < 0.001f &&
                          Quaternion.Angle(originalWorldRotation, original.rotation) < 0.01f &&
                          Vector3.Distance(originalLossyScale, original.lossyScale) < 0.001f,
                          "world-pose-drift-" + original.name);
                    moved++;
                }

                var cell = cells.GetArrayElementAtIndex(previousCellCount + d);
                cell.FindPropertyRelative("scenePath").stringValue = district.path;
                Bounds bounds = district.worldBounds;
                cell.FindPropertyRelative("center").vector2Value =
                    new Vector2(bounds.center.x, bounds.center.z);
                cell.FindPropertyRelative("halfExtents").vector2Value =
                    new Vector2(Mathf.Max(32f, bounds.extents.x + 1f),
                                Mathf.Max(32f, bounds.extents.z + 1f));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(district.path));
                Check(EditorSceneManager.SaveScene(cellScene, district.path),
                      "save-district-scene-" + district.name);
                SceneManager.SetActiveScene(bootstrap);
                EditorSceneManager.CloseScene(cellScene, true);
                buildScenes.Add(new EditorBuildSettingsScene(district.path, true));
            }
            chunkData.ApplyModifiedPropertiesWithoutUndo();
            Check(EditorSceneManager.SaveScene(bootstrap, MainScene),
                  "save-main-scene");
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AARIS_DISTRICT_APPLY_PASS districts=" + districts.Count +
                      " buildings=" + moved + " chunks=" + cells.arraySize);
        }

        [MenuItem("Aaris/World/Apply and Verify Native District Chunks (12)")]
        public static void ApplyAndVerify()
        {
            Apply();
            Verify();
            Debug.Log("AARIS_DISTRICT_APPLY_VERIFY_COMPLETE");
        }

        [MenuItem("Aaris/World/Verify Native District Chunks (12)")]
        public static void Verify()
        {
            Scene bootstrap = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            GameObject mobile = GameObject.Find("MobileRuntime");
            Check(bootstrap.IsValid() && mobile != null, "persistent-world-missing");
            var physics = mobile.GetComponent<AarisNearbyColliders>();
            var streamer = mobile.GetComponent<AarisAdditiveChunkStreamer>();
            Check(physics != null && streamer != null, "streamer-missing");
            var data = new SerializedObject(physics).FindProperty("obstacles");
            var chunkData = new SerializedObject(streamer).FindProperty("chunks");
            Check(data != null && chunkData != null &&
                  chunkData.arraySize >= Districts.Length + 1,
                  "chunk-manifest-not-written");
            int validated = 0;
            foreach (string name in Districts)
            {
                string path = PathFor(name);
                Check(File.Exists(path), "chunk-scene-missing-" + name);
                int enabledCount = 0;
                foreach (var scene in EditorBuildSettings.scenes)
                    if (scene.path == path && scene.enabled) enabledCount++;
                Check(enabledCount == 1, "build-settings-invalid-" + name);
                var district = GameObject.Find(name);
                Check(district != null && district.scene == bootstrap,
                      "persistent-district-missing-" + name);
                int cellsFound = 0;
                for (int i = 0; i < chunkData.arraySize; i++)
                    if (chunkData.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("scenePath").stringValue == path)
                        cellsFound++;
                Check(cellsFound == 1, "manifest-duplicate-" + name);
                Scene authored = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (GameObject root in authored.GetRootGameObjects())
                {
                    var lod = root.GetComponent<LODGroup>();
                    Check(lod != null && root.GetComponent<MeshCollider>() == null,
                          "authored-lod-or-collision-" + root.name);
                    Transform cover = district.transform.Find(
                        "__AarisPersistentCover_" + root.name);
                    Check(cover != null && cover.gameObject.scene == bootstrap,
                          "persistent-cover-not-found-" + root.name);
                    MeshCollider collider = cover.GetComponent<MeshCollider>();
                    Check(collider != null && collider.sharedMesh != null,
                          "mesh-collision-lost-" + root.name);
                    int matches = 0;
                    for (int i = 0; i < data.arraySize; i++)
                        if (data.GetArrayElementAtIndex(i).objectReferenceValue == collider)
                            matches++;
                    Check(matches == 1, "physics-index-not-rebound-" + root.name);
                    var fallback = cover.GetComponent<AarisChunkFallbackVisibility>();
                    Check(fallback != null, "fall-back-silhouette-missing");
                    var fallbackData = new SerializedObject(fallback);
                    Check(fallbackData.FindProperty("chunkScenePath").stringValue == path,
                          "far-chunk-path-mismatch");
                    var silhouette = fallbackData.FindProperty("silhouetteRenderers")
                        .GetArrayElementAtIndex(0).objectReferenceValue as MeshRenderer;
                    LOD[] levels = lod.GetLODs();
                    var originalFar = levels[levels.Length - 1].renderers[0] as MeshRenderer;
                    Check(silhouette != null && originalFar != null &&
                          silhouette.GetComponent<MeshFilter>().sharedMesh ==
                            originalFar.GetComponent<MeshFilter>().sharedMesh,
                          "far-mesh-identity-changed-" + root.name);
                    Check(Vector3.Distance(silhouette.transform.position,
                         originalFar.transform.position) < 0.001f &&
                          Quaternion.Angle(silhouette.transform.rotation,
                         originalFar.transform.rotation) < 0.01f &&
                          Vector3.Distance(silhouette.transform.lossyScale,
                         originalFar.transform.lossyScale) < 0.001f,
                          "far-silhouette-world-pose-drift-" + root.name);
                    Material[] originals = originalFar.sharedMaterials;
                    Material[] copies = silhouette.sharedMaterials;
                    Check(originals.Length == copies.Length,
                          "far-material-slot-mismatch-" + root.name);
                    for (int i = 0; i < originals.Length; i++)
                        Check(originals[i] == copies[i],
                              "far-material-changed-" + root.name);
                    validated++;
                }
                EditorSceneManager.CloseScene(authored, true);
            }
            Debug.Log("AARIS_DISTRICT_VERIFY_PASS districts=" + Districts.Length +
                      " exact_collision_and_far_lod=" + validated);
        }
    }
}
#endif
