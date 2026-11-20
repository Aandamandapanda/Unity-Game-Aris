"""Source contract for Android native teardown and next-frame admission.

These static tests are not a substitute for Unity compilation or device profiling.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "AarisWorld" / "Scripts"


class NativeTeardownSettleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.gate = (SCRIPTS / "AarisMobileStreamingFrameGate.cs").read_text()
        cls.vehicles = (SCRIPTS / "AarisVehicleWorld.cs").read_text()
        cls.bikes = (SCRIPTS / "AarisRoadsideBikeScenery.cs").read_text()

    def test_android_only_one_frame_fence(self):
        self.assertIn("nativeTeardownSettleFrame = -1;", self.gate)
        self.assertIn("nativeTeardownSettleFrame = Time.frameCount + 1;", self.gate)
        method = self.gate.split("internal static void ReserveNativeTeardownFrame()", 1)[1]
        method = method.split("private static bool IsNativeTeardownSettleFrame()", 1)[0]
        self.assertIn("#if UNITY_ANDROID && !UNITY_EDITOR", method)
        self.assertIn("if (!IsConstrainedDevice()) return;", method)

    def test_optional_load_and_diagnostics_admission(self):
        self.assertIn("if (IsNativeTeardownSettleFrame()) return false;", self.gate)
        self.assertIn("IsNativeTeardownSettleFrame()) return false;", self.gate)
        self.assertIn("if (!TryClaimOptionalModelBuild()) return null;", self.gate)

    def test_streamers_reserve_native_teardown_fence(self):
        self.assertGreaterEqual(self.vehicles.count("ReserveNativeTeardownFrame();"), 2)
        self.assertGreaterEqual(self.bikes.count("ReserveNativeTeardownFrame();"), 3)

    def test_no_vegetation_or_view_distance_edits(self):
        for code in (self.gate, self.vehicles, self.bikes):
            self.assertNotIn("QualitySettings.masterTextureLimit =", code)
        self.assertIn("dist2 > UnloadRadius * UnloadRadius", self.vehicles)


if __name__ == "__main__":
    unittest.main()
