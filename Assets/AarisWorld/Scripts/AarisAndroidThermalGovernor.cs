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
        // -1 = unsupported/unobserved. 1.0 predicts severe thermal pressure.
        internal static float ForecastThermalHeadroom { get; private set; } = -1f;
        internal static bool SuspendSpeculativePrefetch { get; private set; }
        internal static bool ModerateThermalPressure =>
            ThermalStatus >= 2 || ForecastThermalHeadroom >= 0.95f;

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android PowerManager.THERMAL_STATUS_MODERATE = 2;
        // THERMAL_STATUS_SEVERE = 3. Only severe pressure stops speculative work.
        // Three *consecutive* cool samples avoid rapid load/unload oscillation.
        private const int SevereStatus = 3;
        private const int CoolRecoverySamples = 3;
        private const float SampleIntervalSeconds = 5f;
        // Android recommends headroom no more than once per ten seconds;
        // one call per three OS-status polls gives a 15-second interval.
        private const int HeadroomPollPeriod = 3;
        private static AarisAndroidThermalGovernor active;
        private AndroidJavaObject powerManager;
        private int coolSamples, statusPolls;
        private bool supportsHeadroom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            active = null;
            ThermalStatus = -1;
            ForecastThermalHeadroom = -1f;
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
            if (!InitializePowerManager()) yield break;
            while (isActiveAndEnabled)
            {
                if (!SampleThermalStatus()) yield break;
                yield return delay;
            }
        }

        private bool InitializePowerManager()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    int sdk = version.GetStatic<int>("SDK_INT");
                    if (sdk < 29)
                    {
                        enabled = false;
                        return false;
                    }
                    supportsHeadroom = sdk >= 30;
                }
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) throw new InvalidOperationException("no-activity");
                    powerManager = activity.Call<AndroidJavaObject>("getSystemService", "power");
                    if (powerManager == null) throw new InvalidOperationException("no-power-manager");
                }
                return true;
            }
            catch (Exception ex)
            {
                DisableUnsupported(ex);
                return false;
            }
        }

        private bool SampleThermalStatus()
        {
            try
            {
                int status = powerManager.Call<int>("getCurrentThermalStatus");
                if (supportsHeadroom && statusPolls++ % HeadroomPollPeriod == 0)
                {
                    try
                    {
                        // 15s forecast: proactively sheds predicted-only IO
                        // before the OS reports severe throttling on some OEMs.
                        float headroom = powerManager.Call<float>("getThermalHeadroom", 15);
                        if (float.IsNaN(headroom) || float.IsInfinity(headroom) ||
                            headroom < 0f)
                        {
                            supportsHeadroom = false;
                            ForecastThermalHeadroom = -1f;
                        }
                        else ForecastThermalHeadroom = headroom;
                    }
                    catch (AndroidJavaException)
                    {
                        // Status remains useful on OEMs missing headroom.
                        supportsHeadroom = false;
                        ForecastThermalHeadroom = -1f;
                    }
                }
                ObserveStatus(status);
                return true;
            }
            catch (Exception ex)
            {
                DisableUnsupported(ex);
                return false;
            }
        }

        private void ObserveStatus(int status)
        {
            if (status < 0 || status > 6) return;
            ThermalStatus = status;
            if (status >= SevereStatus || ForecastThermalHeadroom >= 1f)
            {
                SuspendSpeculativePrefetch = true;
                coolSamples = 0;
                return;
            }
            if (status >= 2 || ForecastThermalHeadroom >= 0.95f)
            {
                // MODERATE/forecast warning retains one speculative cell.
                // No actual-position scene or game mechanic is deferred.
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
            ForecastThermalHeadroom = -1f;
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
                ForecastThermalHeadroom = -1f;
                SuspendSpeculativePrefetch = false;
            }
        }
#endif
    }
}
