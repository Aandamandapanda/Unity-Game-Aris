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
        self.assertIn("if (parkedVictim == null) break;", update)
        self.assertIn("spawnProbes < spawnProbeBudgetPerPoll", update)
        self.assertIn("built < spawnBudgetPerPoll", update)
        self.assertIn("active--;", update)
        self.assertIn("active++;", update)

    def test_validated_placement_precedes_eviction(self):
        update = WORLD.split("private void Update()", 1)[1].split(
            "private AarisVehicleRecord FarthestReplaceableParkedCar(", 1)[0]
        self.assertLess(update.index("!TryResolvePlanned(record, out Vector3 pos)"),
                        update.index("Destroy(retired.gameObject);"))
        self.assertIn("if (retired == null || !retired.CanPark)", update)
        self.assertIn("record.nextProbe = now + .3f;", update)
        self.assertIn("parkedVictim.position = retired.transform.position;", update)
        self.assertIn("parkedVictim.rotation = retired.transform.rotation;", update)
        self.assertIn("parkedVictim.nextProbe = now + 3.0f;", update)

    def test_authored_car_pbr_conversion_is_released_on_scene_teardown(self):
        # Runtime materials are shared for streamed cars and must not leak
        # when the level reloads. Imported shared source assets remain alive.
        cleanup = WORLD.split("private void OnDestroy()", 1)[1].split(
            "public AarisVehicleMotor NearestAvailable(", 1)[0]
        self.assertIn("foreach (Material converted in authoredMaterialCache.Values)", cleanup)
        self.assertIn("if (converted != null) Destroy(converted);", cleanup)
        self.assertIn("authoredMaterialCache.Clear();", cleanup)

    def test_nearest_ready_vehicle_wins_regardless_of_match_plan_order(self):
        update = WORLD.split("private void Update()", 1)[1].split(
            "private AarisVehicleRecord FarthestReplaceableParkedCar(", 1)[0]
        self.assertIn("float nearestDistanceSq = LoadRadius * LoadRadius;", update)
        self.assertIn("foreach (var pending in plans)", update)
        self.assertIn("pending.visible != null || now < pending.nextProbe", update)
        self.assertIn("!authoredBodyResolved[pendingType]", update)
        self.assertIn("(distanceSq == nearestDistanceSq && record != null)", update)
        self.assertIn("record = pending;", update)
        self.assertIn("if (record == null) break;", update)
        self.assertLess(update.index("record = pending;"),
                        update.index("TryResolvePlanned(record, out Vector3 pos)"))
        def nearest(records):
            best, found = 105 ** 2, None
            for index, (distance, ready) in enumerate(records):
                distance_squared = distance ** 2
                if not ready or distance_squared > best or (
                    distance_squared == best and found is not None
                ):
                    continue
                best, found = distance_squared, index
            return found
        self.assertEqual(nearest([(94, True), (12, True), (51, True)]), 1)
        self.assertEqual(nearest([(4, False), (32, True), (10, False)]), 1)
        self.assertEqual(nearest([(27, True), (27, True), (30, True)]), 0)
        self.assertIsNone(nearest([(106, True), (200, True)]))

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
