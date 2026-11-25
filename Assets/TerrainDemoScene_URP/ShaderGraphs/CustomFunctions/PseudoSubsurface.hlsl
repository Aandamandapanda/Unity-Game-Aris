#ifndef AARIS_PSEUDO_SUBSURFACE_INCLUDED
#define AARIS_PSEUDO_SUBSURFACE_INCLUDED

// Reuse the graph's vertex-sampled terrain colour, authored height AO and
// already-defined stable instance seed. The species profile preserves red
// and dry foliage, rather than applying a universal green overlay.
void AarisDetailGround_half(half3 LeafColor, half3 TerrainColor, half DistanceFade,
    half Height01, half InstanceSeed, half4 Profile, out half3 Out, out half3 TransmissionColor)
{
    half contact = saturate(1.0h - 2.0h * Height01);
    half rootBlend = 0.38h * contact * contact * (3.0h - 2.0h * contact);
    half variation = (saturate(InstanceSeed) - 0.5h) * saturate(Profile.x);
    half3 localColor = LeafColor * Profile.w *
        (1.0h + variation * half3(0.10h, 0.07h, -0.05h));
    half3 rootedColor = lerp(localColor, TerrainColor * half3(0.88h, 0.83h, 0.75h), rootBlend);
    Out = lerp(rootedColor, TerrainColor, saturate(DistanceFade));
    // Suppress artificial emission near soil and far silhouettes. Existing
    // thickness, AO and normal textures continue to define the detail.
    half heightMask = saturate(Height01);
    heightMask = heightMask * heightMask * (3.0h - 2.0h * heightMask);
    TransmissionColor = Out * (0.20h + 0.80h * heightMask) *
        (1.0h - 0.75h * saturate(DistanceFade));
}

void AarisDetailSunNormal_half(half3 CanopyNormalWS, half3 FaceNormalWS,
    half4 Profile, out half3 Out)
{
    Out = lerp(FaceNormalWS, CanopyNormalWS, step(0.001h, Profile.x));
}

// Bounded, warm transmission from the scene's existing directional light.
// URP Lit handles real direct-light shadows; this inexpensive approximate
// lobe must not imply an independent realtime shadow sample or self-glow.
void PseudoSubsurface_half(float3 WorldPosition, half3 WorldNormal, half3 SSRadius,
                           half ShadowResponse, out half3 ssAmount)
{
#ifdef SHADERGRAPH_PREVIEW
    half3 color = half3(0.0h, 0.0h, 0.0h);
    half3 ambient = half3(0.0h, 0.0h, 0.0h);
    half3 lightDir = normalize(half3(0.707h, 0.5h, 0.707h));
#else
    Light mainLight = GetMainLight();
    half3 color = mainLight.color * saturate(mainLight.distanceAttenuation);
    half3 lightDir = mainLight.direction;
    half3 ambient = max(half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), 0.0h);
#endif
    half3 n = SafeNormalize(WorldNormal);
    // Authored SSS values range from 0.5 to 4, not just 0 to 1.
    half radius = saturate(0.25h * SSRadius.x);
    half ndl = dot(n, lightDir);
    half back = saturate((radius - ndl) / (1.0h + radius));
    half wrapped = back * back * (3.0h - 2.0h * back);
    half edge = 1.0h - saturate(abs(ndl));
    half rim = edge * edge;
    half skyFill = 0.055h * saturate(n.y * 0.5h + 0.5h);
    half transmission = (0.14h + 0.23h * radius) * wrapped + 0.10h * rim;
    half3 warmSun = half3(1.04h, 1.0h, 0.86h);
    half3 coolFill = half3(0.94h, 1.0h, 1.06h);
    half response = 1.0h - 0.20h * saturate(ShadowResponse);
    ssAmount = max(0.0h, color * response * transmission * warmSun
        + ambient * skyFill * coolFill);
}

#endif
