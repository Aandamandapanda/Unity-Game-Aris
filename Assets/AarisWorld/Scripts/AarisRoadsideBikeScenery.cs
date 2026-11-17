using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aaris.MapReview
{
    /// <summary>
    /// A bounded stream of PARKED motorcycle scenery, not rideable bikes.
    /// Two-wheel driving must never be faked with the four-wheel car motor.
    /// </summary>
    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    public sealed class AarisRoadsideBikeScenery : MonoBehaviour
    {
        private const string ModelResource = "AarisVehicles/Authored/AarisRoadBike";
        private const int MaximumVisibleBikes = 3;
        private const float LoadRadius = 45f;
        private const float UnloadRadius = 66f;
        private readonly List<BikePlan> plans = new List<BikePlan>(18);
        private readonly RaycastHit[] supportHits = new RaycastHit[20];
        private readonly RaycastHit[] denseSupportHits = new RaycastHit[96];
        private readonly Collider[] blockingHits = new Collider[24];
        private readonly Collider[] denseBlockingHits = new Collider[96];
        private readonly Material[] surfaces = new Material[8];
        private readonly Material[] paintVariants = new Material[4];
        private Transform player;
        private AarisNearbyColliders nearbyColliders;
        private GameObject bikeSource;
        private float nextPoll;

        private sealed class BikePlan
        {
            internal AarisVehiclePoint point;
            internal GameObject visible;
            internal float nextProbe;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            AarisThirdPersonPlayer motor = FindFirstObjectByType<AarisThirdPersonPlayer>();
            if (motor == null || FindFirstObjectByType<AarisRoadsideBikeScenery>() != null)
                return;
            var root = new GameObject("Aaris Roadside Motorcycles (streamed scenery)");
            root.AddComponent<AarisRoadsideBikeScenery>().player = motor.transform;
        }

        private void Start()
        {
            nearbyColliders = FindFirstObjectByType<AarisNearbyColliders>();
            bikeSource = Resources.Load<GameObject>(ModelResource);
            TextAsset data = Resources.Load<TextAsset>("AarisVehicles/vehicle_candidates");
            if (bikeSource == null || data == null)
            {
                Debug.LogWarning("Aaris bikes: motorcycle model or road nodes missing.");
                enabled = false;
                return;
            }
            AarisVehiclePointsFile file;
            try { file = JsonUtility.FromJson<AarisVehiclePointsFile>(data.text); }
            catch (Exception e)
            {
                Debug.LogWarning("Aaris bikes: invalid road nodes (" + e.GetType().Name + ").");
                enabled = false;
                return;
            }
            if (file == null || file.schema != 1 || file.sizeMetres != 2000 ||
                file.candidates == null) { enabled = false; return; }
            int index = 0;
            foreach (var point in file.candidates)
            {
                if (point == null || point.kind != "roadside") continue;
                // More authored roads, same maximum of three visible models.
                if (index++ % 2 == 0) plans.Add(new BikePlan { point = point });
            }
            SetupMaterials();
        }

        private void SetupMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { enabled = false; return; }
            Color[] tones = {
                new Color(.08f,.23f,.40f), new Color(.034f,.039f,.048f),
                new Color(.57f,.61f,.67f), new Color(.15f,.17f,.20f),
                new Color(.055f,.062f,.075f), new Color(.94f,.91f,.72f),
                new Color(.78f,.038f,.02f), new Color(.98f,.43f,.035f)
            };
            for (int i = 0; i < surfaces.Length; i++)
            {
                surfaces[i] = new Material(shader)
                {
                    name = "Aaris Road Bike " + i,
                    enableInstancing = true
                };
                surfaces[i].SetColor("_BaseColor", tones[i]);
                surfaces[i].SetFloat("_Metallic",
                    i == 2 ? .78f : i == 0 ? .44f : .03f);
                surfaces[i].SetFloat("_Smoothness",
                    i == 1 ? .15f : i == 4 ? .25f : i == 2 ? .78f : .52f);
                if (i >= 5)
                {
                    surfaces[i].SetColor("_EmissionColor", tones[i] * .65f);
                    surfaces[i].EnableKeyword("_EMISSION");
                }
            }
            // Three extra shared paint finishes; never create a material per bike.
            paintVariants[0] = surfaces[0];
            Color[] paints = {
                new Color(.08f,.23f,.40f),
                new Color(.36f,.054f,.062f),
                new Color(.095f,.11f,.13f),
                new Color(.05f,.30f,.26f)
            };
            for (int i = 1; i < paintVariants.Length; i++)
            {
                paintVariants[i] = new Material(surfaces[0])
                {
                    name = "Aaris Road Bike Paint " + i,
                    enableInstancing = true
                };
                paintVariants[i].SetColor("_BaseColor", paints[i]);
            }
        }

        private void Update()
        {
            if (player == null || bikeSource == null || !enabled) return;
            float now = Time.unscaledTime;
            if (now < nextPoll) return;
            nextPoll = now + .4f;
            Vector3 position = player.position;
            int visibleCount = 0, unloaded = 0;
            foreach (var plan in plans)
            {
                if (plan.visible == null) continue;
                if ((plan.visible.transform.position - position).sqrMagnitude >
                    UnloadRadius * UnloadRadius && unloaded == 0)
                {
                    Destroy(plan.visible);
                    plan.visible = null;
                    unloaded++;
                }
                else visibleCount++;
            }
            if (visibleCount >= MaximumVisibleBikes) return;
            BikePlan nearest = null;
            float best = LoadRadius * LoadRadius;
            foreach (var plan in plans)
            {
                if (plan.visible != null || now < plan.nextProbe) continue;
                Vector3 point = new Vector3(plan.point.x, plan.point.y, plan.point.z);
                float d2 = (point - position).sqrMagnitude;
                if (d2 >= best) continue;
                best = d2;
                nearest = plan;
            }
            if (nearest == null) return;
            nearest.nextProbe = now + 3f;
            if (TryParkingSpot(nearest.point, out Vector3 spot))
                nearest.visible = CreateBike(nearest.point, spot);
        }

        private bool TryParkingSpot(AarisVehiclePoint point, out Vector3 spot)
        {
            spot = default;
            Quaternion rotation = Quaternion.Euler(0f, point.yaw, 0f);
            Vector3 right = rotation * (Vector3.right * 3.8f);
            for (int side = 1; side >= -1; side -= 2)
            {
                Vector3 origin = new Vector3(point.x, point.y + 3f, point.z)
                    + right * side;
                int count = Physics.RaycastNonAlloc(origin, Vector3.down,
                    supportHits, 7f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                RaycastHit[] floorHits = supportHits;
                if (count >= supportHits.Length)
                {
                    // Saturated NonAlloc results are unordered and incomplete.
                    // Do not hide a valid bike bay just because nearby roads,
                    // terrain seams or houses filled the small hit buffer.
                    count = Physics.RaycastNonAlloc(origin, Vector3.down,
                        denseSupportHits, 7f, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore);
                    floorHits = denseSupportHits;
                    if (count >= denseSupportHits.Length)
                    {
                        // Exceptional dense scene only: preserve the same slope
                        // and authored elevation checks on every actual hit.
                        floorHits = Physics.RaycastAll(origin, Vector3.down,
                            7f, Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore);
                        count = floorHits.Length;
                    }
                }
                float nearestError = 1.5f;
                Vector3 ground = default;
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = floorHits[i];
                    if (hit.collider == null || hit.normal.y < .965f) continue;
                    float error = Mathf.Abs(hit.point.y - point.y);
                    if (error >= nearestError) continue;
                    nearestError = error;
                    ground = hit.point;
                    found = true;
                }
                if (!found) continue;
                Vector3 center = ground + Vector3.up * .79f;
                Vector3 half = new Vector3(.55f,.69f,1.21f);
                if (nearbyColliders != null &&
                    nearbyColliders.HasPotentialInactiveObstacle(center,half,rotation))
                    continue;
                int obstacles = Physics.OverlapBoxNonAlloc(center, half,
                    blockingHits, rotation, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                Collider[] foundColliders = blockingHits;
                if (obstacles >= blockingHits.Length)
                {
                    // Never accept a partial overlap set as clear; retry with
                    // preallocated storage before rare complete-query fallback.
                    obstacles = Physics.OverlapBoxNonAlloc(center, half,
                        denseBlockingHits, rotation, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore);
                    foundColliders = denseBlockingHits;
                    if (obstacles >= denseBlockingHits.Length)
                    {
                        foundColliders = Physics.OverlapBox(center, half, rotation,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        obstacles = foundColliders.Length;
                    }
                }
                bool blocked = false;
                for (int i = 0; i < obstacles; i++)
                {
                    Collider collider = foundColliders[i];
                    if (collider != null && !(collider is TerrainCollider))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;
                spot = ground + Vector3.up * .035f;
                return true;
            }
            return false;
        }

        private static int SurfaceIndex(Material source, int fallback)
        {
            if (source == null) return fallback;
            string name = source.name.ToLowerInvariant();
            if (name.Contains("paint")) return 0;
            if (name.Contains("tyre") || name.Contains("rubber")) return 1;
            if (name.Contains("rim") || name.Contains("chrome")) return 2;
            if (name.Contains("engine")) return 3;
            if (name.Contains("seat")) return 4;
            if (name.Contains("headlamp")) return 5;
            if (name.Contains("taillamp")) return 6;
            if (name.Contains("amber")) return 7;
            return fallback;
        }

        private static int StablePaintIndex(string id)
        {
            // Deterministic across Android launches; unlike GetHashCode.
            if (string.IsNullOrEmpty(id)) return 0;
            uint hash = 2166136261u;
            unchecked
            {
                foreach (char c in id) hash = (hash ^ c) * 16777619u;
            }
            return (int)(hash % 4u);
        }

        private GameObject CreateBike(AarisVehiclePoint point, Vector3 location)
        {
            var root = new GameObject("Aaris parked motorcycle " + point.id);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(location,
                Quaternion.Euler(0f, point.yaw, 0f));
            GameObject imported = Instantiate(bikeSource, root.transform, false);
            imported.name = "Original sport motorcycle mesh";
            foreach (var sourceCollider in imported.GetComponentsInChildren<Collider>(true))
                sourceCollider.enabled = false;
            Renderer[] renderers = imported.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(root);
                return null;
            }
            // One shared URP material per authored surface, reused across
            // all visible bikes. No per-bike Material instances or per-frame GC.
            // This matches OBJ usemtl section order, NOT palette declaration
            // order: Tyre, Rim, Engine, Seat, Paint, Amber, Headlamp, TailLamp.
            int[] fallbackOrder = {1,2,3,4,0,7,5,6};
            int paintIndex = StablePaintIndex(point.id);
            foreach (var renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                if (source == null || source.Length == 0) continue;
                Material[] converted = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    int kind = SurfaceIndex(source[i],
                        fallbackOrder[Mathf.Min(i, fallbackOrder.Length - 1)]);
                    converted[i] = kind == 0 ? paintVariants[paintIndex] : surfaces[kind];
                }
                renderer.sharedMaterials = converted;
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, .76f, 0f);
            collider.size = new Vector3(.96f, 1.47f, 2.27f);
            return root;
        }

        private void OnDestroy()
        {
            foreach (Material material in surfaces)
                if (material != null) Destroy(material);
            // Paint variant zero aliases surfaces[0], so do not destroy twice.
            for (int i = 1; i < paintVariants.Length; i++)
                if (paintVariants[i] != null) Destroy(paintVariants[i]);
        }
    }
}
