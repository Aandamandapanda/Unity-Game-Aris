using UnityEngine;
using UnityEngine.Rendering;

namespace Aaris.MapReview
{
    /// <summary>
    /// Shared bot gunfire presentation. Only true physically simulated
    /// near-range gunshots emit; virtual offscreen combat stays math-only.
    /// Up to six recyclable slots, with no per-bot renderer or Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AarisRivalGunfireVfx : MonoBehaviour
    {
        private const int MaximumSlots = 6;
        private sealed class Slot
        {
            internal LineRenderer tracer;
            internal Renderer flash, casing;
            internal Transform flashPose, casingPose;
            internal Vector3 origin, end, velocity, spin;
            internal float shotAt, tracerUntil, flashUntil, casingUntil, groundY;
            internal bool grounded;
        }

        private readonly Slot[] slots = new Slot[MaximumSlots];
        private static Material glowMaterial, brassMaterial, shotgunMaterial;
        private Transform observer;
        private int nextSlot;

        internal void Bind(Transform player) { observer = player; }

        private static Material MaterialFor(bool brass, bool shotgun = false)
        {
            Material cached = brass
                ? (shotgun ? shotgunMaterial : brassMaterial) : glowMaterial;
            if (cached != null) return cached;
            Shader shader = Shader.Find(brass ? "Universal Render Pipeline/Lit" :
                "Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;
            Color color = brass ? (shotgun
                ? new Color(.55f, .11f, .07f) : new Color(.85f, .63f, .24f)) :
                new Color(2.4f, 1.1f, .27f, .82f);
            cached = new Material(shader)
            {
                name = brass ? "Aaris Bot Casing Metal" : "Aaris Bot Gunfire Glow",
                enableInstancing = true
            };
            if (cached.HasProperty("_BaseColor")) cached.SetColor("_BaseColor", color);
            if (cached.HasProperty("_Color")) cached.SetColor("_Color", color);
            if (cached.HasProperty("_EmissionColor"))
            {
                cached.EnableKeyword("_EMISSION");
                cached.SetColor("_EmissionColor", color);
            }
            if (brass)
            {
                if (shotgun) shotgunMaterial = cached;
                else brassMaterial = cached;
            }
            else glowMaterial = cached;
            return cached;
        }

        private Slot CreateSlot()
        {
            var slot = new Slot();
            GameObject tracer = new GameObject("Aaris Bot Reused Tracer");
            tracer.transform.SetParent(transform, false);
            slot.tracer = tracer.AddComponent<LineRenderer>();
            slot.tracer.useWorldSpace = true;
            slot.tracer.positionCount = 2;
            slot.tracer.widthMultiplier = .008f;
            slot.tracer.alignment = LineAlignment.View;
            slot.tracer.startColor = new Color(1f, .93f, .63f, .84f);
            slot.tracer.endColor = new Color(1f, .58f, .16f, .12f);
            slot.tracer.sharedMaterial = MaterialFor(false);
            slot.tracer.shadowCastingMode = ShadowCastingMode.Off;
            slot.tracer.receiveShadows = false;
            slot.tracer.enabled = false;

            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Aaris Bot Reused Barrel Fire";
            flash.transform.SetParent(transform, false);
            slot.flashPose = flash.transform;
            slot.flash = flash.GetComponent<MeshRenderer>();
            slot.flash.sharedMaterial = MaterialFor(false);
            slot.flash.shadowCastingMode = ShadowCastingMode.Off;
            slot.flash.receiveShadows = false;
            slot.flash.enabled = false;
            var hitbox = flash.GetComponent<Collider>();
            if (hitbox != null) { hitbox.enabled = false; Destroy(hitbox); }

            var casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            casing.name = "Aaris Bot Reused Ejected Casing";
            casing.transform.SetParent(transform, false);
            slot.casingPose = casing.transform;
            slot.casing = casing.GetComponent<MeshRenderer>();
            slot.casing.sharedMaterial = MaterialFor(true);
            slot.casing.shadowCastingMode = ShadowCastingMode.Off;
            slot.casing.receiveShadows = false;
            slot.casing.enabled = false;
            hitbox = casing.GetComponent<Collider>();
            if (hitbox != null) { hitbox.enabled = false; Destroy(hitbox); }
            return slot;
        }

        internal void Emit(Transform shooter, Vector3 target, bool shotgun)
        {
            if (observer == null || shooter == null || Time.timeScale <= 0f ||
                (shooter.position - observer.position).sqrMagnitude > 70f * 70f)
                return;
            Vector3 from = shooter.position + Vector3.up * 1.29f +
                shooter.forward * .52f;
            float length = (target - from).magnitude;
            if (length < .18f) return;
            int index = nextSlot++ % MaximumSlots;
            Slot slot = slots[index];
            if (slot == null) slots[index] = slot = CreateSlot();
            float now = Time.time;
            slot.origin = from;
            slot.end = from + (target - from) *
                (Mathf.Min(length, 50f) / length);
            slot.shotAt = now;
            slot.tracerUntil = now +
                Mathf.Clamp(length / 350f, .025f, .14f);
            slot.flashUntil = now + .045f;
            slot.tracer.SetPosition(0, from);
            slot.tracer.SetPosition(1, from);
            slot.tracer.enabled = true;
            slot.flashPose.position = from;
            slot.flashPose.rotation = shooter.rotation;
            slot.flashPose.localScale = shotgun
                ? new Vector3(.22f, .13f, .30f)
                : new Vector3(.15f, .09f, .24f);
            slot.flash.enabled = true;

            slot.casing.sharedMaterial = MaterialFor(true, shotgun);
            Vector3 port = shooter.position + Vector3.up * 1.32f +
                shooter.forward * .22f + shooter.right * .18f;
            slot.casingPose.position = port;
            slot.casingPose.rotation = shooter.rotation * Quaternion.Euler(90f, 0f, 0f);
            slot.casingPose.localScale = shotgun
                ? new Vector3(.008f, .023f, .008f)
                : new Vector3(.006f, .015f, .006f);
            slot.velocity = shooter.right * 2.25f + Vector3.up * 1.65f -
                shooter.forward * .2f;
            slot.spin = new Vector3(520f, 940f, 650f);
            slot.grounded = false;
            // One ground probe on the accepted local gunshot, none on the Update loop.
            if (Physics.Raycast(port + Vector3.up * .1f, Vector3.down,
                out RaycastHit ground, 5.5f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && ground.normal.y > .45f)
                slot.groundY = ground.point.y + .012f;
            else slot.groundY = shooter.position.y + .012f;
            slot.groundY = Mathf.Min(slot.groundY, port.y - .07f);
            slot.casingUntil = now + .85f;
            slot.casing.enabled = true;
        }

        private void Update()
        {
            float now = Time.time;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot == null) continue;
                if (slot.flash.enabled && now >= slot.flashUntil)
                    slot.flash.enabled = false;
                if (slot.tracer.enabled)
                {
                    if (now >= slot.tracerUntil) slot.tracer.enabled = false;
                    else
                    {
                        float head = Mathf.Clamp01((now - slot.shotAt) /
                            Mathf.Max(.001f, slot.tracerUntil - slot.shotAt));
                        Vector3 segment = slot.end - slot.origin;
                        float metres = segment.magnitude;
                        float tail = Mathf.Max(0f, head - 1.65f / Mathf.Max(.01f, metres));
                        slot.tracer.SetPosition(0, slot.origin + segment * tail);
                        slot.tracer.SetPosition(1, slot.origin + segment * head);
                    }
                }
                if (!slot.casing.enabled) continue;
                if (now >= slot.casingUntil)
                {
                    slot.casing.enabled = false;
                    continue;
                }
                if (slot.grounded) continue;
                slot.velocity += Physics.gravity * dt;
                Vector3 next = slot.casingPose.position + slot.velocity * dt;
                if (next.y <= slot.groundY && slot.velocity.y < 0f)
                {
                    next.y = slot.groundY;
                    slot.velocity = new Vector3(
                        slot.velocity.x * .58f,
                        -slot.velocity.y * .28f,
                        slot.velocity.z * .58f);
                    slot.spin *= .45f;
                    if (slot.velocity.y < .5f) slot.grounded = true;
                }
                slot.casingPose.position = next;
                slot.casingPose.Rotate(slot.spin * dt, Space.World);
            }
        }
    }
}
