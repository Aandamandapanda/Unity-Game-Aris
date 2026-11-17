using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Aaris.MapReview
{
    /// <summary>
    /// Offline-only match authority: a bounded set of local rivals, a nested
    /// closing zone, actual elimination counting and a final survivor result.
    /// No network, NavMesh bake, asset download or scene authoring required.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(280)]
    public sealed class AarisOfflineBRDirector : MonoBehaviour
    {
        private const int RivalCount = 6;
        private const float OceanCutoff = -37.35f;
        private static readonly float[] ZoneRadii = { 1080f, 660f, 360f, 175f, 65f };
        private static readonly float[] CloseStarts = { 45f, 165f, 285f, 400f };
        private static readonly float[] CloseEnds = { 140f, 245f, 360f, 465f };
        private readonly Vector2[] centers = new Vector2[5];
        private readonly AarisOfflineRival[] rivals = new AarisOfflineRival[RivalCount];
        private readonly Collider[] spawnHits = new Collider[24];
        private readonly System.Random random = new System.Random();
        private Transform player;
        private AarisOfflineHealth playerHealth;
        private AarisOfflineDrop drop;
        private AarisOfflineMatchEnd matchEnd;
        private AarisNearbyColliders nearbyColliders;
        private Terrain[] terrains;
        private int attemptedSlots, activeRivals, spawnedRivals;
        private float startedAt, nextSpawn, nextZoneTick, nextDamageTick, nextRingDraw;
        private float radius;
        private Vector2 center;
        private bool started, spawnComplete;
        private Canvas statusCanvas;
        private Text status;
        private LineRenderer boundary;
        private Material boundaryMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var actor = FindFirstObjectByType<AarisThirdPersonPlayer>();
            if (actor == null || FindFirstObjectByType<AarisOfflineBRDirector>() != null) return;
            var root = new GameObject("Aaris Offline Battle Royale Director");
            var director = root.AddComponent<AarisOfflineBRDirector>();
            director.player = actor.transform;
        }

        private void Start()
        {
            if (player == null) { enabled = false; return; }
            playerHealth = player.GetComponent<AarisOfflineHealth>();
            if (playerHealth == null)
                playerHealth = player.gameObject.AddComponent<AarisOfflineHealth>();
            matchEnd = player.GetComponent<AarisOfflineMatchEnd>();
            if (matchEnd == null)
                matchEnd = player.gameObject.AddComponent<AarisOfflineMatchEnd>();
            drop = player.GetComponent<AarisOfflineDrop>();
            terrains = Terrain.activeTerrains;
            nearbyColliders = FindFirstObjectByType<AarisNearbyColliders>();
        }

        private void Update()
        {
            if (player == null || playerHealth == null || playerHealth.IsDead ||
                matchEnd == null || matchEnd.IsFinished) return;
            if (!started)
            {
                // Runtime boot order is unspecified. Do not start an unseen
                // ground battle while the real player is still parachuting.
                if (drop == null) drop = player.GetComponent<AarisOfflineDrop>();
                if (drop != null && !drop.HasLanded) return;
                Begin();
            }

            float elapsed = Time.time - startedAt;
            if (!spawnComplete && Time.time >= nextSpawn)
            {
                // Exactly one authored encounter attempt per half-second.
                // Avoid a burst of costly Humanoid imports on 2GB phones.
                TrySpawnSlot(attemptedSlots++);
                nextSpawn = Time.time + 0.5f;
                if (attemptedSlots >= RivalCount)
                {
                    spawnComplete = true;
                    if (spawnedRivals == 0)
                        Debug.LogWarning("Aaris BR: no safe rival positions. Victory disabled for this match.");
                    else if (activeRivals == 0) matchEnd.WinMatch();
                }
            }

            if (Time.time >= nextZoneTick)
            {
                nextZoneTick = Time.time + 0.5f;
                EvaluateZone(elapsed);
                RefreshStatus(elapsed);
            }
            if (Time.time >= nextDamageTick)
            {
                nextDamageTick = Time.time + 2f;
                ApplyZoneDamage();
            }
            if (boundary != null && Time.time >= nextRingDraw)
            {
                nextRingDraw = Time.time + 2f;
                DrawBoundary();
            }
        }

        private void Begin()
        {
            started = true;
            startedAt = Time.time;
            nextSpawn = Time.time + 0.6f;
            terrains = Terrain.activeTerrains;
            Vector2 landing = new Vector2(player.position.x, player.position.z);
            centers[0] = landing;
            for (int i = 1; i < centers.Length; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                float shift = (ZoneRadii[i - 1] - ZoneRadii[i]) * 0.54f;
                Vector2 offset = new Vector2((float)Math.Cos(angle),
                    (float)Math.Sin(angle)) * shift;
                Vector2 proposed = centers[i - 1] + offset;
                centers[i] = new Vector2(Mathf.Clamp(proposed.x, -930f, 930f),
                    Mathf.Clamp(proposed.y, -930f, 930f));
            }
            center = centers[0];
            radius = ZoneRadii[0];
            CreateStatus();
            CreateBoundary();
            RefreshStatus(0f);
        }

        private void TrySpawnSlot(int index)
        {
            if (index < 0 || index >= RivalCount) return;
            float sectorAngle = Mathf.PI * 2f * (index + 0.5f) / RivalCount;
            for (int attempt = 0; attempt < 9; attempt++)
            {
                float angle = sectorAngle + (float)(random.NextDouble() - .5) * .85f;
                float distance = 72f + (float)random.NextDouble() * 80f;
                float x = player.position.x + Mathf.Cos(angle) * distance;
                float z = player.position.z + Mathf.Sin(angle) * distance;
                if (!TryGetGround(x, z, out float ground)) continue;
                Vector3 point = new Vector3(x, ground + 0.08f, z);
                if (!SpawnClear(point)) continue;

                var actor = new GameObject("Aaris Offline Rival " + (index + 1));
                actor.transform.SetPositionAndRotation(point,
                    Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                var capsule = actor.AddComponent<CharacterController>();
                capsule.height = 1.8f;
                capsule.radius = 0.31f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.stepOffset = 0.28f;
                capsule.skinWidth = 0.05f;
                capsule.slopeLimit = 43f;
                var health = actor.AddComponent<AarisOfflineHealth>();
                var rival = actor.AddComponent<AarisOfflineRival>();
                rival.Initialize(this, health, player, playerHealth);
                rivals[index] = rival;
                activeRivals++;
                spawnedRivals++;
                return;
            }
        }

        private bool TryGetGround(float x, float z, out float height)
        {
            height = 0f;
            if (terrains == null || terrains.Length == 0) return false;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain tile = terrains[i];
                if (tile == null || tile.terrainData == null) continue;
                Vector3 origin = tile.transform.position, size = tile.terrainData.size;
                if (x < origin.x + 2f || x > origin.x + size.x - 2f ||
                    z < origin.z + 2f || z > origin.z + size.z - 2f) continue;
                float u = (x - origin.x) / size.x;
                float v = (z - origin.z) / size.z;
                if (tile.terrainData.GetInterpolatedNormal(u, v).y < 0.78f) continue;
                height = origin.y + tile.SampleHeight(new Vector3(x, 0f, z));
                if (height > OceanCutoff) return true;
            }
            return false;
        }

        private bool SpawnClear(Vector3 point)
        {
            Vector3 low = point + Vector3.up * 0.36f;
            Vector3 high = point + Vector3.up * 1.50f;
            if (nearbyColliders != null &&
                nearbyColliders.HasPotentialInactiveObstacle(
                    point + Vector3.up * 0.90f,
                    new Vector3(0.38f, 0.90f, 0.38f), Quaternion.identity))
                return false;
            int count = Physics.OverlapCapsuleNonAlloc(low, high, 0.34f,
                spawnHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider[] hits = spawnHits;
            if (count >= spawnHits.Length)
            {
                hits = Physics.OverlapCapsule(low, high, 0.34f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
                if (hits[i] != null && !(hits[i] is TerrainCollider)) return false;
            return true;
        }

        private void EvaluateZone(float elapsed)
        {
            center = centers[centers.Length - 1];
            radius = ZoneRadii[ZoneRadii.Length - 1];
            for (int stage = 0; stage < CloseStarts.Length; stage++)
            {
                if (elapsed < CloseStarts[stage])
                {
                    center = centers[stage];
                    radius = ZoneRadii[stage];
                    return;
                }
                if (elapsed <= CloseEnds[stage])
                {
                    float blend = Mathf.Clamp01((elapsed - CloseStarts[stage]) /
                        (CloseEnds[stage] - CloseStarts[stage]));
                    center = Vector2.Lerp(centers[stage], centers[stage + 1], blend);
                    radius = Mathf.Lerp(ZoneRadii[stage], ZoneRadii[stage + 1], blend);
                    return;
                }
            }
        }

        private bool Outside(Vector3 position)
        {
            float dx = position.x - center.x, dz = position.z - center.y;
            float safe = Mathf.Max(1f, radius - 2f);
            return dx * dx + dz * dz > safe * safe;
        }

        private void ApplyZoneDamage()
        {
            if (!spawnComplete || playerHealth == null || playerHealth.IsDead) return;
            if (Outside(player.position))
                playerHealth.ApplyDamage(7f, null);
            for (int i = 0; i < rivals.Length; i++)
                if (rivals[i] != null && rivals[i].IsAlive &&
                    Outside(rivals[i].transform.position))
                    rivals[i].TakeZoneDamage(9f);
        }

        internal void RivalDefeated(AarisOfflineRival rival)
        {
            if (activeRivals > 0) activeRivals--;
            if (spawnComplete && spawnedRivals > 0 && activeRivals == 0 &&
                playerHealth != null && !playerHealth.IsDead &&
                matchEnd != null && !matchEnd.IsFinished)
                matchEnd.WinMatch();
        }

        internal bool CanEngage => started && !playerHealth.IsDead &&
            matchEnd != null && !matchEnd.IsFinished;

        private void RefreshStatus(float elapsed)
        {
            if (status == null) return;
            float wait = 0f;
            for (int i = 0; i < CloseStarts.Length; i++)
                if (elapsed < CloseEnds[i])
                {
                    wait = Mathf.Max(0f, CloseStarts[i] - elapsed);
                    break;
                }
            string label = Outside(player.position) ? "OUTSIDE ZONE" :
                wait > 0f ? "CLOSE IN " + Mathf.CeilToInt(wait) + "s" : "ZONE MOVING";
            status.text = "ALIVE " + (activeRivals + 1) +
                "    ZONE " + Mathf.CeilToInt(radius) + "m    " + label;
        }

        private void CreateStatus()
        {
            var canvasRoot = new GameObject("Aaris BR Status", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);
            statusCanvas = canvasRoot.GetComponent<Canvas>();
            statusCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            statusCanvas.sortingOrder = 165; // Below the five real action buttons.
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 540f);
            var labelRoot = new GameObject("Alive and zone", typeof(RectTransform), typeof(Text));
            labelRoot.transform.SetParent(canvasRoot.transform, false);
            var rect = labelRoot.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .98f);
            rect.sizeDelta = new Vector2(670f, 44f);
            status = labelRoot.GetComponent<Text>();
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 18;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = Color.white;
            status.raycastTarget = false;
        }

        private void CreateBoundary()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            boundaryMaterial = new Material(shader) { name = "Aaris Offline Zone Edge" };
            if (boundaryMaterial.HasProperty("_BaseColor"))
                boundaryMaterial.SetColor("_BaseColor", new Color(.25f, .9f, 1f, .85f));
            var objectRoot = new GameObject("Aaris Zone Boundary");
            objectRoot.transform.SetParent(transform, false);
            boundary = objectRoot.AddComponent<LineRenderer>();
            boundary.sharedMaterial = boundaryMaterial;
            boundary.positionCount = 65;
            boundary.useWorldSpace = true;
            boundary.widthMultiplier = 1.1f;
            boundary.startColor = boundary.endColor = new Color(.27f, .9f, 1f, .80f);
            boundary.shadowCastingMode = ShadowCastingMode.Off;
            boundary.receiveShadows = false;
            boundary.enabled = false;
        }

        private void DrawBoundary()
        {
            if (boundary == null) return;
            // Until the ring enters the map, it adds no draw calls at all.
            if (radius > 925f) { boundary.enabled = false; return; }
            for (int i = 0; i < 65; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.y + Mathf.Sin(angle) * radius;
                float ground = TryGetGround(x, z, out float sample) ? sample : OceanCutoff;
                boundary.SetPosition(i, new Vector3(x, ground + 1.2f, z));
            }
            boundary.enabled = true;
        }

        private void OnDestroy()
        {
            if (boundaryMaterial != null) Destroy(boundaryMaterial);
            // Child HUD/ring and rivals are scene-local, never DontDestroyOnLoad.
            // The scene manager handles their cleanup on restart.
        }
    }

    /// <summary>
    /// Cheap local rival: capsule collision, terrain-aware pursuit, bounded
    /// perception, genuine cover-occluded hits and a reused local Humanoid.
    /// The player's controller is never duplicated or modified.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AarisOfflineRival : MonoBehaviour
    {
        private AarisOfflineBRDirector owner;
        private AarisOfflineHealth health, targetHealth;
        private Transform target;
        private CharacterController capsule;
        private readonly RaycastHit[] sightHits = new RaycastHit[20];
        private float nextThink, nextAttack, verticalSpeed, turnOffset, visualBlend;
        private bool canSee;
        private PlayableGraph graph;
        private AnimationMixerPlayable animations;
        private bool hasAnimations;
        public bool IsAlive => health != null && !health.IsDead;

        internal void Initialize(AarisOfflineBRDirector director, AarisOfflineHealth self,
            Transform victim, AarisOfflineHealth victimHealth)
        {
            owner = director;
            health = self;
            target = victim;
            targetHealth = victimHealth;
            capsule = GetComponent<CharacterController>();
            health.Died += OnDied;
            turnOffset = UnityEngine.Random.Range(-12f, 12f);
            BuildVisual();
        }

        private void BuildVisual()
        {
            // The bundled CC0 humanoid is shared by all rivals. Avoid six
            // full hero-quality tactical rigs on entry-level 2GB devices.
            GameObject prefab = Resources.Load<GameObject>(
                "AarisCharacters/Kenney/characterMedium");
            if (prefab == null)
            {
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Rival fallback silhouette";
                fallback.transform.SetParent(transform, false);
                fallback.transform.localPosition = Vector3.up * .90f;
                fallback.transform.localScale = new Vector3(.62f, .9f, .62f);
                Collider c = fallback.GetComponent<Collider>();
                if (c != null) { c.enabled = false; Destroy(c); }
                return;
            }
            var visual = Instantiate(prefab, transform, false);
            visual.name = "Rival humanoid visual";
            foreach (var c in visual.GetComponentsInChildren<Collider>(true))
            { c.enabled = false; Destroy(c); }
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.detectCollisions = false; }
            Bounds bounds = new Bounds();
            bool found = false;
            foreach (var render in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!render.enabled || !render.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = render.bounds; found = true; }
                else bounds.Encapsulate(render.bounds);
            }
            if (found && bounds.size.y > .01f && bounds.size.y < 1000f)
            {
                float scale = Mathf.Clamp(1.74f / bounds.size.y, .02f, 25f);
                visual.transform.localScale *= scale;
                float low = float.PositiveInfinity;
                foreach (var render in visual.GetComponentsInChildren<Renderer>(true))
                    if (render.enabled && render.gameObject.activeInHierarchy)
                        low = Mathf.Min(low, render.bounds.min.y);
                if (!float.IsInfinity(low))
                    visual.transform.position += Vector3.up * (transform.position.y - low);
            }

            var animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman) return;
            var idle = FirstHumanoidClip("AarisCharacters/Kenney/idle");
            var run = FirstHumanoidClip("AarisCharacters/Kenney/run");
            if (idle == null || run == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            graph = PlayableGraph.Create("Aaris Offline Rival");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            animations = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(AnimationClipPlayable.Create(graph, idle), 0, animations, 0);
            graph.Connect(AnimationClipPlayable.Create(graph, run), 0, animations, 1);
            animations.SetInputWeight(0, 1f);
            animations.SetInputWeight(1, 0f);
            AnimationPlayableOutput.Create(graph, "Local rival animation", animator)
                .SetSourcePlayable(animations);
            graph.Play();
            hasAnimations = true;
        }

        private static AnimationClip FirstHumanoidClip(string resource)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>(resource))
                if (clip != null && !clip.legacy && clip.humanMotion && clip.length > .02f)
                    return clip;
            return null;
        }

        private void Update()
        {
            if (owner == null || !owner.CanEngage || !IsAlive ||
                target == null || targetHealth == null || targetHealth.IsDead) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float distance = diff.magnitude;
            Vector3 toward = distance > .01f ? diff / distance : transform.forward;
            if (Time.time >= nextThink)
            {
                nextThink = Time.time + .23f + UnityEngine.Random.Range(0f, .09f);
                canSee = distance <= 42f && HasSight();
                if (!canSee && distance < 9f) turnOffset = UnityEngine.Random.Range(-55f, 55f);
            }
            float moveSpeed = distance > 12f ? 2.85f : distance < 7f ? -1.15f : 0f;
            Vector3 desired = Quaternion.Euler(0f, turnOffset, 0f) * toward * moveSpeed;
            if (canSee) desired *= .23f; // Hold position to keep firing readable.
            Vector3 facing = distance > .05f ? toward : transform.forward;
            if (facing.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(facing), 1f - Mathf.Exp(-5f * dt));
            verticalSpeed = capsule.isGrounded ? -1.5f :
                Mathf.Max(-24f, verticalSpeed - 19.6f * dt);
            CollisionFlags hit = capsule.Move((desired +
                Vector3.up * verticalSpeed) * dt);
            if ((hit & CollisionFlags.Sides) != 0 && !canSee)
                turnOffset = -turnOffset + UnityEngine.Random.Range(-28f, 28f);

            if (hasAnimations)
            {
                float wanted = Mathf.Clamp01(Mathf.Abs(moveSpeed) / 2.9f);
                visualBlend = Mathf.MoveTowards(visualBlend, wanted, dt * 4f);
                animations.SetInputWeight(0, 1f - visualBlend);
                animations.SetInputWeight(1, visualBlend);
            }
            if (canSee && distance <= 38f &&
                Vector3.Dot(transform.forward, toward) > .84f &&
                Time.time >= nextAttack && HasSight())
            {
                // Offline damage contract, not real-world ballistics.
                nextAttack = Time.time + UnityEngine.Random.Range(1.15f, 1.65f);
                AarisAudioDirector.GunShot("SMG", "Rival", transform.position + Vector3.up);
                targetHealth.ApplyDamage(6f, gameObject);
            }
        }

        private bool HasSight()
        {
            Vector3 eye = transform.position + transform.forward * .48f + Vector3.up * 1.28f;
            Vector3 to = target.position + Vector3.up * 1.07f - eye;
            float distance = to.magnitude;
            if (distance < .1f) return false;
            Ray ray = new Ray(eye, to / distance);
            int count = Physics.RaycastNonAlloc(ray, sightHits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = sightHits;
            if (count >= sightHits.Length)
            {
                hits = Physics.RaycastAll(ray, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            float first = distance + 1f;
            Collider closest = null;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    continue;
                if (hit.distance < first)
                { first = hit.distance; closest = hit.collider; }
            }
            return closest != null && closest.transform.IsChildOf(target);
        }

        internal void TakeZoneDamage(float amount)
        {
            if (health != null && !health.IsDead) health.ApplyDamage(amount, null);
        }

        private void OnDied(AarisOfflineHealth defeated)
        {
            if (capsule != null) capsule.enabled = false;
            if (owner != null) owner.RivalDefeated(this);
            Destroy(gameObject, 1.2f);
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
