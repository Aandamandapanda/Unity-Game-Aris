#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Aaris.MapReview.Editor
{
    /// <summary>Real PhysX shots in a disposable additive scene; no scene saves or Play transition.</summary>
    public static class AarisMuzzleCoverQA
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);

        [MenuItem("Aaris/CI/Verify muzzle cannot fire through close cover")]
        public static void Run()
        {
            var report = new StringBuilder("UNITY=" + Application.unityVersion + "\n");
            int failures = 0;
            Scene original = SceneManager.GetActiveScene();
            Scene temporary = default(Scene);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("EDITOR_BUSY");
                // A fresh batch project starts with an empty untitled scene;
                // Unity refuses an additive scene beside that initial scene.
                bool emptyBatchScene = Application.isBatchMode &&
                    string.IsNullOrEmpty(original.path) && original.rootCount == 0;
                if (!emptyBatchScene)
                {
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporary);
                }
                foreach (float height in new[] { 1.3f, 0.85f, 0.30f })
                {
                    foreach (string kind in new[] { "open", "inside", "beyond", "normal",
                        "side", "trigger", "ignored-layer", "owner", "dense-owner", "shotgun" })
                    {
                        bool passed = CheckShot(kind, height, out string detail);
                        report.AppendLine((passed ? "PASS " : "FAIL ") + kind + " height=" + height + " " + detail);
                        if (!passed) failures++;
                    }
                }
            }
            catch (Exception ex)
            {
                failures++;
                report.AppendLine("FAIL " + ex.GetType().Name + " " + ex.Message);
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (temporary.IsValid() && temporary.isLoaded)
                    EditorSceneManager.CloseScene(temporary, true);
            }
            report.AppendLine("RESULT=" + (failures == 0 ? "PASS" : "FAIL"));
            string folder = Path.Combine(Path.GetTempPath(), "aaris-muzzle-cover-qa");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static GameObject Box(Transform parent, Vector3 position, Vector3 scale)
        {
            var obj = new GameObject("QA collider");
            obj.transform.SetParent(parent, false);
            obj.transform.position = Origin + position;
            obj.AddComponent<BoxCollider>().size = scale;
            return obj;
        }

        private static AarisOfflineHealth Health(GameObject obj)
        {
            var health = obj.AddComponent<AarisOfflineHealth>();
            // Edit Mode does not invoke ordinary runtime Awake.
            typeof(AarisOfflineHealth).GetField("current", Private).SetValue(health, 200f);
            return health;
        }

        private static bool CheckShot(string kind, float height, out string detail)
        {
            var root = new GameObject("Aaris temporary muzzle QA");
            try
            {
                var owner = new GameObject("QA owner");
                owner.transform.SetParent(root.transform, false);
                // No stance component: the normal fallback muzzle is 1.3 m.
                // Shift the owner to exercise exactly the same path at all heights.
                owner.transform.position = Origin + Vector3.up * (height - 1.3f);
                var cameraObj = new GameObject("QA camera");
                cameraObj.transform.SetParent(root.transform, false);
                var camera = cameraObj.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = Origin + new Vector3(0.65f, height, 0.65f);
                Vector3 targetPoint = Origin + new Vector3(0f, height, 4f);
                camera.transform.LookAt(targetPoint);
                var target = Health(Box(root.transform, new Vector3(0f, height, 4f), Vector3.one * 0.5f));
                AarisOfflineHealth cover = null;
                bool blocked = kind == "inside" || kind == "beyond" || kind == "normal" ||
                    kind == "dense-owner" || kind == "shotgun";
                if (kind != "open" && kind != "owner")
                {
                    float z = kind == "normal" ? 1f : kind == "beyond" ? 0.1f : 0.24f;
                    var box = Box(root.transform, new Vector3(kind == "side" ? 1f : 0f, height, z),
                        new Vector3(0.8f, 0.8f, 0.06f));
                    if (kind == "trigger") box.GetComponent<Collider>().isTrigger = true;
                    if (kind == "ignored-layer") box.layer = 2; // Ignore Raycast.
                    cover = Health(box);
                }
                if (kind == "owner" || kind == "dense-owner")
                {
                    int count = kind == "dense-owner" ? 64 : 1;
                    for (int i = 0; i < count; i++)
                        Box(owner.transform, new Vector3(0f, height, 0.01f + i * 0.003f),
                            new Vector3(0.1f, 0.2f, 0.001f));
                }
                var gunObj = new GameObject("QA gun");
                gunObj.transform.SetParent(owner.transform, false);
                var gun = gunObj.AddComponent<AarisEquippedGun>();
                int pellets = kind == "shotgun" ? 8 : 1;
                var spec = new AarisWeaponSpec { id = "qa", category = "Rifle", rpm = 600,
                    magazineSize = 30, reserveAmmo = 60, damage = 10f, rangeMetres = 50f,
                    pellets = pellets, spreadDegrees = 0f, recoilPitchDegrees = 0f };
                // Exercise the real trigger, query and damage code without creating
                // runtime FX in Edit Mode. Audio is not part of this physics test.
                typeof(AarisEquippedGun).GetField("spec", Private).SetValue(gun, spec);
                typeof(AarisEquippedGun).GetField("magazine", Private).SetValue(gun, 30);
                Physics.SyncTransforms(); // Explicit QA setup only.
                bool fired = gun.TryFire(camera, owner);
                float expectedTarget = blocked ? 200f : 200f - 10f * pellets;
                float expectedCover = blocked ? 200f - 10f * pellets : 200f;
                detail = "target=" + target.CurrentHealth + " cover=" +
                    (cover != null ? cover.CurrentHealth.ToString() : "none") + " ammo=" + gun.Magazine;
                return fired && gun.Magazine == 29 &&
                    Mathf.Approximately(target.CurrentHealth, expectedTarget) &&
                    (cover == null || Mathf.Approximately(cover.CurrentHealth, expectedCover));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
