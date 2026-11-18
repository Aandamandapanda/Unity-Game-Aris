#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Aaris.MapReview.Editor
{
    /// <summary>Native control-state audit without wheel simulation, asset saves or Play transition.</summary>
    public static class AarisDrivingInputQA
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object obj, string field, object value) =>
            obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
        private static void Call(object obj, string method, params object[] args) =>
            obj.GetType().GetMethod(method, Private).Invoke(obj, args);

        [MenuItem("Aaris/CI/Verify driving input cannot survive suspension")]
        public static void Run()
        {
            string[] cases = { "session-paused", "session-inactive-car", "session-disabled",
                "motor-paused", "motor-disabled-driver", "update-paused", "update-inactive-car",
                "vehicle-disable", "vehicle-reenable", "hud-paused", "hud-inactive-car",
                "hud-paused-late", "hud-inactive-car-late", "valid-input", "nonfinite-input",
                "valid-arrows", "resume-fresh-press", "foreign-vehicle-disable", "mode-paused",
                "motor-disabled", "focus-lost", "application-paused", "release-paused", "clamped-reverse" };
            Scene original = SceneManager.GetActiveScene(), temporary = default(Scene);
            float scale = Time.timeScale;
            int failures = 0;
            var report = new StringBuilder("UNITY=" + Application.unityVersion + "\n");
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("EDITOR_BUSY");
                if (!(Application.isBatchMode && string.IsNullOrEmpty(original.path) && original.rootCount == 0))
                {
                    temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.SetActiveScene(temporary);
                }
                foreach (string kind in cases)
                {
                    bool passed; string detail;
                    try { passed = Check(kind, out detail); }
                    catch (Exception ex) { passed = false; detail = ex.GetType().Name; }
                    report.AppendLine((passed ? "PASS " : "FAIL ") + kind + " " + detail);
                    if (!passed) failures++;
                }
            }
            catch (Exception ex) { failures++; report.AppendLine("FAIL " + ex.GetType().Name); }
            finally
            {
                Time.timeScale = scale;
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (temporary.IsValid() && temporary.isLoaded) EditorSceneManager.CloseScene(temporary, true);
            }
            report.AppendLine("RESULT=" + (failures == 0 ? "PASS" : "FAIL"));
            string folder = Path.Combine(Path.GetTempPath(), "aaris-driving-input-qa");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static bool Check(string kind, out string detail)
        {
            Time.timeScale = 1f;
            var root = new GameObject("Temporary driving input QA");
            try
            {
                var actor = new GameObject("Driver"); actor.transform.SetParent(root.transform);
                var foot = actor.AddComponent<AarisThirdPersonPlayer>(); foot.enabled = false;
                var session = actor.AddComponent<AarisVehicleSession>();
                var worldObj = new GameObject("Temporary vehicle world"); worldObj.transform.SetParent(root.transform);
                Set(session, "world", worldObj.AddComponent<AarisVehicleWorld>());
                Set(session, "footMotor", foot);
                var carObj = new GameObject("Car"); carObj.transform.SetParent(root.transform);
                var car = carObj.AddComponent<AarisVehicleMotor>();
                Set(session, "car", car); Set(car, "driver", session);
                var hudObj = new GameObject("Drive HUD"); hudObj.transform.SetParent(root.transform);
                var hud = hudObj.AddComponent<AarisVehicleHUD>(); Set(hud, "session", session);
                // The new cached link is installed by the normal HUD Configure.
                session.GetType().GetField("vehicleHud", Private)?.SetValue(session, hud);
                var arrows = new GameObject("Arrows"); arrows.transform.SetParent(root.transform);
                var button = new GameObject("Exit button"); button.transform.SetParent(root.transform);
                var speed = new GameObject("Speed"); speed.transform.SetParent(root.transform);
                var label = new GameObject("Caption", typeof(RectTransform), typeof(Text)); label.transform.SetParent(root.transform);
                Set(hud, "arrowPanel", arrows); Set(hud, "driveButton", button); Set(hud, "speedPanel", speed);
                Set(hud, "driveCaption", label.GetComponent<Text>());
                bool[] pressed = Get<bool[]>(hud, "pressed");
                Action seed = () => {
                    Set(session, "touchMove", 1f); Set(session, "touchSteer", 1f);
                    Set(car, "forward", 1f); Set(car, "steering", 1f);
                    pressed[0] = pressed[3] = true;
                };
                Func<bool> neutral = () => Mathf.Approximately(Get<float>(session, "touchMove"), 0f) &&
                    Mathf.Approximately(Get<float>(session, "touchSteer"), 0f) &&
                    Mathf.Approximately(Get<float>(car, "forward"), 0f) &&
                    Mathf.Approximately(Get<float>(car, "steering"), 0f) &&
                    !Array.Exists(pressed, p => p);
                Func<float, float, bool> axes = (move, steer) =>
                    Mathf.Approximately(Get<float>(session, "touchMove"), move) &&
                    Mathf.Approximately(Get<float>(session, "touchSteer"), steer) &&
                    Mathf.Approximately(Get<float>(car, "forward"), move) &&
                    Mathf.Approximately(Get<float>(car, "steering"), steer);
                bool passed;
                switch (kind)
                {
                    case "session-paused": Time.timeScale = 0f; session.SetSteering(.8f, .5f); passed = neutral(); break;
                    case "session-inactive-car": car.enabled = false; session.SetSteering(.8f, .5f); passed = neutral(); break;
                    case "session-disabled": session.enabled = false; session.SetSteering(.8f, .5f); passed = neutral(); break;
                    case "motor-paused": Time.timeScale = 0f; car.SetInput(.8f, .5f); passed = neutral(); break;
                    case "motor-disabled-driver": session.enabled = false; car.SetInput(.8f, .5f); passed = neutral(); break;
                    case "update-paused": seed(); Set(session, "world", null); Time.timeScale = 0f; Call(session, "Update"); passed = neutral(); break;
                    case "update-inactive-car": seed(); car.enabled = false; Set(car, "forward", 0f); Set(car, "steering", 0f); Call(session, "Update"); Call(hud, "LateUpdate"); passed = neutral(); break;
                    case "vehicle-disable": seed(); car.enabled = false; Call(car, "OnDisable"); passed = neutral() && session.IsDriving; break;
                    case "vehicle-reenable": seed(); car.enabled = false; Call(car, "OnDisable"); car.enabled = true; Call(session, "Update"); passed = neutral(); break;
                    case "hud-paused": Time.timeScale = 0f; hud.ArrowState(0, true); passed = neutral(); break;
                    case "hud-inactive-car": car.enabled = false; hud.ArrowState(0, true); passed = neutral(); break;
                    case "hud-paused-late": seed(); Time.timeScale = 0f; Call(hud, "LateUpdate"); passed = neutral() && !arrows.activeSelf && !button.activeSelf; break;
                    case "hud-inactive-car-late": seed(); car.enabled = false; Set(car, "forward", 0f); Set(car, "steering", 0f); Call(hud, "LateUpdate"); passed = neutral() && !arrows.activeSelf && button.activeSelf; break;
                    case "valid-input": session.SetSteering(.8f, -.5f); passed = axes(.8f, -.5f); break;
                    case "nonfinite-input": session.SetSteering(float.NaN, float.PositiveInfinity); passed = neutral(); break;
                    case "valid-arrows": hud.ArrowState(0, true); hud.ArrowState(3, true); passed = axes(1f, 1f) && pressed[0] && pressed[3]; break;
                    case "resume-fresh-press":
                        hud.ArrowState(0, true); Time.timeScale = 0f; Call(session, "Update");
                        Time.timeScale = 1f; Call(session, "Update"); bool stopped = neutral();
                        hud.ArrowState(0, true); passed = stopped && axes(1f, 0f); break;
                    case "foreign-vehicle-disable":
                        seed(); var otherObj = new GameObject("Other car"); otherObj.transform.SetParent(root.transform);
                        var other = otherObj.AddComponent<AarisVehicleMotor>(); Set(other, "driver", session);
                        Call(other, "OnDisable"); passed = axes(1f, 1f) && pressed[0] && pressed[3]; break;
                    case "mode-paused": seed(); Time.timeScale = 0f; bool changed = session.TryEnterOrExit(); passed = !changed && session.IsDriving && neutral(); break;
                    case "motor-disabled": car.enabled = false; car.SetInput(.8f, .5f); passed = neutral(); break;
                    case "focus-lost": seed(); Call(session, "OnApplicationFocus", false); passed = neutral() && session.IsDriving; break;
                    case "application-paused": seed(); Call(session, "OnApplicationPause", true); passed = neutral() && session.IsDriving; break;
                    case "release-paused": hud.ArrowState(0, true); Time.timeScale = 0f; hud.ArrowState(0, false); passed = neutral(); break;
                    case "clamped-reverse": session.SetSteering(-2f, 2f); passed = axes(-1f, 1f); break;
                    default: throw new InvalidOperationException("UNKNOWN_CASE");
                }
                detail = "touch=" + Get<float>(session, "touchMove") + "," + Get<float>(session, "touchSteer") +
                    " motor=" + Get<float>(car, "forward") + "," + Get<float>(car, "steering") +
                    " pressed=" + Array.FindAll(pressed, p => p).Length;
                return passed;
            }
            finally { Object.DestroyImmediate(root); Time.timeScale = 1f; }
        }
    }
}
#endif
