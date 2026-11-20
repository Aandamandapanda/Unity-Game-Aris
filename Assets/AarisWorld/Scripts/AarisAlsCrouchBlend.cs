using UnityEngine;

namespace Aaris.MapReview
{
    // Crouch animation blend adapted from ALS-Unity (MIT)
    // https://github.com/schyro/ALS-Unity
    // Source: Assets/ALS/Scripts/Runtime/ALSCharacterAnimation.cs,
    //         Assets/ALS/Scripts/Runtime/ALSMath.cs
    // Copyright (c) 2026 Serkan Yalnız. See Docs/ThirdParty/ALS_Unity_MIT_LICENSE.txt.
    //
    // Use ALS's separate crouch idle/move states and direction interpolation,
    // without replacing this game's single CharacterController or foot solver.
    internal static class AarisAlsCrouchBlend
    {
        internal const float StanceBlendSeconds = 0.25f;
        internal const float NominalForwardMetresPerSecond = 0.72f;
        internal const float DirectionInterpSpeed = 10f;

        // ALSMath.InterpTo (MIT), used for gradual crouch-idle/move weights.
        internal static float InterpTo(float current, float target, float deltaTime, float speed)
        {
            if (speed <= 0f) return target;
            float dist = target - current;
            if (dist * dist < 1e-8f) return target;
            return current + dist * Mathf.Clamp01(deltaTime * speed);
        }

        internal static float MovingWeight(float previous, float horizontalSpeed, float deltaTime)
        {
            float target = Mathf.InverseLerp(0.06f, NominalForwardMetresPerSecond,
                Mathf.Max(0f, horizontalSpeed));
            return InterpTo(previous, target, deltaTime, DirectionInterpSpeed);
        }
    }
}
