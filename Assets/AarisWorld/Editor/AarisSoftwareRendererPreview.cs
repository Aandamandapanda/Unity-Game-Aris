using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aaris.MapReview.Editor
{
    /// <summary>
    /// Improves the Unity Codespace preview on Mesa's CPU renderer.
    /// Neither the map assets nor the Android URP profile are modified.
    /// </summary>
    [InitializeOnLoad]
    internal static class AarisSoftwareRendererPreview
    {
        private static RenderPipelineAsset originalQualityPipeline;
        private static UniversalRenderPipelineAsset temporaryPipeline;

        static AarisSoftwareRendererPreview()
        {
            if (Application.isBatchMode) return;
            EditorApplication.update += ApplyWhenReady;
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            EditorApplication.quitting += Restore;
        }

        private static bool IsSoftwareRenderer(string device)
        {
            return !string.IsNullOrEmpty(device) &&
                (device.IndexOf("llvmpipe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 device.IndexOf("softpipe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 device.IndexOf("SwiftShader", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void ApplyWhenReady()
        {
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                (EditorApplication.isPlayingOrWillChangePlaymode &&
                !EditorApplication.isPlaying)) return;

            string renderer = SystemInfo.graphicsDeviceName;
            if (string.IsNullOrEmpty(renderer)) return;
            if (!IsSoftwareRenderer(renderer))
            {
                EditorApplication.update -= ApplyWhenReady;
                return;
            }

            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (current == null) return;

            // This clone is in memory only; do not modify the shared URP asset.
            originalQualityPipeline = QualitySettings.renderPipeline;
            var local = UnityEngine.Object.Instantiate(current);
            local.name = "Aaris Software Preview (temporary; not saved)";
            local.hideFlags = HideFlags.HideAndDontSave;
            local.supportsHDR = false;
            local.msaaSampleCount = 1;
            // llvmpipe draws the full game on CPU. The extra ~22% pixel
            // reduction versus 85% scale makes Codespace Play Mode usable
            // without degrading authored world textures or Android output.
            local.renderScale = Mathf.Min(local.renderScale, 0.75f);
            local.shadowDistance = Mathf.Min(local.shadowDistance, 80f);
            local.mainLightShadowmapResolution =
                Mathf.Min(local.mainLightShadowmapResolution, 1024);
            local.shadowCascadeCount = 1;

            temporaryPipeline = local;
            QualitySettings.renderPipeline = local;
            EditorApplication.update -= ApplyWhenReady;
            SceneView.RepaintAll();
            Debug.Log("AARIS_SOFTWARE_PREVIEW_ACTIVE: software OpenGL " + renderer +
                "; temporary LDR, 75% scale, 1x MSAA, 80m shadows and 1024 shadow atlas. " +
                "Saved map and Android graphics remain unchanged.");
        }

        internal static void SuspendForBuild()
        {
            // Builds use the original saved quality asset, never the preview clone.
            Restore();
            EditorApplication.update -= ApplyWhenReady;
            EditorApplication.update += ApplyWhenReady;
        }

        private static void Restore()
        {
            if (temporaryPipeline == null) return;

            if (QualitySettings.renderPipeline == temporaryPipeline)
                QualitySettings.renderPipeline = originalQualityPipeline;

            UnityEngine.Object.DestroyImmediate(temporaryPipeline);
            temporaryPipeline = null;
            originalQualityPipeline = null;
        }
    }

    internal sealed class AarisSoftwarePreviewBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            AarisSoftwareRendererPreview.SuspendForBuild();
        }
    }
}
