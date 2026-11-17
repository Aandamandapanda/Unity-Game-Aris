// Editor-only remedy for the broken URP terrain view on headless Linux GLCore.
// Never changes the saved TerrainData, original material assets, or Android rendering.
#if UNITY_EDITOR_LINUX
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Aaris.MapReview.Editor
{
    [InitializeOnLoad]
    internal static class AarisGlcoreTerrainPreview
    {
        private const string NativeShader = "Universal Render Pipeline/Terrain/Lit";
        private const string PreviewShader = "Hidden/Aaris/GlcoreTerrainPreview";

        private struct TerrainSnapshot
        {
            internal Material material;
            internal bool drawInstanced;
            internal Material preview;
        }

        private static readonly Dictionary<Terrain, TerrainSnapshot> Originals =
            new Dictionary<Terrain, TerrainSnapshot>();
        private static Material previewMaterial;
        private static bool refreshing;
        private static bool warned;
        private static double nextCheck;

        static AarisGlcoreTerrainPreview()
        {
            EditorApplication.update += Tick;
            EditorApplication.hierarchyChanged += Refresh;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.quitting += RestoreAll;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
            EditorApplication.delayCall += Refresh;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 4;
            Refresh();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode) { Refresh(); }
        private static void OnSceneSaved(Scene scene) { EditorApplication.delayCall += Refresh; }

        private static void OnSceneSaving(Scene scene, string path)
        {
            // No temporary material or instancing override is ever serialized.
            RestoreScene(scene);
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
                RestoreAll();
            else if (state == PlayModeStateChange.EnteredEditMode ||
                     state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            if (refreshing || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore)
            {
                RestoreAll();
                return;
            }

            Shader native = Shader.Find(NativeShader);
            if (native != null && native.isSupported)
            {
                RestoreAll();
                return;
            }

            Shader fallback = Shader.Find(PreviewShader);
            if (fallback == null || !fallback.isSupported)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("AARIS_GLCORE_TERRAIN_PREVIEW_SHADER_UNAVAILABLE");
                }
                return;
            }

            refreshing = true;
            try
            {
                if (previewMaterial == null)
                {
                    previewMaterial = new Material(fallback);
                    previewMaterial.name = "Aaris GLCore editor-only terrain preview";
                    previewMaterial.hideFlags = HideFlags.HideAndDontSave;
                }

                int count = 0;
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    if (terrain == null || !terrain.gameObject.scene.IsValid() ||
                        Originals.ContainsKey(terrain))
                        continue;

                    Material nativeMaterial = terrain.materialTemplate;
                    if (nativeMaterial == null || nativeMaterial.shader == null ||
                        nativeMaterial.shader.name != NativeShader)
                        continue;

                    // Each tile needs its own secondary control textures.
                    // Create one unsaved preview material per tile, never per frame.
                    Material local = new Material(previewMaterial)
                    {
                        name = "Aaris GLCore tile " + terrain.name,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    BindExtraLayers(local, terrain.terrainData);
                    Originals.Add(terrain, new TerrainSnapshot
                    {
                        material = nativeMaterial,
                        drawInstanced = terrain.drawInstanced,
                        preview = local
                    });
                    // GLCore's native Terrain Lit is unavailable: retain the native
                    // geometry, paint-control textures and layer images in a plain
                    // non-instanced editor view, without touching mobile quality.
                    terrain.drawInstanced = false;
                    terrain.materialTemplate = local;
                    count++;
                }
                if (count > 0)
                    Debug.Log("AARIS_GLCORE_TERRAIN_PREVIEW_ACTIVE tiles=" + count +
                        " (temporary, scene-save-safe, Android unchanged)");
            }
            finally { refreshing = false; }
        }

        // Terrain/Lit uses several splat passes. Bind them in one Linux
        // preview; Android and the serialized TerrainData stay unchanged.
        private static void BindExtraLayers(Material local, TerrainData data)
        {
            if (data == null) return;
            Texture2D[] controls = data.alphamapTextures;
            for (int i = 1; i < 3 && i < controls.Length; i++)
                if (controls[i] != null)
                    local.SetTexture("_Control" + i, controls[i]);
            TerrainLayer[] layers = data.terrainLayers;
            for (int i = 4; i < Mathf.Min(10, layers.Length); i++)
            {
                TerrainLayer layer = layers[i];
                if (layer == null || layer.diffuseTexture == null) continue;
                string property = "_Splat" + i;
                local.SetTexture(property, layer.diffuseTexture);
                float sx = Mathf.Max(0.01f, Mathf.Abs(layer.tileSize.x));
                float sy = Mathf.Max(0.01f, Mathf.Abs(layer.tileSize.y));
                local.SetTextureScale(property,
                    new Vector2(data.size.x / sx, data.size.z / sy));
                local.SetTextureOffset(property,
                    new Vector2(layer.tileOffset.x / sx, layer.tileOffset.y / sy));
            }
        }

        private static void RestoreScene(Scene scene)
        {
            if (refreshing || Originals.Count == 0)
                return;

            refreshing = true;
            try
            {
                var terrains = new List<Terrain>(Originals.Keys);
                foreach (Terrain terrain in terrains)
                {
                    if (terrain == null)
                    {
                        Originals.Remove(terrain);
                        continue;
                    }
                    if (terrain.gameObject.scene != scene)
                        continue;

                    TerrainSnapshot snapshot = Originals[terrain];
                    terrain.materialTemplate = snapshot.material;
                    terrain.drawInstanced = snapshot.drawInstanced;
                    if (snapshot.preview != null)
                        UnityEngine.Object.DestroyImmediate(snapshot.preview);
                    Originals.Remove(terrain);
                }
            }
            finally { refreshing = false; }
        }

        private static void RestoreAll()
        {
            if (refreshing || Originals.Count == 0)
                return;

            refreshing = true;
            try
            {
                var terrains = new List<Terrain>(Originals.Keys);
                foreach (Terrain terrain in terrains)
                {
                    if (terrain != null)
                    {
                        TerrainSnapshot snapshot = Originals[terrain];
                        terrain.materialTemplate = snapshot.material;
                        terrain.drawInstanced = snapshot.drawInstanced;
                        if (snapshot.preview != null)
                            UnityEngine.Object.DestroyImmediate(snapshot.preview);
                    }
                }
                Originals.Clear();
            }
            finally { refreshing = false; }
        }
    }
}
#endif
