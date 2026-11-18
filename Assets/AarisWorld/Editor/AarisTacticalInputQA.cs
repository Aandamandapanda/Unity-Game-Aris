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
    /// <summary>Temporary control-state audit; no scene saves or Play transition.</summary>
    public static class AarisTacticalInputQA
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        private static T Get<T>(object target, string field) =>
            (T)target.GetType().GetField(field, Private).GetValue(target);
        private static void Call(object target, string method, params object[] args)
        {
            // A missing lifecycle callback is the baseline behavior under test.
            target.GetType().GetMethod(method, Private)?.Invoke(target, args);
        }

        [MenuItem("Aaris/CI/Verify tactical pause and control authority")]
        public static void Run()
        {
            string[] cases = {
                "start-paused", "start-disabled", "start-motor-disabled", "start-inactive",
                "finish-paused", "finish-disabled", "finish-motor-disabled", "finish-inactive",
                "focus-lost", "application-paused", "focus-retained", "application-resumed",
                "start-valid", "finish-valid", "throw-paused", "throw-disabled",
                "throw-motor-disabled", "throw-inactive", "throw-cooldown", "throw-empty",
                "wall-paused", "wall-motor-disabled", "wall-disabled", "wall-inactive",
                "aim-paused", "aim-motor-disabled", "wall-valid", "start-dead",
                "finish-dead", "throw-dead"
            };
            var report = new StringBuilder("UNITY=" + Application.unityVersion + "\n");
            int failures = 0;
            Scene original = SceneManager.GetActiveScene();
            Scene temporary = default(Scene);
            float previousTimeScale = Time.timeScale;
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
                foreach (string kind in cases)
                {
                    bool passed;
                    string detail;
                    try { passed = Check(kind, out detail); }
                    catch (Exception ex) { passed = false; detail = ex.GetType().Name; }
                    report.AppendLine((passed ? "PASS " : "FAIL ") + kind + " " + detail);
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
                Time.timeScale = previousTimeScale;
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (temporary.IsValid() && temporary.isLoaded)
                    EditorSceneManager.CloseScene(temporary, true);
            }
            report.AppendLine("RESULT=" + (failures == 0 ? "PASS" : "FAIL"));
            string folder = Path.Combine(Path.GetTempPath(), "aaris-tactical-input-qa");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static bool Check(string kind, out string detail)
        {
            Time.timeScale = 1f;
            var actor = new GameObject("Aaris temporary tactical QA");
            actor.transform.position = Origin;
            try
            {
                var motor = actor.AddComponent<AarisThirdPersonPlayer>();
                var cameraObj = new GameObject("Temporary aim camera");
                cameraObj.transform.SetParent(actor.transform, false);
                var camera = cameraObj.AddComponent<Camera>();
                camera.enabled = false;
                Set(motor, "followCamera", camera);
                var health = actor.AddComponent<AarisOfflineHealth>();
                Set(health, "current", 100f);
                var wall = actor.AddComponent<AarisOfflineGlooWall>();
                Set(wall, "motor", motor);
                var hud = actor.AddComponent<AarisOfflineTacticalHUD>();
                Set(hud, "motor", motor);
                Set(hud, "medicalHealth", health);
                Set(hud, "gloo", wall);
                Set(hud, "medkits", 1);
                bool finish = kind.StartsWith("finish-", StringComparison.Ordinal);
                bool lifecycle = kind == "focus-lost" || kind == "application-paused" ||
                    kind == "focus-retained" || kind == "application-resumed";
                if (finish || lifecycle)
                {
                    Set(hud, "healing", true);
                    Set(hud, "healthAtHealStart", 100f);
                    Set(hud, "healEnd", Time.time - 1f);
                    if (lifecycle) Set(wall, "aiming", true);
                }
                if (kind.EndsWith("-paused", StringComparison.Ordinal) && !lifecycle)
                    Time.timeScale = 0f;
                if (kind.EndsWith("-motor-disabled", StringComparison.Ordinal)) motor.enabled = false;
                if (kind.EndsWith("-inactive", StringComparison.Ordinal)) actor.SetActive(false);
                if (kind == "start-disabled" || kind == "finish-disabled" || kind == "throw-disabled")
                    hud.enabled = false;
                if (kind == "wall-disabled") wall.enabled = false;
                if (kind.EndsWith("-dead", StringComparison.Ordinal)) Set(health, "dead", true);
                if (kind == "throw-cooldown") Set(hud, "nextThrow", Time.time + 10f);
                if (kind == "throw-empty") Set(hud, "fragGrenades", 0);

                bool passed;
                if (kind.StartsWith("start-", StringComparison.Ordinal))
                {
                    Call(hud, "StartHealing");
                    passed = Get<bool>(hud, "healing") == (kind == "start-valid");
                }
                else if (finish)
                {
                    Call(hud, "UpdateHealing");
                    passed = !Get<bool>(hud, "healing") &&
                        Get<int>(hud, "medkits") == (kind == "finish-valid" ? 0 : 1) &&
                        Mathf.Approximately(health.CurrentHealth, kind == "finish-valid" ? 175f : 100f);
                }
                else if (lifecycle)
                {
                    bool keep = kind == "focus-retained" || kind == "application-resumed";
                    if (kind.StartsWith("focus-", StringComparison.Ordinal))
                        Call(hud, "OnApplicationFocus", keep);
                    else Call(hud, "OnApplicationPause", !keep);
                    passed = Get<bool>(hud, "healing") == keep && wall.Aiming == keep;
                }
                else if (kind.StartsWith("throw-", StringComparison.Ordinal))
                {
                    int grenades = Get<int>(hud, "fragGrenades");
                    hud.ThrowFrag();
                    passed = Get<int>(hud, "fragGrenades") == grenades;
                }
                else if (kind.StartsWith("aim-", StringComparison.Ordinal))
                {
                    wall.BeginAim();
                    passed = !wall.Aiming && wall.Count == 5;
                }
                else passed = wall.CanUse == (kind == "wall-valid");

                detail = "healing=" + Get<bool>(hud, "healing") + " medkits=" + Get<int>(hud, "medkits") +
                    " health=" + health.CurrentHealth + " grenades=" + Get<int>(hud, "fragGrenades") +
                    " wallAim=" + wall.Aiming;
                return passed;
            }
            finally
            {
                // Baseline forbidden throws can create a grenade. Delete only
                // objects from this isolated QA position, never world gameplay.
                foreach (var grenade in Object.FindObjectsByType<AarisOfflineFragGrenade>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if ((grenade.transform.position - Origin).sqrMagnitude < 16f)
                        Object.DestroyImmediate(grenade.gameObject);
                Object.DestroyImmediate(actor);
                Time.timeScale = 1f;
            }
        }
    }
}
#endif
