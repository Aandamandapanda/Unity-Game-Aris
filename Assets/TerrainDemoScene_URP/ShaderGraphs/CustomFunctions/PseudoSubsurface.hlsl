#ifndef AARIS_PSEUDO_SUBSURFACE_INCLUDED
#define AARIS_PSEUDO_SUBSURFACE_INCLUDED

// The detail graph's existing height AO already supplies Height01. Use the
// same vertex-sampled terrain tint as its distance blend to ground nearby stems.
void AarisDetailGround_half(half3 LeafColor, half3 TerrainColor, half DistanceFade,
    half Height01, out half3 Out)
{
    half contact = saturate(1.0h - 2.0h * Height01);
    // Smooth at both ends: no abrupt color band around the ground contact.
    half rootBlend = 0.38h * contact * contact * (3.0h - 2.0h * contact);
    half3 rootedColor = lerp(LeafColor, TerrainColor * half3(0.88h, 0.83h, 0.75h), rootBlend);
    Out = lerp(rootedColor, TerrainColor, DistanceFade);
}

// Mobile detail transmission using the scene's main light, without another
// shadow sample. The regular URP Lit graph still handles direct shadows.
void PseudoSubsurface_half(float3 WorldPosition, half3 WorldNormal, half3 SSRadius,
                           half ShadowResponse, out half3 ssAmount)
{
#ifdef SHADERGRAPH_PREVIEW
    half3 color = half3(0.0h, 0.0h, 0.0h);
    half3 ambient = half3(0.0h, 0.0h, 0.0h);
    half3 lightDir = normalize(half3(0.707h, 0.5h, 0.707h));
#else
    // Directional lighting only; geometry occlusion stays in the regular URP
    // material lighting path rather than triggering a second shadow lookup.
    Light mainLight = GetMainLight();
    half3 color = mainLight.color * saturate(mainLight.distanceAttenuation);
    half3 lightDir = mainLight.direction;
    // Reuse the constant band of the already-bound spherical harmonics.
    // Shade fill must not be tinted/scaled by the direct sunlight itself.
    // No new shadow lookup, reflection sample or SH polynomial evaluation.
    half3 ambient = max(half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 0.0h);
#endif
    half3 n = normalize(WorldNormal);
    half radius = saturate(SSRadius.x);
    half back = saturate((dot(n, -lightDir) + radius) / (1.0h + radius));
    half wrapped = back * back * (3.0h - 2.0h * back);
    half edge = 1.0h - saturate(abs(dot(n, lightDir)));
    half rim = edge * edge;
    half skyFill = 0.085h * saturate(n.y * 0.5h + 0.5h);
    half transmission = 0.80h * wrapped + 0.12h * rim;
    half3 warmSun = half3(1.04h, 1.0h, 0.86h);
    half3 coolFill = half3(0.94h, 1.0h, 1.06h);
    half response = 1.0h - 0.12h * saturate(ShadowResponse);
    ssAmount = max(0.0h, color * response * transmission * warmSun
        + ambient * skyFill * coolFill);
}

#endif
