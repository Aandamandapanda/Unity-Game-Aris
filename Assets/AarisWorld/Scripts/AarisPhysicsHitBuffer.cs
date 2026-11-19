using UnityEngine;

namespace Aaris.MapReview
{
    // Main-thread query scratch only. Retain a bounded larger buffer after
    // saturation so dense cover does not allocate an All() result every frame.
    // Exactly-full NonAlloc results are incomplete, even after promotion.
    internal static class AarisPhysicsHitBuffer
    {
        internal static int Raycast(Ray ray, float distance, int mask,
            ref RaycastHit[] retained, int retainedLimit, out RaycastHit[] hits)
        {
            int count = Physics.RaycastNonAlloc(ray, retained, distance,
                mask, QueryTriggerInteraction.Ignore);
            if (count >= retained.Length && retained.Length < retainedLimit)
            {
                // Promote once, lazily, for this caller. Subsequent queries
                // start directly with the larger buffer, not a small retry.
                retained = new RaycastHit[retainedLimit];
                count = Physics.RaycastNonAlloc(ray, retained, distance,
                    mask, QueryTriggerInteraction.Ignore);
            }
            hits = retained;
            if (count >= retained.Length)
            {
                // Hard cap applies to retained scratch, never to correctness.
                // Preserve every hit when even the larger buffer saturates.
                hits = Physics.RaycastAll(ray, distance,
                    mask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            return count;
        }

        // NonAlloc sphere overlaps are unordered just like ray/sweep queries.
        // Keep a caller-owned growable scratch array for both grenade endpoints.
        internal static int OverlapSphere(Vector3 position, float radius, int mask,
            ref Collider[] retained, int retainedLimit, out Collider[] hits)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, retained,
                mask, QueryTriggerInteraction.Ignore);
            if (count >= retained.Length && retained.Length < retainedLimit)
            {
                retained = new Collider[retainedLimit];
                count = Physics.OverlapSphereNonAlloc(position, radius, retained,
                    mask, QueryTriggerInteraction.Ignore);
            }
            hits = retained;
            if (count >= retained.Length)
            {
                // A full 128-hit result must NEVER incorrectly approve release.
                hits = Physics.OverlapSphere(position, radius, mask,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            return count;
        }

        internal static int SphereCast(Ray ray, float radius, float distance,
            int mask, ref RaycastHit[] retained, int retainedLimit,
            out RaycastHit[] hits)
        {
            int count = Physics.SphereCastNonAlloc(ray, radius, retained, distance,
                mask, QueryTriggerInteraction.Ignore);
            if (count >= retained.Length && retained.Length < retainedLimit)
            {
                retained = new RaycastHit[retainedLimit];
                count = Physics.SphereCastNonAlloc(ray, radius, retained, distance,
                    mask, QueryTriggerInteraction.Ignore);
            }
            hits = retained;
            if (count >= retained.Length)
            {
                hits = Physics.SphereCastAll(ray, radius, distance,
                    mask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            return count;
        }
    }
}
