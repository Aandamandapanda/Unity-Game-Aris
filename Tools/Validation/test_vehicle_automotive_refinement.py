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

    def test_mass_aware_roll_and_no_airborne_suction(self):
        motor = MOTOR
        configure = motor.split("public void Configure(string id,", 1)[1].split(
            "private static float SafeHandlingScale(", 1)[0]
        antiroll = motor.split("private void ApplyAntiRoll(", 1)[1].split(
            "private void OnDisable()", 1)[0]
        step = motor.split("private void FixedUpdate()", 1)[1].split(
            "private void ApplyAckermannSteering(", 1)[0]
        self.assertIn("AntiRollStrength = 4800f", motor)
        self.assertIn("antiRollMassScale = Mathf.Clamp(", configure)
        self.assertIn("Mathf.Sqrt(body.mass / 1250f), .85f, 1.24f", configure)
        self.assertIn("groundedWheelCount = 0;", step)
        self.assertEqual(antiroll.count("groundedWheelCount++;"), 2)
        self.assertEqual(antiroll.count("GetGroundHit(out WheelHit"), 2)
        self.assertIn("effectiveAntiRoll = AntiRollStrength * antiRollMassScale", antiroll)
        self.assertIn("if (planarSpeed > 6f && groundedWheelCount >= 2)", step)
        self.assertNotIn("Physics.Raycast(", step)
        for mass in (850, 1050, 1250, 1650, 1950, 2300):
            factor = max(.85, min(1.24, math.sqrt(mass / 1250)))
            self.assertTrue(.85 <= factor <= 1.24)
            self.assertTrue(math.isfinite(4800 * factor))
        for velocity in (0, 6, 12, 28):
            for grounded in range(5):
                applies = velocity > 6 and grounded >= 2
                if grounded == 0:
                    self.assertFalse(applies)

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

    def test_service_brakes_bias_front_only_while_rolling(self):
        apply = MOTOR.split("private void ApplyWheelForces(", 1)[1].split(
            "private static float ContinuousDriveTorque(", 1)[0]
        self.assertIn("driver != null && body != null", apply)
        self.assertIn("brake >= 500f", apply)
        self.assertIn("brake < ParkingBrakeTorque", apply)
        self.assertIn("body.linearVelocity.sqrMagnitude > 4f", apply)
        self.assertIn("frontBiased != appliedServiceBrakeBias", apply)
        self.assertIn("i < 2 ? 1.12f : .88f", apply)
        self.assertIn("appliedServiceBrakeBias = frontBiased", apply)
        self.assertIn("frontBiased ? (i < 2 ? 1.12f : .88f) : 1f", apply)
        for brake in (500, 1200, 3200):
            total = 2 * brake * 1.12 + 2 * brake * .88
            self.assertAlmostEqual(total, 4 * brake)
        self.assertIn("appliedServiceBrakeBias = false;", MOTOR)

    def test_paint_keeps_imported_surface_tone_and_clearcoat_keyword(self):
        method = WORLD.split("private Material AuthoredUrpMaterial(", 1)[1].split(
            "private void ApplyAuthoredMaterials(", 1)[0]
        body = WORLD.split("private static void PaintVerifiedBodySlots(", 1)[1].split(
            "private void FallBackToVerifiedModel(", 1)[0]
        build = WORLD.split("private bool Build(", 1)[1].split(
            "private void Update()", 1)[0]
        self.assertIn('sourceLabel.Contains("aarisciviliansurface")', method)
        self.assertIn('polished.EnableKeyword("_CLEARCOAT")', method)
        self.assertIn("record.type.preserveAuthoredUrpMaterials)", build)
        self.assertIn("ApplyAuthoredMaterials(renderers);", build)
        self.assertIn('material.GetColor("_BaseColor")', body)
        for field in ("original.r * paintFactor.r",
                      "original.g * paintFactor.g",
                      "original.b * paintFactor.b",
                      "original.a"):
            self.assertIn(field, body)
        self.assertNotIn("new Material(", body)
        self.assertNotIn("renderer.material", body)

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
