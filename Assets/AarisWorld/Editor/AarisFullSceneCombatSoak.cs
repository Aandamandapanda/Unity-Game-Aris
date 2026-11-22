#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Aaris.MapReview.Editor
{
    /// <summary>
    /// Tests actual authored terrain and authored runtime spawn logic in Play Mode.
    /// Reports headless Editor timings; never claims Android FPS.
    /// Run ONLY in a dedicated batchmode session; no authored scene is saved.
    /// </summary>
    [InitializeOnLoad]
    public static class AarisFullSceneCombatSoak
    {
        private const string Scene = "Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity";
        private const string Session = "AarisFullSceneCombatSoakV1";
        private static readonly string DirectoryPath =
            Path.Combine(Path.GetTempPath(), "aaris-full-scene-combat-soak");
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int BaselineSamples = 200;
        private const int LoadedSamplesMinimum = 200;
        private const float LoadedSecondsMinimum = 1.30f;
        private static AarisOfflineBRDirector director;
        private static AarisOfflineDrop drop;
        private static AarisOfflineHealth health;
        private static AarisThirdPersonPlayer player;
        private static ProfilerRecorder mainThread, gcFrame;
        private static Array rivals;
        private static readonly List<float> baselineMain = new List<float>(250);
        private static readonly List<float> loadedMain = new List<float>(3500);
        private static readonly List<long> baselineGc = new List<long>(250);
        private static readonly List<long> loadedGc = new List<long>(3500);
        private static readonly List<float> baselineDelta = new List<float>(250);
        private static readonly List<float> loadedDelta = new List<float>(3500);
        private static int stage, lastFrame = -1, warmup, loadedFrames, damageEvents;
        private static float sampleStarted, previousTimeScale, previousFixedDelta;
        private static bool sawRealRival, hadRuntimeError;
        private static int peakSpawned, peakActive;
        private static string errorFirst = "";
        private static bool startedPlay;
        static AarisFullSceneCombatSoak()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += RuntimeLog;
        }

        [MenuItem("Aaris/CI/Soak real scene with 100 spawned rivals")]
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("CI_BATCHMODE_ONLY");
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling)
                throw new InvalidOperationException("EDITOR_BUSY");
            System.IO.Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath, "result.txt"),
                "UNITY " + Application.unityVersion +
                "\nSCENE " + Scene +
                "\nLIMIT=Linux headless Editor only; not Android device FPS\n");
            SessionState.SetBool(Session, false);
            SessionState.SetBool(Session + "Start", true);
            SessionState.SetBool(Session + "Finish", false);
            SessionState.SetInt(Session + "Exit", 1);
            SessionState.SetFloat(Session + "Deadline",
                (float)EditorApplication.timeSinceStartup + 360f);
            stage = 0;
            startedPlay = false;
            hadRuntimeError = false;
            errorFirst = "";
        }

        private static object Read(object instance, string field)
        {
            var member = instance.GetType().GetField(field, Private);
            if (member == null)
                throw new InvalidOperationException("MISSING_FIELD_" + field);
            return member.GetValue(instance);
        }
        private static void Note(string line) =>
            File.AppendAllText(Path.Combine(DirectoryPath, "result.txt"), line + "\n");

        private static void RuntimeLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Session, false)) return;
            if (type != LogType.Exception && type != LogType.Error &&
                type != LogType.Assert) return;
            // Unity's background Search indexer is unrelated to gameplay.
            if (stack != null && stack.Contains("UnityEditor.Search.")) return;
            if (!hadRuntimeError)
            {
                hadRuntimeError = true;
                errorFirst = message.Split('\n')[0];
                Note("RUNTIME_ERROR " + errorFirst);
            }
        }

        private static void Tick()
        {
            if (SessionState.GetBool(Session + "Finish", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Session, false);
                SessionState.SetBool(Session + "Finish", false);
                int code = SessionState.GetInt(Session + "Exit", 1);
                if (Application.isBatchMode) EditorApplication.Exit(code);
                return;
            }
            if (!SessionState.GetBool(Session, false))
            {
                if (!SessionState.GetBool(Session + "Start", false) ||
                    EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                try
                {
                    SessionState.SetBool(Session + "Start", false);
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        if (SceneManager.GetSceneAt(i).isDirty)
                            throw new InvalidOperationException("UNSAVED_SCENE");
                    EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
                    SessionState.SetBool(Session, true);
                    EditorApplication.isPlaying = true;
                }
                catch (Exception ex) { Fail(ex.GetType().Name + " " + ex.Message); }
                return;
            }
            if (EditorApplication.timeSinceStartup >
                SessionState.GetFloat(Session + "Deadline", float.MaxValue))
            { Fail("TIMEOUT stage=" + stage + " spawned=" + peakSpawned); return; }
            if (!EditorApplication.isPlaying) return;
            int frame = Time.frameCount;
            if (frame == lastFrame || frame == 0) return;
            lastFrame = frame;
            try
            {
                if (stage == 0) { Setup(); return; }
                if (stage == 1) { PrewarmAndBaseline(); return; }
                if (stage == 2) { SpawnAll(); return; }
                if (stage == 3) { MoveToCombatAndMeasure(); return; }
            }
            catch (Exception ex)
            {
                Fail(ex.GetType().Name + " " + ex.Message +
                    (ex.InnerException == null ? "" :
                        " => " + ex.InnerException.Message));
            }
        }

        private static void Setup()
        {
            if (Time.frameCount < 6) return;
            player = Object.FindFirstObjectByType<AarisThirdPersonPlayer>();
            director = Object.FindFirstObjectByType<AarisOfflineBRDirector>();
            if (player == null || director == null || player.AimCamera == null) return;
            // Wait for MonoBehaviour.Start to schedule the real async visual prewarm.
            if ((bool)Read(director, "rivalVisualPrewarmComplete")) return;
            drop = player.GetComponent<AarisOfflineDrop>();
            health = player.GetComponent<AarisOfflineHealth>();
            if (drop == null || health == null)
                throw new InvalidOperationException("SAVED_PLAYER_MISSING_DROP_OR_HEALTH");
            director.enabled = false;
            if (!drop.HasLanded)
            {
                // OnAction hands off from the aircraft; private Land uses the
                // same player renderer/capsule restoration path as real landing.
                drop.OnAction();
                var land = typeof(AarisOfflineDrop).GetMethod("Land", Private);
                if (land == null) throw new InvalidOperationException("LAND_METHOD_MISSING");
                land.Invoke(drop, null);
            }
            if (!drop.HasLanded) throw new InvalidOperationException("LAND_FAILED");
            previousTimeScale = Time.timeScale;
            previousFixedDelta = Time.fixedDeltaTime;
            baselineMain.Clear(); baselineGc.Clear(); baselineDelta.Clear();
            loadedMain.Clear(); loadedGc.Clear(); loadedDelta.Clear();
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 16);
            gcFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory,
                "GC Allocated In Frame", 16);
            health.Damaged += TrackDamage;
            stage = 1;
            Note("PASS scene loaded, original player landed, director temporarily paused");
        }

        private static void PrewarmAndBaseline()
        {
            // Keep naturally scheduled async Resources prewarm; a disabled
            // MonoBehaviour still runs an already-started Coroutine in Unity.
            if (!(bool)Read(director, "rivalVisualPrewarmComplete")) return;
            if (warmup++ < 40) return;
            Capture(baselineMain, baselineGc, baselineDelta);
            if (baselineMain.Count < BaselineSamples) return;
            Note("PASS 0-bot real-scene baseline captured; prewarm complete");
            director.enabled = true;
            Time.timeScale = 8f;
            Time.fixedDeltaTime = previousFixedDelta * 8f;
            stage = 2;
        }

        private static void SpawnAll()
        {
            if ((Time.frameCount % 20) != 0) return;
            int spawned = (int)Read(director, "spawnedRivals");
            int active = (int)Read(director, "activeRivals");
            if (spawned > peakSpawned) peakSpawned = spawned;
            if (active > peakActive) peakActive = active;
            if (!(bool)Read(director, "spawnComplete")) return;
            rivals = (Array)Read(director, "rivals");
            Note("SPAWNED_REAL_SCENE=" + spawned + "/100" +
                " ACTIVE=" + active + " ATTEMPTED_PASSES=" + Read(director, "spawnPasses"));
            Time.timeScale = 1f;
            Time.fixedDeltaTime = previousFixedDelta;
            // Real model import happens naturally on the next visual update.
            // Move the player to the first valid spawn and 5 other genuine
            // rivals into nearby sight range. This is transient Play Mode only.
            AarisOfflineRival first = null;
            for (int i = 0; i < rivals.Length; i++)
            {
                first = rivals.GetValue(i) as AarisOfflineRival;
                if (first != null && first.IsAlive) break;
            }
            if (first == null) throw new InvalidOperationException("NO_ALIVE_RIVAL");
            Vector3 center = first.transform.position;
            player.transform.position = center + new Vector3(0f, 0.15f, -17f);
            int near = 0;
            for (int i = 0; i < rivals.Length && near < 6; i++)
            {
                var bot = rivals.GetValue(i) as AarisOfflineRival;
                if (bot == null || !bot.IsAlive) continue;
                if (near > 0)
                {
                    float angle = near * Mathf.PI * 2f / 6f;
                    bot.transform.position = center +
                        new Vector3(Mathf.Sin(angle) * 11f, .15f,
                            Mathf.Cos(angle) * 11f);
                }
                near++;
            }
            Physics.SyncTransforms();
            Note("NEARBY_ACTUAL_RIVALS=" + near);
            sampleStarted = Time.time;
            warmup = 0;
            stage = 3;
        }

        private static void MoveToCombatAndMeasure()
        {
            if (health.IsDead)
                throw new InvalidOperationException("PLAYER_DIED_BEFORE_PROFILE");
            if (health.CurrentHealth < 130f) health.RestoreHealth(150f);
            if (warmup++ <= 80) return;
            Capture(loadedMain, loadedGc, loadedDelta);
            loadedFrames++;
            // Native visual imports and 0.55 sec staggered checks must get
            // game-time to run, not just many ultra-fast Editor frames.
            if (Time.time - sampleStarted < LoadedSecondsMinimum ||
                loadedFrames < LoadedSamplesMinimum) return;
            int humanoids = 0, far = 0, visible = 0;
            for (int i = 0; i < rivals.Length; i++)
            {
                var bot = rivals.GetValue(i) as AarisOfflineRival;
                if (bot == null || !bot.IsAlive) continue;
                if (bot.HasVisibleBody) visible++;
                var close = (GameObject)Read(bot, "visualRoot");
                var distant = (GameObject)Read(bot, "farVisualRoot");
                if (close != null && close.activeInHierarchy) humanoids++;
                if (distant != null && distant.activeInHierarchy) far++;
            }
            Note("RUNTIME_ALIVE=" + Read(director, "activeRivals") +
                " VISIBLE=" + visible + " CLOSE_RIGS=" + humanoids +
                " FAR_LODS=" + far + " PLAYER_DAMAGE_EVENTS=" + damageEvents);
            Note("BASELINE_MAIN_THREAD=" + Stats(baselineMain));
            Note("100_BOT_MAIN_THREAD=" + Stats(loadedMain));
            Note("BASELINE_GC=" + GcStats(baselineGc));
            Note("100_BOT_GC=" + GcStats(loadedGc));
            Note("BASELINE_FRAME=" + Stats(baselineDelta));
            Note("100_BOT_FRAME=" + Stats(loadedDelta));
            Note("LOADED_SECONDS=" + (Time.time - sampleStarted).ToString("F3"));
            bool pass = peakSpawned == 100 && humanoids > 0 &&
                visible > 0 && !hadRuntimeError;
            Note("RESULT=" + (pass ? "PASS" : "FAIL") +
                (pass ? "" : " expected_100_spawns_and_visible_real_rigs"));
            Complete(pass ? 0 : 1);
        }

        private static void TrackDamage(AarisOfflineHealth receiver,
            float amount, GameObject source)
        {
            if (amount > 0f) damageEvents++;
        }

        private static void Capture(List<float> cpu, List<long> gc,
            List<float> frames)
        {
            if (mainThread.Valid && mainThread.LastValue > 0)
                cpu.Add(mainThread.LastValue / 1000000f);
            if (gcFrame.Valid && gcFrame.LastValue >= 0)
                gc.Add(gcFrame.LastValue);
            float elapsed = Time.unscaledDeltaTime * 1000f;
            if (elapsed > 0f && elapsed < 3000f) frames.Add(elapsed);
        }

        private static string Stats(List<float> values)
        {
            if (values.Count == 0) return "UNAVAILABLE";
            float[] sorted = values.ToArray();
            Array.Sort(sorted);
            double total = 0;
            foreach (float v in sorted) total += v;
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Length * .95f) - 1,
                0, sorted.Length - 1);
            return "frames=" + sorted.Length + " mean=" +
                (total / sorted.Length).ToString("F3") +
                "ms p95=" + sorted[index].ToString("F3") + "ms";
        }

        private static string GcStats(List<long> values)
        {
            if (values.Count == 0) return "UNAVAILABLE";
            long total = 0, peak = 0;
            foreach (long value in values)
            {
                total += value;
                peak = Math.Max(peak, value);
            }
            return "frames=" + values.Count + " mean=" +
                (total / (double)values.Count).ToString("F1") +
                "B/frame max=" + peak + "B/frame";
        }

        private static void Fail(string failure)
        {
            Note("RESULT=FAIL " + failure);
            Complete(1);
        }

        private static void Complete(int code)
        {
            if (mainThread.Valid) mainThread.Dispose();
            if (gcFrame.Valid) gcFrame.Dispose();
            if (health != null) health.Damaged -= TrackDamage;
            Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1f;
            if (previousFixedDelta > 0) Time.fixedDeltaTime = previousFixedDelta;
            SessionState.SetInt(Session + "Exit", code);
            SessionState.SetBool(Session + "Finish", true);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
#endif
