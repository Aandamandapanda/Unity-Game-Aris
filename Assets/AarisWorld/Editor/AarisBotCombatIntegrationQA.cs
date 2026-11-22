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
    /// <summary>
    /// Real PhysX combat contract in an isolated temporary scene.
    /// 100 simplified logical rivals, not a device FPS benchmark.
    /// </summary>
    public static class AarisBotCombatIntegrationQA
    {
        private static readonly Vector3 Origin = new Vector3(10000f, 10000f, 10000f);
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type BotType = typeof(AarisOfflineBRDirector).Assembly.GetType(
            "Aaris.MapReview.AarisOfflineRival", true);

        private static FieldInfo Field(Type type, string name, BindingFlags flags)
        {
            var field = type.GetField(name, flags);
            if (field == null) throw new InvalidOperationException("Missing field " + name);
            return field;
        }
        private static void Set(object target, string name, object value)
        {
            Field(target.GetType(), name, Instance).SetValue(target, value);
        }
        private static object Get(object target, string name)
        {
            return Field(target.GetType(), name, Instance).GetValue(target);
        }
        private static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethod(name, Instance);
            if (method == null) throw new InvalidOperationException("Missing method " + name);
            return method.Invoke(target, args);
        }
        private static void Check(bool pass, string label, StringBuilder result)
        {
            result.AppendLine((pass ? "PASS " : "FAIL ") + label);
            if (!pass) throw new InvalidOperationException(label);
        }
        private static AarisOfflineHealth Health(GameObject actor)
        {
            var hp = actor.AddComponent<AarisOfflineHealth>();
            Field(typeof(AarisOfflineHealth), "current", Instance).SetValue(hp, 200f);
            return hp;
        }
        private static CharacterController Capsule(GameObject actor)
        {
            var c = actor.AddComponent<CharacterController>();
            c.height = 1.8f;
            c.radius = .31f;
            c.center = Vector3.up * .9f;
            return c;
        }

        [MenuItem("Aaris/CI/Verify 100-bot combat selection and real damage")]
        public static void Run()
        {
            var report = new StringBuilder("Unity " + Application.unityVersion + "\n");
            Scene original = SceneManager.GetActiveScene();
            Scene scratch = default(Scene);
            int failures = 0;
            var active = Field(typeof(AarisOfflineBRDirector), "activeDirector", Static);
            object oldDirector = active.GetValue(null);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                    throw new InvalidOperationException("EDITOR_BUSY");
                // Batchmode can start with an unsaved empty untitled scene.
                // Unity disallows additive scene creation beside that scene.
                bool emptyBatchScene = Application.isBatchMode &&
                    string.IsNullOrEmpty(original.path) && original.rootCount == 0;
                if (!emptyBatchScene)
                {
                    scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                        NewSceneMode.Additive);
                    SceneManager.SetActiveScene(scratch);
                }
                VerifyDamage(report);
                VerifyBots(report, active);
            }
            catch (Exception ex)
            {
                failures++;
                report.AppendLine("FAIL " + ex.GetType().Name + ": " + ex.Message);
                if (ex.InnerException != null)
                    report.AppendLine("CAUSE " + ex.InnerException.GetType().Name +
                        ": " + ex.InnerException.Message);
            }
            finally
            {
                active.SetValue(null, oldDirector);
                if (original.IsValid() && original.isLoaded)
                    SceneManager.SetActiveScene(original);
                if (scratch.IsValid() && scratch.isLoaded)
                    EditorSceneManager.CloseScene(scratch, true);
            }
            report.AppendLine("RESULT=" + (failures == 0 ? "PASS" : "FAIL"));
            string directory = Path.Combine(Path.GetTempPath(), "aaris-bot-combat-qa");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void VerifyDamage(StringBuilder report)
        {
            var root = new GameObject("Temporary two-way damage audit");
            try
            {
                var player = new GameObject("Player");
                var bot = new GameObject("Bot");
                player.transform.SetParent(root.transform, false);
                bot.transform.SetParent(root.transform, false);
                player.transform.position = Origin + new Vector3(0f, 0f, 3f);
                bot.transform.position = Origin;
                var pBody = Capsule(player);
                var bBody = Capsule(bot);
                var pHp = Health(player);
                var bHp = Health(bot);
                int playerHits = 0, botHits = 0;
                pHp.Damaged += (receiver, loss, source) => { if (loss > 0f && source == bot) playerHits++; };
                bHp.Damaged += (receiver, loss, source) => { if (loss > 0f && source == player) botHits++; };
                var route = typeof(AarisEquippedGun).GetMethod("ApplyDamage", Static, null,
                    new[] { typeof(Collider), typeof(float), typeof(GameObject),
                        typeof(Vector3?) }, null);
                Check(route != null, "Shared authoritative collider damage route", report);
                Check(pHp.CurrentHealth == 200f && bHp.CurrentHealth == 200f,
                    "Both actors start with 200 HP", report);
                Physics.SyncTransforms();
                route.Invoke(null, new object[] { bBody, 20f, player,
                    (Vector3?)(Origin + Vector3.up * .6f) });
                route.Invoke(null, new object[] { pBody, 20f, bot,
                    (Vector3?)(player.transform.position + Vector3.up * .6f) });
                Check(Mathf.Abs(pHp.CurrentHealth - 180f) < .01f &&
                    Mathf.Abs(bHp.CurrentHealth - 180f) < .01f &&
                    playerHits == 1 && botHits == 1,
                    "Player-to-bot and bot-to-player real hit events", report);
                route.Invoke(null, new object[] { bBody, 20f, player,
                    (Vector3?)(Origin + Vector3.up * 1.65f) });
                Check(Mathf.Abs(bHp.CurrentHealth - 145f) < .01f && botHits == 2,
                    "Headshot applies 1.75x and notifies victim", report);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void VerifyBots(StringBuilder report, FieldInfo active)
        {
            var root = new GameObject("Temporary 100-bot encounter audit");
            try
            {
                var player = new GameObject("Player target");
                player.transform.SetParent(root.transform, false);
                player.transform.position = Origin;
                var pBody = Capsule(player);
                var pHp = Health(player);
                var end = player.AddComponent<AarisOfflineMatchEnd>();
                var directorObj = new GameObject("Director");
                directorObj.transform.SetParent(root.transform, false);
                var director = directorObj.AddComponent<AarisOfflineBRDirector>();
                Set(director, "player", player.transform);
                Set(director, "playerHealth", pHp);
                Set(director, "matchEnd", end);
                Set(director, "started", true);
                var roster = (Array)Get(director, "rivals");
                Check(roster.Length == 100, "Exactly 100 rival roster slots", report);
                var bots = new Component[100];
                var health = new AarisOfflineHealth[100];
                Vector3[] nearby = { new Vector3(6f, 0f, 0f),
                    new Vector3(0f, 0f, 7f), new Vector3(-8f, 0f, 0f) };
                for (int i = 0; i < 100; i++)
                {
                    var go = new GameObject("QA rival " + i);
                    go.transform.SetParent(root.transform, false);
                    Vector3 offset;
                    if (i < nearby.Length) offset = nearby[i];
                    else
                    {
                        float angle = (i - nearby.Length) * (Mathf.PI * 2f / 97f);
                        float radius = 500f + (i % 9) * 24f;
                        offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    }
                    go.transform.position = Origin + offset;
                    go.transform.rotation = Quaternion.LookRotation(-offset.normalized);
                    Capsule(go);
                    health[i] = Health(go);
                    bots[i] = go.AddComponent(BotType);
                    Set(bots[i], "health", health[i]);
                    Set(bots[i], "playerTarget", player.transform);
                    Set(bots[i], "playerTargetHealth", pHp);
                    Set(bots[i], "playerTargetCapsule", pBody);
                    var visual = new GameObject("Visible body signal");
                    visual.transform.SetParent(go.transform, false);
                    Set(bots[i], "visualRoot", visual);
                    roster.SetValue(bots[i], i);
                }
                active.SetValue(null, director);
                Physics.SyncTransforms();
                // The production selection advances only once per actual frame.
                // In edit-mode test explicitly emulate nine successive frames.
                for (int frame = 0; frame < 9; frame++)
                {
                    Set(director, "attackerScanFrame", -1);
                    Call(director, "RefreshPlayerAttackers");
                }
                for (int i = 0; i < 3; i++)
                {
                    object[] ray = { player.transform, default(RaycastHit) };
                    bool los = (bool)Call(bots[i], "TryGetSightHit", ray);
                    bool selected = (bool)Call(bots[i], "HasPlayerSightForSelection");
                    report.AppendLine("DEBUG bot=" + i + " visible=" +
                        ((GameObject)Get(bots[i], "visualRoot")).activeInHierarchy +
                        " los=" + los + " selected=" + selected +
                        " hit=" + (((RaycastHit)ray[1]).collider != null ?
                            ((RaycastHit)ray[1]).collider.name : "null"));
                }
                report.AppendLine("DEBUG cursor=" + Get(director, "attackerScanCursor") +
                    " nearest=" + Get(director, "attackerScanFirst") +
                    " second=" + Get(director, "attackerScanSecond"));
                var first = Get(director, "attackerOne");
                var second = Get(director, "attackerTwo");
                Check(first != null && second != null && first != second,
                    "Two distinct nearby player attackers selected", report);
                int permissions = 0;
                for (int i = 0; i < 100; i++)
                    if ((bool)Call(director, "MayAttackPlayer", bots[i])) permissions++;
                Check(permissions == 2, "100 rivals allow exactly two player attackers", report);
                Check(!(bool)Call(director, "MayAttackPlayer", bots[50]),
                    "Far rival cannot swarm player", report);
                var firstVisible = (GameObject)Get(first, "visualRoot");
                firstVisible.SetActive(false);
                Check(!(bool)Call(director, "MayAttackPlayer", first),
                    "Invisible rival loses attack permission immediately", report);
                firstVisible.SetActive(true);

                var muzzle = BotType.GetMethod("MuzzlePathClear", Instance);
                Check(muzzle != null, "Bot close-muzzle guard present", report);
                Check((bool)muzzle.Invoke(bots[1], new object[] { player.transform }),
                    "Bot muzzle clear in open terrain", report);
                var cover = new GameObject("Bot muzzle obstruction");
                cover.transform.SetParent(root.transform, false);
                cover.transform.position = Origin + new Vector3(0f, 1.28f, 6.77f);
                cover.AddComponent<BoxCollider>().size = new Vector3(.8f, 1f, .08f);
                Physics.SyncTransforms();
                Check(!(bool)muzzle.Invoke(bots[1], new object[] { player.transform }),
                    "Bot shot blocked by close solid muzzle cover", report);
                Object.DestroyImmediate(cover);
                Physics.SyncTransforms();

                var cameraObj = new GameObject("QA scope camera");
                cameraObj.transform.SetParent(player.transform, false);
                var camera = cameraObj.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = Origin + Vector3.up * 1.35f;
                camera.transform.LookAt(bots[1].transform.position + Vector3.up * 1.12f);
                var acquire = typeof(AarisOfflineBRDirector).GetMethod(
                    "TryAcquireAimTarget", Static);
                Check(acquire != null, "HUD and gun shared Red-Lock query present", report);
                object[] args = { camera, player.transform, 50f, 2.3f, Vector3.zero };
                Check((bool)acquire.Invoke(null, args),
                    "Red-Lock finds physical visible rival in range", report);
                var sightWall = new GameObject("QA LOS wall");
                sightWall.transform.SetParent(root.transform, false);
                sightWall.transform.position = Origin + new Vector3(0f, 1.25f, 3f);
                sightWall.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, .15f);
                Physics.SyncTransforms();
                Check(!(bool)acquire.Invoke(null, args),
                    "Red-Lock turns off behind solid wall", report);
                Object.DestroyImmediate(sightWall);
                Physics.SyncTransforms();

                var gunObj = new GameObject("QA equipped gun");
                gunObj.transform.SetParent(player.transform, false);
                var gun = gunObj.AddComponent<AarisEquippedGun>();
                var spec = new AarisWeaponSpec { id = "qa", category = "AR",
                    magazineSize = 30, reserveAmmo = 60, rpm = 600, damage = 20f,
                    rangeMetres = 50f, pellets = 1, spreadDegrees = 0f,
                    recoilPitchDegrees = 0f, reloadSeconds = 1f, scopePower = 2 };
                Set(gun, "spec", spec);
                Set(gun, "magazine", 30);
                Check(gun.TryFire(camera, player) && gun.Magazine == 29 &&
                    health[1].CurrentHealth < 200f,
                    "Real 2x Red-Lock shot consumes ammo and damages bot", report);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
