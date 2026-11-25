#ifndef AARIS_GRASS_PHASE_INCLUDED
#define AARIS_GRASS_PHASE_INCLUDED

float AarisSmoothWindWave(float phase)
{
    float triangleValue = 1.0 - abs(2.0 * frac(phase) - 1.0);
    return triangleValue * triangleValue * (3.0 - 2.0 * triangleValue);
}

// DaylightWind in the native scene points along +Z. The low-frequency
// weather envelope is shared by all materials and trees; species retain
// their authored heading, wavelength and speed for the finer local ripple.
// Full precision and a continuous render clock avoid phase stepping on GLES.
float AarisLandscapePhase(float2 pivotXZ)
{
    return dot(pivotXZ, float2(0.011, 0.035));
}

float AarisLandscapeGust(float fieldPhase, float timeSeconds)
{
    return AarisSmoothWindWave(fieldPhase - timeSeconds * 0.18);
}

float AarisWindDistanceFade(float distanceSq, float fullSq, float endSq)
{
    float fade = saturate((endSq - distanceSq) / (endSq - fullSq));
    return fade * fade * (3.0 - 2.0 * fade);
}

// Wind supplies a unit heading and Speed scaled by Period.
void AarisGrassPhase_float(float3 WindDirection, float3 PivotWS, float TimeSeconds,
    float Period, float Speed, float InstancePhase, float Distortion, float Ripple,
    out float Phase)
{
    float2 heading = WindDirection.xz;
    float2 crosswind = float2(-heading.y, heading.x);
    float2 scaledPivot = PivotWS.xz / max(Period, 4.0);
    float along = dot(scaledPivot, heading);
    float across = dot(scaledPivot, crosswind);
    float clock = TimeSeconds * (Speed / max(Period, 0.001)) * 0.15915494;

    // Freeze the weather phase for an explicitly zero-speed material.
    // Otherwise, neighboring species share a slowly travelling gust.
    float gust = AarisLandscapeGust(AarisLandscapePhase(PivotWS.xz), TimeSeconds * step(0.0001, abs(Speed)));
    float flutter = AarisSmoothWindWave(along * 0.83 - clock * 0.72
        + across * (0.14 + 0.09 * saturate(Distortion)) + InstancePhase * 0.37);
    Phase = 0.11 + 0.58 * gust + (0.13 + 0.16 * saturate(Ripple)) * flutter;
}

#endif
