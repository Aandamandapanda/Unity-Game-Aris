#!/usr/bin/env python3
"""Contracts for the opt-in, non-invasive mobile CSV performance overlay."""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[2] / "Assets" / "AarisWorld"
          / "Scripts" / "AarisMobilePerfOverlay.cs").read_text(encoding="utf-8")
VEHICLES = (Path(__file__).resolve().parents[2] / "Assets" / "AarisWorld"
            / "Scripts" / "AarisVehicleWorld.cs").read_text(encoding="utf-8")


class MobilePerfOverlayContract(unittest.TestCase):
    def test_separate_opt_in_runtime_component(self):
        self.assertIn("sealed class AarisMobilePerfOverlay : MonoBehaviour", SOURCE)
        self.assertIn("RuntimeInitializeLoadType.AfterSceneLoad", SOURCE)
        self.assertIn("if (!overlayVisible && !recording && !capturing)", SOURCE)
        self.assertIn("F8", SOURCE)
        self.assertIn("fingers == 3 || fingers == 4", SOURCE)

    def test_constant_memory_sampling_and_percentiles(self):
        self.assertIn("new float[SampleCapacity]", SOURCE)
        self.assertIn("Array.Sort(sortedFramesMs, 0, frameCount);", SOURCE)
        self.assertIn("Percentile(0.95f)", SOURCE)
        self.assertIn("Percentile(0.99f)", SOURCE)
        self.assertIn("Time.unscaledDeltaTime * 1000f", SOURCE)

    def test_vehicle_counters_use_the_active_world_at_capture_start(self):
        self.assertNotIn("FindFirstObjectByType", SOURCE)
        self.assertIn("vehicleWorld = AarisVehicleWorld.ActiveWorld;", SOURCE)
        self.assertIn("vehicleWorld.FreshVehicleBuilds - captureFreshAtStart", SOURCE)
        self.assertIn("vehicleWorld.ReusedVehicleBuilds - captureReusedAtStart", SOURCE)
        self.assertIn("private static void ResetActiveWorld() => ActiveWorld = null;", VEHICLES)
        self.assertIn("ActiveWorld = this;", VEHICLES.split("private void OnEnable()", 1)[1].split(
            "private void OnDisable()", 1)[0])
        self.assertIn("if (ActiveWorld == this) ActiveWorld = null;", VEHICLES)

    def test_async_bounded_csv_and_honest_counter_fallbacks(self):
        self.assertIn("Task.Run(() =>", SOURCE)
        self.assertIn("MaxPendingCsvChars = 65536", SOURCE)
        self.assertIn("Path.Combine(Application.persistentDataPath", SOURCE)
        self.assertIn('cpuMs < 0 ? "n/a"', SOURCE)
        self.assertIn("Profiler.GetTotalReservedMemoryLong()", SOURCE)
        self.assertIn("mainThreadRecorder.Valid", SOURCE)
        self.assertIn("gcAllocRecorder.Valid", SOURCE)
        self.assertNotIn(".IsValid", SOURCE)
        self.assertNotIn("Screen.SetResolution", SOURCE)
        self.assertNotIn("renderScale =", SOURCE)
        self.assertNotIn("QualitySettings.lodBias =", SOURCE)


if __name__ == "__main__":
    unittest.main(verbosity=2)
