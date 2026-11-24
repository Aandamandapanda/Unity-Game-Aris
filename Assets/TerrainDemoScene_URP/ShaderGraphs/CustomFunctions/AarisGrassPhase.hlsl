#ifndef AARIS_GRASS_PHASE_INCLUDED
#define AARIS_GRASS_PHASE_INCLUDED

float AarisSmoothWindWave(float phase)
{
    float triangleValue = 1.0 - abs(2.0 * frac(phase) - 1.0);
    return triangleValue * triangleValue * (3.0 - 2.0 * triangleValue);
}

// Wind supplies a unit heading and Speed scaled by Period. The pivot and
// time stay at full precision so cards share a phase anywhere on the island.
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

    float gust = AarisSmoothWindWave(along * 0.35 - clock * 0.28
        + across * (0.07 + 0.09 * saturate(Distortion)));
    float flutter = AarisSmoothWindWave(along * 0.83 - clock * 0.72
        + across * 0.14 + InstancePhase * 0.37);
    Phase = 0.11 + 0.58 * gust + (0.13 + 0.16 * saturate(Ripple)) * flutter;
}

#endif
