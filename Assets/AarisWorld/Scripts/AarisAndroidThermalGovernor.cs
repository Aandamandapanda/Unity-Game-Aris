using System;
using System.Collections;
using UnityEngine;

namespace Aaris.MapReview
{
    /// <summary>
    /// Android PowerManager thermal telemetry for optional, ahead-of-player
    /// streaming only. It never reduces texture/mesh fidelity, visible range,
    /// collisions, player controls or authoritative near-player loads.
    /// Queries are infrequent and only after the first rendered scene.
    /// Unsupported OEM implementations fail OPEN to the existing scheduler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AarisAndroidThermalGovernor : MonoBehaviour
    {
        internal static int ThermalStatus { get; private set; } = -1;
        internal static bool SuspendSpeculativePrefetch { get; private set; }
        internal static bool ModerateThermalPressure =>
            ThermalStatus >= 2 && ThermalStatus <= 6;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android PowerManager.THERMAL_STATUS_MODERATE = 2;
        // THERMAL_STATUS_SEVERE = 3. Only severe pressure stops speculative work.
        // Three *consecutive* cool samples avoid rapid load/unload oscillation.
        private const int SevereStatus = 3;
        private const int CoolRecoverySamples = 3;
        private const float SampleIntervalSeconds = 5f;
        private static AarisAndroidThermalGovernor active;
        private AndroidJavaObject powerManager;
        private int coolSamples;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            active = null;
            ThermalStatus = -1;
            SuspendSpeculativePrefetch = false;
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                enabled = false;
                return;
            }
            active = this;
        }

        private IEnumerator Start()
        {
            // No Java class lookup, binder call or extra work on the opening frame.
            var delay = new WaitForSecondsRealtime(SampleIntervalSeconds);
            yield return delay;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") < 29)
                    {
                        enabled = false;
                        yield break;
                    }
                }
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) throw new InvalidOperationException("no-activity");
                    powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power");
                    if (powerManager == null) throw new InvalidOperationException("no-power-manager");
                }
            }
            catch (Exception ex)
            {
                // Some OEMs expose no thermal service. Never punish world detail.
                DisableUnsupported(ex);
                yield break;
            }

            while (isActiveAndEnabled)
            {
                try
                {
                    int status = powerManager.Call<int>("getCurrentThermalStatus");
                    ObserveStatus(status);
                }
                catch (Exception ex)
                {
                    DisableUnsupported(ex);
                    yield break;
                }
                yield return delay;
            }
        }

        private void ObserveStatus(int status)
        {
            if (status < 0 || status > 6) return;
            ThermalStatus = status;
            if (status >= SevereStatus)
            {
                SuspendSpeculativePrefetch = true;
                coolSamples = 0;
                return;
            }
            if (status >= 2)
            {
                // MODERATE retains current prefetch, but limits speculative
                // residency in the chunk streamer to one cell.
                coolSamples = 0;
                return;
            }
            if (SuspendSpeculativePrefetch &&
                ++coolSamples >= CoolRecoverySamples)
            {
                SuspendSpeculativePrefetch = false;
                coolSamples = 0;
            }
        }

        private void DisableUnsupported(Exception ex)
        {
            // One bounded warning, no exception/retry storm every frame.
            Debug.LogWarning("AARIS_THERMAL_UNAVAILABLE: " + ex.GetType().Name);
            enabled = false;
            ThermalStatus = -1;
            SuspendSpeculativePrefetch = false;
        }

        private void OnDestroy()
        {
            if (powerManager != null)
            {
                powerManager.Dispose();
                powerManager = null;
            }
            if (active == this)
            {
                active = null;
                ThermalStatus = -1;
                SuspendSpeculativePrefetch = false;
            }
        }
#endif
    }
}
