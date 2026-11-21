using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aaris.MapReview
{
    /// <summary>
    /// Preserves the authored last-LOD silhouette while its full-detail
    /// scene is not resident. The original collider stays in the persistent
    /// world on a separately indexed, mesh-identical physics proxy.
    /// No per-frame polling, reflection, allocation or quality downgrade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AarisChunkFallbackVisibility : MonoBehaviour
    {
        [SerializeField] private string chunkScenePath;
        [SerializeField] private Renderer[] silhouetteRenderers = new Renderer[0];

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            Reconcile();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            // Disabling the optional manager must not make distant geometry
            // disappear if a caller switches streaming modes.
            ShowSilhouette(true);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == chunkScenePath) Reconcile();
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene.path == chunkScenePath) Reconcile();
        }

        private void Reconcile()
        {
            if (string.IsNullOrEmpty(chunkScenePath))
            {
                ShowSilhouette(true);
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(chunkScenePath);
            ShowSilhouette(!scene.IsValid() || !scene.isLoaded);
        }

        private void ShowSilhouette(bool visible)
        {
            if (silhouetteRenderers == null) return;
            for (int i = 0; i < silhouetteRenderers.Length; i++)
            {
                Renderer renderer = silhouetteRenderers[i];
                if (renderer != null && renderer.enabled != visible)
                    renderer.enabled = visible;
            }
        }
    }
}
