#!/usr/bin/env python3
"""Offline automotive polish regression; not proof of a Unity render or drive."""
import json
import math
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORLD = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleWorld.cs").read_text()
MOTOR = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleMotor.cs").read_text()
SESSION = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleSession.cs").read_text()
CATALOG = json.loads((ROOT / "Assets/Resources/AarisVehicles/vehicle_catalog.json").read_text())


class AutomotiveRefinement(unittest.TestCase):
    def test_catalog_has_distinct_stroke_profiles_and_safe_bounds(self):
        ride = {v["id"]: v["suspensionTravelScale"] for v in CATALOG["vehicles"]}
        self.assertEqual(len(ride), 7)
        self.assertLess(ride["Sports"], ride["Sedan"])
        self.assertGreater(ride["SUV"], ride["Sedan"])
        self.assertGreater(ride["Buggy"], ride["SUV"])
        self.assertLessEqual(max(ride.values()), 1.25)
        for factor in ride.values():
            stroke = min(.31, max(.18, .24 * factor))
            self.assertTrue(math.isfinite(stroke))
            self.assertGreaterEqual(stroke, .18)
            self.assertLessEqual(stroke, .31)
        self.assertIn("motor.ConfigureHandling(record.type.drivetrain,", WORLD)
        self.assertIn("record.type.steeringScale, record.type.suspensionTravelScale)", WORLD)
        self.assertIn("wheel.suspensionDistance * travelScale", MOTOR)
        self.assertIn("spring.spring *= springScale / Mathf.Sqrt(travelScale)", MOTOR)
        self.assertIn("spring.damper /= Mathf.Sqrt(travelScale)", MOTOR)

    def test_tire_feedback_preserves_wheel_authority(self):
        section = MOTOR.split("private void FixedUpdate()", 1)[1].split(
            "private void ApplyAckermannSteering(", 1)[0]
        self.assertIn("previousTyreSlip", section)
        self.assertIn("float slipBlend =", section)
        self.assertIn("float steeringRateScale =", section)
        self.assertIn("Mathf.Lerp(1f, .82f, slipBlend)", section)
        self.assertIn("Mathf.Lerp(1f, .63f, speedBlend)", section)
        self.assertIn("ApplyAckermannSteering(smoothSteer)", section)
        self.assertNotIn("body.linearVelocity =", section)
        for speed in (0, 10, 25, 100):
            speed01 = max(0, min(1, (speed - 10) / 15))
            steer_rate = 1 - .37 * speed01
            self.assertGreaterEqual(steer_rate, .63)
            self.assertLessEqual(steer_rate, 1)

    def test_shared_pbr_textures_no_per_frame_allocations(self):
        method = WORLD.split("private Material AuthoredUrpMaterial(", 1)[1].split(
            "private void ApplyAuthoredMaterials(", 1)[0]
        self.assertIn("authoredMaterialCache[source] = polished;", method)
        self.assertIn("new Material(source)", method)
        self.assertIn("sourceLabel.Contains(\"body_pbr\")", method)
        self.assertIn("sourceLabel.Contains(\"aarisglass\")", method)
        self.assertIn("sourceLabel.Contains(\"aarispaint\")", method)
        self.assertIn("polished.EnableKeyword(\"_CLEARCOAT\")", method)
        self.assertIn("if (!dedicatedPaint && !mixedBodyAtlas && !separateGlass)", method)
        self.assertNotIn("renderer.material", method)
        for src in ("_BaseMap", "_BumpMap", "_MetallicGlossMap"):
            self.assertIn(src, method)
        self.assertIn("foreach (Material converted in authoredMaterialCache.Values)", WORLD)
        self.assertIn("paletteMaterial.SetFloat(\"_Metallic\", .24f);", WORLD)
        self.assertIn("wheelPaletteMaterial.SetFloat(\"_Smoothness\", .18f);", WORLD)

    def test_camera_uses_velocity_but_keeps_wall_sweep(self):
        camera = SESSION.split("private void FollowCarCamera()", 1)[1].split(
            "private void ClearHeldDrivingInput()", 1)[0]
        self.assertIn("car.DrivingVelocityWorld", camera)
        self.assertIn("Vector3.ClampMagnitude(planarTravel * .035f, .75f)", camera)
        self.assertIn("Physics.SphereCastNonAlloc", camera)
        self.assertIn("followCamera.transform.position = next;", camera)
        self.assertIn("Vector3.SmoothDamp(", camera)
        self.assertNotIn("new Material(", camera)


if __name__ == "__main__":
    unittest.main()
