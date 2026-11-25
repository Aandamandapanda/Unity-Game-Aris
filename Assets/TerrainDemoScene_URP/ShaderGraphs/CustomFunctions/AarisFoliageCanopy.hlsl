#ifndef AARIS_FOLIAGE_CANOPY_INCLUDED
#define AARIS_FOLIAGE_CANOPY_INCLUDED

// Object-space canopy lighting, shared by native SpeedTree leaves and
// instanced terrain shrubs. Stable undeformed coordinates, no sampler,
// per-instance material override, extra varying, or fragment normal search.
float3 AarisUnitOr(float3 v, float3 fallback)
{
    float lenSq = dot(v, v);
    return lenSq > 1e-8 ? v * rsqrt(max(lenSq, 1e-8)) : fallback;
}

float3 AarisVolumeNormalOS(float3 authored, float3 gradient, float strength)
{
    float3 n = AarisUnitOr(authored, float3(0.0, 1.0, 0.0));
    float3 crown = AarisUnitOr(gradient, float3(0.0, 1.0, 0.0));
    // Reflect only the inward component; continuous at a tangent plane.
    // This prevents cancellation for cards whose normals originally face
    // inside the canopy while retaining their authored local detail.
    n -= 2.0 * min(dot(n, crown), 0.0) * crown;
    return AarisUnitOr(lerp(n, crown, saturate(strength)), crown);
}

float3 AarisCanopyTangentOS(float3 tangent, float3 normal)
{
    // Keep tangent-space normal maps valid after changing the basis.
    float3 projected = tangent - normal * dot(tangent, normal);
    float lenSq = dot(projected, projected);
    UNITY_BRANCH if (lenSq > 1e-8)
        return projected * rsqrt(lenSq);
    float3 axis = abs(normal.y) < 0.90 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    return AarisUnitOr(cross(axis, normal), float3(0.0, 0.0, 1.0));
}

// Shader Graph vertex-only: profile = (volume strength, crown centre Y,
// vertical ellipsoid-gradient weight, original albedo multiplier).
// Ordinary grass retains its prior normal/tangent path. Far shrubs converge
// into the graph's authored upward normal instead of changing LOD colour.
void AarisDetailCanopyFrame_float(float3 BasePositionOS, float3 OriginalNormalOS,
    float3 OriginalTangentOS, float DistanceFade, float MotionFade, float4 Profile, float3 BendSlopeOS,
    out float3 NormalOS, out float3 TangentOS)
{
    NormalOS = OriginalNormalOS;
    TangentOS = OriginalTangentOS;
    UNITY_BRANCH if (Profile.x > 0.001)
    {
        // Reuse the existing continuous wind-distance interpolation. After
        // the authored motion cutoff, skip crown/tangent reconstruction.
        float far = saturate((MotionFade - 0.64) * 2.7777778);
        far = far * far * (3.0 - 2.0 * far);
        float fade = max(saturate(DistanceFade), far);
        UNITY_BRANCH if (fade >= 0.999)
        {
            NormalOS = float3(0.0, 1.0, 0.0);
            TangentOS = AarisUnitOr(float3(OriginalTangentOS.x, 0.0, OriginalTangentOS.z),
                float3(1.0, 0.0, 0.0));
            return;
        }
        float3 gradient = float3(BasePositionOS.x,
            (BasePositionOS.y - Profile.y) * max(Profile.z, 0.05), BasePositionOS.z);
        // The base remains shaded, but no nearly vertical downward card
        // creates a black underside in the mobile canopy.
        gradient.y = max(gradient.y, -0.55 * max(abs(gradient.x), abs(gradient.z)));
        float3 volume = AarisVolumeNormalOS(OriginalNormalOS, gradient, Profile.x);
        // The contact offset is quadratic in rest height. Apply its local
        // shear to the tangent and its inverse transpose to the normal.
        UNITY_BRANCH if (dot(BendSlopeOS, BendSlopeOS) > 1e-8)
        {
            volume.y -= dot(volume, BendSlopeOS) / (1.0 + BendSlopeOS.y);
            volume = AarisUnitOr(volume, float3(0.0, 1.0, 0.0));
            OriginalTangentOS += BendSlopeOS * OriginalTangentOS.y;
        }
        NormalOS = AarisUnitOr(lerp(volume, float3(0.0, 1.0, 0.0),
            fade), float3(0.0, 1.0, 0.0));
        TangentOS = AarisCanopyTangentOS(OriginalTangentOS, NormalOS);
    }
}
#endif
