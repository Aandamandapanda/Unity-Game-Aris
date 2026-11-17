#!/usr/bin/env python3
"""Frame-pacing contract for the noncombat vehicle chase camera.

These deterministic maths checks do not claim Unity Play Mode coverage.
"""
import math
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SESSION = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleSession.cs").read_text("utf-8")
CAMERA = SESSION[SESSION.index("private void FollowCarCamera()"):
                 SESSION.index("private void ClearHeldDrivingInput()")]
MAX_FRAME_SECONDS = 0.15


class VehicleCameraPacing(unittest.TestCase):
    def test_one_bounded_time_step_for_position_rotation_and_fov(self):
        self.assertIn("float cameraFrameDt = Mathf.Clamp(Time.deltaTime, 0f, .15f);", CAMERA)
        self.assertIn(".12f, Mathf.Infinity, cameraFrameDt);", CAMERA)
        self.assertIn("1f - Mathf.Exp(-10f * cameraFrameDt)", CAMERA)
        self.assertIn("float fovDelta = cameraFrameDt;", CAMERA)
        self.assertNotIn("Mathf.Min(.05f, Time.deltaTime)", CAMERA)
        self.assertEqual(CAMERA.count("float cameraFrameDt ="), 1)

    def test_rotation_and_fov_recover_in_real_time_at_10_to_120_fps(self):
        for fps in (10, 15, 20, 30, 45, 60, 90, 120):
            dt = min(MAX_FRAME_SECONDS, 1.0 / fps)
            rotation = fov = 0.0
            for _ in range(fps):
                rotation += (1.0 - rotation) * (1.0 - math.exp(-10.0 * dt))
                fov += (1.0 - fov) * (1.0 - math.exp(-5.0 * dt))
            self.assertAlmostEqual(rotation, 1.0 - math.exp(-10.0), places=7)
            self.assertAlmostEqual(fov, 1.0 - math.exp(-5.0), places=7)

    def test_hitches_are_bounded_but_normal_low_fps_is_not_shortened(self):
        for dt in (0.0, .001, .016, .033, .067, .100, .150, .200, .500, 5.0):
            bounded = max(0.0, min(MAX_FRAME_SECONDS, dt))
            self.assertGreaterEqual(bounded, 0.0)
            self.assertLessEqual(bounded, MAX_FRAME_SECONDS)
            if 0.0 <= dt <= MAX_FRAME_SECONDS:
                self.assertAlmostEqual(bounded, dt)


if __name__ == "__main__":
    unittest.main(verbosity=2)
