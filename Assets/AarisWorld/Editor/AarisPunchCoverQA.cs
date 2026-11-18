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
    /// <summary>Real melee physics in temporary objects, without Play transitions or scene saves.</summary>
    public static class AarisPunchCoverQA
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);

        [MenuItem("Aaris/CI/Verify punch ownership and complete cover checks")]
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
                bool emptyBatchScene = Application.isBatchMode &&
                    string.IsNullOrEmpty(original.path) && original.rootCount == 0;
                if (!emptyBatchScene)
                {
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporary);
                }
                foreach (AarisBattleActions.PlayerStance stance in
                    Enum.GetValues(typeof(AarisBattleActions.PlayerStance)))
                    foreach (string kind in new[] { "open", "owner", "owner-wall", "sibling",
                        "sibling-wall", "nearest-hidden", "trigger", "ignored-layer",
                        "dense-owner", "dense-wall" })
                    {
                        bool passed = CheckPunch(kind, stance, out string detail);
                        report.AppendLine((passed ? "PASS " : "FAIL ") + kind + " stance=" + stance + " " + detail);
                        if (!passed) failures++;
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
            string folder = Path.Combine(Path.GetTempPath(), "aaris-punch-cover-qa");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static GameObject Box(Transform parent, Vector3 position, Vector3 size)
        {
            var obj = new GameObject("Temporary punch collider");
            obj.transform.SetParent(parent, false);
            obj.transform.position = Origin + position;
            obj.AddComponent<BoxCollider>().size = size;
            return obj;
        }

        private static AarisOfflineHealth Health(GameObject obj)
        {
            var health = obj.AddComponent<AarisOfflineHealth>();
            typeof(AarisOfflineHealth).GetField("current", Private).SetValue(health, 200f);
            return health;
        }

        private static bool CheckPunch(string kind, AarisBattleActions.PlayerStance stance, out string detail)
        {
            var root = new GameObject("Aaris temporary punch QA");
            try
            {
                float height = stance == AarisBattleActions.PlayerStance.Prone ? 0.4f :
                    stance == AarisBattleActions.PlayerStance.Crouching ? 0.73f : 1.15f;
                var owner = new GameObject("Punch owner");
                owner.transform.SetParent(root.transform, false);
                owner.transform.position = Origin;
                var combat = owner.AddComponent<AarisBattleActions>();
                var capsule = owner.GetComponent<CharacterController>();
                capsule.center = Vector3.up;
                capsule.height = 2f;
                capsule.radius = 0.3f;
                typeof(AarisBattleActions).GetField("stance", Private).SetValue(combat, stance);
                var target = Health(Box(root.transform, new Vector3(0f, height, 0.9f),
                    new Vector3(0.15f, 0.3f, 0.15f)));
                AarisOfflineHealth secondary = null;
                if (kind == "nearest-hidden")
                {
                    target.transform.position = Origin + new Vector3(-0.18f, height, 0.75f);
                    secondary = Health(Box(root.transform, new Vector3(0.33f, height, 1f),
                        new Vector3(0.15f, 0.3f, 0.15f)));
                    Box(root.transform, new Vector3(-0.09f, height, 0.38f),
                        new Vector3(0.12f, 0.5f, 0.04f));
                }
                if (kind == "owner" || kind == "owner-wall" ||
                    kind == "dense-owner" || kind == "dense-wall")
                {
                    int count = kind.StartsWith("dense", StringComparison.Ordinal) ? 64 : 1;
                    for (int i = 0; i < count; i++)
                        Box(owner.transform, new Vector3(0f, height, 0.02f + i * 0.005f),
                            new Vector3(0.1f, 0.2f, 0.002f));
                }
                if (kind == "sibling" || kind == "sibling-wall")
                    Box(target.transform, new Vector3(0f, height, 0.25f),
                        new Vector3(0.1f, 0.2f, 0.03f));
                bool blocked = kind.EndsWith("-wall", StringComparison.Ordinal) || kind == "dense-wall";
                if (blocked || kind == "trigger" || kind == "ignored-layer")
                {
                    var wall = Box(root.transform, new Vector3(0f, height, 0.55f),
                        new Vector3(0.5f, 0.5f, 0.06f));
                    if (kind == "trigger") wall.GetComponent<Collider>().isTrigger = true;
                    if (kind == "ignored-layer") wall.layer = 2;
                }
                Physics.SyncTransforms(); // Explicit QA setup, never a gameplay hot path.
                typeof(AarisBattleActions).GetMethod("Punch", Private).Invoke(combat, null);
                float expectedTarget = blocked || secondary != null ? 200f : 180f;
                detail = "target=" + target.CurrentHealth + " secondary=" +
                    (secondary != null ? secondary.CurrentHealth.ToString() : "none");
                return Mathf.Approximately(target.CurrentHealth, expectedTarget) &&
                    (secondary == null || Mathf.Approximately(secondary.CurrentHealth, 180f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
