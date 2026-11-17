#!/usr/bin/env python3
"""Prevent saturated vehicle pools from hiding nearby parked cars.

A source-level contract plus a small deterministic priority model. Real
Unity WheelCollider, streamed transforms and Android frame timing remain
outside this test and require native playtesting.
"""
import math
import unittest
from pathlib import Path

WORLD = (Path(__file__).resolve().parents[2] /
         "Assets/AarisWorld/Scripts/AarisVehicleWorld.cs").read_text("utf-8")


def pick_parked_victim(cars, new_distance):
    """Mirror the distance hysteresis and immutable parked-only priority."""
    threshold = (new_distance + 20.0) ** 2
    candidate = None
    for i, (distance, parked) in enumerate(cars):
        if not parked or distance ** 2 <= threshold:
            continue
        threshold = distance ** 2
        candidate = i
    return candidate


class VehicleStreamingPriority(unittest.TestCase):
    def test_pool_full_has_bounded_parked_car_replacement(self):
        update = WORLD.split("private void Update()", 1)[1].split(
            "private AarisVehicleRecord FarthestReplaceableParkedCar(", 1)[0]
        self.assertIn("if (active >= MaximumNearbyVehicles)", update)
        self.assertIn("if (unloaded >= unloadBudgetPerPoll) break;", update)
        self.assertIn("FarthestReplaceableParkedCar(", update)
        self.assertIn("if (parkedVictim == null) continue;", update)
        self.assertIn("spawnProbes >= spawnProbeBudgetPerPoll", update)
        self.assertIn("built >= spawnBudgetPerPoll", update)
        self.assertIn("active--;", update)
        self.assertIn("active++;", update)

    def test_validated_placement_precedes_eviction(self):
        update = WORLD.split("private void Update()", 1)[1].split(
            "private AarisVehicleRecord FarthestReplaceableParkedCar(", 1)[0]
        self.assertLess(update.index("!TryResolvePlanned(record, out Vector3 pos)"),
                        update.index("Destroy(retired.gameObject);"))
        self.assertIn("if (retired == null || !retired.CanPark) continue;", update)
        self.assertIn("parkedVictim.position = retired.transform.position;", update)
        self.assertIn("parkedVictim.rotation = retired.transform.rotation;", update)
        self.assertIn("parkedVictim.nextProbe = now + 3.0f;", update)

    def test_hysteresis_and_occupied_safety(self):
        helper = WORLD.split("private AarisVehicleRecord FarthestReplaceableParkedCar(", 1)[1].split(
            "private void OnDestroy()", 1)[0]
        self.assertIn("const float PriorityMarginMetres = 20f;", helper)
        self.assertIn("if (car == null || !car.CanPark) continue;", helper)
        self.assertIn("distanceSq <= farthestDistanceSq", helper)
        self.assertEqual(pick_parked_victim([(135, True), (80, True)], 35), 0)
        self.assertIsNone(pick_parked_victim([(70, True), (100, False)], 65))
        self.assertIsNone(pick_parked_victim([(10, True), (40, True)], 25))
        self.assertEqual(pick_parked_victim([(135, True), (150, True)], 20), 1)
        self.assertIsNone(pick_parked_victim([(150, False)], 20))


if __name__ == "__main__":
    unittest.main()
